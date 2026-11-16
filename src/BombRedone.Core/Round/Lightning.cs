using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;

namespace BombRedone.Core.Round;

/// <summary>The lightning O6:14C5 (docs/analyse/runde.md section 5.9 K13).</summary>
public static class Lightning
{
    /// <summary>Colour of the bolt (DAC 200 = white) and of its edge (DAC 201 = (0, 0, 50)).</summary>
    public const byte BoltColor = 200, EdgeColor = 201;

    // [ORIGINAL] O6:1543: 189 (Real48 88 00 00 00 00 3D).
    private static readonly Real48 Bottom = Real48.FromInt32(189);

    /// <summary>
    /// [ORIGINAL] O6:14C5(0) (BOMB.OVR 0x1A3F1), called by O4:4D9A: DAC 200 = (63, 63, 63), 201 = (0, 0, 50); top
    /// x₀ = Random(300) + 10, bottom x₁ = Random(300) + 10, slope s = (x₁ − x₀) / 189; path for y = 31..189 with
    /// o += Random(7) − 3: x(y) = x₀ + Trunc(((y − 189) · o as 16-bit product) / 189 + y · s); Random(8) + 1 bolts, each:
    /// per y the pixel on the path (a tank 20..29 takes 3 damage on its shield, O10:0000, and is drawn again), the path in 200
    /// with 201 left and right, and the next path with a new o (x₀ + Trunc(o · ((y − 189) / 189) + y · s)); then the old
    /// path from the background (sound Random(600) + 600). Record 0 gets weapon (bolts div 3), 0 → 11, and O6:00E0(record 0,
    /// x₁, 189, colour 0) explodes; then all tanks U431:07BA, Crumble &gt; 0 → O7:03C1, Fall down → O7:0020.
    /// </summary>
    public static void Run(Playfield.Playfield field, PlayfieldTank record0, RoundSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(record0);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        var screen = field.Screen.Pixels.AsSpan();
        var background = field.Background.Pixels;
        field.Palette.Set(BoltColor, 63, 63, 63);
        field.Palette.Set(EdgeColor, 0, 0, 50);
        var x0 = (short)(random.Next(300) + 10);
        var x1 = (short)(random.Next(300) + 10);
        var slope = Real48.FromInt32((short)(x1 - x0)) / Bottom;
        var paths = new short[2, 190];
        var current = 1;
        short offset = 0;
        for (var y = 31; y <= 189; y++)
        {
            offset = (short)(offset + random.Next(7) - 3);
            var product = (short)(ushort)((ushort)(y - 189) * (ushort)offset);
            var t = Real48.FromInt32(y) * slope + Real48.FromInt32(product) / Bottom;
            paths[current, y] = (short)(x0 + (short)t.Trunc());
        }

        var bolts = random.Next(8) + 1;
        for (var k = 1; k <= bolts; k++)
        {
            offset = 0;
            for (var y = 31; y <= 189; y++)
            {
                var x = paths[current, y];
                var color = Pixels.Get(screen, x, y);
                if (color is >= 20 and <= 29)
                {
                    var tank = field.Tanks[color - 20];
                    Damage.Apply(field, tank, 3, Damage.Shield, random);
                    field.DrawTank(tank);
                }

                Pixels.Put(screen, x, y, BoltColor);
                Pixels.Put(screen, x + 1, y, EdgeColor);
                Pixels.Put(screen, x - 1, y, EdgeColor);
                offset = (short)(offset + random.Next(7) - 3);
                var t = Real48.FromInt32(y) * slope + Real48.FromInt32(offset) * (Real48.FromInt32(y - 189) / Bottom);
                paths[1 - current, y] = (short)(x0 + (short)t.Trunc());
            }

            settings.Frame?.Invoke(ShotFrame.Flight);
            for (var y = 31; y <= 189; y++)
            {
                var x = paths[current, y];
                Pixels.Copy(background, screen, x, y);
                Pixels.Copy(background, screen, x + 1, y);
                Pixels.Copy(background, screen, x - 1, y);
                if (settings.Sound)
                    random.Next(600);
            }

            current = 1 - current;
        }

        record0.Weapon = bolts / 3 == 0 ? 11 : bolts / 3;
        Impact.Run(field, record0, 0, Real48.FromInt32(x1), Bottom, settings.Shot, random);
        foreach (var t in field.Tanks)
            field.DrawTank(t);
        if (settings.Crumble > 0)
            AfterShot.Crumble(field, settings.Crumble, random, settings.Frame);
        if (settings.FallDown)
            AfterShot.Fall(field, random, settings.Frame);
    }
}
