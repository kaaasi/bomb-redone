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
    public void Matches_original_runtime(string op, string expectedHash)
    {
        Assert.Equal(expectedHash, Real48ProbeCases.Hash(op, Count));
    }
}
