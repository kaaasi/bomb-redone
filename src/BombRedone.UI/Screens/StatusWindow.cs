using System.Globalization;
using BombRedone.Formats;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>Values the "Game Status" window shows besides the game options.</summary>
/// <param name="Settings">Game options (DS:E8A4, E8AA, E8B4, E8B5, E8B8, E8BA, E8AC, E8A8).</param>
/// <param name="SoundEffects">"Effect" (DS:E8B3; Ctrl-S toggles it, UC02:03DF).</param>
/// <param name="FlySound">"Fly" (DS:E8B2).</param>
/// <param name="Game">Number of the current game (DS:47E2).</param>
/// <param name="Games">Games of the battle (DS:E8A6).</param>
/// <param name="RoundWalls">Walls of this round (DS:47EC; differs from the option with "RND").</param>
/// <param name="RoundWind">Wind of this round (DS:47E4).</param>
public sealed record StatusWindowData(OptionsSettings Settings, bool SoundEffects, bool FlySound, int Game, int Games,
    WallSetting RoundWalls, WindSetting RoundWind);

/// <summary>
/// "Game Status" window during a round (Ctrl-P): U431:0F1B (BOMB.EXE 0x068CB); docs/anforderungen/wertung-einkauf.md §8.
/// </summary>
/// <remarks>
/// [ORIGINAL] UC02:041F–042B (BOMB.EXE 0x0DAEB): Ctrl and P (key table DS:452A + scan code, P = 19h → DS:4543) open the
/// window. It saves the screen to DS:E5BC, draws over it, waits for a key or button, waits for its release and copies
/// the saved screen back (U431:1725); the round continues unchanged.
/// </remarks>
public sealed class StatusWindow : IScreen
{
    /// <summary>[ORIGINAL] U431:0FA1–0FEA: black bar (10,25)–(310,175), two outlines in colour 13.</summary>
    public const byte FrameColor = 13;

    /// <summary>[ORIGINAL] Colours of the lines: label 1, text 5, value 14, title 15.</summary>
    public const byte LabelColor = 1, TextColor = 5, ValueColor = 14, TitleColor = 15;

    private readonly Surface _saved = new();
    private readonly byte[] _dac;
    private readonly GraphText _text;
    private readonly Func<IScreen?> _close;
    private bool _waitingForRelease;
    private int _x, _y;

