using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Turn;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Synthetic checks of the tank info window (O9:0040), without original files.</summary>
public sealed class TankInfoWindowTests
{
    private static TurnPlayer Player()
    {
        var field = Playfield.Build(new PlayfieldOptions(2, 0, 0, 0xFFFF, 2, false), new TurboPascalRandom(0x12345678));
        var p = new TurnPlayer(field.Tanks[0]);
        p.SetDefaultStocks();
        return p;
    }

    [Fact]
    public void Height_grows_by_twelve_per_item_line()
    {
        var p = Player();
        Assert.Equal(60 + 3 * 12, TankInfoWindow.Height(p, teamPlay: false));
        p.GuidanceStock[1] = 2;
        p.Tank.Defense[0] = 5;
        p.Tank.Defense[2] = -1;
        Assert.Equal(60 + 5 * 12, TankInfoWindow.Height(p, teamPlay: false));
        p.TeamName = "Blue";
        Assert.Equal(60 + 6 * 12, TankInfoWindow.Height(p, teamPlay: true));
    }

    [Theory]
    [InlineData(0, 120, "12%")]
    [InlineData(0, 1000, "100%")]
    [InlineData(0, 1001, "100%")]
    [InlineData(1, 100, "100%")]
    [InlineData(1, 99, "99%")]
    [InlineData(1, 101, "100%")]
    [InlineData(2, 1000, "1000")]
    [InlineData(0, 65536 + 50, "5%")]
    public void Defence_values_follow_O9(int kind, int value, string expected) =>
        Assert.Equal(expected, TankInfoWindow.DefenseText(kind, value));
}
