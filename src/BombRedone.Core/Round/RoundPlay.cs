using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Round;

/// <summary>
/// The round loop of O4:44A7 from the start player on (docs/analyse/runde.md sections 2 and 5.9, probe
/// docs/analyse/feld.md section 9): turn start Z1, the turn, the weapon, tanks, Crumble and falling (K11), shot log, the
/// death loop (K12) with the death pictures O5, the lightning (K13) and the round end (K14).
/// </summary>
public sealed class RoundPlay
{
    // [ORIGINAL] O4:4A28: 32767 (Real48 8F 00 00 00 FE 7F), the limit of the logged x.
    private static readonly Real48 LogLimit = Real48.FromHex("8F000000FE7F");

    private readonly Playfield.Playfield _field;
    private readonly IReadOnlyList<TurnPlayer> _players;
    private readonly TurnSession _session;
    private readonly RoundSettings _settings;
    private readonly TurboPascalRandom _random;

    /// <param name="players">Records 1..N on the tanks of <paramref name="field"/> (index 0 = record 1).</param>
    /// <param name="session">Turn start Z1 (<see cref="TurnSession.Begin"/>) and the weapon bar O4:0000.</param>
    /// <param name="start">Start player and pass of O4:4704 (<see cref="StartPlayer"/>).</param>
    public RoundPlay(Playfield.Playfield field, IReadOnlyList<TurnPlayer> players, TurnSession session, RoundSettings settings,
        TurboPascalRandom random, StartPlayer start)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        if (players.Count != field.Tanks.Count)
            throw new ArgumentException("One record per tank.", nameof(players));
        _field = field;
        _players = players;
        _session = session;
        _settings = settings;
        _random = random;
        ToMove = start.Player;
        Pass = start.Pass;
        Log = new ShotLog(players.Count);
    }

    /// <summary>The player to move (DS:47EE).</summary>
    public int ToMove { get; private set; }

    /// <summary>The pass (DS:47EA): 1 at the start player, + 1 each time the turn wraps from N to 1.</summary>
    public int Pass { get; set; }

    /// <summary>The winning team (DS:47E0) after the round end check; 0 while the round goes on or if nobody is left.</summary>
    public int WinnerTeam { get; private set; }

    /// <summary>Whether the round is over (DS:4813 after O4:4DAE).</summary>
    public bool Over { get; private set; }

    /// <summary>Record 0 (DS:402E), shooter of the explosions of O5:29A0 and of the lightning; it keeps its state.</summary>
    public PlayfieldTank RecordZero { get; init; } = PlayfieldTank.CreateRecordZero();

    /// <summary>The shot log DS:4A80.</summary>
    public ShotLog Log { get; }

    /// <summary>
    /// The shot state x, y (DS:4796, DS:479C) after the last weapon that set it: O4:01B7 and the laser; the MIRV keeps it.
    /// </summary>
    public (Real48 X, Real48 Y) ShotPosition { get; set; }

    /// <summary>
    /// Plays the round. <paramref name="turn"/> is called after Z1 with the record to move; it sets angle, power, weapon,
    /// guidance and target on the tank and returns the MIRV choice, or null if there is no turn (the loop stops with
    /// <see cref="RoundPointKind.Exhausted"/>). The enumeration reports each measuring point.
    /// </summary>
    public IEnumerable<RoundPoint> Play(Func<TurnPlayer, TurnOutcome?> turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        return PlayLoop(turn);
    }

    private IEnumerable<RoundPoint> PlayLoop(Func<TurnPlayer, TurnOutcome?> turn)
    {
        var count = 0;
        while (true)
        {
            // [ORIGINAL] O4:479A–4958: Z1 (tank, +5Eh, +70h, weapon with stock, weapon bar, header, power ≤ 10 · energy).
            var player = Record(ToMove);
            _session.Begin(ToMove);
            var outcome = turn(player);
            if (outcome is null)
            {
                yield return new RoundPoint(RoundPointKind.Exhausted, count);
                yield break;
            }

            yield return new RoundPoint(RoundPointKind.Turn, count);
            Fire(player.Tank, outcome.Value);

            // [ORIGINAL] O4:49CB–4A0A: all tanks U431:07BA, Crumble > 0 → O7:03C1, Fall down → O7:0020.
            AfterShot.Run(_field, _settings.Crumble, _settings.FallDown, _random, _settings.Frame);
            yield return new RoundPoint(RoundPointKind.AfterShot, count);

            WriteLog(player.Tank);
            WeaponBar.SetColors(_field.Palette, player);  // O4:4B4F: O4:0000 (weapon bar colours)

            foreach (var point in DeathLoop(count))
                yield return point;

            CheckEnd();
            if (Over)
            {
                // [ORIGINAL] O4:4E6B–4EEA: U431:0000 (rotation flag DS:4766 off), +68h += 100 − energy (100 outside 0..100).
                foreach (var p in _players)
                {
                    var e = p.Tank.Energy;
                    p.Tank.RoundSum += e is > 100 or < 0 ? 100 : 100 - e;
                }

                yield return new RoundPoint(RoundPointKind.End, count);
                yield break;
            }

            yield return new RoundPoint(RoundPointKind.Continue, count);
            NextPlayer();
            count++;
        }
    }

    private TurnPlayer Record(int number) => _players[number - 1];

    /// <summary>[ORIGINAL] O4:4965–49CA: DS:47EE; weapon 5 → laser O4:3426, 7 → MIRV O4:13D4, otherwise O4:01B7.</summary>
    private void Fire(PlayfieldTank shooter, TurnOutcome outcome)
    {
        var shot = _settings.Shot;
        switch (shooter.Weapon)
        {
            case 5:
                if (LaserShot.Fire(_field, shooter, shot, _random) is { } end)
                    ShotPosition = end;
                break;
            case 7:
                MirvShot.Fire(_field, shooter, outcome.Pattern, outcome.Detonation, shot, _random);
                break;
            default:
                var result = BallisticShot.Fire(_field, shooter, shot, _random);
                ShotPosition = (result.X, result.Y);
                break;
        }
    }

    /// <summary>[ORIGINAL] O4:4A0B–4B47: entry +5Eh of the shot log (see <see cref="ShotLog"/>).</summary>
    private void WriteLog(PlayfieldTank tank)
    {
        var (x, y) = ShotPosition;
        var ix = Real48.Compare(x, LogLimit) > 0 ? 0x7FFF : x.Trunc();
        Log.Write(tank.Number, tank.Field5E, tank.Angle, tank.Power, tank.Direction, ix, y.Trunc());
    }

    /// <summary>
    /// [ORIGINAL] O4:4B57–4DAB: per record with energy ≤ 0 that is still alive: +23h := 0, tank U431:07BA, name in the
    /// header, Random(7) → picture, the player to move gets +1500 and a kill (+6Eh) for another team, −1000 for the own team
    /// (also himself); frame O7:0364, O7:03C1 (without the check of Crumble &gt; 0), Fall down → O7:0020. Repeated while somebody
    /// died; without a death in pass &gt; 10 Random(100) and for pass &gt; value the lightning O6:14C5(0), then the loop again.
    /// </summary>
    private IEnumerable<RoundPoint> DeathLoop(int count)
    {
        bool again;
        do
        {
            again = false;
            foreach (var p in _players)
            {
                var tank = p.Tank;
                if (tank.Energy > 0 || !tank.Active)
                    continue;
                again = true;
                tank.Active = false;
                _field.DrawTank(tank);
                yield return new RoundPoint(RoundPointKind.DeathStart, count, tank.Number);  // O4:4BB8: name in the header
                var choice = _random.Next(DeathExplosion.Choices);
                DeathExplosion.Run(choice, _field, tank, RecordZero, _settings, _random);
                var mover = Record(ToMove).Tank;
                if (mover.Team != tank.Team)
                {
                    mover.Score += 1500;
                    mover.Kills++;
                }
                else
                {
                    mover.Score -= 1000;
                }

                _field.RedrawFrame();
                AfterShot.Crumble(_field, _settings.Crumble, _random, _settings.Frame);
                if (_settings.FallDown)
                    AfterShot.Fall(_field, _random, _settings.Frame);
                yield return new RoundPoint(RoundPointKind.Death, count, tank.Number, choice);
            }

            if (!again && Pass > 10)
            {
                var value = _random.Next(100);
                if (Pass > value)
                {
                    Lightning.Run(_field, RecordZero, _settings, _random);
                    again = true;
                    yield return new RoundPoint(RoundPointKind.Lightning, count);
                }
            }
        }
        while (again);
    }

    /// <summary>
    /// [ORIGINAL] O4:4DAE–4E61: team = team (+66h) of the last living record (0 if none); over when every living record has
    /// that team (also when nobody lives); DS:47E0 = that team, else 0.
    /// </summary>
    private void CheckEnd()
    {
        var team = 0;
        foreach (var p in _players)
        {
            if (p.Tank.Active)
                team = p.Tank.Team;
        }

        var same = _players.All(p => !p.Tank.Active || p.Tank.Team == team);
        WinnerTeam = same ? team : 0;
        Over = same;
    }

    /// <summary>[ORIGINAL] O4:4770–4797: next record, after N again 1 with pass + 1; records with +23h = 0 are skipped.</summary>
    private void NextPlayer()
    {
        do
        {
            ToMove++;
            if (ToMove > _players.Count)
            {
                ToMove = 1;
                Pass++;
            }
        }
        while (!Record(ToMove).Tank.Active);
    }
}
