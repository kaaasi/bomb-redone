namespace BombRedone.Core.Tests.Numerics;

/// <summary>
/// Bit-exact comparison with the original runtime routines. The expected hashes come from
/// <c>python3 tools/reverse-engineering/real48.py hashes --count 20000</c>, which executes the routines of
/// BOMB.EXE (segment 12CE) in an 8086 emulator for the same generated operands (Real48ProbeCases).
/// A hash covers error code and result bytes of every case; on a mismatch, <c>real48.py cases</c> writes the
/// cases as TSV to analyse/real48/ for a case-by-case comparison.
/// </summary>
public sealed class Real48ProbeTests
{
    private const int Count = 20000;

    [Theory]
    [InlineData("add", "a6dec85b34cd82c7f4ab364a011774107b51e6544db49c70a60989e979c7498e")]
    [InlineData("sub", "e58389e413ce5589ca20d2e21c200af1426083f4b4089fb085645775c5df947e")]
    [InlineData("cmp", "0f06203c60f5ec88efcff3eb4d78835b3d7ad0536fba01fa99a5091a211bbf8c")]
    [InlineData("float", "8121a35fbfdfcd800cd968029a705217c2af58858ffec9690309883b7b50b403")]
    [InlineData("sqr", "cc8cce3944698302009b30f2c15171b9b846e7cd1fbeb0a6ca4a1cf47b72642d")]
    [InlineData("mul", "2bb47cb84d93faf6ad4ff061e11d42c3324a7f9d567a311bb51326c3a66b57bc")]
    [InlineData("div", "eacb70cb52d43f5987f9e3d3c875d13a71abc45e3d4ab1bc4f70449d81ef8cca")]
    [InlineData("trunc", "7d64dd0f3d90e47deade44d763029a7a464266637957c5b079693b2b6a00abc5")]
    [InlineData("round", "be5a50ea449adb19a8af0d430dc853769ca5a21c7e8d5c2a1f526088b01b4e7e")]
    public void Matches_original_runtime(string op, string expectedHash)
    {
        Assert.Equal(expectedHash, Real48ProbeCases.Hash(op, Count));
    }
}
