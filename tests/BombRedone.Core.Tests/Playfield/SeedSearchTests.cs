using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>Shuffle steps and the start value search (R2-D3, docs/analyse/runde.md section 4).</summary>
public sealed class SeedSearchTests
{
    /// <summary>
    /// RandSeed after the shuffle O3:2417 of the original:
    /// <c>feld.py run --seed 1234ABCD --players N --before-shuffle</c> (field seed_after_shuffle).
    /// </summary>
    [Theory]
    [InlineData(2, 0x7FE86108u)]
    [InlineData(5, 0x19B34590u)]
    [InlineData(10, 0x92560E34u)]
    public void Shuffle_takes_the_steps_of_the_original(int players, uint expected)
    {
        var random = new TurboPascalRandom(0x1234ABCD);
        var swaps = SeedSearch.SkipShuffle(random, players);

        Assert.InRange(swaps, 10, 29);
        Assert.Equal(expected, random.Seed);
    }

    [Fact]
    public void Search_finds_the_number_of_steps_before_the_shuffle()
    {
        const uint start = 0x0C1E2D3Fu;
        var options = new PlayfieldOptions(4, 2, 7);
        var random = new TurboPascalRandom(start);
        for (var i = 0; i < 37; i++)
            random.Next(2);
        SeedSearch.SkipShuffle(random, 4);
        var field = Core.Playfield.Playfield.Build(options, random);
        var target = new PlayfieldTarget(
            field.Tanks.Select(t => t.X).ToArray(),
            [(40, Core.Playfield.Playfield.TerrainTop(field, 40)), (160, Core.Playfield.Playfield.TerrainTop(field, 160))]);

        var found = SeedSearch.Search(start, 60, options, target).ToList();

        // The shuffle takes a varying number of steps, so several k end in the same seed after it (here 26, 37, 60):
        // the playfield fixes the seed after the shuffle, not the menu steps before it.
        Assert.Contains(found, c => c.StepsBeforeShuffle == 37);
        Assert.Single(found.Select(c => c.SeedAfterShuffle).Distinct());
    }

    [Fact]
    public void Empty_target_matches_every_candidate() =>
        Assert.Equal(4, SeedSearch.Search(1, 3, new PlayfieldOptions(2), new PlayfieldTarget()).Count());

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(12, 34, 56, 78)]
    [InlineData(17, 12, 39, 69)]
    [InlineData(23, 59, 59, 99)]
    public void Dos_time_gives_the_seed_of_Randomize(int hour, int minute, int second, int hundredths)
    {
        var time = new DosTime(hour, minute, second, hundredths);
        var clock = TurboPascalRandom.FromClock(DateTime.MinValue.Add(new TimeSpan(0, hour, minute, second, hundredths * 10)));

        Assert.Equal(clock.Seed, time.Seed);
        Assert.Equal(time, DosTime.FromTicks(time.Ticks));
    }

    /// <summary>The prefilter steps through the same Random calls as Playfield.Build and Wind.Choose.</summary>
    [Theory]
    [InlineData(2, 0, PlayfieldOptions.RandomBackground, 2)]
    [InlineData(3, PlayfieldOptions.RandomWall, PlayfieldOptions.UserBackground, PlayfieldOptions.RandomWind)]
    [InlineData(5, 1, 1, 4)]
    [InlineData(10, 4, 7, 3)]
    [InlineData(4, 0, 6, 1)]
    public void Prefilter_agrees_with_the_playfield(int players, int wall, int background, int windOption)
    {
        var options = new PlayfieldOptions(players, wall, background, 0x0124, windOption);
        var seed = 0x2468ACE1u;
        for (var i = 0; i < (background == 6 ? 4 : 60); i++)
        {
            seed = unchecked(seed * 0x41C64E6Du + 12345u);
            var random = new TurboPascalRandom(seed);
            var field = Core.Playfield.Playfield.Build(options, random);
            var wind = Wind.Choose(windOption, random).Value;
            var margin = 190 - Core.Playfield.Playfield.TerrainTop(field, 11);
            var exact = new PlayfieldPrefilter(field.BackgroundNumber, margin, field.Tanks[0].X, field.Tanks[^1].X, wind);

            Assert.True(exact.Passes(seed, options, windOption));
            Assert.False((exact with { LastTankX = field.Tanks[^1].X + 1 }).Passes(seed, options, windOption));
            Assert.False((exact with { WindValue = wind + 1 }).Passes(seed, options, windOption));
            Assert.False((exact with { MarginHeight = margin + 1 }).Passes(seed, options, windOption));
        }
    }

    /// <summary>
    /// s3_02 (DOSBox session 3, round 1 before the first shot; options of ablauf1-GAME1.TWS = defaults of BOMBCFG.EXE with
    /// 2 players): measured in the picture are background 8 (Asterisk), tanks at x 25 and 281, the topmost terrain pixel
    /// of some columns and wind −92. The whole day searched with k ≤ 30 gives one time (docs/analyse/feld.md section 6).
    /// </summary>
    [Fact]
    public void Window_search_finds_the_start_value_of_session_3()
    {
        var options = PlayfieldOptions.FromConfig(GameConfig.BombCfgDefaults, 2);
        var target = new PlayfieldTarget([25, 281], [(20, 136), (100, 132), (160, 161), (200, 185), (250, 141), (300, 141)]);
        var prefilter = new PlayfieldPrefilter(8, 54, 25, 281, -92);

        var found = SeedSearch.SearchWindow(new DosTime(17, 12, 39, 0), new DosTime(17, 12, 40, 99), 30, options,
            (int)GameConfig.BombCfgDefaults.Wind, prefilter, target).ToList();

        var hit = Assert.Single(found);
        Assert.Equal(new DosTime(17, 12, 39, 69), hit.Time);
        Assert.Equal(new SeedCandidate(1, 0x4983853Du, 0xD2E3BA6Eu), hit.Candidate);
    }

    [Fact]
    public void Window_search_passes_every_time_of_the_window_to_an_empty_target()
    {
        var found = SeedSearch.SearchWindow(new DosTime(9, 59, 59, 98), new DosTime(10, 0, 0, 1), 2, new PlayfieldOptions(2),
            0, new PlayfieldPrefilter(), new PlayfieldTarget()).ToList();

        Assert.Equal(12, found.Count);
        Assert.Equal(new DosTime(10, 0, 0, 0), found[6].Time);
        Assert.Equal(new DosTime(10, 0, 0, 0).Seed, found[6].Candidate.SeedBeforeShuffle);
    }
}
