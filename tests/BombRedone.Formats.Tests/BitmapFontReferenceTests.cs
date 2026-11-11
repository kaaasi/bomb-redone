using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

/// <summary>Tests against the reference font of DOSBox 0.74-3 (R1-schrift-1 = D); skipped without it.</summary>
public class BitmapFontReferenceTests
{
    [ReferenceFontFact]
    public void ReferenceFontHas256CharactersAndTheExpectedChecksum() =>
        Assert.Equal(256, BitmapFontSource.ReadReference(TestReferenceFont.Path).CharCount);

    [ReferenceFontFact]
    public void DefaultLoadPrefersTheReference()
    {
        var selection = BitmapFontSource.Load();
        Assert.Equal(BitmapFontOrigin.Reference, selection.Origin);
        Assert.Equal(TestReferenceFont.Path, selection.Path);
    }

    [ReferenceFontFact]
    public void SubstituteDiffersFromTheReferenceOnlyInExclamationAndQuote()
    {
        // Documents how close the substitute comes (docs/formate/schriften.md, section 4).
        var reference = BitmapFontSource.ReadReference(TestReferenceFont.Path);
        var substitute = BitmapFontSubstitute.Create();
        var differing = Enumerable.Range(32, 95)
            .Where(c => !reference.GetRows((byte)c).SequenceEqual(substitute.GetRows((byte)c)))
            .Select(c => (char)c);
        Assert.Equal("!\"", string.Concat(differing));
    }

    [ReferenceFontFact]
    public void BoxDrawingCharacterOfTheUpperHalfIsPresent()
    {
        // Code 0xDB (full block) comes from the second half, reached in the original through INT 1Fh.
        var reference = BitmapFontSource.ReadReference(TestReferenceFont.Path);
        Assert.All(reference.GetRows(0xDB).ToArray(), row => Assert.Equal(0xFF, row));
    }
}
