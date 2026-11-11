using BombRedone.Formats;

namespace BombRedone.Formats.Tests;

/// <summary>Synthetic tests of the BOMB.CFG reader: one per field, plus the error cases.</summary>
public class GameConfigTests
{
    /// <summary>A synthetic, valid file: version tag followed by zero bytes.</summary>
    internal static byte[] Blank()
    {
        var data = new byte[GameConfig.Size];
        GameConfig.VersionTag.CopyTo(data);
        return data;
    }

    private static GameConfig WithByte(int offset, byte value)
    {
        var data = Blank();
        data[offset] = value;
        return GameConfig.Read(data);
    }

    private static GameConfig WithWord(int offset, ushort value)
    {
        var data = Blank();
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        return GameConfig.Read(data);
    }

    [Fact]
    public void BlankFileHasAllFieldsZero()
    {
        var config = GameConfig.Read(Blank());

        Assert.Equal(new GameConfig(), config);
    }

    [Theory]
    [InlineData(5, nameof(GameConfig.SoundEffects))]
    [InlineData(6, nameof(GameConfig.FlySound))]
    [InlineData(13, nameof(GameConfig.InvisibleShot))]
    [InlineData(14, nameof(GameConfig.ShotTrail))]
    [InlineData(17, nameof(GameConfig.TanksFall))]
    [InlineData(22, nameof(GameConfig.ForceMouse))]
    [InlineData(23, nameof(GameConfig.MouseEnabled))]
    [InlineData(24, nameof(GameConfig.DirectVideo))]
    [InlineData(25, nameof(GameConfig.ExtendedKeyboard))]
    [InlineData(26, nameof(GameConfig.PaletteRotation))]
    [InlineData(27, nameof(GameConfig.CrudeVideo))]
    [InlineData(30, nameof(GameConfig.ComputersBuy))]
    [InlineData(41, nameof(GameConfig.PaletteUsesRetrace))]
    [InlineData(42, nameof(GameConfig.TeamPlay))]
    public void BooleanFieldIsReadFromItsOwnByteOnly(int offset, string property)
    {
        var config = WithByte(offset, 1);

        foreach (var p in typeof(GameConfig).GetProperties().Where(p => p.PropertyType == typeof(bool)))
            Assert.Equal(p.Name == property, (bool)p.GetValue(config)!);
    }

    [Theory]
    [InlineData(9, nameof(GameConfig.CrumblePercent))]
    [InlineData(15, nameof(GameConfig.GamesToPlay))]
    [InlineData(18, nameof(GameConfig.Background))]
    [InlineData(28, nameof(GameConfig.PaletteSpeed))]
    [InlineData(31, nameof(GameConfig.TurretSpeed))]
    [InlineData(33, nameof(GameConfig.FallSpeed))]
    [InlineData(35, nameof(GameConfig.ExplosionSpeed))]
    [InlineData(37, nameof(GameConfig.ShotSpeed))]
    [InlineData(39, nameof(GameConfig.LaserSpeed))]
    public void NumberFieldIsLittleEndianWordAtItsOffset(int offset, string property)
    {
        var config = WithWord(offset, 0x0102);

        foreach (var p in typeof(GameConfig).GetProperties().Where(p => p.PropertyType == typeof(int) && p.CanWrite))
            Assert.Equal(p.Name == property ? 0x0102 : 0, (int)p.GetValue(config)!);
    }

    [Fact]
    public void WindIsWordAtOffset7()
    {
        Assert.Equal(WindSetting.Gale, WithWord(7, 4).Wind);
        Assert.Equal(WindSetting.Random, WithWord(7, 5).Wind);
    }

    [Fact]
    public void WallIsWordAtOffset11()
    {
        Assert.Equal(WallSetting.Warp, WithWord(11, 4).Wall);
        Assert.Equal(WallSetting.Sticky, WithWord(11, 1).Wall);
    }

    // Values with the high byte set catch a reader that only takes the low byte.
    [Fact]
    public void WindAndWallUseBothBytes()
    {
        Assert.Equal((WindSetting)0x0104, WithWord(7, 0x0104).Wind);
        Assert.Equal((WallSetting)0x0104, WithWord(11, 0x0104).Wall);
    }

    [Fact]
    public void BossScreenMemoryUsesBothBytes()
    {
        Assert.Equal(0x0140, WithWord(43, 0x0140).BossScreenMemoryKiB);
    }

    // BOMB.EXE treats these fields as Turbo Pascal Integer (cwd before Write(LongInt), BOMB.OVR 0x79AE ff.).
    [Theory]
    [InlineData(9, nameof(GameConfig.CrumblePercent))]
    [InlineData(15, nameof(GameConfig.GamesToPlay))]
    [InlineData(18, nameof(GameConfig.Background))]
    [InlineData(28, nameof(GameConfig.PaletteSpeed))]
    [InlineData(31, nameof(GameConfig.TurretSpeed))]
    [InlineData(33, nameof(GameConfig.FallSpeed))]
    [InlineData(35, nameof(GameConfig.ExplosionSpeed))]
    [InlineData(37, nameof(GameConfig.ShotSpeed))]
    [InlineData(39, nameof(GameConfig.LaserSpeed))]
    public void NumberFieldIsSignedInteger(int offset, string property)
    {
        var config = WithWord(offset, 0x8001);

        Assert.Equal(-32767, (int)typeof(GameConfig).GetProperty(property)!.GetValue(config)!);
    }

