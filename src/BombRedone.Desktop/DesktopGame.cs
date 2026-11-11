using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.UI;
using BombRedone.UI.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BombRedone.Desktop;

/// <summary>
/// Window of the game: feeds time and input to the current screen and shows its palette frame buffer scaled by whole
/// numbers. F11 toggles full screen, F10 toggles the 4:3 correction, Esc quits; these keys are not passed to the screens.
/// </summary>
public sealed class DesktopGame : Game
{
    private const int DefaultScale = 3;

    /// <summary>Keys the window keeps for itself.</summary>
    private static readonly Keys[] WindowKeys = [Keys.Escape, Keys.F10, Keys.F11];

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
    private bool _aspectCorrection = true;

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
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _texture = new Texture2D(GraphicsDevice, _surface.Width, _surface.Height);
    }

    protected override void Update(GameTime gameTime)
    {
        var keys = Keyboard.GetState();
        if (Pressed(keys, Keys.Escape))
            Exit();
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
    /// the options screen, START the player selection; Sound Options and loading follow later (the menu stays or a
    /// placeholder), the game after the player selection is a placeholder.
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
            MenuChoice.Start => new PlayerSelectScreen(menu, roster, random.Next, _ => new PlaceholderScreen(), teams),
            _ => new PlaceholderScreen(),
        });
    }

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
        var shift = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
        return new ScreenInput(keyPressed, bothShifts, buttons)
        {
            Keys = typed.Select(k => ToBiosKey(k, shift)).Where(k => k != 0).ToList(),
            MouseX = mouseX,
            MouseY = mouseY,
        };
    }

    /// <summary>BIOS key word of a key, as far as the menus use it; 0 for keys without one here.</summary>
    private static ushort ToBiosKey(Keys key, bool shift) => key switch
    {
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
        >= Keys.A and <= Keys.Z => BiosKey.Char((char)((shift ? 'A' : 'a') + (key - Keys.A))),
        >= Keys.D0 and <= Keys.D9 => BiosKey.Char((char)('0' + (key - Keys.D0))),
        _ => 0,
    };

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);
}
