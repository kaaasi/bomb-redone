using BombRedone.Core;
using BombRedone.Tests.Support;

namespace BombRedone.Core.Tests;

public class GameLimitsTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void PlayerCountIsLimitedToOneToTen(int players, bool valid) =>
        Assert.Equal(valid, GameLimits.IsValidPlayerCount(players));

    [GameDataFact]
    public void LimitsMatchTheOriginalDescription()
    {
        var text = File.ReadAllText(TestGameData.Load().Require("BOMB.TXT"));
        Assert.Contains($"{GameLimits.MinPlayers} to {GameLimits.MaxPlayers} players", text);
        Assert.Contains($"{GameLimits.ComputerIntelligences} computer intelligences", text);
    }
}
