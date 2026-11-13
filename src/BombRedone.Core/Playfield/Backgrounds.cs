using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>
/// The nine backgrounds of the playfield. [ORIGINAL] O8:0000 (BOMB.OVR 0x1C0C0), docs/analyse/runde.md section 3.3:
/// clear the save buffer, draw background n on the screen, copy the field into the save buffer and frame it there.
/// </summary>
public static class Backgrounds
{
    /// <summary>Names as on the options screen (O2:0E35).</summary>
    public static readonly IReadOnlyList<string> Names =
        ["None", "Stars", "Bars", "Circles", "Spread", "Sky", "Static", "Rectangle", "Asterisk"];

    public const int Count = 9;

    /// <summary>[ORIGINAL] Viewport set by O8:0000 for Circles, Spread, Static and Asterisk: SetViewPort(11, 31, 309, 189, True).</summary>
    public static readonly Viewport Field = new(11, 31, 309, 189, true);

    /// <summary>First DAC entry of the background colours (30..59).</summary>
    public const int FirstColor = 30;

    private static readonly Real48 Five = Real48.FromInt32(5);
    private static readonly Real48 Ten = Real48.FromInt32(10);
    private static readonly Real48 Half = Real48.FromHex("800000000000");

    /// <summary>
    /// Draws background <paramref name="number"/> and fills the save buffer. Returns whether the palette rotation runs
    /// backwards ([ORIGINAL] O8:08EE: Random:Real &lt; 0.5 calls 0C79:0428; the rotation itself is not modelled).
    /// </summary>
    public static bool Draw(int number, IndexBuffer screen, IndexBuffer background, VgaPalette palette, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(random);
        if (number is < 0 or >= Count)
            throw new ArgumentOutOfRangeException(nameof(number));

        // FillChar(DS:E5C0^, 64000, 0).
        background.Clear();
        var pixels = screen.Pixels.AsSpan();
        switch (number)
        {
            case 1:
                Stars(pixels, palette, random);
                break;
            case 2:
                // O8:0163: Line(11, y, 310, y) for y = 31..190 in (y mod 30) + 30.
                GradientPalette(palette, random);
                for (var y = 31; y <= 190; y++)
                    VgaDraw.Line(pixels, 11, y, 310, y, (byte)(y % 30 + FirstColor));
                break;
            case 3:
                Circles(pixels, palette, random);
                break;
            case 4:
                Spread(pixels, palette, random);
                break;
            case 5:
                // O8:04AC: DAC 30 + i = (i, 0, 63 − i) for i = 0..19, Line(11, y, 310, y) in (y − 30) div 8 + 30.
                for (var i = 0; i <= 19; i++)
                    palette.Set(FirstColor + i, i, 0, 63 - i);
                for (var y = 31; y <= 190; y++)
                    VgaDraw.Line(pixels, 11, y, 310, y, (byte)((y - 30) / 8 + FirstColor));
                break;
            case 6:
                // O8:052C: every pixel of the field in Random(30) + 30, x outside, y inside.
                GradientPalette(palette, random);
                for (var x = Field.X1; x <= Field.X2; x++)
                {
                    for (var y = Field.Y1; y <= Field.Y2; y++)
                        VgaDraw.PutPixel(pixels, x, y, (byte)(random.Next(30) + FirstColor));
                }

                break;
            case 7:
                Rectangles(pixels, palette, random);
                break;
            case 8:
                Asterisk(pixels, palette, random);
                break;
        }

        // O8:0877: VideoSeg := save buffer, copy the columns 10..310 (rows 30..190) from A000h, frame the buffer in 13.
        for (var x = 10; x <= 310; x++)
            VgaDraw.CopyLine(screen.Pixels, background.Pixels, x, 30, x, 190);
        VgaDraw.Rectangle(background.Pixels, 8, 28, 312, 192, Playfield.FrameColor);
        VgaDraw.Rectangle(background.Pixels, 10, 30, 310, 190, Playfield.FrameColor);
        return random.NextReal() < Half;
    }

