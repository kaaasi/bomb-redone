using System.IO.Compression;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Round 1 of DOSBox session 3 (spur gelaende-bilder, docs/analyse/feld.md section 6) and the screenshots of the reference
/// repository for the session tests (PlayfieldScreenSessionTests, PlayfieldShotSessionTests). Pictures are never committed
/// (AGENTS.md 2.1).
/// </summary>
internal static class PlayfieldSession
{
    /// <summary>RandSeed before the shuffle: Randomize at 17:12:39.69, one menu step (feld.md 6.1).</summary>
    internal const uint SeedBeforeShuffle = 0x4983853D;

    /// <summary>
    /// The round from the start value: shuffle with the swaps of the records (O3:2417, here only to know which name ends in
    /// which record), set-up, wind. The header name is the one of the last record, colour 19 + N (O8:0984 P12).
    /// </summary>
    internal static PlayfieldScreen CreateScreen() => CreateScreen(out _);

    /// <inheritdoc cref="CreateScreen()"/>
    /// <param name="random">The generator after the wind, for the rest of the round.</param>
    internal static PlayfieldScreen CreateScreen(out TurboPascalRandom random)
    {
        var fontPath = BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories())
            ?? throw new InvalidOperationException(
                "Reference font missing (tools/claude/fetch-reference.sh fetches schriften/ to ~/referenz).");
        var fonts = MenuFonts.Load(TestGameData.Load(), BitmapFontSource.ReadReference(fontPath));
        random = new TurboPascalRandom(SeedBeforeShuffle);
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
    internal static int[] ShuffleOrder(TurboPascalRandom random, int players)
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

    internal static byte[] Rgb8(ReadOnlySpan<byte> rgb6) => rgb6.ToArray().Select(v => (byte)((v << 2) | (v >> 4))).ToArray();

    /// <summary>
    /// Finds the picture: <c>BOMB_BILDER</c> (folder of the pictures of runde-1), <c>bilder/runde-1</c> next to the folder
    /// that holds the (resolved) <c>gamedata</c>, or <c>~/referenz/bilder/runde-1</c>.
    /// </summary>
    internal static string PicturePath(string pictureName)
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
            var path = Path.Combine(dir!, pictureName);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"{pictureName} not found (BOMB_BILDER, bilder/runde-1 of the reference, ~/referenz/bilder/runde-1). " +
            "Load it with tools/claude/fetch-reference.sh bilder/runde-1.");
    }

    /// <summary>Minimal PNG reader for the DOSBox screenshots: 8-bit indexed, no interlace; returns indices and PLTE.</summary>
    internal static (byte[] Pixels, byte[] Palette) ReadPicture(string path)
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
