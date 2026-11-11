using BombRedone.Formats;
using BombRedone.UI.Text;
using G = BombRedone.UI.Tests.Text.SyntheticFont.Glyph;

namespace BombRedone.UI.Tests.Text;

public class GraphTextTests
{
    // 'I': vertical bar 5 high, advance 3. 'L': bar plus foot, advance 4, width table 2 (differs on purpose).
    // 'N': only a move with a negative x, advance -1.
    private static readonly BgiFont Font = BgiFont.Parse(SyntheticFont.Build("SYNT", 73,
    [
        new G(3, ('M', 0, 0), ('D', 0, 4), ('M', 3, 0)),
        new G(5),
        new G(5),
        new G(2, ('M', 0, 4), ('D', 0, 0), ('D', 2, 0), ('M', 4, 0)),
        new G(0),
        new G(1, ('M', -1, 0)),
    ], top: 4, bottom: -1));

    private static GraphText Stroked(int size = 4, TextDirection direction = TextDirection.Horizontal)
    {
        var text = new GraphText { Color = 9 };
        text.SetTextStyle(Font, direction, size);
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Bottom);
        return text;
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

    [Theory]
    [InlineData(1, 3, 5)]
    [InlineData(2, 2, 3)]
    [InlineData(3, 3, 4)]
    [InlineData(4, 1, 1)]
    [InlineData(5, 4, 3)]
    [InlineData(6, 5, 3)]
    [InlineData(7, 2, 1)]
    [InlineData(8, 5, 2)]
    [InlineData(9, 3, 1)]
    [InlineData(10, 4, 1)]
    [InlineData(11, 4, 1)]
    [InlineData(200, 4, 1)]
    public void CharSizeSelectsTheScaleTableAndIsLimitedToTen(int size, int mult, int div)
    {
        var text = Stroked(size);
        Assert.Equal((mult, div, mult, div), text.Scale);
    }

    [Fact]
    public void UserCharSizeAppliesAtSizeZeroAndTreatsDivisorZeroAsOne()
    {
        var text = Stroked(4);
        text.SetUserCharSize(3, 0, 5, 2);
        Assert.Equal(0, text.CharSize);
        Assert.Equal((3, 1, 5, 2), text.Scale);
        text.SetTextStyle(Font, TextDirection.Horizontal, 2);
        Assert.Equal((2, 3, 2, 3), text.Scale);
        text.SetTextStyle(Font, TextDirection.Horizontal, 0);
        Assert.Equal((3, 1, 5, 2), text.Scale);
    }

    [Fact]
    public void TextWidthSumsTheWidthTableFirstAndScalesOnce()
    {
        var text = Stroked(1); // 3/5
        // I + L = 3 + 2 = 5 -> 5 * 3 / 5 = 3 (scaling each first would give 1 + 1 = 2)
        Assert.Equal(3, text.TextWidth("IL"));
        // Characters outside the font are skipped; a NUL does not end the sum.
        Assert.Equal(3, text.TextWidth("I\0zL"));
        Assert.Equal(0, text.TextWidth(""));
    }

    [Fact]
    public void TextHeightIsTopMinusBottomScaledAndIndependentOfTheText()
    {
        Assert.Equal(5, Stroked(4).TextHeight("I"));
        Assert.Equal(3, Stroked(1).TextHeight("whatever")); // 5 * 3 / 5
        Assert.Equal(20, Stroked(10).TextHeight(""));
    }

    [Fact]
    public void LeftBottomDrawsFromTheBaselineUpwards()
    {
        var surface = new Surface(40, 30);
        Stroked().OutTextXY(surface, 10, 20, "I");
        Assert.Equal([(10, 16), (10, 17), (10, 18), (10, 19), (10, 20)], SetPixels(surface));
        Assert.All(SetPixels(surface), p => Assert.Equal(9, surface[p.X, p.Y]));
    }

    [Fact]
    public void TheOriginAdvancesByTheLastPointNotByTheWidthTable()
    {
        var surface = new Surface(40, 30);
        Stroked().OutTextXY(surface, 10, 20, "LI");
        // 'L' advances by 4 (its width table says 2), so 'I' starts at x = 14.
        Assert.Contains((14, 16), SetPixels(surface));
        Assert.Contains((12, 20), SetPixels(surface));
        Assert.DoesNotContain((13, 20), SetPixels(surface));
    }

