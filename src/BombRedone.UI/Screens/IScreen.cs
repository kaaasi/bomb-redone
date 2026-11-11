namespace BombRedone.UI.Screens;

/// <summary>
/// One screen of the game (title, menu, ...). Time and input come from outside (AGENTS.md section 4), so every
/// sequence can be replayed with a given clock in tests.
/// </summary>
public interface IScreen
{
    /// <summary>
    /// Advances the screen by <paramref name="elapsed"/> with the input collected since the previous call.
    /// </summary>
    /// <returns>The screen to show from now on: <c>this</c> to stay, another screen to switch, <c>null</c> to quit.</returns>
    IScreen? Update(TimeSpan elapsed, ScreenInput input);

    /// <summary>Draws the current state into <paramref name="surface"/> (pixels and palette); never changes the state.</summary>
    void Draw(Surface surface);
}
