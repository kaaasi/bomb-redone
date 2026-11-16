using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Round;

/// <summary>
/// One game of a battle on the records of <see cref="BattleRecords"/>: builds the round like O4:44A7 up to the start player
/// and hands the record fields to the tanks and back. In the original record and tank are one (DS:402E + 74h · n); here the
/// account (<see cref="PlayerAccount"/>), the battle record (<see cref="BattlePlayer"/>) and the tank are separate, so the
/// fields go over at the round start (<see cref="Prepare"/>) and back at the round end (<see cref="Finish"/>).
/// </summary>
public sealed class RoundGame
{
    private readonly BattleRecords _records;

    private RoundGame(BattleRecords records, Playfield.Playfield field, Wind wind, StartPlayer start,
        IReadOnlyList<TurnPlayer> players)
    {
        _records = records;
        Field = field;
        Wind = wind;
        Start = start;
        Players = players;
    }

    public Playfield.Playfield Field { get; }

    public Wind Wind { get; }

    public StartPlayer Start { get; }

    /// <summary>Records 1..N in the order of the battle records (after the shuffle of this game).</summary>
    public IReadOnlyList<TurnPlayer> Players { get; }

    /// <summary>
    /// Stocks of new records: Lead Ball 100, Baby Missile 30, weapon 11 100, weapon 1 chosen (O2:0216, measured with zug.py).
    /// [APPROX] The original sets them once at the program start (O2:0216 for records 1..10); the remake at the start of a
    /// battle.
    /// </summary>
    public static void InitializeRecords(BattleRecords records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (var r in records.InOrder)
        {
            for (var w = 0; w < TurnPlayer.Weapons; w++)
                r.Account.SetStock(ItemKind.Weapon, w, w switch { 0 => 100, 1 => 30, 11 => 100, _ => 0 });
            r.Weapon = 1;
        }
    }

    /// <summary>
    /// The round of a game after the shuffle: playfield O8:0984 with the colour divisors of the records (they follow the
    /// player, #153), wind, start player from the counters +72h; then per record the stocks (<c>TakeStock</c>, #147), score,
    /// team, weapon, guidance and the counters to the tank. <paramref name="previous"/> hands on the CRI list (#165).
    /// </summary>
    public static RoundGame Prepare(BattleRecords records, PlayfieldOptions options, TurboPascalRandom random,
        Playfield.Playfield? previous = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        if (records.Count != options.Players)
            throw new ArgumentException("One record per player.", nameof(records));
        var divisors = records.InOrder.Select(r => Playfield.Playfield.ColorDivisors[r.Account.Number - 1]).ToArray();
        var field = Playfield.Playfield.Build(options, random, divisors);
        if (previous is not null)
            field.TakeOverCriList(previous);
        var wind = Wind.Choose(options.Wind, random);
        var counters = records.InOrder.Select(r => r.RoundsStarted).ToArray();
        var start = StartPlayer.Choose(counters, Enumerable.Repeat(true, records.Count).ToArray(), random);
        for (var i = 0; i < counters.Length; i++)
            records.InOrder[i].RoundsStarted = counters[i];

        var players = new List<TurnPlayer>();
        for (var i = 0; i < records.Count; i++)
        {
            var r = records.InOrder[i];
            var tank = field.Tanks[i];
            tank.TakeStock(r.Account);
            tank.Score = r.Account.Score;
            tank.Team = r.Team;
            tank.Weapon = r.Weapon;
            tank.Guidance = r.Guidance;
            tank.GuidanceTarget = r.GuidanceTarget;
            tank.Hits = r.Hits;
            tank.Kills = r.Kills;
            tank.RoundSum = r.RoundSum;
            players.Add(new TurnPlayer(tank, r.Name) { Turns = r.Turns, Spent = r.Account.Spent, Wins = r.Account.Wins });
        }

        return new RoundGame(records, field, wind, start, players);
    }

    /// <summary>
    /// The round end for the records: stocks back (<c>ReturnStock</c>, #147), score, weapon, guidance, counters and whether the
    /// tank still lives (+23h, for the wins of O3).
    /// </summary>
    public void Finish()
    {
        for (var i = 0; i < _records.Count; i++)
        {
            var r = _records.InOrder[i];
            var p = Players[i];
            var tank = p.Tank;
            tank.ReturnStock(r.Account);
            r.Account.Score = tank.Score;
            r.Weapon = tank.Weapon;
            r.Guidance = tank.Guidance;
            r.GuidanceTarget = tank.GuidanceTarget;
            r.Hits = tank.Hits;
            r.Kills = tank.Kills;
            r.RoundSum = tank.RoundSum;
            r.Turns = p.Turns;
            r.Alive = tank.Active;
        }
    }
}
