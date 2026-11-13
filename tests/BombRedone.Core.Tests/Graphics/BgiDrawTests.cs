using System.Security.Cryptography;
using BombRedone.Core.Graphics;

namespace BombRedone.Core.Tests.Graphics;

/// <summary>
/// BGI line against the original: <c>feld.py call --fill 0 "0E26:1DC2(1)" "0E26:1C86(165,38,202,166)" …</c>, line k (from 0)
/// in colour k + 1 (SetColor 0E26:1DC2, Line 0E26:1C86).
/// </summary>
public sealed class BgiDrawTests
{
    private static readonly (int X0, int Y0, int X1, int Y1)[] Lines =
    [
        (165, 38, 202, 166),
        (24, 18, 274, 24),
        (187, 149, 29, 129),
        (109, 9, 44, 111),
        (214, 17, 123, 23),
        (282, 108, 30, 144),
        (63, 57, 298, 15),
        (295, 149, 203, 12),
        (113, 11, 285, 34),
        (148, 107, 73, 138),
        (60, 146, 157, 143),
        (92, 26, 297, 146),
        (116, 115, 110, 123),
        (52, 164, 44, 161),
        (274, 156, 278, 157),
        (258, 169, 263, 171),
        (173, 83, 169, 81),
        (61, 167, 61, 174),
        (273, 107, 278, 107),
        (57, 50, 64, 54),
        (104, 107, 99, 113),
        (235, 30, 228, 38),
        (180, 107, 182, 113),
        (253, 37, 246, 36),
        (5, 5, 5, 5),
        (0, 0, 319, 199),
        (319, 0, 0, 199),
        (100, 50, 101, 190),
        (10, 100, 300, 101),
    ];

    [Fact]
    public void Lines_match_the_Graph_line_of_the_original()
    {
        var buffer = new IndexBuffer();
        for (var k = 0; k < Lines.Length; k++)
        {
            var (x0, y0, x1, y1) = Lines[k];
            BgiDraw.Line(buffer.Pixels, IndexBuffer.Width, IndexBuffer.Height, x0, y0, x1, y1, (byte)(k + 1));
        }

        Assert.Equal("a1e412d552ac7687cdd4af273908f8c9ee88250efd77f18ec652e3e2d2d162bc",
            Convert.ToHexStringLower(SHA256.HashData(buffer.Pixels)));
    }

    [Fact]
    public void Pixels_outside_the_buffer_are_dropped()
    {
        var pixels = new byte[4 * 3];
        BgiDraw.Line(pixels, 4, 3, -2, 1, 6, 1, 9);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 9, 9, 9, 9, 0, 0, 0, 0 }, pixels);
    }
}
