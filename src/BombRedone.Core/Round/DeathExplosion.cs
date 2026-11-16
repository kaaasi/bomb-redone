using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Round;

/// <summary>
/// The death pictures O5 (docs/analyse/runde.md section 5.9 K15, K16; probe docs/analyse/feld.md section 9). The round loop
/// picks one with Random(7): 0 O5:001F, 1 O5:0DA0(0), 2 O5:1BDE, 3 O5:0DA0(1), 4 O5:2662, 5 O5:28DE, 6 O5:29A0.
/// </summary>
public static class DeathExplosion
{
    /// <summary>Number of pictures the Random(7) of O4:4C10 chooses from.</summary>
    public const int Choices = 7;

    /// <summary>Whether <paramref name="choice"/> is ported (the others follow with part 3 of spur runde).</summary>
    public static bool IsSupported(int choice) => choice is 0 or 2 or 4 or 5 or 6;

    /// <summary>
    /// Runs picture <paramref name="choice"/> (0..6) for the dead <paramref name="tank"/>. <paramref name="record0"/> is
    /// record 0 (DS:402E), the shooter of the explosions of O5:29A0 and of the lightning.
    /// </summary>
    /// <returns>The shot state x, y (DS:4796, DS:479C) if the picture changed it (O5:001F), otherwise null.</returns>
    public static (Real48 X, Real48 Y)? Run(int choice, Playfield.Playfield field, PlayfieldTank tank, PlayfieldTank record0,
        RoundSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(record0);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        switch (choice)
        {
            case 0:
                return Splinters(field, tank, settings, random);
            case 2:
                Cluster(field, tank, settings, random);
                break;
            case 4:
                Fountain(field, tank, settings, random);
                break;
            case 5:
                Fade(field, tank, settings);
                break;
            case 6:
                Blast(field, tank, record0, settings, random);
                break;
            default:
                throw new NotSupportedException($"Death picture {choice} is not ported yet.");
        }

        return null;
    }

    // [ORIGINAL] Real48 constants of O5:001F: 4, 1, 63 (O5:0049–0067), 0.5, 0.1, 0.007, 5000 (O5:028A–0348).
    private static readonly Real48 Four = Real48.FromInt32(4);
    private static readonly Real48 One = Real48.FromInt32(1);
    private static readonly Real48 SixtyThree = Real48.FromInt32(63);
    private static readonly Real48 Half = Real48.FromHex("800000000000");
    private static readonly Real48 Tenth = Real48.FromHex("7DCDCCCCCC4C");
    private static readonly Real48 Gravity = Real48.FromHex("793789416065");
    private static readonly Real48 WindScale = Real48.FromInt32(5000);
    private static readonly Viewport Window = new(11, 31, 309, 189, true);

    /// <summary>
    /// [ORIGINAL] O5:0000 (BOMB.OVR 0x1613C): SetPal(colour, 1, Ptr(0, Random(65535))) – the colour of the dead tank takes three
    /// bytes of low memory. [APPROX] Those bytes depend on the machine; here the Random value itself (6 bits per component).
    /// Every later write of the colour replaces it (O5:001F ends with SetRGB of that colour).
    /// </summary>
    private static void Flicker(Playfield.Playfield field, byte color, TurboPascalRandom random)
    {
        var r = random.Next(65535);
        field.Palette.Set(color, r & 63, (r >> 6) & 63, (r >> 12) & 15);
    }

