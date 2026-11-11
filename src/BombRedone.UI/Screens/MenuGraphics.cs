using BombRedone.Formats;

namespace BombRedone.UI.Screens;

/// <summary>
/// Drawing routines of the original that the menus use, working directly on a 320 x 200 frame buffer like the original
/// on segment A000h (or on a save buffer). No clipping unless stated: the original writes the computed address.
/// </summary>
/// <remarks>
/// Addresses: UCC8 = segment 0CC8 of BOMB.EXE (file offset 0x0E320 + offset), U431 = segment 0431 (file offset 0x059B0
/// + offset); docs/analyse/re-grundlagen.md section 1.
/// </remarks>
public static class MenuGraphics
{
    /// <summary>[ORIGINAL] U431:01D8 (BOMB.EXE 0x05B88): palette entry of the dark frame grey (21, 21, 21).</summary>
    public const byte FrameDark = 17;

    /// <summary>[ORIGINAL] U431:01D8: palette entry of the medium grey (42, 42, 42), the face of buttons.</summary>
    public const byte FrameFace = 18;

    /// <summary>[ORIGINAL] U431:01D8: palette entry of white (63, 63, 63); also the colour of the menu marker.</summary>
    public const byte FrameWhite = 19;

    /// <summary>[ORIGINAL] UCC8:0015 (BOMB.EXE 0x0E335): row length of mode 13h, address = y * 320 + x.</summary>
    public const int Stride = 320;

    /// <summary>
    /// Line from (x1, y1) to (x2, y2) in <paramref name="color"/>, both ends included.
    /// [ORIGINAL] UCC8:09E4 (BOMB.EXE 0x0ED04): vertical and horizontal lines as runs, otherwise Bresenham with the
    /// error term 2·minor − major, starting at the left end; no clipping.
    /// </summary>
    public static void Line(Surface target, int x1, int y1, int x2, int y2, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);
        var pixels = target.Pixels;
        var dx = x2 - x1;
        if (dx == 0)
        {
            // 0x0ED6C: vertical run from the upper end.
            var top = Math.Min(y1, y2);
            var count = Math.Abs(y2 - y1) + 1;
            var address = Address(x1, top);
            for (var i = 0; i < count; i++, address += Stride)
                pixels[address] = color;
            return;
        }

        if (dx < 0)
        {
            dx = -dx;
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var rowStep = Stride;
        var dy = y2 - y1;
        if (dy == 0)
        {
            // 0x0ED94: horizontal run (rep stosb).
            pixels.AsSpan(Address(x1, y1), dx + 1).Fill(color);
            return;
        }

        if (dy < 0)
        {
            dy = -dy;
            rowStep = -Stride;
        }

        var yMajor = dy > dx;
        var (major, minor) = yMajor ? (dy, dx) : (dx, dy);
        var straight = 2 * minor;
        var error = 2 * minor - major;
        var diagonal = 2 * minor - 2 * major;
        var di = Address(x1, y1);
        for (var n = major + 1; n > 0; n--)
        {
            pixels[di++] = color;
            if (yMajor)
            {
                // 0x0EDC3: step a row after every pixel; the column advances only on a diagonal step.
                di += rowStep;
                if (error < 0)
                {
                    error += straight;
                    di--;
                }
                else
                {
                    error += diagonal;
                }
            }
            else if (error < 0)
            {
                error += straight;
            }
            else
            {
                error += diagonal;
                di += rowStep;
            }
        }
    }

    /// <summary>
    /// Outline of a rectangle. [ORIGINAL] UCC8:0031 (BOMB.EXE 0x0E351): four <see cref="Line"/> calls in the order right,
    /// left, bottom, top; works with swapped corners as well.
    /// </summary>
    public static void Rectangle(Surface target, int x1, int y1, int x2, int y2, byte color)
    {
        Line(target, x2, y1, x2, y2, color);
        Line(target, x1, y1, x1, y2, color);
        Line(target, x1, y2, x2, y2, color);
        Line(target, x1, y1, x2, y1, color);
    }

    /// <summary>
    /// Filled rectangle, corners included, like Graph.Bar with SolidFill. [APPROX] The BGI kernel and the VGA256 driver
    /// were not traced; inclusive filling is the documented behaviour of Bar, the menus only use ordered corners on the
    /// full-screen viewport.
    /// </summary>
    public static void Bar(Surface target, int x1, int y1, int x2, int y2, byte color)
    {
        ArgumentNullException.ThrowIfNull(target);
        var left = Math.Max(0, Math.Min(x1, x2));
        var right = Math.Min(target.Width - 1, Math.Max(x1, x2));
        var top = Math.Max(0, Math.Min(y1, y2));
        var bottom = Math.Min(target.Height - 1, Math.Max(y1, y2));
        for (var y = top; y <= bottom; y++)
            target.Pixels.AsSpan(y * target.Width + left, right - left + 1).Fill(color);
    }

