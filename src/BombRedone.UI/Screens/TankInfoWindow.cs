using BombRedone.Core.Economy;
using BombRedone.Core.Turn;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>
/// The tank info window. [ORIGINAL] O9:0040–0857 (BOMB.OVR 0x1E25D): 260 pixels wide, 60 high plus 12 per extra line
/// (team name, each weapon and guidance with stock, each defence &gt; 0), centred at (160, 100).
/// </summary>
public static class TankInfoWindow
{
    public const int Width = 260, BaseHeight = 60, LineHeight = 12;

    /// <summary>[ORIGINAL] O9:0005 "XXX" instead of the energy of a dead tank.</summary>
    public const string Dead = "XXX";

    public static int Height(TurnPlayer p, bool teamPlay)
    {
        ArgumentNullException.ThrowIfNull(p);
        var h = BaseHeight;
        if (ShowsTeam(p, teamPlay))
            h += LineHeight;
        h += LineHeight * p.Tank.Stock.Count(s => s > 0);
        h += LineHeight * ((p.Tank.Herbs > 0 ? 1 : 0) + (p.Tank.Vicks > 0 ? 1 : 0));
        h += LineHeight * p.Tank.Defense.Count(d => d > 0);
        return h;
    }

    private static bool ShowsTeam(TurnPlayer p, bool teamPlay) => teamPlay && p.TeamName != p.Name;

    /// <summary>
    /// Frame: SetFillStyle(SolidFill, 0), Bar and Rectangle around the window 2 pixels out, Rectangle on it, colour 13.
    /// Name (colour 19 + n) and team centred at the top; then left/top in colour 5 with values in colour 14: "Men:",
    /// "Wins:", "Maximum Power:", "Score:", "Cash:", "Accesories:" and one line per item "name:" plus count (the chosen
    /// weapon all in colour 4, guidance names in colour 2, defences in colour 1; shield in tenths of a percent, inertia in
    /// percent, both at most "100%").
    /// </summary>
    public static void Draw(Surface s, GraphText text, TurnPlayer p, ItemCatalog catalog, bool teamPlay)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(catalog);
        var h = Height(p, teamPlay);
        var top = 100 - h / 2;
        var bottom = 100 + h / 2;
        var left = 160 - Width / 2;
        var right = 160 + Width / 2;
        MenuGraphics.Bar(s, left - 2, top - 2, right + 2, bottom + 2, 0);
        ClippedRectangle(s, left - 2, top - 2, right + 2, bottom + 2, 13);
        ClippedRectangle(s, left, top, right, bottom, 13);

        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        text.Color = p.Tank.Color;
        Out(s, text, 160, top + 4, GraphText.ToBytes(p.Name));
        if (ShowsTeam(p, teamPlay))
        {
            top += LineHeight;
            Out(s, text, 160, top + 4, GraphText.ToBytes(p.TeamName));
        }

        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        var energy = p.Tank.Energy;
        Pair(s, text, left + 4, top + 16, "Men:", 32, energy > 0 ? PlayfieldTurnView.Str(energy) : Dead);
        Label(s, text, 184, top + 16, "Wins:");
        Value(s, text, 224, top + 16, PlayfieldTurnView.Str(p.Wins));
        var max = energy > 0 ? PlayfieldTurnView.Str((short)(ushort)(10 * energy)) : Dead;
        Pair(s, text, left + 4, top + 28, "Maximum Power:", 112, max);
        Label(s, text, 184, top + 28, "Score:");
        Value(s, text, 232, top + 28, PlayfieldTurnView.Str(p.Tank.Score));
        Label(s, text, 184, top + 40, "Cash:");
        Value(s, text, 224, top + 40, PlayfieldTurnView.Str(unchecked(p.Tank.Score - p.Spent)));
        Label(s, text, left + 4, top + 40, "Accesories:");

