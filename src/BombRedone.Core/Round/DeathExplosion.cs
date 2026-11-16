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
    public static bool IsSupported(int choice) => choice is 4 or 5 or 6;

    /// <summary>
    /// Runs picture <paramref name="choice"/> (0..6) for the dead <paramref name="tank"/>. <paramref name="record0"/> is
    /// record 0 (DS:402E), the shooter of the explosions of O5:29A0 and of the lightning.
    /// </summary>
    public static void Run(int choice, Playfield.Playfield field, PlayfieldTank tank, PlayfieldTank record0,
        RoundSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(record0);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        switch (choice)
        {
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
