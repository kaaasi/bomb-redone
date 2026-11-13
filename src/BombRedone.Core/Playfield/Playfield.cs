using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>
/// Playfield of a round: screen and save buffer as palette indices, palette, wall and tanks. The rules of the original
/// read the screen back (falling tanks, later hits and craters), so the core keeps and draws the pixels itself
/// (Review #104 P2-2); texts drawn with BGI fonts (wall label, names, header) are added by the UI and never read.
/// </summary>
/// <remarks>
/// [ORIGINAL] O8:0984 (BOMB.OVR 0x1CA44), docs/analyse/runde.md section 3; checked against the original code run in the
/// probe <c>tools/reverse-engineering/feld.py</c> (docs/analyse/feld.md).
/// </remarks>
public sealed class Playfield
{
    /// <summary>Colour of the frames (13) and of the terrain (2), the two colours a tank stands on (O8:0964 = [2, 13]).</summary>
    public const byte FrameColor = 13;

    public const byte TerrainColor = 2;

    /// <summary>Player n is drawn in colour 19 + n (O2:4FB6, O10:01AC).</summary>
    public const int PlayerColorBase = 19;

    /// <summary>Colour of the shot (19): white, black with invisible shots (O8:09FF).</summary>
    public const int ShotColor = 19;

    /// <summary>
    /// [ORIGINAL] Words +60h/+62h/+64h of the records as set by O2:0216 (BOMB.OVR 0x00929–0x00A76): DAC 19 + n =
    /// (63 · e div 100) div r, … for energy e (O10:01AC). Same table as UI PlayerSelectScreen.ColorDivisors.
    /// </summary>
    public static readonly IReadOnlyList<(int R, int G, int B)> ColorDivisors =
    [
        (1, 0x7FFF, 0x7FFF), (0x7FFF, 1, 0x7FFF), (0x7FFF, 0x7FFF, 1), (1, 1, 0x7FFF), (0x7FFF, 1, 1),
        (1, 0x7FFF, 1), (1, 1, 1), (1, 2, 0x7FFF), (1, 0x7FFF, 2), (1, 2, 2),
    ];

    /// <summary>[ORIGINAL] Wall factors DS:47D2 (O8:0B21, 0B55, 0B89): 0.707106, 1, 1.41421.</summary>
    public static readonly Real48 StickyFactor = Real48.FromHex("80CE18E60435");

    public static readonly Real48 ElasticFactor = Real48.FromHex("810000000000");

    public static readonly Real48 AccelerateFactor = Real48.FromHex("81D751D50435");

    // [ORIGINAL] Terrain thresholds O8:0EA6 / O8:0EC4 (Real48 constants, slightly above 0.2 and 0.1).
    private static readonly Real48 SlopeChange = Real48.FromHex("7ECDCCCCCC4C");
    private static readonly Real48 DirectionChange = Real48.FromHex("7DCDCCCCCC4C");

    private readonly List<PlayfieldTank> _tanks = [];

    private Playfield(PlayfieldOptions options) => Options = options;

    public PlayfieldOptions Options { get; }

    /// <summary>Screen (segment A000h) without the BGI texts.</summary>
    public IndexBuffer Screen { get; } = new();

    /// <summary>Save buffer DS:E5C0: background and frames, from which craters restore the screen.</summary>
    public IndexBuffer Background { get; } = new();

    /// <summary>DAC; <see cref="VgaPalette.IsWritten"/> marks the entries the set-up wrote.</summary>
    public VgaPalette Palette { get; } = new();

    /// <summary>Wall of the round DS:47EC.</summary>
    public WallKind Wall { get; private set; }

    /// <summary>DS:4814: the wall is Sticky, Elastic or Accelerate.</summary>
    public bool WallBounces => Wall is WallKind.Sticky or WallKind.Elastic or WallKind.Accelerate;

    /// <summary>DS:4815: the wall is Warp.</summary>
    public bool WallWarps => Wall == WallKind.Warp;

    /// <summary>Wall factor DS:47D2 for Sticky, Elastic and Accelerate; null for None and Warp (left unchanged, P7).</summary>
    public Real48? WallFactor => Wall switch
    {
        WallKind.Sticky => StickyFactor,
        WallKind.Elastic => ElasticFactor,
        WallKind.Accelerate => AccelerateFactor,
        _ => null,
    };

    /// <summary>Number of the background drawn (0..8).</summary>
    public int BackgroundNumber { get; private set; }