        var y = 40;
        for (var w = 0; w < TurnPlayer.Weapons; w++)
        {
            if (p.Tank.Stock[w] <= 0)
                continue;
            y += LineHeight;
            var chosen = p.Tank.Weapon == w;
            var x = Item(s, text, left + 4, top + y, catalog.Get(ItemKind.Weapon, w).Name + ":", chosen ? (byte)4 : (byte)5);
            Item(s, text, x, top + y, PlayfieldTurnView.Str(p.Tank.Stock[w]), chosen ? (byte)4 : (byte)14);
        }

        for (var g = 0; g < 2; g++)
        {
            if (p.GuidanceStock(g) <= 0)
                continue;
            y += LineHeight;
            var x = Item(s, text, left + 4, top + y, catalog.Get(ItemKind.Guidance, g).Name + ":", 2);
            Item(s, text, x, top + y, PlayfieldTurnView.Str(p.GuidanceStock(g)), 14);
        }

        for (var d = 0; d < 3; d++)
        {
            if (p.Tank.Defense[d] <= 0)
                continue;
            y += LineHeight;
            var x = Item(s, text, left + 4, top + y, catalog.Get(ItemKind.Defense, d).Name + ":", 1);
            Item(s, text, x, top + y, DefenseText(d, p.Tank.Defense[d]), 14);
        }
    }

    /// <summary>
    /// [ORIGINAL] O9:076A–0852: the low word v of the defence; shield v div 10 + "%" up to 1000, inertia v + "%" up to
    /// 100, above "100%"; repulser v.
    /// </summary>
    public static string DefenseText(int kind, int value)
    {
        var v = (short)value;
        return kind switch
        {
            0 => v > 1000 ? "100%" : PlayfieldTurnView.Str(v / 10) + "%",
            1 => v > 100 ? "100%" : PlayfieldTurnView.Str(v) + "%",
            _ => PlayfieldTurnView.Str(v),
        };
    }

    private static void Label(Surface s, GraphText text, int x, int y, string label)
    {
        text.Color = 5;
        Out(s, text, x, y, GraphText.ToBytes(label));
    }

    private static void Value(Surface s, GraphText text, int x, int y, string value)
    {
        text.Color = 14;
        Out(s, text, x, y, GraphText.ToBytes(value));
    }

    /// <summary>
    /// OutTextXY with the clipping of the full-screen viewport for the DefaultFont: a line whose 8-pixel cell is not
    /// completely inside y 0..199 is not drawn. [ORIGINAL] measured with the probe (zug.py, turn 24: 20 lines, the window
    /// reaches from y −52 to 252; the line at y 194 is missing, the one at 182 is complete).
    /// </summary>
    private static void Out(Surface s, GraphText text, int x, int y, byte[] bytes)
    {
        if (y < 0 || y + 8 > Surface.OriginalHeight)
            return;
        text.OutTextXY(s, x, y, bytes);
    }

    /// <summary>BGI Rectangle (0E26:10D2) clipped to the screen pixel by pixel.</summary>
    private static void ClippedRectangle(Surface s, int x1, int y1, int x2, int y2, byte color)
    {
        for (var x = x1; x <= x2; x++)
        {
            Plot(s, x, y1, color);
            Plot(s, x, y2, color);
        }

        for (var y = y1; y <= y2; y++)
        {
            Plot(s, x1, y, color);
            Plot(s, x2, y, color);
        }
    }

    private static void Plot(Surface s, int x, int y, byte color)
    {
        if (x >= 0 && y >= 0 && x < Surface.OriginalWidth && y < Surface.OriginalHeight)
            s[x, y] = color;
    }

    private static void Pair(Surface s, GraphText text, int x, int y, string label, int offset, string value)
    {
        Label(s, text, x, y, label);
        Value(s, text, x + offset, y, value);
    }

    /// <summary>OutText at the current position (MoveTo, then CP moves on by the text width): returns the new x.</summary>
    private static int Item(Surface s, GraphText text, int x, int y, string item, byte color)
    {
        text.Color = color;
        var bytes = GraphText.ToBytes(item);
        Out(s, text, x, y, bytes);
        return x + text.TextWidth(bytes);
    }
}
