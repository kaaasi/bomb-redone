using System.Globalization;
using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Battle;

/// <summary>
/// Swap O3:0000, shuffle O3:2417, sort of the rating O3:5AEF, team bonus O3:7885 and team rating O3:6696 against the probe
/// (tools/reverse-engineering/folge.py, cases in <see cref="BattleProbeData"/>): order of the records (by name), control,
/// intelligence, score, wins and RandSeed after the shuffle.
/// </summary>
public sealed class BattleRecordsProbeTests
{
    /// <summary>One probe case; lists are comma-separated per record 1..N.</summary>
    public sealed record RecordCase(int Index, string Kind, int Players, int A, int B, string Seed, string Controls,
        string Score, string Wins, string Teams, string Alive, int Winner, string ExpectedOrder, string ExpectedControls,
        string ExpectedIntelligence, string ExpectedScore, string ExpectedWins, string ExpectedSeed)
    {
        public override string ToString() => $"{Index} {Kind} {Players}";
    }

    public static TheoryData<RecordCase> Cases
    {
        get
        {
            var data = new TheoryData<RecordCase>();
            foreach (var c in BattleProbeData.All)
                data.Add(c);
            return data;
        }
    }

    private static int[] Ints(string csv) => [.. csv.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture))];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Records_match_the_probe(RecordCase c)
    {
        var controls = Ints(c.Controls);
        var score = Ints(c.Score);
        var wins = Ints(c.Wins);
        var teams = Ints(c.Teams);
        var alive = Ints(c.Alive);
        // As in the probe: record i is "Player i" with intelligence i.
        var records = new BattleRecords(Enumerable.Range(1, c.Players).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)) { Score = score[i - 1], Wins = wins[i - 1] },
                (PlayerControl)controls[i - 1], i, teams[i - 1]) { Alive = alive[i - 1] != 0 }));
        var random = new TurboPascalRandom(uint.Parse(c.Seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

        switch (c.Kind)
        {
            case "swap": records.Swap(c.A, c.B); break;
            case "shuffle": records.Shuffle(random); break;
            case "rating": records.SortForRating(); break;
            case "bonus": records.AddTeamBonus(c.Winner); break;
            default: throw new InvalidOperationException(c.Kind);
        }

        Assert.Equal(Ints(c.ExpectedOrder), records.InOrder.Select(r => r.Account.Number));
        Assert.Equal(Ints(c.ExpectedControls), records.InOrder.Select(r => (int)r.Control));
        Assert.Equal(Ints(c.ExpectedIntelligence), records.InOrder.Select(r => r.Intelligence ?? 0));
        Assert.Equal(Ints(c.ExpectedScore), records.InOrder.Select(r => r.Account.Score));
        Assert.Equal(Ints(c.ExpectedWins), records.InOrder.Select(r => r.Account.Wins));
        if (c.Kind == "shuffle")
            Assert.Equal(c.ExpectedSeed, random.Seed.ToString("X8", CultureInfo.InvariantCulture));
    }

    /// <summary>One team rating of the probe: team words, score and wins per record, sorted slots (0 = no members).</summary>
    public sealed record TeamCase(int Index, string Teams, string Score, string Wins, string ExpectedOrder,
        int ExpectedCounted)
    {
        public override string ToString() => $"{Index} teams {Teams}";
    }

    public static TheoryData<TeamCase> TeamCases
    {
        get
        {
            var data = new TheoryData<TeamCase>();
            foreach (var c in BattleProbeData.Teams)
                data.Add(c);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(TeamCases))]
    public void Team_rating_sorts_the_slots_like_the_probe(TeamCase c)
    {
        var teams = Ints(c.Teams);
        var score = Ints(c.Score);
        var wins = Ints(c.Wins);
        var accounts = teams.Select((_, i) =>
            new PlayerAccount(i + 1, PlayerSlot.DefaultName(i + 1)) { Score = score[i], Wins = wins[i] }).ToArray();
        var ranking = TeamStandings.Rank(teams.Distinct().Select(t =>
            new Team(t, $"Team {t}", [.. accounts.Where((_, i) => teams[i] == t)])));

        Assert.Equal(Ints(c.ExpectedOrder), ranking.Order.Select(t => t?.Number ?? 0));
        Assert.Equal(c.ExpectedCounted, ranking.CountedTeams);
    }

    [Fact]
    public void Every_kind_has_cases()
    {
        Assert.All(new[] { "swap", "shuffle", "rating", "bonus" },
            kind => Assert.Contains(BattleProbeData.All, c => c.Kind == kind));
        Assert.All(new[] { 2, 5, 10 },
            n => Assert.Contains(BattleProbeData.All, c => c.Kind == "shuffle" && c.Players == n));
    }
}