    /// <summary>
    /// Button with a two-pixel bevel around the face (x1, y1)–(x2, y2).
    /// [ORIGINAL] U431:02A7 (BOMB.EXE 0x05C57): SetFillStyle(SolidFill, face), Bar, then with <paramref name="dark"/> the
    /// right edges x2+1, x2+2 and the bottom edges y2+1, y2+2, then with <paramref name="light"/> the left and top edges –
    /// the light edges are drawn last and win at the corners they share.
    /// </summary>
    public static void Frame(Surface target, int x1, int y1, int x2, int y2, byte face, byte light, byte dark)
    {
        Bar(target, x1, y1, x2, y2, face);
        Line(target, x2 + 1, y1 - 1, x2 + 1, y2 + 1, dark);
        Line(target, x2 + 2, y1 - 2, x2 + 2, y2 + 2, dark);
        Line(target, x1 - 1, y2 + 1, x2 + 1, y2 + 1, dark);
        Line(target, x1 - 2, y2 + 2, x2 + 2, y2 + 2, dark);
        Line(target, x1 - 1, y1 - 1, x1 - 1, y2 + 1, light);
        Line(target, x1 - 2, y1 - 2, x1 - 2, y2 + 2, light);
        Line(target, x1 - 1, y1 - 1, x2 + 1, y1 - 1, light);
        Line(target, x1 - 2, y1 - 2, x2 + 2, y1 - 2, light);
    }

    /// <summary>
    /// Copies the rectangle (x1, y1)–(x2, y2), corners included, from <paramref name="source"/> to the same place in
    /// <paramref name="target"/>. [ORIGINAL] UCC8:00CC (BOMB.EXE 0x0E3EC): corners are ordered first, then row by row.
    /// </summary>
    public static void CopyRect(Surface source, Surface target, int x1, int y1, int x2, int y2)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (x1 > x2)
            (x1, x2) = (x2, x1);
        if (y1 > y2)
            (y1, y2) = (y2, y1);
        for (var y = y1; y <= y2; y++)
        {
            var address = Address(x1, y);
            source.Pixels.AsSpan(address, x2 - x1 + 1).CopyTo(target.Pixels.AsSpan(address));
        }
    }

    /// <summary>
    /// Marker of the selected menu entry: a plus of five pixels in <see cref="FrameWhite"/>.
    /// [ORIGINAL] U431:03C3 (BOMB.EXE 0x05D73): drawn only if 0 &lt; x &lt; 320 and 0 &lt; y &lt; 199.
    /// </summary>
    public static void Marker(Surface target, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!MarkerVisible(x, y))
            return;
        foreach (var address in MarkerAddresses(x, y))
            target.Pixels[address] = FrameWhite;
    }

    /// <summary>
    /// Removes a <see cref="Marker"/> by copying its five pixels back from <paramref name="saved"/>.
    /// [ORIGINAL] U431:040F (BOMB.EXE 0x05DBF), same bounds as the marker.
    /// </summary>
    public static void EraseMarker(Surface target, Surface saved, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(saved);
        if (!MarkerVisible(x, y))
            return;
        foreach (var address in MarkerAddresses(x, y))
            target.Pixels[address] = saved.Pixels[address];
    }

    /// <summary>
    /// The 16 colours of the VGA default palette (6-bit values) that the menus use for text.
    /// [APPROX] Not from the original files: mode 13h of the VGA BIOS sets these EGA colours, and the title restores the
    /// palette that was active before it (anforderungen/titel.md). Whether the BGI driver changes them is not checked.
    /// </summary>
    public static ReadOnlySpan<byte> DefaultColors =>
    [
        0, 0, 0, 0, 0, 42, 0, 42, 0, 0, 42, 42, 42, 0, 0, 42, 0, 42, 42, 21, 0, 42, 42, 42,
        21, 21, 21, 21, 21, 63, 21, 63, 21, 21, 63, 63, 63, 21, 21, 63, 21, 63, 63, 63, 21, 63, 63, 63,
    ];

    /// <summary>
    /// Sets the grey ramp of the buttons. [ORIGINAL] U431:01D8: entries 17, 18, 19 = (21,21,21), (42,42,42),
    /// (63,63,63) through the palette routine at DS:E8F6.
    /// </summary>
    public static void SetFrameColors(Span<byte> dac)
    {
        SetRgb(dac, FrameDark, 21, 21, 21);
        SetRgb(dac, FrameFace, 42, 42, 42);
        SetRgb(dac, FrameWhite, 63, 63, 63);
    }

    /// <summary>Sets one DAC entry (6-bit values).</summary>
    public static void SetRgb(Span<byte> dac, int index, int red, int green, int blue)
    {
        dac[index * 3] = (byte)red;
        dac[index * 3 + 1] = (byte)green;
        dac[index * 3 + 2] = (byte)blue;
    }

    /// <summary>Converts a 6-bit DAC into the 32-bit palette of a <see cref="Surface"/>.</summary>
    public static void ApplyDac(ReadOnlySpan<byte> dac, Surface target)
    {
        ArgumentNullException.ThrowIfNull(target);
        for (var i = 0; i < Formats.Palette.Size; i++)
        {
            var r = Formats.Palette.Widen(dac[i * 3] & 0x3F);
            var g = Formats.Palette.Widen(dac[i * 3 + 1] & 0x3F);
            var b = Formats.Palette.Widen(dac[i * 3 + 2] & 0x3F);
            target.Palette[i] = 0xFF000000u | ((uint)b << 16) | ((uint)g << 8) | r;
        }
    }

    private static bool MarkerVisible(int x, int y) => x > 0 && x < 320 && y > 0 && y < 199;

    private static int[] MarkerAddresses(int x, int y)
    {
        var address = Address(x, y);
        return [address, address + Stride, address - Stride, address + 1, address - 1];
    }

    private static int Address(int x, int y) => y * Stride + x;
}