    /// <summary>
    /// [ORIGINAL] O5:001F (BOMB.OVR 0x1615B), the splinters: ramps 130..134 red, 140..144 green, 150..154 blue
    /// (Trunc((1 − i/4) · 63)), 104 flickers of the tank colour (O5:0000); window 11..309 × 31..189, the record's x, y relative
    /// to it; 11 splinters from the tank: vx = 0.5 − Random:Real, vy = 0.1 − Random:Real in the shot state DS:4796–47AC, per step
    /// x + vx, y + vy, vy + 0.007, vx := Wind / 5000 + vx; Graph.GetPixel: terrain or frame ends it, a tank 20..29 takes
    /// Random(10) + 5 on its shield; outside 0..320 × 0..200 ends it; flicker, Graph.PutPixel in the tank colour. At its end
    /// a burst of k (Random(10): 1, 1, 1, 2, 2, 2, 3, 3, 4, 5) · 4 + 1 rings in colour 100 + (Random(3) + 3) · 10 + j div k
    /// and back from the background; then 15 double rings around the tank (colours 130 + s, 140 + s, 150 + s); the ramps
    /// fade with the tank colour = Random(63) · n div 16 per component; 31 rings from the background; window back, the
    /// record's x, y back, all tanks U431:07BA, the tank colour and colours 130..159 from the background (U431:05C4,
    /// U431:0594).
    /// </summary>
    private static (Real48 X, Real48 Y) Splinters(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings,
        TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        var c = tank.Color;
        Ramps(field, c, settings, random);
        tank.X -= Window.X1;
        tank.Y -= Window.Y1;
        Real48 x = default, y = default;
        var wind = Real48.FromInt32(settings.Wind) / WindScale;
        for (var s = 0; s <= 10; s++)
        {
            if (settings.Sound)
                random.Next(500);
            x = Real48.FromInt32(tank.X);
            y = Real48.FromInt32(tank.Y);
            var vx = Half - random.NextReal();
            var vy = Tenth - random.NextReal();
            short ix, iy;
            bool stop;
            do
            {
                stop = false;
                x += vx;
                y += vy;
                vy += Gravity;
                vx = wind + vx;
                ix = (short)x.Trunc();
                iy = (short)y.Trunc();
                var p = GraphGetPixel(screen, ix, iy);
                if (p is Playfield.Playfield.TerrainColor or Playfield.Playfield.FrameColor)
                    stop = true;
                if (p is >= 20 and <= 29)
                    Shot.Damage.Apply(field, field.Tanks[p - 20], random.Next(10) + 5, Shot.Damage.Shield, random);
                if (ix > 320 || ix < 0 || iy > 200 || iy < 0)
                    stop = true;
                Flicker(field, c, random);
                BgiDraw.PutPixel(screen, IndexBuffer.Width, IndexBuffer.Height, Window, ix, iy, c);
            }
            while (!stop);

            settings.Frame?.Invoke(Shot.ShotFrame.Flight);
            var tone = (random.Next(3) + 3) * 10;
            var k = BurstSize(random);
            for (var j = 0; j <= 4 * k; j++)
            {
                if (settings.Sound)
                    random.Next(2500);
                var color = (byte)(tone + 100 + j / k);
                VgaDraw.Ellipse(screen, Window, ix, iy, j, j, color);
                VgaDraw.Ellipse(screen, Window, ix + 1, iy, j, j, color);
                settings.Frame?.Invoke(Shot.ShotFrame.Explosion);
                Flicker(field, c, random);
            }

            for (var j = 4 * k; j >= 0; j--)
            {
                if (settings.Sound)
                    random.Next(2500);
                VgaDraw.CopyEllipse(background, screen, Window, ix, iy, j, j);
                VgaDraw.CopyEllipse(background, screen, Window, ix + 1, iy, j, j);
                Flicker(field, c, random);
                settings.Frame?.Invoke(Shot.ShotFrame.Crater);
            }
        }

        Rings(field, tank, settings, random, twin: true, 600, 100);
        Finish(field, tank, settings, 159);
        return (x, y);
    }

