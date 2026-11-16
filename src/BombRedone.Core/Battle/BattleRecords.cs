using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Battle;

/// <summary>
/// The player records 1..N of a battle in their current order. The game sequence reorders them for good: the shuffle
/// before every round and the sort of the rating (issue #71, R2-D4 = A); both go through <see cref="Swap"/>.
/// </summary>
public sealed class BattleRecords
{
    private readonly BattlePlayer[] _records;

    /// <param name="players">Records 1..N in order (2..10).</param>
    public BattleRecords(IEnumerable<BattlePlayer> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        _records = [.. players];
        if (_records.Length is < GameLimits.MinPlayers or > GameLimits.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(players), _records.Length, "A battle has 2 to 10 players.");
    }

    /// <summary>
    /// Records of a new battle from the player selection. [ORIGINAL] The team word +66h is the player number after
    /// O2:0216 and the team number after the team set-up (O2:2749); <see cref="PlayerSetup.Team"/> is 0 without team play.
    /// </summary>
    public static BattleRecords FromSetup(GameSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        return new BattleRecords(setup.Players.Select(p => new BattlePlayer(new PlayerAccount(p.Number, p.Name), p.Control,
            p.Intelligence, p.Team != 0 ? p.Team : p.Number)));
    }

    /// <summary>Number of players (DS:47DA).</summary>
    public int Count => _records.Length;

    /// <summary>Record <paramref name="number"/> (1..N).</summary>
    public BattlePlayer this[int number] =>
        number >= 1 && number <= _records.Length
            ? _records[number - 1]
            : throw new ArgumentOutOfRangeException(nameof(number), number, "Record numbers are 1 to N.");

    /// <summary>Records 1..N in order (index 0 = record 1).</summary>
    public IReadOnlyList<BattlePlayer> InOrder => _records;

    /// <summary>Record number of <paramref name="player"/>.</summary>
    public int NumberOf(BattlePlayer player) => Array.IndexOf(_records, player) + 1;

    /// <summary>
    /// Swaps records <paramref name="a"/> and <paramref name="b"/>. [ORIGINAL] O3:0000 (BOMB.OVR 0x08A8C, S4): whole
    /// record, intelligence pointer and control word change places; the numbers (+00h, Self+10h) follow the position.
    /// </summary>
    public void Swap(int a, int b)
    {
        _ = this[a];
        _ = this[b];
        (_records[a - 1], _records[b - 1]) = (_records[b - 1], _records[a - 1]);
    }

    /// <summary>
    /// Shuffle before every round. [ORIGINAL] O3:2417 (BOMB.OVR 0x0AEA3, S3): Random(20) + 10 swaps of a = Random(N) + 1
    /// and b = Random(N) + 1, b drawn again until b ≠ a.
    /// </summary>
    /// <returns>The number of swaps.</returns>
    public int Shuffle(TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var swaps = random.Next(20) + 10;
        for (var i = 0; i < swaps; i++)
        {
            var a = random.Next(Count) + 1;
            int b;
            do
            {
                b = random.Next(Count) + 1;
            }
            while (b == a);
            Swap(a, b);
        }

        return swaps;
    }

    /// <summary>
    /// Sort of the rating, kept for the rest of the battle. [ORIGINAL] O3:5B61–5C6D (BOMB.OVR 0x0E5ED, S5): N passes of a
    /// bubble sort that swaps neighbours with fewer wins (+6Ah), then N passes that swap neighbours with equal wins and a
    /// lower score (+13h, signed); swaps through O3:0000. This is the order of the shop and of the next shuffle.
    /// </summary>
    public void SortForRating()
    {
        var n = Count;
        for (var pass = 0; pass < n; pass++)
            for (var i = 1; i < n; i++)
                if (this[i].Account.Wins < this[i + 1].Account.Wins)
                    Swap(i, i + 1);
        for (var pass = 0; pass < n; pass++)
            for (var i = 1; i < n; i++)
                if (this[i].Account.Wins == this[i + 1].Account.Wins && this[i].Account.Score < this[i + 1].Account.Score)
                    Swap(i, i + 1);
    }

    /// <summary>
    /// Bonus of the winning team after a round. [ORIGINAL] O3:7885–7968 (BOMB.OVR 0x10311, S2): with DS:47E0 ≠ 0 every
    /// record whose team word equals DS:47E0 gets 1000 · N div (members of that team) points (+13h, LongInt; word
    /// arithmetic, the members counted over all records, dead ones included) and, if alive (+23h ≠ 0), one win (+6Ah).
    /// </summary>
    /// <param name="winnerTeam">DS:47E0 after the round, 0 if no team won.</param>
    public void AddTeamBonus(int winnerTeam)
    {
        if (winnerTeam == 0)
            return;
        var members = _records.Count(r => r.Team == winnerTeam);
        if (members == 0)
            throw new ArgumentException("The winning team has no members.", nameof(winnerTeam));
        var bonus = (short)(1000 * Count) / members;
        foreach (var record in _records.Where(r => r.Team == winnerTeam))
        {
            if (record.Alive)
                record.Account.Wins++;
            record.Account.Score = unchecked(record.Account.Score + bonus);
        }
    }

    /// <summary>
    /// Records that enter the shop after a game, in record order. [ORIGINAL] O3:0F1C–0F90 (BOMB.OVR 0x099A8): records
    /// 1..N, money at least the cheapest price (<see cref="Shop.Visits"/>). [APPROX] Computer players (control 3) need
    /// U129:018D and "Computers Can Buy" (O3:0F3B); computers follow in round 4, they never shop here.
    /// </summary>
    public IEnumerable<int> ShopVisits(ItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        for (var number = 1; number <= Count; number++)
        {
            var record = this[number];
            if (record.Control != PlayerControl.Computer && Shop.Visits(record.Account, catalog))
                yield return number;
        }
    }
}
