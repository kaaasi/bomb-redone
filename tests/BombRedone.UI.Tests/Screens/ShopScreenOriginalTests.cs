using System.Security.Cryptography;
using BombRedone.Core.Economy;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Shop with the fonts of BOMB.EXE in the situation of the short video, frame TW 00100 (player "ZZZ", 12930 dollars,
/// one 5 Meg Nuke selected). [APPROX] No DOSBox image of the shop yet: the checksum fixes the current result; tile order,
/// texts and layout were compared with TW 00100 by position and shape (docs/anforderungen/wertung-einkauf.md §4).
/// </summary>
public class ShopScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static ShopScreen Create()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 12930 };
        player.SetStock(ItemKind.Weapon, 1, 8);
        player.SetStock(ItemKind.Weapon, 2, 7);
        player.SetStock(ItemKind.Weapon, 3, 1);
        player.SetStock(ItemKind.Weapon, 5, 1000);
        player.SetStock(ItemKind.Weapon, 6, 3);
        var dac = new byte[768];
        MenuGraphics.DefaultColors.CopyTo(dac);
        var screen = new ShopScreen(ShopFonts.Load(TestGameData.Load(), BitmapFontSubstitute.Create()), new Surface(), dac,
            new Shop(player, ItemCatalog.Original), ShopControl.Keyboard, (63, 0, 0), () => null);
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)
        {
            Keys = [BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Char('+')],
        });
        return screen;
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

    [GameDataFact]
    public void TilesAreThoseOfTheVideoFrame()
    {
        var screen = Create();
        Assert.Equal(
            ["Incinerator", "Mark II Incin.", "20 Kilo Nuke", "5 Meg Nuke", "Laser", "CRI 4/D", "MIRV", "CRI 8/D",
             "CRI 20/ND", "Sonic Blaster", "Ball 'O' Dirt", "Mark II Dirt", "Pressured Dirt", "High Pres. Dirt",
             "Xplosive Dirt", "Inertia Damp."],
            screen.Shop.Offers.Select(o => o.Item.ShortName));
        Assert.Equal((2930, 12930), (screen.Shop.Unallocated, screen.Shop.CurrentlyHave));
    }

    [GameDataFact]
    public void TextsHaveTheirColours()
    {
        var surface = new Surface();
        Create().Draw(surface);
        Assert.InRange(Count(surface, 20, 120, 5, 200, 35), 30, 2000); // name in the player colour
        Assert.InRange(Count(surface, 6, 11, 0, 100, 35), 20, 1000); // unallocated
        Assert.InRange(Count(surface, 14, 220, 0, 309, 35), 20, 1000); // currently have
        Assert.InRange(Count(surface, 4, 5, 40, 104, 47), 10, 400); // weapon name
        Assert.InRange(Count(surface, 9, 109, 148, 208, 155), 10, 400); // defense name in blue
        Assert.InRange(Count(surface, 6, 96, 97, 102, 103), 1, 40); // quantity 1 in the box of 5 Meg Nuke
    }

    [GameDataFact]
    public void ScreenMatchesTheRecordedChecksum()
    {
        var surface = new Surface();
        Create().Draw(surface);
        Assert.Equal("29fd93c97b5ade7577b0971372dd567fd22c3953686d425bdbcb96004f72802e", Sha(surface.Pixels));
    }
}
