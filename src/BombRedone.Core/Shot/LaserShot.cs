using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>
/// The laser (weapon 5). [ORIGINAL] O4:3426 (BOMB.OVR 0x1412B), docs/analyse/runde.md section 5.10 and
/// docs/analyse/feld.md section 8.4: charge picture in colour 254, then a straight beam that burns through the terrain and
/// stands at a tank until its energy is used up.
/// </summary>
public static class LaserShot
{
    /// <summary>DAC entry and drawing colour of the beam.</summary>
    public const byte BeamColor = 254;

    private const int Weapon = 5;
    private const int Sparks = 15;

    // [ORIGINAL] Real48 constants of O4:3426.
    private static readonly Real48 Eight = Real48.FromInt32(8);                       // O4:3538
    private static readonly Real48 Hundred = Real48.FromHex("870000000048");          // O4:35F0
    private static readonly Real48 SelfDamageChance = Real48.FromHex("7ECDCCCCCC4C"); // 0.2, O4:39E4
    private static readonly Real48 Seven = Real48.FromHex("830000000060");            // O4:3A15
    private static readonly Real48 FiftyThree = Real48.FromHex("860000000054");       // O4:3BA0
    private static readonly Real48 Ten = Real48.FromHex("840000000020");              // O4:3C27
    private static readonly Real48 Twenty = Real48.FromHex("850000000020");           // O4:3E58
    private static readonly Real48 Left = Real48.FromHex("840000000030");             // 11, O4:4116
    private static readonly Real48 Right = Real48.FromHex("89000000801A");            // 309, O4:4165
    private static readonly Real48 Top = Real48.FromHex("850000000078");              // 31, O4:41B4
    private static readonly Real48 Bottom = Real48.FromHex("88000000003D");           // 189, O4:4203
    private static readonly Real48 WarpLeft = Real48.FromHex("840000000020");         // 10, O4:425C
    private static readonly Real48 WarpRight = Real48.FromHex("89000000001B");        // 310, O4:4299
    private static readonly Real48 WarpWidth = Real48.FromHex("890000000016");        // 300, O4:4276

