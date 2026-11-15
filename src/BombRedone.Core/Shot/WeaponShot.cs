using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>The shot of the weapon chosen by the shooter, as O4:44A7 picks it (docs/analyse/runde.md section 5.1 Q1).</summary>
public static class WeaponShot
{
    /// <summary>
    /// [ORIGINAL] O4:4977–49C8 (BOMB.OVR 0x1567C): weapon 5 → laser O4:3426, otherwise O4:01B7. The MIRV (7, O4:13D4)
    /// follows in spur waffen part 5 and still throws <see cref="NotSupportedException"/>.
    /// </summary>
    public static void Fire(Playfield.Playfield field, PlayfieldTank shooter, ShotSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(shooter);
        switch (shooter.Weapon)
        {
            case 5:
                LaserShot.Fire(field, shooter, settings, random);
                break;
            case 7:
                throw new NotSupportedException("The MIRV (O4:13D4) is not ported yet (spur waffen part 5).");
            default:
                BallisticShot.Fire(field, shooter, settings, random);
                break;
        }
    }
}
