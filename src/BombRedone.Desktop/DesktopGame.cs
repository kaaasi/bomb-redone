using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Round;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;
using BombRedone.Formats;
using BombRedone.UI;
using BombRedone.UI.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BombRedone.Desktop;

/// <summary>
/// Window of the game: feeds time and input to the current screen and shows its palette frame buffer scaled by whole
/// numbers. F11 toggles full screen, F10 toggles the 4:3 correction; these keys are not passed to the screens. Esc goes
/// to the screens like any other key (START, Random, Done, leaving dialogs); the window is closed with its close button
/// (or Alt+F4) or when the screens end.
/// </summary>
public sealed class DesktopGame : Game
{
    private const int DefaultScale = 3;

    /// <summary>Keys the window keeps for itself.</summary>
    private static readonly Keys[] WindowKeys = [Keys.F10, Keys.F11];

    /// <summary>Keys that put nothing into the BIOS keyboard buffer when pressed alone.</summary>
    private static readonly Keys[] ModifierKeys =
    [
        Keys.LeftShift, Keys.RightShift, Keys.LeftControl, Keys.RightControl, Keys.LeftAlt, Keys.RightAlt,
        Keys.LeftWindows, Keys.RightWindows, Keys.CapsLock, Keys.NumLock, Keys.Scroll,
    ];

    private readonly GraphicsDeviceManager _graphics;
    private readonly Surface _surface = new();
    private readonly uint[] _rgba;
    private readonly ScreenHost _screens;
    private SpriteBatch? _batch;
    private Texture2D? _texture;
    private KeyboardState _previousKeys;
    private readonly List<ushort> _typed = [];
    private bool _aspectCorrection = true;

    /// <summary>
    /// The end result of the last battle (O3[1], <see cref="ScoreResults"/>), printed when the program ends like the original
    /// in text mode; here on the console.
    /// </summary>
    public static IReadOnlyList<TextLine> Results { get; private set; } = [];

    /// <summary>
    /// The playfield of the last round of the program run, for the CRI list DS:E820 (#165, review P3-3 #176). [ORIGINAL] Every
    /// reference to offset E820h lies in the CRI (BOMB.OVR 0x19964–0x19D35); nothing else clears the global, so the list goes
    /// on over the rounds of all battles.
    /// </summary>
    private static Playfield? _lastField;

    public DesktopGame()
    {
        _rgba = new uint[_surface.Width * _surface.Height];
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _surface.Width * DefaultScale,
            PreferredBackBufferHeight = _surface.Height * DefaultScale * Presentation.AspectNumerator
                                        / Presentation.AspectDenominator,
        };
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Bomb Redone";
        _screens = new ScreenHost(new TitleScreen(LoadTitleImages(), CreateMenu));
        // Printable characters come from the text input (keyboard layout and Shift applied: "?", punctuation, letters).
        Window.TextInput += (_, e) =>
        {
            var word = BiosKey.FromTypedCharacter(e.Character);
            if (word != 0)
                _typed.Add(word);
        };
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _texture = new Texture2D(GraphicsDevice, _surface.Width, _surface.Height);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (Pressed(keys, Keys.F11))
            _graphics.ToggleFullScreen();
        if (Pressed(keys, Keys.F10))
            _aspectCorrection = !_aspectCorrection;

        _screens.Update(gameTime.ElapsedGameTime, ReadInput(keys));
        if (!_screens.IsRunning)
            Exit();
        _previousKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screens.Draw(_surface);
        _surface.CopyTo(_rgba);
        _texture!.SetData(_rgba);

