using System.Security.Cryptography;
using System.Text;
using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Team setup and team names with the fonts of BOMB.EXE. [APPROX] Neither a DOSBox image nor a video frame of these
/// screens exists yet: the checksums fix the current result.
/// </summary>
public class PlayerSelectTeamScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static (PlayerSelectTeamScreen Screen, PlayerRoster Roster) Create(int players)
    {
        var gameData = TestGameData.Load();
        var fonts = MenuFonts.Load(gameData, BitmapFontSubstitute.Create());
        var menu = new MenuScreen(fonts, new MenuSettings(Players: players), _ => 1, (_, m) => m);
        var roster = new PlayerRoster();
        var select = new PlayerSelectScreen(menu, roster, _ => 0, _ => null);
        var teams = new PlayerSelectTeams(new OptionsSettings { TeamPlay = true },
            BgiFontLibrary.Load(gameData)[BgiFontLibrary.Tiny]);
        return (new PlayerSelectTeamScreen(menu, roster, select.Screen, roster.Start(_ => 0), teams, _ => 0, _ => null), roster);
    }

    private static int Count(Surface screen, byte color, int x1, int y1, int x2, int y2)
    {
        var count = 0;
        for (var y = y1; y <= y2; y++)
            for (var x = x1; x <= x2; x++)
                if (screen[x, y] == color)
                    count++;
        return count;
    }

    private static void Press(IScreen screen, params ushort[] keys) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true) { Keys = keys });

    [GameDataFact]
    public void TextsAreThePascalStringsOfBombOvr()
    {
        var ovr = File.ReadAllBytes(TestGameData.Load().Find("BOMB.OVR")!);
        var texts = PlayerSelectTeamScreen.OriginalTexts.Concat(PlayerSelectTeamNamesScreen.OriginalTexts).ToList();
        Assert.Equal(21, texts.Count);
        foreach (var (at, text) in texts)
            Assert.Equal(text, Encoding.Latin1.GetString(ovr, at + 1, ovr[at]));
    }

    [GameDataFact]
    public void KeySetsAreTheSetsOfBombOvr()
    {
        // [ORIGINAL] Sets of char (32 bytes): O2:26DF = {13, 27, 'D'} (Done), O2:3125 = {8}, O2:3145 = {13, 27},
        // O2:3165 = {32..125}, O2:3188 = {0, 32}.
        var ovr = File.ReadAllBytes(TestGameData.Load().Find("BOMB.OVR")!);
        static int[] Set(byte[] file, int offset) =>
            [.. Enumerable.Range(0, 256).Where(c => (file[PlayerSelectScreen.OverlayO2FileOffset + offset + c / 8] >> (c % 8) & 1) != 0)];
        Assert.Equal([13, 27, 'D'], Set(ovr, 0x26DF));
        Assert.Equal([8], Set(ovr, 0x3125));
        Assert.Equal([13, 27], Set(ovr, 0x3145));
        Assert.Equal(Enumerable.Range(32, 94), Set(ovr, 0x3165));
        Assert.Equal([0, 32], Set(ovr, 0x3188));
    }

    [GameDataTheory]
    [InlineData(3, "")]
    [InlineData(10, "")]
    [InlineData(3, "moved")]
    [InlineData(3, "cancel")]
    [InlineData(3, "names")]
    public void ScreenMatchesTheRecordedChecksum(int players, string state)
    {
        var (screen, _) = Create(players);
        IScreen current = screen;
        Surface pixels = screen.Screen;
        if (state == "moved")
            Press(screen, BiosKey.Down, BiosKey.Left);
        if (state == "cancel")
            Press(screen, BiosKey.Enter);
        if (state == "names")
        {
            Press(screen, BiosKey.Down, BiosKey.Left, BiosKey.Enter);
            current = screen.Update(TimeSpan.Zero, ScreenInput.None)!;
            var names = Assert.IsType<PlayerSelectTeamNamesScreen>(current);
            Press(names, BiosKey.Char('X'));
            pixels = names.Screen;
        }

        Assert.Equal(Expected[(players, state)], Sha(pixels.Pixels));
    }

    private static readonly Dictionary<(int, string), string> Expected = new()
    {
        [(3, "")] = "dc5ebf8b67a734f683d30e21b1c482b46e99c804118cdb64659cd3c5157be53c",
        [(10, "")] = "09bbf7ab89849e81bfca3239c7c7c29308bd0fa16460288d0a352bef8cb363a7",
        [(3, "moved")] = "ddc84dc5a3fae35c6d51f199ba2f1164fec69c9048e68be65ae8ba665f53779e",
        [(3, "cancel")] = "dacfa377deb218107233f718de42e7037f792646875668b7e6dc8c42f92566f1",
        [(3, "names")] = "349260f74e454fec466ff0f536990f3fb23e5062581fcc1b5307679eb075f62e",
    };

    [GameDataFact]
    public void TextsAreDrawnInTheirColours()
    {
        var (screen, _) = Create(3);
        var s = screen.Screen;
        Assert.InRange(Count(s, 10, 130, 45, 190, 57), 20, 400);   // "Team Setup"
        Assert.InRange(Count(s, 5, 130, 186, 190, 197), 20, 400);  // "-- Done --"
        Assert.InRange(Count(s, 14, 64, 62, 145, 70), 20, 400);    // "Team # -->" and team numbers 1..3
        Assert.InRange(Count(s, 20, 64, 72, 135, 79), 10, 400);    // name of player 1
        Assert.InRange(Count(s, 22, 64, 92, 135, 99), 10, 400);    // name of player 3
        Assert.Equal(0, Count(s, 23, 64, 102, 135, 109));          // player 4 is drawn black
    }

    [GameDataFact]
    public void CancelMessageUsesBrownAndRed()
    {
        var (screen, _) = Create(3);
        Press(screen, BiosKey.Enter);
        Assert.True(screen.ShowsCancelMessage);
        Assert.InRange(Count(screen.Screen, 6, 90, 112, 230, 128), 50, 2000);
        Assert.InRange(Count(screen.Screen, 4, 90, 129, 230, 142), 20, 2000);
    }
}
