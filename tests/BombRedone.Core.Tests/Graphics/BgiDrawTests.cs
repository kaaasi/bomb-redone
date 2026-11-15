using System.Security.Cryptography;
using BombRedone.Core.Graphics;

namespace BombRedone.Core.Tests.Graphics;

/// <summary>
/// BGI line and circle against the original: <c>feld.py call --fill 0 "0E26:1DC2(1)" "0E26:1C86(165,38,202,166)" …</c>, line k
/// (from 0) in colour k + 1 (SetColor 0E26:1DC2, Line 0E26:1C86); circles with Circle 0E26:11EA.
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

    /// <summary>
    /// <c>feld.py call --fill 0</c> with SetColor(k) and Circle(x, y, r) per entry, the driver aspect 8500 / 10000 and
    /// the full-screen viewport of InitGraph (clip on): circles leaving the screen are clipped per point.
    /// </summary>
    private static readonly (int Color, int X, int Y, int R)[] DriverCircles =
    [
        (1, 160, 100, 63), (10, 40, 30, 7), (4, 300, 180, 25), (13, 5, 100, 12), (8, 100, 50, 1),
        (2, 200, 60, 0), (9, 250, 120, 40), (5, 80, 150, 33), (13, 150, 20, 5), (8, 310, 10, 18),
    ];

    [Fact]
    public void Circles_match_the_Graph_circle_of_the_original()
    {
        var buffer = new IndexBuffer();
        var screen = new Viewport(0, 0, 319, 199, true);
        foreach (var (color, x, y, r) in DriverCircles)
        {
            BgiDraw.Circle(buffer.Pixels, IndexBuffer.Width, IndexBuffer.Height, screen, x, y, r, (byte)color,
                BgiDraw.DriverXAspect, BgiDraw.DriverYAspect);
        }

        Assert.Equal("50f07ca39671bacc9c4021804066d7df3e4684ae56afa9939508524b6a56a0f3",
            Convert.ToHexStringLower(SHA256.HashData(buffer.Pixels)));
    }

    /// <summary>
    /// <c>feld.py call --fill 0 "0E26:192B(1,1)" "0E26:0F93(11,31,309,189,1)"</c>, then SetColor(2) and Circle per entry:
    /// aspect 1 : 1 and the clipped playfield viewport as in Dirt (O6:1014).
    /// </summary>
    [Fact]
    public void Round_circles_are_clipped_by_the_viewport_like_the_original()
    {
        var buffer = new IndexBuffer();
        var field = new Viewport(11, 31, 309, 189, true);
        foreach (var (x, y, r) in new[]
                 {
                     (0, 0, 20), (298, 158, 30), (150, 80, 60), (-3, 100, 10), (5, 170, 15), (290, -2, 12), (140, 70, 2),
                     (200, 100, 45),
                 })
        {
            BgiDraw.Circle(buffer.Pixels, IndexBuffer.Width, IndexBuffer.Height, field, x, y, r, 2, 1, 1);
        }

        Assert.Equal("2c31b1d1bcf5149281a6ce46d5cb8000f59d7644d6138862f45df63894948d49",
            Convert.ToHexStringLower(SHA256.HashData(buffer.Pixels)));
    }

    [Fact]
    public void A_circle_of_radius_one_sets_the_four_neighbours()
    {
        var pixels = new byte[5 * 5];
        BgiDraw.Circle(pixels, 5, 5, new Viewport(0, 0, 4, 4, true), 2, 2, 1, 7, BgiDraw.DriverXAspect, BgiDraw.DriverYAspect);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 7, 0, 7, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0 }, pixels);
    }

    [Fact]
    public void Pixels_outside_the_buffer_are_dropped()
    {
        var pixels = new byte[4 * 3];
        BgiDraw.Line(pixels, 4, 3, -2, 1, 6, 1, 9);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 9, 9, 9, 9, 0, 0, 0, 0 }, pixels);
    }
}
