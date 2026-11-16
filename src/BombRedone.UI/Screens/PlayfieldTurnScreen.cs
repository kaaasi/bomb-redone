using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;
using BombRedone.Core.Shot;
using BombRedone.Core.Turn;

namespace BombRedone.UI.Screens;

/// <summary>
/// The turns of a round (spur zug, docs/analyse/zug.md): Z1 and the turn of Core (<see cref="TurnSession"/>) with keyboard
/// and mouse, the MIRV dialog for weapon 7, then the shot of Core/Shot and the next player (R5). Each frame the coroutine
/// runs until its next poll that is not preceded by a delay; the delays of the turn (TS) are taken from the frame time, so
/// a held arrow turns about as fast as in the original. At a wait-for-release loop of Core the coroutine stays until the
/// player has let go of the keys or buttons, then goes on with the input of that moment (O7:0EF7 reads the live table).
/// F2 builds the next round.
/// </summary>
/// <remarks>
/// [APPROX] Until spur runde: no death and no end of the round (a tank with energy 0 counts as out, below two tanks the
/// next round starts); weapons that Core/Shot cannot fire yet (laser, rollers, sonic, MIRV split, repulser in the field)
/// skip the shot; the stock goes down by one per shot here (O4:01B7, later in Core/Shot); computer players fire at once
/// (round 4); stocks start like a new record (no shop yet); no sound (the host passes <see cref="TurnSettings.Sound"/> false).
/// </remarks>
public sealed class PlayfieldTurnScreen : IScreen
{
    /// <summary>Scancode of F2 in the held-key table.</summary>
    public const int NextRoundKey = 0x3C;

    private readonly PlayfieldScreen _playfield;
    private readonly PlayfieldTurnView _view;
    private readonly List<TurnPlayer> _players;
    private readonly IReadOnlyList<PlayerControl> _controls;
    private readonly TurnSession _session;
    private readonly TurnInput _input = new() { MousePresent = true };
    private readonly Func<bool[]> _heldKeys;
    private readonly ShotSettings _settings;
    private readonly int _crumble;
    private readonly bool _fallDown;
    private readonly TurboPascalRandom _random;
    private readonly ShotTiming _timing;
    private readonly Func<IScreen?> _nextRound;
    private readonly HashSet<int> _blocked = [];
    private readonly List<ushort> _typed = [];
    private bool _atPoll;
    private TurnWait? _release;
    private IEnumerator<TurnWait>? _turn;
    private bool _mirvDone;
    private int _player;
    private int _pass;
    private double _budgetMs;
    private double _delayMs;

    /// <param name="heldKeys">The held-key table (DS:452A + scancode, E0 keys + 100h) at the moment of the call.</param>
    /// <param name="turn">Options of the turn: TS (DS:E8C4), team play (DS:E8BC), sound.</param>
    public PlayfieldTurnScreen(PlayfieldScreen playfield, PlayfieldTurnView view, List<TurnPlayer> players,
        IReadOnlyList<PlayerControl> controls, int startPlayer, int pass, Func<bool[]> heldKeys, ShotSettings settings,
        int crumble, bool fallDown, TurboPascalRandom random, ShotTiming timing, TurnSettings turn, Func<IScreen?> nextRound)
    {
        ArgumentNullException.ThrowIfNull(turn);
        _playfield = playfield;
        _view = view;
        _players = players;
        _controls = controls;
        _heldKeys = heldKeys;
        _settings = settings;
        _crumble = crumble;
        _fallDown = fallDown;
        _random = random;
        _timing = timing;
        _nextRound = nextRound;
        _session = new TurnSession(playfield.Field, players, _input, turn, view);
        _player = startPlayer;
        _pass = pass;
        StartTurn();
    }

    private TurnPlayer Current => _players[_player - 1];

    private PlayerControl Control => _controls[_player - 1];

