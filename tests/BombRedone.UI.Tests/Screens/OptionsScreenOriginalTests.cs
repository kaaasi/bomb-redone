using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Game Options with the fonts of BOMB.EXE over the main menu (background pattern 1, 8 x 8 substitute font).
/// [APPROX] No DOSBox image of the options yet: the checksum fixes the current result; the layout was compared with
/// Longplay frame 00017 by position and shape (docs/anforderungen/menue.md §6).
/// </summary>
public class OptionsScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    /// <summary>The values Longplay frame 00017 shows.</summary>
    private static OptionsSettings VideoValues() => new()
    {
        CrumblePercent = 75, Walls = WallSetting.None, TrailVisible = true, Background = 9, Wind = WindSetting.Normal,
    };

    private static (MenuScreen Menu, OptionsScreen Options) Create()
    {
        var menu = new MenuScreen(MenuFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create()), new MenuSettings(),
            _ => 1, (_, m) => m);
        menu.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [BiosKey.Down] });
        return (menu, new OptionsScreen(menu, VideoValues()));
    }

    private static int Count(MenuScreen menu, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (menu.Screen[x, y] == color)
                    count++;
        return count;
    }

    [GameDataFact]
    public void ScreenMatchesTheRecordedChecksum()
    {
        Assert.Equal("6277d56b14de8f32d1a03a929105ab3772432aa036486c97599a15b9692f775c", Sha(Create().Menu.Screen.Pixels));
    }

    [GameDataFact]
    public void LabelsAreBlackOnTheLeftAndValuesYellowOnTheRight()
    {
        var (menu, _) = Create();
        for (var line = 0; line < OptionsScreen.LineCount; line++)
        {
            var y = 52 + 10 * line;
            Assert.InRange(Count(menu, 0, 100, y - 4, 160, y + 4), 10, 400);
            Assert.InRange(Count(menu, 14, 190, y - 4, 223, y + 4), 5, 200);
            Assert.Equal(0, Count(menu, 14, 96, y - 4, 160, y + 4));
            Assert.Equal(0, Count(menu, 0, 224, y - 4, 225, y + 4));
        }
    }

    [GameDataFact]
    public void ChangingAValueTwiceBackDrawsTheSamePixels()
    {
        var (menu, options) = Create();
        var before = menu.Screen.Pixels.ToArray();
        foreach (var key in new[] { BiosKey.Down, BiosKey.Right, BiosKey.Left, BiosKey.Up })
            options.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = [key] });
        Assert.Equal(before, menu.Screen.Pixels);
    }
}
