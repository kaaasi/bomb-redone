using System.Security.Cryptography;
using BombRedone.Core.Graphics;

namespace BombRedone.Core.Tests.Graphics;

/// <summary>
/// Drawing routines against the original routines run in the probe (docs/analyse/feld.md). The expected hash is the
/// SHA-256 of the screen after the same calls in the emulated original:
/// <c>python3 -I tools/reverse-engineering/feld.py call --fill 0 "0CC8:09E4(10,20,300,25,5)" … "0431:05ED(4,4,22)"</c>
/// (argument list as in <see cref="DrawSequence"/>, Pascal order).
/// </summary>
public sealed class VgaDrawTests
{
    private const string OriginalScreenHash = "d3ebafa3cc9727f71214a3880c7bef73c438d212f1ec527e3e81eef88a664e59";

    private static void DrawSequence(Span<byte> pixels)
    {
        VgaDraw.Line(pixels, 10, 20, 300, 25, 5);
        VgaDraw.Line(pixels, 300, 30, 12, 190, 6);
        VgaDraw.Line(pixels, 50, 190, 60, 10, 7);
        VgaDraw.Line(pixels, 200, 100, 200, 40, 8);
        VgaDraw.Line(pixels, 150, 150, 20, 150, 9);
        VgaDraw.Line(pixels, 5, 5, 6, 180, 10);
        VgaDraw.Line(pixels, 318, 2, 1, 3, 11);
        VgaDraw.Line(pixels, 40, 60, 70, 90, 12);
        VgaDraw.Rectangle(pixels, 30, 40, 250, 170, 13);
        VgaDraw.Rectangle(pixels, 250, 170, 30, 40, 14);
        VgaDraw.PutPixel(pixels, 319, 199, 15);
        VgaDraw.TankBody(pixels, 100, 120, 21);
        VgaDraw.TankBody(pixels, 4, 4, 22);
    }

    [Fact]
    public void Sequence_matches_the_original_routines()
    {
        var buffer = new IndexBuffer();
        DrawSequence(buffer.Pixels);

        Assert.Equal(OriginalScreenHash, Convert.ToHexStringLower(SHA256.HashData(buffer.Pixels)));
    }

    [Fact]
    public void Tank_body_has_the_rows_of_U431_05ED()
    {
        var buffer = new IndexBuffer();
        VgaDraw.TankBody(buffer.Pixels, 100, 120, 1);

        string Row(int y) => string.Concat(Enumerable.Range(95, 11).Select(x => buffer[x, y] == 0 ? '.' : '#'));
        Assert.Equal(".#########.", Row(120));
        Assert.Equal(".###.#.###.", Row(119));
        Assert.Equal("..#######..", Row(118));
        Assert.Equal("..#######..", Row(117));
        Assert.Equal("....###....", Row(116));
        Assert.Equal("...........", Row(115));
    }
}
