using System.Buffers.Binary;
using BombRedone.Formats;
using BombRedone.Tools;

namespace BombRedone.UI.Tests.Tools;

/// <summary><c>bombtool tws</c> with a synthetic saved game (no original data needed).</summary>
[Collection(ConsoleCollection.Name)]
public sealed class TwsCommandTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bomb-tws-" + Guid.NewGuid().ToString("N"));

    public TwsCommandTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Two players, player 2 a computer (Wind Master), layout of docs/formate/spielstand.md.</summary>
    private static byte[] Synthetic(bool teamPlay = false)
    {
        var data = new byte[SavedGame.SizeFor(2, 1)];
        SavedGame.Header.CopyTo(data);
        void Word(int offset, int value) => BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset), (short)value);
        Word(32, 2);            // PlayerCount
        Word(34, 3);            // CurrentGame
        Word(50, -92);          // WindValue
        Word(56, 5);            // Wind: Random
        data[70] = teamPlay ? (byte)1 : (byte)0;
        foreach (var (at, number, name) in new[] { (71, 1, "Anna"), (189, 2, "Wind Master") })
        {
            data[at] = (byte)number;
            data[at + 1] = (byte)name.Length;
            for (var k = 0; k < name.Length; k++)
                data[at + 2 + k] = (byte)name[k];
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at + 0x13), 5000 * number);
            Word(at + 0x66, number);
        }

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(71 + 0x17), 3000);
        Word(71 + 0x2E + 2, 100);  // Incinerator
        Word(71 + 0x2A, 5);        // Herb's Guide
        Word(187, 1);              // keyboard
        Word(305, 3);              // computer
        Word(307, 7);              // intelligence kind 7
        Word(310, 4);              // choice
        Word(312, 2);              // player
        var team1 = 314 + 53;
        data[team1] = 1;
        data[team1 + 1] = (byte)'X';
        Word(team1 + 0x0F, 2);
        return data;
    }

    private (int Code, string Out, string Error) Run(params string[] args)
    {
        var (oldOut, oldError) = (Console.Out, Console.Error);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            return (TwsCommand.Run(args), output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
        }
    }

    private string Write(byte[] data)
    {
        var path = Path.Combine(_dir, "GAME1.TWS");
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public void PrintsFieldsWithOffsets()
    {
        var path = Write(Synthetic());
        var (code, output, error) = Run(path);

        Assert.Equal(0, code);
        Assert.Equal("", error);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal($"Saved game {Path.GetFullPath(path)} (release 3.2, 2 players, 1 computers, 897 bytes)", lines[0]);
        Assert.Contains("    34  CurrentGame                3", lines);
        Assert.Contains("    50  WindValue                  -92", lines);
        Assert.Contains("    56  Wind                       5 (Random)", lines);
        Assert.Contains("Player record 1 at offset 71", lines);
        Assert.Contains("    72  Name                       \"Anna\"", lines);
        Assert.Contains("    94  Spent                      3000 (money 2000)", lines);
        Assert.Contains("   113  Stock                      Herb's Guide 5, Incinerator 100", lines);
        Assert.Contains("   187  Control                    1 (keyboard)", lines);
        Assert.Contains("Player record 2 at offset 189", lines);
        Assert.Contains("   305  Control                    3 (computer)", lines);
        Assert.Contains("   307  Intelligence               kind 7 (Wind Master), +0Bh 0, choice 4, player 2", lines);
        Assert.Contains("Team table at offset 314 (583 bytes)", lines);
        Assert.Contains("  not used (team play off), 3 non-zero bytes kept raw", lines);
    }

    [Fact]
    public void ListsTeamsWithTeamPlay()
    {
        var (code, output, _) = Run(Write(Synthetic(teamPlay: true)));

        Assert.Equal(0, code);
        Assert.Contains("   367  Team 1                     \"X\", members 2, all computers no", output);
        Assert.Contains("   314  Team 0                     \"\", members 0, all computers no", output);
    }

    [Fact]
    public void InvalidFileGivesExitCodeOne()
    {
        var data = Synthetic();
        data[0] = (byte)'x';

        var (code, output, error) = Run(Write(data));

        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.Contains("header differs", error);
    }

    [Fact]
    public void WrongArgumentsGiveUsage()
    {
        var (code, _, error) = Run();

        Assert.Equal(2, code);
        Assert.Contains("Usage: bombtool tws", error);
    }
}
