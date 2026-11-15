using System.Buffers.Binary;
using System.Text;

namespace BombRedone.Formats;

/// <summary>
/// A saved game <c>*.TWS</c> of release 3.2, read only (decision R1-D3 = A). Layout and evidence:
/// <c>docs/formate/spielstand.md</c> (findings T1–T7 of <c>tools/reverse-engineering/tws.py befunde</c>).
/// </summary>
/// <remarks>
/// [ORIGINAL] Untyped file with record size 1, written by O3:3B54 (BOMB.OVR 0x0C5E0) as a sequence of BlockWrite calls
/// straight from the global variables and read back by O3:3FE4 (BOMB.OVR 0x0CA70) with BlockRead in the same order and
/// sizes (T2b, T3b): header (30 bytes), 26 global values (41 bytes), per player 1..count the record of 0x74 bytes and the
/// control word, for computers the 7 bytes of the intelligence (T2c, BOMB.OVR 0x0C971), and the team table (583 bytes,
/// T7e). No checksum, no length field, numbers little endian.
/// <para>
/// Every part keeps its raw bytes; <see cref="ToBytes"/> joins them again, so a file round-trips byte for byte, including
/// fields without known meaning and stale memory the original wrote into unused parts (e.g. the team table without team
/// play). Typed properties exist only for fields whose meaning is documented; everything else stays raw.
/// </para>
/// </remarks>
public sealed class SavedGame
{
    /// <summary>Size of the header text. [ORIGINAL] T4, BOMB.EXE 0x16446 (version tag DS:0056).</summary>
    public const int HeaderSize = 30;

    /// <summary>Size of the block of global values after the header. [ORIGINAL] T2b (26 BlockWrite of 1 or 2 bytes).</summary>
    public const int GlobalsSize = SavedGameGlobals.Size;

    /// <summary>Highest player count. [ORIGINAL] O3:3FE4 clears 11 pointers DS:3BE8 (players 0..10, T3c, BOMB.OVR 0x0CEDC).</summary>
    public const int MaxPlayers = 10;

    /// <summary>
    /// Header compared by the loader: "Tank Wars Saved Game", CR LF, "V", the version tag "3.2" + NUL without its
    /// length byte, CR LF, ^Z. [ORIGINAL] T4 (BOMB.EXE 0x16446), compared in O3:3FE4 (T3d, BOMB.OVR 0x0CC5B).
    /// </summary>
    public static ReadOnlySpan<byte> Header => "Tank Wars Saved Game\r\nV3.2\0\r\n\x1a"u8;

    private readonly byte[] _header;
    private readonly byte[] _trailing;

    private SavedGame(byte[] header, SavedGameGlobals globals, IReadOnlyList<SavedGamePlayer> players,
        SavedGameTeamTable teams, byte[] trailing)
    {
        _header = header;
        Globals = globals;
        Players = players;
        Teams = teams;
        _trailing = trailing;
    }

    /// <summary>The 26 global values (offset 30).</summary>
    public SavedGameGlobals Globals { get; }

    /// <summary>Players 1..<see cref="SavedGameGlobals.PlayerCount"/> in the order of the file (records as stored, #71).</summary>
    public IReadOnlyList<SavedGamePlayer> Players { get; }

    /// <summary>Team table DS:4838 (always stored, also without team play).</summary>
    public SavedGameTeamTable Teams { get; }

    /// <summary>Bytes after the team table. The original does not read them ([ORIGINAL] T3b: fixed sequence of BlockRead).</summary>
    public ReadOnlyMemory<byte> TrailingBytes => _trailing;

    /// <summary>
    /// Length of a file with this many players and computer players:
    /// 30 + 41 + 118 per player + 7 per computer + 583. [ORIGINAL] T2b, T2c.
    /// </summary>
    public static int SizeFor(int players, int computers) =>
        HeaderSize + GlobalsSize + players * (SavedGamePlayerRecord.Size + SavedGamePlayer.ControlSize)
        + computers * SavedGameIntelligence.Size + SavedGameTeamTable.Size;

    /// <summary>Reads a saved game file.</summary>
    public static SavedGame Load(string path) => Read(File.ReadAllBytes(path));