    /// <summary>
    /// [ORIGINAL] O5:1BDE (BOMB.OVR 0x17D1A), the splinter cluster: like <see cref="Splinters"/>, but the 11 splinters fly
    /// together (one flicker per pass over all, ends after a pass without a moving splinter), a tank takes Random(10) + 10,
    /// the window test is 0..298 × 0..158, the shot state stays; then the bursts at each splinter's end, 15 single rings around
    /// the tank, the fade with sound Random(200), and at the end colours 130..154 from the background.
    /// </summary>
    private static void Cluster(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings,
        TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        var c = tank.Color;
        Ramps(field, c, settings, random);
        tank.X -= Window.X1;
        tank.Y -= Window.Y1;
        const int count = 11;
        var x = new Real48[count];
        var y = new Real48[count];
        var vx = new Real48[count];
        var vy = new Real48[count];
        var done = new bool[count];
        var endX = new short[count];
        var endY = new short[count];
        for (var s = 0; s < count; s++)
        {
            if (settings.Sound)
                random.Next(500);
            x[s] = Real48.FromInt32(tank.X);
            y[s] = Real48.FromInt32(tank.Y);
            vx[s] = Half - random.NextReal();
            vy[s] = Tenth - random.NextReal();
        }

        var wind = Real48.FromInt32(settings.Wind) / WindScale;
        bool idle;
        do
        {
            idle = true;
            Flicker(field, c, random);
            for (var s = 0; s < count; s++)
            {
                if (done[s])
                    continue;
                x[s] += vx[s];
                y[s] += vy[s];
                vy[s] += Gravity;
                vx[s] = wind + vx[s];
                var ix = (short)x[s].Trunc();
                var iy = (short)y[s].Trunc();
                idle = false;
                var p = GraphGetPixel(screen, ix, iy);
                if (p is Playfield.Playfield.TerrainColor or Playfield.Playfield.FrameColor)
                    done[s] = true;
                if (p is >= 20 and <= 29)
                    Shot.Damage.Apply(field, field.Tanks[p - 20], random.Next(10) + 10, Shot.Damage.Shield, random);
                if (ix > 298 || ix < 0 || iy > 158 || iy < 0)
                    done[s] = true;
                BgiDraw.PutPixel(screen, IndexBuffer.Width, IndexBuffer.Height, Window, ix, iy, c);
                if (done[s])
                {
                    endX[s] = ix;
                    endY[s] = iy;
                }
            }

            settings.Frame?.Invoke(Shot.ShotFrame.Flight);
        }
        while (!idle);

        for (var s = 0; s < count; s++)
        {
            var tone = (random.Next(3) + 3) * 10;
            var k = BurstSize(random);
            for (var j = 0; j <= 4 * k; j++)
            {
                if (settings.Sound)
                    random.Next(2500);
                Flicker(field, c, random);
                var color = (byte)(tone + 100 + j / k);
                VgaDraw.Ellipse(screen, Window, endX[s], endY[s], j, j, color);
                VgaDraw.Ellipse(screen, Window, endX[s] + 1, endY[s], j, j, color);
                settings.Frame?.Invoke(Shot.ShotFrame.Explosion);
            }

            for (var j = 4 * k; j >= 0; j--)
            {
                if (settings.Sound)
                    random.Next(2500);
                Flicker(field, c, random);
                VgaDraw.CopyEllipse(background, screen, Window, endX[s], endY[s], j, j);
                VgaDraw.CopyEllipse(background, screen, Window, endX[s] + 1, endY[s], j, j);
                settings.Frame?.Invoke(Shot.ShotFrame.Crater);
            }
        }

        Rings(field, tank, settings, random, twin: false, 200, 0);
        Finish(field, tank, settings, 154);
    }

    /// <summary>Random(10) → 1 (0–2), 2 (3–5), 3 (6–7), 4 (8), 5 (9): rings of a burst div 4.</summary>
    private static int BurstSize(TurboPascalRandom random) => random.Next(10) switch
    {
        <= 2 => 1,
        <= 5 => 2,
        <= 7 => 3,
        8 => 4,
        _ => 5,
    };

    /// <summary>O5:0022–01CB (and O5:1BE1–1D8B): the three ramps and 104 flickers of the tank colour.</summary>
    private static void Ramps(Playfield.Playfield field, byte c, RoundSettings settings, TurboPascalRandom random)
    {
        for (var ramp = 0; ramp < 3; ramp++)
        {
            for (var i = 0; i <= 4; i++)
            {
                var v = ((One - Real48.FromInt32(i) / Four) * SixtyThree).Trunc();
                field.Palette.Set(130 + 10 * ramp + i, ramp == 0 ? v : 0, ramp == 1 ? v : 0, ramp == 2 ? v : 0);
            }

            if (settings.Sound)
                random.Next(500);
            Flicker(field, c, random);
        }

        for (var i = 0; i <= 100; i++)
        {
            if (settings.Sound)
                random.Next(500);
            Flicker(field, c, random);
        }
    }

