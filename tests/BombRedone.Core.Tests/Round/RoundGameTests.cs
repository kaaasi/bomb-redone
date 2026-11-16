using System.Reflection;
using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Tests.Round;

/// <summary>
/// The round on the battle records (spur runde): colour divisors after the shuffle (#153), stocks to the tank and back
/// (#147), the CRI list over rounds (#165), the round step of <see cref="BattleSequence"/>. Synthetic, no original data.
/// </summary>
public sealed class RoundGameTests
{
    private const int Nuke = 4;

    private static BattleRecords Records(int players = 2) => new(Enumerable.Range(1, players).Select(i =>
        new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null, i)));

    private static PlayfieldOptions Options(int players = 2) => new(players, Wind: 2);

    /// <summary>Plays the round: every turn the player to move fires a nuke straight up onto himself.</summary>
    private static List<RoundPoint> PlaySuicides(RoundGame game, TurboPascalRandom random)
    {
        var settings = new RoundSettings(10, game.Wind.Value, Crumble: 0, FallDown: true);
        var session = new TurnSession(game.Field, game.Players, new TurnInput(), new TurnSettings());
        var round = new RoundPlay(game.Field, game.Players, session, settings, random, game.Start);
        return round.Play(p =>
        {
            p.Tank.Aim(90, 50);
            p.Tank.Weapon = Nuke;
            return new TurnOutcome();
        }).ToList();
    }

    [Fact]
    public void Colour_divisors_follow_the_record_after_a_swap()
    {
        var records = Records();
        records.Swap(1, 2);

        var game = RoundGame.Prepare(records, Options(), new TurboPascalRandom(0x12345678));

        Assert.Equal(Core.Playfield.Playfield.ColorDivisors[1], game.Field.Tanks[0].ColorDivisors);
        Assert.Equal(Core.Playfield.Playfield.ColorDivisors[0], game.Field.Tanks[1].ColorDivisors);
        var (r, g, b) = Core.Playfield.Playfield.ColorDivisors[1];
        Assert.Equal(((byte)(63 / r), (byte)(63 / g), (byte)(63 / b)), game.Field.Palette[game.Field.Tanks[0].Color]);
        Assert.Equal("Player 2", game.Players[0].Name);
    }

    [Fact]
    public void Stocks_score_and_survival_go_to_the_tank_and_back()
    {
        var records = Records();
        RoundGame.InitializeRecords(records);
        foreach (var r in records.InOrder)
            r.Account.SetStock(ItemKind.Weapon, Nuke, 5);
        var random = new TurboPascalRandom(0x12345678);

        var game = RoundGame.Prepare(records, Options(), random);
        Assert.All(game.Field.Tanks, t => Assert.Equal(5, t.Stock[Nuke]));
        Assert.All(game.Field.Tanks, t => Assert.Equal(30, t.Stock[1]));
        var shooter = game.Start.Player;
        var points = PlaySuicides(game, random);
        game.Finish();

        Assert.Equal(RoundPointKind.End, points[^1].Kind);
        var mover = records[shooter];
        var other = records[3 - shooter];
        Assert.Equal(4, mover.Account.Stock(ItemKind.Weapon, Nuke));
        Assert.Equal(5, other.Account.Stock(ItemKind.Weapon, Nuke));
        Assert.Equal(game.Field.Tanks[shooter - 1].Score, mover.Account.Score);
        Assert.False(mover.Alive);
        Assert.True(other.Alive);
        Assert.Equal(Nuke, mover.Weapon);
        Assert.Equal(1, mover.Turns);
        Assert.Equal(1, mover.RoundsStarted);
    }

    [Fact]
    public void The_cri_list_goes_on_to_the_next_round()
    {
        var random = new TurboPascalRandom(0x12345678);
        var first = RoundGame.Prepare(Records(), Options(), random);
        var list = CriList(first.Field);
        list[3] = (100, 120);

        var second = RoundGame.Prepare(Records(), Options(), random, first.Field);

        Assert.Equal((100, 120), CriList(second.Field)[3]);
        Assert.Equal((0, 0), CriList(RoundGame.Prepare(Records(), Options(), random).Field)[3]);
    }

    private static (short X, short Y)[] CriList(Core.Playfield.Playfield field) =>
        ((short X, short Y)[])typeof(Core.Playfield.Playfield)
            .GetProperty("CriList", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(field)!;

    [Fact]
    public void A_battle_without_round_object_waits_for_each_round()
    {
        var records = Records();
        RoundGame.InitializeRecords(records);
        foreach (var r in records.InOrder)
            r.Account.SetStock(ItemKind.Weapon, Nuke, 5);
        var random = new TurboPascalRandom(0x12345678);
        var battle = new BattleSequence(records, 2, false, random, null, ItemCatalog.Original);
        Core.Playfield.Playfield? previous = null;
        var rounds = 0;

        var step = battle.Start();
        while (step.Kind != BattleStepKind.Finished)
        {
            if (step.Kind == BattleStepKind.Round)
            {
                Assert.Throws<InvalidOperationException>(() => battle.Continue());
                var game = RoundGame.Prepare(records, Options(), random, previous);
                var winner = PlaySuicides(game, random)[^1].Kind == RoundPointKind.End
                    ? game.Players.First(p => p.Tank.Active).Tank.Team
                    : 0;
                game.Finish();
                previous = game.Field;
                rounds++;
                step = battle.CompleteRound(winner);
                Assert.Equal(BattleStepKind.Rating, step.Kind);
                continue;
            }

            step = battle.Continue();
        }

        Assert.Equal(2, rounds);
        Assert.Equal(2, records.InOrder.Sum(r => r.Account.Wins));
    }
}
