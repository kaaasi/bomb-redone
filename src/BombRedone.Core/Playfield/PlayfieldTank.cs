using BombRedone.Core.Economy;

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

    /// <summary>Guidance of the shot (+26h): 0 none, 1 Herb, 2 Vick, 3 both (docs/analyse/runde.md section 5.12).</summary>
    public int Guidance { get; set; }

    /// <summary>Target player of the guidance (+28h, 1..10).</summary>
    public int GuidanceTarget { get; set; }

    /// <summary>Herbs in stock (+2Ah, word).</summary>
    public int Herbs { get; set; }

    /// <summary>Vicks in stock (+2Ch, word).</summary>
    public int Vicks { get; set; }

    /// <summary>
    /// Stock per weapon (+2Eh + 2 · weapon, word; ItemCatalog.WeaponTable). The laser (5) holds energy: its power is
    /// capped at the stock and taken from it (docs/analyse/runde.md section 5.10).
    /// </summary>
    public int[] Stock { get; } = new int[18];

    /// <summary>
    /// Takes stock, guidance and defences of the player for the round. In the original account and tank are one record
    /// (+2Ah guidance, +2Eh weapons, +52h defences); the port keeps them apart and copies at the start of the round, the
    /// shots change only the tank (<see cref="ReturnStock"/> at the end of the round). Uses the public API of
    /// <see cref="PlayerAccount"/> only.
    /// </summary>
    public void TakeStock(PlayerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        for (var w = 0; w < Stock.Length; w++)
            Stock[w] = account.Stock(ItemKind.Weapon, w);
        Herbs = account.Stock(ItemKind.Guidance, 0);
        Vicks = account.Stock(ItemKind.Guidance, 1);
        for (var d = 0; d < Defense.Length; d++)
            Defense[d] = account.Stock(ItemKind.Defense, d);
    }

    /// <summary>Gives stock, guidance and defences back to the player at the end of the round (see <see cref="TakeStock"/>).</summary>
    public void ReturnStock(PlayerAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        for (var w = 0; w < Stock.Length; w++)
            account.SetStock(ItemKind.Weapon, w, Stock[w]);
        account.SetStock(ItemKind.Guidance, 0, Herbs);
        account.SetStock(ItemKind.Guidance, 1, Vicks);
        for (var d = 0; d < Defense.Length; d++)
            account.SetStock(ItemKind.Defense, d, Defense[d]);
    }

    /// <summary>Sets the barrel and the power for the next shot (the turn input of O7:040C, Teil 3 of runde.md).</summary>
    public void Aim(int angle, int power)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(angle, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(angle, 180);
        Angle = angle;
        Power = power;
    }
}
