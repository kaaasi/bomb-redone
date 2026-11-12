using BombRedone.Core.Numerics;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Readable reference cases for Real48. Operands and results are 6-byte memory images in hex (byte 0 exponent);
/// every expected value was produced by the original routine via
/// <c>python3 tools/reverse-engineering/real48.py eval &lt;op&gt; &lt;a&gt; [&lt;b&gt;]</c> (docs/analyse/real48.md).
/// </summary>
public sealed class Real48Tests
{
    [Fact]
    public void Bytes_round_trip_and_layout()
    {
        var value = Real48.FromHex("8500000000E0"); // -28
        Assert.Equal("8500000000E0", value.ToHex());
        Assert.Equal(0x85, value.Exponent);
        Assert.True(value.IsNegative);
        Assert.False(value.IsZero);
        Assert.Equal(-28.0, value.ToDouble());
    }

    [Fact]
    public void Exponent_zero_is_zero_whatever_the_mantissa()
    {
        var garbage = Real48.FromHex("0012345678FF");
        Assert.True(garbage.IsZero);
        Assert.Equal(0.0, garbage.ToDouble());
        Assert.Equal(Real48.Zero, Real48.FromInt32(0));
        Assert.Equal(1.0, Real48.One.ToDouble());
    }

    [Theory]
    [InlineData(0, "000000000000")]
    [InlineData(1, "810000000000")]
    [InlineData(-1, "810000000080")]
    [InlineData(255, "88000000007F")]
    [InlineData(256, "890000000000")]
    [InlineData(65535, "90000000FF7F")]
    [InlineData(65536, "910000000000")]
    [InlineData(-32768, "900000000080")]
    [InlineData(int.MaxValue, "9F00FEFFFF7F")]
    [InlineData(int.MinValue, "A00000000080")]
    public void FromInt32_matches_original(int value, string expected)
    {
        var real = Real48.FromInt32(value);
        Assert.Equal(expected, real.ToHex());
        Assert.Equal(value, real.ToDouble());
    }

    [Theory]
    [InlineData("810000000000", "810000000000", "820000000000")] // 1 + 1 = 2
    [InlineData("810000000000", "590000000000", "810000000000")] // 1 + 2^-40: exponent gap 40 returns the larger
    [InlineData("810000000000", "5A0000000000", "810100000000")] // 1 + 2^-39: last mantissa bit
    [InlineData("810000000000", "5B0000000000", "810200000000")]
    [InlineData("81FFFFFFFF7F", "5A0000000000", "820000000000")] // carry out of the mantissa
    [InlineData("81FFFFFFFF7F", "590000000000", "81FFFFFFFF7F")]
    [InlineData("810000000000", "810000000080", "000000000000")] // 1 + (-1)
    [InlineData("820000000000", "81FFFFFFFF7F", "830000000000")] // rounding on the guard byte carries over
    [InlineData("000000000000", "83AABBCCDD80", "83AABBCCDD80")] // zero returns the other operand unchanged
    [InlineData("0012345678FF", "820000000000", "820000000000")]
    [InlineData("811111111111", "000000000080", "811111111111")]
    public void Add_matches_original(string a, string b, string expected)
    {
        Assert.Equal(expected, (Real48.FromHex(a) + Real48.FromHex(b)).ToHex());
    }

    [Theory]
    [InlineData("810000000000", "810000000000", "000000000000")]
    [InlineData("820000000000", "810000000000", "810000000000")] // 2 - 1
    [InlineData("830000000000", "860000000000", "8500000000E0")] // 4 - 32 = -28
    [InlineData("010000000000", "01FFFFFFFF7F", "000000000000")] // underflow while normalising gives 0
    [InlineData("020000000000", "01FFFFFFFF7F", "000000000000")]
    public void Subtract_matches_original(string a, string b, string expected)
    {
        Assert.Equal(expected, (Real48.FromHex(a) - Real48.FromHex(b)).ToHex());
    }

    [Fact]
    public void Add_overflow_is_runtime_error_205()
    {
        var max = Real48.FromHex("FFFFFFFFFF7F");
        var e = Assert.Throws<RunErrorException>(() => max + max);
        Assert.Equal(RunErrorException.FloatingPointOverflow, e.Code);
    }

    [Theory]
    [InlineData("810000000000", "810000000000", 0)]
    [InlineData("000000000000", "000000000080", 1)] // +0 > -0: different signs decide by the sign bit
    [InlineData("0012340000FF", "000000000000", -1)]
    [InlineData("820000000000", "81FFFFFFFF7F", 1)]
    [InlineData("820000000080", "810000000080", -1)] // -2 < -1
    [InlineData("000000000000", "810000000080", 1)]
    [InlineData("81000000AA00", "81000000AB00", -1)]
    [InlineData("81AA00000000", "81AB00000000", -1)]
    public void Compare_matches_original(string a, string b, int expected)
    {
        Assert.Equal(expected, Real48.Compare(Real48.FromHex(a), Real48.FromHex(b)));
    }

    [Fact]
    public void Comparison_operators_follow_Compare()
    {
        var one = Real48.One;
        var two = Real48.FromInt32(2);
        Assert.True(one < two);
        Assert.True(two > one);
        Assert.True(one <= Real48.One);
        Assert.True(two >= one);
        Assert.False(Real48.FromHex("000000000000") == Real48.FromHex("000000000080"));
    }

    [Fact]
    public void Negation_flips_the_sign_bit()
    {
        Assert.Equal("810000000080", (-Real48.One).ToHex());
        Assert.Equal("000000000080", (-Real48.Zero).ToHex());
    }
}
