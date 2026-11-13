namespace BombRedone.Core.Playfield;

/// <summary>
/// The fields of a player record (DS:402E + 116 · n) that the set-up of the playfield writes
/// (docs/analyse/runde.md section 3.6, docs/formate/spielstand.md).
/// </summary>
public sealed class PlayfieldTank
{
    internal PlayfieldTank(int number) => Number = number;

    /// <summary>Player number 1..10 (record +00h); colour 19 + n.</summary>
    public int Number { get; }

    /// <summary>DAC entry and drawing colour of the tank.</summary>
    public byte Color => (byte)(Playfield.PlayerColorBase + Number);

    /// <summary>x of the middle of the bottom row (+0Fh).</summary>
    public int X { get; internal set; }

    /// <summary>y of the bottom row (+11h).</summary>
    public int Y { get; internal set; }

    /// <summary>Barrel angle in degrees (+1Dh).</summary>
    public int Angle { get; internal set; }

    /// <summary>Power (+1Bh).</summary>
    public int Power { get; internal set; }

    /// <summary>+1 or −1 (+1Fh): the barrel points to the right for +1.</summary>
    public int Direction { get; internal set; }

    /// <summary>Energy in percent (+21h); the brightness of the colour (O10:01AC).</summary>
    public int Energy { get; internal set; }

    /// <summary>Still in the round (+23h).</summary>
    public bool Active { get; internal set; }

    /// <summary>Word +5Eh, set to 0 (meaning not derived, docs/formate/spielstand.md).</summary>
    public int Field5E { get; internal set; }
}
