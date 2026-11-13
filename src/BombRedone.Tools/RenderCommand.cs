using System.Globalization;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.UI;
using BombRedone.UI.Screens;

namespace BombRedone.Tools;

/// <summary>
/// <c>bombtool render &lt;screen&gt; [gamedata] [out.png]</c>: draws a screen of the status/shop package with fixed sample
/// data (status screen: Longplay frame 00064; shop: short video frame TW 00100, drawn over the status
/// background; team status over the status background; game status window over a black playfield) and writes it as PNG. No window, no game round needed.
/// <c>bombtool render playfield [gamedata] [out.png] [--seed HEX] [--players N] [--cfg BOMB.CFG]</c> draws the playfield at
/// the start of a round (spur gelaende); <c>bombtool seed …</c> searches the start value behind a picture (<see cref="RunSeedSearch"/>).
/// </summary>
internal static class RenderCommand
{
    /// <summary>Screens that can be rendered.</summary>
    public static readonly IReadOnlyList<string> Screens = ["shop", "score", "team", "results", "status", "playfield"];

    public static int Run(string[] args)
    {
        if (args.Length > 0 && args[0] == "playfield")
            return RunPlayfield(args[1..]);
        if (args.Length is < 1 or > 3 || args[0] is "-h" or "--help" || !Screens.Contains(args[0]))
        {
            Console.Error.WriteLine($"Usage: bombtool render <{string.Join('|', Screens)}> [gamedata] [out.png]");
            return 2;
        }

        if (args[0] == "results")
        {
            // Text mode: printed, not drawn (O3:7566 writes to the DOS console).
            foreach (var line in ScoreResults.Build(
                [
                    new PlayerAccount(1, "Javier") { Wins = 8, Score = 41230 },
                    new PlayerAccount(2, "Lob & Shoot") { Wins = 2, Score = 900 },
                ], gamesCounter: 11))
                Console.WriteLine(line.Text);
            return 0;
        }

        GameData data;
        try
        {
            data = GameData.Locate(args.ElementAtOrDefault(1));
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var surface = new Surface();
        var dac = Render(args[0], data, surface);
        var output = args.ElementAtOrDefault(2) ?? $"{args[0]}.png";
        File.WriteAllBytes(output, ExportPng.Encode(new RawImage(surface.Pixels, Palette.FromBytes(dac))));
        Console.WriteLine($"{args[0]}: {Path.GetFullPath(output)}");
        return 0;
    }

    /// <summary>Draws <paramref name="screen"/> into <paramref name="surface"/>; returns the 6-bit DAC.</summary>
    internal static byte[] Render(string screen, GameData data, Surface surface)
    {
        var fonts = ShopFonts.Load(data, BitmapFontSubstitute.Create());
        switch (screen)
        {
            case "shop":
                var shop = SampleShop();
                shop.Update(TimeSpan.Zero, ScreenInput.None);
                shop.Draw(surface);
                return shop.Dac.ToArray();
            case "score":
                var score = SampleScore();
                score.Draw(surface);
                return score.Dac.ToArray();
            case "team":
                var team = SampleTeams();
                team.Draw(surface);
                return team.Dac.ToArray();
            case "status":
                var dac = new byte[768];
                MenuGraphics.DefaultColors.CopyTo(dac);
                var window = new StatusWindow(fonts.Default, new Surface(), dac, SampleStatus(), () => null);
                window.Draw(surface);
                return window.Dac.ToArray();
            default:
                throw new ArgumentOutOfRangeException(nameof(screen), screen, null);
        }

        ScoreScreen SampleScore() => new(fonts,
            [
                new ScoreLine(new PlayerAccount(1, "Javier") { Wins = 1, Score = 4260 }, (63, 0, 0)),
                new ScoreLine(new PlayerAccount(2, "Lob & Shoot") { Score = 0 }, (0, 63, 0)),
            ], 1, 10, buttons: true, (_, _, _) => null);

        ScoreTeamScreen SampleTeams()
        {
            var status = SampleScore();
            return new ScoreTeamScreen(fonts, status.Background, status.Dac,
                [
                    new Team(1, "Red Team", [new PlayerAccount(1, "Javier") { Wins = 1, Score = 4260 },
                        new PlayerAccount(3, "Ann") { Score = 1200 }]),
                    new Team(2, "Blue Team", [new PlayerAccount(2, "Lob & Shoot")]),
                ], 1, 10, buttons: true, (_, _, _) => null);
        }

        ShopScreen SampleShop()
        {
            var player = SamplePlayer();
            var status = SampleScore();
            var screen = new ShopScreen(fonts, status.Background, status.Dac, new Shop(player, ItemCatalog.Original),
                ShopControl.Keyboard, (63, 0, 0), () => null);
            // Cursor to "5 Meg Nuke" and select one bundle, as in TW 00100.
            screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)
            {
                Keys = [BiosKey.Down, BiosKey.Down, BiosKey.Down, BiosKey.Char('+')],
            });
            return screen;
        }
    }

    private const string PlayfieldUsage =
        "Usage: bombtool render playfield [gamedata] [out.png] [--seed HEX] [--players N] [--cfg BOMB.CFG]";

    /// <summary>
    /// Playfield of a round: <c>--seed</c> is RandSeed after the shuffle (hex, default 12345678), <c>--players</c> 2..10
    /// (default 2), options from <c>--cfg</c> or the defaults of BOMBCFG.EXE; prints RandSeed, wall, background, wind and
    /// tanks, writes the screen at the start of the round as PNG.
    /// </summary>
    private static int RunPlayfield(string[] args)
    {
        if (!TryParseOptions(args, out var positional, out var options) || positional.Count > 2
            || options.Keys.Any(k => k is not ("seed" or "players" or "cfg")))
        {
            Console.Error.WriteLine(PlayfieldUsage);
            return 2;
        }

        GameData data;
        GameConfig config;
        uint seed;
        int players;
        try
        {
            data = GameData.Locate(positional.ElementAtOrDefault(0));
            config = options.TryGetValue("cfg", out var cfg) ? GameConfig.Load(cfg) : GameConfig.BombCfgDefaults;
            seed = uint.Parse(options.GetValueOrDefault("seed", "12345678"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            players = int.Parse(options.GetValueOrDefault("players", "2"), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or GameConfigException or FormatException
                                       or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (!Core.GameLimits.IsValidPlayerCount(players))
        {
            Console.Error.WriteLine(PlayfieldUsage);
            return 2;
        }

        var screen = RenderPlayfield(data, config, seed, players, out var report);
        var output = positional.ElementAtOrDefault(1) ?? "playfield.png";
        File.WriteAllBytes(output, ExportPng.Encode(new RawImage(screen.Screen.Pixels, Palette.FromBytes(screen.Dac.ToArray()))));
        foreach (var line in report)
            Console.WriteLine(line);
        Console.WriteLine($"playfield: {Path.GetFullPath(output)}");
        return 0;
    }

    /// <summary>Builds the round from <paramref name="seed"/> (RandSeed after the shuffle) and draws it.</summary>
    internal static PlayfieldScreen RenderPlayfield(GameData data, GameConfig config, uint seed, int players,
        out IReadOnlyList<string> report)
    {
        var random = new TurboPascalRandom(seed);
        var field = Playfield.Build(PlayfieldOptions.FromConfig(config, players), random);
        var afterSetup = random.Seed;
        var wind = Wind.Choose((int)config.Wind, random);
        var fonts = MenuFonts.Load(data, BitmapFontSource.Load().Font);
        var names = Enumerable.Range(1, players).Select(PlayerSlotName).ToArray();
        report =
        [
            $"seed {seed:X8}, after set-up {afterSetup:X8}, after wind {random.Seed:X8}",
            $"wall {field.Wall}, background {field.BackgroundNumber} ({Backgrounds.Names[field.BackgroundNumber]}), " +
            $"wind {wind.Value} (level {wind.Level})",
            "tanks " + string.Join(" ", field.Tanks.Select(t => $"{t.X},{t.Y}")),
        ];
        return new PlayfieldScreen(fonts, field, wind, names);

        static string PlayerSlotName(int n) => Core.Setup.PlayerSlot.DefaultName(n);
    }

    private const string SeedUsage =
        "Usage: bombtool seed --time HH:MM:SS.cc --players N [--cfg BOMB.CFG] [--max-steps K] [--tanks X1,X2,...] " +
        "[--terrain X:Y,...]";

    /// <summary>
    /// <c>bombtool seed</c>: start value search (R2-D3, docs/analyse/runde.md section 4): RandSeed from the DOS time of
    /// Randomize (<c>--time</c>), then k = 0..K steps before the shuffle (default 2000); prints every k whose playfield
    /// has the tanks at <c>--tanks</c> (x from left to right) and the topmost terrain pixels <c>--terrain</c> (x:y).
    /// </summary>
    public static int RunSeedSearch(string[] args)
    {
        if (!TryParseOptions(args, out var positional, out var options) || positional.Count > 0
            || !options.ContainsKey("time") || !options.ContainsKey("players")
            || options.Keys.Any(k => k is not ("time" or "players" or "cfg" or "max-steps" or "tanks" or "terrain")))
        {
            Console.Error.WriteLine(SeedUsage);
            return 2;
        }

        PlayfieldOptions playfield;
        PlayfieldTarget target;
        TimeSpan time;
        int maxSteps;
        try
        {
            var config = options.TryGetValue("cfg", out var cfg) ? GameConfig.Load(cfg) : GameConfig.BombCfgDefaults;
            playfield = PlayfieldOptions.FromConfig(config, int.Parse(options["players"], CultureInfo.InvariantCulture));
            playfield.Validate();
            time = TimeSpan.ParseExact(options["time"], @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
            maxSteps = int.Parse(options.GetValueOrDefault("max-steps", "2000"), CultureInfo.InvariantCulture);
            target = new PlayfieldTarget(
                options.TryGetValue("tanks", out var tanks) ? tanks.Split(',').Select(ParseInt).ToArray() : null,
                options.TryGetValue("terrain", out var terrain)
                    ? terrain.Split(',').Select(p => p.Split(':')).Select(p => (ParseInt(p[0]), ParseInt(p[1]))).ToArray()
                    : null);
        }
        catch (Exception ex) when (ex is IOException or GameConfigException or FormatException or OverflowException
                                       or ArgumentException or IndexOutOfRangeException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var start = TurboPascalRandom.FromClock(DateTime.MinValue.Add(time)).Seed;
        Console.WriteLine($"RandSeed after Randomize {start:X8}, k = 0..{maxSteps}");
        var count = 0;
        foreach (var c in SeedSearch.Search(start, maxSteps, playfield, target))
        {
            Console.WriteLine($"k {c.StepsBeforeShuffle}: before shuffle {c.SeedBeforeShuffle:X8}, after {c.SeedAfterShuffle:X8}");
            count++;
        }

        Console.WriteLine($"{count} candidate(s)");
        return count > 0 ? 0 : 1;

        static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    }

    /// <summary>Splits arguments into positional ones and <c>--name value</c> pairs.</summary>
    private static bool TryParseOptions(string[] args, out List<string> positional, out Dictionary<string, string> options)
    {
        positional = [];
        options = [];
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "-h" or "--help")
                return false;
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                positional.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Length)
                return false;
            options[args[i][2..]] = args[++i];
        }

        return true;
    }

    /// <summary>Values of the status window: defaults of the game options, game 3 of 10.</summary>
    internal static StatusWindowData SampleStatus() => new(new OptionsSettings(), SoundEffects: true, FlySound: false,
        Game: 3, Games: 10, WallSetting.None, WindSetting.Normal);

    /// <summary>Player of TW 00100: "ZZZ" with 12930 dollars and the stock shown in the frame.</summary>
    internal static PlayerAccount SamplePlayer()
    {
        var player = new PlayerAccount(1, "ZZZ") { Score = 12930 };
        player.SetStock(ItemKind.Weapon, 1, 8);
        player.SetStock(ItemKind.Weapon, 2, 7);
        player.SetStock(ItemKind.Weapon, 3, 1);
        player.SetStock(ItemKind.Weapon, 5, 1000);
        player.SetStock(ItemKind.Weapon, 6, 3);
        return player;
    }
}
