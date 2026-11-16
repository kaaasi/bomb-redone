using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Screens of the game sequence against a test double of the round.</summary>
public sealed class ScoreBattleFlowTests
{
    private sealed class DoubleRound : IBattleRound
    {
        public int Play(BattleRecords records, int game)
        {
            // Player 2 wins every round with 6000 points, so it shops; the others stay below the cheapest item.
            var winner = records.InOrder.Single(r => r.Name == "Player 2");
            foreach (var r in records.InOrder)
                r.Alive = r == winner;
            winner.Account.Score += 6000;
            return winner.Team;
        }
    }

    private static (ScoreBattleFlow Flow, BattleSequence Battle, List<string> Log) Flow(int games, bool teamPlay,
        Func<BattlePlayer, (int, int, int)>? color = null)
    {
        var records = new BattleRecords(Enumerable.Range(1, 3).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null,
                teamPlay ? 1 + (i % 2) : i)));
        var battle = new BattleSequence(records, games, teamPlay, new TurboPascalRandom(0x12345678), new DoubleRound(),
            ItemCatalog.Original);
        var log = new List<string>();
        var flow = new ScoreBattleFlow(ShopScreenTests.Fonts, battle, ItemCatalog.Original, false, color ?? (_ => (63, 0, 0)),
            t => $"Team {t}", () =>
            {
                log.Add("finished");
                return null;
            });
        return (flow, battle, log);
    }

    private static IScreen? Press(IScreen screen, ushort key) =>
        screen.Update(TimeSpan.FromMilliseconds(15), new ScreenInput(KeyPressed: true) { Keys = [key] });

    [Fact]
    public void Rating_shop_of_the_winner_and_next_round_until_the_end()
    {
        var (flow, battle, log) = Flow(2, teamPlay: false);
        var kinds = new List<string>();

        var screen = flow.Start();
        while (screen is not null)
        {
            kinds.Add($"{screen.GetType().Name} {battle.Game}");
            screen = Press(screen, screen is ShopScreen ? BiosKey.Escape : BiosKey.Enter);
        }

        Assert.Equal(["ScoreScreen 1", "ShopScreen 1", "ScoreScreen 2"], kinds);
        Assert.Equal(["finished"], log);
        Assert.Equal(BattleStepKind.Finished, battle.Current.Kind);
        Assert.Equal(1, battle.Records.NumberOf(battle.Records.InOrder.Single(r => r.Name == "Player 2")));
    }

    [Fact]
    public void The_shop_colours_the_name_with_entry_nineteen_plus_the_record_not_the_account()
    {
        // Player 2 (account 2) wins and is sorted onto record 1 by the rating, so the shop uses DAC entry 19 + 1 (#151).
        var (flow, battle, _) = Flow(2, teamPlay: false, r => r.Name == "Player 2" ? (10, 20, 30) : (40, 50, 60));

        var screen = flow.Start();
        Assert.IsType<ScoreScreen>(screen);
        var shop = Assert.IsType<ShopScreen>(Press(screen!, BiosKey.Enter));
        Assert.Equal(1, battle.Current.Record);
        Assert.Equal(2, shop.Shop.Account.Number);

        // The rating left entry 19 + 2 with the colour of record 2; the shop must not overwrite it with Player 2.
        Assert.Equal(new byte[] { 10, 20, 30 }, shop.Dac.Slice((ShopScreen.PlayerColorBase + 1) * 3, 3).ToArray());
        Assert.NotEqual(new byte[] { 10, 20, 30 }, shop.Dac.Slice((ShopScreen.PlayerColorBase + 2) * 3, 3).ToArray());
    }

    [Fact]
    public void Team_play_shows_the_team_status_screen_after_the_status_screen()
    {
        var (flow, battle, _) = Flow(1, teamPlay: true);

        var screen = flow.Start();
        Assert.IsType<ScoreScreen>(screen);
        screen = Press(screen!, BiosKey.Enter);
        Assert.IsType<ScoreTeamScreen>(screen);
        Assert.Null(Press(screen!, BiosKey.Enter));
        Assert.Equal(BattleStepKind.Finished, battle.Current.Kind);
    }

    [Fact]
    public void Add_keeps_the_status_screen()
    {
        var (flow, battle, _) = Flow(1, teamPlay: false);

        var screen = flow.Start();

        Assert.Same(screen, Press(screen!, 0x1E61)); // 'a'
        Assert.Equal(BattleStepKind.Rating, battle.Current.Kind);
    }
}
