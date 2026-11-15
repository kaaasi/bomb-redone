namespace BombRedone.Core.Graphics;

/// <summary>
/// Drawing of the Graph unit with the linked VGA256 2.00 driver (BOMB.EXE 0x7EB7), as far as derived. Unlike
/// <see cref="VgaDraw"/> these routines take the buffer size, because the UI also draws them into surfaces of other sizes.
/// </summary>
public static class BgiDraw
{
    /// <summary>
    /// Line from (x0, y0) to (x1, y1) in <paramref name="color"/>, both ends included, pixels outside the
    /// <paramref name="width"/> × <paramref name="height"/> buffer dropped.
    /// </summary>
    /// <remarks>
    /// [ORIGINAL] for lines inside the viewport: Graph Line (0E26:1C86) through driver function 0Ch gives the pixels of the
    /// symmetric Bresenham algorithm from (x0, y0); checked against the original with <c>feld.py call</c> on 1,000+
    /// random lines (docs/analyse/feld.md section 3a, test BgiDrawTests). [APPROX] Lines leaving the viewport: Graph clips
    /// the line geometrically before the driver (BOMB.EXE 0x134E6), which can move the pixels of the remaining part;
    /// here each pixel is dropped instead.
    /// </remarks>
    public static void Line(Span<byte> pixels, int width, int height, int x0, int y0, int x1, int y1, byte color)
    {
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            Plot(pixels, width, height, x0, y0, color);
            if (x0 == x1 && y0 == y1)
                return;
            var twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twice <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    /// <summary>Aspect ratio of the VGA256 driver after InitGraph (Graph DS:EBDA / DS:EBDC).</summary>
    public const int DriverXAspect = 8500;

    /// <summary>See <see cref="DriverXAspect"/>.</summary>
    public const int DriverYAspect = 10000;

    /// <summary>
    /// Graph Circle(x, y, radius) in <paramref name="color"/> with the aspect ratio set by SetAspectRatio, coordinates
    /// relative to the viewport; with <see cref="Viewport.Clip"/> every point outside the viewport is dropped.
    /// </summary>
    /// <remarks>
    /// [ORIGINAL] Graph Circle 0E26:11EA: y radius = radius · xAspect div yAspect (0E26:11B7, long multiply and divide), then the
    /// arc 0..360° through driver function 14h, which VGA256 2.00 leaves to the emulation of the Graph unit (dispatcher
    /// BOMB.EXE 0x11EEB, arc 0x12346). A closed arc ends in the integer ellipse at BOMB.EXE 0x12913: both radii at least
    /// 1, S = max(rx, ry)² · 100, Kx = S div rx div rx, Ky = S div ry div ry, four symmetric points per step, first
    /// x-major while the x term stays below the y term, then y-major down to y = 0. Its angle test (pseudo angle
    /// BOMB.EXE 0x12B43) lets every point through for start = end. Measured against the original with the probe for
    /// radius 0..63 at two centres with the driver aspect and in the clipped playfield viewport (docs/analyse/feld.md
    /// section 8.2).
    /// [APPROX] Unclipped points outside the buffer are dropped; the overflow checks of the original (S above 32 bits,
    /// radius above 6553) end the circle without drawing.
    /// </remarks>
    public static void Circle(Span<byte> pixels, int width, int height, Viewport viewport, int x, int y, int radius,
        byte color, int xAspect, int yAspect)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(yAspect);
        var rx = (int)(ushort)radius;
        var ry = (int)((uint)rx * (uint)xAspect / (uint)yAspect);
        if (rx == 0)
            rx = 1;
        if (ry == 0)
            ry = 1;
        var m = (ulong)Math.Max(rx, ry);
        if (m * m * 100 > uint.MaxValue)
            return;
        var s = (long)(m * m * 100);
        long kx = s / rx / rx;
        long ky = s / ry / ry;
        var d = ky * ry * ry - s;
        var f = 2 * ky * ry;
        var i = 0;
        var j = ry;
        long e = 0;
        while (true)
        {
            Plot4(pixels, width, height, viewport, x, y, i, j, color);
            var g = e + kx;
            var h = f - ky;
            if (2 * d + 2 * g >= h)
            {
                j--;
                d -= h;
                f = h - ky;
            }

            i++;
            d += g;
            e = g + kx;
            if (e >= f)
                break;
        }

        while (true)
        {
            Plot4(pixels, width, height, viewport, x, y, i, j, color);
            var g = e + kx;
            var h = f - ky;
            if ((g >> 1) + d <= h)
            {
                i++;
                d += g;
                e = g + kx;
            }

            j--;
            d -= h;
            f = h - ky;
            if (j < 0)
                return;
        }
    }

    /// <summary>Graph PutPixel: like <see cref="Plot"/>, relative to the viewport and clipped by it (driver function 30h).</summary>
    public static void PutPixel(Span<byte> pixels, int width, int height, Viewport viewport, int x, int y, byte color)
    {
        if (viewport.Clip && ((uint)x > (uint)(viewport.X2 - viewport.X1) || (uint)y > (uint)(viewport.Y2 - viewport.Y1)))
            return;
        Plot(pixels, width, height, viewport.X1 + x, viewport.Y1 + y, color);
    }

    // Points (i, j), (−i, j), (i, −j), (−i, −j) in this order (BOMB.EXE 0x12B01).
    private static void Plot4(Span<byte> pixels, int width, int height, Viewport viewport, int x, int y, int i, int j,
        byte color)
    {
        PutPixel(pixels, width, height, viewport, x + i, y + j, color);
        PutPixel(pixels, width, height, viewport, x - i, y + j, color);
        PutPixel(pixels, width, height, viewport, x + i, y - j, color);
        PutPixel(pixels, width, height, viewport, x - i, y - j, color);
    }

    /// <summary>Sets one pixel if it lies in the buffer.</summary>
    public static void Plot(Span<byte> pixels, int width, int height, int x, int y, byte color)
    {
        if ((uint)x < (uint)width && (uint)y < (uint)height)
            pixels[y * width + x] = color;
    }
}
