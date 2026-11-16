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
    public static bool IsSupported(int choice) => choice == 5;

    /// <summary>Runs picture <paramref name="choice"/> (0..6) for the dead <paramref name="tank"/>.</summary>
    public static void Run(int choice, Playfield.Playfield field, PlayfieldTank tank, RoundSettings settings,
        TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        switch (choice)
        {
            case 5:
                Fade(field, tank, settings);
                break;
            default:
                throw new NotSupportedException($"Death picture {choice} is not ported yet.");
        }
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