    /// <summary>
    /// Colours 30..59 as three gradients between three random colours. [ORIGINAL] U431:000C (BOMB.EXE 0x059BC):
    /// P[i, c] = Random(64) for i, c = 0..2 (i outside); DAC 30 + 10 i + k = Trunc(P[i, c] + k · ((P[(i+1) mod 3, c] − P[i, c]) / 10))
    /// in Real48 for k = 0..9, c = R, G, B.
    /// </summary>
    public static void GradientPalette(VgaPalette palette, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(random);
        var points = new int[3, 3];
        for (var i = 0; i < 3; i++)
        {
            for (var c = 0; c < 3; c++)
                points[i, c] = random.Next(64);
        }

        Span<int> rgb = stackalloc int[3];
        for (var i = 0; i < 3; i++)
        {
            for (var k = 0; k <= 9; k++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var from = points[i, c];
                    var to = points[(i + 1) % 3, c];
                    var slope = Real48.FromInt32(to - from) / Ten;
                    rgb[c] = (Real48.FromInt32(from) + Real48.FromInt32(k) * slope).Trunc();
                }

                palette.Set(FirstColor + 10 * i + k, rgb[0], rgb[1], rgb[2]);
            }
        }
    }

    /// <summary>
    /// [ORIGINAL] O8:0029: DAC 30 + i = (v, v, v) and 40 + i = (v, v, 0) with v = Trunc(i · 5.0) for i = 0..9; then
    /// Random(500) + 250 stars, each x = Random(299) + 11, y = Random(159) + 31, colour Random(20) + 30 (BGI PutPixel).
    /// </summary>
    private static void Stars(Span<byte> pixels, VgaPalette palette, TurboPascalRandom random)
    {
        for (var i = 0; i <= 9; i++)
        {
            var v = (Real48.FromInt32(i) * Five).Trunc();
            palette.Set(FirstColor + i, v, v, v);
            palette.Set(FirstColor + 10 + i, v, v, 0);
        }

        var count = random.Next(500) + 250;
        for (var i = 1; i <= count; i++)
        {
            var x = random.Next(299) + 11;
            var y = random.Next(159) + 31;
            var color = random.Next(20) + FirstColor;
            VgaDraw.PutPixel(pixels, x, y, (byte)color);
        }
    }

    /// <summary>[ORIGINAL] O8:01DA: ellipses UCC8:0351 with radius i = 0..170 around the middle of the field and one
    /// pixel to the right of it, colour (i mod 30) + 30, clipped to the field.</summary>
    private static void Circles(Span<byte> pixels, VgaPalette palette, TurboPascalRandom random)
    {
        GradientPalette(palette, random);
        var x = Field.CenterX;
        var y = Field.CenterY;
        for (var i = 0; i <= 170; i++)
        {
            var color = (byte)(i % 30 + FirstColor);
            VgaDraw.Ellipse(pixels, Field, x, y, i, i, color);
            VgaDraw.Ellipse(pixels, Field, x + 1, y, i, i, color);
        }
    }

    /// <summary>
    /// [ORIGINAL] O8:02A9: lines UCC8:09E4 from (160, 100) to every point of the field's border (top left to right,
    /// right top to bottom, bottom right to left, left bottom to top); line k (from 0) in (k mod 15) · 2 + 30
    /// (LongInt mod 12CE:0AB8, mul 12CE:0A9D).
    /// </summary>
    private static void Spread(Span<byte> pixels, VgaPalette palette, TurboPascalRandom random)
    {
        GradientPalette(palette, random);
        const int cx = 160, cy = 100;
        var k = 0;
        for (var x = Field.X1; x <= Field.X2; x++)
            Ray(pixels, x, Field.Y1, ref k);
        for (var y = Field.Y1; y <= Field.Y2; y++)
            Ray(pixels, Field.X2, y, ref k);
        for (var x = Field.X2; x >= Field.X1; x--)
            Ray(pixels, x, Field.Y2, ref k);
        for (var y = Field.Y2; y >= Field.Y1; y--)
            Ray(pixels, Field.X1, y, ref k);

        static void Ray(Span<byte> pixels, int x, int y, ref int k)
        {
            VgaDraw.Line(pixels, cx, cy, x, y, (byte)(k % 15 * 2 + FirstColor));
            k++;
        }
    }

    /// <summary>[ORIGINAL] O8:05E9: Random(3) chooses 80, 150 or 159 frames UCC8:0031 (i + 10, i + 30)–(310 − i, 190 − i),
    /// i from 1, colour (i mod 30) + 30.</summary>
    private static void Rectangles(Span<byte> pixels, VgaPalette palette, TurboPascalRandom random)
    {
        GradientPalette(palette, random);
        var count = random.Next(3) switch
        {
            0 => 80,
            1 => 150,
            _ => 159,
        };
        for (var i = 1; i <= count; i++)
            VgaDraw.Rectangle(pixels, i + 10, i + 30, 310 - i, 190 - i, (byte)(i % 30 + FirstColor));
    }

    /// <summary>
    /// [ORIGINAL] O8:06AA: lines UCC8:09E4 from (160, 110) to (160 ∓ i, 31) and (160 ∓ i, 189) for i = 0..149 in
    /// (i mod 30) + 30, then to (11, 110 ∓ j) and (309, 110 ∓ j) for j = 0..79 in ((j + 70) mod 30) + 30.
    /// </summary>
    private static void Asterisk(Span<byte> pixels, VgaPalette palette, TurboPascalRandom random)
    {
        GradientPalette(palette, random);
        const int cx = 160, cy = 110;
        for (var i = 0; i <= 149; i++)
        {
            var color = (byte)(i % 30 + FirstColor);
            VgaDraw.Line(pixels, cx, cy, cx - i, 31, color);
            VgaDraw.Line(pixels, cx, cy, cx + i, 31, color);
            VgaDraw.Line(pixels, cx, cy, cx - i, 189, color);
            VgaDraw.Line(pixels, cx, cy, cx + i, 189, color);
        }

        for (var j = 0; j <= 79; j++)
        {
            var color = (byte)((j + 70) % 30 + FirstColor);
            VgaDraw.Line(pixels, cx, cy, 11, cy - j, color);
            VgaDraw.Line(pixels, cx, cy, 309, cy - j, color);
            VgaDraw.Line(pixels, cx, cy, 11, cy + j, color);
            VgaDraw.Line(pixels, cx, cy, 309, cy + j, color);
        }
    }
}
