using BombRedone.Core.Economy;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Shop (O3:0E1F) with synthetic fonts: tiles, cursor, keys, mouse, Purchase and Done.</summary>
public class ShopScreenTests
{
    /// <summary>Fonts without visible strokes: only boxes, "+" and cursor are drawn.</summary>
    internal static readonly ShopFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("TINY", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private sealed class Next : IScreen
    {
        public IScreen? Update(TimeSpan elapsed, ScreenInput input) => this;

        public void Draw(Surface surface)
        {
        }
    }

    private static readonly Next After = new();

    private static ShopScreen Create(int score, ShopControl control = ShopControl.Keyboard, PlayerAccount? player = null)
    {
        player ??= new PlayerAccount(1, "ZZZ") { Score = score };
        var dac = new byte[768];
        MenuGraphics.DefaultColors.CopyTo(dac);
        return new ShopScreen(Fonts, new Surface(), dac, new Shop(player, ItemCatalog.Original), control, (63, 0, 0),
            () => After);
    }

    private static IScreen? Press(ShopScreen screen, params ushort[] keys) =>
        screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: keys.Length > 0) { Keys = keys });

    private static IScreen? Click(ShopScreen screen, MouseButtons buttons, int x, int y)
    {
        Assert.Same(screen, screen.Update(TimeSpan.Zero, new ScreenInput(Mouse: buttons) { MouseX = x * 2, MouseY = y }));
        return screen.Update(TimeSpan.Zero, ScreenInput.None);
    }

    private static Surface Picture(ShopScreen screen)
    {
        var surface = new Surface();
        screen.Draw(surface);
        return surface;
    }

    [Fact]
    public void TilesFillColumnsOfNineFromTheTopLeft()
    {
        var screen = Create(20000);
        Assert.Equal(21, screen.Shop.Offers.Count);
        Assert.Equal((5, 40, 104, 53), screen.TileBounds(1));
        Assert.Equal((5, 184, 104, 197), screen.TileBounds(9));
        Assert.Equal((109, 40, 208, 53), screen.TileBounds(10));
        Assert.Equal((213, 40, 312, 53), screen.TileBounds(19));
    }

    [Fact]
    public void HeaderTileAndCursorAreDrawn()
    {
        var picture = Picture(Create(5000));
        Assert.Equal(MenuGraphics.FrameFace, picture[100, 20]); // header face
        Assert.Equal(15, picture[3, 3]); // light bevel of the header
        Assert.Equal(MenuGraphics.FrameFace, picture[50, 45]); // tile 1
        // Cursor box of tile 1 (affordable: colour 11) at (x2 - 9, y1 + 3).
        Assert.Equal(11, picture[95, 43]);
        // "+" of tile 1 in colour 101 at (x2 - 40, y2 - 2).
        Assert.Equal(101, picture[64, 51]);
        Assert.Equal(0, picture[200, 100]); // background below stays
    }

    [Fact]
    public void PlusColourIsRedOnlyWhileAffordable()
    {
        var screen = Create(4000);
        Assert.Equal(63, screen.Dac[101 * 3]); // Incinerator 3000
        Press(screen, BiosKey.Char('+'));
        Assert.Equal(0, screen.Dac[101 * 3]); // 1000 left
    }

    [Fact]
    public void PlayerColourGoesToEntryNineteenPlusNumber()
    {
        var screen = Create(5000);
        Assert.Equal(new byte[] { 63, 0, 0 }, screen.Dac.Slice(20 * 3, 3).ToArray());
    }

    [Fact]
    public void ArrowKeysMoveTheCursorWithinTheTiles()
    {
        var screen = Create(20000);
        Press(screen, BiosKey.Up);
        Assert.Equal(1, screen.Cursor);
        Press(screen, BiosKey.Down, BiosKey.Down);
        Assert.Equal(3, screen.Cursor);
        Press(screen, BiosKey.Right, BiosKey.Right);
        Assert.Equal(21, screen.Cursor);
        Press(screen, BiosKey.Right, BiosKey.Down);
        Assert.Equal(21, screen.Cursor);
        Press(screen, BiosKey.Left);
        Assert.Equal(12, screen.Cursor);
    }

    [Fact]
    public void PlusAndMinusChangeTheQuantityAtTheCursor()
    {
        var screen = Create(10000);
        Press(screen, BiosKey.Char('+'), BiosKey.Space, BiosKey.Char('='));
        Assert.Equal(3, screen.Shop.Offers[0].Quantity);
        Press(screen, BiosKey.Char('-'));
        Assert.Equal(2, screen.Shop.Offers[0].Quantity);
        Assert.Equal(4000, screen.Shop.Unallocated);
    }

    [Fact]
    public void PurchaseKeyBuysAndRebuildsTheTiles()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 10000 };
        var screen = Create(0, player: player);
        Press(screen, BiosKey.Char('+'), BiosKey.Char('+'), BiosKey.Char('+'));
        Assert.Same(screen, Press(screen, BiosKey.Char('p')));
        Assert.Equal(300, player.Stock(ItemKind.Weapon, 1));
        Assert.Equal(1000, screen.Shop.Unallocated);
        Assert.Single(screen.Shop.Offers); // only the stocked Incinerator stays
        Assert.Equal(1, screen.Cursor);
    }

    [Fact]
    public void EnterIsPurchaseAndEscapeIsDone()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 10000 };
        var screen = Create(0, player: player);
        Press(screen, BiosKey.Char('+'));
        Press(screen, BiosKey.Enter);
        Assert.Equal(100, player.Stock(ItemKind.Weapon, 1));
        Assert.Equal(0, player.Spent);
        Assert.Same(After, Press(screen, BiosKey.Escape));
        Assert.Equal(3000, player.Spent);
    }

    [Fact]
    public void DoneKeyBuysTheSelectionAndLeaves()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 10000 };
        var screen = Create(0, player: player);
        Press(screen, BiosKey.Down, BiosKey.Char('+'));
        Assert.Same(After, Press(screen, BiosKey.Char('d')));
        Assert.Equal(10, player.Stock(ItemKind.Weapon, 2));
        Assert.Equal(4000, player.Spent);
    }

    [Fact]
    public void KeyboardPlayerIgnoresTheMouse()
    {
        var screen = Create(10000);
        Click(screen, MouseButtons.Right, 50, 45);
        Assert.Equal(0, screen.Shop.Offers[0].Quantity);
    }

    [Fact]
    public void MouseRightSelectsLeftDeselectsAfterRelease()
    {
        var screen = Create(10000, ShopControl.Mouse);
        var (x1, y1, _, _) = screen.TileBounds(2);
        Click(screen, MouseButtons.Right, x1 + 5, y1 + 5);
        Assert.Equal(2, screen.Cursor);
        Assert.Equal(1, screen.Shop.Offers[1].Quantity);
        Click(screen, MouseButtons.Left, x1 + 5, y1 + 5);
        Assert.Equal(0, screen.Shop.Offers[1].Quantity);
    }

    [Fact]
    public void MousePlayerIgnoresKeys()
    {
        var screen = Create(10000, ShopControl.Mouse);
        Press(screen, BiosKey.Char('+'), BiosKey.Char('d'));
        Assert.Equal(0, screen.Shop.Offers[0].Quantity);
        Assert.False(screen.Shop.Finished);
    }

    [Fact]
    public void MouseButtonsPurchaseAndDone()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 10000 };
        var screen = Create(0, ShopControl.Mouse, player);
        Click(screen, MouseButtons.Right, 50, 45);
        Assert.Same(screen, Click(screen, MouseButtons.Left, 30, 20));
        Assert.Equal(100, player.Stock(ItemKind.Weapon, 1));
        Assert.Same(After, Click(screen, MouseButtons.Left, 300, 20));
        Assert.Equal(3000, player.Spent);
    }

    [Fact]
    public void CursorIsBlackOnAnUnaffordableTile()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 3000 };
        player.SetStock(ItemKind.Defense, 2, 1);
        var screen = Create(0, player: player);
        Press(screen, BiosKey.Down);
        Assert.Equal(0, Picture(screen)[screen.TileBounds(2).X2 - 9, screen.TileBounds(2).Y1 + 3]);
        Assert.Equal(MenuGraphics.FrameFace, Picture(screen)[screen.TileBounds(1).X2 - 9, screen.TileBounds(1).Y1 + 3]);
    }

    [Theory]
    [InlineData(ItemKind.Weapon, 1, 100, "100")]
    [InlineData(ItemKind.Weapon, 1, 0, null)]
    [InlineData(ItemKind.Weapon, 1, -5, null)]
    [InlineData(ItemKind.Defense, 0, 1000, "100%")]
    [InlineData(ItemKind.Defense, 0, 2000, "100%")]
    [InlineData(ItemKind.Defense, 0, 550, "55%")]
    [InlineData(ItemKind.Defense, 1, 40, "40%")]
    [InlineData(ItemKind.Defense, 1, 300, "100%")]
    [InlineData(ItemKind.Defense, 2, 2, "2")]
    public void StockTextFollowsTheOriginalRules(ItemKind kind, int index, int stock, string? text) =>
        Assert.Equal(text, ShopScreen.StockText(ItemCatalog.Original.Get(kind, index), stock));

    [Theory]
    [InlineData(ItemKind.Weapon, 5, "1000 FOR 10000")]
    [InlineData(ItemKind.Defense, 0, "100% FOR 20000")]
    [InlineData(ItemKind.Defense, 1, "100% FOR 10000")]
    [InlineData(ItemKind.Defense, 2, "1 FOR 15000")]
    public void OfferTextShowsAmountAndPrice(ItemKind kind, int index, string text) =>
        Assert.Equal(text, ShopScreen.OfferText(ItemCatalog.Original.Get(kind, index)));

    [Fact]
    public void DollarsAppendsTheWord() => Assert.Equal("2930 DOLLARS", ShopScreen.Dollars(2930));
}
