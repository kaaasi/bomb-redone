using BombRedone.Core.Economy;
using BombRedone.Formats;
using BombRedone.UI;
using BombRedone.UI.Screens;

namespace BombRedone.Tools;

/// <summary>
/// <c>bombtool render &lt;screen&gt; [gamedata] [out.png]</c>: draws a screen of the status/shop package with fixed sample
/// data (status screen: Longplay frame 00064; shop: short video frame TW 00100, drawn over the status
/// background) and writes it as PNG. No window, no game round needed.
/// </summary>
internal static class RenderCommand
{
    /// <summary>Screens that can be rendered.</summary>
    public static readonly IReadOnlyList<string> Screens = ["shop", "score"];

    public static int Run(string[] args)
    {
        if (args.Length is < 1 or > 3 || args[0] is "-h" or "--help" || !Screens.Contains(args[0]))
        {
            Console.Error.WriteLine($"Usage: bombtool render <{string.Join('|', Screens)}> [gamedata] [out.png]");
            return 2;
        }

        GameData data;
        try
        {
            data = GameData.Locate(args.ElementAtOrDefault(1));
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var surface = new Surface();
        var dac = Render(args[0], data, surface);
        var output = args.ElementAtOrDefault(2) ?? $"{args[0]}.png";
        File.WriteAllBytes(output, ExportPng.Encode(new RawImage(surface.Pixels, Palette.FromBytes(dac))));
        Console.WriteLine($"{args[0]}: {Path.GetFullPath(output)}");
        return 0;
    }

    /// <summary>Draws <paramref name="screen"/> into <paramref name="surface"/>; returns the 6-bit DAC.</summary>
    internal static byte[] Render(string screen, GameData data, Surface surface)
    {
        var fonts = ShopFonts.Load(data, BitmapFontSubstitute.Create());
        switch (screen)
        {
            case "shop":
                var shop = SampleShop();
                shop.Update(TimeSpan.Zero, ScreenInput.None);
                shop.Draw(surface);
                return shop.Dac.ToArray();
            case "score":
                var score = SampleScore();
                score.Draw(surface);
                return score.Dac.ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
        }

        ScoreScreen SampleScore() => new(fonts,
            [
                new ScoreLine(new PlayerAccount(1, "Javier") { Wins = 1, Score = 4260 }, (63, 0, 0)),
                new ScoreLine(new PlayerAccount(2, "Lob & Shoot") { Score = 0 }, (0, 63, 0)),
            ], 1, 10, buttons: true, (_, _, _) => null);

        ShopScreen SampleShop()
        {
            var player = SamplePlayer();
            var status = SampleScore();
            var screen = new ShopScreen(fonts, status.Background, status.Dac, new Shop(player, ItemCatalog.Original),
                ShopControl.Keyboard, (63, 0, 0), () => null);
            // Cursor to "5 Meg Nuke" and select one bundle, as in TW 00100.
            screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)
            {
                Keys = [BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Char('+')],
            });
            return screen;
        }
    }

    /// <summary>Player of TW 00100: "ZZZ" with 12930 dollars and the stock shown in the frame.</summary>
    internal static PlayerAccount SamplePlayer()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 12930 };
        player.SetStock(ItemKind.Weapon, 1, 8);
        player.SetStock(ItemKind.Weapon, 2, 7);
        player.SetStock(ItemKind.Weapon, 3, 1);
        player.SetStock(ItemKind.Weapon, 5, 1000);
        player.SetStock(ItemKind.Weapon, 6, 3);
        return player;
    }
}
