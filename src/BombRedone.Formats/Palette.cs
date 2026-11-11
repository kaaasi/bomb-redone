namespace BombRedone.Formats;

/// <summary>How 6-bit VGA DAC values (0..63) are widened to 8 bits per channel.</summary>
public enum PaletteScaling
{
    /// <summary>
    /// Full range by bit replication: <c>(v &lt;&lt; 2) | (v &gt;&gt; 4)</c>; 63 becomes 255. It differs from rounding
    /// <c>v * 255 / 63</c> by at most 1 (checked in the tests). [APPROX] Believed to match DOSBox's VGA DAC widening.
    /// </summary>
    FullRange,

    /// <summary>Plain shift <c>v * 4</c>; 63 becomes 252 (white is not quite white).</summary>
    Times4,
}

/// <summary>
/// A 256-entry VGA palette with 6 bits per channel, as stored in the original files.
/// [ORIGINAL] VGA DAC registers have 6 bits per channel (AGENTS.md section 4, "Originaltreue" point 3).
/// </summary>
public sealed class Palette
{
    /// <summary>Number of entries of the 256-color mode.</summary>
    public const int Size = 256;

    /// <summary>Largest value of a 6-bit channel.</summary>
    public const int MaxChannelValue = 63;

    /// <summary>Size in bytes of the palette as stored: 256 entries of R, G, B.</summary>
    public const int ByteLength = Size * 3;

    /// <summary>
    /// [APPROX] Default widening of 6 to 8 bits: the original shows 6-bit DAC values on a VGA card, so any exact
    /// 8-bit value is the choice of the emulator. DOSBox uses <see cref="PaletteScaling.FullRange"/>; not yet
    /// confirmed against a DOSBox screenshot of this program (reference images pending).
    /// </summary>
    public const PaletteScaling DefaultScaling = PaletteScaling.FullRange;

    private readonly byte[] _values;

    private Palette(byte[] values) => _values = values;

    /// <summary>Reads 768 bytes (R, G, B per entry, each 0..63).</summary>
    /// <exception cref="FormatException">Wrong length or a channel value above 63.</exception>
    public static Palette FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length != ByteLength)
            throw new FormatException($"Palette needs {ByteLength} bytes, got {data.Length}.");
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] > MaxChannelValue)
                throw new FormatException(
                    $"Palette value {data[i]} at byte {i} (entry {i / 3}, channel {"RGB"[i % 3]}) exceeds {MaxChannelValue}.");
        }
        return new Palette(data.ToArray());
    }

    /// <summary>The 6-bit values as stored (768 bytes, copy).</summary>
    public byte[] ToBytes() => (byte[])_values.Clone();

    /// <summary>The 6-bit channel value (0..63) of an entry; channel 0 = red, 1 = green, 2 = blue.</summary>
    public byte GetChannel(int index, int channel)
    {
        if ((uint)index >= Size) throw new ArgumentOutOfRangeException(nameof(index));
        if ((uint)channel >= 3) throw new ArgumentOutOfRangeException(nameof(channel));
        return _values[index * 3 + channel];
    }

    /// <summary>Widens a 6-bit value to 8 bits.</summary>
    public static byte Widen(int value6, PaletteScaling scaling = DefaultScaling)
    {
        if ((uint)value6 > MaxChannelValue) throw new ArgumentOutOfRangeException(nameof(value6));
        return scaling switch
        {
            PaletteScaling.FullRange => (byte)((value6 << 2) | (value6 >> 4)),
            PaletteScaling.Times4 => (byte)(value6 * 4),
            _ => throw new ArgumentOutOfRangeException(nameof(scaling)),
        };
    }

    /// <summary>The entry as 8-bit red, green, blue.</summary>
    public (byte R, byte G, byte B) GetRgb(int index, PaletteScaling scaling = DefaultScaling) =>
        (Widen(GetChannel(index, 0), scaling), Widen(GetChannel(index, 1), scaling), Widen(GetChannel(index, 2), scaling));

    /// <summary>
    /// All entries as 0xAABBGGRR with full alpha, the layout of <c>Surface.Palette</c> in the UI layer
    /// (this assembly does not depend on it).
    /// </summary>
    public uint[] ToAbgr(PaletteScaling scaling = DefaultScaling)
    {
        var result = new uint[Size];
        for (var i = 0; i < Size; i++)
        {
            var (r, g, b) = GetRgb(i, scaling);
            result[i] = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
        }
        return result;
    }
}
