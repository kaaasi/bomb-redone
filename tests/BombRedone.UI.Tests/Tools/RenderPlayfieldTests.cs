using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.Tools;

namespace BombRedone.UI.Tests.Tools;

/// <summary><c>bombtool render playfield</c> and <c>bombtool seed</c> (spur gelaende, Teil 4).</summary>
[Collection(ConsoleCollection.Name)]
public sealed class RenderPlayfieldTests
{
    private static (int Code, string Output, string Error) Capture(Func<int> run)
    {
        var (output, error) = (new StringWriter(), new StringWriter());
        var (oldOut, oldError) = (Console.Out, Console.Error);
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            return (run(), output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
        }
    }

    [GameDataFact]
    public void Render_builds_the_round_of_the_core_and_draws_it()
    {
        var screen = RenderCommand.RenderPlayfield(TestGameData.Load(), GameConfig.BombCfgDefaults, 0xDEADBEEF, 5,
            out var report);

        var random = new TurboPascalRandom(0xDEADBEEF);
        var field = Playfield.Build(PlayfieldOptions.FromConfig(GameConfig.BombCfgDefaults, 5), random);
        Assert.Equal(field.Background.Pixels, screen.Field.Background.Pixels);
        Assert.Equal("tanks " + string.Join(" ", field.Tanks.Select(t => $"{t.X},{t.Y}")), report[2]);
        Assert.StartsWith("seed DEADBEEF, after set-up " + random.Seed.ToString("X8"), report[0]);
    }

    [Fact]
    public void Render_rejects_unknown_options_and_player_counts()
    {
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--colour", "3"])).Code);
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "a", "b", "c"])).Code);
    }

    [Fact]
    public void Render_rejects_bad_shots()
    {
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--shot", "45"])).Code);
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--shot", "181,300"])).Code);
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--shot", "45,300,5"])).Code);
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--shooter", "1"])).Code);
        Assert.Equal(2, Capture(() => RenderCommand.Run(["playfield", "--shot", "45,300", "--shooter", "3"])).Code);
    }

    [GameDataFact]
    public void Render_with_a_shot_fires_like_the_core_and_shows_the_field_afterwards()
    {
        var config = GameConfig.BombCfgDefaults;
        var screen = RenderCommand.RenderPlayfield(TestGameData.Load(), config, 0x12345678, 2, out var report, [45, 270, 1], 1);

        // The same round and shot with Core alone (shot 3 of feld_schuss.py, here with the options of BOMBCFG.EXE).
        var random = new TurboPascalRandom(0x12345678);
        var field = Playfield.Build(PlayfieldOptions.FromConfig(config, 2), random);
        var wind = Wind.Choose((int)config.Wind, random);
        StartPlayer.Choose(new int[2], [true, true], random);
        field.Tanks[0].Aim(45, 270);
        field.Tanks[0].Weapon = 1;
        Core.Shot.BallisticShot.Fire(field, field.Tanks[0], new Core.Shot.ShotSettings(config.ShotSpeed, wind.Value,
            config.ShotTrail, config.SoundEffects), random);
        Core.Shot.AfterShot.Run(field, config.CrumblePercent, config.TanksFall, random);

        Assert.Contains("RandSeed " + random.Seed.ToString("X8"), report[3]);
        Assert.Equal(field.Tanks[1].Energy, screen.Field.Tanks[1].Energy);
        for (var y = 28; y <= 192; y++)
        {
            for (var x = 8; x <= 312; x++)
                Assert.Equal(field.Screen[x, y], screen.Screen[x, y]);
        }
    }

    [Fact]
    public void Seed_search_finds_a_round_from_the_tank_positions()
    {
        // Randomize at 12:34:56.78, 25 menu steps, then the shuffle and the round with the defaults of BOMBCFG.EXE.
        var random = TurboPascalRandom.FromClock(DateTime.MinValue.Add(new TimeSpan(0, 12, 34, 56, 780)));
        for (var i = 0; i < 25; i++)
            random.Next(2);
        SeedSearch.SkipShuffle(random, 3);
        var field = Playfield.Build(PlayfieldOptions.FromConfig(GameConfig.BombCfgDefaults, 3), random);
        var tanks = string.Join(",", field.Tanks.Select(t => t.X));

        var (code, output, _) = Capture(() => RenderCommand.RunSeedSearch(
            ["--time", "12:34:56.78", "--players", "3", "--max-steps", "40", "--tanks", tanks]));

        Assert.Equal(0, code);
        Assert.Contains("k 25: ", output);
    }

    [Fact]
    public void Seed_search_needs_time_and_players() =>
        Assert.Equal(2, Capture(() => RenderCommand.RunSeedSearch(["--players", "3"])).Code);
}