    /// <param name="font">
    /// 8 x 8 font. [APPROX] The window sets no font (U431:0F1B keeps the text settings of the round and restores them);
    /// the playfield is drawn with the DefaultFont (O4:44A7), which is assumed here.
    /// </param>
    /// <param name="screen">The playfield under the window.</param>
    /// <param name="dac">6-bit DAC of the playfield (768 bytes); the window does not change it.</param>
    /// <param name="data">Values to show.</param>
    /// <param name="close">Called when the window closes; returns the next screen (the round).</param>
    public StatusWindow(BitmapFont font, Surface screen, ReadOnlySpan<byte> dac, StatusWindowData data,
        Func<IScreen?> close)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(close);
        if (screen.Width != 320 || screen.Height != 200)
            throw new ArgumentException("The status window draws into a 320 x 200 surface only.", nameof(screen));
        if (dac.Length != 768)
            throw new ArgumentException("The DAC has 768 entries.", nameof(dac));
        _dac = dac.ToArray();
        _close = close;
        _text = new GraphText(font);
        screen.Pixels.CopyTo(_saved.Pixels, 0);
        screen.Pixels.CopyTo(Screen.Pixels, 0);
        DrawWindow(data);
    }

    /// <summary>Video memory with the window.</summary>
    public Surface Screen { get; } = new();

    /// <summary>The screen as it was before the window (buffer DS:E5BC), copied back on closing.</summary>
    public Surface Saved => _saved;

    /// <summary>6-bit DAC.</summary>
    public ReadOnlySpan<byte> Dac => _dac;

    /// <summary>
    /// [ORIGINAL] U431:1660–16F0: waits until a mouse button or Esc, Enter or Space is down (DS:452B, 4546, 4563 = key
    /// table + scan code 01h, 1Ch, 39h), then until all of them are up. [APPROX] Keys come as typed key words here, not as
    /// held keys: a typed Esc, Enter or Space counts as pressed and as released in the next update without one.
    /// </summary>
    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        var down = input.MouseHolds(MouseButtons.Left) || input.MouseHolds(MouseButtons.Right)
            || input.Keys.Any(k => k is BiosKey.Escape or BiosKey.Enter or BiosKey.Space);
        if (!_waitingForRelease)
        {
            _waitingForRelease = down;
            return this;
        }

        if (down)
            return this;
        // [ORIGINAL] U431:1725–1737: the saved screen goes back to video memory.
        _saved.Pixels.CopyTo(Screen.Pixels, 0);
        return _close();
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (surface.Width != Screen.Width || surface.Height != Screen.Height)
            throw new ArgumentException("The status window draws into a 320 x 200 surface only.", nameof(surface));
        Screen.Pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(_dac, surface);
    }

    /// <summary>
    /// [ORIGINAL] U431:0F69–165B (BOMB.EXE 0x06919): viewport (0,0)–(319,199), SetFillStyle(SolidFill, 0), Bar, two
    /// rectangles (UCC8:0031), title centred at (160, 35) in colour 15; then left/centred lines with MoveTo and OutText
    /// (texts U431:0E30–0F11, "Backgound" as in the original). Second lines of a group start below the first text after
    /// the label (GetX, U431:11A8, 128B, 15B0).
    /// </summary>
    private void DrawWindow(StatusWindowData data)
    {
        var s = data.Settings;
        MenuGraphics.Bar(Screen, 10, 25, 310, 175, 0);
        MenuGraphics.Rectangle(Screen, 10, 25, 310, 175, FrameColor);
        MenuGraphics.Rectangle(Screen, 12, 27, 308, 173, FrameColor);

        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        _text.Color = TitleColor;
        _text.OutTextXY(Screen, 160, 35, "Game Status");
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Center);

        MoveTo(20, 45);
        Out(LabelColor, "Sound: ");
        Value("Effect (", OptionsSettings.YesNo(data.SoundEffects));
        Value(")   Fly (", OptionsSettings.YesNo(data.FlySound));
        Out(TextColor, ")");

        MoveTo(20, 55);
        Out(LabelColor, "Ground: ");
        Value("Crumbles (", Number(s.CrumblePercent) + "%");
        Out(TextColor, ")");

        MoveTo(20, 65);
        Out(LabelColor, "The Walls: ");
        var column = _x;
        Value("Rebound (", OptionsSettings.WallName(s.Walls));
        Out(TextColor, ")");
        MoveTo(column, 75);
        Value("This Round (", OptionsSettings.WallName(data.RoundWalls));
        Out(TextColor, ")");

        MoveTo(20, 85);
        Out(LabelColor, "The Shot: ");
        column = _x;
        Value("Invisible (", OptionsSettings.YesNo(s.InvisibleShot));
        Out(TextColor, ")");
        MoveTo(column, 95);
        Value("Trail (", OptionsSettings.YesNo(s.TrailVisible));
        Out(TextColor, ")");

        MoveTo(20, 105);
        Out(LabelColor, "Game: ");
        Value("Number (", Number(data.Game));
        Value(") of (", Number(data.Games));
        Out(TextColor, ")");

        MoveTo(20, 115);
        Out(LabelColor, "Tanks: ");
        Value("Fall (", OptionsSettings.YesNo(s.TanksFall));
        Out(TextColor, ")");

        // [ORIGINAL] U431:147B: the next line is at y = 135; y = 125 stays empty.
        MoveTo(20, 135);
        Out(LabelColor, "Computers: ");
        Value("Buy Weapons (", OptionsSettings.YesNo(s.ComputersBuy));
        Out(TextColor, ")");

        MoveTo(20, 145);
        Out(LabelColor, "Screen: ");
        Value("Backgound (", OptionsSettings.BackgroundName(s.Background));
        Out(TextColor, ")");

        MoveTo(20, 155);
        Out(LabelColor, "The Wind: ");
        column = _x;
        Value("Control (", OptionsSettings.WindName(s.Wind));
        Out(TextColor, ")");
        MoveTo(column, 165);
        Value("This Round (", OptionsSettings.WindName(data.RoundWind));
        Out(TextColor, ")");
    }

    private void MoveTo(int x, int y) => (_x, _y) = (x, y);

    private void Value(string text, string value)
    {
        Out(TextColor, text);
        Out(ValueColor, value);
    }

    /// <summary>OutText: draws at the current position and moves it by the text width (left-justified, horizontal).</summary>
    private void Out(byte color, string text)
    {
        _text.Color = color;
        _text.OutTextXY(Screen, _x, _y, text);
        _x += _text.TextWidth(text);
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
