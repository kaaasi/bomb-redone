using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Drawing routines of UCC8 and U431 with synthetic surfaces.</summary>
public class MenuGraphicsTests
{
    private static HashSet<(int X, int Y)> Set(Surface surface, byte color)
    {
        var result = new HashSet<(int, int)>();
        for (var y = 0; y < surface.Height; y++)
            for (var x = 0; x < surface.Width; x++)
                if (surface[x, y] == color)
                    result.Add((x, y));
        return result;
    }

    [Theory]
    [InlineData(10, 5, 3, 5)]
    [InlineData(3, 5, 10, 5)]
    public void HorizontalLinesIncludeBothEnds(int x1, int y1, int x2, int y2)
    {
        var surface = new Surface();
        MenuGraphics.Line(surface, x1, y1, x2, y2, 7);
        Assert.Equal(Enumerable.Range(3, 8).Select(x => (x, 5)).ToHashSet(), Set(surface, 7));
    }

    [Theory]
    [InlineData(4, 9, 4, 2)]
    [InlineData(4, 2, 4, 9)]
    public void VerticalLinesIncludeBothEnds(int x1, int y1, int x2, int y2)
    {
        var surface = new Surface();
        MenuGraphics.Line(surface, x1, y1, x2, y2, 7);
        Assert.Equal(Enumerable.Range(2, 8).Select(y => (4, y)).ToHashSet(), Set(surface, 7));
    }

    [Fact]
    public void FlatDiagonalFollowsTheErrorTermOfTheOriginal()
    {
        var surface = new Surface();
        MenuGraphics.Line(surface, 4, 2, 0, 0, 7);
        Assert.Equal(new HashSet<(int, int)> { (0, 0), (1, 1), (2, 1), (3, 2), (4, 2) }, Set(surface, 7));
    }

    [Fact]
    public void SteepDiagonalFollowsTheErrorTermOfTheOriginal()
    {
        var surface = new Surface();
        MenuGraphics.Line(surface, 0, 0, 1, 3, 7);
        Assert.Equal(new HashSet<(int, int)> { (0, 0), (0, 1), (1, 2), (1, 3) }, Set(surface, 7));
    }

    [Fact]
    public void UpwardDiagonalStartsAtTheLeftEnd()
    {
        var surface = new Surface();
        MenuGraphics.Line(surface, 4, 10, 0, 12, 7);
        Assert.Equal(new HashSet<(int, int)> { (0, 12), (1, 11), (2, 11), (3, 10), (4, 10) }, Set(surface, 7));
    }

    [Fact]
    public void RectangleWithCrossedCornersDrawsBothHorizontalLines()
    {
        // The background draws frames up to k = 160, where the upper corner lies below the lower one.
        var surface = new Surface();
        MenuGraphics.Rectangle(surface, 150, 150, 169, 49, 7);
        Assert.Equal(7, surface[160, 150]);
        Assert.Equal(7, surface[160, 49]);
        Assert.Equal(7, surface[150, 100]);
        Assert.Equal(7, surface[169, 100]);
        Assert.Equal(0, surface[160, 100]);
    }

    [Fact]
    public void FrameHasALightTopLeftAndADarkBottomRightBevel()
    {
        var surface = new Surface();
        MenuGraphics.Frame(surface, 10, 10, 20, 15, face: 18, light: 15, dark: 17);
        Assert.Equal(18, surface[10, 10]);
        Assert.Equal(18, surface[20, 15]);
        Assert.Equal(15, surface[9, 12]);
        Assert.Equal(15, surface[8, 12]);
        Assert.Equal(15, surface[15, 8]);
        Assert.Equal(17, surface[21, 12]);
        Assert.Equal(17, surface[22, 12]);
        Assert.Equal(17, surface[15, 17]);
        // Light edges are drawn last: they win at the top right and bottom left corners.
        Assert.Equal(15, surface[21, 9]);
        Assert.Equal(15, surface[22, 8]);
        Assert.Equal(15, surface[9, 16]);
        Assert.Equal(15, surface[8, 17]);
        Assert.Equal(17, surface[21, 16]);
        Assert.Equal(17, surface[22, 17]);
        Assert.Equal(0, surface[7, 7]);
        Assert.Equal(0, surface[23, 18]);
    }

    [Fact]
    public void CopyRectCopiesTheInclusiveAreaWithCornersInAnyOrder()
    {
        var source = new Surface();
        source.Clear(5);
        var target = new Surface();
        MenuGraphics.CopyRect(source, target, 30, 20, 10, 12);
        Assert.Equal(21 * 9, Set(target, 5).Count);
        Assert.Equal(5, target[10, 12]);
        Assert.Equal(5, target[30, 20]);
        Assert.Equal(0, target[31, 20]);
        Assert.Equal(0, target[10, 21]);
    }

    [Fact]
    public void MarkerIsAWhitePlusAndEraseRestoresIt()
    {
        var saved = new Surface();
        saved.Clear(3);
        var surface = new Surface();
        saved.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.Marker(surface, 112, 95);
        Assert.Equal(new HashSet<(int, int)> { (112, 95), (111, 95), (113, 95), (112, 94), (112, 96) },
            Set(surface, MenuGraphics.FrameWhite));
        MenuGraphics.EraseMarker(surface, saved, 112, 95);
        Assert.Equal(saved.Pixels, surface.Pixels);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(320, 50)]
    [InlineData(50, 0)]
    [InlineData(50, 199)]
    public void MarkerOutsideTheOpenBoundsIsSkipped(int x, int y)
    {
        var surface = new Surface();
        MenuGraphics.Marker(surface, x, y);
        Assert.Empty(Set(surface, MenuGraphics.FrameWhite));
    }

    [Fact]
    public void FrameColorsAreTheThreeGreys()
    {
        var dac = new byte[768];
        MenuGraphics.SetFrameColors(dac);
        Assert.Equal(new byte[] { 21, 21, 21, 42, 42, 42, 63, 63, 63 }, dac[(17 * 3)..(20 * 3)]);
    }
}
