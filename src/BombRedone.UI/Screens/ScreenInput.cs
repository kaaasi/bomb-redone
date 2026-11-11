namespace BombRedone.UI.Screens;

/// <summary>Mouse buttons as the mouse driver reports them (INT 33h function 3, register BX).</summary>
[Flags]
public enum MouseButtons
{
    None = 0,
    Left = 1,
    Right = 2,
}

/// <summary>
/// Input for one <see cref="IScreen.Update"/> call, independent of any window system.
/// </summary>
/// <param name="KeyPressed">
/// At least one key that the BIOS would put into the keyboard buffer (not a lone Shift, Ctrl or Alt) was pressed since the
/// previous update.
/// </param>
/// <param name="BothShiftsDown">Left and right Shift are held at the moment of the update (BIOS flags 0040:0017 bits 0 and 1).</param>
/// <param name="Mouse">Mouse buttons held at the moment of the update.</param>
public readonly record struct ScreenInput(bool KeyPressed = false, bool BothShiftsDown = false, MouseButtons Mouse = MouseButtons.None)
{
    public static ScreenInput None => default;

    /// <summary>True if every button in <paramref name="buttons"/> is held; the test of BOMB.EXE 0x0F827 (UE11:0077).</summary>
    public bool MouseHolds(MouseButtons buttons) => (Mouse & buttons) == buttons;
}