    /// <summary>
    /// O5:0640–08CC (O5:2317–253F): 15 ring pairs around the tank (radius n and n + 1, colours 130 + s, 140 + s, 150 + s;
    /// <paramref name="twin"/>: also at x + 1), then the fade: the tank colour Random(63) · n div 16 per component and the
    /// ramps 63 · t div 4; with sound Random(<paramref name="toneBase"/> + <paramref name="toneStep"/> · t).
    /// </summary>
    private static void Rings(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings, TurboPascalRandom random,
        bool twin, int toneBase, int toneStep)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var palette = field.Palette;
        var c = tank.Color;
        var n = 0;
        for (var s = 0; s <= 4; s++)
        {
            for (var t = 3; t <= 5; t++)
            {
                n++;
                var color = (byte)(10 * t + 100 + s);
                for (var ring = n; ring <= n + 1; ring++)
                {
                    VgaDraw.Ellipse(screen, Window, tank.X, tank.Y, ring, ring, color);
                    if (twin)
                        VgaDraw.Ellipse(screen, Window, tank.X + 1, tank.Y, ring, ring, color);
                    for (var q = 0; q <= 4; q++)
                    {
                        if (settings.Sound)
                            random.Next((ushort)(1300 - 40 * n));
                        Flicker(field, c, random);
                    }
                }

                n++;
                settings.Frame?.Invoke(Shot.ShotFrame.Explosion);
            }
        }

