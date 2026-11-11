namespace BombRedone.Desktop;

/// <summary>
/// Random generator of the Turbo Pascal 6.0 runtime linked into BOMB.EXE (docs/analyse/re-grundlagen.md section 3).
/// [ORIGINAL] BOMB.EXE 0x15A44 (12CE:16C4): RandSeed := RandSeed * 08088405h + 1; Random(n) (12CE:1675) returns the
/// high word of the new seed mod n, 0 for n = 0.
/// </summary>
internal sealed class TurboPascalRandom(uint seed)
{
    private uint _seed = seed;

    /// <summary>
    /// Seed like Randomize (12CE:16FC) from the DOS time (INT 21h AH=2Ch: CX hours and minutes, DX seconds and
    /// hundredths). [APPROX] Which word becomes the high word of RandSeed is not checked; any seed is valid here.
    /// </summary>
    public static TurboPascalRandom FromClock(DateTime now)
    {
        var cx = (uint)((now.Hour << 8) | now.Minute);
        var dx = (uint)((now.Second << 8) | (now.Millisecond / 10));
        return new TurboPascalRandom((cx << 16) | dx);
    }

    public int Next(int n)
    {
        _seed = unchecked(_seed * 0x08088405u + 1);
        var high = (ushort)(_seed >> 16);
        return n == 0 ? 0 : high % (ushort)n;
    }
}
