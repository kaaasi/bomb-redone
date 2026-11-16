using BombRedone.Core.Setup;
using BombRedone.Core.Turn;

namespace BombRedone.UI.Screens;

/// <summary>
/// Runs the turn coroutine of <see cref="TurnSession"/> over the frames of the window (spur zug): polls take the held keys,
/// typed keys and the mouse of the frame, delays wait for the elapsed time, wait-for-release loops for the player to let
/// go; after the turn the MIRV dialog for weapon 7. Shared by <see cref="PlayfieldTurnScreen"/> and
/// <see cref="PlayfieldRoundScreen"/>.
/// </summary>
internal sealed class PlayfieldTurnDriver
{
    private readonly TurnSession _session;
    private readonly TurnInput _input;
    private readonly Func<bool[]> _heldKeys;
    private readonly HashSet<int> _blocked = [];
    private readonly List<ushort> _typed = [];
    private bool _atPoll;
    private TurnWait? _release;
    private IEnumerator<TurnWait>? _turn;
    private bool _mirvDone;
    private int _player;
    private PlayerControl _control;
    private TurnPlayer? _record;
    private double _budgetMs;
    private double _delayMs;

    public PlayfieldTurnDriver(TurnSession session, TurnInput input, Func<bool[]> heldKeys)
    {
        _session = session;
        _input = input;
        _heldKeys = heldKeys;
    }

    /// <summary>The MIRV choice of the dialog of this turn, if it ran.</summary>
    public (int Pattern, int Detonation)? MirvChoice => _mirvDone ? _session.MirvChoice : null;

    /// <summary>Starts the turn of <paramref name="player"/> (after Z1); a computer fires at once (no computers yet).</summary>
    public void Start(int player, PlayerControl control, TurnPlayer record)
    {
        _player = player;
        _control = control;
        _mirvDone = false;
        _release = null;
        _turn = control == PlayerControl.Computer ? null : _session.Play(player, control).GetEnumerator();
        _record = record;
    }

    /// <summary>Runs the turn for one frame; true when it is over (the record has its angle, power and weapon).</summary>
    public bool Update(TimeSpan elapsed, ScreenInput input, bool[] held)
    {
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

        if (_turn is not null)
            return false;

        if (_record?.Tank.Weapon == TurnPlayer.Mirv && !_mirvDone && _control != PlayerControl.Computer)
        {
            _mirvDone = true;
            _turn = _session.MirvSetup(_player, _control).GetEnumerator();
            return false;
        }

        return true;
    }

    /// <summary>The held-key table of the window.</summary>
    public bool[] Held() => _heldKeys();

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
}
