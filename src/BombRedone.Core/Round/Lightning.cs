using BombRedone.Core.Numerics;

namespace BombRedone.Core.Round;

/// <summary>The lightning O6:14C5 (docs/analyse/runde.md section 5.9 K13).</summary>
public static class Lightning
{
    /// <summary>Whether the lightning is ported (part 3 of spur runde).</summary>
    public static bool IsSupported => false;

    /// <summary>[ORIGINAL] O6:14C5(0) (BOMB.OVR 0x1A3F1), called by O4:4D9A.</summary>
    public static void Run(Playfield.Playfield field, RoundSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        throw new NotSupportedException("The lightning is not ported yet.");
    }
}
