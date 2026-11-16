using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Tests.Turn;

/// <summary>
/// Turn start Z1, the turn O7:040C with O7:0EF7, tank info, target selection and MIRV dialog against the original code run
/// in the probe (docs/analyse/zug.md). Expected values from <c>python3 -I tools/reverse-engineering/zug.py hashes</c>:
/// the playfield of a feld.py case up to the start player, then one turn fed with the same input steps. Compared: status
/// and steps used, every command, every delay, the wait-for-release loops passed, the poll sites, the shooter's record,
/// the MIRV choice, every written DAC entry and frame and field of the screen.
/// </summary>
public sealed class TurnProbeTests
{
    public sealed record TurnCase(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, int Shooter, int Control, string Sets,
        string Steps, string Options, string Status, int StepsUsed, string Commands, string Delays, string Releases,
        string Polls, string ShooterState, string Mirv, string ScreenField, string Dac)
    {
        public override string ToString() => $"turn {Nr}";
    }

    public static readonly TheoryData<TurnCase> Cases = TurnProbeData.Cases;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Turn_matches_the_probe(TurnCase c)
    {
        var run = TurnHarness.Run(c);

        Assert.Equal(c.Status, run.Status);
        Assert.Equal(c.StepsUsed, run.StepsUsed);
        Assert.Equal(c.Polls, run.Polls);
        Assert.Equal(c.Commands, run.Commands);
        Assert.Equal(c.Delays, run.Delays);
        Assert.Equal(c.Releases, run.Releases);
        Assert.Equal(c.ShooterState, run.ShooterState);
        Assert.Equal(c.Mirv, run.Mirv);
        Assert.Equal(c.Dac, run.Dac);
        if (!c.Options.Contains("mirv", StringComparison.Ordinal))
            Assert.Equal(c.ScreenField, run.ScreenField);
    }

    [Fact]
    public void The_probe_list_covers_every_command_and_poll_site()
    {
        var commands = TurnProbeData.All.SelectMany(c => c.Commands.Split(',')).Select(int.Parse).ToHashSet();
        Assert.Equal(Enumerable.Range(0, 11).ToHashSet(), commands);
        var sites = string.Join(",", TurnProbeData.All.Select(c => c.Polls));
        foreach (var site in TurnHarness.SiteNames.Values)
            Assert.Contains(site, sites, StringComparison.Ordinal);
    }
}

/// <summary>Drives a <see cref="TurnSession"/> with the input model of the probe (docs/analyse/zug.md section 2).</summary>
internal static class TurnHarness
{
    public sealed record Result(
        string Status, int StepsUsed, string Commands, string Delays, string Releases, string Polls, string ShooterState,
        string Mirv, string ScreenField, string Dac);

    public static readonly IReadOnlyDictionary<TurnPollSite, string> SiteNames = new Dictionary<TurnPollSite, string>
    {
        [TurnPollSite.Input] = "0EF7",
        [TurnPollSite.Cheat] = "cheat",
        [TurnPollSite.InfoKey] = "info key",
        [TurnPollSite.InfoMouse] = "info mouse",
        [TurnPollSite.TargetKey] = "target key",
        [TurnPollSite.TargetMouse] = "target mouse",
        [TurnPollSite.MirvKey1] = "mirv key 1",
        [TurnPollSite.MirvMouse1] = "mirv mouse 1",
        [TurnPollSite.MirvKey2] = "mirv key 2",
        [TurnPollSite.MirvMouse2] = "mirv mouse 2",
    };

