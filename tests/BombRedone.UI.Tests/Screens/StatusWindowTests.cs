using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>"Game Status" window (U431:0F1B) with the substitute 8 x 8 font: frame, lines, closing.</summary>
public class StatusWindowTests
{
    private static int _closed;

    private static StatusWindow Create(Surface? playfield = null)
    {
        _closed = 0;
        return new StatusWindow(BitmapFontSubstitute.Create(), playfield ?? new Surface(), new byte[768],
            new StatusWindowData(new OptionsSettings(), true, false, 3, 10, WallSetting.Warp, WindSetting.High), () =>
            {
                _closed++;
                return null;
            });
    }

    private static int Count(Surface surface, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (surface[x, y] == color)
                    count++;
        return count;
    }

    [Fact]
    public void WindowIsABlackBarWithTwoOutlines()
    {
        var playfield = new Surface();
        playfield.Pixels.AsSpan().Fill(7);
        var window = Create(playfield);
        Assert.Equal(7, window.Screen[9, 25]);
        Assert.Equal(StatusWindow.FrameColor, window.Screen[10, 25]);
        Assert.Equal(StatusWindow.FrameColor, window.Screen[310, 175]);
        Assert.Equal(0, window.Screen[11, 26]);
        Assert.Equal(StatusWindow.FrameColor, window.Screen[12, 100]);
        Assert.Equal(StatusWindow.FrameColor, window.Screen[308, 100]);
        Assert.Equal(7, window.Screen[311, 176]);
        Assert.Equal(7, window.Saved[100, 100]);
    }

    [Fact]
    public void LinesUseLabelTextAndValueColours()
    {
        var screen = Create().Screen;
        Assert.InRange(Count(screen, StatusWindow.TitleColor, 100, 30, 220, 40), 20, 400);
        // Row y = 45: "Sound: " in colour 1 from x = 20, then text 5 and values 14.
        Assert.InRange(Count(screen, StatusWindow.LabelColor, 20, 41, 75, 49), 20, 300);
        Assert.InRange(Count(screen, StatusWindow.TextColor, 76, 41, 300, 49), 20, 600);
        Assert.InRange(Count(screen, StatusWindow.ValueColor, 76, 41, 300, 49), 10, 300);
        // y = 125 stays empty.
        Assert.Equal(0, Count(screen, StatusWindow.LabelColor, 14, 121, 306, 129)
            + Count(screen, StatusWindow.TextColor, 14, 121, 306, 129) + Count(screen, StatusWindow.ValueColor, 14, 121, 306, 129));
    }

    [Fact]
    public void SecondLinesStartAfterTheLabel()
    {
        var screen = Create().Screen;
        // "The Walls: " is 11 characters of 8 pixels: "This Round (" starts at x = 108 on row 75, nothing left of it.
        Assert.Equal(0, Count(screen, StatusWindow.TextColor, 14, 71, 107, 79));
        Assert.True(Count(screen, StatusWindow.TextColor, 108, 71, 120, 79) > 0);
    }

    [Fact]
    public void ClosesAfterAButtonIsPressedAndReleased()
    {
        var window = Create();
        Assert.Same(window, window.Update(TimeSpan.Zero, ScreenInput.None));
        Assert.Same(window, window.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Left)));
        Assert.Same(window, window.Update(TimeSpan.Zero, new ScreenInput(Mouse: MouseButtons.Left)));
        Assert.Equal(0, _closed);
        Assert.Null(window.Update(TimeSpan.Zero, ScreenInput.None));
        Assert.Equal(1, _closed);
        Assert.Equal(0, window.Screen[10, 25]);
    }

    [Theory]
    [InlineData(BiosKey.Escape)]
    [InlineData(BiosKey.Enter)]
    [InlineData(BiosKey.Space)]
    public void ClosesWithEscEnterOrSpace(ushort key)
    {
        var window = Create();
        window.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [key] });
        Assert.Null(window.Update(TimeSpan.Zero, ScreenInput.None));
    }

    [Fact]
    public void OtherKeysDoNotClose()
    {
        var window = Create();
        window.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [BiosKey.Char('x')] });
        Assert.Same(window, window.Update(TimeSpan.Zero, ScreenInput.None));
        Assert.Equal(0, _closed);
    }

    /// <summary>
    /// [APPROX] Substitute 8 x 8 font (the ROM font is not part of the original files) and no recording of the window:
    /// the checksum fixes the current result of the disassembly.
    /// </summary>
    [Fact]
    public void WindowMatchesTheRecordedChecksum()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Create().Screen.Pixels)).ToLowerInvariant();
        Assert.Equal("40ad6f8d3976913580fe1b878e744ee8cf4b91c58a2d1599244fa3a264a35d0a", hash);
    }
}
