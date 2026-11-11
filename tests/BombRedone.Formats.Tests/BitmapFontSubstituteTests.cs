using System.Security.Cryptography;
using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

public class BitmapFontSubstituteTests
{
    [Fact]
    public void TableMatchesItsChecksum() =>
        Assert.Equal(BitmapFontSubstitute.Sha256, Convert.ToHexStringLower(SHA256.HashData(BitmapFontSubstitute.Rows)));

    [Fact]
    public void HoldsTheFirst128Characters() => Assert.Equal(128, BitmapFontSubstitute.Create().CharCount);

    [Fact]
    public void CapitalAIsBitReversedFromTheSource()
    {
        // font8x8_basic.h, U+0041: { 0x0C, 0x1E, 0x33, 0x33, 0x3F, 0x33, 0x33, 0x00 } with bit 0 leftmost.
        byte[] expected = [0x30, 0x78, 0xCC, 0xCC, 0xFC, 0xCC, 0xCC, 0x00];
        Assert.Equal(expected, BitmapFontSubstitute.Create().GetRows((byte)'A').ToArray());
    }

    [Fact]
    public void ControlCharactersAndSpaceAreEmpty()
    {
        var font = BitmapFontSubstitute.Create();
        for (var code = 0; code <= 32; code++)
            Assert.All(font.GetRows((byte)code).ToArray(), row => Assert.Equal(0, row));
    }

    [Fact]
    public void CodesAbove127AreEmptyCells() =>
        Assert.All(BitmapFontSubstitute.Create().GetRows(200).ToArray(), row => Assert.Equal(0, row));
}
