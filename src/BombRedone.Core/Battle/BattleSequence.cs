using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;

namespace BombRedone.Core.Battle;

/// <summary>
/// One round of the battle as the game sequence sees it: O4[0] = O4:44A7 (docs/analyse/runde.md section 2 and 5.9).
/// The round works on the records in their current order (alive +23h, score +13h, wins +6Ah, stock, ...).
/// </summary>
public interface IBattleRound
{
    /// <summary>Plays game <paramref name="game"/> of the battle.</summary>
    /// <returns>The winning team DS:47E0 (team word +66h), 0 if no team won.</returns>
    int Play(BattleRecords records, int game);
}

/// <summary>What the battle waits for: a screen of the game sequence.</summary>
public enum BattleStepKind
{
    /// <summary>Status screen O3:5AEF (records already sorted).</summary>
    Rating,

    /// <summary>Team status screen O3:6696 (Team Play only).</summary>
    TeamRating,

    /// <summary>Shop O3:0E1F of record <see cref="BattleStep.Record"/>.</summary>
    Shop,

    /// <summary>The screen is wiped from the middle to the sides (O3:79B2–79FB).</summary>
    Wipe,

    /// <summary>All games are played; the end result O3[1] follows when the program ends.</summary>
    Finished,
}

/// <summary>A step of the game sequence.</summary>
/// <param name="Kind">What is shown.</param>
/// <param name="Game">Game number DS:47E2.</param>
/// <param name="Record">Record number in the shop, otherwise 0.</param>
public readonly record struct BattleStep(BattleStepKind Kind, int Game, int Record = 0);

/// <summary>
/// The game sequence O3[0] = O3:7847 (BOMB.OVR 0x102D3, docs/analyse/runde.md section 1, S1) over the games 1..DS:E8A6,
/// driven step by step by the screens.
/// </summary>
/// <remarks>
/// [ORIGINAL] Per game: shuffle O3:2417, round O4[0], team bonus (DS:47E0 ≠ 0), rating O3:5AEF (sorts the records), team
/// rating O3:6696 with Team Play, shop O3:0E1F for records 1..N except after the last game, wipe, DS:47E2 + 1. The probe
/// (tools/reverse-engineering/folge.py, kind "battle") shows no Random call between two shuffles for human players.
/// [APPROX] Autosave O3:3B54 and the dialog "SAVE GAME" O3:46DE are not modelled (saving follows in round 4); computer
/// players never shop (round 4); a loaded game (DS:4817) is not supported.
/// </remarks>
public sealed class BattleSequence
{
    private readonly TurboPascalRandom _random;
    private readonly IBattleRound _round;
    private readonly ItemCatalog _catalog;

    /// <param name="records">Records 1..N of the battle.</param>
    /// <param name="games">Games to play (DS:E8A6, option "Number of Games").</param>
    /// <param name="teamPlay">Team Play (DS:E8BC).</param>
    /// <param name="random">The one random generator of the game (RandSeed).</param>
    /// <param name="round">The round O4[0].</param>
    /// <param name="catalog">Items of the shop (for <see cref="BattleRecords.ShopVisits"/>).</param>
    public BattleSequence(BattleRecords records, int games, bool teamPlay, TurboPascalRandom random, IBattleRound round,
        ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentOutOfRangeException.ThrowIfLessThan(games, 1);
        Records = records;
        Games = games;
        TeamPlay = teamPlay;
        _random = random;
        _round = round;
        _catalog = catalog;
        Game = 1;
    }

    public BattleRecords Records { get; }

    /// <summary>Games of the battle (DS:E8A6); "Add" on the status screen raises it.</summary>
    public int Games { get; private set; }

    /// <summary>Current game (DS:47E2); one more than the games played once the battle is finished.</summary>
    public int Game { get; private set; }

    public bool TeamPlay { get; }

    /// <summary>Winning team of the last round (DS:47E0).</summary>
    public int WinnerTeam { get; private set; }

    /// <summary>The step the screens show now.</summary>
    public BattleStep Current { get; private set; }

    /// <summary>
    /// "Add" with a number on the status screen (dialog "# TO ADD:" O3:5717): more games. [ORIGINAL] The sequence
    /// compares DS:47E2 with DS:E8A6 again before the shop and after the wipe (O3:79A5, O3:7A01).
    /// </summary>
    public void AddGames(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Games += count;
    }

    /// <summary>Whether <see cref="Start"/> was called.</summary>
    public bool Started { get; private set; }

    /// <summary>Plays game 1 up to its status screen.</summary>
    public BattleStep Start()
    {
        if (Started)
            throw new InvalidOperationException("The battle has started.");
        Started = true;
        Current = PlayGame();
        return Current;
    }

    /// <summary>The current screen is done; the sequence runs to the next screen.</summary>
    public BattleStep Continue()
    {
        if (!Started)
            throw new InvalidOperationException("Start the battle first.");
        Current = Current.Kind switch
        {
            BattleStepKind.Rating when TeamPlay => new BattleStep(BattleStepKind.TeamRating, Game),
            BattleStepKind.Rating or BattleStepKind.TeamRating => ShopAfter(0),
            BattleStepKind.Shop => ShopAfter(Current.Record),
            BattleStepKind.Wipe => NextGame(),
            BattleStepKind.Finished => throw new InvalidOperationException("The battle is finished."),
            _ => throw new InvalidOperationException(),
        };
        return Current;
    }

    private BattleStep PlayGame()
    {
        Records.Shuffle(_random);
        WinnerTeam = _round.Play(Records, Game);
        Records.AddTeamBonus(WinnerTeam);
        Records.SortForRating();
        return new BattleStep(BattleStepKind.Rating, Game);
    }

    /// <summary>[ORIGINAL] O3:79A5: no shop after the last game; O3:0F1C: records after <paramref name="record"/>.</summary>
    private BattleStep ShopAfter(int record)
    {
        if (Game != Games)
        {
            var next = Records.ShopVisits(_catalog).FirstOrDefault(n => n > record);
            if (next != 0)
                return new BattleStep(BattleStepKind.Shop, Game, next);
        }

        return new BattleStep(BattleStepKind.Wipe, Game);
    }

    private BattleStep NextGame()
    {
        Game++;
        return Game > Games ? new BattleStep(BattleStepKind.Finished, Game) : PlayGame();
    }
}
