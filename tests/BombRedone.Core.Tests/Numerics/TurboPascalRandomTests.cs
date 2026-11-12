using BombRedone.Core.Numerics;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Random generator of the TP 6.0 runtime. The sequence below characterises the generator as it was in
/// BombRedone.Desktop before the move to Core (RandSeed := RandSeed * 08088405h + 1, Random(n) = high word
/// mod n); the same values come from the original routine 12CE:1675 via
/// <c>python3 tools/reverse-engineering/real48.py eval random_n &lt;seed&gt; &lt;n&gt;</c>.
/// </summary>
public sealed class TurboPascalRandomTests
{
    [Fact]
    public void Sequence_is_unchanged_by_the_move_to_core()
    {
        var random = new TurboPascalRandom(0x12345678);
        int[] ranges = [100, 0, 1, 2, 10, 320, 200, 65535, 7, 1000];
        int[] expected = [59, 0, 0, 0, 9, 79, 8, 51783, 3, 947];

        Assert.Equal(expected, ranges.Select(random.Next).ToArray());
        Assert.Equal(0xDE73BF76u, random.Seed);
    }

    [Fact]
    public void Random_n_matches_original_runtime()
    {
        Assert.Equal("3c6b43e91796983bbaec4e5d3aa50ea465b07ad7fd1a1d867372d34fc252d6c6",
            Real48ProbeCases.Hash("random_n", 20000));
    }

    [Fact]
    public void FromClock_puts_hours_and_minutes_into_the_low_word()
    {
        // Randomize (12CE:16FC) stores CX (hours, minutes) to DS:3AD6 and DX (seconds, hundredths) to DS:3AD8.
        var random = TurboPascalRandom.FromClock(new DateTime(2026, 11, 12, 12, 34, 56, 780));
        Assert.Equal(0x384E_0C22u, random.Seed);
    }

    [Theory]
    [InlineData(0x00000000u, "610000000000", 0x00000001u)] // new seed 1: 2^-32
    [InlineData(0x12345678u, "800059905B4B", 0xCB5B9059u)]
    [InlineData(0xFFFFFFFFu, "8000FC7BF777", 0xF7F77BFCu)]
    [InlineData(0x80000000u, "800001000000", 0x80000001u)]
    public void NextReal_matches_original(uint seed, string expected, uint newSeed)
    {
        var random = new TurboPascalRandom(seed);
        var value = random.NextReal();
        Assert.Equal(expected, value.ToHex());
        Assert.Equal(newSeed, random.Seed);
        Assert.Equal(newSeed / 4294967296.0, value.ToDouble());
    }

    [Fact]
    public void NextReal_is_zero_for_seed_zero()
    {
        // The seed whose successor is 0: s * 08088405h + 1 = 0 (mod 2^32).
        uint predecessor = unchecked((uint)((0UL - 1) * ModularInverse(0x08088405u)));
        var random = new TurboPascalRandom(predecessor);
        Assert.True(random.NextReal().IsZero);
        Assert.Equal(0u, random.Seed);
    }

    [Fact]
    public void Random_real_matches_original_runtime()
    {
        Assert.Equal("a0757e90d0868bf65094446ea6e370cf45fdc7a42423e52e31495110a660fcbe",
            Real48ProbeCases.Hash("random", 20000));
    }

    private static uint ModularInverse(uint a)
    {
        uint x = a; // Newton iteration for the inverse modulo 2^32 (a is odd)
        for (int i = 0; i < 5; i++)
        {
            x = unchecked(x * (2 - a * x));
        }

        return x;
    }
}
