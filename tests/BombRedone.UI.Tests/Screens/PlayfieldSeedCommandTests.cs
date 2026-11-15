using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.Tools;
using BombRedone.UI.Tests.Tools;

namespace BombRedone.UI.Tests.Screens;

/// <summary><c>bombtool seed</c> with a time window (spur gelaende-bilder, Teil 1; Review #123 P2-1).</summary>
[Collection(ConsoleCollection.Name)]
public sealed class PlayfieldSeedCommandTests
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

    [Fact]
    public void Window_search_finds_the_time_of_a_round()
    {
        // Randomize at 08:15:30.40, 3 menu steps, shuffle, round with the defaults of BOMBCFG.EXE and 2 players.
        var random = new TurboPascalRandom(new DosTime(8, 15, 30, 40).Seed);
        for (var i = 0; i < 3; i++)
            random.Next(2);
        var before = random.Seed;
        SeedSearch.SkipShuffle(random, 2);
        var after = random.Seed;
        var field = Playfield.Build(PlayfieldOptions.FromConfig(GameConfig.BombCfgDefaults, 2), random);
        var wind = Wind.Choose((int)GameConfig.BombCfgDefaults.Wind, random).Value;
        var tanks = string.Join(",", field.Tanks.Select(t => t.X));
        var terrain = string.Join(",", new[] { 30, 150, 290 }.Select(x => $"{x}:{Playfield.TerrainTop(field, x)}"));

        // The hit lies in the last second of the window, which ends at the hit (review #128 P3-2).
        var (code, output, _) = Capture(() => RenderCommand.RunSeedSearch(
        [
            "--time-from", "08:15:29.00", "--time-to", "08:15:30.40", "--players", "2", "--max-steps", "5",
            "--tanks", tanks, "--terrain", terrain, "--background", field.BackgroundNumber.ToString(),
            "--wind", wind.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ]));

        Assert.Equal(0, code);
        Assert.Contains($"08:15:30.40 k 3: before shuffle {before:X8}, after {after:X8}", output);
    }

    [Fact]
    public void Prefilter_takes_the_margin_height_from_the_terrain_points_inside_the_margin()
    {
        var filter = RenderCommand.BuildPrefilter(2, [25, 281], [(20, 136), (70, 136), (71, 150), (100, 132)], 8, -92);

        Assert.Equal(new PlayfieldPrefilter(8, 54, 25, 281, -92), filter);
        // 3 players: the margin ends before x = 64; a point on y = 189 (height 0 or 1) inside it gives no margin height.
        Assert.Null(RenderCommand.BuildPrefilter(3, null, [(63, 189), (65, 120)], null, null).MarginHeight);
        Assert.Throws<ArgumentException>(() => RenderCommand.BuildPrefilter(2, null, [(20, 136), (30, 137)], null, null));
        Assert.Throws<ArgumentException>(() => RenderCommand.BuildPrefilter(2, [25], null, null, null));
    }

    [Theory]
    [InlineData("--time", "10:00:00.00", "--time-from", "09:00:00.00")]
    [InlineData("--time-from", "10:00:00.00", "--time-to", "09:00:00.00")]
    public void Window_search_rejects_bad_windows(params string[] window)
    {
        var (code, _, _) = Capture(() => RenderCommand.RunSeedSearch([.. window, "--players", "2"]));

        Assert.NotEqual(0, code);
    }
}
