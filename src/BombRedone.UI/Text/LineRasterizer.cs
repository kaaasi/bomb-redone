using BombRedone.Core.Graphics;

namespace BombRedone.UI.Text;

/// <summary>Draws one-pixel lines into a <see cref="Surface"/>, clipped to its bounds.</summary>
/// <remarks>
/// The original hands every stroke to the line function of the VGA256 driver (driver function 0Ch, called from the Graph
/// kernel at BOMB.EXE 0x134EB after clipping to the viewport at 0x134E6). The pixel choice is <see cref="BgiDraw.Line"/>
/// ([ORIGINAL] for lines inside the viewport, checked against the probe). [APPROX] Clipping per pixel to the surface stands
/// in for the viewport clipping (the program draws its texts with the full screen as viewport, not checked).
/// </remarks>
public static class LineRasterizer
{
    public static void Draw(Surface surface, int x0, int y0, int x1, int y1, byte color)
    {
        ArgumentNullException.ThrowIfNull(surface);
        BgiDraw.Line(surface.Pixels, surface.Width, surface.Height, x0, y0, x1, y1, color);
    }

    /// <summary>Sets one pixel if it lies on the surface.</summary>
    public static void Plot(Surface surface, int x, int y, byte color)
    {
        ArgumentNullException.ThrowIfNull(surface);
        BgiDraw.Plot(surface.Pixels, surface.Width, surface.Height, x, y, color);
    }
}