    /// <summary>Random:Real &lt; 0.5 after the background: the palette rotation runs backwards (0C79:0428, [APPROX] meaning).</summary>
    public bool RotationReversed { get; private set; }

    /// <summary>Tanks of players 1..N.</summary>
    public IReadOnlyList<PlayfieldTank> Tanks => _tanks;

    /// <summary>
    /// Builds the playfield like O8:0984. <paramref name="random"/> holds RandSeed after the shuffle of the records
    /// (docs/analyse/runde.md section 4 no. 2–5) and is advanced exactly as in the original.
    /// </summary>
    public static Playfield Build(PlayfieldOptions options, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        options.Validate();
        var field = new Playfield(options);

        // O8:09A6: wall of the round.
        field.Wall = (WallKind)(options.Wall == PlayfieldOptions.RandomWall ? random.Next(5) : options.Wall);

        // O8:09FF: shot colour, then ClearDevice (0E26:0F80) clears the screen to 0.
        field.Palette.Set(ShotColor, 63, 63, 63);
        if (options.InvisibleShots)
            field.Palette.Set(ShotColor, 0, 0, 0);
        field.Screen.Clear();

        // O8:0A2D: background; RND = Random(9), USER = Random(9) until its bit is in the mask.
        var number = options.Background;
        if (number == PlayfieldOptions.RandomBackground)
        {
            number = random.Next(9);
        }
        else if (number == PlayfieldOptions.UserBackground)
        {
            do
            {
                number = random.Next(9);
            }
            while (((1 << number) & options.BackgroundMask) <= 0);
        }

        field.BackgroundNumber = number;
        field.RotationReversed = Backgrounds.Draw(number, field.Screen, field.Background, field.Palette, random);

        // O8:0A7D: colour 0, frames with BGI Rectangle in colour 13 (same pixels as UCC8:0031).
        field.Palette.Set(0, 14, 9, 10);
        var screen = field.Screen.Pixels.AsSpan();
        VgaDraw.Rectangle(screen, 8, 28, 312, 192, FrameColor);
        VgaDraw.Rectangle(screen, 10, 30, 310, 190, FrameColor);

        field.DrawTerrain(random);
        field.PlaceTanks(random);
        return field;
    }

    /// <summary>
    /// [ORIGINAL] O8:0BEC–0F3A (docs/analyse/runde.md section 3.5): columns x = 11..309 from 190 − h to 189 in colour 2;
    /// flat margins of 60 − 7 (N − 2) columns, inside slope s (−4..4) with direction d, two Random:Real per step.
    /// The margins use BGI Line, the inside UCC8:09E4; both give the same vertical run.
    /// </summary>
    private void DrawTerrain(TurboPascalRandom random)
    {
        var screen = Screen.Pixels.AsSpan();
        var x = 11;
        var slope = 0;
        var direction = 1 - Real48.FromInt32(random.Next(2) * 2).Trunc();
        var margin = 60 - (Options.Players - 2) * 7;
        var height = random.Next(150);

        while (x < 310)
        {
            if (margin + 11 > x || 309 - margin < x)
            {
                Column(screen, x, height);
            }
            else
            {
                var sign = slope > 0 ? 1 : -1;
                switch (Math.Abs(slope))
                {
                    case 0:
                        Column(screen, x, height);
                        x++;
                        Column(screen, x, height);
                        x++;
                        Column(screen, x, height);
                        break;
                    case 1:
                        Column(screen, x, height);
                        x++;
                        Column(screen, x, height);
                        x++;
                        height += sign;
                        Column(screen, x, height);
                        break;
                    case 2:
                        Column(screen, x, height);
                        x++;
                        height += sign;
                        Column(screen, x, height);
                        x++;
                        height += sign;
                        Column(screen, x, height);
                        break;
                    case 3:
                        Column(screen, x, height);
                        x++;
                        height += sign;
                        Column(screen, x, height);
                        height += sign;
                        break;
                    case 4:
                        Column(screen, x, height);
                        height += sign;
                        height += sign;
                        break;
                }

                if (random.NextReal() < SlopeChange)
                    slope += direction;
                if (random.NextReal() < DirectionChange || Math.Abs(slope) > 4)
                    direction = -direction;
                slope = Math.Clamp(slope, -4, 4);
                if (height <= 20)
                    direction = 1;
                if (height >= 140)
                    direction = -1;
                if (height <= 3)
                    slope = 1;
                if (height >= 155)
                    slope = -1;
            }

            x++;
        }

        static void Column(Span<byte> screen, int x, int height) =>
            VgaDraw.Line(screen, x, 190 - height, x, 189, TerrainColor);
    }

