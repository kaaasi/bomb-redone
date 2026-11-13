using System.Globalization;
using BombRedone.Core.Graphics;
using BombRedone.Core.Playfield;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// Playfield at the start of a round: the pixels of <see cref="Playfield"/> plus what the original draws with BGI text
/// and the header (docs/analyse/runde.md sections 3.2, 3.6, 6.1; docs/analyse/feld.md section 2). Drawing never changes
/// the playfield.
/// </summary>
/// <remarks>
/// Order as in the original: O8:0984 (wall label, per player the name in the header), O4:45F1 (header texts and wind),
/// O8:1268 (weapon bar). Checked against the probe at messpunkte A and K (PlayfieldScreenOriginalTests).
/// [APPROX] DAC entries the round does not set keep the values of earlier screens in the original; here the 16 colours of
/// the VGA default palette (<see cref="MenuGraphics.DefaultColors"/>: terrain 2 green, frame 13 magenta, as in the probe),
/// the rest 0.
/// </remarks>
public sealed class PlayfieldScreen : IScreen
{
    /// <summary>[ORIGINAL] O8:0AFD: colour of the wall label.</summary>
    public const byte WallLabelColor = 6;

    /// <summary>[ORIGINAL] O4:45F1: colour of the header labels; O4:4691 colour of the wind.</summary>
    public const byte HeaderColor = 5, WindColor = 9;

    /// <summary>[ORIGINAL] O8:1268: weapon i (0..17, 18 = Herb, 19 = Vick) is drawn in colour 100 + i.</summary>
    public const int WeaponColorBase = 100, WeaponFields = 20;

    /// <summary>[ORIGINAL] Character for the wind direction: DS:0064 (27, ←) for wind &lt; 0, DS:0066 (26, →) otherwise.</summary>
    public const byte WindLeft = 27, WindRight = 26;

    private readonly Surface _screen = new();
    private readonly byte[] _dac = new byte[Formats.Palette.ByteLength];
    private readonly bool[] _dacWritten = new bool[Surface.PaletteSize];
    private readonly Func<IScreen?>? _next;

