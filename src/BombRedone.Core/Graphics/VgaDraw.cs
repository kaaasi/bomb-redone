namespace BombRedone.Core.Graphics;

/// <summary>
/// Drawing routines of the original that write straight into a mode 13h buffer (segment A000h or a save buffer),
/// shared by the playfield (Core) and the menus (UI). No clipping unless stated: the original writes the computed
/// address. Pixels are a span of at least 64000 bytes with 320 bytes per row.
/// </summary>
/// <remarks>
/// Addresses: UCC8 = segment 0CC8 of BOMB.EXE (file offset 0x0E320 + offset), U431 = segment 0431 (file offset 0x059B0
/// + offset); docs/analyse/re-grundlagen.md section 1. Checked against the original routines with
/// <c>python3 tools/reverse-engineering/feld.py call</c> (docs/analyse/feld.md).
/// </remarks>
public static class VgaDraw
{
    /// <summary>[ORIGINAL] UCC8:0015 (BOMB.EXE 0x0E335): row length of mode 13h, address = y * 320 + x.</summary>
    public const int Stride = IndexBuffer.Width;

    /// <summary>Address of (x, y) in mode 13h.</summary>
    public static int Address(int x, int y) => y * Stride + x;

    /// <summary>
    /// Line from (x1, y1) to (x2, y2) in <paramref name="color"/>, both ends included.
    /// [ORIGINAL] UCC8:09E4 (BOMB.EXE 0x0ED04): vertical and horizontal lines as runs, otherwise Bresenham with the
    /// error term 2·minor − major, starting at the left end; no clipping.
    /// </summary>
    public static void Line(Span<byte> pixels, int x1, int y1, int x2, int y2, byte color)
    {
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
            pixels.Slice(Address(x1, y1), dx + 1).Fill(color);
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
    public static void Rectangle(Span<byte> pixels, int x1, int y1, int x2, int y2, byte color)
    {
        Line(pixels, x2, y1, x2, y2, color);
        Line(pixels, x1, y1, x1, y2, color);
        Line(pixels, x1, y2, x2, y2, color);
        Line(pixels, x1, y1, x2, y1, color);
    }

    /// <summary>One pixel. [ORIGINAL] UCC8:0BB6 (BOMB.EXE 0x0EED6): byte at y * 320 + x, no clipping.</summary>
    public static void PutPixel(Span<byte> pixels, int x, int y, byte color) => pixels[Address(x, y)] = color;

    /// <summary>
    /// Body of a tank (without barrel), (x, y) = middle of the bottom row: rows y (x−4..x+4), y−1 (x−4..x+4 without x±1),
    /// y−2 and y−3 (x−3..x+3), y−4 (x−1..x+1). [ORIGINAL] U431:05ED (BOMB.EXE 0x05F9D): fixed byte stores relative to
    /// y * 320 + x on A000h, no clipping.
    /// </summary>
    public static void TankBody(Span<byte> pixels, int x, int y, byte color)
    {
        for (var dx = -4; dx <= 4; dx++)
        {
            pixels[Address(x + dx, y)] = color;
            if (dx is not (-1 or 1))
                pixels[Address(x + dx, y - 1)] = color;
            if (dx is >= -3 and <= 3)
            {
                pixels[Address(x + dx, y - 2)] = color;
                pixels[Address(x + dx, y - 3)] = color;
            }

            if (dx is >= -1 and <= 1)
                pixels[Address(x + dx, y - 4)] = color;
        }
    }
    /// <summary>
    /// Copies the pixels on the line from (x1, y1) to (x2, y2) from <paramref name="source"/> to <paramref name="target"/>
    /// (same address in both). [ORIGINAL] UCC8:0AC0 (BOMB.EXE 0x0EDE0): the pixel choice of <see cref="Line"/> (runs for
    /// vertical and horizontal lines, otherwise Bresenham from the left end), source segment as argument, target VideoSeg.
    /// </summary>
    public static void CopyLine(ReadOnlySpan<byte> source, Span<byte> target, int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        if (dx == 0)
        {
            var top = Math.Min(y1, y2);
            var count = Math.Abs(y2 - y1) + 1;
            var address = Address(x1, top);
            for (var i = 0; i < count; i++, address += Stride)
                target[address] = source[address];
            return;
        }

        if (dx < 0)
        {
            dx = -dx;
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var dy = y2 - y1;
        if (dy == 0)
        {
            source.Slice(Address(x1, y1), dx + 1).CopyTo(target.Slice(Address(x1, y1)));
            return;
        }

        var rowStep = Stride;
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
            target[di] = source[di];
            di++;
            if (yMajor)
            {
                // 0x0EE73: a row after every pixel, the column only on a diagonal step.
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
    /// Restores the pixels of a tank body (mask of <see cref="TankBody"/>) from <paramref name="source"/>.
    /// [ORIGINAL] U431:0685 (BOMB.EXE 0x06035): 29 byte moves from the save buffer to A000h at fixed offsets.
    /// </summary>
    public static void CopyTankBody(ReadOnlySpan<byte> source, Span<byte> target, int x, int y)
    {
        for (var dx = -4; dx <= 4; dx++)
        {
            Copy(source, target, x + dx, y);
            if (dx is not (-1 or 1))
                Copy(source, target, x + dx, y - 1);
            if (dx is >= -3 and <= 3)
            {
                Copy(source, target, x + dx, y - 2);
                Copy(source, target, x + dx, y - 3);
            }

            if (dx is >= -1 and <= 1)
                Copy(source, target, x + dx, y - 4);
        }

        static void Copy(ReadOnlySpan<byte> source, Span<byte> target, int x, int y) =>
            target[Address(x, y)] = source[Address(x, y)];
    }

    /// <summary>
    /// Ellipse around (x, y) relative to the viewport corner with radii <paramref name="rx"/>, <paramref name="ry"/>.
    /// [ORIGINAL] UCC8:0351 (BOMB.EXE 0x0E671): midpoint algorithm in 32-bit integers, four points per step from the top
    /// and bottom apex; with <see cref="Viewport.Clip"/> and an ellipse that leaves the viewport every point is checked
    /// (UCC8:0595), otherwise none (UCC8:053F). Addresses are 16-bit like the original's offsets.
    /// </summary>
    /// <remarks>
    /// [APPROX] Writes whose 16-bit address lies beyond the 64000 screen bytes are dropped; the original stores them in
    /// the rest of the segment (off-screen video memory or, with VideoSeg = a heap buffer, behind it). Only unclipped
    /// ellipses that leave the screen get there; the playfield never draws one.
    /// </remarks>
    public static void Ellipse(Span<byte> pixels, Viewport viewport, int x, int y, int rx, int ry, byte color)
    {
        var cx = (short)(x + viewport.X1);
        var cy = (short)(y + viewport.Y1);
        var clipped = viewport.Clip && !((short)(cx + rx) <= viewport.X2 && (short)(cx - rx) >= viewport.X1
                                         && (short)(cy + ry) <= viewport.Y2 && (short)(cy - ry) >= viewport.Y1);
        var a2 = (int)((uint)(ushort)rx * (ushort)rx);
        var twoA2 = a2 << 1;
        var b2 = (int)((uint)(ushort)ry * (ushort)ry);
        var twoB2 = b2 << 1;
        var row = (ushort)(Stride * ry);
        var center = (ushort)(cx + (cy << 8) + (cy << 6));

        // Addresses and coordinates of the four points: bottom right, top right, bottom left, top left.
        var bottomRight = (ushort)(center + row);
        var bottomLeft = bottomRight;
        var topRight = (ushort)(center - row);
        var topLeft = topRight;
        int xRight = cx, xLeft = cx, yBottom = cy + ry, yTop = cy - ry;

        var dxTerm = 0;
        var dyTerm = (int)((uint)twoA2 * (ushort)ry);
        var error = (a2 >> 2) + b2 - (int)((uint)a2 * (ushort)ry);
        var count = (short)ry;
        var step = 0;

        while (dxTerm - dyTerm < 0)
        {
            Plot(pixels, step);
            step = 1;
            if (error >= 0)
            {
                step = 0x101;
                count--;
                dyTerm -= twoA2;
                error -= dyTerm;
            }

            dxTerm += twoB2;
            error += dxTerm + b2;
        }

        var t = a2 - b2;
        error += ((t >> 1) + t - dxTerm - dyTerm) >> 1;
        do
        {
            Plot(pixels, step);
            step = 0x100;
            if (error < 0)
            {
                step = 0x101;
                dxTerm += twoB2;
                error += dxTerm;
            }

            dyTerm -= twoA2;
            error -= dyTerm - a2;
            count--;
        }
        while (count >= 0);

        void Plot(Span<byte> target, int stepFlags)
        {
            var sx = stepFlags & 1;
            var sy = stepFlags >> 8;
            var rowStep = sy != 0 ? -Stride : 0;
            if (clipped)
            {
                xRight += sx;
                xLeft -= sx;
                yBottom -= sy;
                yTop += sy;
            }

            bottomRight = (ushort)(bottomRight + sx + rowStep);
            topRight = (ushort)(topRight + sx - rowStep);
            Store(target, bottomRight, xRight, yBottom);
            Store(target, topRight, xRight, yTop);
            bottomLeft = (ushort)(bottomLeft - sx + rowStep);
            topLeft = (ushort)(topLeft - sx - rowStep);
            Store(target, bottomLeft, xLeft, yBottom);
            Store(target, topLeft, xLeft, yTop);
        }

        void Store(Span<byte> target, ushort address, int px, int py)
        {
            if (clipped && !viewport.Contains(px, py))
                return;
            if (address < IndexBuffer.Size)
                target[address] = color;
        }
    }
}
