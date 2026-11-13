using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>
/// What the round does right after the weapon (docs/analyse/runde.md sections 5.8 and 5.9 K11): all tanks anew, landslide
/// with Crumble, falling tanks. Bit-identical to the probe (<c>feld_schuss.py post</c>, AfterShotProbeTests).
/// </summary>
public static class AfterShot
{
    // [ORIGINAL] O7:03DC: 100 (Real48 87 00 00 00 00 48).
    private static readonly Real48 Hundred = Real48.FromHex("870000000048");

    /// <summary>
    /// [ORIGINAL] O4:49CB (BOMB.OVR 0x156D0): U431:07BA for every record, Crumble (DS:E8A4) &gt; 0 → O7:03C1, FallDown
    /// (DS:E8B8) → O7:0020. The shot protocol and the death loop that follow belong to the turn (round 3).
    /// </summary>
    public static void Run(Playfield.Playfield field, int crumble, bool fallDown, TurboPascalRandom random,
        Action<ShotFrame>? frame = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(random);
        foreach (var tank in field.Tanks)
            field.DrawTank(tank);
        if (crumble > 0)
            Crumble(field, crumble, random, frame);
        if (fallDown)
            Fall(field, random, frame);
    }

    /// <summary>
    /// [ORIGINAL] O7:03C1 (BOMB.OVR 0x1AD57): frames anew (O7:0364), then Random:Real · 100 ≤ Crumble → landslide.
    /// </summary>
    public static void Crumble(Playfield.Playfield field, int crumble, TurboPascalRandom random, Action<ShotFrame>? frame = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(random);
        field.RedrawFrame();
        if (Real48.Compare(random.NextReal() * Hundred, Real48.FromInt32(crumble)) <= 0)
        {
            Landslide(field.Screen.Pixels, field.Background.Pixels);
            frame?.Invoke(ShotFrame.Landslide);
        }
    }

    /// <summary>
    /// Landslide. [ORIGINAL] U431:0496 (BOMB.EXE 0x05E46): scan the screen backwards from address 64000 for colour 2; a
    /// pixel with "air" below (0 or 30..99) moves down at once (below := 2, itself from the background buffer) and its new
    /// address goes into the list. Then passes over the list: each entry moves one row further while there is air below,
    /// otherwise it leaves the list; until the list is empty. No Random, no delay.
    /// </summary>
    /// <remarks>
    /// [APPROX] Reads beyond the 64000 screen bytes give 0 and writes there are dropped; the original reaches the rest of
    /// the video segment only for terrain in the last row, which the playfield never has.
    /// </remarks>
    public static void Landslide(Span<byte> screen, ReadOnlySpan<byte> background)
    {
        var list = new List<int>();
        int di = IndexBuffer.Size, count = IndexBuffer.Size;
        while (true)
        {
            // repne scasb with the direction flag set: compare, then di - 1, until equal or count = 0.
            while (count > 0)
            {
                count--;
                var value = Read(screen, di);
                di--;
                if (value == Playfield.Playfield.TerrainColor)
                    break;
            }

            if (di == 0)
                break;
            if (IsAir(Read(screen, di + IndexBuffer.Width + 1)))
            {
                var at = di + 1;
                list.Add(at + IndexBuffer.Width);
                Write(screen, at + IndexBuffer.Width, Playfield.Playfield.TerrainColor);
                Write(screen, at, background[at]);
            }
        }

        var start = 0;
        while (true)
        {
            while (start < list.Count && list[start] == 0)
                start++;
            if (start == list.Count)
                return;
            for (var j = start; j < list.Count; j++)
            {
                var at = list[j];
                if (at == 0)
                    continue;
                if (IsAir(Read(screen, at + IndexBuffer.Width)))
                {
                    Write(screen, at + IndexBuffer.Width, Playfield.Playfield.TerrainColor);
                    Write(screen, at, background[at]);
                    list[j] = at + IndexBuffer.Width;
                }
                else
                {
                    list[j] = 0;
                }
            }
        }
    }

    /// <summary>
    /// Falling tanks. [ORIGINAL] O7:0020 (BOMB.OVR 0x1A9B6): frames anew; a tank stands on colour 2 or 13 (set O7:0000) at one of
    /// (x − 3 .. x + 3, y + 1) or at both (x ± 4, y + 1). Otherwise the barrel is removed, then per row: body drawn, colour,
    /// y + 1, slide away from a solid (x ± 4, y + 1), done when all nine pixels below are solid, Random(3) + 1 damage on
    /// inertia (O10:0000 with defence 1), Delay(FS), old body from the buffer. Every tank is drawn anew (U431:07BA).
    /// </summary>
    public static void Fall(Playfield.Playfield field, TurboPascalRandom random, Action<ShotFrame>? frame = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(random);
        field.RedrawFrame();
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        foreach (var tank in field.Tanks)
        {
            var falls = tank.Active;
            for (var dx = -3; dx <= 3; dx++)
            {
                if (IsSolid(screen, tank.X + dx, tank.Y + 1))
                    falls = false;
            }

            if (IsSolid(screen, tank.X + 4, tank.Y + 1) && IsSolid(screen, tank.X - 4, tank.Y + 1))
                falls = false;
            if (falls)
            {
                var (bx, by) = Playfield.Playfield.BarrelOffset(tank.Angle);
                VgaDraw.CopyLine(background, screen, tank.X, tank.Y, tank.X + bx * tank.Direction, tank.Y - by);
                bool landed;
                do
                {
                    int ox = tank.X, oy = tank.Y;
                    VgaDraw.TankBody(screen, tank.X, tank.Y, tank.Color);
                    field.UpdateColor(tank);
                    landed = true;
                    tank.Y++;
                    if (IsSolid(screen, tank.X + 4, tank.Y + 1))
                        tank.X--;
                    if (IsSolid(screen, tank.X - 4, tank.Y + 1))
                        tank.X++;
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        if (!IsSolid(screen, tank.X + dx, tank.Y + 1))
                            landed = false;
                    }

                    Damage.Apply(field, tank, random.Next(3) + 1, Damage.Inertia, random);
                    frame?.Invoke(ShotFrame.Fall);
                    VgaDraw.CopyTankBody(background, screen, ox, oy);
                }
                while (!landed);
            }

            field.DrawTank(tank);
        }
    }

    private static bool IsSolid(ReadOnlySpan<byte> screen, int x, int y) =>
        Read(screen, (ushort)VgaDraw.Address(x, y)) is Playfield.Playfield.TerrainColor or Playfield.Playfield.FrameColor;

    private static bool IsAir(byte value) => value == 0 || value is >= 30 and <= 99;

    private static byte Read(ReadOnlySpan<byte> screen, int address) =>
        address is >= 0 and < IndexBuffer.Size ? screen[address] : (byte)0;

    private static void Write(Span<byte> screen, int address, byte value)
    {
        if (address is >= 0 and < IndexBuffer.Size)
            screen[address] = value;
    }
}