        n = 16;
        for (var s = 4; s >= 0; s--)
        {
            for (var t = 4 - s; t >= 0; t--)
            {
                n--;
                if (settings.Sound)
                    random.Next((ushort)(toneBase + toneStep * t));
                var r = random.Next(63) * n / 16;
                var g = random.Next(63) * n / 16;
                var b = random.Next(63) * n / 16;
                palette.Set(c, r, g, b);
                var v = 63 * t / 4;
                palette.Set(130 + s, v, 0, 0);
                palette.Set(140 + s, 0, v, 0);
                palette.Set(150 + s, 0, 0, v);
                settings.Frame?.Invoke(Shot.ShotFrame.Palette);
            }
        }
    }

    /// <summary>
    /// O5:08D1–09E9: 31 ring pairs from the background, window back, the record's x, y back, all tanks U431:07BA, the tank
    /// colour and colours 130..<paramref name="last"/> from the background (U431:05C4, U431:0594).
    /// </summary>
    private static void Finish(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings, byte last)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        for (var r = 30; r >= 0; r--)
        {
            VgaDraw.CopyEllipse(background, screen, Window, tank.X, tank.Y, r, r);
            VgaDraw.CopyEllipse(background, screen, Window, tank.X + 1, tank.Y, r, r);
        }

        tank.X += Window.X1;
        tank.Y += Window.Y1;
        foreach (var t in field.Tanks)
            field.DrawTank(t);
        Shot.LaserShot.RestoreColor(screen, background, tank.Color, tank.Color);
        Shot.LaserShot.RestoreColor(screen, background, 130, last);
        settings.Frame?.Invoke(Shot.ShotFrame.Crater);
    }

    /// <summary>
    /// Graph.GetPixel (0E26:1FCC, driver function 2Eh): the pixel at the window origin + (x, y), not clipped.
    /// [APPROX] address (y + 31) · 320 + x + 11 in 16 bits, 0 beyond the 64000 screen bytes (the driver is not disassembled).
    /// </summary>
    private static byte GraphGetPixel(ReadOnlySpan<byte> screen, int x, int y)
    {
        var a = (ushort)((y + Window.Y1) * IndexBuffer.Width + x + Window.X1);
        return a < IndexBuffer.Size ? screen[a] : (byte)0;
    }

    // [ORIGINAL] O5:26D8 and O5:270A: 2 and 5 (Real48 82 00 00 00 00 00, 83 00 00 00 00 20).
    private static readonly Real48 Two = Real48.FromInt32(2);
    private static readonly Real48 Five = Real48.FromInt32(5);

    /// <summary>
    /// [ORIGINAL] O5:2662 (BOMB.OVR 0x1879E): k = 1..Random(30) + 30 rings; ring k: m = Random(400) grains, grain j:
    /// a = Random(10 · k), dx = Trunc(Random(a + 10) − a / 2 − 5), at (x + dx, y − a), mirrored into the window 11..309 ×
    /// 31..189 (x &gt; 309 → 618 − x, y &gt; 189 → 378 − y, y &lt; 31 → 62 − y, x &lt; 11 → 22 − x, until inside) as terrain
    /// (UCC8:0BB6, colour 2), with sound Sound(Random(1000) + 100 · k); then all tanks U431:07BA and the dead tank away
    /// (U431:08D7).
    /// </summary>
    private static void Fountain(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings,
        TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var rings = random.Next(30) + 30;
        for (var k = 1; k <= rings; k++)
        {
            var spread = (ushort)(10 * k);
            var grains = random.Next(400);
            for (var j = 1; j <= grains; j++)
            {
                var a = (short)random.Next(spread);
                var r = Real48.FromInt32(random.Next((ushort)(a + 10)));
                var dx = (short)(r - Real48.FromInt32(a) / Two - Five).Trunc();
                int y = (short)(tank.Y - a), x = (short)(tank.X + dx);
                while (x > 309 || y < 31 || x < 11 || y > 189)
                {
                    if (x > 309)
                        x = (short)(618 - x);
                    if (y > 189)
                        y = (short)(378 - y);
                    if (y < 31)
                        y = (short)(62 - y);
                    if (x < 11)
                        x = (short)(22 - x);
                }

                Pixels.Put(screen, x, y, Playfield.Playfield.TerrainColor);
                if (settings.Sound)
                    random.Next(1000);
            }

            settings.Frame?.Invoke(Shot.ShotFrame.Explosion);
        }

        foreach (var t in field.Tanks)
            field.DrawTank(t);
        Erase(field, tank);
    }

    /// <summary>
    /// [ORIGINAL] O5:29A0 (BOMB.OVR 0x18ADC): delay = ES, 0 if Random(2) = 1; tank U431:07BA; three times energy 0..100, then
    /// 100..1 (O10:0171, Delay); the record is copied to record 0; Random(10) → k = 1 (0–1), 2 (2–4), 3 (5–7), 4 (8–9);
    /// for w = 1..k record 0 gets weapon w and O6:00E0(record 0, x, y, colour 2) explodes at the tank, then all tanks
    /// U431:07BA; NoSound; energy −1.
    /// </summary>
    private static void Blast(Playfield.Playfield field, PlayfieldTank tank, PlayfieldTank record0, RoundSettings settings,
        TurboPascalRandom random)
    {
        random.Next(2);  // only the delay (ES or 0) depends on it
        field.DrawTank(tank);
        for (var n = 1; n <= 3; n++)
        {
            for (var i = 0; i <= 100; i++)
                SetEnergy(field, tank, i, settings);
        }

        for (var i = 100; i >= 1; i--)
            SetEnergy(field, tank, i, settings);
        record0.CopyFrom(tank);
        var value = random.Next(10);
        var count = value switch
        {
            <= 1 => 1,
            <= 4 => 2,
            <= 7 => 3,
            _ => 4,
        };
        var shot = settings.Shot;
        for (var w = 1; w <= count; w++)
        {
            record0.Weapon = w;
            Shot.Impact.Run(field, record0, Playfield.Playfield.TerrainColor, Real48.FromInt32(record0.X),
                Real48.FromInt32(record0.Y), shot, random);
            foreach (var t in field.Tanks)
                field.DrawTank(t);
        }

        tank.Energy = -1;
        field.UpdateColor(tank);
    }

    /// <summary>
    /// [ORIGINAL] O5:28DE (BOMB.OVR 0x18A1A): tank U431:07BA; energy 0..100 and back to 0 through O10:0171 (energy := i, colour
    /// O10:014A), each with Delay(ES) (and Sound(2000 + 100 · i) with sound); NoSound; energy −1 (colour black); the tank
    /// is taken away U431:08D7 (body U431:0685 and barrel UCC8:0AC0 from the background). No Random.
    /// </summary>
    private static void Fade(Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings)
    {
        field.DrawTank(tank);
        for (var i = 0; i <= 100; i++)
            SetEnergy(field, tank, i, settings);
        for (var i = 100; i >= 0; i--)
            SetEnergy(field, tank, i, settings);
        tank.Energy = -1;
        field.UpdateColor(tank);
        Erase(field, tank);
    }

    private static void SetEnergy(Playfield.Playfield field, PlayfieldTank tank, int energy, RoundSettings settings)
    {
        tank.Energy = energy;
        field.UpdateColor(tank);
        settings.Frame?.Invoke(Shot.ShotFrame.Palette);
    }

    /// <summary>[ORIGINAL] U431:08D7 (BOMB.EXE 0x06287): body and barrel of the tank from the background buffer.</summary>
    internal static void Erase(Playfield.Playfield field, PlayfieldTank tank)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        VgaDraw.CopyTankBody(background, screen, tank.X, tank.Y);
        var (bx, by) = Playfield.Playfield.BarrelOffset(tank.Angle);
        VgaDraw.CopyLine(background, screen, tank.X, tank.Y, tank.X + bx * tank.Direction, tank.Y - by);
    }
}
