using BombRedone.Core.Graphics;
using BombRedone.Core.Setup;
using PlayfieldModel = BombRedone.Core.Playfield.Playfield;

namespace BombRedone.Core.Turn;

/// <summary>
/// One player's turn of a round: turn start Z1 (O4:479A–495D), the turn O7:040C with the input routine O7:0EF7, the tank
/// info O9:0040, the target selection O9:092E and the MIRV dialog at the start of O4:13D4 (docs/analyse/runde.md section 6,
/// docs/analyse/zug.md). The waits of the original are yields of a coroutine (<see cref="TurnWait"/>): at a poll the host
/// updates <see cref="Input"/>, delays and tones it plays or skips. At wait-for-release loops it resumes once the keys or
/// buttons are up (<see cref="TurnWaitKind.ReleaseKeys"/>, <see cref="TurnWaitKind.ReleaseButtons"/>); the turn then
/// releases them in <see cref="Input"/> itself. Verified bit for bit against the probe zug.py (TurnProbeTests).
/// </summary>
public sealed class TurnSession
{
    private readonly PlayfieldModel _field;
    private readonly IReadOnlyList<TurnPlayer> _players;
    private readonly TurnSettings _settings;
    private readonly ITurnView _view;
    private bool _biosMode;

    /// <param name="players">Records 1..N (index 0 = player 1), on the tanks of <paramref name="field"/>.</param>
    public TurnSession(PlayfieldModel field, IReadOnlyList<TurnPlayer> players, TurnInput input, TurnSettings settings,
        ITurnView? view = null)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(settings);
        _field = field;
        _players = players;
        Input = input;
        _settings = settings;
        _view = view ?? NullTurnView.Instance;
    }

    public TurnInput Input { get; }

    /// <summary>The commands O7:0EF7 returned, in order (0 included).</summary>
    public List<TurnCommand> Commands { get; } = [];

    /// <summary>Pattern 0..4 and detonation 0..2 of the last MIRV dialog.</summary>
    public (int Pattern, int Detonation)? MirvChoice { get; private set; }

    private VgaPalette Palette => _field.Palette;

    private int PlayerCount => _players.Count;

    private TurnPlayer Player(int number) => _players[number - 1];

    /// <summary>
    /// Turn start Z1. [ORIGINAL] O4:479A–4958 (BOMB.OVR 0x1549F–0x1565D): tank U431:07BA, +5Eh + 1, +70h + 1; a weapon
    /// without stock (+2Eh + 2w = 0) is replaced by the next one with stock &gt; 0 (w + 1, after 17 again 0); weapon bar
    /// O4:0000; header (name, wind); power at most 10 · energy.
    /// </summary>
    public void Begin(int number)
    {
        var p = Player(number);
        _field.DrawTank(p.Tank);
        p.Tank.Field5E++;
        p.Turns++;
        if (p.Tank.Stock[p.Tank.Weapon] == 0)
        {
            do
            {
                p.Tank.Weapon++;
                if (p.Tank.Weapon > TurnPlayer.Weapons - 1)
                    p.Tank.Weapon = 0;
            }
            while (p.Tank.Stock[p.Tank.Weapon] <= 0);
        }

        WeaponBar.SetColors(Palette, p);
        _view.TurnStarted(p);
        var max = MaxPower(p);
        if (p.Tank.Power > max)
            p.Tank.Power = max;
    }

    /// <summary>10 · energy as the 16-bit product of the original (<c>mul word</c>, O4:4922 and O7:0B1E).</summary>
    private static int MaxPower(TurnPlayer p) => (short)(ushort)(10 * p.Tank.Energy);

    /// <summary>
    /// The turn. [ORIGINAL] O7:040C (BOMB.OVR 0x1ADA2): weapon bar, frame O7:0364, the four fields; then commands of
    /// O7:0EF7 until Fire; then wait until both mouse buttons are up and, with guidance switched on, the target selection.
    /// </summary>
    public IEnumerable<TurnWait> Play(int number, PlayerControl control)
    {
        var p = Player(number);
        WeaponBar.SetColors(Palette, p);
        _field.RedrawFrame();
        _view.DrawFields(p);
        var fire = false;
        do
        {
            var command = TurnCommand.None;
            foreach (var w in ReadCommand(p, control, c => command = c))
                yield return w;
            Commands.Add(command);
            if (command == TurnCommand.None)
                continue;

            foreach (var w in Execute(p, command))
                yield return w;
            if (command == TurnCommand.Fire)
                fire = true;
        }
        while (!fire);

        // O7:0E97: until both buttons are up.
        foreach (var w in WaitForButtons("mouse fire"))
            yield return w;
        if (p.Tank.Guidance > 0)
        {
            foreach (var w in SelectTarget(p, control))
                yield return w;
        }
    }

    private IEnumerable<TurnWait> Execute(TurnPlayer p, TurnCommand command)
    {
        var tank = p.Tank;
        switch (command)
        {
            case TurnCommand.PreviousWeapon:
            case TurnCommand.NextWeapon:
                ChangeWeapon(p, command == TurnCommand.NextWeapon ? 1 : -1);
                break;
            case TurnCommand.ToggleHerb:
            case TurnCommand.ToggleVick:
                ToggleGuidance(p, command == TurnCommand.ToggleHerb ? 0 : 1);
                break;
            case TurnCommand.AnglePlus:
            case TurnCommand.AngleMinus:
                // O7:07D9: barrel erased from the background buffer with the old angle; the angle turns over the vertical.
                EraseBarrel(p);
                tank.Angle += command == TurnCommand.AnglePlus ? tank.Direction : -tank.Direction;
                if (tank.Angle > 90)
                {
                    tank.Direction = -tank.Direction;
                    tank.Angle = 180 - tank.Angle;
                    _view.DrawDirection(p);
                }

                if (tank.Angle < 0)
                {
                    tank.Direction = -tank.Direction;
                    tank.Angle = Math.Abs(tank.Angle);
                    _view.DrawDirection(p);
                }

                _field.DrawTank(tank);
                if (_settings.Sound)
                {
                    foreach (var w in Tone(command == TurnCommand.AnglePlus ? 1750 : 2000, 10))
                        yield return w;
                    yield return TurnWait.NoSound;
                }

                _view.DrawAngle(p);
                break;
        }

        // O7:0AC2: power.
        if (command is TurnCommand.PowerDown or TurnCommand.PowerUp)
        {
            tank.Power += command == TurnCommand.PowerUp ? 1 : -1;
            var max = MaxPower(p);
            if (tank.Power > max)
                tank.Power = max;
            if (tank.Power < 0)
                tank.Power = 0;
            if (_settings.Sound)
            {
                foreach (var w in Tone(command == TurnCommand.PowerDown ? 1500 : 2500, 10))
                    yield return w;
                yield return TurnWait.NoSound;
            }

            _view.DrawPower(p);
        }

        // O7:0C05: after every command the laser's power is at most its stock.
        if (tank.Weapon == TurnPlayer.Laser && tank.Power > p.Tank.Stock[TurnPlayer.Laser])
        {
            tank.Power = p.Tank.Stock[TurnPlayer.Laser];
            if (_settings.Sound)
            {
                foreach (var w in Tone(1500, 10))
                    yield return w;
                foreach (var w in Tone(2500, 10))
                    yield return w;
                yield return TurnWait.NoSound;
            }

            _view.DrawPower(p);
        }

        // O7:0CD8: direction.
        if (command == TurnCommand.FlipDirection)
        {
            EraseBarrel(p);
            tank.Direction = -tank.Direction;
            _field.DrawTank(tank);
            if (_settings.Sound)
            {
                foreach (var hz in (int[])[1750, 2000, 1500, 1250])
                {
                    foreach (var w in Tone(hz, 5))
                        yield return w;
                }

                yield return TurnWait.NoSound;
            }

            _view.DrawDirection(p);
        }
    }

    /// <summary>
    /// A wait-for-release loop of O7:0EF7: the host resumes once <paramref name="scancodes"/> are up (the probe at once);
    /// the routine only goes on afterwards, so the command takes effect after the release like in the original.
    /// </summary>
    private IEnumerable<TurnWait> WaitForRelease(string name, int[] scancodes)
    {
        if (!Input.AnyHeld(scancodes))
            yield break;
        Input.Releases.Add(name);
        yield return TurnWait.ReleaseKeys(scancodes);
        Input.Release(scancodes);
    }

    /// <summary>A loop that waits until both mouse buttons are up (O7:0E97, O7:13EB ff., O4:13D4 ff.).</summary>
    private IEnumerable<TurnWait> WaitForButtons(string name)
    {
        if (Input.MouseButtons == 0)
            yield break;
        Input.Releases.Add(name);
        yield return TurnWait.ReleaseButtons;
        Input.MouseButtons = 0;
    }

    private static IEnumerable<TurnWait> Tone(int hz, int ms)
    {
        yield return TurnWait.Sound(hz);
        yield return TurnWait.Delay(ms);
    }

    /// <summary>[ORIGINAL] O7:07E1–08A3 / 0CE9–0DAB: UCC8:0AC0 copies the barrel line back from the buffer DS:E5C0.</summary>
    private void EraseBarrel(TurnPlayer p)
    {
        var t = p.Tank;
        var (dx, dy) = PlayfieldModel.BarrelOffset(t.Angle);
        VgaDraw.CopyLine(_field.Background.Pixels, _field.Screen.Pixels, t.X, t.Y, t.X + dx * t.Direction, t.Y - dy);
    }

    /// <summary>
    /// [ORIGINAL] O7:0569–0699: weapon w ± 1 (17 ↔ 0) until one with stock &gt; 0, the laser only with stock ≥ 100; then
    /// 16 steps v = 3, 7, …, 63: DAC 100 + old = (63, v, v), 100 + new = (63, 63 − v, 63 − v).
    /// </summary>
    private void ChangeWeapon(TurnPlayer p, int step)
    {
        var tank = p.Tank;
        var old = tank.Weapon;
        bool ok;
        do
        {
            tank.Weapon += step;
            if (tank.Weapon < 0)
                tank.Weapon = TurnPlayer.Weapons - 1;
            if (tank.Weapon > TurnPlayer.Weapons - 1)
                tank.Weapon = 0;
            ok = p.Tank.Stock[tank.Weapon] > 0;
            if (tank.Weapon == TurnPlayer.Laser && p.Tank.Stock[tank.Weapon] < 100)
                ok = false;
        }
        while (!ok);

        for (var v = 3; ; v += 4)
        {
            Palette.Set(WeaponBar.ColorBase + old, 63, v, v);
            Palette.Set(WeaponBar.ColorBase + tank.Weapon, 63, 63 - v, 63 - v);
            if (v == 63)
                break;
        }
    }

    /// <summary>
    /// [ORIGINAL] O7:069C–07C7: bit 0 (Herb) or 1 (Vick) of +26h toggles only with stock; DAC 118/119 white with stock,
    /// otherwise black, red while the bit is set.
    /// </summary>
    private void ToggleGuidance(TurnPlayer p, int kind)
    {
        if (p.GuidanceStock(kind) > 0)
            p.Tank.Guidance ^= 1 << kind;
        WeaponBar.SetGuidanceColors(Palette, p);
    }

    /// <summary>
    /// The input routine. [ORIGINAL] O7:0EF7 (BOMB.OVR 0x1B88D): docs/analyse/runde.md section 6.3 with the corrections of
    /// docs/analyse/zug.md section 4. Without stock, and for the laser with stock ≤ 100, 8 at once without delay.
    /// </summary>
    private IEnumerable<TurnWait> ReadCommand(TurnPlayer p, PlayerControl control, Action<TurnCommand> result)
    {
        yield return TurnWait.Poll(TurnPollSite.Input);
        var stock = p.Tank.Stock[p.Tank.Weapon];
        if (stock <= 0 || (stock <= 100 && p.Tank.Weapon == TurnPlayer.Laser))
        {
            result(TurnCommand.NextWeapon);
            yield break;
        }

        var command = TurnCommand.None;
        var k = Input;
        if (control == PlayerControl.Keyboard)
        {
            if (k[Scancode.PadDelete] && k[Scancode.PadInsert])
            {
                foreach (var w in Cheat(p))
                    yield return w;
            }

            if (k[Scancode.I])
            {
                command = TurnCommand.FlipDirection;
                foreach (var w in WaitForRelease("I", [Scancode.I]))
                    yield return w;
            }

            if (k[Scancode.H])
            {
                command = TurnCommand.ToggleHerb;
                foreach (var w in WaitForRelease("H", [Scancode.H]))
                    yield return w;
            }

            if (k[Scancode.V])
            {
                command = TurnCommand.ToggleVick;
                foreach (var w in WaitForRelease("V", [Scancode.V]))
                    yield return w;
            }

            if (k[Scancode.Tab] && (k[Scancode.LeftShift] || k[Scancode.RightShift]))
            {
                command = TurnCommand.PreviousWeapon;
                foreach (var w in WaitForRelease("Shift+Tab", [Scancode.Tab]))
                    yield return w;
            }

            if (k[Scancode.Tab])
            {
                command = TurnCommand.NextWeapon;
                foreach (var w in WaitForRelease("Tab", [Scancode.Tab]))
                    yield return w;
            }

            if (k[Scancode.I])
            {
                command = TurnCommand.FlipDirection;
                foreach (var w in WaitForRelease("I", [Scancode.I]))
                    yield return w;
            }

            if (k[Scancode.Space] || k[Scancode.Enter] || k[Scancode.PadEnter])
                command = TurnCommand.Fire;

            // O7:1125: key 0 is tank 10, then 1..9; the routine goes on afterwards.
            if (k[Scancode.Digit0])
            {
                foreach (var w in TankInfo(0, control))
                    yield return w;
            }

            for (var n = 1; n <= 9; n++)
            {
                if (k[Scancode.Digit1 + n - 1])
                {
                    foreach (var w in TankInfo(n, control))
                        yield return w;
                }
            }

            var alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadLeft] || k[Scancode.Left])
            {
                command = TurnCommand.AnglePlus;
                if (alt)
                    foreach (var w in WaitForRelease("Left", [Scancode.PadLeft, Scancode.Left]))
                        yield return w;
            }

            alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadRight] || k[Scancode.Right])
            {
                command = TurnCommand.AngleMinus;
                if (alt)
                    foreach (var w in WaitForRelease("Right", [Scancode.PadRight, Scancode.Right]))
                        yield return w;
            }

            alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadDown] || k[Scancode.Down])
            {
                if (alt)
                    foreach (var w in WaitForRelease("Down", [Scancode.PadDown, Scancode.Down]))
                        yield return w;
                command = TurnCommand.PowerDown;
            }

            alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadUp] || k[Scancode.Up])
            {
                command = TurnCommand.PowerUp;
                if (alt)
                    foreach (var w in WaitForRelease("Up", [Scancode.PadUp, Scancode.Up]))
                        yield return w;
            }

            alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadPageUp] || k[Scancode.PageUp])
            {
                command = TurnCommand.PowerUp;
                p.Tank.Power += 99;
                if (alt)
                    foreach (var w in WaitForRelease("PgUp", [Scancode.PadPageUp, Scancode.PageUp]))
                        yield return w;
            }

            alt = k[Scancode.Alt] || k[Scancode.RightAlt];
            if (k[Scancode.PadPageDown] || k[Scancode.PageDown])
            {
                command = TurnCommand.PowerDown;
                p.Tank.Power -= 99;
                if (alt)
                    foreach (var w in WaitForRelease("PgDn", [Scancode.PadPageDown, Scancode.PageDown]))
                        yield return w;
            }
        }

        if (control == PlayerControl.Mouse)
        {
            var b1 = k.Button(1);
            var b2 = k.Button(2);
            if (!b1 && !b2)
            {
                result(TurnCommand.None);
                yield break;
            }

            var x = k.MouseX;
            var y = k.MouseY;
            var color = _field.Screen[x, y];
            if (color is >= 20 and <= 29)
            {
                foreach (var w in TankInfo(color - 19, control))
                    yield return w;
            }

            if (y is >= 18 and <= 27)
            {
                if (x >= WeaponBar.FieldLeft(0) && x < WeaponBar.FieldLeft(18))
                {
                    if (b1)
                        command = TurnCommand.PreviousWeapon;
                    if (b2)
                        command = TurnCommand.NextWeapon;
                    foreach (var w in WaitForButtons("mouse bar"))
                        yield return w;
                }

                if (x >= WeaponBar.FieldLeft(18) && x < WeaponBar.FieldLeft(19))
                {
                    command = TurnCommand.ToggleHerb;
                    foreach (var w in WaitForButtons("mouse Herb"))
                        yield return w;
                }

                if (x >= WeaponBar.FieldLeft(19) && x < WeaponBar.FieldLeft(20))
                {
                    command = TurnCommand.ToggleVick;
                    foreach (var w in WaitForButtons("mouse Vick"))
                        yield return w;
                }
            }

            if (x is >= 56 and <= 70 && y is >= 0 and <= 7)
            {
                if (b1)
                    command = TurnCommand.AnglePlus;
                if (b2)
                    command = TurnCommand.AngleMinus;
            }

            if (x is >= 56 and <= 86 && y is >= 10 and <= 17)
            {
                if (b1)
                    command = TurnCommand.PowerDown;
                if (b2)
                    command = TurnCommand.PowerUp;
            }

            if (x is >= 8 and <= 54 && y is >= 10 and <= 17)
            {
                if (b1)
                {
                    command = TurnCommand.PowerDown;
                    p.Tank.Power -= 99;
                }

                if (b2)
                {
                    command = TurnCommand.PowerUp;
                    p.Tank.Power += 99;
                }
            }

            if (x is >= 296 and <= 302 && y is >= 0 and <= 7)
            {
                command = TurnCommand.FlipDirection;
                foreach (var w in WaitForButtons("mouse direction"))
                    yield return w;
            }

            if (b1 && b2)
                command = TurnCommand.Fire;
        }

        if (control == PlayerControl.Computer)
            throw new NotSupportedException("Computer players come with round 4 (docs/analyse/runde.md section 6.5).");

        yield return TurnWait.Delay(_settings.Delay);
        result(command);
    }

    /// <summary>
    /// [ORIGINAL] O7:0F76–1086: keypad Insert + Delete; loop until both again or Space: C restores colour 2 from the buffer
    /// (U431:0594(2, 2)), W all weapons 999, G Herb and Vick 10, D shield, inertia, repulser 1000.
    /// </summary>
    private IEnumerable<TurnWait> Cheat(TurnPlayer p)
    {
        var k = Input;
        k.FlushBios();
        k[Scancode.PadDelete] = false;
        k[Scancode.PadInsert] = false;
        do
        {
            yield return TurnWait.Poll(TurnPollSite.Cheat);
            if (k[Scancode.C])
                RestoreColor(2, 2);
            if (k[Scancode.W])
                Array.Fill(p.Tank.Stock, 999);
            if (k[Scancode.G])
                p.Tank.Herbs = p.Tank.Vicks = 10;
            if (k[Scancode.D])
                Array.Fill(p.Tank.Defense, 1000);
        }
        while (!((k[Scancode.PadDelete] && k[Scancode.PadInsert]) || k[Scancode.Space]));

        k.FlushBios();
        k[Scancode.Space] = false;
        k[Scancode.PadDelete] = false;
        k[Scancode.PadInsert] = false;
    }

    /// <summary>
    /// [ORIGINAL] U431:0594 (BOMB.EXE 0x05F44): every screen pixel with a colour in lo..hi gets the buffer's pixel
    /// (addresses 64000 down to 1; [APPROX] address 64000 lies outside both buffers and is skipped, address 0 is never
    /// touched like in the original).
    /// </summary>
    private void RestoreColor(byte lo, byte hi)
    {
        var screen = _field.Screen.Pixels;
        var buffer = _field.Background.Pixels;
        for (var a = screen.Length - 1; a >= 1; a--)
        {
            if (screen[a] >= lo && screen[a] <= hi)
                screen[a] = buffer[a];
        }
    }

    /// <summary>
    /// Tank info. [ORIGINAL] O9:0040 (BOMB.OVR 0x1E25D): n = 0 means 10; n above the player count returns at once (and
    /// leaves the keyboard in BIOS mode, docs/analyse/zug.md section 4 no. 4). Otherwise the window, then a key (keyboard)
    /// or a button (mouse), the computer does not wait; the tank's colour O10:014A; the screen is restored.
    /// </summary>
    private IEnumerable<TurnWait> TankInfo(int n, PlayerControl control)
    {
        var saved = _biosMode;
        _biosMode = true;
        Input.FlushBios();
        if (n == 0)
            n = 10;
        if (n > PlayerCount)
            yield break;

        var shown = Player(n);
        _view.ShowTankInfo(shown, _players);

        // O9:0223: O10:01AC(100, record): the name is drawn in the full colour of the tank.
        var (r, g, b) = PlayfieldModel.ColorDivisors[n - 1];
        Palette.Set(shown.Tank.Color, 63 / r, 63 / g, 63 / b);
        if (control == PlayerControl.Keyboard)
        {
            do
            {
                yield return TurnWait.Poll(TurnPollSite.InfoKey);
            }
            while (!Input.KeyPressed);
        }

        if (control == PlayerControl.Mouse)
        {
            do
            {
                yield return TurnWait.Poll(TurnPollSite.InfoMouse);
            }
            while (!Input.Button(1) && !Input.Button(2));
        }

        if (!saved)
            RestoreKeyboard();
        _field.UpdateColor(shown.Tank);
        _view.CloseTankInfo();
    }

    private void RestoreKeyboard()
    {
        _biosMode = false;
        Input.FlushBios();
    }

    /// <summary>
    /// Target of the guidance. [ORIGINAL] O9:092E (BOMB.OVR 0x1EB4B): keyboard – start at +28h, moved to the next live
    /// player that is not the shooter; → / ← (scancodes 77/75 after a 0) go on in that direction past the shooter and the
    /// dead without a new key; Enter or Space (set O9:08F7) end it. Mouse – a click on a live tank (colour 20..19 + N, the
    /// own included).
    /// </summary>
    private IEnumerable<TurnWait> SelectTarget(TurnPlayer shooter, PlayerControl control)
    {
        var saved = _biosMode;
        _biosMode = true;
        Input.FlushBios();
        var n = PlayerCount;
        if (control == PlayerControl.Keyboard)
        {
            if (!_players.Any(q => q.Number != shooter.Number && q.Tank.Active))
                throw new InvalidOperationException("[APPROX] The original loops forever without another live player.");
            while (!(shooter.Tank.GuidanceTarget >= 1 && shooter.Tank.GuidanceTarget <= n && Player(shooter.Tank.GuidanceTarget).Tank.Active
                     && shooter.Tank.GuidanceTarget != shooter.Number))
            {
                shooter.Tank.GuidanceTarget++;
                if (shooter.Tank.GuidanceTarget > n)
                    shooter.Tank.GuidanceTarget = 1;
                if (shooter.Tank.GuidanceTarget < 1)
                    shooter.Tank.GuidanceTarget = n;
            }

            _view.ShowTargetBar(shooter, mouse: false);
            Input.FlushBios();
            byte key;
            do
            {
                _view.DrawTarget(shooter, Player(shooter.Tank.GuidanceTarget));
                do
                {
                    yield return TurnWait.Poll(TurnPollSite.TargetKey);
                }
                while (!Input.KeyPressed);

                key = UpCase(Input.ReadKey());
                if (key == 0)
                {
                    key = Input.ReadKey();
                    do
                    {
                        if (key == 77)
                            shooter.Tank.GuidanceTarget++;
                        if (key == 75)
                            shooter.Tank.GuidanceTarget--;
                        if (shooter.Tank.GuidanceTarget > n)
                            shooter.Tank.GuidanceTarget = 1;
                        if (shooter.Tank.GuidanceTarget < 1)
                            shooter.Tank.GuidanceTarget = n;
                    }
                    while (shooter.Tank.GuidanceTarget == shooter.Number || !Player(shooter.Tank.GuidanceTarget).Tank.Active);
                }
            }
            while (key is not (13 or 32));
        }
        else if (control == PlayerControl.Mouse)
        {
            _view.ShowTargetBar(shooter, mouse: true);
            var done = false;
            do
            {
                yield return TurnWait.Poll(TurnPollSite.TargetMouse);
                if (Input.Button(1) || Input.Button(2))
                {
                    var color = _field.Screen[Input.MouseX, Input.MouseY];
                    if (color >= 20 && color <= 19 + n && Player(color - 19).Tank.Active)
                    {
                        shooter.Tank.GuidanceTarget = color - 19;
                        done = true;
                    }
                }
            }
            while (!done);
        }
        else
        {
            throw new NotSupportedException("Computer players come with round 4 (0129:01A6).");
        }

        _view.CloseTargetBar();
        if (!saved)
            RestoreKeyboard();
    }

    /// <summary>
    /// The MIRV dialog before the shot. [ORIGINAL] O4:13D4–1AB9 (BOMB.OVR 0x120D9): page 1 pattern 0..4, page 2 detonation
    /// 0..2; ↑/↓ (the game clears the key after reading it), Enter, keypad Enter or Space end a page, Esc on page 2 goes
    /// back to page 1; with the mouse only y counts and any button ends the page. The chosen line is DAC 130 + i =
    /// (63, 22, 22), the others (0, 0, 44). Before each page the dialog waits until both buttons are up. [APPROX] The
    /// explosion speed ES (DS:E8C6) is divided by 5 at the start and restored after the shot: that belongs to the shot.
    /// </summary>
    public IEnumerable<TurnWait> MirvSetup(int number, PlayerControl control)
    {
        _ = Player(number);
        int pattern;
        int detonation;
        var k = Input;
        while (true)
        {
            _view.ShowMirvPage(1);
            foreach (var w in WaitForButtons("mirv mouse 1"))
                yield return w;
            ClearFireKeys();
            pattern = 0;
            var shown = -1;
            var done = false;
            if (control == PlayerControl.Keyboard)
            {
                do
                {
                    yield return TurnWait.Poll(TurnPollSite.MirvKey1);
                    shown = MirvColors(pattern, shown);
                    pattern = MirvKeys(pattern, 4);
                    done = FireKeyHeld();
                }
                while (!done);
            }

            if (control == PlayerControl.Mouse)
            {
                do
                {
                    yield return TurnWait.Poll(TurnPollSite.MirvMouse1);
                    shown = MirvColors(pattern, shown);
                    var y = k.MouseY;
                    if (y < 89)
                        pattern = 0;
                    if (y > 91 && y < 99)
                        pattern = 1;
                    if (y > 101 && y < 109)
                        pattern = 2;
                    if (y > 111 && y < 119)
                        pattern = 3;
                    if (y > 121)
                        pattern = 4;
                    done = k.Button(1) || k.Button(2);
                }
                while (!done);
            }

            _view.ShowMirvPage(2);
            foreach (var w in WaitForButtons("mirv mouse 2"))
                yield return w;
            ClearFireKeys();
            detonation = 0;
            shown = -1;
            var back = false;
            done = false;
            if (control == PlayerControl.Keyboard)
            {
                do
                {
                    yield return TurnWait.Poll(TurnPollSite.MirvKey2);
                    shown = MirvColors(detonation, shown);
                    detonation = MirvKeys(detonation, 2);
                    done = FireKeyHeld();
                    if (k[Scancode.Esc])
                    {
                        back = true;
                        break;
                    }
                }
                while (!done);
            }

            if (control == PlayerControl.Mouse)
            {
                do
                {
                    yield return TurnWait.Poll(TurnPollSite.MirvMouse2);
                    shown = MirvColors(detonation, shown);
                    var y = k.MouseY;
                    if (y < 89)
                        detonation = 0;
                    if (y > 91 && y < 99)
                        detonation = 1;
                    if (y > 101)
                        detonation = 2;
                    done = k.Button(1) || k.Button(2);
                }
                while (!done);
            }

            if (!back)
                break;
            _view.CloseMirv();
        }

        MirvChoice = (pattern, detonation);
        _view.CloseMirv();
    }

    /// <summary>System.UpCase (12CE:2056): only a..z.</summary>
    private static byte UpCase(byte c) => c is >= (byte)'a' and <= (byte)'z' ? (byte)(c - 32) : c;

    private void ClearFireKeys()
    {
        Input[Scancode.PadEnter] = false;
        Input[Scancode.Space] = false;
        Input[Scancode.Enter] = false;
    }

    private bool FireKeyHeld() => Input[Scancode.Enter] || Input[Scancode.PadEnter] || Input[Scancode.Space];

    private int MirvColors(int chosen, int shown)
    {
        if (shown != chosen)
        {
            for (var i = 130; i <= 134; i++)
                Palette.Set(i, 0, 0, 44);
            Palette.Set(130 + chosen, 63, 22, 22);
        }

        return chosen;
    }

    private int MirvKeys(int value, int max)
    {
        var k = Input;
        foreach (var key in (int[])[Scancode.PadUp, Scancode.Up])
        {
            if (k[key] && value > 0)
            {
                value--;
                k[key] = false;
            }
        }

        foreach (var key in (int[])[Scancode.PadDown, Scancode.Down])
        {
            if (k[key] && value < max)
            {
                value++;
                k[key] = false;
            }
        }

        return value;
    }
}
