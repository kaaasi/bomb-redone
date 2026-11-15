using BombRedone.Core.Turn;
using BombRedone.UI.Text;

namespace BombRedone.UI.Screens;

/// <summary>The bar of the target selection. [ORIGINAL] O9:09FC–0B2E and 0C0F–0CD5 (BOMB.OVR 0x1EC19, 0x1EE2C).</summary>
public static class TargetSelectBar
{
    /// <summary>
    /// SetFillStyle(SolidFill, 0), Bar (0, 0)–(320, 27), Rectangle (0, 0)–(319, 28) in colour 13; centred at (160, 4) in
    /// colour 19 + n "&lt;name&gt;: Guidance Type &lt;+26h&gt;"; with the mouse also "Please click on target" at (160, 15).
    /// </summary>
    public static void Draw(Surface s, GraphText text, TurnPlayer shooter, bool mouse)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(shooter);
        MenuGraphics.Bar(s, 0, 0, 320, 27, 0);
        MenuGraphics.Rectangle(s, 0, 0, 319, 28, 13);
        text.SetTextJustify(HorizontalJustify.Center, VerticalJustify.Top);
        text.Color = shooter.Tank.Color;
        text.OutTextXY(s, 160, 4, shooter.Name + ": Guidance Type " + PlayfieldTurnView.Str(shooter.Guidance));
        if (mouse)
            text.OutTextXY(s, 160, 15, "Please click on target");
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
    }

    /// <summary>
    /// [ORIGINAL] O9:0ABD–0B2E: "Current Target:" at (70, 15) in colour 19 + n, Bar (198, 15)–(317, 23) in black, the name
    /// of the target at (198, 15) in its colour.
    /// </summary>
    public static void DrawTarget(Surface s, GraphText text, TurnPlayer shooter, TurnPlayer target)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(target);
        text.SetTextJustify(HorizontalJustify.Left, VerticalJustify.Top);
        text.Color = shooter.Tank.Color;
        text.OutTextXY(s, 70, 15, "Current Target:");
        MenuGraphics.Bar(s, 198, 15, 317, 23, 0);
        text.Color = target.Tank.Color;
        text.OutTextXY(s, 198, 15, target.Name);
    }
}
