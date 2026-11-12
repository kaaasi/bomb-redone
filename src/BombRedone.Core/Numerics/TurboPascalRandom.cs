namespace BombRedone.Core.Numerics;

/// <summary>
/// Random generator of the Turbo Pascal 6.0 runtime linked into BOMB.EXE (docs/analyse/real48.md "Zufall").
/// [ORIGINAL] BOMB.EXE 0x15A44 (12CE:16C4): RandSeed (DS:3AD6) := RandSeed * 08088405h + 1. The seed is an
/// input of the game core (AGENTS.md section 4): pass a fixed one for reproducible runs, or
/// <see cref="FromClock"/> with the time the caller chooses.
/// </summary>
public sealed class TurboPascalRandom(uint seed)
{
    private uint _seed = seed;

    /// <summary>Current RandSeed (DS:3AD6).</summary>
    public uint Seed => _seed;

    /// <summary>
    /// Seed like Randomize. [ORIGINAL] BOMB.EXE 0x15A7C (12CE:16FC): INT 21h AH=2Ch, then CX (hours, minutes)
    /// to the low word DS:3AD6 and DX (seconds, hundredths) to the high word DS:3AD8.
    /// [APPROX] Hundredths as milliseconds / 10; DOS derives them from the 18.2 Hz timer tick.
    /// </summary>
    public static TurboPascalRandom FromClock(DateTime now)
    {
        var cx = (uint)((now.Hour << 8) | now.Minute);
        var dx = (uint)((now.Second << 8) | (now.Millisecond / 10));
        return new TurboPascalRandom((dx << 16) | cx);
    }

    /// <summary>
    /// Random(n) for a Word range. [ORIGINAL] BOMB.EXE 0x159F5 (12CE:1675): high word of the new seed mod n,
    /// 0 for n = 0.
    /// </summary>
    public int Next(int n)
    {
        Step();
        var high = (ushort)(_seed >> 16);
        return n == 0 ? 0 : high % (ushort)n;
    }

    /// <summary>
    /// Random as Real in [0, 1). [ORIGINAL] BOMB.EXE 0x15A0B (12CE:168B): the new seed as unsigned 32-bit
    /// mantissa with exponent 80h, normalised; exactly RandSeed / 2^32 (0 for seed 0).
    /// </summary>
    public Real48 NextReal()
    {
        Step();
        uint mantissa = _seed;
        if (mantissa == 0)
        {
            return Real48.Zero;
        }

        int exponent = 0x80;
        while ((mantissa & 0x8000_0000) == 0)
        {
            mantissa <<= 1;
            exponent--;
        }

        Span<byte> bytes = stackalloc byte[Real48.Size];
        bytes[0] = (byte)exponent;
        bytes[1] = 0;
        bytes[2] = (byte)mantissa;
        bytes[3] = (byte)(mantissa >> 8);
        bytes[4] = (byte)(mantissa >> 16);
        bytes[5] = (byte)((mantissa >> 24) & 0x7F);
        return Real48.FromBytes(bytes);
    }

    private void Step() => _seed = unchecked(_seed * 0x08088405u + 1);
}
