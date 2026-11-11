using BombRedone.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BombRedone.Desktop;

/// <summary>
/// Window of the game: shows the palette frame buffer scaled by whole numbers.
/// F11 toggles full screen, F10 toggles the 4:3 correction, Esc quits.
/// </summary>
public sealed class DesktopGame : Game
{
    private const int DefaultScale = 3;

    private readonly GraphicsDeviceManager _graphics;
    private readonly Surface _surface = new();
    private readonly uint[] _rgba;
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
        // Opaque black as palette entry 0 until the screens set the original palette.
        _surface.Palette[0] = 0xFF000000;
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
        _previousKeys = keys;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
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

    private bool Pressed(KeyboardState keys, Keys key) => keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);
}
