using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>What a picture of the original shows about a playfield (all parts optional).</summary>
/// <param name="TankX">x of the tanks from left to right (record +0Fh of players 1..N).</param>
/// <param name="TerrainTop">Pairs (x, y) of the topmost terrain pixel of a column (colour 2), e.g. read from a screenshot.</param>
public sealed record PlayfieldTarget(IReadOnlyList<int>? TankX = null, IReadOnlyList<(int X, int Y)>? TerrainTop = null)
{
    public bool Matches(Playfield field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (TankX is not null && !TankX.SequenceEqual(field.Tanks.Select(t => t.X)))
            return false;
        if (TerrainTop is not null)
        {
            foreach (var (x, y) in TerrainTop)
            {
                if (Playfield.TerrainTop(field, x) != y)
                    return false;
            }
        }

        return true;
    }
}

/// <summary>A start value that reproduces the target.</summary>
/// <param name="StepsBeforeShuffle">Random calls between Randomize and the shuffle (menu, intelligences: runde.md section 4).</param>
/// <param name="SeedBeforeShuffle">RandSeed before the shuffle.</param>
/// <param name="SeedAfterShuffle">RandSeed after the shuffle, the input of <see cref="Playfield.Build"/>.</param>
public readonly record struct SeedCandidate(int StepsBeforeShuffle, uint SeedBeforeShuffle, uint SeedAfterShuffle);

/// <summary>
/// Search for the start value of the random generator behind a picture of the original (R2-D3, docs/analyse/runde.md
/// section 4 "Vorschlag"): from RandSeed after Randomize try k = 0..K further steps (main menu, intelligences), then
/// the steps of the shuffle and the set-up of the playfield, and compare with the picture.
/// </summary>
public static class SeedSearch
{
    /// <summary>
    /// Advances <paramref name="random"/> like the shuffle O3:2417 (runde.md section 1, S3): Random(20) + 10 swaps, each
    /// a = Random(N) + 1 and b = Random(N) + 1 repeated until b ≠ a. Only the steps are taken; the records are not swapped
    /// (R2-D4 = A: the order of the records follows with the game sequence in round 3).
    /// </summary>
    /// <returns>Number of swaps.</returns>
    public static int SkipShuffle(TurboPascalRandom random, int players)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (players < 2)
            throw new ArgumentOutOfRangeException(nameof(players));
        var swaps = random.Next(20) + 10;
        for (var i = 0; i < swaps; i++)
        {
            var a = random.Next(players) + 1;
            int b;
            do
            {
                b = random.Next(players) + 1;
            }
            while (b == a);
        }

        return swaps;
    }

    /// <summary>All k in 0..<paramref name="maxSteps"/> whose playfield matches <paramref name="target"/>.</summary>
    public static IEnumerable<SeedCandidate> Search(uint seedAfterRandomize, int maxSteps, PlayfieldOptions options,
        PlayfieldTarget target)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSteps);
        var seed = seedAfterRandomize;
        for (var k = 0; k <= maxSteps; k++)
        {
            var random = new TurboPascalRandom(seed);
            SkipShuffle(random, options.Players);
            var afterShuffle = random.Seed;
            if (target.Matches(Playfield.Build(options, random)))
                yield return new SeedCandidate(k, seed, afterShuffle);
            seed = unchecked(seed * 0x08088405u + 1);
        }
    }
}
