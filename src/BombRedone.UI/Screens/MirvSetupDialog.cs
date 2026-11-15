using BombRedone.Core.Graphics;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>The MIRV dialog. [ORIGINAL] O4:140A–1575 and 17A2–189F (BOMB.OVR 0x1210F, 0x124A7).</summary>
public static class MirvSetupDialog
{
    /// <summary>Line i is drawn in colour 130 + i; Core sets those DAC entries (TurnSession.MirvSetup).</summary>
    public const int ColorBase = 130;

    public static readonly IReadOnlyList<string> Patterns = ["Random", "Horizontal", "Vertical", "+ 4 Way", "X 4 Way"];

    /// <summary>[ORIGINAL] O4:13BB–13CF, the spelling "Simultanious" as in the original.</summary>
    public static readonly IReadOnlyList<string> Detonations = ["Impact", "Simultanious", "None"];

    /// <summary>
    /// Bar (100, 65)–(220, 135) in black, frames UCC8:0031 (100, 65)–(220, 135) and (102, 67)–(218, 133) in colour 13,
    /// centred title at (160, 75) in colour 15, line UCC8:09E4 (102, 79)–(218, 79) in colour 13, then the lines at y = 85 +
    /// 10 i in colour 130 + i.
    /// </summary>
    public static void Draw(Surface s, GraphText text, int page)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(text);
        MenuGraphics.Bar(s, 100, 65, 220, 135, 0);
        VgaDraw.Rectangle(s.Pixels, 100, 65, 220, 135, 13);
        VgaDraw.Rectangle(s.Pixels, 102, 67, 218, 133, 13);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Center);
        text.Color = 15;
        text.OutTextXY(s, 160, 75, page == 1 ? "MIRV Setup" : "Detonations");
        VgaDraw.Line(s.Pixels, 102, 79, 218, 79, 13);
        var lines = page == 1 ? Patterns : Detonations;
        for (var i = 0; i < lines.Count; i++)
        {
            text.Color = (byte)(ColorBase + i);
            text.OutTextXY(s, 160, 85 + 10 * i, lines[i]);
        }

        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
    }
}
