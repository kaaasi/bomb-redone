using System.IO.Compression;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Playfield screen against the DOSBox screenshot s3_02 (session 3, round 1 before the first shot; spur gelaende-bilder,
/// docs/analyse/feld.md section 6). Start value from the window search: Randomize at 17:12:39.69, one menu step, RandSeed
/// 4983853D before the shuffle. The picture is read from the reference repository (never committed, AGENTS.md 2.1).
/// </summary>
public sealed class PlayfieldScreenSessionTests
{
    private const string PictureName = "s3_02_r1-spielfeld-vor-erstem-schuss.png";
    private const uint SeedBeforeShuffle = 0x4983853D;

    /// <summary>
    /// Header values the turn draws in colour 14 (O7:040C, docs/analyse/runde.md section 6.1, not implemented in round 2):
    /// angle, power, direction arrow, wind value and "Men". (x1, y1, x2, y2), both corners included.
    /// </summary>
    private static readonly (int X1, int Y1, int X2, int Y2)[] TurnValues =
    [
        (56, 0, 69, 6), (56, 10, 79, 16), (290, 1, 302, 5), (248, 10, 270, 16), (145, 10, 175, 16),
    ];

    [GameDataFact]
    public void Round_one_of_session_three_matches_the_screenshot()
    {
        var (pixels, palette) = ReadPicture(PicturePath());
        var screen = CreateScreen();
        var ours = screen.Screen.Pixels;

        var differences = Enumerable.Range(0, 320 * 200).Where(i => pixels[i] != ours[i]).ToList();

        // Frame, background, terrain, tanks, labels of the header and the weapon bar are equal pixel for pixel; only the
        // values the turn writes are missing.
        Assert.All(differences, i =>
        {
            Assert.Equal((14, 0), (pixels[i], ours[i]));
            Assert.Contains(TurnValues, r => i % 320 >= r.X1 && i % 320 <= r.X2 && i / 320 >= r.Y1 && i / 320 <= r.Y2);
        });
        Assert.Equal(248, differences.Count);

        // DAC of all colours the picture uses (DOSBox writes 6 bits as v << 2 | v >> 4), including the gradient 30..59 of
        // the background (no rotation step yet). Not equal: tank colours 20/21 follow the record that holds the player's
        // colour divisors, which the shuffle swaps (R2-D4 = A: records are swapped in round 3); weapon colours 100..119
        // are set by the turn.
        foreach (var index in pixels.Distinct().Where(i => i is not (20 or 21) && i < PlayfieldScreen.WeaponColorBase))
        {
            for (var c = 0; c < 3; c++)
            {
                var v = screen.Dac[3 * index + c];
                Assert.Equal(palette[3 * index + c], (byte)((v << 2) | (v >> 4)));
            }
        }

        Assert.Equal(palette.AsSpan(3 * 20, 3).ToArray(), Rgb8(screen.Dac.Slice(3 * 21, 3)));
        Assert.Equal(palette.AsSpan(3 * 21, 3).ToArray(), Rgb8(screen.Dac.Slice(3 * 20, 3)));
    }

    /// <summary>
    /// The round from the start value: shuffle with the swaps of the records (O3:2417, here only to know which name ends in
    /// which record), set-up, wind. The header name is the one of the last record, colour 19 + N (O8:0984 P12).
    /// </summary>
    private static PlayfieldScreen CreateScreen()
    {
        var fontPath = BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories())
            ?? throw new InvalidOperationException(
                "Reference font missing (tools/claude/fetch-reference.sh fetches schriften/ to ~/referenz).");
        var fonts = MenuFonts.Load(TestGameData.Load(), BitmapFontSource.ReadReference(fontPath));
        var random = new TurboPascalRandom(SeedBeforeShuffle);
        var order = ShuffleOrder(random, 2);
        Assert.Equal(0xD2E3BA6Eu, random.Seed);
        Assert.Equal([2, 1], order);

        var options = PlayfieldOptions.FromConfig(GameConfig.BombCfgDefaults, 2);
        var field = Playfield.Build(options, random);
        var wind = Wind.Choose((int)GameConfig.BombCfgDefaults.Wind, random);
        Assert.Equal(-92, wind.Value);
        Assert.Equal(8, field.BackgroundNumber);
        Assert.Equal([25, 281], field.Tanks.Select(t => t.X));
        return new PlayfieldScreen(fonts, field, wind, order.Select(n => "Player " + n).ToArray());
    }

    /// <summary>Like SeedSearch.SkipShuffle, but swaps the player numbers 1..N (O3:0000 swaps whole records).</summary>
    private static int[] ShuffleOrder(TurboPascalRandom random, int players)
    {
        var order = Enumerable.Range(1, players).ToArray();
        var swaps = random.Next(20) + 10;
        for (var i = 0; i < swaps; i++)
        {
            var a = random.Next(players);
            int b;
            do
            {
                b = random.Next(players);
            }
            while (b == a);
            (order[a], order[b]) = (order[b], order[a]);
        }

        return order;
    }

    private static byte[] Rgb8(ReadOnlySpan<byte> rgb6) => rgb6.ToArray().Select(v => (byte)((v << 2) | (v >> 4))).ToArray();

    /// <summary>
    /// Finds the picture: <c>BOMB_BILDER</c> (folder of the pictures of runde-1), <c>bilder/runde-1</c> next to the folder
    /// that holds the (resolved) <c>gamedata</c>, or <c>~/referenz/bilder/runde-1</c>.
    /// </summary>
    private static string PicturePath()
    {
        var gamedata = new DirectoryInfo(TestGameData.Load().RootDirectory);
        var resolved = gamedata.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo ?? gamedata;
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("BOMB_BILDER"),
            resolved.Parent?.Parent is { } root ? Path.Combine(root.FullName, "bilder", "runde-1") : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "referenz", "bilder", "runde-1"),
        };
        foreach (var dir in candidates.Where(d => !string.IsNullOrEmpty(d)))
        {
            var path = Path.Combine(dir!, PictureName);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"{PictureName} not found (BOMB_BILDER, bilder/runde-1 of the reference, ~/referenz/bilder/runde-1). " +
            "Load it with tools/claude/fetch-reference.sh bilder/runde-1.");
    }

    /// <summary>Minimal PNG reader for the DOSBox screenshots: 8-bit indexed, no interlace; returns indices and PLTE.</summary>
    private static (byte[] Pixels, byte[] Palette) ReadPicture(string path)
    {
        var data = File.ReadAllBytes(path);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A], data[..8]);
        int width = 0, height = 0;
        var palette = Array.Empty<byte>();
        using var idat = new MemoryStream();
        for (var p = 8; p < data.Length;)
        {
            var length = (data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3];
            var type = System.Text.Encoding.ASCII.GetString(data, p + 4, 4);
            var chunk = data.AsSpan(p + 8, length);
            switch (type)
            {
                case "IHDR":
                    width = (chunk[0] << 24) | (chunk[1] << 16) | (chunk[2] << 8) | chunk[3];
                    height = (chunk[4] << 24) | (chunk[5] << 16) | (chunk[6] << 8) | chunk[7];
                    Assert.Equal((8, 3, 0), (chunk[8], chunk[9], chunk[12]));
                    break;
                case "PLTE":
                    palette = chunk.ToArray();
                    break;
                case "IDAT":
                    idat.Write(chunk);
                    break;
            }

            p += 12 + length;
        }

        Assert.Equal((320, 200), (width, height));
        idat.Position = 0;
        using var inflated = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
            z.CopyTo(inflated);
        var raw = inflated.ToArray();
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (width + 1)];
            for (var x = 0; x < width; x++)
            {
                int a = x > 0 ? pixels[y * width + x - 1] : 0, b = y > 0 ? pixels[(y - 1) * width + x] : 0;
                var c = x > 0 && y > 0 ? pixels[(y - 1) * width + x - 1] : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new InvalidDataException($"PNG filter {filter}"),
                };
                pixels[y * width + x] = (byte)(raw[y * (width + 1) + 1 + x] + predictor);
            }
        }

        return (pixels, palette);

        static int Paeth(int a, int b, int c)
        {
            int pa = Math.Abs(b - c), pb = Math.Abs(a - c), pc = Math.Abs(a + b - 2 * c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }
    }
}
