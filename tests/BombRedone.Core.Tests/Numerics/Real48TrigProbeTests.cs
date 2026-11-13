using System.Security.Cryptography;
using BombRedone.Core.Numerics;

namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Bit-exact comparison of Sin, Cos and Sqrt with the original runtime. Expected hashes from
/// <c>python3 tools/reverse-engineering/real48.py hashes --count 20000</c> (ops sin, cos, sqrt with the generator of
/// <see cref="Real48ProbeCases"/>, seeds 12..14) and the line <c>deg</c> (whole degrees -360..720 through Rad, Sin, Cos).
/// </summary>
public sealed class Real48TrigProbeTests
{
    private const int Count = 20000;

    private static readonly Dictionary<string, ulong> Seeds = new() { ["sin"] = 12, ["cos"] = 13, ["sqrt"] = 14 };

    [Theory]
    [InlineData("sin", "18293f7e141d5c9c99453add547d5a49134ee4b1199b8262fcbcc6c971b4d485")]
    [InlineData("cos", "a6b7a5f25172f7ae5b02cf07be89d6d2fd1867f0c6bdb13640fd57bfe7b26a78")]
    [InlineData("sqrt", "70cb64746e23de834a6477bf3f34b25e0aa415fe982c806edf012ed0592d17e3")]
    public void Matches_original_runtime(string op, string expectedHash)
    {
        var g = new Real48ProbeCases.SplitMix64(Seeds[op]);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int i = 0; i < Count; i++)
        {
            var a = Real48.FromBytes(Real48ProbeCases.GenerateReal(g));
            var (error, result) = Run(() => op switch
            {
                "sin" => Real48.Sin(a),
                "cos" => Real48.Cos(a),
                _ => Real48.Sqrt(a),
            });
            sha.AppendData([(byte)error, (byte)result.Length]);
            sha.AppendData(result);
        }

        Assert.Equal(expectedHash, Convert.ToHexStringLower(sha.GetHashAndReset()));
    }

    [Fact]
    public void Whole_degrees_match_original_rad_sin_cos()
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (int deg = -360; deg <= 720; deg++)
        {
            var rad = Real48.DegreesToRadians(Real48.FromInt32(deg));
            sha.AppendData(rad.ToBytes());
            var (es, s) = Run(() => Real48.Sin(rad));
            sha.AppendData([(byte)es]);
            sha.AppendData(s);
            var (ec, c) = Run(() => Real48.Cos(rad));
            sha.AppendData([(byte)ec]);
            sha.AppendData(c);
        }

        Assert.Equal("acdc1235b6ddd3dc6acd2674f4a3b7e168eaa96257730bd3bf3bfa477f6fff67", Convert.ToHexStringLower(sha.GetHashAndReset()));
    }

    [Theory]
    [InlineData(0, 7, 0)]
    [InlineData(30, 6, 4)]
    [InlineData(60, 3, 6)]
    [InlineData(90, 0, 7)]
    public void Barrel_values_from_feld_md(int degrees, int cos8, int sin8)
    {
        // docs/analyse/feld.md 3a: the runtime gives cos 0° · 8 -> 7, sin 30° · 8 -> 4, cos 60° · 8 -> 3, sin 90° · 8 -> 7.
        var rad = Real48.DegreesToRadians(Real48.FromInt32(degrees));
        var eight = Real48.FromInt32(8);
        Assert.Equal(cos8, (Real48.Cos(rad) * eight).Trunc());
        Assert.Equal(sin8, (Real48.Sin(rad) * eight).Trunc());
    }

    [Fact]
    public void Sqrt_of_negative_is_runtime_error_207()
    {
        var e = Assert.Throws<RunErrorException>(() => Real48.Sqrt(Real48.FromInt32(-4)));
        Assert.Equal(RunErrorException.InvalidFloatingPointOperation, e.Code);
    }

    private static (int Error, byte[] Result) Run(Func<Real48> f)
    {
        try
        {
            return (0, f().ToBytes());
        }
        catch (RunErrorException e)
        {
            return (e.Code, []);
        }
    }
}