    /// <summary>
    /// Fires the laser of <paramref name="shooter"/>. [ORIGINAL] O4:3426: nothing for power 0; the power is capped at the
    /// laser stock (+38h), which loses it; colour of the shooter (O10:014A); colour 254 = colour of the shooter, barrel
    /// in 254 and body; charge ramp of 100 colours from the shooter's colour to (63, 63, 0), run up and down twice with
    /// Delay(LS div k) and once more up (with sound tones only); then E = 10 · power, direction b = a (r &gt; 0) or 180 − a,
    /// Random:Real &lt; 0.2 → 1 damage on the shooter, start 7 pixels along the barrel, the trail colours 100 − i, and the
    /// beam steps (<see cref="Beam"/>). At the end colour 254 = black and every pixel of colour 254 (the beam and the barrel)
    /// from the background buffer (U431:0594).
    /// </summary>
    /// <returns>The last position of the beam (DS:4796, DS:479C; the round loop logs it), null for power 0 (nothing fired,
    /// the shot state keeps its value).</returns>
    public static (Real48 X, Real48 Y)? Fire(Playfield.Playfield field, PlayfieldTank shooter, ShotSettings settings,
        TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var contact = new bool[field.Tanks.Count + 1];
        contact[shooter.Number] = true;
        if (shooter.Power == 0)
            return null;
        if (shooter.Power > (short)shooter.Stock[Weapon])
            shooter.Power = (short)shooter.Stock[Weapon];
        shooter.Stock[Weapon] = unchecked((short)(shooter.Stock[Weapon] - shooter.Power));
        field.UpdateColor(shooter);

        // O4:34E2 (O10:021C): v = 63 · energy div 100 divided by the colour divisors, without the check for energy > 0.
        var v = (short)(63 * shooter.Energy) / 100;
        var (dr, dg, db) = shooter.ColorDivisors;
        int red = v / dr, green = v / dg, blue = v / db;
        field.Palette.Set(BeamColor, red, green, blue);

        var screen = field.Screen.Pixels.AsSpan();
        var (bx, by) = Playfield.Playfield.BarrelOffset(shooter.Angle);
        VgaDraw.Line(screen, shooter.X, shooter.Y, shooter.X + bx * shooter.Direction, shooter.Y - by, BeamColor);
        VgaDraw.TankBody(screen, shooter.X, shooter.Y, shooter.Color);

        // O4:35E4–372A: ramp DS:E5C4 + 3i = Trunc(i · (63 − c) / 100 + c) per component (blue towards 0).
        var stepR = Real48.FromInt32(63 - red) / Hundred;
        var stepG = Real48.FromInt32(63 - green) / Hundred;
        var stepB = Real48.FromInt32(-blue) / Hundred;
        var ramp = new (int R, int G, int B)[101];
        for (var i = 1; i <= 100; i++)
        {
            var fi = Real48.FromInt32(i);
            ramp[i] = (
                (byte)(Real48.FromInt32(red) + fi * stepR).Trunc(),
                (byte)(Real48.FromInt32(green) + fi * stepG).Trunc(),
                (byte)(Real48.FromInt32(blue) + fi * stepB).Trunc());
        }

        // O4:372D–388D: twice up and down, then once up (only SetPal, Delay and tones; no Random).
        for (var k = 1; k <= 2; k++)
        {
            for (var i = 1; i <= 100; i++)
                SetRamp(field, ramp[i], settings);
            for (var i = 100; i >= 1; i--)
                SetRamp(field, ramp[i], settings);
        }

        for (var i = 1; i <= 100; i++)
            SetRamp(field, ramp[i], settings);

        var energy = (short)(10 * shooter.Power);
        var direction = shooter.Direction > 0 ? shooter.Angle : 90 - shooter.Angle + 90;
        if (Real48.Compare(random.NextReal(), SelfDamageChance) < 0)
            Damage.Apply(field, shooter, 1, Damage.Shield, random);

        // O4:3A09–3B13: start and unit step, operand order as in the original.
        var radians = Real48.DegreesToRadians(Real48.FromInt32(shooter.Angle));
        var x = Real48.FromInt32(shooter.X) + Real48.FromInt32(shooter.Direction) * (Real48.Cos(radians) * Seven);
        var y = Real48.FromInt32(shooter.Y) - Real48.Sin(radians) * Seven;
        var beam = Real48.DegreesToRadians(Real48.FromInt32(direction));
        var vx = Real48.Cos(beam);
        var sin = Real48.Sin(beam);
        var vy = sin.IsZero || sin.IsNegative ? sin : -sin;  // or dh,80h for a non-zero value

        for (var i = 1; i <= field.Tanks.Count; i++)
        {
            var (r, g, b) = field.Tanks[i - 1].ColorDivisors;
            field.Palette.Set(100 - i, 32 / r, 32 / g, 32 / b);
        }

        var end = Beam(field, shooter, contact, energy, x, y, vx, vy, settings, random);

        field.Palette.Set(BeamColor, 0, 0, 0);
        RestoreColor(screen, field.Background.Pixels, BeamColor, BeamColor);
        settings.Frame?.Invoke(ShotFrame.Crater);
        return end;
    }

    private static void SetRamp(Playfield.Playfield field, (int R, int G, int B) c, ShotSettings settings)
    {
        field.Palette.Set(BeamColor, c.R, c.G, c.B);
        settings.Frame?.Invoke(ShotFrame.Palette);
    }

