using System.Globalization;
using BombRedone.Core.Economy;
using BombRedone.Core.Playfield;
using BombRedone.Core.Turn;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// The BGI side of a turn on a <see cref="PlayfieldScreen"/> (docs/analyse/runde.md section 6, docs/analyse/zug.md):
/// header fields, tank info window, target bar and MIRV dialog. Windows save the screen and restore it when they close,
/// like the original (buffer DS:E5BC). Field pixels and DAC come from Core (<see cref="Refresh"/>).
/// </summary>
public sealed class PlayfieldTurnView : ITurnView
{
    /// <summary>[ORIGINAL] O7:0424 / 0533: the fields are drawn in colour 14, afterwards the colour is 15.</summary>
    public const byte FieldColor = 14;

    private readonly PlayfieldScreen _screen;
    private readonly GraphText _text;
    private readonly Wind _wind;
    private readonly ItemCatalog _catalog;
    private readonly bool _teamPlay;
    private byte[]? _saved;

    /// <param name="fonts">DefaultFont of the turn (O4:44B4 SetTextStyle(DefaultFont, HorizDir, 1)).</param>
    public PlayfieldTurnView(PlayfieldScreen screen, MenuFonts fonts, Wind wind, ItemCatalog? catalog = null,
        bool teamPlay = false)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(fonts);
        _screen = screen;
        _text = new GraphText(fonts.Default);
        _text.SetTextStyle(null, TextDirection.Horizontal, 1);
        _wind = wind;
        _catalog = catalog ?? ItemCatalog.Original;
        _teamPlay = teamPlay;
    }

    private Surface Target => _screen.Screen;

    /// <summary>A window (tank info, MIRV dialog) covers the field.</summary>
    public bool WindowOpen { get; private set; }

    /// <summary>Takes field and DAC from Core unless a window covers the field.</summary>
    public void Refresh()
    {
        if (!WindowOpen)
            _screen.RefreshField();
    }

    internal static string Str(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>[ORIGINAL] DS:0064 (27, ←) for direction −1, DS:0066 (26, →) for +1 (O7:04E8 reads [dir + 65h]).</summary>
    internal static byte Arrow(int direction) => direction < 0 ? PlayfieldScreen.WindLeft : PlayfieldScreen.WindRight;

    /// <summary>
    /// [ORIGINAL] O4:482C–4910: SetFillStyle(SolidFill, 0), Bar (109, 0)–(211, 7), name in colour 19 + n centred at
    /// (160, 0); colour 9 "Wind: " + |wind| + " " + arrow centred at (160, 10).
    /// </summary>
    public void TurnStarted(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Refresh();
        MenuGraphics.Bar(Target, 109, 0, 211, 7, 0);
        _text.Color = player.Tank.Color;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        _text.OutTextXY(Target, 160, 0, player.Name);
        _text.Color = PlayfieldScreen.WindColor;
        var line = new List<byte>(GraphText.ToBytes("Wind: " + Str(Math.Abs(_wind.Value)) + " "));
        line.Add(_wind.Value < 0 ? PlayfieldScreen.WindLeft : PlayfieldScreen.WindRight);
        _text.OutTextXY(Target, 160, 10, line.ToArray());
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
    }

    /// <summary>
    /// [ORIGINAL] O4:4BB8–4C07: a record died: Bar (109, 0)–(211, 7) in black, its name in colour 19 + n centred at (160, 0).
    /// </summary>
    public void DeathStarted(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Refresh();
        MenuGraphics.Bar(Target, 109, 0, 211, 7, 0);
        _text.Color = player.Tank.Color;
        _text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        _text.OutTextXY(Target, 160, 0, player.Name);
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
    }

    /// <summary>[ORIGINAL] O7:0424–0537: colour 14, left/top; each field Bar in black, then the value.</summary>
    public void DrawFields(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Refresh();
        DrawAngle(player);
        DrawPower(player);
        DrawDirection(player);
        Field(248, 10, 270, 17, GraphText.ToBytes(Str(player.Tank.Energy)));
    }

    /// <summary>[ORIGINAL] O7:0A6A: Bar (56, 0)–(70, 7), angle at (56, 0).</summary>
    public void DrawAngle(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Field(56, 0, 70, 7, GraphText.ToBytes(Str(player.Tank.Angle)));
    }

    /// <summary>[ORIGINAL] O7:0BAB: Bar (56, 10)–(86, 17), power at (56, 10).</summary>
    public void DrawPower(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Field(56, 10, 86, 17, GraphText.ToBytes(Str(player.Tank.Power)));
    }

    /// <summary>[ORIGINAL] O7:0E28: Bar (296, 0)–(302, 7), arrow at (296, 0).</summary>
    public void DrawDirection(TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Field(296, 0, 302, 7, [Arrow(player.Tank.Direction)]);
    }

    private void Field(int x1, int y1, int x2, int y2, byte[] value)
    {
        MenuGraphics.Bar(Target, x1, y1, x2, y2, 0);
        _text.Color = FieldColor;
        _text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        _text.OutTextXY(Target, x1, y1, value);
    }

    public void ShowTankInfo(TurnPlayer shown, IReadOnlyList<TurnPlayer> players)
    {
        Save();
        WindowOpen = true;
        TankInfoWindow.Draw(Target, _text, shown, _catalog, _teamPlay);
    }

    public void CloseTankInfo() => Restore();

    public void ShowTargetBar(TurnPlayer shooter, bool mouse)
    {
        Refresh();
        Save();
        TargetSelectBar.Draw(Target, _text, shooter, mouse);
    }

    public void DrawTarget(TurnPlayer shooter, TurnPlayer target) =>
        TargetSelectBar.DrawTarget(Target, _text, shooter, target);

    public void CloseTargetBar() => Restore();

    public void ShowMirvPage(int page)
    {
        if (!WindowOpen)
        {
            Refresh();
            Save();
            WindowOpen = true;
        }

        MirvSetupDialog.Draw(Target, _text, page);
    }

    public void CloseMirv() => Restore();

    private void Save() => _saved = (byte[])Target.Pixels.Clone();

    private void Restore()
    {
        if (_saved is not null)
            _saved.CopyTo(Target.Pixels, 0);
        WindowOpen = false;
    }
}
