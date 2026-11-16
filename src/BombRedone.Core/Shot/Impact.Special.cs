using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>CRI and Sonic Blaster in O6:00E0 (docs/analyse/runde.md section 5.7 K5, K6).</summary>
public static partial class Impact
{
    /// <summary>Number of entries of the branch list DS:E820 of the CRI (index 1..20 used).</summary>
    internal const int CriListLength = 21;

    /// <summary>
    /// Whether <paramref name="weapon"/> is a CRI. [ORIGINAL] set O6:0020 = {5..10}; 5 (laser) never reaches O6 and 7
    /// (MIRV) is taken by the explosives first, so {6, 8, 9, 10} remain.
    /// </summary>
    public static bool IsCri(int weapon) => weapon is 6 or 8 or 9 or 10;

    /// <summary>
    /// Branches DS:47D8 of the CRI in the terrain. [ORIGINAL] O6:0942–0999 (BOMB.OVR 0x1986E): 4 (5, 6), 8 (7, 8), 20 (9, 10).
    /// </summary>
    public static int CriBranches(int weapon) => weapon switch
    {
        5 or 6 => 4,
        7 or 8 => 8,
        9 or 10 => 20,
        _ => 0,
    };

    /// <summary>[ORIGINAL] set O6:0060 = {6, 8, 10}: these CRIs also move diagonally.</summary>
    private static bool CriDiagonal(int weapon) => weapon is 6 or 8 or 10;

    /// <summary>
    /// CRI. [ORIGINAL] O6:084E–0E47 (BOMB.OVR 0x1977A). Tank colour 20..29: Random(6 · weapon) + 1 damage (shield first),
    /// 10 points per energy lost for another team, siren 1000 → 30000 → 1000 Hz with sound (no Random). Terrain colour 2:
    /// start at (Trunc(x), Trunc(y)) and move down until one of the four neighbours is terrain; every branch starts there.
    /// Then passes until every branch is dead: per branch the directions with terrain (1 up, 3 right, 5 down, 7 left, the
    /// diagonals 2, 4, 6, 8 only for {6, 8, 10}) in this order, Random(n) + 1 picks one, the branch moves there and the
    /// pixel comes from the background buffer (with sound Random(500)); a branch without terrain dies (x = 0). After the
    /// pass every dead branch takes the place of the first living entry 1..20 other than itself (all 20 entries of the list
    /// DS:E820, also those beyond the branch count); if all branches were dead at the start of that check, the CRI ends.
    /// Other colours: nothing.
    /// </summary>
    /// <remarks>
    /// [APPROX] The list DS:E820 is a global of the original and keeps the branches of earlier CRIs for the whole game;
    /// here it lives in the playfield, so it starts empty in every round. [APPROX] Reads below the screen give 0 and
    /// writes there are dropped (the original reads and writes the rest of the video segment).
    /// </remarks>
    private static void Cri(Playfield.Playfield field, PlayfieldTank shooter, int color, Real48 x, Real48 y,
        ShotSettings settings, TurboPascalRandom random)
    {
        var weapon = shooter.Weapon;
        if (color is >= 20 and <= 29)
        {
            var amount = random.Next((ushort)(6 * weapon)) + 1;
            HitTank(field, shooter, field.Tanks[color - 20], amount, random);
            return;
        }

        if (color != Terrain)
            return;

        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        var list = field.CriList;
        var branches = CriBranches(weapon);
        var tx = (short)x.Trunc();
        var ty = (short)y.Trunc();
        var found = false;
        for (var guard = 0; !found && guard <= ushort.MaxValue; guard++)
        {
            if (GetPixel(screen, tx, ty + 1) == Terrain || GetPixel(screen, tx, ty - 1) == Terrain
                || GetPixel(screen, tx + 1, ty) == Terrain || GetPixel(screen, tx - 1, ty) == Terrain)
                found = true;
            else
                ty++;
        }

        for (var k = 1; k <= branches; k++)
            list[k] = (tx, ty);

        Span<byte> directions = stackalloc byte[9];
        var diagonal = CriDiagonal(weapon);
        while (branches != 0)
        {
            for (var k = 1; k <= branches; k++)
            {
                var n = 0;
                var (bx, by) = list[k];
                for (var d = 1; d <= 8; d++)
                {
                    if ((d & 1) == 0 && !diagonal)
                        continue;
                    var (dx, dy) = Step(d);
                    if (GetPixel(screen, bx + dx, by + dy) == Terrain)
                        directions[++n] = (byte)d;
                }

                if (n > 0)
                {
                    var (dx, dy) = Step(directions[random.Next(n) + 1]);
                    list[k] = ((short)(bx + dx), (short)(by + dy));
                    if (settings.Sound)
                        random.Next(500);
                    CopyPixel(background, screen, list[k].X, list[k].Y);
                }
                else
                {
                    list[k] = (0, 0);
                }
            }

            settings.Frame?.Invoke(ShotFrame.Explosion);
            var dead = 0;
            for (var k = 1; k <= branches; k++)
            {
                if (list[k].X != 0)
                    continue;
                dead++;
                var j = 1;
                while (j < CriListLength && (j == k || list[j].X == 0))
                    j++;
                if (j == CriListLength)
                    j = k;
                list[k] = list[j];
            }

            if (dead == branches)
                branches = 0;
        }
    }

