namespace BombRedone.Core.Graphics;

/// <summary>
/// Graph viewport as the original keeps it in DS:E8FA (ViewPortType: x1, y1, x2, y2 as Integer, Clip as Boolean at
/// DS:E902). [ORIGINAL] UCC8:0006 (BOMB.EXE 0x0E326) copies it with GetViewSettings (0E26:1028); the routines of
/// segment 0CC8 that honour it read these five fields.
/// </summary>
public readonly record struct Viewport(int X1, int Y1, int X2, int Y2, bool Clip)
{
    /// <summary>Whole mode 13h screen, as set by UDBD:001E (BOMB.EXE 0x0F28E: SetViewPort(0, 0, GetMaxX, GetMaxY, True)).</summary>
    public static Viewport FullScreen => new(0, 0, IndexBuffer.Width - 1, IndexBuffer.Height - 1, true);

    /// <summary>
    /// Middle of the viewport relative to its corner. [ORIGINAL] UDBD:003D (BOMB.EXE 0x0F2AD): (x1 + x2) div 2 − x1.
    /// </summary>
    public int CenterX => (X1 + X2) / 2 - X1;

    /// <summary>[ORIGINAL] UDBD:0066 (BOMB.EXE 0x0F2D6): (y1 + y2) div 2 − y1.</summary>
    public int CenterY => (Y1 + Y2) / 2 - Y1;

    /// <summary>Whether (x, y) lies inside, with the signed comparisons of UCC8:0595 (BOMB.EXE 0x0E8B5).</summary>
    public bool Contains(int x, int y) => x >= X1 && x <= X2 && y >= Y1 && y <= Y2;
}