    /// <summary>Reads the bytes of a release 3.2 saved game.</summary>
    /// <exception cref="SavedGameException">Header differs, player count outside 1..10, or the file ends early.</exception>
    public static SavedGame Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize || !data[..HeaderSize].SequenceEqual(Header))
        {
            // [ORIGINAL] T3d (BOMB.OVR 0x0CC5B): Pascal string compare of the 30 bytes; on mismatch the original shows
            // "Save File is not valid!", waits for a key and halts. Nothing else of the file is checked.
            throw new SavedGameException(SavedGameError.InvalidHeader, "Not a Tank Wars 3.2 saved game: header differs.");
        }

        var pos = HeaderSize;
        var globals = new SavedGameGlobals(Take(data, ref pos, GlobalsSize, "global values"));
        var count = globals.PlayerCount;
        if (count is < 1 or > MaxPlayers)
        {
            // [APPROX] The original loop runs i = 1..DS:47DA without a check (T2c); more than 10 players would read
            // past the record table, 0 or less would read no record. Neither can be saved by the game, so the reader
            // rejects them.
            throw new SavedGameException(SavedGameError.PlayerCountOutOfRange,
                $"Player count {count} at offset 32 is outside 1..{MaxPlayers}.");
        }

        var players = new SavedGamePlayer[count];
        for (var i = 0; i < count; i++)
        {
            var record = new SavedGamePlayerRecord(Take(data, ref pos, SavedGamePlayerRecord.Size, $"record of player {i + 1}"));
            var control = Take(data, ref pos, SavedGamePlayer.ControlSize, $"control of player {i + 1}");
            SavedGameIntelligence? intelligence = null;
            // [ORIGINAL] T2c/T3b: the 7 bytes follow only if the control word is 3 (computer).
            if (BinaryPrimitives.ReadInt16LittleEndian(control) == SavedGamePlayer.ComputerControl)
                intelligence = new SavedGameIntelligence(Take(data, ref pos, SavedGameIntelligence.Size, $"intelligence of player {i + 1}"));
            players[i] = new SavedGamePlayer(i + 1, record, control, intelligence);
        }

        var teams = new SavedGameTeamTable(Take(data, ref pos, SavedGameTeamTable.Size, "team table"));
        return new SavedGame(data[..HeaderSize].ToArray(), globals, players, teams, data[pos..].ToArray());
    }

    /// <summary>The file again, byte for byte: all parts in file order plus <see cref="TrailingBytes"/>.</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        stream.Write(_header);
        stream.Write(Globals.Raw.Span);
        foreach (var player in Players)
        {
            stream.Write(player.Record.Raw.Span);
            stream.Write(player.RawControl.Span);
            if (player.Intelligence is { } intelligence)
                stream.Write(intelligence.Raw.Span);
        }

        stream.Write(Teams.Raw.Span);
        stream.Write(_trailing);
        return stream.ToArray();
    }

    private static byte[] Take(ReadOnlySpan<byte> data, ref int pos, int size, string what)
    {
        if (pos + size > data.Length)
        {
            // [APPROX] The original reads on without a check ($I-, BlockRead without result variable: only InOutRes is
            // set, T1c/T2d) and keeps the old values of the unread variables. A reader has no old values, so it rejects
            // the file like GameConfig does for a short BOMB.CFG.
            throw new SavedGameException(SavedGameError.TooShort,
                $"Saved game ends at offset {data.Length} inside the {what} (needs {pos + size} bytes).");
        }

        var part = data.Slice(pos, size).ToArray();
        pos += size;
        return part;
    }

    // Shared helpers of the parts.
    internal static short Int16(byte[] raw, int offset) => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(offset));

    internal static ushort UInt16(byte[] raw, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(offset));

    internal static int Int32(byte[] raw, int offset) => BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(offset));

    // [APPROX] Turbo Pascal Boolean; how BOMB.EXE treats values other than 0 and 1 is not traced (as in GameConfig).
    internal static bool Flag(byte[] raw, int offset) => raw[offset] != 0;

    /// <summary>
    /// Turbo Pascal String[n] at <paramref name="offset"/>: length byte, then up to <paramref name="capacity"/> characters.
    /// </summary>
    /// <remarks>
    /// [APPROX] A length byte above the capacity cannot be written by the game; the reader cuts it to the capacity.
    /// Characters are decoded one byte per character (Latin-1); the names the game accepts are ASCII 32..125
    /// (spielerauswahl.md §4, §9), the code page of other bytes is not traced.
    /// </remarks>
    internal static string PascalString(byte[] raw, int offset, int capacity) =>
        Encoding.Latin1.GetString(raw, offset + 1, Math.Min((int)raw[offset], capacity));
}