    private void StartTurn()
    {
        _session.Begin(_player);
        _mirvDone = false;
        _release = null;
        _turn = Control == PlayerControl.Computer ? null : _session.Play(_player, Control).GetEnumerator();
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        var held = _heldKeys();
        if (held[NextRoundKey])
            return _nextRound();

        _budgetMs = Math.Min(_budgetMs + elapsed.TotalMilliseconds, 100);
        _typed.AddRange(input.Keys);
        var polled = false;
        var delayed = false;
        if (_atPoll)
        {
            Sample(held, input);
            _atPoll = false;
            polled = true;
        }

        while (_turn is not null)
        {
            if (_release is { } release)
            {
                if (!Released(release, held, input))
                    break;
                _release = null;
                Sample(held, input);
            }

            if (_delayMs > 0)
            {
                if (_budgetMs < _delayMs)
                    break;
                _budgetMs -= _delayMs;
                _delayMs = 0;
                delayed = true;
            }

            var before = Snapshot();
            var more = _turn.MoveNext();
            Block(before);
            if (!more)
            {
                _turn = null;
                break;
            }

            var w = _turn.Current;
            if (w.Kind == TurnWaitKind.Delay)
            {
                _delayMs = w.Value;
                continue;
            }

            if (w.Kind is TurnWaitKind.ReleaseKeys or TurnWaitKind.ReleaseButtons)
            {
                _release = w;
                continue;
            }

            if (w.Kind != TurnWaitKind.Poll)
                continue;

            // A poll without a delay since the previous one in this frame waits for the next frame (busy loops).
            if (polled && !delayed)
            {
                _atPoll = true;
                break;
            }

            Sample(held, input);
            polled = true;
            delayed = false;
        }

        _view.Refresh();
        if (_turn is not null)
            return this;

        if (Current.Tank.Weapon == TurnPlayer.Mirv && !_mirvDone && Control != PlayerControl.Computer)
        {
            _mirvDone = true;
            _turn = _session.MirvSetup(_player, Control).GetEnumerator();
            return this;
        }

        return Fire();
    }

    public void Draw(Surface surface) => _playfield.Draw(surface);

    /// <summary>Keys held before the coroutine ran on (to see which ones it cleared).</summary>
    private bool[] Snapshot()
    {
        var keys = new bool[Scancode.Count];
        for (var i = 0; i < Scancode.Count; i++)
            keys[i] = _input[i];
        return keys;
    }

    /// <summary>The player has let go of what a wait-for-release loop of Core waits for.</summary>
    private static bool Released(TurnWait wait, bool[] held, ScreenInput input) =>
        wait.Kind == TurnWaitKind.ReleaseButtons ? input.Mouse == MouseButtons.None : !wait.Keys!.Any(k => held[k]);

    /// <summary>
    /// A key the turn cleared without waiting (MIRV dialog, cheat loop) stays up until the player lets go of it: the
    /// keyboard handler sends no new make code while it is held. [APPROX] The typematic repeat of the keyboard (new make
    /// codes while held) is not reproduced.
    /// </summary>
    private void Block(bool[] before)
    {
        for (var i = 0; i < Scancode.Count; i++)
        {
            if (before[i] && !_input[i])
                _blocked.Add(i);
        }
    }

    private void Sample(bool[] held, ScreenInput input)
    {
        _blocked.RemoveWhere(k => !held[k]);
        for (var i = 0; i < Scancode.Count; i++)
            _input[i] = held[i] && !_blocked.Contains(i);
        foreach (var word in _typed)
            _input.TypeKey((byte)word, (byte)(word >> 8));
        _typed.Clear();

        _input.MouseButtons = (int)input.Mouse;
        _input.MouseX = input.MouseX / 2;
        _input.MouseY = input.MouseY;
    }

    private IScreen? Fire()
    {
        // The shot takes the stock itself (O4:01B7, O4:13D4, O4:3426 in Core/Shot); the host takes nothing (review zug P2-3).
        var tank = Current.Tank;
        if (!(tank.Weapon is TurnPlayer.Laser or TurnPlayer.Mirv || Impact.IsSupported(tank.Weapon)))
            return NextTurn();

        // [ORIGINAL] O4:4977–49C8: MIRV with the choice of its dialog. [APPROX] Computer players fire at once without the
        // dialog until the game flow (spur runde) and use the defaults of WeaponShot.
        var (pattern, detonation) = _mirvDone && _session.MirvChoice is { } choice
            ? ((MirvPattern)choice.Pattern, (MirvDetonation)choice.Detonation)
            : (MirvPattern.Random, MirvDetonation.Impact);
        return PlayfieldShotScreen.Fire(_playfield, tank, _settings, _crumble, _fallDown, _random, _timing, NextTurn,
            pattern, detonation);
    }

    private IScreen? NextTurn()
    {
        var active = _players.Select(q => q.Tank.Active && q.Tank.Energy > 0).ToArray();
        if (active.Count(a => a) < 2)
            return _nextRound();

        (_player, _pass) = TurnOrder.Next(_player, _pass, active);
        StartTurn();
        _view.Refresh();
        return this;
    }
}
