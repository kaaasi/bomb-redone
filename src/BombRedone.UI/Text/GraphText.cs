using BombRedone.Formats;

namespace BombRedone.UI.Text;

/// <summary>
/// Text output with the semantics of the Turbo Pascal Graph unit as linked into BOMB.EXE: SetColor, SetTextStyle,
/// SetUserCharSize, SetTextJustify, OutTextXY, TextWidth and TextHeight. Holds the text state like the Graph kernel.
/// </summary>
/// <remarks>
/// All "BOMB.EXE 0x..." references point into the BGI kernel of the Graph unit (unit 0E26). Its code addresses its own
/// variables relative to the file offset 0x11B23 (kernel offset 14F0h = BOMB.EXE 0x13013); docs/formate/schriften.md.
/// The procedure names are inferred from the argument patterns (docs/analyse/re-grundlagen.md section 4).
/// Strings are byte strings (code page of the original); <see cref="string"/> overloads map chars 0-255 one to one.
/// </remarks>
public sealed class GraphText
{
    /// <summary>
    /// [ORIGINAL] Scale factors of the stroked fonts by character size, (multiplier, divisor) for sizes 0 to 10;
    /// table at BOMB.EXE 0x13013, read at 0x13032 after limiting the size to 10. Size 0 selects SetUserCharSize.
    /// </summary>
    public static readonly IReadOnlyList<(int Multiplier, int Divisor)> StrokedScale =
    [
        (0, 0), (3, 5), (2, 3), (3, 4), (1, 1), (4, 3), (5, 3), (2, 1), (5, 2), (3, 1), (4, 1),
    ];

    /// <summary>Largest character size; larger values count as this one (BOMB.EXE 0x13036).</summary>
    public const int MaxCharSize = 10;

    private readonly BitmapFont? _defaultFont;
    private BgiFont? _font;
    private int _multX = 1, _divX = 1, _multY = 1, _divY = 1;
    private int _userMultX = 1, _userDivX = 1, _userMultY = 1, _userDivY = 1;

    /// <param name="defaultFont">
    /// 8 x 8 font for the DefaultFont; may be null if only stroked fonts are drawn (the ROM font is not part of the
    /// original files, see <see cref="BitmapFont"/>).
    /// </param>
    public GraphText(BitmapFont? defaultFont = null)
    {
        _defaultFont = defaultFont;
        SetTextStyle(null, TextDirection.Horizontal, 1);
    }

    /// <summary>Current drawing color (SetColor), a palette index.</summary>
    public byte Color { get; set; } = 15;

    /// <summary>Current stroked font, or null for the DefaultFont.</summary>
    public BgiFont? Font => _font;

    public TextDirection Direction { get; private set; }

    public int CharSize { get; private set; }

    public HorizontalJustify Horizontal { get; private set; } = HorizontalJustify.Left;

    public VerticalJustify Vertical { get; private set; } = VerticalJustify.Top;

    /// <summary>Effective scale of stroked fonts (multiplier and divisor per axis).</summary>
    public (int MultX, int DivX, int MultY, int DivY) Scale => (_multX, _divX, _multY, _divY);

