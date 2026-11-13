using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>Damage and points of a player record (O10, docs/analyse/runde.md section 5.5 Q11).</summary>
public static class Damage
{
    /// <summary>Index of the defences in <see cref="PlayfieldTank.Defense"/>.</summary>
    public const int Shield = 0;

    public const int Inertia = 1;

    public const int Repulser = 2;

    // [ORIGINAL] O10:00E7: 40 (Real48 86 00 00 00 00 20).
    private static readonly Real48 Forty = Real48.FromInt32(40);

    /// <summary>
    /// Damage <paramref name="amount"/> against defence <paramref name="defense"/> first, the rest from the energy.
    /// [ORIGINAL] O10:0000 (BOMB.OVR 0x1F123): defence (LongInt) −= amount; if it is now negative, energy (word) += defence
    /// (not below 0); for a defence other than inertia and a repulser &gt; 0, Random:Real &lt; −defence / 40 takes one
    /// repulser; defence := 0 and the colour of the tank is updated (O10:014A). Returns the energy lost.
    /// </summary>
    public static int Apply(Playfield.Playfield field, PlayfieldTank tank, int amount, int defense, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(random);
        var before = tank.Energy;
        tank.Defense[defense] = unchecked(tank.Defense[defense] - (short)amount);
        if (tank.Defense[defense] < 0)
        {
            var energy = unchecked((short)(tank.Energy + tank.Defense[defense]));
            tank.Energy = energy < 0 ? 0 : energy;
            if (defense != Inertia && tank.Defense[Repulser] > 0)
            {
                var chance = Real48.FromInt32(unchecked(-tank.Defense[defense])) / Forty;
                if (Real48.Compare(random.NextReal(), chance) < 0)
                    tank.Defense[Repulser]--;
            }

            tank.Defense[defense] = 0;
            field.UpdateColor(tank);
        }

        return unchecked((short)(before - tank.Energy));
    }

    /// <summary>Points. [ORIGINAL] O10:018B (BOMB.OVR 0x1F2AE): +13h (LongInt) += the word, sign-extended.</summary>
    public static void AddPoints(PlayfieldTank tank, int points)
    {
        ArgumentNullException.ThrowIfNull(tank);
        tank.Score = unchecked(tank.Score + (short)points);
    }
}