        var view = GraphicsDevice.Viewport;
        var rect = Presentation.Fit(view.Width, view.Height, _surface.Width, _surface.Height, _aspectCorrection);
        GraphicsDevice.Clear(Color.Black);
        _batch!.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_texture, new Rectangle(rect.X, rect.Y, rect.Width, rect.Height), Color.White);
        _batch.End();
        base.Draw(gameTime);
    }

    /// <summary>Title files from the original data; without them the title is skipped like without BOMB2.RAW.</summary>
    private static TitleImages LoadTitleImages()
    {
        try
        {
            return TitleImages.Load(GameData.Locate());
        }
        catch (DirectoryNotFoundException e)
        {
            Console.Error.WriteLine(e.Message);
            return TitleImages.None;
        }
    }

    /// <summary>
    /// Main menu after the title. Without the original files (fonts in BOMB.EXE) a placeholder stays. Game Options open
    /// the options screen, START the player selection and then the battle: rounds, status screens, shop, until the last
    /// game; then back to the menu (the end result is printed when the program ends). Sound Options and loading follow
    /// later (the menu stays).
    /// </summary>
    private static IScreen CreateMenu()
    {
        GameData gameData;
        try
        {
            gameData = GameData.Locate();
        }
        catch (DirectoryNotFoundException e)
        {
            Console.Error.WriteLine(e.Message);
            return new PlaceholderScreen();
        }

        var fonts = MenuFonts.Load(gameData, BitmapFontSource.Load().Font);
        var shopFonts = ShopFonts.Load(gameData, BitmapFontSource.Load().Font);
        var configPath = gameData.Find("BOMB.CFG");
        var config = configPath is null ? null : GameConfig.Load(configPath);
        // Without BOMB.CFG the defaults of BOMBCFG.EXE apply (R1-D5 = C); the original halts instead.
        var settings = MenuSettings.ForConfig(config, mouseAvailable: true);
        var options = OptionsSettings.ForConfig(config);
        var random = TurboPascalRandom.FromClock(DateTime.Now);
        var roster = new PlayerRoster();
        var teams = new PlayerSelectTeams(options, BgiFontLibrary.Load(gameData)[BgiFontLibrary.Tiny]);

        return new MenuScreen(fonts, settings, random.Next, (choice, menu) => choice switch
        {
            MenuChoice.GameOptions => new OptionsScreen(menu, options),
            MenuChoice.SoundOptions => menu,
            MenuChoice.Start => new PlayerSelectScreen(menu, roster, random.Next,
                setup => Battle(fonts, shopFonts, config ?? GameConfig.BombCfgDefaults, options, random, setup, menu), teams),
            _ => new PlaceholderScreen(),
        });
    }

    /// <summary>
    /// A battle after the player selection (O3[0], spur spielfolge and runde): new records (O2:0216 stocks), per game the
    /// shuffle, the round (<see cref="PlayfieldRoundScreen"/> on <see cref="RoundGame"/>), the status screens and the shop
    /// (<see cref="ScoreBattleFlow"/>); after the last game the end result and back to <paramref name="menu"/>.
    /// </summary>
    private static IScreen? Battle(MenuFonts fonts, ShopFonts shopFonts, GameConfig cfg, OptionsSettings options,
        TurboPascalRandom random, GameSetup setup, IScreen menu)
    {
        var records = BattleRecords.FromSetup(setup);
        RoundGame.InitializeRecords(records);
        var battle = new BattleSequence(records, Math.Max(1, cfg.GamesToPlay), options.TeamPlay, random, null,
            ItemCatalog.Original);
        var teamNames = setup.Teams.ToDictionary(t => t.Number, t => t.Name);

        IScreen? PlayRound(Func<int, IScreen?> complete)
        {
            var playfieldOptions = new PlayfieldOptions(records.Count, (int)options.Walls, options.Background,
                cfg.UserBackgroundMask, (int)options.Wind, options.InvisibleShot);
            var game = RoundGame.Prepare(records, playfieldOptions, random, _lastField);
            _lastField = game.Field;
            if (options.TeamPlay)
            {
                for (var i = 0; i < records.Count; i++)
                    game.Players[i].TeamName = teamNames.GetValueOrDefault(records.InOrder[i].Team, "");
            }

            var screen = new PlayfieldScreen(fonts, game.Field, game.Wind, game.Players.Select(p => p.Name).ToArray());
            var view = new PlayfieldTurnView(screen, fonts, game.Wind, teamPlay: options.TeamPlay);
            // The pictures draw their Random numbers with the sound option (DS:E8B3) as in the original. [APPROX] No sound
            // output yet; the turn yields no tones (TurnSettings.Sound false).
            var roundSettings = new RoundSettings(cfg.ShotSpeed, game.Wind.Value, options.CrumblePercent, options.TanksFall,
                cfg.SoundEffects, options.TrailVisible);
            var turn = new TurnSettings(Sound: false, Delay: cfg.TurretSpeed, TeamPlay: options.TeamPlay);
            return new PlayfieldRoundScreen(screen, view, game.Players, [.. records.InOrder.Select(r => r.Control)],
                game.Start, HeldScancodes, roundSettings, random, ShotTiming.FromOptions(cfg.ExplosionSpeed, cfg.FallSpeed),
                turn, winner =>
                {
                    game.Finish();
                    return complete(winner);
                });
        }

        var flow = new ScoreBattleFlow(shopFonts, battle, ItemCatalog.Original, setup.AllComputers, RecordColor,
            t => teamNames.GetValueOrDefault(t, ""), () =>
            {
                Results = ScoreResults.Build(records.InOrder.Select(r => r.Account), battle.Game);
                return menu;
            }, PlayRound, () => _lastField?.Palette);
        return flow.Start();
    }

    /// <summary>6-bit colour of a record at full energy (O10:01AC with the divisors of its player, #153).</summary>
    private static (int Red, int Green, int Blue) RecordColor(BattlePlayer record)
    {
        var (r, g, b) = Playfield.ColorDivisors[record.Account.Number - 1];
        return (63 / r, 63 / g, 63 / b);
    }

    /// <summary>
    /// The held-key table DS:452A of the keyboard handler U0C48:0008 from the keyboard state (scancodes, E0 keys + 100h).
    /// [APPROX] The window layer does not tell the keypad Enter from Enter; the keypad keys count as keypad keys (52h, 53h:
    /// cheat key Ins + Del) whatever NumLock says.
    /// </summary>
    private static bool[] HeldScancodes()
    {
        var held = new bool[Scancode.Count];
        var state = Keyboard.GetState();
        foreach (var key in state.GetPressedKeys())
        {
            var code = ToScancode(key);
            if (code > 0)
                held[code] = true;
        }

        return held;
    }

    private static int ToScancode(Keys key) => key switch
    {
        Keys.Escape => Scancode.Esc,
        >= Keys.D1 and <= Keys.D9 => Scancode.Digit1 + (key - Keys.D1),
        Keys.D0 => Scancode.Digit0,
        Keys.Tab => Scancode.Tab,
        Keys.W => Scancode.W,
        Keys.I => Scancode.I,
        Keys.Enter => Scancode.Enter,
        Keys.LeftControl => 0x1D,
        Keys.D => Scancode.D,
        Keys.G => Scancode.G,
        Keys.H => Scancode.H,
        Keys.LeftShift => Scancode.LeftShift,
        Keys.C => Scancode.C,
        Keys.V => Scancode.V,
        Keys.RightShift => Scancode.RightShift,
        Keys.LeftAlt => Scancode.Alt,
        Keys.RightAlt => Scancode.RightAlt,
        Keys.Space => Scancode.Space,
        Keys.NumPad8 => Scancode.PadUp,
        Keys.NumPad9 => Scancode.PadPageUp,
        Keys.NumPad4 => Scancode.PadLeft,
        Keys.NumPad6 => Scancode.PadRight,
        Keys.NumPad2 => Scancode.PadDown,
        Keys.NumPad3 => Scancode.PadPageDown,
        Keys.NumPad0 => Scancode.PadInsert,
        Keys.Decimal => Scancode.PadDelete,
        Keys.Up => Scancode.Up,
        Keys.PageUp => Scancode.PageUp,
        Keys.Left => Scancode.Left,
        Keys.Right => Scancode.Right,
        Keys.Down => Scancode.Down,
        Keys.PageDown => Scancode.PageDown,
        Keys.Insert => Scancode.Insert,
        Keys.Delete => Scancode.Delete,
        _ => 0,
    };

    private ScreenInput ReadInput(KeyboardState keys)
    {
        var typed = keys.GetPressedKeys()
            .Where(k => _previousKeys.IsKeyUp(k) && !WindowKeys.Contains(k) && !ModifierKeys.Contains(k))
            .ToList();
        var keyPressed = typed.Count > 0;
        var bothShifts = keys.IsKeyDown(Keys.LeftShift) && keys.IsKeyDown(Keys.RightShift);
        var mouse = Mouse.GetState();
        var buttons = MouseButtons.None;
        if (IsActive && mouse.LeftButton == ButtonState.Pressed)
            buttons |= MouseButtons.Left;
        if (IsActive && mouse.RightButton == ButtonState.Pressed)
            buttons |= MouseButtons.Right;
        // [APPROX] Mouse driver coordinates from the window position: horizontal 0..639, vertical 0..199.
        var view = GraphicsDevice.Viewport;
        var rect = Presentation.Fit(view.Width, view.Height, _surface.Width, _surface.Height, _aspectCorrection);
        var mouseX = rect.Width > 0 ? Math.Clamp((mouse.X - rect.X) * 640 / rect.Width, 0, 639) : 0;
        var mouseY = rect.Height > 0 ? Math.Clamp((mouse.Y - rect.Y) * 200 / rect.Height, 0, 199) : 0;
        // [APPROX] Keys without a character first, then the typed characters of this frame; the order between the two
        // within one frame is not known (the BIOS buffer would hold them in typing order).
        var words = typed.Select(ToBiosKey).Where(k => k != 0).Concat(_typed).ToList();
        _typed.Clear();
        return new ScreenInput(keyPressed || words.Count > 0, bothShifts, buttons)
        {
            Keys = words,
            MouseX = mouseX,
            MouseY = mouseY,
        };
    }

    /// <summary>
    /// BIOS key word of a key without a printable character, as far as the screens use it; 0 for other keys (printable
    /// characters arrive through <see cref="GameWindow.TextInput"/>, <see cref="BiosKey.FromTypedCharacter"/>).
    /// </summary>
    private static ushort ToBiosKey(Keys key) => key switch
    {
        Keys.Escape => BiosKey.Escape,
        Keys.Enter => BiosKey.Enter,
        Keys.Space => BiosKey.Space,
        Keys.Up => BiosKey.Up,
        Keys.Down => BiosKey.Down,
        Keys.Left => BiosKey.Left,
        Keys.Right => BiosKey.Right,
        Keys.Back => 0x0E08,
        Keys.Home => 0x4700,
        Keys.End => 0x4F00,
        Keys.Delete => 0x5300,
        _ => 0,
    };

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);
}
