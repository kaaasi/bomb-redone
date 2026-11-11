using BombRedone.Tests.Support;
using BombRedone.UI;

namespace BombRedone.UI.Tests;

public class SurfaceTests
{
    [Fact]
    public void DefaultSizeIsTheOriginalMode()
    {
        var surface = new Surface();
        Assert.Equal(320, surface.Width);
        Assert.Equal(200, surface.Height);
        Assert.Equal(64000, surface.Pixels.Length);
        Assert.Equal(256, surface.Palette.Length);
    }

    [Fact]
    public void PixelsAreStoredRowByRowAndBoundsAreChecked()
    {
        var surface = new Surface(4, 3);
        surface[3, 2] = 7;
        Assert.Equal(7, surface.Pixels[11]);
        Assert.Equal(7, surface[3, 2]);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[4, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => surface[0, -1]);
    }

    [Fact]
    public void CopyToMapsIndicesThroughThePalette()
    {
        var surface = new Surface(2, 1);
        surface.Palette[1] = 0xFF112233;
        surface.Palette[2] = 0xFF445566;
        surface[0, 0] = 1;
        surface[1, 0] = 2;
        var target = new uint[2];
        surface.CopyTo(target);
        Assert.Equal([0xFF112233u, 0xFF445566u], target);
        Assert.Throws<ArgumentException>(() => surface.CopyTo(new uint[1]));
    }

    [Theory]
    // window, aspect correction -> x, y, width, height, scale
    [InlineData(960, 600, false, 0, 0, 960, 600, 3)]
    [InlineData(960, 720, true, 0, 0, 960, 720, 3)]
    [InlineData(960, 720, false, 0, 60, 960, 600, 3)]
    [InlineData(1000, 700, true, 180, 110, 640, 480, 2)]
    [InlineData(1920, 1080, true, 320, 60, 1280, 960, 4)]
    [InlineData(1920, 1080, false, 160, 40, 1600, 1000, 5)]
    [InlineData(100, 100, false, -110, -50, 320, 200, 1)]
    public void PresentationScalesByWholeNumbersAndCenters(int windowWidth, int windowHeight, bool aspect,
        int x, int y, int width, int height, int scale)
    {
        var rect = Presentation.Fit(windowWidth, windowHeight, Surface.OriginalWidth, Surface.OriginalHeight, aspect);
        Assert.Equal(new PresentationRect(x, y, width, height, scale), rect);
    }

    [GameDataFact]
    public void TitleImagesFitOneFrameBufferPlusPalette()
    {
        // 320 x 200 palette indices followed by 256 RGB triples; the layout itself is analysed in round 1.
        var data = TestGameData.Load();
        var expected = Surface.OriginalWidth * Surface.OriginalHeight + Surface.PaletteSize * 3;
        Assert.Equal(expected, new FileInfo(data.Require("BOMB1.RAW")).Length);
        Assert.Equal(expected, new FileInfo(data.Require("BOMB2.RAW")).Length);
    }
}
