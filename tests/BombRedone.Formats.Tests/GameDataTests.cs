using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Formats.Tests;

public class GameDataTests
{
    [Fact]
    public void PackageListHasThirteenDistinctFiles()
    {
        Assert.Equal(13, GameData.PackageFiles.Count);
        Assert.Equal(13, GameData.PackageFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void FindIgnoresCaseAndReportsMissingFiles()
    {
        var dir = Directory.CreateTempSubdirectory("bomb-test-");
        try
        {
            // Synthetic stand-ins, not original content.
            File.WriteAllBytes(Path.Combine(dir.FullName, "bomb.exe"), [0]);
            File.WriteAllBytes(Path.Combine(dir.FullName, "Bomb1.Raw"), [0]);
            var data = new GameData(dir.FullName);

            Assert.NotNull(data.Find("BOMB.EXE"));
            Assert.NotNull(data.Find("BOMB1.RAW"));
            Assert.Null(data.Find("BOMB2.RAW"));
            Assert.Throws<FileNotFoundException>(() => data.Require("BOMB.OVR"));
            Assert.Equal(11, data.MissingPackageFiles().Count);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void MissingFolderIsReported()
    {
        var missing = Path.Combine(Path.GetTempPath(), "bomb-test-missing-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<DirectoryNotFoundException>(() => new GameData(missing));
    }

    [GameDataFact]
    public void OriginalPackageIsComplete()
    {
        var data = TestGameData.Load();
        Assert.Empty(data.MissingPackageFiles());
    }

    // Sizes from the package of release 3.2 (reference repository, original/README.md).
    [GameDataTheory]
    [InlineData("BOMB.EXE", 106192)]
    [InlineData("BOMB.OVR", 127951)]
    [InlineData("BOMB1.RAW", 64768)]
    [InlineData("BOMB2.RAW", 64768)]
    [InlineData("BOMBCFG.EXE", 109952)]
    public void OriginalFilesHaveTheSizesOfRelease32(string name, long size)
    {
        var data = TestGameData.Load();
        Assert.Equal(size, new FileInfo(data.Require(name)).Length);
    }
}