    /// <summary>[ORIGINAL] O8:0F3D–1251 (docs/analyse/runde.md section 3.6, P11–P13).</summary>
    private void PlaceTanks(TurboPascalRandom random)
    {
        var n = Options.Players;
        for (var i = 1; i <= n; i++)
            _tanks.Add(new PlayfieldTank(i));

        _tanks[0].X = random.Next(28) + 16;
        _tanks[n - 1].X = 305 - random.Next(28);
        var spacing = (_tanks[n - 1].X - _tanks[0].X) / (n - 1);
        for (var i = 2; i <= n - 1; i++)
            _tanks[i - 1].X = (i - 1) * spacing + _tanks[0].X;

        var screen = Screen.Pixels.AsSpan();
        foreach (var tank in _tanks)
        {
            tank.Angle = tank.Number == 1 || tank.Number == n ? 75 : 90;
            tank.Power = 200;
            tank.Direction = tank.Number == n ? -1 : 1;
            SetEnergy(tank, 100);
            tank.Active = true;
            tank.Y = 32;
            tank.Field5E = 0;
            UpdateColor(tank);

            // O8:1118: the tank falls (without being drawn at y = 32) until all nine pixels below it are terrain or frame.
            if (!Supported(tank))
            {
                do
                {
                    VgaDraw.CopyTankBody(Background.Pixels, screen, tank.X, tank.Y);
                    tank.Y++;
                    VgaDraw.TankBody(screen, tank.X, tank.Y, tank.Color);
                }
                while (!FullySupported(tank));
            }

            DrawTank(tank);
            RedrawFrame();
        }
    }

    /// <summary>Energy and colour. [ORIGINAL] O10:0171 (BOMB.OVR 0x1F294): +21h := e, then O10:014A.</summary>
    private void SetEnergy(PlayfieldTank tank, int energy)
    {
        tank.Energy = energy;
        UpdateColor(tank);
    }

    /// <summary>
    /// [ORIGINAL] O10:01AC (BOMB.OVR 0x1F2CF): v = 63 · e div 100; DAC 19 + n = (v div r, v div g, v div b) for e &gt; 0,
    /// otherwise black.
    /// </summary>
    private void UpdateColor(PlayfieldTank tank)
    {
        var v = (short)(63 * tank.Energy) / 100;
        if (tank.Energy > 0)
        {
            var (r, g, b) = ColorDivisors[tank.Number - 1];
            Palette.Set(tank.Color, v / r, v / g, v / b);
        }
        else
        {
            Palette.Set(tank.Color, 0, 0, 0);
        }
    }

    private bool IsGround(int x, int y) => Screen[x, y] is TerrainColor or FrameColor;

    private bool Supported(PlayfieldTank tank)
    {
        for (var dx = -4; dx <= 4; dx++)
        {
            if (IsGround(tank.X + dx, tank.Y + 1))
                return true;
        }

        return false;
    }