    /// <summary>SetTextStyle: font (null = DefaultFont), direction and character size.</summary>
    public void SetTextStyle(BgiFont? font, TextDirection direction, int charSize)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        ArgumentOutOfRangeException.ThrowIfNegative(charSize);
        _font = font;
        Direction = direction;
        CharSize = charSize;
        ApplyScale();
    }

    /// <summary>
    /// SetUserCharSize: own scale of stroked fonts, effective while the character size is 0.
    /// [ORIGINAL] BOMB.EXE 0x13158: stores the four values and sets the size to 0; a divisor 0 counts as 1 (0x13184,
    /// 0x1318F).
    /// </summary>
    public void SetUserCharSize(int multX, int divX, int multY, int divY)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(multX);
        ArgumentOutOfRangeException.ThrowIfNegative(multY);
        ArgumentOutOfRangeException.ThrowIfNegative(divX);
        ArgumentOutOfRangeException.ThrowIfNegative(divY);
        _userMultX = multX;
        _userDivX = divX == 0 ? 1 : divX;
        _userMultY = multY;
        _userDivY = divY == 0 ? 1 : divY;
        CharSize = 0;
        ApplyScale();
    }

    /// <summary>SetTextJustify.</summary>
    public void SetTextJustify(HorizontalJustify horizontal, VerticalJustify vertical)
    {
        if (!Enum.IsDefined(horizontal))
            throw new ArgumentOutOfRangeException(nameof(horizontal));
        if (!Enum.IsDefined(vertical))
            throw new ArgumentOutOfRangeException(nameof(vertical));
        Horizontal = horizontal;
        Vertical = vertical;
    }

    public int TextWidth(string text) => TextWidth(ToBytes(text));

    /// <summary>
    /// TextWidth. Stroked: [ORIGINAL] BOMB.EXE 0x13242: sum of the width table entries of all characters in the font
    /// (others are skipped, a NUL does not end the sum), then scaled once with the x factor.
    /// </summary>
    public int TextWidth(ReadOnlySpan<byte> text)
    {
        if (_font is null)
            return text.Length * BitmapCellWidth;
        var sum = 0;
        foreach (var code in text)
            sum += _font.GetGlyph(code)?.Width ?? 0;
        return (sum & 0xFFFF) * _multX / _divX;
    }

    public int TextHeight(string text) => TextHeight(ToBytes(text));

    /// <summary>
    /// TextHeight. Stroked: [ORIGINAL] BOMB.EXE 0x13214: (top - bottom) of the font header scaled with the y factor;
    /// independent of the text.
    /// </summary>
    public int TextHeight(ReadOnlySpan<byte> text)
    {
        if (_font is null)
            return BitmapCellHeight;
        return ((_font.Top - _font.Bottom) & 0xFF) * _multY / _divY;
    }

    public void OutTextXY(Surface surface, int x, int y, string text) => OutTextXY(surface, x, y, ToBytes(text));

    /// <summary>OutTextXY: draws the text at (x, y) according to font, direction, justification and color.</summary>
    public void OutTextXY(Surface surface, int x, int y, ReadOnlySpan<byte> text)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (_font is null)
            DrawBitmap(surface, x, y, text);
        else
            DrawStroked(surface, _font, x, y, text);
    }

    /// <summary>Maps chars 0-255 to bytes; other chars become '?'.</summary>
    public static byte[] ToBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
            bytes[i] = text[i] <= 0xFF ? (byte)text[i] : (byte)'?';
        return bytes;
    }

    // [APPROX] Cell size of the DefaultFont: the kernel asks the driver (function 24h, BOMB.EXE 0x13003) and keeps the
    // answer; 8 * size is assumed (driver status table: 8 x 8 character cell at BOMB.EXE 0x08023).
    private int BitmapCellWidth => BitmapFont.CellSize * Math.Max(1, CharSize);

    private int BitmapCellHeight => BitmapFont.CellSize * Math.Max(1, CharSize);

    private void ApplyScale()
    {
        var (mult, div) = StrokedScale[Math.Min(CharSize, MaxCharSize)];
        if (mult == 0)
        {
            // [ORIGINAL] BOMB.EXE 0x13171: size 0 takes the values of SetUserCharSize.
            (_multX, _divX, _multY, _divY) = (_userMultX, _userDivX, _userMultY, _userDivY);
            return;
        }

        // [ORIGINAL] BOMB.EXE 0x13195: both axes get the table factor.
        // [APPROX] The kernel then corrects the x factor of horizontal text by 10000 / kernel word 81h if that word is
        // below 10000 (0x131AC). The word is 10000 in the file (BOMB.EXE 0x11BA4) and no write to it was found, so no
        // correction is applied.
        (_multX, _divX, _multY, _divY) = (mult, div, mult, div);
    }

    private void DrawStroked(Surface surface, BgiFont font, int x, int y, ReadOnlySpan<byte> text)
    {
        var width = TextWidth(text);
        var height = TextHeight(text);
        var vertical = Direction == TextDirection.Vertical;

        // [ORIGINAL] BOMB.EXE 0x13445: horizontal justification; vertical text first moves right by the height.
        var amount = width;
        if (vertical)
        {
            amount = height;
            x += height;
        }

        if (Horizontal == HorizontalJustify.Center)
            x -= amount / 2 - 1;
        else if (Horizontal == HorizontalJustify.Right)
            x -= amount;

        // [ORIGINAL] BOMB.EXE 0x13478: vertical justification moves the origin (baseline) down.
        amount = vertical ? width : height;
        if (Vertical == VerticalJustify.Center)
            y += amount / 2;
        else if (Vertical == VerticalJustify.Top)
            y += amount;

        foreach (var code in text)
        {
            // [ORIGINAL] BOMB.EXE 0x1341C: a NUL ends the output.
            if (code == 0)
                return;
            var glyph = font.GetGlyph(code);
            if (glyph is null)
                continue;
            DrawGlyph(surface, glyph, ref x, ref y, vertical);
        }
    }

    /// <summary>
    /// [ORIGINAL] BOMB.EXE 0x13289: the pen starts at the origin; Move and Draw scale each point on its own
    /// (0x133BE: multiply, then divide with truncation towards zero), Draw draws a line from the previous scaled point
    /// (0x134B1, y upwards). The origin advances by the last scaled point (0x132D9), not by the width table.
    /// </summary>
    private void DrawGlyph(Surface surface, BgiGlyph glyph, ref int originX, ref int originY, bool vertical)
    {
        int penX = 0, penY = 0;
        foreach (var stroke in glyph.Strokes)
        {
            if (stroke.Op == BgiStrokeOp.Scan)
                continue;
            var nextX = stroke.X * _multX / _divX;
            var nextY = stroke.Y * _multY / _divY;
            if (stroke.Op == BgiStrokeOp.Draw)
            {
                if (vertical)
                    LineRasterizer.Draw(surface, originX - penY, originY - penX, originX - nextY, originY - nextX, Color);
                else
                    LineRasterizer.Draw(surface, originX + penX, originY - penY, originX + nextX, originY - nextY, Color);
            }

            penX = nextX;
            penY = nextY;
        }

        if (vertical)
        {
            originX += penY;
            originY -= penX;
        }
        else
        {
            originX += penX;
            originY -= penY;
        }
    }

    private void DrawBitmap(Surface surface, int x, int y, ReadOnlySpan<byte> text)
    {
        if (_defaultFont is null)
            throw new InvalidOperationException("No 8 x 8 font given for the DefaultFont.");
        var cellWidth = BitmapCellWidth;
        var cellHeight = BitmapCellHeight;
        var vertical = Direction == TextDirection.Vertical;

        // [ORIGINAL] BOMB.EXE 0x130AE: horizontal justification of the DefaultFont. Vertical text moves left by the
        // cell height for every justification except Center (cell height / 2 - 1).
        int amount;
        if (vertical)
            amount = cellHeight;
        else
            amount = Horizontal == HorizontalJustify.Left ? 0 : text.Length * cellWidth;
        if (Horizontal == HorizontalJustify.Center)
            amount = amount / 2 - 1;
        x -= amount;

        // [ORIGINAL] BOMB.EXE 0x130D5: vertical justification; (x, y) is the top left corner after it.
        amount = vertical ? text.Length * cellWidth : cellHeight;
        if (Vertical == VerticalJustify.Center)
            y -= amount / 2;
        else if (Vertical == VerticalJustify.Bottom)
            y -= amount;

        // [ORIGINAL] BOMB.EXE 0x1307D: with the DefaultFont nothing is drawn if x is negative.
        if (x < 0)
            return;

        // [APPROX] Drawing itself is driver function 26h (VGA256); assumed: set pixels become size x size blocks,
        // background untouched; vertical text is the horizontal cell rotated counterclockwise, running upwards from the
        // bottom left corner (x, y + length * cell width - 1). The program uses the DefaultFont only horizontally.
        var size = cellWidth / BitmapFont.CellSize;
        for (var i = 0; i < text.Length; i++)
        {
            var rows = _defaultFont.GetRows(text[i]);
            for (var row = 0; row < BitmapFont.CellSize; row++)
            for (var column = 0; column < BitmapFont.CellSize; column++)
            {
                if ((rows[row] & (0x80 >> column)) == 0)
                    continue;
                int left, top;
                if (vertical)
                {
                    left = x + row * size;
                    top = y + (text.Length - i) * cellWidth - (column + 1) * size;
                }
                else
                {
                    left = x + i * cellWidth + column * size;
                    top = y + row * size;
                }

                for (var dy = 0; dy < size; dy++)
                for (var dx = 0; dx < size; dx++)
                    LineRasterizer.Plot(surface, left + dx, top + dy, Color);
            }
        }
    }
}
