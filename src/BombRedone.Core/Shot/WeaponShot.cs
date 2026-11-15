using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>The shot of the weapon chosen by the shooter, as O4:44A7 picks it (docs/analyse/runde.md section 5.1 Q1).</summary>
public static class WeaponShot
{
    /// <summary>
    /// [ORIGINAL] O4:4977–49C8 (BOMB.OVR 0x1567C): weapon 5 → laser O4:3426, 7 → MIRV O4:13D4 with the choice of its
    /// dialog (<paramref name="pattern"/>, <paramref name="detonation"/>; the dialog itself belongs to the turn), otherwise
    /// O4:01B7.
    /// </summary>
    public static void Fire(Playfield.Playfield field, PlayfieldTank shooter, ShotSettings settings, TurboPascalRandom random,
        MirvPattern pattern = MirvPattern.Random, MirvDetonation detonation = MirvDetonation.Impact)
    {
        ArgumentNullException.ThrowIfNull(shooter);
        switch (shooter.Weapon)
        {
            case 5:
                LaserShot.Fire(field, shooter, settings, random);
                break;
            case 7:
                MirvShot.Fire(field, shooter, pattern, detonation, settings, random);
                break;
            default:
                BallisticShot.Fire(field, shooter, settings, random);
                break;
        }
    }
}
