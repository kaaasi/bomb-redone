using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

public class BitmapFontTests
{
    private static byte[] Rows(int chars)
    {
        var rows = new byte[chars * 8];
        rows[65 * 8 + 0] = 0b1000_0001; // 'A', top row: leftmost and rightmost pixel
        rows[65 * 8 + 7] = 0b0001_0000; // 'A', bottom row: fourth pixel
        return rows;
    }

    [Theory]
    [InlineData(128)]
    [InlineData(256)]
    public void AcceptsHalfAndFullCharacterSets(int chars) =>
        Assert.Equal(chars, new BitmapFont(Rows(chars)).CharCount);

    [Theory]
    [InlineData(0)]
    [InlineData(1023)]
    [InlineData(2049)]
    public void RejectsOtherSizes(int length) => Assert.Throws<FormatException>(() => new BitmapFont(new byte[length]));

    [Fact]
    public void BitSevenIsTheLeftmostPixel()
    {
        var font = new BitmapFont(Rows(128));
        Assert.True(font.IsSet(65, 0, 0));
        Assert.True(font.IsSet(65, 7, 0));
        Assert.False(font.IsSet(65, 1, 0));
        Assert.True(font.IsSet(65, 3, 7));
        Assert.False(font.IsSet(65, 4, 7));
        Assert.Throws<ArgumentOutOfRangeException>(() => font.IsSet(65, 8, 0));
    }

    [Fact]
    public void CodesBeyondAHalfSetAreEmpty()
    {
        var font = new BitmapFont(Rows(128));
        Assert.Equal(new byte[8], font.GetRows(200).ToArray());
        Assert.Equal(new byte[8], font.GetRows(128 + 65).ToArray()); // would be 'A' if wrapped around
    }

    [Fact]
    public void KeepsACopyOfTheRows()
    {
        var rows = Rows(128);
        var font = new BitmapFont(rows);
        rows[65 * 8] = 0;
        Assert.True(font.IsSet(65, 0, 0));
    }
}
