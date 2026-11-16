using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>MIRV pattern of the dialog "MIRV Setup" (O4:13D4, order of the entries).</summary>
public enum MirvPattern
{
    Random = 0,
    Horizontal = 1,
    Vertical = 2,
    FourWay = 3,
    XFourWay = 4,
}

/// <summary>MIRV detonation of the dialog "MIRV Setup".</summary>
public enum MirvDetonation
{
    Impact = 0,
    Simultaneous = 1,
    None = 2,
}

/// <summary>
/// The MIRV (weapon 7). [ORIGINAL] O4:13D4 (BOMB.OVR 0x120D9) from O4:1AD0 on (after the dialog, which belongs to the turn),
/// docs/analyse/runde.md section 5.11 and docs/analyse/feld.md section 8.5: the mother flies like O4:01B7, splits at the
/// apex into five parts and every part explodes as weapon 7.
/// </summary>
public static class MirvShot
{
    private const int Parts = 5;

    // [ORIGINAL] Real48 constants of O4:13D4.
    private static readonly Real48 Ten = Real48.FromHex("840000000020");          // O4:1C47
    private static readonly Real48 Fifty = Real48.FromHex("860000000048");        // O4:1CFE
    private static readonly Real48 Gravity = Real48.FromHex("7DCDCCCCCC4C");      // 0.1, O4:2015
    private static readonly Real48 WindScale = Real48.FromHex("8D000000401C");    // 5000, O4:2070
    private static readonly Real48 StopSpeed = Gravity;                           // 0.1, O4:2A39
    private static readonly Real48 MaxSpeed = Real48.FromHex("850000000070");     // 30, O4:2C22
    private static readonly Real48 WarpWidth = Real48.FromHex("890000000016");    // 300, O4:2AB0
    private static readonly Real48 Six = Real48.FromHex("830000000040");          // O4:3048
    private static readonly Real48 Three = Real48.FromHex("820000000040");        // O4:3072
    private static readonly Real48 Spread = Real48.FromHex("810000000060");       // 1.75, O4:30FE