    [Fact]
    public void NegativeAdvanceAndSkippedCharactersMoveAsInTheKernel()
    {
        var surface = new Surface(40, 30);
        // 'N' moves back by one, 'z' is not in the font and does not move.
        Stroked().OutTextXY(surface, 10, 20, "INzI");
        Assert.Equal(10, SetPixels(surface).Count);
        Assert.Contains((12, 16), SetPixels(surface));
    }

    [Fact]
    public void NulEndsTheOutput()
    {
        var surface = new Surface(40, 30);
        Stroked().OutTextXY(surface, 10, 20, "I\0I");
        Assert.Equal(5, SetPixels(surface).Count);
    }

    [Fact]
    public void PointsAreScaledEachWithTruncationTowardsZero()
    {
        var surface = new Surface(40, 30);
        var text = Stroked(1); // 3/5: y 4 -> 2, advance 3 -> 1
        text.OutTextXY(surface, 10, 20, "II");
        Assert.Equal([(10, 18), (11, 18), (10, 19), (11, 19), (10, 20), (11, 20)], SetPixels(surface));
    }

    [Theory]
    [InlineData(HorizontalJustify.Left, VerticalJustify.Bottom, 10, 20)]
    [InlineData(HorizontalJustify.Center, VerticalJustify.Bottom, 7, 20)]
    [InlineData(HorizontalJustify.Right, VerticalJustify.Bottom, 2, 20)]
    [InlineData(HorizontalJustify.Left, VerticalJustify.Center, 10, 22)]
    [InlineData(HorizontalJustify.Left, VerticalJustify.Top, 10, 25)]
    [InlineData(HorizontalJustify.Center, VerticalJustify.Center, 7, 22)]
    public void JustificationMovesTheOrigin(HorizontalJustify horizontal, VerticalJustify vertical, int originX,
        int originY)
    {
        // "ILI": width table 3 + 2 + 3 = 8, height 5. Center: x - (8 / 2 - 1), y + 5 / 2; right: x - 8; top: y + 5.
        var surface = new Surface(60, 40);
        var text = Stroked();
        text.SetTextJustify(horizontal, vertical);
        text.OutTextXY(surface, 10, 20, "ILI");
        var expected = new Surface(60, 40);
        Stroked().OutTextXY(expected, originX, originY, "ILI");
        Assert.Equal(expected.Pixels, surface.Pixels);
    }

    [Fact]
    public void VerticalTextIsRotatedCounterclockwiseAndRunsUpwards()
    {
        var surface = new Surface(40, 40);
        Stroked(direction: TextDirection.Vertical).OutTextXY(surface, 10, 30, "II");
        // Left/bottom: the origin first moves right by the height (5); glyph y goes to the left, glyph x upwards.
        var expected = new List<(int, int)>();
        foreach (var y in new[] { 27, 30 })
            for (var x = 11; x <= 15; x++)
                expected.Add((x, y));
        Assert.Equal(expected.OrderBy(p => p.Item2).ThenBy(p => p.Item1), SetPixels(surface));
    }

    [Fact]
    public void VerticalJustificationOfVerticalTextUsesTheWidth()
    {
        var surface = new Surface(40, 40);
        var text = Stroked(direction: TextDirection.Vertical);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        text.OutTextXY(surface, 10, 10, "II");
        // x: +5 (height), centre -(5 / 2 - 1) = -1 -> 14; y: + width 6 -> 16.
        Assert.Contains((14 - 4, 16), SetPixels(surface));
        Assert.Contains((14, 16), SetPixels(surface));
        Assert.Contains((14, 13), SetPixels(surface));
        Assert.Equal(10, SetPixels(surface).Count);
    }

    [Fact]
    public void LinesOutsideTheSurfaceAreClipped()
    {
        var surface = new Surface(12, 18);
        Stroked().OutTextXY(surface, 10, 20, "II");
        Assert.Equal([(10, 16), (10, 17)], SetPixels(surface));
    }

    [Fact]
    public void RejectsUndefinedEnumValues()
    {
        var text = new GraphText();
        Assert.Throws<ArgumentOutOfRangeException>(() => text.SetTextStyle(Font, (TextDirection)2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => text.SetTextJustify((HorizontalJustify)3, VerticalJustify.Top));
        Assert.Throws<ArgumentOutOfRangeException>(() => text.SetTextJustify(HorizontalJustify.Left, (VerticalJustify)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => text.SetTextStyle(Font, TextDirection.Horizontal, -1));
    }

    [Fact]
    public void StringsMapCharsUpTo255OneToOne()
    {
        Assert.Equal(new byte[] { 65, 0, 0xE9, (byte)'?' }, GraphText.ToBytes("A\0é€"));
    }
}
