using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

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
}
