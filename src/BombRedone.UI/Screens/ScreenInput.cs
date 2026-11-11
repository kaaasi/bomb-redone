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

    /// <summary>
    /// Keys typed since the previous update in the order of the BIOS keyboard buffer, as BIOS key words (high byte scan
    /// code, low byte character; see <see cref="BiosKey"/>). Empty if the window layer reports only <see cref="KeyPressed"/>.
    /// </summary>
    public IReadOnlyList<ushort> Keys
    {
        get => _keys ?? [];
        init => _keys = value;
    }

    private readonly IReadOnlyList<ushort>? _keys;

    /// <summary>
    /// Mouse position in the coordinates of the mouse driver (INT 33h function 3: CX horizontal 0..639 in mode 13h,
    /// DX vertical 0..199).
    /// </summary>
    public int MouseX { get; init; }

    /// <inheritdoc cref="MouseX"/>
    public int MouseY { get; init; }

    /// <summary>True if every button in <paramref name="buttons"/> is held; the test of BOMB.EXE 0x0F827 (UE11:0077).</summary>
    public bool MouseHolds(MouseButtons buttons) => (Mouse & buttons) == buttons;
}

/// <summary>BIOS key words (INT 16h): high byte scan code, low byte character, 0 for extended keys.</summary>
public static class BiosKey
{
    public const ushort Escape = 0x011B;
    public const ushort Enter = 0x1C0D;
    public const ushort Space = 0x3920;
    public const ushort Up = 0x4800;
    public const ushort Down = 0x5000;
    public const ushort Left = 0x4B00;
    public const ushort Right = 0x4D00;

    /// <summary>
    /// Key word of a character typed in the window (text input with the keyboard layout and Shift applied), or 0 if it
    /// is not passed this way: only printable ASCII 0x21..0x7E ("?", punctuation, letters, digits). Space, Enter, Esc,
    /// Backspace and the extended keys come from the key itself with their scan code; characters above 0x7E are left
    /// out because the window delivers Unicode and the original expects code page 437 [APPROX].
    /// </summary>
    public static ushort FromTypedCharacter(char c) => c is >= '\x21' and <= '\x7E' ? c : (ushort)0;

    /// <summary>Key word of a character key (scan code left 0; Crt.ReadKey only returns the character).</summary>
    public static ushort Char(char c) => c is > '\0' and <= '\xFF'
        ? c
        : throw new ArgumentOutOfRangeException(nameof(c), "Only characters 1-255 are BIOS characters.");
}
