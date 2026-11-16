using System.Globalization;
using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Shot;
using BombRedone.Core.Tests.Turn;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Tests.Round;

/// <summary>
/// The round loop O4:44A7 against the original code run in the probe (spur runde, docs/analyse/feld.md section 9). Expected
/// values from <c>python3 -I tools/reverse-engineering/tod.py runden</c> and <c>runden-cs</c>: the playfield of a feld.py
/// case up to the start player, the fields of <c>--set</c>, then the round with the turn results of the script. At every
/// measuring point: kind, RandSeed, frame and field of the screen, background buffer, DAC entries written since the start
/// player, shot log, pass, player to move, winner and the record fields.
/// </summary>
public sealed class RoundProbeTests
{
    public sealed record RoundCase(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, bool Invisible, bool NoFall, string Turns,
        string? Sets, bool Sound, int? Crumble, int? Pass, string? DeathSeeds, string Status, string Start, int SubSteps,
        bool Trail, int Wind)
    {
        public override string ToString() => $"round {Nr}";
    }

    public sealed record ProbePoint(
        int Nr, int Index, string Kind, int Turn, int? Victim, int? Choice, int Pass, int ToMove, int Winner, string Seed,
        string Field, string Background, string DacSet, string ShotLog, string Records);

    /// <summary>The default Crumble of the probe's BOMB.CFG (feld.py DEFAULT_CFG).</summary>
    private const int DefaultCrumble = 75;

    public static TheoryData<RoundCase> Cases
    {
        get
        {
            var data = new TheoryData<RoundCase>();
            foreach (var c in RoundProbeData.Cases)
                data.Add(c);
            return data;
        }
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string FieldHash(Core.Playfield.Playfield field)
    {
        var crop = new byte[305 * 165];
        for (var y = 28; y <= 192; y++)
            field.Screen.Pixels.AsSpan(y * 320 + 8, 305).CopyTo(crop.AsSpan((y - 28) * 305));
        return Sha(crop);
    }

    private static string Kind(RoundPointKind kind) => kind switch
    {
        RoundPointKind.Turn => "turn",
        RoundPointKind.AfterShot => "post",
        RoundPointKind.Death => "death",
        RoundPointKind.Lightning => "bolt",
        RoundPointKind.Continue => "next",
        RoundPointKind.End => "end",
        RoundPointKind.Exhausted => "exhausted",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Records(IReadOnlyList<TurnPlayer> players) => string.Join(" ", players.Select(p =>
    {
        var t = p.Tank;
        int[] v =
        [
            t.X, t.Y, t.Energy, t.Active ? 1 : 0, t.Score, t.Defense[Damage.Shield], t.Defense[Damage.Inertia],
            t.Defense[Damage.Repulser], t.Field5E, t.Hits, t.Kills, p.Turns, t.RoundSum, t.Weapon,
        ];
        return string.Join("/", v.Select(x => x.ToString(CultureInfo.InvariantCulture)));
    }));

    /// <summary>--set FIELD=V of tod.py on the record (fields beyond <see cref="TurnHarness.Apply"/>: team).</summary>
    private static void Apply(List<TurnPlayer> players, int start, string set)
    {
        if (set.Contains("team=", StringComparison.Ordinal))
        {
            var who = set.Contains(':', StringComparison.Ordinal)
                ? int.Parse(set[..set.IndexOf(':', StringComparison.Ordinal)], CultureInfo.InvariantCulture)
                : start;
            players[who - 1].Tank.Team = int.Parse(set[(set.IndexOf('=', StringComparison.Ordinal) + 1)..],
                CultureInfo.InvariantCulture);
            return;
        }

        TurnHarness.Apply(players, start, set);
    }

    /// <summary>Runs the round like the probe; stops at a picture or lightning that is not ported (none since part 3).</summary>
    internal static (List<ProbePoint> Points, bool Complete) Run(RoundCase c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, c.Invisible), random);
        var wind = Wind.Choose(c.WindOption, random);
        var start = StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);
        Assert.Equal(c.Start, $"{start.Player}/{random.Seed:X8}");
        Assert.Equal(c.Wind, wind.Value);

