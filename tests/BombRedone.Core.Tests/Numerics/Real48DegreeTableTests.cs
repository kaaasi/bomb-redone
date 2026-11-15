using BombRedone.Core.Numerics;
using BombRedone.Tests.Support;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// The degree table 0B25:00F9 (BOMB.EXE 0x0C9E9, 91 Real48 values) used by the repulser: computed here as Sin(a · pi /
/// 180) of the runtime, compared with the bytes of the original.
/// </summary>
public sealed class Real48DegreeTableTests
{
    private const int TableOffset = 0x0C8F0 + 0xF9;

    [GameDataFact]
    public void Sine_table_matches_the_original()
    {
        var exe = File.ReadAllBytes(TestGameData.Load().Require("BOMB.EXE"));
        for (var a = 0; a <= 90; a++)
        {
            var expected = Real48.FromBytes(exe.AsSpan(TableOffset + 6 * a, 6));
            Assert.Equal(expected, Real48.SinDegrees(a));
        }
    }

    [Theory]
    [InlineData(-30, 30)]
    [InlineData(210, 30)]
    [InlineData(150, 30)]
    [InlineData(-150, 30)]
    public void Folding_keeps_the_magnitude(int degrees, int reference)
    {
        var value = Real48.SinDegrees(degrees);
        var magnitude = value.IsNegative ? -value : value;

        Assert.Equal(Real48.SinDegrees(reference), magnitude);
        Assert.Equal(degrees is < 0 or > 180, value.IsNegative);
    }

    [Fact]
    public void Cosine_is_the_sine_of_the_complement()
    {
        for (var a = -90; a <= 270; a++)
            Assert.Equal(Real48.SinDegrees(90 - a), Real48.CosDegrees(a));
    }
}
