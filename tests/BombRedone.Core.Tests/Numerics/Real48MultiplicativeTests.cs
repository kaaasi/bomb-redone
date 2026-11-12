using BombRedone.Core.Numerics;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Reference cases for Mul, Sqr, Div, Trunc and Round; expected values from the original routines via
/// <c>python3 tools/reverse-engineering/real48.py eval</c> (docs/analyse/real48.md).
/// </summary>
public sealed class Real48MultiplicativeTests
{
    [Theory]
    [InlineData("820000000000", "830000000000", "840000000000")] // 2 * 4 = 8
    [InlineData("810000000080", "830000000000", "830000000080")] // -1 * 4
    [InlineData("000000000000", "830000000000", "000000000000")]
    [InlineData("830000000000", "0011223344FF", "000000000000")] // zero with mantissa bits
    [InlineData("81FFFFFFFF7F", "81FFFFFFFF7F", "82FEFFFFFF7F")]
    [InlineData("81AAAAAAAA2A", "81AAAAAAAA2A", "818CE3388E63")]
    [InlineData("81CDCCCCCC4C", "84000000001A", "846766666676")] // short path: low 24 bits of one factor are 0
    [InlineData("01FFFFFFFF7F", "01FFFFFFFF7F", "000000000000")] // underflow gives 0
    [InlineData("400000000000", "410000000000", "000000000000")]
    [InlineData("400000000000", "420000000000", "010000000000")] // smallest exponent survives
    public void Multiply_matches_original(string a, string b, string expected)
    {
        Assert.Equal(expected, (Real48.FromHex(a) * Real48.FromHex(b)).ToHex());
    }

    [Theory]
    [InlineData("FF0000000000", "820000000000")]
    [InlineData("C10000000000", "C00000000000")]
    [InlineData("C10000000000", "C10000000000")]
    public void Multiply_overflow_is_runtime_error_205(string a, string b)
    {
        var e = Assert.Throws<RunErrorException>(() => Real48.FromHex(a) * Real48.FromHex(b));
        Assert.Equal(RunErrorException.FloatingPointOverflow, e.Code);
    }

    [Theory]
    [InlineData("820000000000", "830000000000")]
    [InlineData("81CDCCCCCC4C", "82713D0AD723")]
    public void Square_matches_original(string a, string expected)
    {
        Assert.Equal(expected, Real48.Square(Real48.FromHex(a)).ToHex());
    }

    [Theory]
    [InlineData("810000000000", "830000000000", "7F0000000000")] // 1 / 4
    [InlineData("000000000000", "830000000000", "000000000000")]
    [InlineData("810000000000", "82000000003F", "7F83E2698F2B")] // 1 / 3.something
    [InlineData("820000000000", "820000000040", "80ABAAAAAA2A")] // 2 / 3
    [InlineData("840000000020", "8200000000C0", "8255555555D5")] // 10 / -3
    [InlineData("010000000000", "FF0000000000", "000000000000")] // underflow gives 0
    [InlineData("820000000000", "81FFFFFFFF7F", "810100000000")]
    public void Divide_matches_original(string a, string b, string expected)
    {
        Assert.Equal(expected, (Real48.FromHex(a) / Real48.FromHex(b)).ToHex());
    }

    [Theory]
    [InlineData("810000000000", "000000000000", RunErrorException.DivisionByZero)]
    [InlineData("000000000000", "000000000000", RunErrorException.DivisionByZero)]
    [InlineData("FF0000000000", "7F0000000000", RunErrorException.FloatingPointOverflow)]
    public void Divide_errors_match_original(string a, string b, int code)
    {
        var e = Assert.Throws<RunErrorException>(() => Real48.FromHex(a) / Real48.FromHex(b));
        Assert.Equal(code, e.Code);
    }

    [Theory]
    [InlineData("000000000000", 0)]
    [InlineData("7F0000000000", 0)]
    [InlineData("800000000000", 0)] // 0.5
    [InlineData("8000000000FF", 0)]
    [InlineData("820000000040", 3)]
    [InlineData("8200000000C0", -3)]
    [InlineData("9F00FEFFFF7F", int.MaxValue)]
    [InlineData("9FFFFFFFFF7F", int.MaxValue)] // the lowest mantissa byte is ignored
    public void Trunc_matches_original(string a, int expected)
    {
        Assert.Equal(expected, Real48.FromHex(a).Trunc());
    }

    [Theory]
    [InlineData("7FFFFFFFFF7F", 0)]
    [InlineData("800000000000", 1)] // 0.5 rounds away from zero
    [InlineData("800000000080", -1)]
    [InlineData("820000000020", 3)] // 2.5
    [InlineData("8200000000A0", -3)]
    [InlineData("8100000000FF", -2)]
    [InlineData("9F0000000080", int.MinValue + 0x40000000)]
    public void Round_matches_original(string a, int expected)
    {
        Assert.Equal(expected, Real48.FromHex(a).Round());
    }

    [Theory]
    [InlineData("A00000000080", false)] // -2^31 does not pass the exponent check
    [InlineData("A00000000000", false)]
    [InlineData("9FFFFFFFFF7F", true)] // rounds up to 2^31
    [InlineData("9F00FFFFFF7F", true)]
    public void Integer_conversion_errors_are_runtime_error_207(string a, bool roundOnly)
    {
        var value = Real48.FromHex(a);
        var e = Assert.Throws<RunErrorException>(() => value.Round());
        Assert.Equal(RunErrorException.InvalidFloatingPointOperation, e.Code);
        if (!roundOnly)
        {
            Assert.Equal(RunErrorException.InvalidFloatingPointOperation,
                Assert.Throws<RunErrorException>(() => value.Trunc()).Code);
        }
    }
}
