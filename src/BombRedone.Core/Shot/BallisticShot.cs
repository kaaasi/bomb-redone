using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>Options of a shot (option variables of BOMB.CFG and the round).</summary>
/// <param name="SubSteps">SS, DS:E8CA: sub-steps per tick (docs/analyse/runde.md section 5.3).</param>
/// <param name="Wind">Wind of the round, DS:E8A2 (Core/Playfield/Wind).</param>
/// <param name="Trail">Trail, DS:E8B5.</param>
/// <param name="Sound">Sound effects, DS:E8B3: only the explosion picture draws Random numbers for its tones.</param>
public sealed record ShotSettings(int SubSteps, int Wind, bool Trail = false, bool Sound = false);

/// <summary>State at the head of the flight loop (O4:060A): position, speed, sub-step DS:4740, ticks [bp-2Eh].</summary>
public readonly record struct ShotStep(Real48 X, Real48 Y, Real48 Vx, Real48 Vy, int SubStep, int Ticks);

/// <summary>End of the flight (O4:1343): state, last integer position, hit colour DS:47DE, explode DS:4750.</summary>
public sealed record ShotResult(Real48 X, Real48 Y, Real48 Vx, Real48 Vy, int Ix, int Iy, int Hit, bool Exploded, int Steps);

/// <summary>
/// Flight of the ballistic weapons. [ORIGINAL] O4:01B7 (BOMB.OVR 0x10EBC), docs/analyse/runde.md sections 5.1–5.4 and
/// docs/analyse/feld.md section 5; bit-identical to the probe <c>feld_schuss.py</c> (ShotProbeTests).
/// </summary>
/// <remarks>
/// Not in round 2 (R2-D1 = A), rejected with <see cref="NotSupportedException"/>: guidance (+26h), repulsers in the field
/// (+5Ah, Q5) and the weapons whose impact is not ported (only 0 Lead Ball and the explosives {1, 2, 3, 4, 7, 11}).
/// The stock of the weapon (+2Eh) is not kept by the playfield yet and is not decremented. Tones are not played.
/// </remarks>
public static class BallisticShot
{
    // [ORIGINAL] Real48 constants of O4:01B7.
    private static readonly Real48 SelfDamageChance = Real48.FromHex("7ECDCCCCCC4C"); // 0.2, O4:035A
    private static readonly Real48 Ten = Real48.FromHex("840000000020");             // O4:03A3
    private static readonly Real48 Fifty = Real48.FromHex("860000000048");           // O4:045B
    private static readonly Real48 Gravity = Real48.FromHex("7DCDCCCCCC4C");         // 0.1, O4:06A6
    private static readonly Real48 WindScale = Real48.FromHex("8D000000401C");       // 5000, O4:06E8
    private static readonly Real48 MaxSpeed = Real48.FromHex("850000000070");        // 30, O4:129D
    private static readonly Real48 Bounce = Real48.FromHex("819A99999919");          // 1.2, O4:0E0E
    private static readonly Real48 WarpWidth = Real48.FromHex("890000000016");       // 300, O4:107F
    private static readonly Real48 StopSpeed = Gravity;                              // 0.1, O4:104A