    private static readonly Dictionary<string, int> Keys = new()
    {
        ["Esc"] = Scancode.Esc, ["0"] = Scancode.Digit0, ["Tab"] = Scancode.Tab, ["W"] = Scancode.W,
        ["Enter"] = Scancode.Enter, ["D"] = Scancode.D, ["G"] = Scancode.G, ["H"] = Scancode.H, ["I"] = Scancode.I,
        ["LShift"] = Scancode.LeftShift, ["C"] = Scancode.C, ["V"] = Scancode.V, ["RShift"] = Scancode.RightShift,
        ["Alt"] = Scancode.Alt, ["Space"] = Scancode.Space, ["KpUp"] = Scancode.PadUp, ["KpPgUp"] = Scancode.PadPageUp,
        ["KpLeft"] = Scancode.PadLeft, ["KpRight"] = Scancode.PadRight, ["KpDown"] = Scancode.PadDown,
        ["KpPgDn"] = Scancode.PadPageDown, ["KpIns"] = Scancode.PadInsert, ["KpDel"] = Scancode.PadDelete,
        ["KpEnter"] = Scancode.PadEnter, ["RAlt"] = Scancode.RightAlt, ["Up"] = Scancode.Up, ["PgUp"] = Scancode.PageUp,
        ["Left"] = Scancode.Left, ["Right"] = Scancode.Right, ["Down"] = Scancode.Down, ["PgDn"] = Scancode.PageDown,
        ["Ins"] = Scancode.Insert, ["Del"] = Scancode.Delete,
    };

    private static readonly Dictionary<string, (byte Ascii, byte Scan)> Bios = new()
    {
        ["Enter"] = (13, 0x1C), ["Space"] = (32, 0x39), ["Esc"] = (27, 0x01), ["Right"] = (0, 0x4D), ["Left"] = (0, 0x4B),
        ["Up"] = (0, 0x48), ["Down"] = (0, 0x50), ["A"] = ((byte)'a', 0x1E), ["X"] = ((byte)'x', 0x2D),
    };

    static TurnHarness()
    {
        for (var n = 1; n <= 9; n++)
            Keys[n.ToString(CultureInfo.InvariantCulture)] = Scancode.Digit1 + n - 1;
    }

