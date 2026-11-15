using BombRedone.Core.Economy;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Battle;

/// <summary>
/// Content of one player record during a battle: everything that moves with the record when the game sequence swaps two
/// records (O3:0000). The record number itself is the position in <see cref="BattleRecords"/>.
/// </summary>
/// <remarks>
/// [ORIGINAL] Records of 0x74 bytes at DS:402E + 0x74 × number (docs/analyse/runde.md section 1, S4): the swap copies the
/// whole record (score +13h, spent +17h, alive +23h, stock +2Ah..+5Dh, team +66h, wins +6Ah, rounds as start player +72h,
/// ...), the intelligence pointer DS:3BE8 + 4i and the control word DS:E5A6 + 2i, and then writes the new number into
/// byte +00h and into the intelligence (Self+10h). <see cref="PlayerAccount.Number"/> stays the number the player had in
/// the player selection; it identifies the player, not the record.
/// </remarks>
public sealed class BattlePlayer
{
    /// <param name="account">Score, wins, money and stock.</param>
    /// <param name="control">Control word (DS:E5A6).</param>
    /// <param name="intelligence">Index of the computer intelligence (DS:3BE8), null if none is installed.</param>
    /// <param name="team">Team word +66h (team number with team play, otherwise the player number of O2:0216).</param>
    public BattlePlayer(PlayerAccount account, PlayerControl control, int? intelligence, int team)
    {
        ArgumentNullException.ThrowIfNull(account);
        Account = account;
        Control = control;
        Intelligence = intelligence;
        Team = team;
    }

    public PlayerAccount Account { get; }

    public PlayerControl Control { get; }

    public int? Intelligence { get; }

    /// <summary>Team word +66h.</summary>
    public int Team { get; }

    /// <summary>Alive at the end of the round (+23h ≠ 0).</summary>
    public bool Alive { get; set; }

    /// <summary>Rounds as start player (+72h, docs/analyse/runde.md section 2, R4).</summary>
    public int RoundsStarted { get; set; }

    /// <summary>Name of the player (record +01h).</summary>
    public string Name => Account.Name;
}
