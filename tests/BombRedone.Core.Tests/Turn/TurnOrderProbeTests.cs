using System.Globalization;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Tests.Turn;

/// <summary>
/// Turn order R5 and turn start Z1 against the probe (<c>python3 -I tools/reverse-engineering/zug.py folge</c>,
/// analyse/feld/zugfolge.tsv; cases in ORDERS of that script): from the start player on, every turn is skipped; after Z1
/// player, pass (DS:47EA), weapon, +5Eh and +70h. Kills "t:p,…" clear +23h of p after turn t.
/// </summary>
public sealed class TurnOrderProbeTests
{
    public sealed record OrderCase(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, int Count, string Kills, string Sets,
        string Turns)
    {
        public override string ToString() => $"order {Nr}";
    }

    private static readonly OrderCase[] All =
    [
        new(0, "12345678", 2, 0, 0, 2, 6, "-", "-", "2,1,1,1,1 1,2,1,1,1 2,2,1,2,2 1,3,1,2,2 2,3,1,3,3 1,4,1,3,3"),
        new(1, "9E3779B9", 4, 2, 2, 1, 10, "-", "-", "3,1,1,1,1 4,1,1,1,1 1,2,1,1,1 2,2,1,1,1 3,2,1,2,2 4,2,1,2,2 1,3,1,2,2 2,3,1,2,2 3,3,1,3,3 4,3,1,3,3"),
        new(2, "9E3779B9", 4, 2, 2, 1, 10, "0:2", "-", "3,1,1,1,1 4,1,1,1,1 1,2,1,1,1 3,2,1,2,2 4,2,1,2,2 1,3,1,2,2 3,3,1,3,3 4,3,1,3,3 1,4,1,3,3 3,4,1,4,4"),
        new(3, "DEADBEEF", 5, 3, 3, 3, 12, "1:1,5 4:3", "-", "3,1,1,1,1 4,1,1,1,1 2,2,1,1,1 3,2,1,2,2 4,2,1,2,2 2,3,1,2,2 4,3,1,3,3 2,4,1,3,3 4,4,1,4,4 2,5,1,4,4 4,5,1,5,5 2,6,1,5,5"),
        new(4, "9E3779B9", 4, 2, 2, 1, 6, "-", "2:weapon=3 2:stock3=0 3:weapon=17 3:stock17=0 4:weapon=0 4:stock0=0", "3,1,0,1,1 4,1,1,1,1 1,2,1,1,1 2,2,11,1,1 3,2,0,2,2 4,2,1,2,2"),
    ];

    public static TheoryData<OrderCase> Cases
    {
        get
        {
            var data = new TheoryData<OrderCase>();
            foreach (var c in All)
                data.Add(c);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Order_and_turn_start_match_the_probe(OrderCase c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, false), random);
        Wind.Choose(c.WindOption, random);
        var start = StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);
        var players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
        foreach (var p in players)
            p.SetDefaultStocks();
        foreach (var set in c.Sets == "-" ? [] : c.Sets.Split(' '))
            TurnHarness.Apply(players, 1, set);
        var kills = c.Kills == "-"
            ? new Dictionary<int, int[]>()
            : c.Kills.Split(' ').ToDictionary(
                k => int.Parse(k[..k.IndexOf(':', StringComparison.Ordinal)], CultureInfo.InvariantCulture),
                k => k[(k.IndexOf(':', StringComparison.Ordinal) + 1)..].Split(',').Select(int.Parse).ToArray());

        var session = new TurnSession(field, players, new TurnInput(), new TurnSettings());
        var (player, pass) = (start.Player, start.Pass);
        var seen = new List<string>();
        for (var turn = 0; turn < c.Count; turn++)
        {
            if (turn > 0)
                (player, pass) = TurnOrder.Next(player, pass, players.Select(p => p.Tank.Active).ToArray());
            session.Begin(player);
            var p = players[player - 1];
            seen.Add(string.Create(CultureInfo.InvariantCulture,
                $"{player},{pass},{p.Tank.Weapon},{p.Tank.Field5E},{p.Turns}"));
            foreach (var dead in kills.GetValueOrDefault(turn, []))
                TurnHarness.SetInternal(players[dead - 1].Tank, nameof(PlayfieldTank.Active), false);
        }

        Assert.Equal(c.Turns, string.Join(" ", seen));
    }

    [Fact]
    public void Next_wraps_with_the_pass_and_skips_inactive_records()
    {
        Assert.Equal((1, 3), TurnOrder.Next(3, 2, [true, false, true]));
        Assert.Equal((3, 2), TurnOrder.Next(1, 2, [true, false, true]));
        Assert.Throws<ArgumentException>(() => TurnOrder.Next(1, 1, [false, false]));
    }
}
