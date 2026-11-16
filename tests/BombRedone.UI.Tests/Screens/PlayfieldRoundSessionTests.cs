using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Setup;
using BombRedone.Core.Shot;
using BombRedone.Core.Turn;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Round 1 of DOSBox session 3 as a whole (spur runde, acceptance s3_07 and s3_08, docs/analyse/feld.md sections 7 and 9):
/// the battle (<see cref="BattleSequence"/> without round object) shuffles, <see cref="RoundGame.Prepare"/> builds the round,
/// <see cref="RoundPlay"/> plays the four shots of <see cref="PlayfieldShotSessionTests"/> up to the death of record 2 and the
/// round end, then <see cref="ScoreBattleFlow"/> shows the status screen. Pictures are never committed (AGENTS.md 2.1).
/// </summary>
public sealed class PlayfieldRoundSessionTests
{
    private static readonly (int Record, int Angle, int Direction, int Power)[] Shots =
    [
        (2, 75, -1, 200),
        (1, 75, 1, 272),
        (2, 38, -1, 241),
        (1, 30, 1, 272),
    ];

    /// <summary>Mouse pointer of s3_08 (DOSBox draws it into the picture): x 165..175, y 154..169.</summary>
    private static bool InPointer(int i) => i % 320 is >= 165 and <= 175 && i / 320 is >= 154 and <= 169;

    /// <summary>
    /// The glyphs "5" of "50" (x 259, y 110/111) and "a" of "Save" (x 236..240, y 135/137) of the status screen font differ
    /// by one pixel (review P3-2, #175, spur p3-runde-3; not this spur).
    /// </summary>
    private static bool InKnownGlyphs(int i) =>
        (i % 320 == 259 && i / 320 is 110 or 111) || (i % 320 is >= 236 and <= 240 && i / 320 is 135 or 137);

    /// <summary>
    /// Record 2 dies with Random(7) = 1 (particles O5:0DA0, RandSeed 7C99828F before the choice). At the last frame of the
    /// picture the field equals s3_07; after the frame O7:0364 (bottom line y = 189 drawn anew) and O7:03C1 only the 30 pixels
    /// of the bottom line differ: the picture shows the background buffer there, the remake terrain colour 2. The probe
    /// (tod.py, death point) also has colour 2 in y = 189, so s3_07 was taken between the picture and O7:0364.
    /// </summary>
    [GameDataFact]
    public void The_death_of_record_2_matches_s3_07_before_the_frame_is_redrawn()
    {
        var run = Play();

        Assert.Equal([RoundPointKind.Death, RoundPointKind.End], run.Points.Where(p => p.Kind is RoundPointKind.Death
            or RoundPointKind.End).Select(p => p.Kind));
        var death = run.Points.Single(p => p.Kind == RoundPointKind.Death);
        Assert.Equal((2, 1), (death.Player, death.Choice));
        Assert.Equal(0x7C99828Fu, run.SeedBeforeChoice);

        var (pixels, palette) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath("s3_07_r1-spielfeld-nach-einschlag.png"));
        Assert.NotNull(run.PictureEnd);
        Assert.Empty(FieldDifferences(pixels, run.PictureEnd));

        var after = FieldDifferences(pixels, run.AfterDeath);
        Assert.Equal(30, after.Count);
        Assert.All(after, i =>
        {
            Assert.Equal(189, i / 320);
            Assert.InRange(i % 320, 219, 307);
            Assert.Equal(Playfield.TerrainColor, run.AfterDeath[i]);
            Assert.Equal(run.Field.Background.Pixels[i], pixels[i]);
        });