    /// <summary>
    /// [ORIGINAL] O4:3B83–43EC: per step position + step, E − 10, colour 254 = (g, g, 0) with g = Trunc(E · 53 / (10 ·
    /// power) + 10); the pixel at the tip (UCC8:0BFF): terrain → 15 sparks at (x − Random(20) · vx, y − Random(20) · vy),
    /// each in the window and not 254 or a tank pixel from the background (sound Random(500 · k) + 500); a tank 20..29 with
    /// energy &gt; 0 → first contact +1 hit for the shooter, E − 10, without shield 15 sparks in its colour at (x − Random(40)
    /// + 20, y − Random(40) + 20) (drawn again until the pixel there is no other tank and not 254), each shown for Delay(LS)
    /// and then taken back from the background, 3 damage (shield first) and one step back. Then Delay(LS) and sound
    /// Random(12 · E); walls; the tip in 254 unless the beam ended; end at E ≤ 0.
    /// </summary>
    private static (Real48 X, Real48 Y) Beam(Playfield.Playfield field, PlayfieldTank shooter, bool[] contact, short energy,
        Real48 x, Real48 y, Real48 vx, Real48 vy, ShotSettings settings, TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        var scale = FiftyThree / Real48.FromInt32((short)(10 * shooter.Power));
        var sparkX = new short[Sparks + 1];
        var sparkY = new short[Sparks + 1];
        var stop = false;
        do
        {
            x += vx;
            y += vy;
            energy = (short)(energy - 10);
            var grey = (Real48.FromInt32(energy) * scale + Ten).Trunc();
            field.Palette.Set(BeamColor, grey, grey, 0);
            var color = (int)GetPixel(screen, (short)x.Trunc(), (short)y.Trunc());
            if (color == Playfield.Playfield.TerrainColor)
            {
                for (var k = 1; k <= Sparks; k++)
                {
                    var sx = (short)(x - Real48.FromInt32(random.Next(20)) * vx).Trunc();
                    var sy = (short)(y - Real48.FromInt32(random.Next(20)) * vy).Trunc();
                    if (settings.Sound)
                        random.Next((ushort)(500 * k));
                    var g = GetPixel(screen, sx, sy);
                    if (InWindow(sx, sy) && g != BeamColor && g is < 20 or > 29)
                        CopyPixel(background, screen, sx, sy);
                }
            }
            else if (color is > 19 and < 30 && field.Tanks[color - 20].Energy > 0)
            {
                var target = field.Tanks[color - 20];
                if (!contact[target.Number])
                    shooter.Hits++;
                contact[target.Number] = true;
                energy = (short)(energy - 10);
                if (target.Defense[Damage.Shield] <= 0)
                {
                    for (var k = 1; k <= Sparks; k++)
                    {
                        while (true)
                        {
                            sparkX[k] = (short)(x - Real48.FromInt32(random.Next(40)) + Twenty).Trunc();
                            sparkY[k] = (short)(y - Real48.FromInt32(random.Next(40)) + Twenty).Trunc();
                            var g = GetPixel(screen, sparkX[k], sparkY[k]);
                            if (g < 20 || (g > 29 && g != BeamColor) || g == color)
                                break;
                        }

                        if (settings.Sound)
                            random.Next((ushort)(500 * k));
                        settings.Frame?.Invoke(ShotFrame.Palette);
                        if (InWindow(sparkX[k], sparkY[k]))
                            PutPixel(screen, sparkX[k], sparkY[k], (byte)color);
                    }

                    for (var k = 1; k <= Sparks; k++)
                    {
                        if (settings.Sound)
                            random.Next((ushort)(500 * k));
                        if (InWindow(sparkX[k], sparkY[k]))
                            CopyPixel(background, screen, sparkX[k], sparkY[k]);
                    }
                }

                Damage.Apply(field, target, 3, Damage.Shield, random);
                x -= vx;
                y -= vy;
            }

            settings.Frame?.Invoke(ShotFrame.Flight);
            if (settings.Sound)
                random.Next((ushort)(12 * energy));
            if (energy <= 0)
                stop = true;

            if (field.WallBounces)
            {
                // O4:410B–4247: mirrored without factor (sign flip of the original keeps a zero).
                if (Real48.Compare(x, Left) <= 0 && Real48.Compare(vx, Real48.Zero) < 0)
                    vx = -vx;
                if (Real48.Compare(x, Right) >= 0 && Real48.Compare(vx, Real48.Zero) > 0)
                    vx = -vx;
                if (Real48.Compare(y, Top) <= 0 && Real48.Compare(vy, Real48.Zero) < 0)
                    vy = -vy;
                if (Real48.Compare(y, Bottom) >= 0 && Real48.Compare(vy, Real48.Zero) > 0)
                    vy = -vy;
            }
            else
            {
                if (field.WallWarps)
                {
                    if (Real48.Compare(x, WarpLeft) <= 0)
                        x += WarpWidth;
                    if (Real48.Compare(x, WarpRight) >= 0)
                        x -= WarpWidth;
                }
                else
                {
                    if (Real48.Compare(x, Left) <= 0 && Real48.Compare(vx, Real48.Zero) < 0)
                        stop = true;
                    if (Real48.Compare(x, Right) >= 0 && Real48.Compare(vx, Real48.Zero) > 0)
                        stop = true;
                }

                if (Real48.Compare(y, Top) <= 0 && Real48.Compare(vy, Real48.Zero) < 0)
                    stop = true;
                if (Real48.Compare(y, Bottom) >= 0 && Real48.Compare(vy, Real48.Zero) > 0)
                    stop = true;
            }

            if (!stop)
                PutPixel(screen, (short)x.Trunc(), (short)y.Trunc(), BeamColor);
            if (energy <= 0)
                stop = true;
        }
        while (!stop);
        return (x, y);
    }

