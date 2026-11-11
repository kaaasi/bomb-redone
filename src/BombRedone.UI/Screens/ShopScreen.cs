using System.Globalization;
using BombRedone.Core.Economy;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>How a player operates the shop: the original reads either the keyboard or the mouse, never both.</summary>
public enum ShopControl
{
    /// <summary>Player type 1 (DS:E5A6 + 2 × number): keys only (O3:13A3).</summary>
    Keyboard = 1,

    /// <summary>Player type 2: mouse only (O3:14AA).</summary>
    Mouse = 2,
}

/// <summary>
/// The shop ("equipment screen") of one human player: O3:0E1F (BOMB.OVR 0x098AB) with the tile builder O3:015B
/// (BOMB.OVR 0x08BE7); rules in <see cref="Shop"/>, docs/anforderungen/wertung-einkauf.md §3–4.
/// </summary>
/// <remarks>
/// The shop draws over the screen shown before (the status screen in the original: the area below the header is restored
/// from the buffer DS:E5BC, O3:11D1). Busy waits for the mouse release become states that last until an update without
/// a held button. One update runs one pass of the input loop per key typed, at least one for the mouse.
/// </remarks>
public sealed class ShopScreen : IScreen
{
    /// <summary>[ORIGINAL] O3:0199/019F: first tile at (5, 40).</summary>
    public const int FirstTileX = 5, FirstTileY = 40;

    /// <summary>[ORIGINAL] O3:0252–0267: a tile is 99 × 13 pixels inside its bevel (x2 = x1 + 99, y2 = y1 + 13).</summary>
    public const int TileWidth = 99, TileHeight = 13;

    /// <summary>[ORIGINAL] O3:0317–032D: next row 18 pixels lower; below y2 = 197 a new column 104 pixels to the right.</summary>
    public const int RowPitch = 18, ColumnPitch = 104, LastBottom = 197;

    /// <summary>[ORIGINAL] O3:1584–15C6: mouse areas of "Purchase" and "Done".</summary>
    public const int ButtonTop = 16, ButtonBottom = 24, PurchaseLeft = 10, PurchaseRight = 59, DoneLeft = 284, DoneRight = 310;

    /// <summary>[ORIGINAL] O3:0FEB: the player name has colour 19 + player number (set by O10:01AC).</summary>
    public const int PlayerColorBase = 19;

    /// <summary>[ORIGINAL] O3:06ED: the "+" of tile n has its own DAC entry 100 + n.</summary>
    public const int PlusColorBase = 100;

    private const byte Face = MenuGraphics.FrameFace, Dark = MenuGraphics.FrameDark, Light = 15;

    // [ORIGINAL] O3:01A9, O3:0340, O3:04C7: colours of the item names per group (weapons, guidance, defense).
    private static readonly byte[] NameColors = [4, 10, 9];

    private readonly ShopFonts _fonts;
    private readonly Surface _background;
    private readonly Shop _shop;
    private readonly ShopControl _control;
    private readonly Func<IScreen?> _next;
    private readonly Surface _screen = new();
    private readonly byte[] _dac;
    private readonly GraphText _text;
    private readonly Queue<ushort> _keys = new();
    private readonly List<Tile> _tiles = [];

    // Locals of O3:0E1F: [bp-E] cursor, [bp-4] hit, [bp-16] minus, [bp-17] plus, [bp-19]/[bp-1B] left/right button.
    private int _hit;
    private bool _minus, _plus, _leftButton, _rightButton;
    private bool _waitingForRelease;
    private int _shownUnallocated, _shownCurrently;

    /// <param name="fonts">Fonts from BOMB.EXE.</param>
    /// <param name="background">The screen the shop is drawn on (pixels only).</param>
    /// <param name="dac">6-bit DAC of <paramref name="background"/> (768 bytes); the shop changes its own copy.</param>
    /// <param name="shop">Rules and money of the visiting player.</param>
    /// <param name="control">Keyboard or mouse.</param>
    /// <param name="playerColor">6-bit colour of the player (O10:01AC computes it from the player record).</param>
    /// <param name="next">Screen after "Done"; null quits.</param>
    public ShopScreen(ShopFonts fonts, Surface background, ReadOnlySpan<byte> dac, Shop shop, ShopControl control,
        (int Red, int Green, int Blue) playerColor, Func<IScreen?> next)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(shop);
        ArgumentNullException.ThrowIfNull(next);
        if (dac.Length != 768)
            throw new ArgumentException("The DAC has 256 × 3 entries.", nameof(dac));
        if (background.Width != Surface.OriginalWidth || background.Height != Surface.OriginalHeight)
            throw new ArgumentException("The shop draws on a 320 x 200 surface only.", nameof(background));
        _fonts = fonts;
        _background = background;
        _shop = shop;
        _control = control;
        _next = next;
        _dac = dac.ToArray();
        _text = new GraphText(fonts.Default);
        background.Pixels.CopyTo(_screen.Pixels, 0);

