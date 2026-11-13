namespace BombRedone.Core.Graphics;

/// <summary>
/// The 256 DAC entries of mode 13h with 6 bits per channel, plus which entries were written since the last
/// <see cref="ClearWritten"/>. Entries never written keep what earlier screens left there; the playfield only
/// defines the entries it sets (docs/analyse/feld.md section 2).
/// </summary>
public sealed class VgaPalette
{
    public const int Entries = 256;

    private readonly byte[] _rgb = new byte[Entries * 3];
    private readonly bool[] _written = new bool[Entries];

    /// <summary>R, G, B of every entry (6 bits each), entry i at 3 i.</summary>
    public ReadOnlySpan<byte> Rgb => _rgb;

    public (byte R, byte G, byte B) this[int index] => (_rgb[3 * index], _rgb[3 * index + 1], _rgb[3 * index + 2]);

    public bool IsWritten(int index) => _written[index];

    /// <summary>
    /// SetRGB as the game calls it through DS:E8F6 (SetRGB(index, r, g, b)). [ORIGINAL] The DAC keeps the low
    /// 6 bits of each channel (VGA, AGENTS.md section 4 "Farben").
    /// </summary>
    public void Set(int index, int r, int g, int b)
    {
        _rgb[3 * index] = (byte)(r & 0x3F);
        _rgb[3 * index + 1] = (byte)(g & 0x3F);
        _rgb[3 * index + 2] = (byte)(b & 0x3F);
        _written[index] = true;
    }

    public void ClearWritten() => Array.Clear(_written);

    /// <summary>Written entries in ascending order as index, R, G, B (the "dac_set" dump of the probe).</summary>
    public byte[] WrittenEntries()
    {
        var result = new List<byte>();
        for (var i = 0; i < Entries; i++)
        {
            if (!_written[i])
                continue;
            result.Add((byte)i);
            result.Add(_rgb[3 * i]);
            result.Add(_rgb[3 * i + 1]);
            result.Add(_rgb[3 * i + 2]);
        }

        return [.. result];
    }
}
