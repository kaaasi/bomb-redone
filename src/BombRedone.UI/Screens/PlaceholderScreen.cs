namespace BombRedone.UI.Screens;

/// <summary>
/// Empty black screen that stands in for the main menu until the track "menue" provides it. It ignores all input;
/// the window layer decides how to quit.
/// </summary>
public sealed class PlaceholderScreen : IScreen
{
    public IScreen? Update(TimeSpan elapsed, ScreenInput input) => this;

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        surface.Clear();
        Array.Clear(surface.Palette);
        surface.Palette[0] = 0xFF000000;
    }
}
