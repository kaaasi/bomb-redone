using BombRedone.Formats;

namespace BombRedone.UI.Screens;

/// <summary>Fonts of the status, result and shop screens.</summary>
/// <param name="Euro">EURO, registered as DS:E910 (player name in the shop).</param>
/// <param name="Small">SmallFont LITT, BGI font number 2 (item names, status lines).</param>
/// <param name="Tiny">TINY, registered as DS:E91A (amounts, prices, money).</param>
/// <param name="Default">8 x 8 DefaultFont.</param>
public sealed record ShopFonts(BgiFont Euro, BgiFont Small, BgiFont Tiny, BitmapFont Default)
{
    /// <summary>Fonts from BOMB.EXE and the 8 x 8 font chosen by <see cref="BitmapFontSource"/>.</summary>
    public static ShopFonts Load(GameData gameData, BitmapFont defaultFont)
    {
        var fonts = BgiFontLibrary.Load(gameData);
        return new ShopFonts(fonts[BgiFontLibrary.Euro], fonts[BgiFontLibrary.Small], fonts[BgiFontLibrary.Tiny], defaultFont);
    }
}
