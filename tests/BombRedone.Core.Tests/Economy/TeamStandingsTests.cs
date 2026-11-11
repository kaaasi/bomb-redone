using BombRedone.Core.Economy;

namespace BombRedone.Core.Tests.Economy;

/// <summary>Team order of the team status screen (O3:6696): sums, averages, the three bubble sorts and the ranks.</summary>
public class TeamStandingsTests
{
    private static PlayerAccount P(int number, int wins, int score) => new(number, $"P{number}") { Wins = wins, Score = score };

    [Fact]
    public void ScoresAreSummedAndWinsAveraged()
    {
        var ranking = TeamStandings.Rank([new Team(1, "A", [P(1, 3, 100), P(2, 0, 50)])]);
        var line = Assert.Single(ranking.Lines);
        Assert.Equal(150, line.Score);
        Assert.Equal(1.5, line.AverageWins);
        Assert.Equal(2, ranking.Players);
    }

    [Fact]
    public void AverageWinsDecideBeforeTheScore()
    {
        // Team 1: average 1, score 900; team 2: average 2, score 10 → team 2 first.
        var ranking = TeamStandings.Rank(
        [
            new Team(1, "A", [P(1, 1, 900)]),
            new Team(2, "B", [P(2, 2, 5), P(3, 2, 5)]),
        ]);
        Assert.Equal([2, 1], ranking.Lines.Select(l => l.Team.Number));
        Assert.Equal([1, 2], ranking.Lines.Select(l => l.Rank));
        Assert.Equal([1, 2], ranking.Lines.Select(l => l.Position));
    }

    [Fact]
    public void EqualAveragesAreOrderedByScore()
    {
        // 1/2 and 2/4 are the same average; the higher score wins.
        var ranking = TeamStandings.Rank(
        [
            new Team(1, "A", [P(1, 1, 10), P(2, 0, 10)]),
            new Team(2, "B", [P(3, 1, 30), P(4, 1, 0), P(5, 0, 0), P(6, 0, 0)]),
        ]);
        Assert.Equal([2, 1], ranking.Lines.Select(l => l.Team.Number));
        Assert.Equal([1, 2], ranking.Lines.Select(l => l.Rank));
    }

    [Fact]
    public void FullTiesShareTheRankAndKeepTheMemberOrder()
    {
        // Same average and score: the first sort (more members first) decides the order.
        var ranking = TeamStandings.Rank(
        [
            new Team(1, "A", [P(1, 1, 50)]),
            new Team(2, "B", [P(2, 1, 25), P(3, 1, 25)]),
        ]);
        Assert.Equal([2, 1], ranking.Lines.Select(l => l.Team.Number));
        Assert.Equal([1, 1], ranking.Lines.Select(l => l.Rank));
    }

    [Fact]
    public void EmptySlotsFollowTheTeams()
    {
        var ranking = TeamStandings.Rank([new Team(3, "C", [P(1, 0, 0)]), new Team(5, "E", [P(2, 0, 0)])]);
        Assert.Equal(10, ranking.Order.Count);
        Assert.Equal([3, 5], ranking.Order.Take(2).Select(t => t!.Number));
        Assert.All(ranking.Order.Skip(2), Assert.Null);
    }

    [Fact]
    public void OnlySlotsUpToThePlayerCountAreCounted()
    {
        // [ORIGINAL] O3:66B8 counts slots 1..players: two players in team 3 → no team counted, no line.
        var ranking = TeamStandings.Rank([new Team(3, "C", [P(1, 0, 0), P(2, 0, 0)])]);
        Assert.Equal(3, ranking.Order[0]!.Number);
        Assert.Single(ranking.Lines);
        Assert.Equal(1, ranking.Lines[0].Position);
        Assert.Equal(0, ranking.CountedTeams);
    }

    [Fact]
    public void DuplicateOrInvalidTeamNumbersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => TeamStandings.Rank([new Team(1, "A", []), new Team(1, "B", [])]));
        Assert.Throws<ArgumentOutOfRangeException>(() => TeamStandings.Rank([new Team(11, "A", [])]));
    }
}