    // Directions of the CRI: 1 up, 2 up right, 3 right, 4 down right, 5 down, 6 down left, 7 left, 8 up left.
    private static (int Dx, int Dy) Step(int direction) => direction switch
    {
        1 => (0, -1),
        2 => (1, -1),
        3 => (1, 0),
        4 => (1, 1),
        5 => (0, 1),
        6 => (-1, 1),
        7 => (-1, 0),
        _ => (-1, -1),
    };

    /// <summary>
    /// Sonic Blaster. [ORIGINAL] O6:0E66–0FB7 (BOMB.OVR 0x19D92): shot point U431:03C3, 100 steps colour 19 = (Random(64),
    /// 0, 0), 100 steps colour 19 the same and colour 2 = (Random(32) + 32, Random(32) + 32, Random(32) + 32), each step
    /// with sound Random(1000) and Delay(ES); then the point from the background (U431:040F), colour 19 = (63, 63, 63),
    /// colour 2 = (0, 42, 0) and the landslide U431:0496. No damage.
    /// </summary>
    private static void Sonic(Playfield.Playfield field, Real48 x, Real48 y, ShotSettings settings, TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var px = (short)x.Trunc();
        var py = (short)y.Trunc();
        BallisticShot.DrawPoint(screen, px, py);
        for (var i = 1; i <= 100; i++)
        {
            field.Palette.Set(Playfield.Playfield.ShotColor, random.Next(64), 0, 0);
            if (settings.Sound)
                random.Next(1000);
            settings.Frame?.Invoke(ShotFrame.Palette);
        }

        for (var i = 1; i <= 100; i++)
        {
            field.Palette.Set(Playfield.Playfield.ShotColor, random.Next(64), 0, 0);
            var r = random.Next(32) + 32;
            var g = random.Next(32) + 32;
            var b = random.Next(32) + 32;
            field.Palette.Set(Terrain, r, g, b);
            if (settings.Sound)
                random.Next(1000);
            settings.Frame?.Invoke(ShotFrame.Palette);
        }

        BallisticShot.RestorePoint(field.Background.Pixels, screen, px, py);
        field.Palette.Set(Playfield.Playfield.ShotColor, 63, 63, 63);
        field.Palette.Set(Terrain, 0, 42, 0);
        AfterShot.Landslide(screen, field.Background.Pixels);
        settings.Frame?.Invoke(ShotFrame.Landslide);
    }

    /// <summary>[ORIGINAL] UCC8:0BFF (BOMB.EXE 0x0EF1F): pixel at x + 320 · y (16 bit) of the screen.</summary>
    private static byte GetPixel(ReadOnlySpan<byte> screen, int x, int y)
    {
        var a = (ushort)(x + (y << 8) + (y << 6));
        return a < IndexBuffer.Size ? screen[a] : (byte)0;
    }

    /// <summary>[ORIGINAL] UCC8:0BD8 (BOMB.EXE 0x0EEF8): the pixel from the background buffer to the screen.</summary>
    private static void CopyPixel(ReadOnlySpan<byte> background, Span<byte> screen, int x, int y)
    {
        var a = (ushort)(x + (y << 8) + (y << 6));
        if (a < IndexBuffer.Size)
            screen[a] = background[a];
    }
}
