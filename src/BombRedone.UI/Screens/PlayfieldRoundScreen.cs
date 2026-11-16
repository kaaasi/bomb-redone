using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Setup;
using BombRedone.Core.Shot;
using BombRedone.Core.Turn;

namespace BombRedone.UI.Screens;

/// <summary>
/// A whole round (spur runde): the loop of Core (<see cref="RoundPlay"/>, O4:44A7) with the turns of the players over the
/// frames (<see cref="PlayfieldTurnDriver"/>, spur zug); everything between two turns – shot, crumble, falling, deaths
/// with their pictures, lightning – runs in Core at once and is played back as the frames Core reports
/// (<see cref="RoundSettings.Frame"/>), like <see cref="PlayfieldShotScreen"/>. At the round end the winning team goes to
/// <c>roundOver</c>.
/// </summary>
/// <remarks>
/// [APPROX] Computer players fire at once with the angle and power they have (round 4); no sound output. The flight has no
/// delay in the original (speed of the machine), here 15 ms per frame (<see cref="ShotTiming"/>).
/// </remarks>
public sealed class PlayfieldRoundScreen : IScreen
{
    private readonly PlayfieldScreen _playfield;
    private readonly PlayfieldTurnView _view;
    private readonly IReadOnlyList<TurnPlayer> _players;
    private readonly IReadOnlyList<PlayerControl> _controls;
    private readonly PlayfieldTurnDriver _driver;
    private readonly RoundPlay _round;
    private readonly IEnumerator<RoundPoint> _steps;
    private readonly ShotTiming _timing;
    private readonly Func<int, IScreen?> _roundOver;
    private readonly List<(byte[] Pixels, byte[] Dac, TimeSpan Hold)> _frames = [];
    private int _frame;
    private TimeSpan _shown;
    private bool _over;

    /// <param name="players">Records 1..N on the tanks of the playfield (<see cref="RoundGame.Players"/>).</param>
    /// <param name="start">Start player and pass (<see cref="RoundGame.Start"/>).</param>
    /// <param name="roundOver">Called with the winning team (DS:47E0) after the round end.</param>
    public PlayfieldRoundScreen(PlayfieldScreen playfield, PlayfieldTurnView view, IReadOnlyList<TurnPlayer> players,
        IReadOnlyList<PlayerControl> controls, StartPlayer start, Func<bool[]> heldKeys, RoundSettings settings,
        TurboPascalRandom random, ShotTiming timing, TurnSettings turn, Func<int, IScreen?> roundOver)
    {
        ArgumentNullException.ThrowIfNull(playfield);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timing);
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(roundOver);
        _playfield = playfield;
        _view = view;
        _players = players;
        _controls = controls;
        _timing = timing;
        _roundOver = roundOver;
        var input = new TurnInput { MousePresent = true };
        var session = new TurnSession(playfield.Field, players, input, turn, view);
        _driver = new PlayfieldTurnDriver(session, input, heldKeys);
        _round = new RoundPlay(playfield.Field, players, session, settings with { Frame = Capture }, random, start);
        _steps = _round.Steps().GetEnumerator();
        Advance();
    }

    /// <summary>The round loop (state of the round, shot log).</summary>
    public RoundPlay Round => _round;

    /// <summary>Whether frames between two turns are being shown.</summary>
    public bool PlayingBack => _frame < _frames.Count;

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        if (PlayingBack)
        {
            _shown += elapsed;
            while (_frame < _frames.Count && _shown >= _frames[_frame].Hold)
            {
                _shown -= _frames[_frame].Hold;
                _frame++;
            }

            if (PlayingBack)
                return this;
            _frames.Clear();
            _frame = 0;
            _view.Refresh();
            return _over ? _roundOver(_round.WinnerTeam) : this;
        }

        if (_over)
            return _roundOver(_round.WinnerTeam);

        var done = _driver.Update(elapsed, input, _driver.Held());
        _view.Refresh();
        if (!done)
            return this;

        var choice = _driver.MirvChoice;
        _round.NextTurn = choice is { } c
            ? new TurnOutcome((MirvPattern)c.Pattern, (MirvDetonation)c.Detonation)
            : new TurnOutcome();
        Advance();
        return this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!PlayingBack)
        {
            _playfield.Draw(surface);
            return;
        }

        var (pixels, dac, _) = _frames[_frame];
        if (surface.Width != Surface.OriginalWidth || surface.Pixels.Length != pixels.Length)
            throw new ArgumentException("The playfield draws into a 320 x 200 surface only.", nameof(surface));
        pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(dac, surface);
    }

    /// <summary>Runs the loop of Core to the next turn start or the round end.</summary>
    private void Advance()
    {
        while (_steps.MoveNext())
        {
            var point = _steps.Current;
            switch (point.Kind)
            {
                case RoundPointKind.TurnStart:
                    Capture(ShotFrame.Flight);
                    _driver.Start(_round.ToMove, _controls[_round.ToMove - 1], _players[_round.ToMove - 1]);
                    return;
                case RoundPointKind.DeathStart:
                    _view.DeathStarted(_players[point.Player - 1]);
                    Capture(ShotFrame.Palette);
                    break;
                case RoundPointKind.End or RoundPointKind.Exhausted:
                    Capture(ShotFrame.Flight);
                    _over = true;
                    return;
            }
        }

        _over = true;
    }

    private void Capture(ShotFrame frame)
    {
        _playfield.RefreshField();
        var hold = frame switch
        {
            ShotFrame.Palette => _timing.Explosion,
            ShotFrame.Fall => _timing.Fall,
            _ => _timing.Tick,
        };
        _frames.Add(([.. _playfield.Screen.Pixels], [.. _playfield.Dac], hold));
    }
}
