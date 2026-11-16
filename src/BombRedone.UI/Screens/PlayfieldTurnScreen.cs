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
    /// <summary>F2 (scancode 3Ch): the next round (until spur runde ends rounds).</summary>
    public const int NextRoundKey = 0x3C;

    private readonly PlayfieldScreen _playfield;
    private readonly PlayfieldTurnView _view;
    private readonly List<TurnPlayer> _players;
    private readonly IReadOnlyList<PlayerControl> _controls;
    private readonly TurnSession _session;
    private readonly PlayfieldTurnDriver _driver;
    private readonly ShotSettings _settings;
    private readonly int _crumble;
    private readonly bool _fallDown;
    private readonly TurboPascalRandom _random;
    private readonly ShotTiming _timing;
    private readonly Func<IScreen?> _nextRound;
    private int _player;
    private int _pass;

    public PlayfieldTurnScreen(PlayfieldScreen playfield, PlayfieldTurnView view, List<TurnPlayer> players,
        IReadOnlyList<PlayerControl> controls, int startPlayer, int pass, Func<bool[]> heldKeys, ShotSettings settings,
        int crumble, bool fallDown, TurboPascalRandom random, ShotTiming timing, TurnSettings turn, Func<IScreen?> nextRound)
    {
        ArgumentNullException.ThrowIfNull(turn);
        _playfield = playfield;
        _view = view;
        _players = players;
        _controls = controls;
        _settings = settings;
        _crumble = crumble;
        _fallDown = fallDown;
        _random = random;
        _timing = timing;
        _nextRound = nextRound;
        var input = new TurnInput { MousePresent = true };
        _session = new TurnSession(playfield.Field, players, input, turn, view);
        _driver = new PlayfieldTurnDriver(_session, input, heldKeys);
        _player = startPlayer;
        _pass = pass;
        StartTurn();
    }

    private TurnPlayer Current => _players[_player - 1];

    private PlayerControl Control => _controls[_player - 1];

    private void StartTurn()
    {
        _session.Begin(_player);
        _driver.Start(_player, Control, Current);
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        var held = _driver.Held();
        if (held[NextRoundKey])
            return _nextRound();

        var done = _driver.Update(elapsed, input, held);
        _view.Refresh();
        return done ? Fire() : this;
    }

    public void Draw(Surface surface) => _playfield.Draw(surface);

    private IScreen? Fire()
    {
        // The shot takes the stock itself (O4:01B7, O4:13D4, O4:3426 in Core/Shot); the host takes nothing (review zug P2-3).
        var tank = Current.Tank;
        if (!(tank.Weapon is TurnPlayer.Laser or TurnPlayer.Mirv || Impact.IsSupported(tank.Weapon)))
            return NextTurn();

        // [ORIGINAL] O4:4977–49C8: MIRV with the choice of its dialog. [APPROX] Computer players fire at once without the
        // dialog until the game flow (spur runde) and use the defaults of WeaponShot.
        var (pattern, detonation) = _driver.MirvChoice is { } choice
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
