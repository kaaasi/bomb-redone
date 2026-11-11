using BombRedone.Formats;

namespace BombRedone.UI.Screens;

/// <summary>
/// The two title files as the title sequence reads them: each one record of 64,768 bytes (320 x 200 pixels plus a
/// 768-byte palette), or null if the file does not exist.
/// </summary>
/// <remarks>
/// [ORIGINAL] BOMB.OVR 0x0001C (O1:0014): existence is tested with FindFirst (attribute 0) for "bomb1.raw"
/// (BOMB.OVR 0x00008) and "bomb2.raw" (BOMB.OVR 0x00012) at 0x001FF / 0x00229; an existing file is read as one record of
/// 0xFD00 bytes (Reset with record size 0xFD00, one Read) at 0x00254 / 0x002B2. Bytes after the first record are never
/// read. DOS file names ignore case, hence <see cref="GameData.Find"/>.
/// </remarks>
public sealed class TitleImages
{
    /// <summary>Size of the one record read from each file.</summary>
    public const int RecordLength = RawImage.FileLength;

    /// <summary>[ORIGINAL] BOMB.OVR 0x00008: file name of the base image.</summary>
    public const string FirstFileName = "BOMB1.RAW";

    /// <summary>[ORIGINAL] BOMB.OVR 0x00012: file name of the overlay image.</summary>
    public const string SecondFileName = "BOMB2.RAW";

    public TitleImages(byte[]? first, byte[]? second)
    {
        First = first is null ? null : ToRecord(first);
        Second = second is null ? null : ToRecord(second);
    }

    /// <summary>Contents of BOMB1.RAW (exactly <see cref="RecordLength"/> bytes) or null if missing.</summary>
    public byte[]? First { get; }

    /// <summary>Contents of BOMB2.RAW (exactly <see cref="RecordLength"/> bytes) or null if missing.</summary>
    public byte[]? Second { get; }

    /// <summary>No title files at all (for example without original data): the title sequence is skipped.</summary>
    public static TitleImages None { get; } = new(null, null);

    /// <summary>Reads both files from the original data folder.</summary>
    public static TitleImages Load(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new TitleImages(Read(data.Find(FirstFileName)), Read(data.Find(SecondFileName)));
    }

    private static byte[]? Read(string? path)
    {
        if (path is null)
            return null;
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // [APPROX] The original reads with I/O checks off ({$I-}, no IOResult call in O1), so a failed read leaves the
            // heap buffer as it was; that content is not reproducible. Treated like an empty file.
            return [];
        }
    }

    /// <summary>
    /// Cuts or pads the file contents to one record. Longer files: the rest is never read [ORIGINAL, see class remarks].
    /// [APPROX] Shorter files: the original fills only the bytes DOS delivers; the rest of the uninitialised heap block is not
    /// reproducible and is taken as 0.
    /// </summary>
    private static byte[] ToRecord(byte[] contents)
    {
        var record = new byte[RecordLength];
        contents.AsSpan(0, Math.Min(contents.Length, RecordLength)).CopyTo(record);
        return record;
    }
}