        var players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
        foreach (var p in players)
            p.SetDefaultStocks();
        foreach (var set in (c.Sets ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            Apply(players, start.Player, set);
        field.Palette.ClearWritten();

        var settings = new RoundSettings(c.SubSteps, wind.Value, c.Crumble ?? DefaultCrumble, !c.NoFall, c.Sound, c.Trail);
        var session = new TurnSession(field, players, new TurnInput(), new TurnSettings(Sound: c.Sound));
        var round = new RoundPlay(field, players, session, settings, random, start);
        if (c.Pass is { } pass)
            round.Pass = pass;
        var turns = new Queue<string>(c.Turns.Split(';', StringSplitOptions.RemoveEmptyEntries));
        var seeds = new Queue<uint>((c.DeathSeeds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Convert.ToUInt32(s, 16)));

        TurnOutcome? Turn(TurnPlayer player)
        {
            if (turns.Count == 0)
                return null;
            var v = turns.Dequeue().Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var t = player.Tank;
            t.Aim(v[0], v[1]);
            t.Weapon = v.Length > 2 ? v[2] : 0;
            t.Guidance = v.Length > 4 ? v[3] : 0;
            if (t.Guidance != 0)
            {
                t.GuidanceTarget = v[4];
                if ((t.Guidance & 1) != 0)
                    t.Herbs = Math.Max(1, t.Herbs);
                if ((t.Guidance & 2) != 0)
                    t.Vicks = Math.Max(1, t.Vicks);
            }

            t.Stock[t.Weapon] = Math.Max(t.Weapon == TurnPlayer.Laser ? t.Power : 1, t.Stock[t.Weapon]);
            return v.Length > 6 ? new TurnOutcome((MirvPattern)v[5], (MirvDetonation)v[6]) : new TurnOutcome();
        }

        var points = new List<ProbePoint>();
        using var e = round.Play(Turn).GetEnumerator();
        while (true)
        {
            bool more;
            try
            {
                more = e.MoveNext();
            }
            catch (NotSupportedException)
            {
                return (points, false);
            }

            if (!more)
                return (points, true);
            var point = e.Current;
            if (point.Kind == RoundPointKind.DeathStart)
            {
                if (seeds.Count > 0)
                    random.SetSeed(seeds.Dequeue());
                continue;
            }

            points.Add(new ProbePoint(c.Nr, points.Count, Kind(point.Kind), point.Turn,
                point.Kind == RoundPointKind.Death ? point.Player : null,
                point.Kind == RoundPointKind.Death ? point.Choice : null, round.Pass, round.ToMove, round.WinnerTeam,
                random.Seed.ToString("X8"), FieldHash(field), Sha(field.Background.Pixels), Sha(field.Palette.WrittenEntries()),
                Sha(round.Log.Bytes), Records(players)));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Round_matches_the_original_at_every_measuring_point(RoundCase c)
    {
        var expected = RoundProbeData.Points.Where(p => p.Nr == c.Nr).ToList();
        var (points, complete) = Run(c);

        if (complete)
            Assert.Equal(expected.Count, points.Count);
        Assert.True(points.Count <= expected.Count, $"{points.Count} points, the probe has {expected.Count}");
        for (var i = 0; i < points.Count; i++)
            Assert.Equal(expected[i], points[i]);
    }

    [Fact]
    public void The_probe_list_covers_every_death_picture_and_the_lightning()
    {
        var choices = RoundProbeData.Points.Where(p => p.Kind == "death").Select(p => p.Choice!.Value).Distinct().Order();
        Assert.Equal(Enumerable.Range(0, DeathExplosion.Choices), choices);
        Assert.Contains(RoundProbeData.Points, p => p.Kind == "bolt");
        Assert.Contains(RoundProbeData.Points, p => p.Kind == "end" && p.Winner == 0);
    }

    [Fact]
    public void Every_round_of_the_probe_runs_to_its_end()
    {
        var incomplete = RoundProbeData.Cases.Where(c => !Run(c).Complete).Select(c => c.Nr);

        Assert.Empty(incomplete);
    }
}