    /// <param name="fonts">SmallFont (wall label) and DefaultFont (names, header).</param>
    /// <param name="field">The playfield of the round.</param>
    /// <param name="wind">Wind of the round (header).</param>
    /// <param name="names">Names of players 1..N.</param>
    /// <param name="next">
    /// Called on a key press; returns the next screen. [APPROX] Preview only (Desktop): the turns of the round follow
    /// later (runde.md section 6); without it the screen stays.
    /// </param>
    public PlayfieldScreen(MenuFonts fonts, Playfield field, Wind wind, IReadOnlyList<string> names,
        Func<IScreen?>? next = null)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count != field.Tanks.Count)
            throw new ArgumentException("One name per player.", nameof(names));
        Field = field;
        _next = next;
        field.Screen.Pixels.CopyTo(_screen.Pixels, 0);
        MenuGraphics.DefaultColors.CopyTo(_dac);
        for (var i = 0; i < Surface.PaletteSize; i++)
        {
            _dacWritten[i] = field.Palette.IsWritten(i);
            if (_dacWritten[i])
                field.Palette.Rgb.Slice(3 * i, 3).CopyTo(_dac.AsSpan(3 * i));
        }

        var text = new GraphText(fonts.Default);
        DrawSetupTexts(_screen, text, fonts, field, names);
        AfterSetup = Snapshot();
        DrawHeader(_screen, text, wind);
        DrawWeaponBar(_screen, text);
        MenuGraphics.ApplyDac(_dac, _screen);
    }

    public Playfield Field { get; }

    /// <summary>Screen after O8:0984 (messpunkt A): playfield plus wall label and the last name.</summary>
    public Surface AfterSetup { get; }

    /// <summary>The full screen at the start of the round (messpunkt K).</summary>
    public Surface Screen => _screen;

    /// <summary>6-bit DAC (768 bytes).</summary>
    public ReadOnlySpan<byte> Dac => _dac;

    /// <summary>DAC entries written by the round, ascending as index, R, G, B (probe dump "dac_set").</summary>
    public byte[] WrittenDacEntries()
    {
        var result = new List<byte>();
        for (var i = 0; i < Surface.PaletteSize; i++)
        {
            if (_dacWritten[i])
                result.AddRange([(byte)i, _dac[3 * i], _dac[3 * i + 1], _dac[3 * i + 2]]);
        }

        return [.. result];
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input) =>
        _next is not null && input.KeyPressed ? _next() : this;

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != _screen.Width || surface.Height != _screen.Height)
            throw new ArgumentException("The playfield draws into a 320 x 200 surface only.", nameof(surface));
        _screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    /// <summary>[ORIGINAL] Texts of the wall label O8:0924–093F (BOMB.OVR 0x1C9E4): none for None.</summary>
    public static string WallLabel(WallKind wall) => wall switch
    {
        WallKind.Sticky => "STICKEY",
        WallKind.Elastic => "ELASTIC",
        WallKind.Accelerate => "ACCELERATE",
        WallKind.Warp => "WARP",
        _ => "",
    };

    /// <summary>
    /// [ORIGINAL] O8:0AE3–0BBF: SmallFont, vertical, SetUserCharSize(1, 1, 1, 1), centred at (0, 100) in colour 6;
    /// O8:0FF1–104B per player: SetFillStyle(SolidFill, 0), Bar (109, 0)–(211, 7), name in colour 19 + n centred at the
    /// top at (160, 0) in the DefaultFont (O4:44B4 SetTextStyle(DefaultFont, HorizDir, 1)).
    /// </summary>
    private static void DrawSetupTexts(Surface screen, GraphText text, MenuFonts fonts, Playfield field,
        IReadOnlyList<string> names)
    {
        var label = WallLabel(field.Wall);
        text.SetTextStyle(fonts.Small, TextDirection.Vertical, 1);
        text.SetUserCharSize(1, 1, 1, 1);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        text.Color = WallLabelColor;
        if (label.Length > 0)
            text.OutTextXY(screen, 0, 100, label);

        text.SetTextStyle(null, TextDirection.Horizontal, 1);
        foreach (var tank in field.Tanks)
        {
            MenuGraphics.Bar(screen, 109, 0, 211, 7, 0);
            text.Color = tank.Color;
            text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
            text.OutTextXY(screen, 160, 0, names[tank.Number - 1]);
        }
    }

    private Surface Snapshot()
    {
        var copy = new Surface();
        _screen.Pixels.CopyTo(copy.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, copy);
        return copy;
    }

    /// <summary>
    /// [ORIGINAL] O4:45F1–46FA: colour 5, left/top: "Angle:" (8, 0), "Power:" (8, 10), "Direction( )" (216, 0), "Men:"
    /// (216, 10); colour 9, centred/top at (160, 10): "Wind: " + |wind| + " " + arrow.
    /// </summary>
    private static void DrawHeader(Surface screen, GraphText text, Wind wind)
    {
        text.Color = HeaderColor;
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        text.OutTextXY(screen, 8, 0, "Angle:");
        text.OutTextXY(screen, 8, 10, "Power:");
        text.OutTextXY(screen, 216, 0, "Direction( )");
        text.OutTextXY(screen, 216, 10, "Men:");
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        text.Color = WindColor;
        var line = new List<byte>(GraphText.ToBytes(
            "Wind: " + Math.Abs(wind.Value).ToString(CultureInfo.InvariantCulture) + " "));
        line.Add(wind.Value < 0 ? WindLeft : WindRight);
        text.OutTextXY(screen, 160, 10, line.ToArray());
    }

    /// <summary>
    /// [ORIGINAL] O8:1268 (BOMB.OVR 0x1D328): per field i = 0..19 DAC 100 + i := black, viewport (15 i + 10, 18)–(15 i + 25,
    /// 27) with clipping, Rectangle (0, 0)–(14, 10) and the picture of the weapon in colour 100 + i (O8:131C–1EE9, see
    /// <see cref="WeaponPictures"/>); the bottom edge of the rectangle lies outside the viewport.
    /// </summary>
    private void DrawWeaponBar(Surface screen, GraphText text)
    {
        for (var i = 0; i < WeaponFields; i++)
        {
            var color = (byte)(WeaponColorBase + i);
            MenuGraphics.SetRgb(_dac, color, 0, 0, 0);
            _dacWritten[color] = true;
            var cell = new Cell(screen, 15 * i + 10, 18, 15 * i + 25, 27, color);
            cell.Line(0, 0, 14, 0);
            cell.Line(14, 0, 14, 10);
            cell.Line(14, 10, 0, 10);
            cell.Line(0, 10, 0, 0);
            WeaponPictures.Draw(i, cell, text);
        }
    }

    /// <summary>A clipped BGI viewport of one field of the weapon bar.</summary>
    internal readonly struct Cell(Surface screen, int x1, int y1, int x2, int y2, byte color)
    {
        public byte Color => color;

        public void PutPixel(int x, int y)
        {
            if (Inside(x, y))
                screen[x1 + x, y1 + y] = color;
        }

        /// <summary>BGI Line relative to the viewport; [APPROX] clipped per pixel (all lines of the bar are straight or
        /// diagonal, for which geometric clipping keeps the same pixels).</summary>
        public void Line(int xa, int ya, int xb, int yb)
        {
            var dx = Math.Abs(xb - xa);
            var dy = -Math.Abs(yb - ya);
            var sx = xa < xb ? 1 : -1;
            var sy = ya < yb ? 1 : -1;
            var error = dx + dy;
            while (true)
            {
                PutPixel(xa, ya);
                if (xa == xb && ya == yb)
                    return;
                var twice = 2 * error;
                if (twice >= dy)
                {
                    error += dy;
                    xa += sx;
                }

                if (twice <= dx)
                {
                    error += dx;
                    ya += sy;
                }
            }
        }

        /// <summary>Bar with SolidFill in the field colour, corners included.</summary>
        public void Bar(int xa, int ya, int xb, int yb)
        {
            for (var y = Math.Min(ya, yb); y <= Math.Max(ya, yb); y++)
            {
                for (var x = Math.Min(xa, xb); x <= Math.Max(xa, xb); x++)
                    PutPixel(x, y);
            }
        }

        /// <summary>
        /// Circle with radius 1. [ORIGINAL] measured with <c>feld.py call "0E26:11EA(50,50,1)"</c>: the four neighbours
        /// of the centre (BGI circles of other radii are not derived).
        /// </summary>
        public void CircleRadius1(int x, int y)
        {
            PutPixel(x, y - 1);
            PutPixel(x - 1, y);
            PutPixel(x + 1, y);
            PutPixel(x, y + 1);
        }

        public void Text(GraphText text, int x, int y, string s)
        {
            text.Color = color;
            text.OutTextXY(screen, x1 + x, y1 + y, s);
        }

        private bool Inside(int x, int y) => x >= 0 && y >= 0 && x <= x2 - x1 && y <= y2 - y1;
    }
}
