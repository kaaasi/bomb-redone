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

    /// <summary>Sets one pixel if it lies in the buffer.</summary>
    public static void Plot(Span<byte> pixels, int width, int height, int x, int y, byte color)
    {
        if ((uint)x < (uint)width && (uint)y < (uint)height)
            pixels[y * width + x] = color;
    }
}