    [Fact]
    public void WindAndWallAreSignedInteger()
    {
        Assert.Equal((WindSetting)(-1), WithWord(7, 0xFFFF).Wind);
        Assert.Equal((WallSetting)(-1), WithWord(11, 0xFFFF).Wall);
    }

    [Fact]
    public void BossScreenMemoryStaysUnsigned()
    {
        Assert.Equal(0xFFFF, WithWord(43, 0xFFFF).BossScreenMemoryKiB);
    }

    [Fact]
    public void UserBackgroundMaskIsWordAtOffset20()
    {
        Assert.Equal(0x01FF, WithWord(20, 0x01FF).UserBackgroundMask);
        Assert.Equal(0xFFFF, WithWord(20, 0xFFFF).UserBackgroundMask);
    }

    [Fact]
    public void BossScreenMemoryIsWordAtOffset43InKiB()
    {
        var config = WithWord(43, 32);

        Assert.Equal(32, config.BossScreenMemoryKiB);
        Assert.Equal(32 * 1024, config.BossScreenMemoryBytes);
    }

    [Theory]
    [InlineData(62, 62 * 1024)] // below the cap
    [InlineData(63, 64000)]     // 64512 is capped at 0xFA00
    [InlineData(64, 0)]         // 16-bit shift overflows to 0, below the cap
    [InlineData(65, 1024)]
    public void BossScreenMemoryUsesSixteenBitShiftAndCap(ushort kib, int bytes)
    {
        Assert.Equal(bytes, WithWord(43, kib).BossScreenMemoryBytes);
    }

    [Theory]
    [InlineData(0, BackgroundKind.None)]
    [InlineData(1, BackgroundKind.Fixed)]
    [InlineData(8, BackgroundKind.Fixed)]
    [InlineData(9, BackgroundKind.Random)]
    [InlineData(10, BackgroundKind.User)]
    [InlineData(11, BackgroundKind.Unknown)]
    [InlineData(0xFFFF, BackgroundKind.Fixed)] // -1 <= 8: BOMB.OVR 0x1CAED compares signed (jg)
    [InlineData(0x8000, BackgroundKind.Fixed)]
    [InlineData(0x7FFF, BackgroundKind.Unknown)]
    public void BackgroundValueIsClassified(ushort value, BackgroundKind kind)
    {
        Assert.Equal(kind, WithWord(18, value).BackgroundKind);
    }

    [Fact]
    public void AnyNonZeroByteIsTrue()
    {
        Assert.True(WithByte(5, 0xFF).SoundEffects);
    }

    [Fact]
    public void ValuesOutsideTheDocumentedRangeAreKept()
    {
        Assert.Equal((WindSetting)9, WithWord(7, 9).Wind);
        Assert.Equal(500, WithWord(9, 500).CrumblePercent);
    }

    [Fact]
    public void TrailingBytesAreIgnored()
    {
        var data = Blank().Concat(new byte[] { 0xAA, 0xBB }).ToArray();
        data[5] = 1;

        Assert.True(GameConfig.Read(data).SoundEffects);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(GameConfig.Size - 1)]
    public void TooShortFileIsRejected(int length)
    {
        var data = Blank()[..length];

        var ex = Assert.Throws<GameConfigException>(() => GameConfig.Read(data));
        Assert.Equal(GameConfigError.TooShort, ex.Error);
    }

    [Theory]
    [InlineData(new byte[] { 4, (byte)'3', (byte)'.', (byte)'0', 0 })] // release 3.0 (BOMBCFG converts it, BOMB.EXE does not)
    [InlineData(new byte[] { 4, (byte)'2', (byte)'.', (byte)'5', 0 })] // release 2.5
    [InlineData(new byte[] { 3, (byte)'3', (byte)'.', (byte)'2', 0 })] // length byte without the NUL
    [InlineData(new byte[] { 4, (byte)'3', (byte)'.', (byte)'2', (byte)' ' })]
    public void WrongVersionIsRejected(byte[] tag)
    {
        var data = Blank();
        tag.CopyTo(data, 0);

        var ex = Assert.Throws<GameConfigException>(() => GameConfig.Read(data));
        Assert.Equal(GameConfigError.WrongVersion, ex.Error);
    }

    [Fact]
    public void LoadReadsFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            var data = Blank();
            data[15] = 30;
            File.WriteAllBytes(path, data);

            Assert.Equal(30, GameConfig.Load(path).GamesToPlay);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