    private bool FullySupported(PlayfieldTank tank)
    {
        for (var dx = -4; dx <= 4; dx++)
        {
            if (!IsGround(tank.X + dx, tank.Y + 1))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Tank with barrel. [ORIGINAL] U431:07BA (BOMB.EXE 0x0616A): only for an active tank; colour (O10:014A), body
    /// U431:05ED, barrel UCC8:09E4 from (x, y) to (x + dx · d, y − dy) with <see cref="BarrelOffset"/>.
    /// </summary>
    public void DrawTank(PlayfieldTank tank)
    {
        ArgumentNullException.ThrowIfNull(tank);
        if (!tank.Active)
            return;
        UpdateColor(tank);
        var screen = Screen.Pixels.AsSpan();
        VgaDraw.TankBody(screen, tank.X, tank.Y, tank.Color);
        var (dx, dy) = BarrelOffset(tank.Angle);
        VgaDraw.Line(screen, tank.X, tank.Y, tank.X + dx * tank.Direction, tank.Y - dy, tank.Color);
    }

    /// <summary>
    /// End of the barrel relative to (x, y) for angle 0..180°: (Trunc(8 cos a), Trunc(8 sin a)) as the Real48 runtime
    /// computes it (0DBD:00D9 degrees to radians, Cos 12CE:12EE, Sin 12CE:1301, Mul, Trunc).
    /// </summary>
    /// <remarks>
    /// [ORIGINAL] Measured table: <c>python3 -I tools/reverse-engineering/feld.py barrel</c> runs U431:07BA for every
    /// angle. The runtime's results differ from exact trigonometry (cos 0° gives 7, sin 30° gives 4, cos 60° gives 3);
    /// spur <c>schuss</c> ports Sin/Cos to Real48 (docs/arbeitspakete.md), after which this table becomes a test.
    /// </remarks>
    public static (int Dx, int Dy) BarrelOffset(int angle)
    {
        if ((uint)angle >= (uint)BarrelOffsets.Length)
            throw new ArgumentOutOfRangeException(nameof(angle));
        return BarrelOffsets[angle];
    }

    private static readonly (int Dx, int Dy)[] BarrelOffsets =
    [
        (7, 0), (7, 0), (7, 0), (7, 0), (7, 0), (7, 0), (7, 0), (7, 0), (7, 1), (7, 1),
        (7, 1), (7, 1), (7, 1), (7, 1), (7, 1), (7, 2), (7, 2), (7, 2), (7, 2), (7, 2),
        (7, 2), (7, 2), (7, 2), (7, 3), (7, 3), (7, 3), (7, 3), (7, 3), (7, 3), (6, 3),
        (6, 4), (6, 4), (6, 4), (6, 4), (6, 4), (6, 4), (6, 4), (6, 4), (6, 4), (6, 5),
        (6, 5), (6, 5), (5, 5), (5, 5), (5, 5), (5, 5), (5, 5), (5, 5), (5, 5), (5, 6),
        (5, 6), (5, 6), (4, 6), (4, 6), (4, 6), (4, 6), (4, 6), (4, 6), (4, 6), (4, 6),
        (3, 6), (3, 6), (3, 7), (3, 7), (3, 7), (3, 7), (3, 7), (3, 7), (2, 7), (2, 7),
        (2, 7), (2, 7), (2, 7), (2, 7), (2, 7), (2, 7), (1, 7), (1, 7), (1, 7), (1, 7),
        (1, 7), (1, 7), (1, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7),
        (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (0, 7), (-1, 7), (-1, 7),
        (-1, 7), (-1, 7), (-1, 7), (-1, 7), (-1, 7), (-2, 7), (-2, 7), (-2, 7), (-2, 7), (-2, 7),
        (-2, 7), (-2, 7), (-2, 7), (-3, 7), (-3, 7), (-3, 7), (-3, 7), (-3, 7), (-3, 7), (-3, 6),
        (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-4, 6), (-5, 6),
        (-5, 6), (-5, 6), (-5, 5), (-5, 5), (-5, 5), (-5, 5), (-5, 5), (-5, 5), (-5, 5), (-6, 5),
        (-6, 5), (-6, 5), (-6, 4), (-6, 4), (-6, 4), (-6, 4), (-6, 4), (-6, 4), (-6, 4), (-6, 4),
        (-6, 4), (-6, 3), (-7, 3), (-7, 3), (-7, 3), (-7, 3), (-7, 3), (-7, 3), (-7, 2), (-7, 2),
        (-7, 2), (-7, 2), (-7, 2), (-7, 2), (-7, 2), (-7, 2), (-7, 1), (-7, 1), (-7, 1), (-7, 1),
        (-7, 1), (-7, 1), (-7, 1), (-7, 0), (-7, 0), (-7, 0), (-7, 0), (-7, 0), (-7, 0), (-7, 0),
        (-7, 0),
    ];

    /// <summary>y of the topmost terrain pixel (colour 2) in column <paramref name="x"/> above the floor, or 190 if none.</summary>
    public static int TerrainTop(Playfield field, int x)
    {
        ArgumentNullException.ThrowIfNull(field);
        for (var y = 31; y <= 189; y++)
        {
            if (field.Screen[x, y] == TerrainColor)
                return y;
        }

        return 190;
    }

    /// <summary>
    /// [ORIGINAL] O7:0364 (BOMB.OVR 0x1ACFA): both frames UCC8:0031 in colour 13; for a wall not in [1, 2, 3] (O7:0344)
    /// the floor line (11, 189)–(309, 189) in colour 2.
    /// </summary>
    public void RedrawFrame()
    {
        var screen = Screen.Pixels.AsSpan();
        VgaDraw.Rectangle(screen, 8, 28, 312, 192, FrameColor);
        VgaDraw.Rectangle(screen, 10, 30, 310, 190, FrameColor);
        if (!WallBounces)
            VgaDraw.Line(screen, 11, 189, 309, 189, TerrainColor);
    }
}
