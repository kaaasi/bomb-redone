using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Battle;

public sealed class BattleTests
{
    private sealed class FixedRound(int winner, int score) : IBattleRound
    {
        public int Played { get; private set; }

        public int Play(BattleRecords records, int game)
        {
            Played++;
            foreach (var r in records.InOrder)
            {
                r.Alive = r.Team == winner;
                r.Account.Score += score;
            }

            return winner;
        }
    }

    private static BattleRecords Records(int n) => new(Enumerable.Range(1, n).Select(i =>
        new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null, i)));

    [Fact]
    public void One_game_has_no_shop()
    {
        var round = new FixedRound(1, 10000);
        var battle = new BattleSequence(Records(2), 1, false, new TurboPascalRandom(1), round, ItemCatalog.Original);

        Assert.Equal(BattleStepKind.Rating, battle.Start().Kind);
        Assert.Equal(BattleStepKind.Wipe, battle.Continue().Kind);
        Assert.Equal(BattleStepKind.Finished, battle.Continue().Kind);
        Assert.Equal(1, round.Played);
    }

    [Fact]
    public void Added_games_bring_back_the_shop_and_the_next_round()
    {
        var round = new FixedRound(2, 10000);
        var battle = new BattleSequence(Records(2), 1, false, new TurboPascalRandom(7), round, ItemCatalog.Original);
        battle.Start();

        battle.AddGames(1);

        Assert.Equal(BattleStepKind.Shop, battle.Continue().Kind);
        Assert.Equal(BattleStepKind.Shop, battle.Continue().Kind);
        Assert.Equal(BattleStepKind.Wipe, battle.Continue().Kind);
        Assert.Equal(new BattleStep(BattleStepKind.Rating, 2), battle.Continue());
        Assert.Equal(2, round.Played);
        Assert.Equal(2, battle.Records.InOrder.Single(r => r.Team == 2).Account.Wins);
    }

    [Fact]
    public void Continue_before_start_and_after_the_end_is_an_error()
    {
        var battle = new BattleSequence(Records(2), 1, false, new TurboPascalRandom(1), new FixedRound(0, 0), ItemCatalog.Original);
        Assert.Throws<InvalidOperationException>(() => { battle.Continue(); });
        battle.Start();
        battle.Continue();
        battle.Continue();
        Assert.Throws<InvalidOperationException>(() => { battle.Continue(); });
    }
}