    /// <summary>Fires the weapon of <paramref name="shooter"/> (<see cref="PlayfieldTank.Weapon"/>) and runs the impact.</summary>
    public static ShotResult Fire(Playfield.Playfield field, PlayfieldTank shooter, ShotSettings settings,
        TurboPascalRandom random, Action<ShotStep>? onStep = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        if (!Impact.IsSupported(shooter.Weapon))
            throw new NotSupportedException($"Weapon {shooter.Weapon} is not ported (round 2: 0 and the explosives).");
        if (field.Tanks.Any(t => t.Active && t.Defense[Damage.Repulser] > 0))
            throw new NotSupportedException("Repulsers in the field are not ported (runde.md 5.3 Q5).");

        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels.AsSpan();
        var tanks = field.Tanks;
        var invisible = field.Options.InvisibleShots;
        var wall = field.Wall;
        var wallFactor = field.WallFactor;

        // O4:01BD–0350: hit colour, M = 3 · energy + 643 (G2); stock and guidance (not kept); Q2.
        var hit = 0;
        var throughPower = unchecked((short)(3 * shooter.Energy + 643));
        if (Real48.Compare(random.NextReal(), SelfDamageChance) < 0)
            Damage.Apply(field, shooter, 1, Damage.Shield, random);

        // O4:0384–0511: start values (Q3), operand order as in the original.
        var radians = Real48.DegreesToRadians(Real48.FromInt32(shooter.Angle));
        var direction = Real48.FromInt32(shooter.Direction);
        var x = Real48.FromInt32(shooter.X) + direction * (Real48.Cos(radians) * Ten);
        var y = Real48.FromInt32(shooter.Y) - Real48.Sin(radians) * Ten;
        var speed = Real48.FromInt32(shooter.Power) / Fifty;
        var vx = direction * (Real48.Cos(radians) * speed);
        var sin = Real48.Sin(radians);
        var vy = (sin.IsZero ? sin : -sin) * speed;

        // O4:0515–05A1: trail colours 100 − i = 32 div the colour divisors, shot colour white.
        var stop = false;
        int ox = -99, oy = -99;
        var subSteps = (short)settings.SubSteps;
        for (var i = 1; i <= tanks.Count; i++)
        {
            var (r, g, b) = Playfield.Playfield.ColorDivisors[i - 1];
            field.Palette.Set(100 - i, 32 / r, 32 / g, 32 / b);
        }

        field.Palette.Set(Playfield.Playfield.ShotColor, 63, 63, 63);

        // O4:05A5–0606: explode by default; the sub-step counter keeps the value of the loop over the records (N).
        var explode = true;
        var subStep = (short)tanks.Count;
        var ticks = 0;
        var leftShooter = false;
        var steps = 0;
        int ix = 0, iy = 0;
        var subStepsReal = Real48.FromInt32(subSteps);
        var windReal = Real48.FromInt32(settings.Wind);

        while (true)
        {
            onStep?.Invoke(new ShotStep(x, y, vx, vy, subStep, ticks));
            steps++;

            // O4:060A: sub-step 0..SS, then the next tick.
            subStep++;
            if (subStep > subSteps)
            {
                subStep = 0;
                ticks++;
            }

            // O4:0624–0715 (Q4).
            x = vx / subStepsReal + x;
            y = vy / subStepsReal + y;
            vy = Gravity / subStepsReal + vy;
            vx = windReal / WindScale / subStepsReal + vx;
            ix = (short)x.Trunc();
            iy = (short)y.Trunc();

            // O4:073F: the shooter's own tank counts only after the shot left its ±40 field once (Q5; no repulsers here).
            if (!leftShooter && (Abs16(iy - shooter.Y) > 40 || Abs16(ix - shooter.X) > 40))
                leftShooter = true;

            // O4:0C34–0CEF: restore the old point, trail, clear the marks on the frame.
            if (ox > 11 && oy > 31 && ox < 309)
            {
                if (!invisible)
                    RestorePoint(background, screen, ox, oy);
                if (settings.Trail)
                    TrailPoint(screen, background, ox, oy, (byte)(100 - shooter.Number));
            }

            if (ox >= 9 && ox <= 311 && !invisible)
            {
                VgaDraw.PutPixel(screen, ox, 29, 0);
                VgaDraw.PutPixel(screen, ox, 191, 0);
            }

            if (oy >= 29 && oy <= 191 && !invisible)
            {
                VgaDraw.PutPixel(screen, 9, oy, 0);
                VgaDraw.PutPixel(screen, 311, oy, 0);
            }

            ox = ix;
            oy = iy;

            if (ix > 11 && iy > 33 && ix < 309 && iy < 187 && !stop)
            {
                // O4:0D30–0E58: hit test on the four neighbours and the point (Q9, G2); all five are read.
                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (Math.Abs(dx) + Math.Abs(dy) > 1)
                            continue;
                        int color = screen[VgaDraw.Address(ix + dx, iy + dy)];
                        if (color == Playfield.Playfield.TerrainColor)
                        {
                            hit = color;
                            if (shooter.Power < throughPower || subStep == subSteps)
                                stop = true;
                            if (shooter.Power == throughPower)
                                stop = false;
                        }

                        if (color is >= 20 and <= 29)
                        {
                            hit = color;
                            stop = true;
                            if (shooter.Weapon is >= 12 and <= 16)
                            {
                                stop = false;
                                vy = (vy.IsZero ? vy : -vy) * Bounce;
                                field.Palette.Set(Playfield.Playfield.ShotColor, 0, 0, 63);
                            }
                        }
                    }
                }
            }
            else if (field.WallBounces)
            {
                // O4:0E5E–1064 (Q6).
                var f = wallFactor!.Value;
                if (ix <= 10 && Real48.Compare(vx, Real48.Zero) < 0)
                {
                    vx = Negate(vx) * f;
                    vy *= f;
                }

                if (ix >= 310 && Real48.Compare(vx, Real48.Zero) > 0)
                {
                    vx = Negate(vx) * f;
                    vy *= f;
                }

                if (iy <= 30 && Real48.Compare(vy, Real48.Zero) < 0)
                {
                    vy = Negate(vy) * f;
                    vx *= f;
                }

                if (iy >= 188 && Real48.Compare(vy, Real48.Zero) > 0)
                {
                    vx *= f;
                    vy = Negate(vy) * f;
                    if (Real48.Compare(Abs(vy), StopSpeed) < 0)
                        explode = stop = true;
                }
            }
            else if (wall == WallKind.Warp)
            {
                // O4:1067–10C1 (Q7).
                if (ix <= 10)
                    x += WarpWidth;
                if (ix >= 310)
                    x -= WarpWidth;
            }
            else
            {
                // O4:10CE–115A (Q7): the left rule tests vx < 1, the right one vx > 1.
                if (ix > 310 && Real48.Compare(vx, Real48.One) > 0 && settings.Wind > 0)
                    y = WarpWidth;
                if (ix < 10 && Real48.Compare(vx, Real48.One) < 0 && settings.Wind < 0)
                    y = WarpWidth;
            }

