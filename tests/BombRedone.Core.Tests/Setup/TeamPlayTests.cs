using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Setup;

/// <summary>Team numbers, the check of the team setup and the team table (synthetic, no original files).</summary>
public class TeamPlayTests
{
    private static PlayerRoster Roster(int players, params int[] teams)
    {
        var roster = new PlayerRoster { Players = players };
        for (var i = 0; i < teams.Length; i++)
            roster[i + 1].Team = teams[i];
        return roster;
    }

    [Fact]
    public void EveryPlayerStartsInTheTeamOfItsNumber()
    {
        var roster = new PlayerRoster();
        for (var n = 1; n <= 10; n++)
            Assert.Equal(n, roster[n].Team);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void TeamNumbersOutsideOneToTenAreRejected(int team)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRoster()[1].Team = team);
    }

    [Theory]
    [InlineData(new[] { 1, 2, 3 }, false)] // everybody alone
    [InlineData(new[] { 1, 1, 1 }, false)] // one team only
    [InlineData(new[] { 1, 1, 3 }, true)]
    [InlineData(new[] { 2, 2, 1 }, true)]
    [InlineData(new[] { 3, 3, 3 }, false)]
    public void TeamPlayNeedsTwoTeamsAndOneOfTwoPlayers(int[] teams, bool valid)
    {
        Assert.Equal(valid, TeamPlay.IsValid(Roster(3, teams)));
    }

    [Fact]
    public void PlayersAboveTheCountDoNotCount()
    {
        // Player 4 shares team 1 with player 1, but only three play.
        Assert.False(TeamPlay.IsValid(Roster(3, 1, 2, 3, 1)));
    }

    [Fact]
    public void ResetGivesEveryRecordItsOwnTeam()
    {
        var roster = Roster(3, 2, 2, 2, 1);
        TeamPlay.Reset(roster);
        for (var n = 1; n <= 10; n++)
            Assert.Equal(n, roster[n].Team);
    }

    [Fact]
    public void TeamsAreNamedByTheirRankAndSinglePlayersByTheirName()
    {
        var roster = Roster(4, 2, 4, 2, 2);
        roster[2].Name = "Alice";
        roster[1].Control = PlayerControl.Computer;
        var teams = TeamPlay.CreateTeams(roster);
        Assert.Equal(4, teams.Count);
        Assert.Empty(teams[0].Members);
        Assert.Equal([1, 3, 4], teams[1].Members);
        Assert.Equal("Team 1", teams[1].Name);
        Assert.False(teams[1].AllComputers);
        Assert.Empty(teams[2].Members);
        Assert.Equal("Team 1", teams[2].Name);
        Assert.Equal([2], teams[3].Members);
        Assert.Equal("Alice", teams[3].Name);
        Assert.True(teams[0].AllComputers);
    }

    [Fact]
    public void AllComputerTeamsAreRecognised()
    {
        var roster = Roster(3, 1, 1, 3);
        roster[1].Control = roster[2].Control = PlayerControl.Computer;
        var teams = TeamPlay.CreateTeams(roster);
        Assert.True(teams[0].AllComputers);
        Assert.False(teams[2].AllComputers);
    }

    [Fact]
    public void RandomNamesAreDrawnUntilNoOtherTeamHasThem()
    {
        var roster = Roster(3, 1, 1, 3);
        roster[3].Name = "Studs";
        var teams = TeamPlay.CreateTeams(roster);
        var calls = new Queue<int>([3, 3, 9]);
        var limits = new List<int>();
        TeamPlay.ChooseRandomName(teams, 1, n =>
        {
            limits.Add(n);
            return calls.Dequeue();
        });
        Assert.Equal("Gravy", teams[0].Name);
        Assert.Equal([11, 11, 11], limits);
    }

    [Fact]
    public void ApplySetsTheTeamNumbersAndListsTeamsWithMembers()
    {
        var roster = Roster(3, 2, 2, 3);
        var setup = roster.Start(_ => 0);
        Assert.All(setup.Players, p => Assert.Equal(0, p.Team));
        Assert.Empty(setup.Teams);
        var teams = TeamPlay.CreateTeams(roster);
        teams[1].Name = "Reds";
        var result = TeamPlay.Apply(setup, roster, teams);
        Assert.Equal([2, 2, 3], result.Players.Select(p => p.Team));
        Assert.Equal(2, result.Teams.Count);
        Assert.Equal(new TeamSetup(2, "Reds", [1, 2]).Name, result.Teams[0].Name);
        Assert.Equal([1, 2], result.Teams[0].Members);
        Assert.Equal((3, "Player 3"), (result.Teams[1].Number, result.Teams[1].Name));
    }

    [Fact]
    public void RandomNamesAreTheElevenNamesOfTheOriginal()
    {
        Assert.Equal(11, TeamPlay.RandomNames.Count);
        Assert.All(TeamPlay.RandomNames, n => Assert.InRange(n.Length, 1, TeamRecord.MaxNameLength));
    }
}
