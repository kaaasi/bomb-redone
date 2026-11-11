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
        _screens = new ScreenHost(new TitleScreen(LoadTitleImages(), () => new PlaceholderScreen()));
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

    private ScreenInput ReadInput(KeyboardState keys)
    {
        var keyPressed = keys.GetPressedKeys().Any(k =>
            _previousKeys.IsKeyUp(k) && !WindowKeys.Contains(k) && !ModifierKeys.Contains(k));
        var bothShifts = keys.IsKeyDown(Keys.LeftShift) && keys.IsKeyDown(Keys.RightShift);
        var mouse = Mouse.GetState();
        var buttons = MouseButtons.None;
        if (IsActive && mouse.LeftButton == ButtonState.Pressed)
            buttons |= MouseButtons.Left;
        if (IsActive && mouse.RightButton == ButtonState.Pressed)
            buttons |= MouseButtons.Right;
        return new ScreenInput(keyPressed, bothShifts, buttons);
    }

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);
}
