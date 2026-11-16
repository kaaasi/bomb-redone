using System.Security.Cryptography;
using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// The round on the desktop (spur runde): the round loop of Core with the turns over the frames and the play-back of the
/// frames between two turns; the field at the end is that of Core alone. Synthetic fonts, no original data.
/// </summary>
public sealed class PlayfieldRoundScreenTests
{
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);
    private const int Nuke = 4;

    private static RoundGame Game(TurboPascalRandom random)
    {
        var records = new BattleRecords(Enumerable.Range(1, 2).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Computer, null, i)));
        RoundGame.InitializeRecords(records);
        foreach (var r in records.InOrder)
        {
            r.Account.SetStock(ItemKind.Weapon, Nuke, 5);
            r.Weapon = Nuke;
        }

        var game = RoundGame.Prepare(records, new PlayfieldOptions(2, Wind: 2), random);
        foreach (var p in game.Players)
            p.Tank.Aim(90, 50);  // a nuke straight up comes down on the shooter
        return game;
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public void The_battle_flow_hands_each_round_to_the_round_screen()
    {
        var records = new BattleRecords(Enumerable.Range(1, 2).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null, i)));
        var battle = new BattleSequence(records, 2, false, new TurboPascalRandom(0x12345678), null, ItemCatalog.Original);
        var rounds = 0;
        Func<int, IScreen?>? complete = null;
        var flow = new ScoreBattleFlow(ShopScreenTests.Fonts, battle, ItemCatalog.Original, false, _ => (63, 0, 0),
            t => $"Team {t}", () => null, done =>
            {
                rounds++;
                complete = done;
                return new PlaceholderScreen();
            });

        Assert.IsType<PlaceholderScreen>(flow.Start());
        Assert.Equal(BattleStepKind.Round, battle.Current.Kind);
        var rating = complete!(2);

        Assert.IsType<ScoreScreen>(rating);
        Assert.Equal(1, rounds);
        Assert.Equal(2000, records.InOrder.Single(r => r.Team == 2).Account.Score);  // team bonus 1000 · N for the winner
    }

    [Fact]
    public void A_round_of_computers_plays_back_and_ends_like_core_alone()
    {
        var random = new TurboPascalRandom(0x12345678);
        var game = Game(random);
        var playfield = new PlayfieldScreen(Fonts, game.Field, game.Wind, game.Players.Select(p => p.Name).ToArray());
        var view = new PlayfieldTurnView(playfield, Fonts, game.Wind);
        var settings = new RoundSettings(10, game.Wind.Value, 0, true);
        int? winner = null;
        var screen = new PlayfieldRoundScreen(playfield, view, game.Players, [PlayerControl.Computer, PlayerControl.Computer],
            game.Start, () => new bool[Scancode.Count], settings, random,
            new ShotTiming(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)),
            new TurnSettings(), w =>
            {
                winner = w;
                return null;
            });

        IScreen? current = screen;
        var playedBack = false;
        var surface = new Surface();
        for (var i = 0; i < 100_000 && current is not null; i++)
        {
            current = current.Update(Frame, default);
            playedBack |= screen.PlayingBack;
            screen.Draw(surface);
        }

        var coreRandom = new TurboPascalRandom(0x12345678);
        var core = Game(coreRandom);
        var session = new TurnSession(core.Field, core.Players, new TurnInput(), new TurnSettings());
        var round = new RoundPlay(core.Field, core.Players, session, settings, coreRandom, core.Start);
        var points = round.Play(_ => new TurnOutcome()).ToList();

        Assert.Null(current);
        Assert.True(playedBack);
        Assert.Equal(RoundPointKind.End, points[^1].Kind);
        Assert.Equal(round.WinnerTeam, winner);
        Assert.NotEqual(0, winner);
        Assert.Equal(coreRandom.Seed, random.Seed);
        Assert.Equal(Sha(core.Field.Screen.Pixels), Sha(game.Field.Screen.Pixels));
    }
}
