using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>Options of a shot (option variables of BOMB.CFG and the round).</summary>
/// <param name="SubSteps">SS, DS:E8CA: sub-steps per tick (docs/analyse/runde.md section 5.3).</param>
/// <param name="Wind">Wind of the round, DS:E8A2 (Core/Playfield/Wind).</param>
/// <param name="Trail">Trail, DS:E8B5.</param>
/// <param name="Sound">Sound effects, DS:E8B3: only the explosion picture draws Random numbers for its tones.</param>
/// <param name="Frame">
/// Optional: called whenever the screen shows a picture the original would show for a while (for an animation in the UI);
/// it must not change the playfield.
/// </param>
/// <param name="Crumble">Option Crumble DS:E8A4 in percent: Xplosive Dirt decides its landslide with it (runde.md 5.7 K8).</param>
public sealed record ShotSettings(int SubSteps, int Wind, bool Trail = false, bool Sound = false, Action<ShotFrame>? Frame = null,
    int Crumble = 0);

/// <summary>Kind of picture passed to <see cref="ShotSettings.Frame"/>.</summary>
public enum ShotFrame
{
    /// <summary>Start of a tick of the flight (no delay in the original: the speed of the machine).</summary>
    Flight,

    /// <summary>Rings of the explosion drawn (O6:035E).</summary>
    Explosion,

    /// <summary>One step of the palette runs, followed by Delay(ES) in the original.</summary>
    Palette,

    /// <summary>Crater copied from the background buffer.</summary>
    Crater,

    /// <summary>Landslide done (U431:0496).</summary>
    Landslide,

    /// <summary>One row of a falling tank, followed by Delay(FS) in the original.</summary>
    Fall,
}

/// <summary>State at the head of the flight loop (O4:060A): position, speed, sub-step DS:4740, ticks [bp-2Eh].</summary>
public readonly record struct ShotStep(Real48 X, Real48 Y, Real48 Vx, Real48 Vy, int SubStep, int Ticks);

/// <summary>End of the flight (O4:1343): state, last integer position, hit colour DS:47DE, explode DS:4750.</summary>
public sealed record ShotResult(Real48 X, Real48 Y, Real48 Vx, Real48 Vy, int Ix, int Iy, int Hit, bool Exploded, int Steps);

