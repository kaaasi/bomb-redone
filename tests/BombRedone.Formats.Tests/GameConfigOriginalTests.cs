using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Formats.Tests;

/// <summary>Tests of the BOMB.CFG reader against the original programs and the example file.</summary>
public class GameConfigOriginalTests
{
    /// <summary>Example written by BOMBCFG.EXE with its defaults (reference repository, never in this repository).</summary>
    private const string ExampleName = "nutzer-2026-11-11.CFG";

    /// <summary>
    /// Finds the example file: <c>BOMB_SPIELSTAENDE</c>, the folder <c>spielstaende</c> next to the (resolved)
    /// <c>gamedata</c> folder, or <c>~/referenz/original/spielstaende</c>. Fails if none exists, because the
    /// test only runs when original data are present.
    /// </summary>
    private static string ExamplePath()
    {
        var gamedata = new DirectoryInfo(TestGameData.Load().RootDirectory);
        var resolved = gamedata.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo ?? gamedata;
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("BOMB_SPIELSTAENDE"),
            resolved.Parent is { } parent ? Path.Combine(parent.FullName, "spielstaende") : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "referenz", "original", "spielstaende"),
        };
        foreach (var dir in candidates.Where(d => !string.IsNullOrEmpty(d)))
        {
            var path = Path.Combine(dir!, ExampleName);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"{ExampleName} not found (BOMB_SPIELSTAENDE, spielstaende next to gamedata, ~/referenz/original/spielstaende). " +
            "Load it with tools/claude/fetch-reference.sh original.");
    }

    [GameDataFact]
    public void VersionTagMatchesBothPrograms()
    {
        var data = TestGameData.Load();

        // [ORIGINAL] BOMB.EXE 0x16446 and BOMBCFG.EXE 0x19EEE: string[4] constant compared with offset 0 of the file.
        Assert.Equal(GameConfig.VersionTag.ToArray(), File.ReadAllBytes(data.Require("BOMB.EXE")).AsSpan(0x16446, 5).ToArray());
        Assert.Equal(GameConfig.VersionTag.ToArray(), File.ReadAllBytes(data.Require("BOMBCFG.EXE")).AsSpan(0x19EEE, 5).ToArray());
    }

    [GameDataFact]
    public void ExampleHasSizeAndVersionOfRelease32()
    {
        var bytes = File.ReadAllBytes(ExamplePath());

        Assert.Equal(GameConfig.Size, bytes.Length);
        Assert.Equal(GameConfig.VersionTag.ToArray(), bytes[..5]);
    }

    /// <summary>
    /// The example holds the defaults of BOMBCFG.EXE. Where BOMB.DOC names a default, it is compared with the
    /// manual; two values differ from the manual and match the defaults routine of BOMBCFG.EXE
    /// ([ORIGINAL] BOMBCFG.EXE 0x7008, segment offset 0x52E8: Crumble "75", Wall 0).
    /// </summary>
    [GameDataFact]
    public void ExampleMatchesDocumentedDefaults()
    {
        var config = GameConfig.Load(ExamplePath());

        // Defaults named in BOMB.DOC, sections "SOUND OPTIONS" and "GAME OPTIONS".
        Assert.True(config.SoundEffects);              // Sound Effects: Default Yes
        Assert.False(config.FlySound);                 // Fly Sound Effects: Default No
        Assert.False(config.InvisibleShot);            // Shot Visible: Default Yes
        Assert.True(config.ShotTrail);                 // Trail Visible: Default Yes
        Assert.Equal(BackgroundKind.Random, config.BackgroundKind); // Background: Default Random
        Assert.Equal(10, config.GamesToPlay);          // Games: Default 10
        Assert.True(config.TanksFall);                 // Tanks Fall: Default Yes
        Assert.True(config.ComputersBuy);              // Computers Can Buy: Default Yes
        Assert.Equal(0, config.PaletteSpeed);          // Palette Speed: Default 0
        Assert.Equal(WindSetting.Normal, config.Wind); // Wind Force: Default Normal
        Assert.False(config.TeamPlay);                 // Team Play: Default Off

        // Differ from BOMB.DOC ("Crumbling Hills: Default 50%", "Walls: Default Random"), match BOMBCFG.EXE.
        Assert.Equal(75, config.CrumblePercent);
        Assert.Equal(WallSetting.None, config.Wall);
    }

    /// <summary>Fields without a default in BOMB.DOC, compared with the defaults routine of BOMBCFG.EXE (0x7008).</summary>
    [GameDataFact]
    public void ExampleMatchesBombCfgDefaultsForUndocumentedFields()
    {
        var config = GameConfig.Load(ExamplePath());

        Assert.Equal(0xFFFF, config.UserBackgroundMask);
        Assert.Equal(9, config.Background);
        Assert.Equal(8, config.TurretSpeed);
        Assert.Equal(5, config.FallSpeed);
        Assert.Equal(8, config.ExplosionSpeed);
        Assert.Equal(10, config.ShotSpeed);
        Assert.Equal(5, config.LaserSpeed);
        Assert.Equal(63, config.BossScreenMemoryKiB);
        Assert.Equal(64000, config.BossScreenMemoryBytes);
    }
}
