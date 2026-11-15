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
        "Usage: bombtool render playfield [gamedata] [out.png] [--seed HEX] [--players N] [--cfg BOMB.CFG] " +
        "[--shot ANGLE,POWER[,WEAPON[,PATTERN[,DETONATION]]]] [--guidance KIND,TARGET] [--shooter N]";

    /// <summary>
    /// Playfield of a round: <c>--seed</c> is RandSeed after the shuffle (hex, default 12345678), <c>--players</c> 2..10
    /// (default 2), options from <c>--cfg</c> or the defaults of BOMBCFG.EXE; prints RandSeed, wall, background, wind and
    /// tanks, writes the screen at the start of the round as PNG. With <c>--shot</c> the player <c>--shooter</c> (default:
    /// the start player) fires once with the options of the configuration (SS, Trail, Sound, Crumble, FallDown), and the PNG
    /// shows the field afterwards (spur schuss). WEAPON 0..17 (ItemCatalog.WeaponTable, default 0 Lead Ball), for the MIRV
    /// (7) PATTERN 0..4 (Random, Horizontal, Vertical, + 4 Way, X 4 Way) and DETONATION 0..2 (Impact, Simultanious, None);
    /// <c>--guidance</c> KIND 1 Herb, 2 Vick, 3 both with the TARGET player (spur waffen). [APPROX] Without the accounts of
    /// a game the stock is set like the probe: the power for the laser, at least 1 for the other weapons, Herb and Vick.
    /// </summary>
    private static int RunPlayfield(string[] args)
    {
        if (!TryParseOptions(args, out var positional, out var options) || positional.Count > 2
            || options.Keys.Any(k => k is not ("seed" or "players" or "cfg" or "shot" or "shooter" or "guidance")))
        {
            Console.Error.WriteLine(PlayfieldUsage);
            return 2;
        }

        GameData data;
        GameConfig config;
        uint seed;
        int players;
        int[]? shot = null;
        int[]? guidance = null;
        int? shooter = null;
        try
        {
            if (options.TryGetValue("shot", out var shotText))
                shot = shotText.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            if (options.TryGetValue("guidance", out var guidanceText))
                guidance = guidanceText.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            if (options.TryGetValue("shooter", out var shooterText))
                shooter = int.Parse(shooterText, CultureInfo.InvariantCulture);
            seed = uint.Parse(options.GetValueOrDefault("seed", "12345678"), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            players = int.Parse(options.GetValueOrDefault("players", "2"), CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        // Usage errors come before the game data is looked for (the CI has none).
        if (!Core.GameLimits.IsValidPlayerCount(players) || shot is not (null or { Length: >= 2 and <= 5 })
            || shot is not null && (shot[0] is < 0 or > 180 || shot.ElementAtOrDefault(2) is < 0 or > 17
                                    || shot.ElementAtOrDefault(3) is < 0 or > 4 || shot.ElementAtOrDefault(4) is < 0 or > 2)
            || shooter is not null && (shot is null || shooter < 1 || shooter > players)
            || guidance is not null && (shot is null || guidance.Length != 2 || guidance[0] is < 1 or > 3
                                        || guidance[1] < 1 || guidance[1] > players))
        {
            Console.Error.WriteLine(PlayfieldUsage);
            return 2;
        }

        try
        {
            data = GameData.Locate(positional.ElementAtOrDefault(0));
            config = options.TryGetValue("cfg", out var cfg) ? GameConfig.Load(cfg) : GameConfig.BombCfgDefaults;
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or GameConfigException or FormatException
                                       or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var screen = RenderPlayfield(data, config, seed, players, out var report, shot, shooter, guidance);
        var output = positional.ElementAtOrDefault(1) ?? "playfield.png";
        File.WriteAllBytes(output, ExportPng.Encode(new RawImage(screen.Screen.Pixels, Palette.FromBytes(screen.Dac.ToArray()))));
        foreach (var line in report)
            Console.WriteLine(line);
        Console.WriteLine($"playfield: {Path.GetFullPath(output)}");
        return 0;
    }

    /// <summary>Builds the round from <paramref name="seed"/> (RandSeed after the shuffle) and draws it.</summary>
    internal static PlayfieldScreen RenderPlayfield(GameData data, GameConfig config, uint seed, int players,
        out IReadOnlyList<string> report, int[]? shot = null, int? shooter = null, int[]? guidance = null)
    {
        var random = new TurboPascalRandom(seed);
        var field = Playfield.Build(PlayfieldOptions.FromConfig(config, players), random);
        var afterSetup = random.Seed;
        var wind = Wind.Choose((int)config.Wind, random);
        var fonts = MenuFonts.Load(data, BitmapFontSource.Load().Font);
        var names = Enumerable.Range(1, players).Select(PlayerSlotName).ToArray();
        var lines = new List<string>
        {
            $"seed {seed:X8}, after set-up {afterSetup:X8}, after wind {random.Seed:X8}",
            $"wall {field.Wall}, background {field.BackgroundNumber} ({Backgrounds.Names[field.BackgroundNumber]}), " +
            $"wind {wind.Value} (level {wind.Level})",
            "tanks " + string.Join(" ", field.Tanks.Select(t => $"{t.X},{t.Y}")),
        };
        report = lines;
        var screen = new PlayfieldScreen(fonts, field, wind, names);
        if (shot is null)
            return screen;

        // O4:479A: start player, then one shot like the turn of that player (spur schuss).
        var start = StartPlayer.Choose(new int[players], Enumerable.Repeat(true, players).ToArray(), random);
        var tank = field.Tanks[(shooter ?? start.Player) - 1];
        tank.Aim(shot[0], shot[1]);
        tank.Weapon = shot.ElementAtOrDefault(2);
        tank.Stock[tank.Weapon] = tank.Weapon == 5 ? shot[1] : Math.Max(1, tank.Stock[tank.Weapon]);
        if (guidance is not null)
        {
            tank.Guidance = guidance[0];
            tank.GuidanceTarget = guidance[1];
            tank.Herbs = Math.Max(1, tank.Herbs);
            tank.Vicks = Math.Max(1, tank.Vicks);
        }

        var settings = new Core.Shot.ShotSettings(config.ShotSpeed, wind.Value, config.ShotTrail, config.SoundEffects,
            Crumble: config.CrumblePercent);
        var result = Core.Shot.WeaponShot.Fire(field, tank, settings, random, (Core.Shot.MirvPattern)shot.ElementAtOrDefault(3),
            (Core.Shot.MirvDetonation)shot.ElementAtOrDefault(4));
        Core.Shot.AfterShot.Run(field, config.CrumblePercent, config.TanksFall, random);
        screen.RefreshField();
        var end = result is null
            ? "no flight path"
            : $"end {result.Ix},{result.Iy}, hit {result.Hit}, {(result.Exploded ? "impact" : "no impact")}, steps {result.Steps}";
        lines.Add($"shot player {tank.Number}: angle {shot[0]}, power {shot[1]}, weapon {tank.Weapon}; {end}, RandSeed {random.Seed:X8}");
        lines.Add("after: " + string.Join(" ", field.Tanks.Select(t => $"{t.X},{t.Y} energy {t.Energy} points {t.Score}")));
        return screen;

        static string PlayerSlotName(int n) => Core.Setup.PlayerSlot.DefaultName(n);
    }

    private const string SeedUsage =
        "Usage: bombtool seed --players N (--time HH:MM:SS.cc | [--time-from HH:MM:SS.cc] [--time-to HH:MM:SS.cc]) " +
        "[--cfg BOMB.CFG] [--max-steps K] [--tanks X1,X2,...] [--terrain X:Y,...] [--background B] [--wind V]";

    /// <summary>
    /// <c>bombtool seed</c>: start value search (R2-D3, docs/analyse/runde.md section 4): RandSeed from the DOS time of
    /// Randomize, either one time (<c>--time</c>) or every hundredth of a window (<c>--time-from</c>/<c>--time-to</c>; a
    /// missing end means midnight or 23:59:59.99, so <c>--time-from 00:00:00.00</c> alone searches the whole day,
    /// Review #123 P2-1); then
    /// k = 0..K steps before the shuffle (default 2000 for one time, 30 for a window). Cheap prefilters in the order of the
    /// Random calls (<see cref="PlayfieldPrefilter"/>): background <c>--background</c>, height of the left margin (from the
    /// <c>--terrain</c> points inside it), first and last tank of <c>--tanks</c>, wind <c>--wind</c> (needs the wind option
    /// of the configuration); then the playfield against all tanks and terrain points. Prints every match.
    /// </summary>
    public static int RunSeedSearch(string[] args)
    {
        if (!TryParseOptions(args, out var positional, out var options) || positional.Count > 0
            || !options.ContainsKey("players")
            || !options.ContainsKey("time") && !options.ContainsKey("time-from") && !options.ContainsKey("time-to")
            || options.ContainsKey("time") && (options.ContainsKey("time-from") || options.ContainsKey("time-to"))
            || options.Keys.Any(k => k is not ("time" or "time-from" or "time-to" or "players" or "cfg" or "max-steps"
                or "tanks" or "terrain" or "background" or "wind")))
        {
            Console.Error.WriteLine(SeedUsage);
            return 2;
        }

        PlayfieldOptions playfield;
        PlayfieldTarget target;
        PlayfieldPrefilter prefilter;
        DosTime from, to;
        int maxSteps, windOption;
        try
        {
            var config = options.TryGetValue("cfg", out var cfg) ? GameConfig.Load(cfg) : GameConfig.BombCfgDefaults;
            windOption = (int)config.Wind;
            playfield = PlayfieldOptions.FromConfig(config, int.Parse(options["players"], CultureInfo.InvariantCulture));
            playfield.Validate();
            if (options.TryGetValue("time", out var time))
            {
                from = to = ParseTime(time);
            }
            else
            {
                from = options.TryGetValue("time-from", out var f) ? ParseTime(f) : new DosTime(0, 0, 0, 0);
                to = options.TryGetValue("time-to", out var t) ? ParseTime(t) : new DosTime(23, 59, 59, 99);
                if (to.Ticks < from.Ticks)
                    throw new ArgumentException("--time-to lies before --time-from.");
            }

            maxSteps = int.Parse(options.GetValueOrDefault("max-steps", from == to ? "2000" : "30"),
                CultureInfo.InvariantCulture);
            ArgumentOutOfRangeException.ThrowIfNegative(maxSteps);
            var tanks = options.TryGetValue("tanks", out var tanksText) ? tanksText.Split(',').Select(ParseInt).ToArray() : null;
            var terrain = options.TryGetValue("terrain", out var terrainText)
                ? terrainText.Split(',').Select(p => p.Split(':')).Select(p => (ParseInt(p[0]), ParseInt(p[1]))).ToArray()
                : null;
            target = new PlayfieldTarget(tanks, terrain);
            prefilter = BuildPrefilter(playfield.Players, tanks, terrain,
                options.TryGetValue("background", out var b) ? ParseInt(b) : null,
                options.TryGetValue("wind", out var w) ? ParseInt(w) : null);
        }
        catch (Exception ex) when (ex is IOException or GameConfigException or FormatException or OverflowException
                                       or ArgumentException or IndexOutOfRangeException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<ClockCandidate> found;
        if (from == to)
        {
            var start = from.Seed;
            Console.WriteLine($"RandSeed after Randomize {start:X8}, k = 0..{maxSteps}");
            found = SeedSearch.Search(start, maxSteps, playfield, target)
                .Where(c => prefilter.Passes(c.SeedAfterShuffle, playfield, windOption))
                .Select(c => new ClockCandidate(from, c)).ToList();
        }
        else
        {
            Console.WriteLine($"time {from}..{to}, k = 0..{maxSteps}");
            // One chunk per second of the window, searched in parallel; the order of the output is sorted afterwards.
            var bag = new System.Collections.Concurrent.ConcurrentBag<ClockCandidate>();
            var first = from.Ticks / 100;
            var last = to.Ticks / 100;
            Parallel.For(first, last + 1, second =>
            {
                var a = DosTime.FromTicks(Math.Max(from.Ticks, second * 100));
                var z = DosTime.FromTicks(Math.Min(to.Ticks, second * 100 + 99));
                foreach (var c in SeedSearch.SearchWindow(a, z, maxSteps, playfield, windOption, prefilter, target))
                    bag.Add(c);
            });
            found = bag.OrderBy(c => c.Time.Ticks).ThenBy(c => c.Candidate.StepsBeforeShuffle).ToList();
        }

        foreach (var c in found)
        {
            Console.WriteLine($"{c.Time} k {c.Candidate.StepsBeforeShuffle}: before shuffle {c.Candidate.SeedBeforeShuffle:X8}, " +
                              $"after {c.Candidate.SeedAfterShuffle:X8}");
        }

        Console.WriteLine($"{found.Count} candidate(s), {stopwatch.Elapsed.TotalSeconds:F1} s");
        return found.Count > 0 ? 0 : 1;

        static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        static DosTime ParseTime(string text)
        {
            var span = TimeSpan.ParseExact(text, @"hh\:mm\:ss\.ff", CultureInfo.InvariantCulture);
            return new DosTime(span.Hours, span.Minutes, span.Seconds, span.Milliseconds / 10);
        }
    }

    /// <summary>
    /// Prefilter from the target: tanks 1 and N, the height of the flat left margin from the terrain points inside it
    /// (x = 11 .. 70 − 7 (N − 2), y &lt; 189, all must agree), background and wind as given.
    /// </summary>
    internal static PlayfieldPrefilter BuildPrefilter(int players, IReadOnlyList<int>? tanks,
        IReadOnlyList<(int X, int Y)>? terrain, int? background, int? wind)
    {
        if (tanks is not null && tanks.Count != players)
            throw new ArgumentException("--tanks needs one x per player.");
        if (background is < 0 or > 8)
            throw new ArgumentException("--background is 0..8.");
        var marginEnd = 60 - (players - 2) * 7 + 11;
        // y = 189 is also the floor line, so it does not tell h = 0 from h = 1.
        var heights = (terrain ?? []).Where(p => p.X >= 11 && p.X < marginEnd && p.Y < 189).Select(p => 190 - p.Y).Distinct()
            .ToArray();
        if (heights.Length > 1)
            throw new ArgumentException("The terrain points of the left margin differ in height.");
        return new PlayfieldPrefilter(background, heights.Length == 1 ? heights[0] : null, tanks?[0], tanks?[^1], wind);
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
