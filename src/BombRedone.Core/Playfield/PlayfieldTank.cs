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

    // Fields used by the shot (spur schuss, docs/analyse/runde.md sections 5.5 and 5.6). They belong to the player record
    // and survive rounds; until the game flow exists (round 3) a playfield starts them like O2:0216 without team play.

    /// <summary>Team (+66h); without team play the player number (Core/Setup/TeamPlay).</summary>
    public int Team { get; set; }

    /// <summary>Points (+13h, LongInt).</summary>
    public int Score { get; set; }

    /// <summary>Defences (+52h, LongInt[3]): 0 shield, 1 inertia, 2 repulser (ItemCatalog.DefenseTable).</summary>
    public int[] Defense { get; } = new int[3];

    /// <summary>Hits on tanks of other teams (+6Ch).</summary>
    public int Hits { get; set; }

    /// <summary>Weapon chosen for the shot (+24h, index into ItemCatalog.WeaponTable).</summary>
    public int Weapon { get; set; }

    /// <summary>Sets the barrel and the power for the next shot (the turn input of O7:040C, Teil 3 of runde.md).</summary>
    public void Aim(int angle, int power)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(angle, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(angle, 180);
        Angle = angle;
        Power = power;
    }
}
