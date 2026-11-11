using BombRedone.Formats;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

public sealed class TitleImagesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bomb-title-" + Guid.NewGuid().ToString("N"));

    public TitleImagesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void MissingFilesAreNull()
    {
        var images = TitleImages.Load(new GameData(_dir));
        Assert.Null(images.First);
        Assert.Null(images.Second);
    }

    [Fact]
    public void NamesIgnoreCase()
    {
        File.WriteAllBytes(Path.Combine(_dir, "bomb1.raw"), new byte[TitleImages.RecordLength]);
        File.WriteAllBytes(Path.Combine(_dir, "Bomb2.Raw"), new byte[TitleImages.RecordLength]);
        var images = TitleImages.Load(new GameData(_dir));
        Assert.NotNull(images.First);
        Assert.NotNull(images.Second);
    }

    [Fact]
    public void ADirectoryWithTheNameDoesNotCount()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "BOMB2.RAW"));
        Assert.Null(TitleImages.Load(new GameData(_dir)).Second);
    }

    [Fact]
    public void LongerFilesAreCutToOneRecord()
    {
        var data = new byte[TitleImages.RecordLength + 10];
        data[0] = 5;
        data[TitleImages.RecordLength - 1] = 6;
        data[TitleImages.RecordLength] = 7;
        File.WriteAllBytes(Path.Combine(_dir, "BOMB2.RAW"), data);
        var second = TitleImages.Load(new GameData(_dir)).Second!;
        Assert.Equal(TitleImages.RecordLength, second.Length);
        Assert.Equal(5, second[0]);
        Assert.Equal(6, second[^1]);
    }

    /// <summary>
    /// #48: a file that exists but cannot be read counts as empty (zero record, not missing), and BOMB2.RAW is still
    /// read, as the original reads both files with I/O checks off. The lock (FileShare.None) makes the read fail.
    /// </summary>
    [Fact]
    public void UnreadableFileIsAZeroRecordAndTheOtherIsStillRead()
    {
        var first = Path.Combine(_dir, "BOMB1.RAW");
        File.WriteAllBytes(first, [9, 9, 9]);
        File.WriteAllBytes(Path.Combine(_dir, "BOMB2.RAW"), [4, 5]);
        TitleImages images;
        using (new FileStream(first, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            images = TitleImages.Load(new GameData(_dir));
        Assert.NotNull(images.First);
        Assert.Equal(TitleImages.RecordLength, images.First.Length);
        Assert.All(images.First, b => Assert.Equal(0, b));
        Assert.Equal([4, 5, 0], images.Second![..3]);
    }

    [Fact]
    public void ShorterFilesArePaddedWithZero()
    {
        File.WriteAllBytes(Path.Combine(_dir, "BOMB1.RAW"), [9, 9, 9]);
        File.WriteAllBytes(Path.Combine(_dir, "BOMB2.RAW"), []);
        var images = TitleImages.Load(new GameData(_dir));
        Assert.Equal(TitleImages.RecordLength, images.First!.Length);
        Assert.Equal([9, 9, 9, 0], images.First[..4]);
        Assert.All(images.Second!, b => Assert.Equal(0, b));
    }

    [Fact]
    public void ConstructorCopiesTheData()
    {
        var data = new byte[TitleImages.RecordLength];
        var images = new TitleImages(data, null);
        data[0] = 1;
        Assert.Equal(0, images.First![0]);
    }
}
