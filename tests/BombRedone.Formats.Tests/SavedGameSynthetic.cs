using System.Buffers.Binary;
using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

/// <summary>
/// Synthetic saved games without original data: the same bytes as <c>synthetic()</c> in
/// <c>tools/reverse-engineering/tws.py</c> (checked by <see cref="SavedGameTests.BuilderMatchesTwsPy"/>).
/// </summary>
internal static class SavedGameSynthetic
{
    /// <summary>
    /// SHA-256 of <c>tws.synthetic(players, computers)</c> for the cases of <c>tws.py selbsttest</c>. Regenerate with
    /// <c>python3 -I -c "import sys,hashlib; sys.path.insert(0,'tools/reverse-engineering'); import tws;
    /// print(hashlib.sha256(tws.synthetic(3,{2})).hexdigest())"</c>.
    /// </summary>
    public static readonly (int Players, int[] Computers, string Sha256)[] TwsPyCases =
    [
        (2, [], "86a3f3325425f322329a92b17d0631571b535fe5203bc7cdd8b9fbc9f4eaecdf"),
        (3, [2], "b673ef5c5462f3164e2c6927ed23a2a4f707377a3234e2a46f6048f4504957e6"),
        (10, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10], "7df963cefca070a81195206cb3bd6bcada60a4d2865c7cb68a4f8a4c1cd9570e"),
    ];

    /// <summary>File offset of the first player record.</summary>
    public const int FirstRecord = SavedGame.HeaderSize + SavedGame.GlobalsSize;

    /// <summary>
    /// Like <c>tws.synthetic</c>: all globals 0 except the player count; record i has number i, name "Pi", score
    /// 1000·i; control 3 for computers (intelligence kind 2, byte 0, choice 1, player i), else 1; team table zero.
    /// </summary>
    public static byte[] Build(int players, params int[] computers)
    {
        using var stream = new MemoryStream();
        stream.Write(SavedGame.Header);
        var globals = new byte[SavedGame.GlobalsSize];
        BinaryPrimitives.WriteInt16LittleEndian(globals.AsSpan(2), (short)players);
        stream.Write(globals);
        for (var i = 1; i <= players; i++)
        {
            var record = new byte[SavedGamePlayerRecord.Size];
            record[0] = (byte)i;
            var name = $"P{i}";
            record[1] = (byte)name.Length;
            for (var k = 0; k < name.Length; k++)
                record[2 + k] = (byte)name[k];
            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(0x13), 1000 * i);
            stream.Write(record);
            var computer = computers.Contains(i);
            stream.Write(Int16(computer ? 3 : 1));
            if (computer)
            {
                stream.Write(Int16(2));
                stream.WriteByte(0);
                stream.Write(Int16(1));
                stream.Write(Int16(i));
            }
        }

        stream.Write(new byte[SavedGameTeamTable.Size]);
        return stream.ToArray();
    }

    /// <summary>File offset of record field <paramref name="field"/> of player <paramref name="index"/> (1-based).</summary>
    /// <param name="computersBefore">Computer players with a smaller index (each adds 7 bytes).</param>
    public static int RecordOffset(int index, int field, int computersBefore = 0) =>
        FirstRecord + (index - 1) * (SavedGamePlayerRecord.Size + SavedGamePlayer.ControlSize)
        + computersBefore * SavedGameIntelligence.Size + field;

    public static void WriteInt16(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), (short)value);

    public static void WriteInt32(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);

    private static byte[] Int16(int value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, (short)value);
        return bytes;
    }
}
