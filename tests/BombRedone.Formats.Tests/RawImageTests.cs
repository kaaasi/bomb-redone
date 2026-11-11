using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Formats.Tests;

public class RawImageTests
{
    private static byte[] Synthetic(Func<int, byte> pixel, Func<int, byte> paletteByte)
    {
        var data = new byte[RawImage.FileLength];
        for (var i = 0; i < RawImage.PixelCount; i++) data[i] = pixel(i);
        for (var i = 0; i < Palette.ByteLength; i++) data[RawImage.PixelCount + i] = paletteByte(i);
        return data;
    }

    [Fact]
    public void RoundTripKeepsEveryByte()
    {
        var data = Synthetic(i => (byte)(i * 7 % 256), i => (byte)(i % 64));
        var image = RawImage.Parse(data);
        Assert.Equal(data, image.ToBytes());
        Assert.Equal((byte)(5 * 7 % 256), image[5, 0]);
        Assert.Equal((byte)((3 * RawImage.Width + 2) * 7 % 256), image[2, 3]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RawImage.FileLength - 1)]
    [InlineData(RawImage.PixelCount)]
    [InlineData(RawImage.FileLength + 1)]
    public void WrongLengthIsRejected(int length) =>
        Assert.Throws<FormatException>(() => RawImage.Parse(new byte[length]));

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(Palette.ByteLength - 1)]
    public void PaletteValueAbove63IsRejected(int position)
    {
        var data = new byte[RawImage.FileLength];
        data[RawImage.PixelCount + position] = 64;
        var ex = Assert.Throws<FormatException>(() => RawImage.Parse(data));
        Assert.Contains("64", ex.Message);
    }

    [Fact]
    public void PaletteValue63IsAccepted()
    {
        var data = new byte[RawImage.FileLength];
        Array.Fill(data, (byte)63, RawImage.PixelCount, Palette.ByteLength);
        Assert.Equal((byte)63, RawImage.Parse(data).Palette.GetChannel(255, 2));
    }

    [Fact]
    public void PixelAccessOutsideTheImageFails()
    {
        var image = RawImage.Parse(new byte[RawImage.FileLength]);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[RawImage.Width, 0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[0, RawImage.Height]);
        Assert.Throws<ArgumentOutOfRangeException>(() => image[-1, 0]);
    }

    [Fact]
    public void WideningCoversTheFullRangeAndBothModesDiffer()
    {
        Assert.Equal(0, Palette.Widen(0));
        Assert.Equal(255, Palette.Widen(63, PaletteScaling.FullRange));
        Assert.Equal(252, Palette.Widen(63, PaletteScaling.Times4));
        // Bit replication stays within 1 of the exact scaling v * 255 / 63 and never decreases.
        for (var v = 0; v <= 63; v++)
        {
            Assert.InRange(Palette.Widen(v) - v * 255.0 / 63.0, -1.0, 1.0);
            if (v > 0) Assert.True(Palette.Widen(v) > Palette.Widen(v - 1));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => Palette.Widen(64));
    }

    [Fact]
    public void ToAbgrPacksChannelsAsSurfaceExpects()
    {
        var data = new byte[RawImage.FileLength];
        data[RawImage.PixelCount + 3] = 63; // entry 1: red
        data[RawImage.PixelCount + 5] = 21; // entry 1: blue
        var abgr = RawImage.Parse(data).Palette.ToAbgr();
        Assert.Equal(0xFF000000u | ((uint)Palette.Widen(21) << 16) | 255u, abgr[1]);
    }

    [GameDataTheory]
    [InlineData("BOMB1.RAW", 61, "58043d419503")]
    [InlineData("BOMB2.RAW", 53, "b827bfbc3a68")]
    public void OriginalTitleImagesHaveExpectedShape(string name, int usedColors, string fileHashPrefix)
    {
        var bytes = File.ReadAllBytes(TestGameData.Load().Require(name));
        Assert.Equal(RawImage.FileLength, bytes.Length);
        Assert.StartsWith(fileHashPrefix, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());

        var image = RawImage.Parse(bytes);
        Assert.Equal(usedColors, image.UsedColorCount());
        Assert.Equal(bytes, image.ToBytes());
    }
}
