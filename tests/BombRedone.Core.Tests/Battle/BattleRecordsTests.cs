using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Battle;

public sealed class BattleRecordsTests
{
    private static BattlePlayer Player(int number, int score = 0, PlayerControl control = PlayerControl.Keyboard,
        int spent = 0) =>
        new(new PlayerAccount(number, PlayerSlot.DefaultName(number)) { Score = score, Spent = spent }, control, null,
            number);

    [Fact]
    public void Swap_moves_the_whole_record_and_the_number_follows_the_position()
    {
        var a = Player(1);
        var b = Player(2);
        a.RoundsStarted = 3;
        var records = new BattleRecords([a, b]);

        records.Swap(1, 2);

        Assert.Same(b, records[1]);
        Assert.Same(a, records[2]);
        Assert.Equal(2, records.NumberOf(a));
        Assert.Equal(3, records[2].RoundsStarted);
        Assert.Equal(1, a.Account.Number);
    }

    [Fact]
    public void Setup_without_team_play_uses_the_player_number_as_team()
    {
        var setup = new GameSetup([new PlayerSetup(1, "Ann", PlayerControl.Keyboard, null, 0),
            new PlayerSetup(2, "Bob", PlayerControl.Mouse, 4, 0)], false);

        var records = BattleRecords.FromSetup(setup);

        Assert.Equal([1, 2], records.InOrder.Select(r => r.Team));
        Assert.Equal(PlayerControl.Mouse, records[2].Control);
        Assert.Equal(4, records[2].Intelligence);
        Assert.Equal("Bob", records[2].Name);
    }

    [Fact]
    public void Team_bonus_without_winner_changes_nothing()
    {
        var records = new BattleRecords([Player(1, 100), Player(2, 200)]);
        records.AddTeamBonus(0);
        Assert.Equal([100, 200], records.InOrder.Select(r => r.Account.Score));
    }

    [Fact]
    public void Shop_visits_follow_the_record_order_and_skip_poor_players_and_computers()
    {
        // O3:0F1C: records 1..N; the cheapest item costs 3000 (wertung-einkauf.md).
        var records = new BattleRecords([Player(1, 5000), Player(2, 2999), Player(3, 9000, PlayerControl.Computer),
            Player(4, 8000, spent: 5000), Player(5, 3000)]);
        records.Swap(1, 5);

        Assert.Equal([1, 4, 5], records.ShopVisits(ItemCatalog.Original));
    }
}
