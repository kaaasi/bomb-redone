using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Tests.Text;

public class GraphTextBitmapTests
{
    // Synthetic 8 x 8 font: 'A' has its top left and bottom right pixel set, 'B' only its bottom left pixel.
    private static readonly BitmapFont Font = CreateFont();

    private static BitmapFont CreateFont()
    {
        var rows = new byte[128 * 8];
        rows[65 * 8] = 0x80;
        rows[65 * 8 + 7] = 0x01;
        rows[66 * 8 + 7] = 0x80;
        return new BitmapFont(rows);
    }

    private static List<(int X, int Y)> SetPixels(Surface surface)
    {
        var result = new List<(int, int)>();
        for (var y = 0; y < surface.Height; y++)
        for (var x = 0; x < surface.Width; x++)
            if (surface[x, y] != 0)
                result.Add((x, y));
        return result;
    }

    private static GraphText Text(int size = 1, HorizontalJustify h = HorizontalJustify.Left,
        VerticalJustify v = VerticalJustify.Top)
    {
        var text = new GraphText(Font) { Color = 4 };
        text.SetTextStyle(null, TextDirection.Horizontal, size);
        text.SetTextJustify(h, v);
        return text;
    }

    [Fact]
    public void DefaultsAreDefaultFontLeftTop()
    {
        var text = new GraphText(Font);
        Assert.Null(text.Font);
        Assert.Equal((HorizontalJustify.Left, VerticalJustify.Top), (text.Horizontal, text.Vertical));
        Assert.Equal(1, text.CharSize);
    }

    [Fact]
    public void LeftTopIsTheTopLeftCornerOfTheFirstCell()
    {
        var surface = new Surface(40, 30);
        Text().OutTextXY(surface, 5, 3, "AA");
        Assert.Equal([(5, 3), (13, 3), (12, 10), (20, 10)], SetPixels(surface));
        Assert.Equal(4, surface[5, 3]);
    }

    [Fact]
    public void SizeScalesEveryPixelToABlock()
    {
        var surface = new Surface(40, 30);
        var text = Text(2);
        Assert.Equal((32, 16), (text.TextWidth("AA"), text.TextHeight("AA")));
        text.OutTextXY(surface, 0, 0, "A");
        Assert.Equal([(0, 0), (1, 0), (0, 1), (1, 1), (14, 14), (15, 14), (14, 15), (15, 15)], SetPixels(surface));
    }

    [Theory]
    [InlineData(HorizontalJustify.Center, VerticalJustify.Center, 20 - (16 / 2 - 1), 10 - 4)]
    [InlineData(HorizontalJustify.Right, VerticalJustify.Bottom, 20 - 16, 10 - 8)]
    [InlineData(HorizontalJustify.Left, VerticalJustify.Bottom, 20, 2)]
    public void JustificationOfTheDefaultFont(HorizontalJustify h, VerticalJustify v, int left, int top)
    {
        var surface = new Surface(40, 30);
        Text(1, h, v).OutTextXY(surface, 20, 10, "AA");
        Assert.Equal((left, top), SetPixels(surface)[0]);
    }

    [Fact]
    public void NothingIsDrawnWhenTheJustifiedXIsNegative()
    {
        var surface = new Surface(40, 30);
        Text(1, HorizontalJustify.Right).OutTextXY(surface, 15, 0, "AA");
        Assert.Empty(SetPixels(surface));
        Text(1, HorizontalJustify.Right).OutTextXY(surface, 16, 0, "AA");
        Assert.Equal((0, 0), SetPixels(surface)[0]);
    }

    [Fact]
    public void VerticalDefaultFontIsRotatedCounterclockwise()
    {
        var surface = new Surface(40, 40);
        var text = new GraphText(Font) { Color = 4 };
        text.SetTextStyle(null, TextDirection.Vertical, 1);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        text.OutTextXY(surface, 10, 5, "A");
        // x: 10 - (8 / 2 - 1) = 7; top-left pixel of 'A' goes to the bottom left, bottom right pixel to the top right.
        Assert.Equal([(14, 5), (7, 12)], SetPixels(surface));
    }

    private static GraphText VerticalText()
    {
        var text = new GraphText(Font) { Color = 4 };
        text.SetTextStyle(null, TextDirection.Vertical, 1);
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        return text;
    }

    [Fact]
    public void VerticalDefaultFontRunsUpwardsFromTheFirstCharacter()
    {
        var surface = new Surface(40, 40);
        // x: 10 - 8 = 2; 'A' fills the lower cell (y 13-20), 'B' the upper one (y 5-12).
        VerticalText().OutTextXY(surface, 10, 5, "AB");
        Assert.Equal([(9, 12), (9, 13), (2, 20)], SetPixels(surface));
    }

    [Fact]
    public void NulEndsTheDefaultFontOutput()
    {
        var surface = new Surface(40, 40);
        Text().OutTextXY(surface, 0, 0, "\0A");
        Assert.Empty(SetPixels(surface));
        Text().OutTextXY(surface, 0, 0, "A\0A");
        Assert.Equal([(0, 0), (7, 7)], SetPixels(surface));
    }

    [Fact]
    public void NulEndsTheVerticalDefaultFontOutputFromTheLastCharacter()
    {
        var surface = new Surface(40, 40);
        // The driver runs from the last character backwards: 'B' in the top cell is drawn, the NUL stops before 'A'.
        VerticalText().OutTextXY(surface, 10, 5, "A\0B");
        Assert.Equal([(9, 12)], SetPixels(surface));
    }

    [Fact]
    public void DefaultFontWithoutDataCannotBeDrawn()
    {
        var text = new GraphText();
        Assert.Throws<InvalidOperationException>(() => text.OutTextXY(new Surface(8, 8), 0, 0, "A"));
    }
}