        // The colour divisors follow the player (#153): every colour of the field that the game sets has the DAC of the
        // picture. The others (terrain 2, explosion 13) are never set and keep the EGA colours of the VGA BIOS.
        var used = FieldIndices().Select(i => (int)pixels[i]).Distinct().ToList();
        Assert.All(used.Where(c => !run.Field.Palette.IsDefined(c)), c => Assert.InRange(c, 0, 15));
        Assert.All(used.Where(run.Field.Palette.IsDefined), c => Assert.Equal(palette.AsSpan(3 * c, 3).ToArray(),
            PlayfieldSession.Rgb8(run.DacAtPictureEnd.AsSpan(3 * c, 3))));
    }

    /// <summary>
    /// Status screen after game 1: "Player 2" 1 win, 4490 points, "Player 1" 0 and 50 as in s3_08 and ablauf1-GAME1.TWS.
    /// Equal pixel for pixel but the mouse pointer and the two glyphs of P3-2; the DAC of every used colour is equal except
    /// colour 0 (review P3-1, #174: the original keeps DAC 0 of the round over the change of screens).
    /// </summary>
    [GameDataFact]
    public void The_status_screen_after_game_1_matches_s3_08()
    {
        var run = Play();
        var score = Assert.IsType<ScoreScreen>(run.Rating);
        Assert.Equal([("Player 2", 1, 4490), ("Player 1", 0, 50)],
            score.Standings.Select(s => (s.Player.Name, s.Player.Wins, s.Player.Score)));

        var (pixels, palette) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath("s3_08_status-nach-runde-1.png"));
        var differences = Enumerable.Range(0, 320 * 200).Where(i => pixels[i] != score.Screen.Pixels[i]).ToList();
        Assert.Equal(8, differences.Count(InKnownGlyphs));
        Assert.All(differences, i => Assert.True(InPointer(i) || InKnownGlyphs(i), $"x {i % 320}, y {i / 320}"));

        var used = Enumerable.Range(0, 320 * 200).Where(i => !InPointer(i)).Select(i => (int)pixels[i]).Distinct().ToList();
        Assert.Contains(0, used);
        Assert.All(used.Where(c => c != 0), c => Assert.Equal(palette.AsSpan(3 * c, 3).ToArray(),
            PlayfieldSession.Rgb8(score.Dac.Slice(3 * c, 3))));
        Assert.Equal(palette.AsSpan(0, 3).ToArray(), PlayfieldSession.Rgb8(run.Field.Palette.Rgb.Slice(0, 3)));
    }

    private sealed record Run(IReadOnlyList<RoundPoint> Points, uint SeedBeforeChoice, byte[]? PictureEnd, byte[] DacAtPictureEnd,
        byte[] AfterDeath, Playfield Field, IScreen? Rating);

    /// <summary>
    /// The battle of session 3 from <see cref="PlayfieldSession.SeedBeforeShuffle"/>: two keyboard players, options of
    /// ablauf1-GAME1.TWS (BOMBCFG defaults). Between the shots the barrel is erased as in
    /// <see cref="PlayfieldShotSessionTests"/> ([APPROX] the turn O7:040C is not part of this test).
    /// </summary>
    private static Run Play()
    {
        var gameData = TestGameData.Load();
        var fontPath = BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories())
            ?? throw new InvalidOperationException(
                "Reference font missing (tools/claude/fetch-reference.sh fetches schriften/ to ~/referenz).");
        var shopFonts = ShopFonts.Load(gameData, BitmapFontSource.ReadReference(fontPath));
        var config = GameConfig.BombCfgDefaults;
        var random = new TurboPascalRandom(PlayfieldSession.SeedBeforeShuffle);
        var records = new BattleRecords(Enumerable.Range(1, 2).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null, i)));
        RoundGame.InitializeRecords(records);
        var battle = new BattleSequence(records, config.GamesToPlay, false, random, null, ItemCatalog.Original);

        var points = new List<RoundPoint>();
        uint seedBeforeChoice = 0;
        byte[]? pictureEnd = null;
        var dac = Array.Empty<byte>();
        byte[] afterDeath = [];
        Playfield? field = null;

        IScreen? PlayRound(Func<int, IScreen?> complete)
        {
            Assert.Equal(0xD2E3BA6Eu, random.Seed);
            var game = RoundGame.Prepare(records, PlayfieldOptions.FromConfig(config, 2), random);
            field = game.Field;
            Assert.Equal(-92, game.Wind.Value);
            Assert.Equal(2, game.Start.Player);
            Assert.Equal(["Player 2", "Player 1"], game.Players.Select(p => p.Name));

            var dying = false;
            void Frame(ShotFrame kind)
            {
                // The frames of the death picture; Crumble and falling after O7:0364 report Landslide and Fall.
                if (dying && kind is not (ShotFrame.Landslide or ShotFrame.Fall))
                {
                    pictureEnd = game.Field.Screen.Pixels.ToArray();
                    dac = game.Field.Palette.Rgb.ToArray();
                }
            }

            var settings = new RoundSettings(config.ShotSpeed, game.Wind.Value, config.CrumblePercent, config.TanksFall,
                config.SoundEffects, config.ShotTrail, Frame);
            var session = new TurnSession(game.Field, game.Players, new TurnInput(), new TurnSettings());
            var round = new RoundPlay(game.Field, game.Players, session, settings, random, game.Start);
            var shot = 0;
            foreach (var point in round.Play(player => Turn(game.Field, player, shot++)))
            {
                points.Add(point);
                if (point.Kind == RoundPointKind.DeathStart)
                {
                    seedBeforeChoice = random.Seed;
                    dying = true;
                }
                else if (point.Kind == RoundPointKind.Death)
                {
                    dying = false;
                    afterDeath = game.Field.Screen.Pixels.ToArray();
                }
            }

            Assert.Equal(4, shot);
            game.Finish();
            return complete(round.WinnerTeam);
        }

        IScreen? rating = null;
        var flow = new ScoreBattleFlow(shopFonts, battle, ItemCatalog.Original, false, RecordColor, _ => "", () => null,
            complete =>
            {
                rating = PlayRound(complete);
                return rating;
            });
        var started = flow.Start();
        Assert.Same(rating, started);
        return new Run(points, seedBeforeChoice, pictureEnd, dac, afterDeath, field!, rating);
    }

    private static TurnOutcome? Turn(Playfield field, TurnPlayer player, int shot)
    {
        if (shot >= Shots.Length)
            return null;
        var (record, angle, direction, power) = Shots[shot];
        var tank = player.Tank;
        Assert.Equal(record, tank.Number);
        Assert.Equal(direction, tank.Direction);
        if (tank.Angle != angle)
        {
            var (dx, dy) = Playfield.BarrelOffset(tank.Angle);
            VgaDraw.CopyLine(field.Background.Pixels, field.Screen.Pixels, tank.X, tank.Y, tank.X + dx * tank.Direction,
                tank.Y - dy);
            tank.Aim(angle, power);
            field.DrawTank(tank);
        }

        tank.Aim(angle, power);
        tank.Weapon = 1;
        return new TurnOutcome();
    }

    /// <summary>6-bit colour of a record at full energy (O10:01AC with the divisors of its player, as DesktopGame).</summary>
    private static (int Red, int Green, int Blue) RecordColor(BattlePlayer record)
    {
        var (r, g, b) = Playfield.ColorDivisors[record.Account.Number - 1];
        return (63 / r, 63 / g, 63 / b);
    }

    private static IEnumerable<int> FieldIndices()
    {
        for (var y = PlayfieldScreen.FieldTop; y <= PlayfieldScreen.FieldBottom; y++)
        {
            for (var x = PlayfieldScreen.FieldLeft; x <= PlayfieldScreen.FieldRight; x++)
                yield return y * 320 + x;
        }
    }

    private static List<int> FieldDifferences(byte[] pixels, byte[] ours) =>
        FieldIndices().Where(i => pixels[i] != ours[i]).ToList();
}