    public static Result Run(TurnProbeTests.TurnCase c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, false), random);
        Wind.Choose(c.WindOption, random);
        StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);

        var players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
        foreach (var p in players)
            p.SetDefaultStocks();
        foreach (var set in c.Sets == "-" ? [] : c.Sets.Split(' '))
            Apply(players, c.Shooter, set);

        var control = (PlayerControl)c.Control;
        var input = new TurnInput { MousePresent = control == PlayerControl.Mouse, MouseX = 160, MouseY = 100 };
        var session = new TurnSession(field, players, input,
            new TurnSettings(Sound: c.Options.Contains("sound", StringComparison.Ordinal)));
        var steps = c.Steps.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var used = 0;
        var polls = new List<string>();
        var delays = new List<string>();
        var status = "ok";

        field.Palette.ClearWritten();
        session.Begin(c.Shooter);
        var waits = session.Play(c.Shooter, control);
        if (c.Options.Contains("mirv", StringComparison.Ordinal))
            waits = waits.Concat(session.MirvSetup(c.Shooter, control));
        foreach (var w in waits)
        {
            if (w.Kind == TurnWaitKind.Delay)
                delays.Add(w.Value.ToString(CultureInfo.InvariantCulture));
            if (w.Kind != TurnWaitKind.Poll)
                continue;
            polls.Add(SiteNames[w.Site]);
            if (used >= steps.Length)
            {
                status = "exhausted at " + SiteNames[w.Site];
                break;
            }

            ApplyStep(input, steps[used++]);
        }

        var shooter = players[c.Shooter - 1];
        return new Result(
            status, used, string.Join(",", session.Commands.Select(x => (int)x)), Compress(delays) is "" ? "-" : Compress(delays),
            input.Releases.Count == 0 ? "-" : string.Join(",", input.Releases), Compress(polls), State(shooter),
            session.MirvChoice is { } m ? $"{m.Pattern},{m.Detonation}" : "-", FieldHash(field), DacText(field));
    }

    private static void ApplyStep(TurnInput input, string step)
    {
        input.ReleaseAll();
        var mouse = false;
        foreach (var part in step.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "-")
                continue;
            if (part.StartsWith("m:", StringComparison.Ordinal))
            {
                var v = part[2..].Split(',').Select(int.Parse).ToArray();
                (input.MouseX, input.MouseY, input.MouseButtons) = (v[0], v[1], v[2]);
                mouse = true;
            }
            else if (part.StartsWith("b:", StringComparison.Ordinal))
            {
                var (a, s) = Bios[part[2..]];
                input.TypeKey(a, s);
            }
            else
            {
                foreach (var key in part.Split('+'))
                    input[Keys[key]] = true;
            }
        }

        if (!mouse)
            input.MouseButtons = 0;
    }

    internal static void Apply(List<TurnPlayer> players, int shooter, string set)
    {
        var (who, rest) = set.Contains(':', StringComparison.Ordinal)
            ? (int.Parse(set[..set.IndexOf(':', StringComparison.Ordinal)], CultureInfo.InvariantCulture),
                set[(set.IndexOf(':', StringComparison.Ordinal) + 1)..])
            : (shooter, set);
        var name = rest[..rest.IndexOf('=', StringComparison.Ordinal)];
        var value = int.Parse(rest[(rest.IndexOf('=', StringComparison.Ordinal) + 1)..], CultureInfo.InvariantCulture);
        var p = players[who - 1];
        var t = p.Tank;
        switch (name)
        {
            case "power": SetInternal(t, nameof(t.Power), value); break;
            case "angle": SetInternal(t, nameof(t.Angle), value); break;
            case "dir": SetInternal(t, nameof(t.Direction), value); break;
            case "energy": SetInternal(t, nameof(t.Energy), value); break;
            case "alive": SetInternal(t, nameof(t.Active), value != 0); break;
            case "weapon": t.Weapon = value; break;
            case "herb": t.Herbs = unchecked((short)value); break;
            case "vick": t.Vicks = unchecked((short)value); break;
            case "shield": t.Defense[0] = value; break;
            default:
                if (!name.StartsWith("stock", StringComparison.Ordinal))
                    throw new ArgumentException(name);
                t.Stock[int.Parse(name[5..], CultureInfo.InvariantCulture)] = unchecked((short)value);
                break;
        }
    }

    /// <summary>Record fields with internal setters (written by the probe with --set, by the game flow later).</summary>
    internal static void SetInternal(PlayfieldTank tank, string property, object value) =>
        typeof(PlayfieldTank).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!
            .GetSetMethod(nonPublic: true)!.Invoke(tank, [value]);

    private static string State(TurnPlayer p)
    {
        var t = p.Tank;
        return string.Create(CultureInfo.InvariantCulture,
            $"{t.Angle},{t.Power},{t.Direction},{t.Energy},{t.Weapon},{t.Guidance},{t.GuidanceTarget},{t.Herbs},"
            + $"{t.Vicks},{t.Field5E},{p.Turns};{string.Join(",", t.Stock)}");
    }

    /// <summary>Runs of equal values as "v" or "vxN", like compress() of zug.py.</summary>
    internal static string Compress(IReadOnlyList<string> seq)
    {
        var parts = new List<string>();
        for (var i = 0; i < seq.Count;)
        {
            var j = i;
            while (j < seq.Count && seq[j] == seq[i])
                j++;
            parts.Add(j - i == 1 ? seq[i] : $"{seq[i]}x{j - i}");
            i = j;
        }

        return string.Join(",", parts);
    }

    private static string FieldHash(Core.Playfield.Playfield field)
    {
        var crop = new byte[305 * 165];
        for (var y = 28; y <= 192; y++)
            field.Screen.Pixels.AsSpan(y * 320 + 8, 305).CopyTo(crop.AsSpan((y - 28) * 305));
        return Convert.ToHexStringLower(SHA256.HashData(crop));
    }

    private static string DacText(Core.Playfield.Playfield field)
    {
        var e = field.Palette.WrittenEntries();
        var parts = new List<string>();
        for (var k = 0; k < e.Length; k += 4)
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{e[k]}:{e[k + 1]},{e[k + 2]},{e[k + 3]}"));
        return parts.Count == 0 ? "-" : string.Join(" ", parts);
    }
}