        // [ORIGINAL] O3:0F93: U431:01D8 sets the grey ramp; O3:1005: O10[2] the colour of the player.
        MenuGraphics.SetFrameColors(_dac);
        MenuGraphics.SetRgb(_dac, PlayerColorBase + shop.Account.Number, playerColor.Red, playerColor.Green, playerColor.Blue);
        DrawHeader();
        // [ORIGINAL] O3:11D1: restore (0, 38)–(319, 199) from the saved screen, then O3:015B.
        MenuGraphics.CopyRect(_background, _screen, 0, 38, 319, 199);
        BuildTiles();
        Cursor = 1;
        DrawCursor();
    }

    /// <summary>Tile with the cursor box (1-based like the original, 1..<see cref="Shop.Offers"/> count).</summary>
    public int Cursor { get; private set; }

    /// <summary>The shop rules behind the screen.</summary>
    public Shop Shop => _shop;

    /// <summary>Area of tile <paramref name="number"/> (1-based) inside its bevel.</summary>
    public (int X1, int Y1, int X2, int Y2) TileBounds(int number)
    {
        var tile = _tiles[number - 1];
        return (tile.X1, tile.Y1, tile.X2, tile.Y2);
    }

    /// <summary>6-bit DAC as the shop set it.</summary>
    public ReadOnlySpan<byte> Dac => _dac;

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        foreach (var key in input.Keys)
            _keys.Enqueue(key);

        if (_waitingForRelease)
        {
            // [ORIGINAL] O3:15C9–15E1: wait until both buttons are released, then the hit counts.
            if (input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right))
                return this;
            _waitingForRelease = false;
            if (Act())
                return _next();
        }

        do
        {
            var key = _keys.Count > 0 ? _keys.Dequeue() : (ushort?)null;
            if (_control == ShopControl.Keyboard && key is { } word)
                ReadKey(word);
            if (_control == ShopControl.Mouse && Click(input))
            {
                _waitingForRelease = true;
                return this;
            }

            if (Act())
                return _next();
        }
        while (_keys.Count > 0);

        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != _screen.Width || surface.Height != _screen.Height)
            throw new ArgumentException("The shop draws into a 320 x 200 surface only.", nameof(surface));
        _screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    /// <summary>
    /// [ORIGINAL] O3:13AD–14A1: Space, "+" and "=" select, "-" deselects (both at the cursor); P or Enter (set O3:0D7F,
    /// after UpCase) = Purchase, D or Esc (set O3:0D9F) = Done; after a zero character the keys of DS:3716: up and down
    /// move by one, right and left by nine tiles (one column), without leaving 1..count.
    /// </summary>
    private void ReadKey(ushort word)
    {
        var count = _tiles.Count;
        var character = char.ToUpperInvariant((char)(word & 0xFF));
        if (character is ' ' or '+' or '=')
            _plus = true;
        if (character == '-')
            _minus = true;
        if (character is 'P' or '\r')
            _hit = count + 1;
        if (character is 'D' or '\x1B')
            _hit = count + 2;
        if (_plus || _minus)
            _hit = Cursor;
        if (character != '\0')
            return;

        switch (word)
        {
            case BiosKey.Up when Cursor > 1:
                Cursor--;
                break;
            case BiosKey.Down when count > Cursor:
                Cursor++;
                break;
            case BiosKey.Right when Cursor + 9 <= count:
                Cursor += 9;
                break;
            case BiosKey.Left when Cursor - 9 >= 1:
                Cursor -= 9;
                break;
        }
    }

    /// <summary>
    /// [ORIGINAL] O3:14B4–15C6: while a button is held, the tile under the mouse, "Purchase" or "Done" becomes the hit;
    /// the buttons are remembered. Coordinates: mouse driver x / 2 (UDBD:022A), y (UDBD:0256).
    /// </summary>
    private bool Click(ScreenInput input)
    {
        if (!input.MouseHolds(MouseButtons.Left) && !input.MouseHolds(MouseButtons.Right))
            return false;
        var x = input.MouseX / 2;
        var y = input.MouseY;
        _leftButton = input.MouseHolds(MouseButtons.Left);
        _rightButton = input.MouseHolds(MouseButtons.Right);
        for (var n = 1; n <= _tiles.Count; n++)
        {
            var tile = _tiles[n - 1];
            if (y >= tile.Y1 && y <= tile.Y2 && x <= tile.X2 && x >= tile.X1)
                _hit = n;
        }

        if (y is >= ButtonTop and <= ButtonBottom && x is >= PurchaseLeft and <= PurchaseRight)
            _hit = _tiles.Count + 1;
        if (y is >= ButtonTop and <= ButtonBottom && x is >= DoneLeft and <= DoneRight)
            _hit = _tiles.Count + 2;
        // [ORIGINAL] O3:15E3–15F6: a hit tile also takes the cursor (after the release; the order does not matter here).
        if (_hit > 0 && _hit <= _tiles.Count)
            Cursor = _hit;
        return true;
    }

    /// <summary>One pass of O3:1A46–23C2 after the input; returns true when the visit is over.</summary>
    private bool Act()
    {
        var previous = _cursorDrawn;
        if (previous == Cursor && _hit <= 0)
        {
            ResetFlags();
            return false;
        }

        // [ORIGINAL] O3:1A68–1AC3: erase the cursor box with the face colour.
        DrawCursorBox(previous, Face);
        var count = _tiles.Count;
        if (_hit < 1 || _hit > count + 2)
        {
            // [ORIGINAL] O3:1AE6 → O3:1217: no hit, only the cursor moved.
            ResetFlags();
            DrawCursor();
            return false;
        }

        if (_hit <= count)
        {
            var tile = _tiles[_hit - 1];
            if ((_minus || _leftButton) && _shop.Deselect(tile.Offer))
                DrawQuantity(tile);
            if ((_plus || _rightButton) && _shop.Select(tile.Offer))
                DrawQuantity(tile);
            SetPlusColors();
        }

        var done = false;
        if (_hit == count + 2)
        {
            // [ORIGINAL] O3:2058–206D: Done buys like Purchase and ends the loop.
            done = true;
            _hit = count + 1;
        }

        if (_hit == count + 1)
        {
            if (done)
            {
                _shop.Done();
                return true;
            }

            if (_shop.Purchase() != 0)
            {
                // [ORIGINAL] O3:2247–2280: restore the background, rebuild the tiles, keep the cursor in range.
                MenuGraphics.CopyRect(_background, _screen, 0, 38, 319, 199);
                BuildTiles();
                if (_tiles.Count < Cursor)
                    Cursor = _tiles.Count;
            }
        }

        DrawMoney();
        ResetFlags();
        DrawCursor();
        return false;
    }

    private int _cursorDrawn;

    private void ResetFlags()
    {
        _hit = 0;
        _minus = _plus = _leftButton = _rightButton = false;
    }

    /// <summary>
    /// [ORIGINAL] O3:121C–1382: the cursor box in colour 11, or 0 if the item is not affordable.
    /// </summary>
    private void DrawCursor()
    {
        if (_tiles.Count == 0)
            return;
        var tile = _tiles[Cursor - 1];
        DrawCursorBox(Cursor, _shop.CanAfford(tile.Offer) ? (byte)11 : (byte)0);
        _cursorDrawn = Cursor;
    }

    /// <summary>[ORIGINAL] UCC8:0031 at (x2 − 9, y1 + 3)–(x2 − 3, y2 − 3) of the tile.</summary>
    private void DrawCursorBox(int number, byte color)
    {
        if (number < 1 || number > _tiles.Count)
            return;
        var tile = _tiles[number - 1];
        MenuGraphics.Rectangle(_screen, tile.X2 - 9, tile.Y1 + 3, tile.X2 - 3, tile.Y2 - 3, color);
    }

    /// <summary>
    /// [ORIGINAL] O3:0F98–11CC (BOMB.OVR 0x09A24): header bar, player name, buttons and money.
    /// </summary>
    private void DrawHeader()
    {
        // EURO size 4, SetUserCharSize(1, 2, 1, 2), centred at the top.
        _text.SetTextStyle(_fonts.Euro, TextDirection.Horizontal, 4);
        _text.SetUserCharSize(1, 2, 1, 2);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        MenuGraphics.Frame(_screen, 5, 5, 314, 35, Face, Light, Dark);
        _text.Color = (byte)(PlayerColorBase + _shop.Account.Number);
        _text.OutTextXY(_screen, 160, 3, _shop.Account.Name);

        // [ORIGINAL] O3:1025–10C8: SmallFont size 4 (user size 1/2 stays), buttons "Purchase" and "Done" in colour 17.
        _text.SetTextStyle(_fonts.Small, TextDirection.Horizontal, 4);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        MenuGraphics.Frame(_screen, 10, 16, 59, 24, Face, Light, Dark);
        _text.Color = Dark;
        _text.OutTextXY(_screen, 11, 14, "Purchase");
        MenuGraphics.Frame(_screen, 284, 16, 310, 24, Face, Light, Dark);
        _text.Color = Dark;
        _text.OutTextXY(_screen, 285, 14, "Done");

        // [ORIGINAL] O3:10CD–11CC: TINY size 4; left in colour 6, right-aligned in colour 14.
        _text.SetTextStyle(_fonts.Tiny, TextDirection.Horizontal, 4);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.Color = 6;
        _text.OutTextXY(_screen, 11, 0, "YOU HAVE UNALLOCATED:");
        _text.OutTextXY(_screen, 11, 21, Dollars(_shop.Unallocated));
        _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Top);
        _text.Color = 14;
        _text.OutTextXY(_screen, 309, 0, "YOU CURRENTLY HAVE:");
        _text.OutTextXY(_screen, 309, 21, Dollars(_shop.CurrentlyHave));
        _shownUnallocated = _shop.Unallocated;
        _shownCurrently = _shop.CurrentlyHave;
    }

    /// <summary>
    /// [ORIGINAL] O3:2282–23B6: redraw a money line whose value changed – Bar in the face colour, then the text.
    /// </summary>
    private void DrawMoney()
    {
        _text.SetTextStyle(_fonts.Tiny, TextDirection.Horizontal, 4);
        if (_shownUnallocated != _shop.Unallocated)
        {
            _text.Color = 6;
            _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
            MenuGraphics.Bar(_screen, 11, 27, 80, 35, Face);
            _text.OutTextXY(_screen, 11, 21, Dollars(_shop.Unallocated));
            _shownUnallocated = _shop.Unallocated;
        }

        if (_shownCurrently != _shop.CurrentlyHave)
        {
            _text.Color = 14;
            _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Top);
            MenuGraphics.Bar(_screen, 309, 27, 239, 35, Face);
            _text.OutTextXY(_screen, 309, 21, Dollars(_shop.CurrentlyHave));
            _shownCurrently = _shop.CurrentlyHave;
        }
    }

    /// <summary>[ORIGINAL] O3:0D62: number followed by " DOLLARS" (Str of a LongInt, UDBD:02FE).</summary>
    public static string Dollars(int value) => Number(value) + " DOLLARS";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// [ORIGINAL] O3:015B (BOMB.OVR 0x08BE7): tiles in columns of nine, names, the "+", stock, "amount FOR price" and the
    /// sunken box.
    /// </summary>
    private void BuildTiles()
    {
        _tiles.Clear();
        int x = FirstTileX, y = FirstTileY;
        _text.SetTextStyle(_fonts.Small, TextDirection.Horizontal, 1);
        _text.SetUserCharSize(8, 8, 8, 8);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        foreach (var offer in _shop.Offers)
        {
            var tile = new Tile(offer, x, y, x + TileWidth, y + TileHeight);
            _tiles.Add(tile);
            MenuGraphics.Frame(_screen, tile.X1, tile.Y1, tile.X2, tile.Y2, Face, Light, Dark);
            _text.Color = NameColors[(int)offer.Item.Kind];
            _text.OutTextXY(_screen, (tile.X1 + tile.X2) / 2 - 7, tile.Y1 + 3, offer.Item.ShortName);
            y += RowPitch;
            if (y + TileHeight > LastBottom)
            {
                y = FirstTileY;
                x += ColumnPitch;
            }
        }

        // [ORIGINAL] O3:0692–085E: the "+" (UCC8:017D with radii 1, 1 and UCC8:0BB6 in the middle) in colour 100 + n.
        SetPlusColors();
        for (var n = 1; n <= _tiles.Count; n++)
        {
            var tile = _tiles[n - 1];
            Plus(tile.X2 - 40, tile.Y2 - 2, (byte)(PlusColorBase + n));
        }

        // [ORIGINAL] O3:0869–0A17: stock right-aligned at (x2 − 13, y2) in colour 14 (TINY 4, right/bottom), only above 0.
        _text.SetTextStyle(_fonts.Tiny, TextDirection.Horizontal, 4);
        _text.SetTextJustify(HorizontalJustify.Right, VerticalJustify.Bottom);
        _text.Color = 14;
        foreach (var tile in _tiles)
        {
            var stock = StockText(tile.Offer);
            if (stock is not null)
                _text.OutTextXY(_screen, tile.X2 - 13, tile.Y2, stock);
        }

        // [ORIGINAL] O3:0A22–0C8B: "amount FOR price" at (x1 + 2, y2) in colour 0 (TINY 4, left/bottom).
        _text.Color = 0;
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Bottom);
        foreach (var tile in _tiles)
            _text.OutTextXY(_screen, tile.X1 + 2, tile.Y2, OfferText(tile.Offer.Item));

        // [ORIGINAL] O3:0C96–0D30: sunken box (x2 − 9, y1 + 3)–(x2 − 3, y2 − 3), light and dark swapped.
        foreach (var tile in _tiles)
            MenuGraphics.Frame(_screen, tile.X2 - 9, tile.Y1 + 3, tile.X2 - 3, tile.Y2 - 3, Face, Dark, Light);
    }

    /// <summary>
    /// Stock text of a tile, or null if nothing is drawn. [ORIGINAL] O3:0897–09ED: the low word of the stock; the energy
    /// shield (defense 0) as stock / 10 and "%", the inertia dampener (defense 1) with "%", both capped at "100%"
    /// (above 1000 and 100); nothing for 0 or less.
    /// </summary>
    public static string? StockText(Item item, int stock)
    {
        var word = (short)stock;
        var text = Number(word);
        if (item.Kind == ItemKind.Defense && item.Index == 0)
        {
            text = Number(word / 10) + "%";
            if (word > 1000)
                text = "100%";
        }

        if (item.Kind == ItemKind.Defense && item.Index == 1)
        {
            text += "%";
            if (word > 100)
                text = "100%";
        }

        return word > 0 ? text : null;
    }

    private string? StockText(ShopOffer offer) => StockText(offer.Item, _shop.Account.Stock(offer.Item));

    /// <summary>
    /// [ORIGINAL] O3:0A7E–0C63: bundle amount (defense 0 and 1: "100%", set O3:0135) + " FOR " + price.
    /// </summary>
    public static string OfferText(Item item)
    {
        var amount = item.Kind == ItemKind.Defense && item.Index is 0 or 1 ? "100%" : Number((short)item.Amount);
        return amount + " FOR " + Number(item.Price);
    }

    /// <summary>
    /// [ORIGINAL] O3:06AD–07F2 and O3:1EB6–204D: DAC entry 100 + n is (63, 0, 0) while the item is affordable with the
    /// unallocated money, else (0, 0, 0).
    /// </summary>
    private void SetPlusColors()
    {
        for (var n = 1; n <= _tiles.Count; n++)
        {
            var red = _shop.CanAfford(_tiles[n - 1].Offer) ? 63 : 0;
            MenuGraphics.SetRgb(_dac, PlusColorBase + n, red, 0, 0);
        }
    }

    /// <summary>
    /// [ORIGINAL] O3:1BE5–1CEC / O3:1DC7–1EB1: clear the box with Bar in the face colour; a quantity above 0 in colour 6,
    /// TINY 4, centred at ((x2 − 13 + x2) / 2 + 1, (y1 + y2) / 2 − 2).
    /// </summary>
    private void DrawQuantity(Tile tile)
    {
        MenuGraphics.Bar(_screen, tile.X2 - 9, tile.Y1 + 3, tile.X2 - 3, tile.Y2 - 3, Face);
        if (tile.Offer.Quantity <= 0)
            return;
        _text.SetTextStyle(_fonts.Tiny, TextDirection.Horizontal, 4);
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.Color = 6;
        _text.OutTextXY(_screen, (tile.X2 - 13 + tile.X2) / 2 + 1, (tile.Y1 + tile.Y2) / 2 - 2,
            Number(tile.Offer.Quantity));
    }

    /// <summary>
    /// [APPROX] UCC8:017D draws an ellipse with the given radii (midpoint method, not traced in detail); with radii 1, 1
    /// it is taken as the four neighbours of the centre. The centre pixel itself is UCC8:0BB6 [ORIGINAL].
    /// </summary>
    private void Plus(int x, int y, byte color)
    {
        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var px = x + dx;
            var py = y + dy;
            if (px >= 0 && px < _screen.Width && py >= 0 && py < _screen.Height)
                _screen[px, py] = color;
        }
    }

    private sealed record Tile(ShopOffer Offer, int X1, int Y1, int X2, int Y2);
}
