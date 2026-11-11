using BombRedone.Core.Economy;

namespace BombRedone.Core.Tests.Economy;

public class StandingsTests
{
    private static PlayerAccount P(int number, int wins, int score) => new(number, $"P{number}") { Wins = wins, Score = score };

    [Fact]
    public void WinsComeFirstThenScore()
    {
        var ranked = Standings.Rank([P(1, 0, 9000), P(2, 2, 100), P(3, 2, 500), P(4, 1, 0)]);
        Assert.Equal([3, 2, 4, 1], ranked.Select(s => s.Player.Number));
        Assert.Equal([1, 2, 3, 4], ranked.Select(s => s.Rank));
    }

    [Fact]
    public void EqualWinsAndScoreShareARankAndKeepTheirOrder()
    {
        var ranked = Standings.Rank([P(1, 1, 50), P(2, 1, 50), P(3, 0, 0), P(4, 0, 0)]);
        Assert.Equal([1, 2, 3, 4], ranked.Select(s => s.Player.Number));
        Assert.Equal([1, 1, 2, 2], ranked.Select(s => s.Rank));
    }

    [Fact]
    public void VideoExample()
    {
        // LP 00064: "1. Javier 1 4260", "2. Lob & Shoot 0 0".
        var ranked = Standings.Rank([P(2, 0, 0), P(1, 1, 4260)]);
        Assert.Equal([(1, 1), (2, 2)], ranked.Select(s => (s.Rank, s.Player.Number)));
    }

    [Fact]
    public void NoPlayersGiveNoLines() => Assert.Empty(Standings.Rank([]));
}
