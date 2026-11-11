namespace BombRedone.UI.Text;

/// <summary>Text direction of SetTextStyle (Graph unit: HorizDir = 0, VertDir = 1).</summary>
public enum TextDirection
{
    Horizontal = 0,

    /// <summary>Rotated by 90 degrees counterclockwise: the text runs upwards.</summary>
    Vertical = 1,
}

/// <summary>Horizontal part of SetTextJustify (LeftText = 0, CenterText = 1, RightText = 2).</summary>
public enum HorizontalJustify
{
    Left = 0,
    Center = 1,
    Right = 2,
}

/// <summary>Vertical part of SetTextJustify (BottomText = 0, CenterText = 1, TopText = 2).</summary>
public enum VerticalJustify
{
    Bottom = 0,
    Center = 1,
    Top = 2,
}
