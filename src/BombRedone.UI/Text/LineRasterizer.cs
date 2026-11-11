namespace BombRedone.UI.Text;

/// <summary>Draws one-pixel lines into a <see cref="Surface"/>, clipped to its bounds.</summary>
/// <remarks>
/// [APPROX] The original hands every stroke to the line function of the VGA256 driver (driver function 0Ch, called from
/// the Graph kernel at BOMB.EXE 0x134EB after clipping to the viewport at 0x134E6). Its pixel choice is not derived;
/// this is the symmetric Bresenham algorithm including both end points. Clipping per pixel to the surface stands in for
/// the viewport clipping (the program draws its texts with the full screen as viewport, not checked).
/// </remarks>
public static class LineRasterizer
{
    public static void Draw(Surface surface, int x0, int y0, int x1, int y1, byte color)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var dx = Math.Abs(x1 - x0);
        var dy = -Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            Plot(surface, x0, y0, color);
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

    /// <summary>Sets one pixel if it lies on the surface.</summary>
    public static void Plot(Surface surface, int x, int y, byte color)
    {
        if ((uint)x < (uint)surface.Width && (uint)y < (uint)surface.Height)
            surface.Pixels[y * surface.Width + x] = color;
    }
}
