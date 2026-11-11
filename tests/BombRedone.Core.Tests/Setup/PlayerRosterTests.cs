using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Setup;

/// <summary>Player records and the setup the player selection hands to the game (synthetic, no original files).</summary>
public class PlayerRosterTests
{
    [Fact]
    public void NewRosterHasDefaultNamesKeyboardAndTwoPlayers()
    {
        var roster = new PlayerRoster();
        Assert.Equal(2, roster.Players);
        Assert.Equal(10, roster.Slots.Count);
        for (var n = 1; n <= 10; n++)
        {
            Assert.Equal(n, roster[n].Number);
            Assert.Equal("Player " + n, roster[n].Name);
            Assert.Equal(PlayerControl.Keyboard, roster[n].Control);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void PlayerNumbersOutsideOneToTenAreRejected(int number)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerRoster()[number]);
    }

    [Fact]
    public void NamesLongerThanThirteenCharactersAreRejected()
    {
        var slot = new PlayerRoster()[1];
        slot.Name = "1234567890123";
        Assert.Throws<ArgumentException>(() => slot.Name = "12345678901234");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public void PlayerCountOutsideTheLimitsIsRejected(int players)
    {
        // The main menu offers 2..10; 1 is still a valid game size (BOMB.TXT), 11 is not.
        var roster = new PlayerRoster();
        if (players == 1)
        {
            roster.Players = 1;
            Assert.Equal(1, roster.Players);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => roster.Players = players);
        }
    }

    [Fact]
    public void StartTakesThePlayersInOrderWithoutCallingRandomForHumans()
    {
        var roster = new PlayerRoster { Players = 3 };
        roster[2].Control = PlayerControl.Mouse;
        roster[3].Name = "Javier";
        var setup = roster.Start(_ => throw new InvalidOperationException("no Random expected"));
        Assert.Equal([1, 2, 3], setup.Players.Select(p => p.Number));
        Assert.Equal(["Player 1", "Player 2", "Javier"], setup.Players.Select(p => p.Name));
        Assert.Equal(
            [PlayerControl.Keyboard, PlayerControl.Mouse, PlayerControl.Keyboard],
            setup.Players.Select(p => p.Control));
        Assert.All(setup.Players, p => Assert.Null(p.Intelligence));
        Assert.All(setup.Players, p => Assert.Equal(0, p.Team));
        Assert.False(setup.AllComputers);
    }

    [Fact]
    public void RandomComputersAreResolvedWithTheGivenGeneratorInPlayerOrder()
    {
        var roster = new PlayerRoster { Players = 4 };
        roster[1].Control = PlayerControl.Computer;
        roster[1].Name = "Random";
        roster[2].Control = PlayerControl.Computer;
        roster[2].Name = "Lobber";
        roster[3].Control = PlayerControl.Computer;
        roster[3].Name = "Random";
        roster[4].Control = PlayerControl.Computer;
        roster[4].Name = "Random";
        var calls = new List<int>();
        var picks = new Queue<int>([0, 6, 3]);
        var setup = roster.Start(n =>
        {
            calls.Add(n);
            return picks.Dequeue();
        });

        Assert.Equal([7, 7, 7], calls);
        Assert.Equal(["Mr. Stupid", "Lobber", "Wind Master", "Lob & Shoot"], setup.Players.Select(p => p.Name));
        Assert.Equal([1, 2, 7, 4], setup.Players.Select(p => p.Intelligence ?? -1));
        Assert.True(setup.AllComputers);

        // The records keep the resolved names (the original overwrites the name).
        Assert.Equal("Wind Master", roster[3].Name);
    }

    [Fact]
    public void RandomIsOnlyResolvedForComputers()
    {
        var roster = new PlayerRoster();
        roster[1].Name = "Random";
        var setup = roster.Start(_ => throw new InvalidOperationException());
        Assert.Equal("Random", setup.Players[0].Name);
        Assert.Null(setup.Players[0].Intelligence);
    }

    [Fact]
    public void AnIntelligenceNameCountsWhateverTheControl()
    {
        // [ORIGINAL] O2:0078 installs the intelligence for every player whose name matches.
        var roster = new PlayerRoster();
        roster[2].Name = "Twanger";
        var setup = roster.Start(_ => 0);
        Assert.Equal(6, setup.Players[1].Intelligence);
        Assert.Equal(PlayerControl.Keyboard, setup.Players[1].Control);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void RandomResultsOutsideZeroToSixAreRejected(int pick)
    {
        var roster = new PlayerRoster();
        roster[1].Control = PlayerControl.Computer;
        roster[1].Name = "Random";
        Assert.Throws<InvalidOperationException>(() => roster.Start(_ => pick));
    }

    [Fact]
    public void FindMatchesExactNamesOnly()
    {
        Assert.Equal(0, ComputerOpponents.Find("Random"));
        Assert.Equal(7, ComputerOpponents.Find("Wind Master"));
        Assert.Null(ComputerOpponents.Find("wind master"));
        Assert.Null(ComputerOpponents.Find("Wind Master "));
    }
}