    private static bool InWindow(int x, int y) => x is >= 11 and <= 309 && y is >= 31 and <= 189;

    /// <summary>
    /// [ORIGINAL] UCC8:0BFF (BOMB.EXE 0x0EF1F) and Graph GetPixel (0E26:1FCC, full-screen viewport): x + 320 · y as a
    /// 16-bit address. [APPROX] Addresses beyond the screen give 0 (the original reads the rest of the video segment).
    /// </summary>
    private static byte GetPixel(ReadOnlySpan<byte> screen, int x, int y)
    {
        var a = (ushort)(x + (y << 8) + (y << 6));
        return a < IndexBuffer.Size ? screen[a] : (byte)0;
    }

    /// <summary>[ORIGINAL] UCC8:0BB6 (BOMB.EXE 0x0EED6): one pixel, 16-bit address; beyond the screen dropped.</summary>
    private static void PutPixel(Span<byte> screen, int x, int y, byte color)
    {
        var a = (ushort)(x + (y << 8) + (y << 6));
        if (a < IndexBuffer.Size)
            screen[a] = color;
    }

    /// <summary>[ORIGINAL] UCC8:0BD8 (BOMB.EXE 0x0EEF8): the pixel from the background buffer.</summary>
    private static void CopyPixel(ReadOnlySpan<byte> background, Span<byte> screen, int x, int y)
    {
        var a = (ushort)(x + (y << 8) + (y << 6));
        if (a < IndexBuffer.Size)
            screen[a] = background[a];
    }

    /// <summary>
    /// [ORIGINAL] U431:0594 (BOMB.EXE 0x05F44): for the addresses 64000 down to 1 (not 0) every screen pixel with
    /// low ≤ colour ≤ high from the background buffer. [APPROX] Address 64000 lies beyond the buffers and is skipped.
    /// </summary>
    internal static void RestoreColor(Span<byte> screen, ReadOnlySpan<byte> background, byte low, byte high)
    {
        for (var a = IndexBuffer.Size - 1; a >= 1; a--)
        {
            var c = screen[a];
            if (c >= low && c <= high)
                screen[a] = background[a];
        }
    }
}
