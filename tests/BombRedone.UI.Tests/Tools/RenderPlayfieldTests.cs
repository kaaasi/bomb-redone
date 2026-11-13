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