/// <summary>
/// Flight of the ballistic weapons. [ORIGINAL] O4:01B7 (BOMB.OVR 0x10EBC), docs/analyse/runde.md sections 5.1–5.4 and
/// docs/analyse/feld.md section 5; bit-identical to the probe <c>feld_schuss.py</c> (ShotProbeTests).
/// </summary>
/// <remarks>
/// Every weapon of O4:01B7 (all but laser 5 and MIRV 7, <see cref="WeaponShot"/>) with repulsers (Q5) and guidance (G1);
/// bit-identical to the probe <c>feld_schuss.py waffen</c> (WeaponProbeTests). Tones are not played.
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
    private static readonly Real48 RepelForce = Real48.FromHex("8A0000000096");      // −600, O4:091A
    private static readonly Real48 Half = Real48.FromHex("800000000000");            // 0.5, O4:0ACF
    private static readonly Real48 MinusOnePointZeroOne = Real48.FromHex("817B14AE4781"); // −1.01, O4:0B80
    private static readonly Real48 OnePointZeroOne = Real48.FromHex("817B14AE4701"); // 1.01, O4:0BBE
    private static readonly Real48 PointOne = Real48.FromHex("7DCDCCCCCC4C");        // 0.1, O4:0BD9

    /// <summary>Fires the weapon of <paramref name="shooter"/> (<see cref="PlayfieldTank.Weapon"/>) and runs the impact.</summary>
    public static ShotResult Fire(Playfield.Playfield field, PlayfieldTank shooter, ShotSettings settings,
        TurboPascalRandom random, Action<ShotStep>? onStep = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        if (!Impact.IsSupported(shooter.Weapon))
            throw new NotSupportedException($"Weapon {shooter.Weapon} is not ported yet (spur waffen).");

        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels.AsSpan();
        var tanks = field.Tanks;
        var invisible = field.Options.InvisibleShots;
        var wall = field.Wall;
        var wallFactor = field.WallFactor;

        // O4:01BD–0350: hit colour, M = 3 · energy + 643 (G2); stock − 1, Herb / Vick − 1 for the guidance (Q1); Q2.
        var hit = 0;
        var throughPower = unchecked((short)(3 * shooter.Energy + 643));
        shooter.Stock[shooter.Weapon] = unchecked((short)(shooter.Stock[shooter.Weapon] - 1));
        if (shooter.Guidance is 1 or 3)
            shooter.Herbs = unchecked((short)(shooter.Herbs - 1));
        if (shooter.Guidance is 2 or 3)
            shooter.Vicks = unchecked((short)(shooter.Vicks - 1));
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

        // O4:05AE–05FC: is there an active record with a repulser (LongInt +5Ah > 0)?
        var repulsers = tanks.Any(t => t.Active && t.Defense[Damage.Repulser] > 0);
        var steps = 0;
        int ix = 0, iy = 0;
        var subStepsReal = Real48.FromInt32(subSteps);
        var windReal = Real48.FromInt32(settings.Wind);

        while (true)
        {
            onStep?.Invoke(new ShotStep(x, y, vx, vy, subStep, ticks));
            if (subStep == subSteps)
                settings.Frame?.Invoke(ShotFrame.Flight);
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

            // O4:073F: the shooter's own tank counts only after the shot left its ±40 field once (Q5).
            if (!leftShooter && (Abs16(iy - shooter.Y) > 40 || Abs16(ix - shooter.X) > 40))
                leftShooter = true;

            if (repulsers)
                Repel(tanks, shooter, leftShooter, ix, iy, subStepsReal, ref vx, ref vy);
            if (shooter.Guidance > 0)
                Guide(field, shooter, ix, iy, ref y, ref vx, ref vy);

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
                                shooter.Guidance = 0;  // O4:0E31
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
                if (ix > 310 && Real48.Compare(vx, Real48.One) > 0 && settings.Wind > 0 && shooter.Guidance == 0)
                    y = WarpWidth;
                if (ix < 10 && Real48.Compare(vx, Real48.One) < 0 && settings.Wind < 0 && shooter.Guidance == 0)
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

    /// <summary>
    /// Repulsers (Q5). [ORIGINAL] O4:0788–0A34 (BOMB.OVR 0x1148D): per active record with repulser &gt; 0 (the shooter
    /// only after the shot left its ±40 field), dx = x_tank − ix, dy = iy − y_tank, both |·| ≤ 40 and d² = Trunc(dy ·
    /// Int(dy) + dx · Int(dx)) ≤ 1600: f = −600 / d² / SS, w = Trunc(Deg(ArcTan(dy / dx))) (+180 for dx &lt; 0; dx = 0:
    /// 90 for dy &gt; 0, else 270), vx := Cos(w) · f + vx, vy := vy − Sin(w) · f with the degree table 0B25. The line
    /// colour 14 from the tank to the shot is drawn and at once restored from the buffer DS:E5BC (no lasting pixel).
    /// d² = 0 is the runtime error of the division, as in the original.
    /// </summary>
    private static void Repel(IReadOnlyList<PlayfieldTank> tanks, PlayfieldTank shooter, bool leftShooter, int ix, int iy,
        Real48 subSteps, ref Real48 vx, ref Real48 vy)
    {
        foreach (var tank in tanks)
        {
            if (!tank.Active || tank.Defense[Damage.Repulser] <= 0)
                continue;
            if (!leftShooter && tank == shooter)
                continue;
            var dy = (short)(iy - tank.Y);
            var dx = (short)(tank.X - ix);
            if (Abs16(dx) > 40 || Abs16(dy) > 40)
                continue;
            var fdx = Real48.FromInt32(dx);
            var fdy = Real48.FromInt32(dy);
            var squares = (Real48.Int(fdy) * fdy + Real48.Int(fdx) * fdx).Trunc();
            if (squares > 1600)
                continue;
            var force = RepelForce / Real48.FromInt32(squares) / subSteps;
            int angle;
            if (dx != 0)
            {
                angle = (short)Real48.RadiansToDegrees(Real48.ArcTan(fdy / fdx)).Trunc();
                if (dx < 0)
                    angle = (short)(angle + 180);
            }
            else
            {
                angle = dy > 0 ? 90 : 270;
            }

            vx = Real48.CosDegrees(angle) * force + vx;
            vy -= Real48.SinDegrees(angle) * force;
        }
    }

    /// <summary>
    /// Guidance (G1). [ORIGINAL] O4:0A37–0C01 (BOMB.OVR 0x1173C): target = record +28h, dx = x_target − ix, dy = y_target −
    /// iy. Vick (+26h ∈ {2, 3}): |dx| ≤ 2 → vx = 0 and y ± 0.5 towards the target (+ for dy &gt; 0). Herb (+26h ∈ {1, 3}):
    /// 0 &lt; dy ≤ 4 → vy = 0, then vx · (−1.01) if dx · vx &lt; 0, else vx · 1.01, or 0.1 for vx = 0. When one of them acts,
    /// colour 19 becomes red (63, 0, 0).
    /// </summary>
    private static void Guide(Playfield.Playfield field, PlayfieldTank shooter, int ix, int iy, ref Real48 y,
        ref Real48 vx, ref Real48 vy)
    {
        var target = field.Tanks[shooter.GuidanceTarget - 1];
        var acted = false;
        var dx = (short)(target.X - ix);
        var dy = (short)(target.Y - iy);
        if (shooter.Guidance is 2 or 3 && Abs16(dx) <= 2)
        {
            acted = true;
            vx = Real48.Zero;
            y = dy > 0 ? y + Half : y - Half;
        }

        if (shooter.Guidance is 1 or 3 && dy <= 4 && dy > 0)
        {
            acted = true;
            vy = Real48.Zero;
            if (Real48.Compare(Real48.FromInt32(dx) * vx, Real48.Zero) < 0)
                vx *= MinusOnePointZeroOne;
            else if (!Real48.PascalEquals(vx, Real48.Zero))
                vx *= OnePointZeroOne;
            else
                vx = PointOne;
        }

        if (acted)
            field.Palette.Set(Playfield.Playfield.ShotColor, 63, 0, 0);
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
