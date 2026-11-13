using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>
/// Number of RandSeed steps per building block (docs/analyse/runde.md section 4, Abnahme of spur gelaende). Every
/// Random(n) and Random:Real is exactly one step.
/// </summary>
public sealed class RandomSequenceTests
{
    private static int Steps(uint seed, Action<TurboPascalRandom> action)
    {
        var random = new TurboPascalRandom(seed);
        action(random);
        return RandomSteps.Between(seed, random.Seed);
    }

    private static int DrawBackground(int number, TurboPascalRandom random) =>
        Backgrounds.Draw(number, new IndexBuffer(), new IndexBuffer(), new VgaPalette(), random) ? 1 : 0;

    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 9 + 1)]
    [InlineData(3, 9 + 1)]
    [InlineData(4, 9 + 1)]
    [InlineData(5, 1)]
    [InlineData(6, 9 + 299 * 159 + 1)]
    [InlineData(8, 9 + 1)]
    public void Background_uses_a_fixed_number_of_steps(int number, int steps) =>
        Assert.Equal(steps, Steps(0x12345678, r => DrawBackground(number, r)));

    [Fact]
    public void Stars_take_three_steps_per_star()
    {
        const uint seed = 0x00000001;
        var count = new TurboPascalRandom(seed).Next(500) + 250;
        Assert.Equal(1 + 3 * count + 1, Steps(seed, r => DrawBackground(1, r)));
    }

    [Fact]
    public void Rectangle_background_takes_one_step_for_the_frame_count() =>
        Assert.Equal(9 + 1 + 1, Steps(0xCAFEBABE, r => DrawBackground(7, r)));

    [Fact]
    public void Gradient_palette_takes_nine_steps()
    {
        Assert.Equal(9, Steps(0x9E3779B9, r => Backgrounds.GradientPalette(new VgaPalette(), r)));
    }

    [Fact]
    public void Gradient_palette_interpolates_in_real48()
    {
        // Random(64) gives 0..63; a gradient from 0 to 63 in tenths: Trunc(k * 6.3) for k = 0..9.
        var palette = new VgaPalette();
        Backgrounds.GradientPalette(palette, new TurboPascalRandom(0x9E3779B9));
        for (var i = 30; i < 60; i++)
            Assert.True(palette.IsWritten(i));
        Assert.False(palette.IsWritten(29));
        Assert.False(palette.IsWritten(60));
    }

    [Fact]
    public void Random_wall_and_background_add_their_calls()
    {
        // Same seed and players: RND wall adds Random(5) before everything else.
        const uint seed = 0x2468ACE0;
        var fixedWall = Steps(seed, r => Core.Playfield.Playfield.Build(new PlayfieldOptions(2, 3, 0), r));
        var random = new TurboPascalRandom(seed);
        var wall = random.Next(5);
        var rest = Steps(random.Seed, r => Core.Playfield.Playfield.Build(new PlayfieldOptions(2, wall, 0), r));
        Assert.Equal(1 + rest, Steps(seed, r => Core.Playfield.Playfield.Build(new PlayfieldOptions(2, 5, 0), r)));
        Assert.True(fixedWall > 0);
    }

    [Fact]
    public void Wind_zero_takes_one_try_and_four_reals() =>
        Assert.Equal(1 + 4, Steps(0x12345678, r => Assert.Equal(new Wind(0, 0), Wind.Choose(0, r))));

    [Fact]
    public void Random_wind_level_adds_one_step()
    {
        const uint seed = 0x0BADF00D;
        var random = new TurboPascalRandom(seed);
        var level = random.Next(5);
        var rest = Steps(random.Seed, r => Wind.Choose(level, r));
        Assert.Equal(1 + rest, Steps(seed, r => Assert.Equal(level, Wind.Choose(5, r).Level)));
    }

    [Theory]
    [InlineData(0x00015446u, 30000)]
    [InlineData(0x002B423Bu, 3008)]
    public void Wind_multiplies_in_16_bits_before_clamping(uint seedAfterSetup, int expected)
    {
        // #98: −548 · 4³ = −35072 wraps to 30464 (clamped to 30000), −977 · 4³ = −62528 wraps to 3008;
        // values confirmed by the probe cases of PlayfieldProbeTests.
        var random = new TurboPascalRandom(seedAfterSetup);
        Core.Playfield.Playfield.Build(new PlayfieldOptions(2, 0, 0, 0xFFFF, 4), random);

        Assert.Equal(new Wind(4, expected), Wind.Choose(4, random));
    }

    [Fact]
    public void Start_player_repeats_until_a_record_with_fewest_starts()
    {
        var rounds = new[] { 1, 0, 1 };
        var random = new TurboPascalRandom(0x13579BDF);
        var start = StartPlayer.Choose(rounds, [true, true, true], random);

        Assert.Equal(new StartPlayer(2, 1), start);
        Assert.Equal(new[] { 1, 1, 1 }, rounds);
    }

    [Fact]
    public void Start_player_skips_inactive_records_and_wraps_with_the_pass()
    {
        // Only record 1 is active: the move goes to record 1; from s = 1 or 2 (records 2, 3) the search wraps, pass 2.
        for (uint seed = 1; seed < 40; seed++)
        {
            var random = new TurboPascalRandom(seed);
            var s = new TurboPascalRandom(seed).Next(3);
            var start = StartPlayer.Choose(new int[3], [true, false, false], random);
            Assert.Equal(1, start.Player);
            Assert.Equal(s == 0 ? 1 : 2, start.Pass);
        }
    }
}