            // O4:115D–11EC (Q8).
            if (ticks > 1000)
                stop = explode = true;
            if (!field.WallBounces)
            {
                if (iy < -5000)
                {
                    stop = true;
                    explode = false;
                }

                if (iy >= 190)
                    stop = true;
            }

            // O4:11EE–128A: new point and marks, or clear the marks with a black rectangle at the end.
            if (!stop && !invisible)
            {
                if (ix > 11 && iy > 31 && ix < 309)
                    DrawPoint(screen, ix, iy);
                if (ix >= 9 && ix <= 311)
                {
                    VgaDraw.PutPixel(screen, ix, 29, Playfield.Playfield.FrameColor);
                    VgaDraw.PutPixel(screen, ix, 191, Playfield.Playfield.FrameColor);
                }

                if (iy >= 29 && iy <= 191)
                {
                    VgaDraw.PutPixel(screen, 9, iy, Playfield.Playfield.FrameColor);
                    VgaDraw.PutPixel(screen, 311, iy, Playfield.Playfield.FrameColor);
                }
            }

            if (stop)
                VgaDraw.Rectangle(screen, 9, 29, 311, 191, 0);

            // O4:128F–12CE: speed limit.
            if (Real48.Compare(Abs(vx), MaxSpeed) > 0 || Real48.Compare(Abs(vy), MaxSpeed) > 0)
                stop = explode = true;
            if (stop)
                break;
        }

        // O4:12DD–1369: restore, hit counter, impact O6:00E0.
        RestorePoint(background, screen, ix, iy);
        RestorePoint(background, screen, ox, oy);
        if (hit is >= 20 and <= 29 && tanks[hit - 20].Team != shooter.Team)
            shooter.Hits++;
        var result = new ShotResult(x, y, vx, vy, ix, iy, hit, explode, steps);
        if (explode)
            Impact.Run(field, shooter, hit, x, y, settings, random);
        return result;
    }

    private static int Abs16(int value)
    {
        var v = (short)value;
        return (short)((v ^ (v >> 15)) - (v >> 15));
    }

    /// <summary>Sign flip of the original (<c>or al,al; jz; xor dh,80h</c>): zero stays as it is.</summary>
    private static Real48 Negate(Real48 value) => value.IsZero ? value : -value;

    /// <summary><c>and dh,7Fh</c>.</summary>
    private static Real48 Abs(Real48 value) => value.IsNegative ? -value : value;

    /// <summary>[ORIGINAL] U431:03C3 (BOMB.EXE 0x05D73): plus of five pixels in colour 19 for 0 &lt; x &lt; 320, 0 &lt; y &lt; 199.</summary>
    internal static void DrawPoint(Span<byte> screen, int x, int y)
    {
        if (x <= 0 || x >= 320 || y <= 0 || y >= 199)
            return;
        var a = VgaDraw.Address(x, y);
        foreach (var o in PlusOffsets)
            screen[a + o] = Playfield.Playfield.ShotColor;
    }

    /// <summary>[ORIGINAL] U431:040F (BOMB.EXE 0x05DBF): the same plus copied from the background buffer.</summary>
    internal static void RestorePoint(ReadOnlySpan<byte> background, Span<byte> screen, int x, int y)
    {
        if (x <= 0 || x >= 320 || y <= 0 || y >= 199)
            return;
        var a = VgaDraw.Address(x, y);
        foreach (var o in PlusOffsets)
            screen[a + o] = background[a + o];
    }

    /// <summary>
    /// [ORIGINAL] U431:056D (BOMB.EXE 0x05F1D): one pixel in screen and background buffer, no bounds check.
    /// [APPROX] Addresses beyond the 64000 screen bytes are dropped (only reachable below the frame).
    /// </summary>
    private static void TrailPoint(Span<byte> screen, Span<byte> background, int x, int y, byte color)
    {
        var a = (ushort)(y * 320 + x);
        if (a >= IndexBuffer.Size)
            return;
        screen[a] = color;
        background[a] = color;
    }

    private static readonly int[] PlusOffsets = [0, 320, -320, 1, -1];
}
