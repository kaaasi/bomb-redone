using BombRedone.Formats;
using BombRedone.Tools;

namespace BombRedone.UI.Tests.Tools;

/// <summary>Redirects the console; tests of this collection never run in parallel with each other.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCollection
{
    public const string Name = "Console";
}

/// <summary><c>bombtool cfg</c> with a synthetic BOMB.CFG (#34; no original data needed).</summary>
[Collection(ConsoleCollection.Name)]
public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bomb-cfg-" + Guid.NewGuid().ToString("N"));

    public ConfigCommandTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>
    /// 45 bytes in the layout of docs/formate/konfiguration.md: version tag, then the 27 fields (booleans 1 byte,
    /// numbers 2 bytes little endian). Distinct values per field so that a wrong offset shows.
    /// </summary>
    private static byte[] SyntheticConfig(ushort wall = 3)
    {
        var data = new byte[GameConfig.Size];
        GameConfig.VersionTag.CopyTo(data);
        void Word(int offset, ushort value) { data[offset] = (byte)value; data[offset + 1] = (byte)(value >> 8); }
        data[5] = 1;            // SoundEffects
        data[6] = 0;            // FlySound
        Word(7, 4);             // Wind: Gale
        Word(9, 42);            // CrumblePercent
        Word(11, wall);         // Wall
        data[13] = 1;           // InvisibleShot
        data[14] = 0;           // ShotTrail
        Word(15, 17);           // GamesToPlay
        data[17] = 1;           // TanksFall
        Word(18, 9);            // Background: Random
        Word(20, 0x1234);       // UserBackgroundMask
        data[22] = 1;           // ForceMouse
        data[23] = 0;           // MouseEnabled
        data[24] = 1;           // DirectVideo
        data[25] = 0;           // ExtendedKeyboard
        data[26] = 1;           // PaletteRotation
        data[27] = 0;           // CrudeVideo
        Word(28, 3);            // PaletteSpeed
        data[30] = 1;           // ComputersBuy
        Word(31, 11);           // TurretSpeed
        Word(33, 12);           // FallSpeed
        Word(35, 13);           // ExplosionSpeed
        Word(37, 14);           // ShotSpeed
        Word(39, 15);           // LaserSpeed
        data[41] = 0;           // PaletteUsesRetrace
        data[42] = 1;           // TeamPlay
        Word(43, 70);           // BossScreenMemoryKiB: 70 << 10 wraps in 16 bits to 6144
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
            return (ConfigCommand.Run(args), output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(oldOut);
            Console.SetError(oldError);
        }
    }

    private string Write(byte[] data)
    {
        var path = Path.Combine(_dir, "BOMB.CFG");
        File.WriteAllBytes(path, data);
        return path;
    }

    [Fact]
    public void PrintsEveryFieldWithOffsetNameAndValue()
    {
        var path = Write(SyntheticConfig());
        var (code, output, error) = Run(path);

        Assert.Equal(0, code);
        Assert.Equal("", error);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal($"BOMB.CFG {Path.GetFullPath(path)} (release 3.2, 45 bytes)", lines[0]);
        string[] expected =
        [
            "   5  SoundEffects                 yes",
            "   6  FlySound                     no",
            "   7  Wind                         4 (Gale)",
            "   9  CrumblePercent               42",
            "  11  Wall                         3 (Accelerate)",
            "  13  InvisibleShot                yes",
            "  14  ShotTrail                    no",
            "  15  GamesToPlay                  17",
            "  17  TanksFall                    yes",
            "  18  Background                   9 (Random)",
            "  20  UserBackgroundMask           0x1234",
            "  22  ForceMouse                   yes",
            "  23  MouseEnabled                 no",
            "  24  DirectVideo                  yes",
            "  25  ExtendedKeyboard             no",
            "  26  PaletteRotation              yes",
            "  27  CrudeVideo                   no",
            "  28  PaletteSpeed                 3",
            "  30  ComputersBuy                 yes",
            "  31  TurretSpeed                  11",
            "  33  FallSpeed                    12",
            "  35  ExplosionSpeed               13",
            "  37  ShotSpeed                    14",
            "  39  LaserSpeed                   15",
            "  41  PaletteUsesRetrace           no (PT, timer)",
            "  42  TeamPlay                     yes",
            "  43  BossScreenMemoryKiB          70 (used: 6144 bytes)",
        ];
        Assert.Equal(expected, lines[1..]);
    }

    [Fact]
    public void UnknownValuesAreMarked()
    {
        var (code, output, _) = Run(Write(SyntheticConfig(wall: 7)));
        Assert.Equal(0, code);
        Assert.Contains("  11  Wall                         7 (unknown)", output);
    }

    [Fact]
    public void InvalidFileGivesExitCodeOne()
    {
        var data = SyntheticConfig();
        data[1] = (byte)'2';
        var (code, output, error) = Run(Write(data));
        Assert.Equal(1, code);
        Assert.Equal("", output);
        Assert.NotEqual("", error);
    }

    [Fact]
    public void MissingFileGivesExitCodeOne()
    {
        var (code, _, error) = Run(Path.Combine(_dir, "missing.cfg"));
        Assert.Equal(1, code);
        Assert.NotEqual("", error);
    }

    [Theory]
    [InlineData]
    [InlineData("-h")]
    [InlineData("a", "b")]
    public void WrongArgumentsGiveUsageAndExitCodeTwo(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(2, code);
        Assert.Contains("Usage: bombtool cfg <BOMB.CFG>", error);
    }
}
