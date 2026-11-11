using BombRedone.UI.Text;

namespace BombRedone.UI.Tests.Text;

public class LineRasterizerTests
{
    private static List<(int X, int Y)> Line(int x0, int y0, int x1, int y1, int width = 20, int height = 20)
    {
        var surface = new Surface(width, height);
        LineRasterizer.Draw(surface, x0, y0, x1, y1, 1);
        var result = new List<(int, int)>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            if (surface[x, y] != 0)
                result.Add((x, y));
        return result;
    }

    [Fact]
    public void SinglePoint() => Assert.Equal([(3, 4)], Line(3, 4, 3, 4));

    [Fact]
    public void BothEndPointsAreDrawn()
    {
        Assert.Equal([(2, 5), (3, 5), (4, 5), (5, 5)], Line(5, 5, 2, 5));
        Assert.Equal([(1, 1), (2, 2), (3, 3)], Line(1, 1, 3, 3));
    }

    [Fact]
    public void ShallowLineHasOnePixelPerColumn()
    {
        var pixels = Line(0, 0, 10, 3);
        Assert.Equal(11, pixels.Count);
        Assert.Equal(Enumerable.Range(0, 11), pixels.Select(p => p.X).Order());
        Assert.Contains((0, 0), pixels);
        Assert.Contains((10, 3), pixels);
    }

    [Fact]
    public void PixelsOutsideTheSurfaceAreSkipped() =>
        Assert.Equal([(0, 2), (1, 2)], Line(-3, 2, 1, 2, 4, 4));
}