    /// <summary>
    /// Fires the MIRV of <paramref name="shooter"/> with the choice of the dialog. [ORIGINAL] O4:1AD0–3423: stock − 1,
    /// guidance off (+26h = 0), mother with the start values of O4:01B7 (Q3); per sub-step the parts that still fly (first
    /// the mother, entry 0, then entries 1..5) move like O4:01B7 (Q4) with repulsers (Q5) but without guidance, the
    /// left-shooter test, marks and trail; a part ends on terrain or a living tank (no power rule G2, no hit counter),
    /// above 30 in speed, after 1000 ticks, and without bouncing walls at y &lt; −5000 or y ≥ 190; bouncing walls end it
    /// on the floor when |vx| and |vy| are below 0.1. The mother also ends when it starts falling (vy &gt; 0); then the
    /// five parts start at its place with its speed, changed by the pattern. While parts fly the sub-steps per tick are
    /// SS div (parts still flying), rounded up. Detonation Impact explodes each part when it ends, Simultaneous all five at
    /// the end, None none; after each explosion all tanks are drawn again.
    /// </summary>
    public static void Fire(Playfield.Playfield field, PlayfieldTank shooter, MirvPattern pattern, MirvDetonation detonation,
        ShotSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels.AsSpan();
        var tanks = field.Tanks;
        var invisible = field.Options.InvisibleShots;

        shooter.Stock[shooter.Weapon] = unchecked((short)(shooter.Stock[shooter.Weapon] - 1));
        shooter.Guidance = 0;

        var x = new Real48[Parts + 1];
        var y = new Real48[Parts + 1];
        var vx = new Real48[Parts + 1];
        var vy = new Real48[Parts + 1];
        var ix = new int[Parts + 1];
        var iy = new int[Parts + 1];
        var ox = new int[Parts + 1];
        var oy = new int[Parts + 1];
        var color = new int[Parts + 1];
        var done = new bool[Parts + 1];
        var exploded = new bool[Parts + 1];

        var radians = Real48.DegreesToRadians(Real48.FromInt32(shooter.Angle));
        var direction = Real48.FromInt32(shooter.Direction);
        x[0] = Real48.FromInt32(shooter.X) + direction * (Real48.Cos(radians) * Ten);
        y[0] = Real48.FromInt32(shooter.Y) - Real48.Sin(radians) * Ten;
        var speed = Real48.FromInt32(shooter.Power) / Fifty;
        vx[0] = direction * (Real48.Cos(radians) * speed);
        vy[0] = BallisticShot.Negate(Real48.Sin(radians)) * speed;
        for (var i = 0; i <= Parts; i++)
        {
            ox[i] = -99;
            oy[i] = -99;
        }

        var subSteps = (short)settings.SubSteps;
        for (var i = 1; i <= tanks.Count; i++)
        {
            var (r, g, b) = Playfield.Playfield.ColorDivisors[i - 1];
            field.Palette.Set(100 - i, 32 / r, 32 / g, 32 / b);
        }

        field.Palette.Set(Playfield.Playfield.ShotColor, 63, 63, 63);
        var repulsers = tanks.Any(t => t.Active && t.Defense[Damage.Repulser] > 0);
        var windReal = Real48.FromInt32(settings.Wind);

        var subStep = (short)tanks.Count;
        var ticks = 0;
        var leftShooter = false;
        var split = false;
        int first = 0, last = 0;
        while (true)
        {
            // O4:1F04: sub-step counter DS:4740 against DS:4744.
            subStep++;
            if (subStep > subSteps)
            {
                subStep = 0;
                ticks++;
            }

            var ss = Real48.FromInt32(subSteps);
            for (var i = first; i <= last; i++)
            {
                if (done[i])
                    continue;
                x[i] = vx[i] / ss + x[i];
                y[i] = vy[i] / ss + y[i];
                vy[i] = Gravity / ss + vy[i];
                vx[i] = windReal / WindScale / ss + vx[i];
                ix[i] = (short)x[i].Trunc();
                iy[i] = (short)y[i].Trunc();
                if (!leftShooter && (BallisticShot.Abs16(iy[i] - shooter.Y) > 40 || BallisticShot.Abs16(ix[i] - shooter.X) > 40))
                    leftShooter = true;
                if (repulsers)
                    BallisticShot.Repel(tanks, shooter, leftShooter, ix[i], iy[i], ss, ref vx[i], ref vy[i]);

                // O4:24CA–25E4: old point, trail, marks.
                if (ox[i] > 11 && oy[i] > 31 && ox[i] < 309)
                {
                    if (!invisible)
                        BallisticShot.RestorePoint(background, screen, ox[i], oy[i]);
                    if (settings.Trail)
                        BallisticShot.TrailPoint(screen, background, ox[i], oy[i], (byte)(100 - shooter.Number));
                }

                if (ox[i] >= 9 && ox[i] <= 311 && !invisible)
                {
                    VgaDraw.PutPixel(screen, ox[i], 29, 0);
                    VgaDraw.PutPixel(screen, ox[i], 191, 0);
                }

                if (oy[i] >= 29 && oy[i] <= 191 && !invisible)
                {
                    VgaDraw.PutPixel(screen, 9, oy[i], 0);
                    VgaDraw.PutPixel(screen, 311, oy[i], 0);
                }

                ox[i] = ix[i];
                oy[i] = iy[i];

                if (ix[i] > 11 && iy[i] > 33 && ix[i] < 309 && iy[i] < 187 && !done[i])
                {
                    // O4:2654–270F: the four neighbours and the point.
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        for (var dy = -1; dy <= 1; dy++)
                        {
                            if (Math.Abs(dx) + Math.Abs(dy) > 1)
                                continue;
                            int c = screen[VgaDraw.Address(ix[i] + dx, iy[i] + dy)];
                            if (c == Playfield.Playfield.TerrainColor)
                            {
                                color[i] = c;
                                done[i] = true;
                            }

                            if (c is >= 20 and <= 29)
                            {
                                if (tanks[c - 20].Energy > 0)
                                    done[i] = true;
                                color[i] = c;
                            }
                        }
                    }
                }
                else if (field.WallBounces)
                {
                    // O4:271C–2A82.
                    var f = field.WallFactor!.Value;
                    if (ix[i] <= 10 && Real48.Compare(vx[i], Real48.Zero) < 0)
                    {
                        vx[i] = BallisticShot.Negate(vx[i]) * f;
                        vy[i] *= f;
                    }

                    if (ix[i] >= 310 && Real48.Compare(vx[i], Real48.Zero) > 0)
                    {
                        vx[i] = BallisticShot.Negate(vx[i]) * f;
                        vy[i] *= f;
                    }

                    if (iy[i] <= 30 && Real48.Compare(vy[i], Real48.Zero) < 0)
                    {
                        vy[i] = BallisticShot.Negate(vy[i]) * f;
                        vx[i] *= f;
                    }

                    if (iy[i] >= 188 && Real48.Compare(vy[i], Real48.Zero) > 0)
                    {
                        vx[i] *= f;
                        vy[i] = BallisticShot.Negate(vy[i]) * f;
                        if (Real48.Compare(BallisticShot.Abs(vx[i]), StopSpeed) < 0
                            && Real48.Compare(BallisticShot.Abs(vy[i]), StopSpeed) < 0)
                            done[i] = true;
                    }
                }
                else if (field.WallWarps)
                {
                    if (ix[i] <= 10)
                        x[i] += WarpWidth;
                    if (ix[i] >= 310)
                        x[i] -= WarpWidth;
                }
                else
                {
                    if (ix[i] > 310 && Real48.Compare(vx[i], Real48.One) > 0 && settings.Wind > 0)
                        y[i] = WarpWidth;
                    if (ix[i] < 10 && Real48.Compare(vx[i], Real48.One) < 0 && settings.Wind < 0)
                        y[i] = WarpWidth;
                }

                // O4:2BD2–2D0D: apex of the mother, speed limit, bottom and top, ticks.
                if (!split && Real48.Compare(vy[i], Real48.Zero) > 0)
                    done[i] = true;
                if (Real48.Compare(BallisticShot.Abs(vx[i]), MaxSpeed) > 0
                    || Real48.Compare(BallisticShot.Abs(vy[i]), MaxSpeed) > 0)
                    done[i] = true;
                if (!field.WallBounces)
                {
                    if (iy[i] < -5000)
                        done[i] = true;
                    if (iy[i] >= 190)
                        done[i] = true;
                }

                if (ticks > 1000)
                    done[i] = true;

                // O4:2D0D–2E3A: new point and marks, or the point taken back.
                if (!done[i] && !invisible)
                {
                    if (ix[i] > 11 && iy[i] > 31 && ix[i] < 309)
                        BallisticShot.DrawPoint(screen, ix[i], iy[i]);
                    if (ix[i] >= 9 && ix[i] <= 311)
                    {
                        VgaDraw.PutPixel(screen, ix[i], 29, Playfield.Playfield.FrameColor);
                        VgaDraw.PutPixel(screen, ix[i], 191, Playfield.Playfield.FrameColor);
                    }

                    if (iy[i] >= 29 && iy[i] <= 191)
                    {
                        VgaDraw.PutPixel(screen, 9, iy[i], Playfield.Playfield.FrameColor);
                        VgaDraw.PutPixel(screen, 311, iy[i], Playfield.Playfield.FrameColor);
                    }
                }
                else if (ix[i] > 0 && ix[i] < 320 && iy[i] > 0 && iy[i] < 200)
                {
                    BallisticShot.RestorePoint(background, screen, ix[i], iy[i]);
                }

                // O4:2E3A–2ED7: detonation on impact.
                if (detonation == MirvDetonation.Impact && split && done[i] && !exploded[i])
                {
                    exploded[i] = true;
                    Impact.Run(field, shooter, color[i], x[i], y[i], settings, random);
                    foreach (var tank in tanks)
                        field.DrawTank(tank);
                    screen = field.Screen.Pixels.AsSpan();
                }
            }

            if (subStep == subSteps)
                settings.Frame?.Invoke(ShotFrame.Flight);

            // O4:2EE6–2F6F: parts still flying (entries 0..5), end of the phase, sub-steps per tick.
            var flying = done.Count(d => !d);
            var end = (!split && flying == Parts) || (split && flying == 0);
            if (split && flying > 0)
            {
                subSteps = (short)(settings.SubSteps / flying);
                if (settings.SubSteps % flying != 0)
                    subSteps++;
            }

            if (subSteps < 1)
                subSteps = 1;
            if (!end)
                continue;
            if (split)
                break;

            // O4:2F83–3383: split.
            first = 1;
            last = Parts;
            for (var i = 1; i <= Parts; i++)
            {
                done[i] = false;
                color[i] = 0;
                x[i] = x[0];
                y[i] = y[0];
                vx[i] = vx[0];
                vy[i] = vy[0];
                if (pattern == MirvPattern.Random)
                {
                    vx[i] = random.NextReal() * Six + vx[i] - Three;
                    vy[i] = random.NextReal() * Six + vy[i] - Three;
                }

                if (pattern == MirvPattern.Horizontal)
                    vx[i] = Real48.FromInt32(i - 3) * Spread + vx[i];
                if (pattern == MirvPattern.Vertical)
                    vy[i] = Real48.FromInt32(i - 3) * Spread + vy[i];
                ox[i] = ox[0];
                oy[i] = oy[0];
            }

            if (pattern == MirvPattern.FourWay)
            {
                vx[1] += Spread;
                vx[2] -= Spread;
                vy[3] -= Spread;
                vy[4] += Spread;
            }

            if (pattern == MirvPattern.XFourWay)
            {
                vx[1] += Spread;
                vy[1] += Spread;
                vx[2] -= Spread;
                vy[2] -= Spread;
                vx[3] += Spread;
                vy[3] -= Spread;
                vx[4] -= Spread;
                vy[4] += Spread;
            }

            split = true;
        }

        if (detonation == MirvDetonation.Simultaneous)
        {
            for (var i = 1; i <= Parts; i++)
            {
                Impact.Run(field, shooter, color[i], x[i], y[i], settings, random);
                foreach (var tank in tanks)
                    field.DrawTank(tank);
            }
        }
    }
}
