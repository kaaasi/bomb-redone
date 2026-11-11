namespace BombRedone.UI.Screens;

/// <summary>
/// Holds the current screen and performs the switches the screens ask for. The window layer only feeds time and input
/// and shows the <see cref="Surface"/>.
/// </summary>
public sealed class ScreenHost
{
    public ScreenHost(IScreen first)
    {
        ArgumentNullException.ThrowIfNull(first);
        Current = first;
    }

    /// <summary>The screen shown now; null once a screen asked to quit.</summary>
    public IScreen? Current { get; private set; }

    public bool IsRunning => Current is not null;

    /// <summary>
    /// Updates the current screen. A screen that is switched to gets its first update with the next call, so time
    /// left over in the call that switched is not passed on.
    /// </summary>
    public void Update(TimeSpan elapsed, ScreenInput input)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Time cannot run backwards.");
        Current = Current?.Update(elapsed, input);
    }

    /// <summary>Draws the current screen; after quitting the surface is left unchanged.</summary>
    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        Current?.Draw(surface);
    }
}
