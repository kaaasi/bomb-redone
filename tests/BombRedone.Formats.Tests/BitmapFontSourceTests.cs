using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

public sealed class BitmapFontSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("bitmapfont-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_root, name)).FullName;

    [Fact]
    public void WithoutReferenceTheSubstituteIsUsed()
    {
        var selection = BitmapFontSource.Load([Folder("empty"), Path.Combine(_root, "does-not-exist")]);
        Assert.Equal(BitmapFontOrigin.Substitute, selection.Origin);
        Assert.Null(selection.Path);
        Assert.Equal(BitmapFontSubstitute.Rows.ToArray(), Enumerable.Range(0, 128)
            .SelectMany(c => selection.Font.GetRows((byte)c).ToArray()).ToArray());
    }

    [Fact]
    public void FirstFolderWithTheFileWins()
    {
        var first = Folder("a");
        var second = Folder("b");
        File.WriteAllBytes(Path.Combine(second, BitmapFontSource.ReferenceFileName), new byte[2048]);
        Assert.Equal(Path.Combine(second, BitmapFontSource.ReferenceFileName),
            BitmapFontSource.FindReference([first, second]));
        File.WriteAllBytes(Path.Combine(first, BitmapFontSource.ReferenceFileName), new byte[2048]);
        Assert.Equal(Path.Combine(first, BitmapFontSource.ReferenceFileName),
            BitmapFontSource.FindReference([first, second]));
    }

    [Fact]
    public void AWrongFileIsAnErrorNotTheSubstitute()
    {
        var folder = Folder("wrong");
        File.WriteAllBytes(Path.Combine(folder, BitmapFontSource.ReferenceFileName), new byte[2048]);
        var error = Assert.Throws<FormatException>(() => BitmapFontSource.Load([folder]));
        Assert.Contains(BitmapFontSource.ReferenceSha256, error.Message);
    }

    [Fact]
    public void DefaultSearchStartsInTheProtectedFolderOfTheCurrentDirectory() =>
        Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "referenz", "schriften"),
            BitmapFontSource.DefaultSearchDirectories()[0]);
}
