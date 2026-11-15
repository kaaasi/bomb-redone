namespace BombRedone.Core.Turn;

/// <summary>
/// Scancodes of the held-key table DS:452A + code (keys with the E0 prefix + 100h), as the keyboard handler U0C48:0008
/// (BOMB.EXE 0x0DB28) fills it. Only the keys the turn reads (docs/analyse/zug.md section 4).
/// </summary>
public static class Scancode
{
    public const int Esc = 0x01, Digit1 = 0x02, Digit0 = 0x0B, Tab = 0x0F, W = 0x11, I = 0x17, Enter = 0x1C, D = 0x20,
        G = 0x22, H = 0x23, LeftShift = 0x2A, C = 0x2E, V = 0x2F, RightShift = 0x36, Alt = 0x38, Space = 0x39,
        PadUp = 0x48, PadPageUp = 0x49, PadLeft = 0x4B, PadRight = 0x4D, PadDown = 0x50, PadPageDown = 0x51,
        PadInsert = 0x52, PadDelete = 0x53;

    /// <summary>Offset of the keys with the E0 prefix (grey keys, right Alt, keypad Enter).</summary>
    public const int Extended = 0x100;

    public const int PadEnter = Extended + Enter, RightAlt = Extended + Alt, Up = Extended + PadUp,
        PageUp = Extended + PadPageUp, Left = Extended + PadLeft, Right = Extended + PadRight, Down = Extended + PadDown,
        PageDown = Extended + PadPageDown, Insert = Extended + PadInsert, Delete = Extended + PadDelete;

    /// <summary>Size of the table (DS:452A..472A).</summary>
    public const int Count = 0x200;
}

/// <summary>
/// The input the turn reads, as an abstract state the host feeds (docs/analyse/zug.md section 2): held keys (DS:452A),
/// the BIOS keyboard buffer (Crt.KeyPressed/ReadKey in O9) and the mouse (INT 33h function 3 through U0E11).
/// At its wait-for-release loops the turn yields <see cref="TurnWaitKind.ReleaseKeys"/> / <see cref="TurnWaitKind.ReleaseButtons"/>
/// and clears the keys itself when resumed.
/// </summary>
public sealed class TurnInput
{
    private readonly bool[] _held = new bool[Scancode.Count];
    private readonly Queue<(byte Ascii, byte Scan)> _bios = new();
    private int _pendingScan = -1;

    /// <summary>Key held (DS:452A + scancode).</summary>
    public bool this[int scancode]
    {
        get => _held[scancode];
        set => _held[scancode] = value;
    }

    /// <summary>Releases every key.</summary>
    public void ReleaseAll() => Array.Clear(_held);

    /// <summary>Mouse x in screen pixels 0..319 (U0DBD:022A halves the driver's 0..639).</summary>
    public int MouseX { get; set; }

    /// <summary>Mouse y 0..199.</summary>
    public int MouseY { get; set; }

    /// <summary>Buttons: bit 0 left, bit 1 right.</summary>
    public int MouseButtons { get; set; }

    /// <summary>Mouse driver present (DS:E94E). Without it every button query of U0E11:0077 is false.</summary>
    public bool MousePresent { get; set; }

    /// <summary>The wait-for-release loops passed with something held, by the names of docs/analyse/zug.md (in order).</summary>
    public List<string> Releases { get; } = [];

    /// <summary>Puts a key into the BIOS keyboard buffer (16 words, 15 usable like the ring at 0040:001E).</summary>
    public void TypeKey(byte ascii, byte scan)
    {
        if (_bios.Count < 15)
            _bios.Enqueue((ascii, scan));
    }

    /// <summary>Keys waiting in the BIOS buffer.</summary>
    public int TypedKeys => _bios.Count;

    /// <summary>[ORIGINAL] U0E11:0077 (BOMB.EXE 0x0F827): (buttons and n) = n, false without a mouse.</summary>
    internal bool Button(int n) => MousePresent && (MouseButtons & n) == n;

    /// <summary>
    /// [ORIGINAL] U0C48:0227 / U0C48:0269 (BOMB.EXE 0x0DD47 / 0x0DD89) and the direct writes 0040:001A := 0040:001C:
    /// the BIOS buffer is emptied (Crt keeps a pending scancode).
    /// </summary>
    internal void FlushBios() => _bios.Clear();

    /// <summary>Crt.KeyPressed: a pending extended scancode or a key in the BIOS buffer.</summary>
    internal bool KeyPressed => _pendingScan >= 0 || _bios.Count > 0;

    /// <summary>Crt.ReadKey: an extended key gives 0 first and its scancode at the next call.</summary>
    internal byte ReadKey()
    {
        if (_pendingScan >= 0)
        {
            var s = (byte)_pendingScan;
            _pendingScan = -1;
            return s;
        }

        var (ascii, scan) = _bios.Dequeue();
        if (ascii == 0)
            _pendingScan = scan;
        return ascii;
    }

    /// <summary>Any of <paramref name="scancodes"/> held.</summary>
    internal bool AnyHeld(IReadOnlyList<int> scancodes) => scancodes.Any(s => _held[s]);

    /// <summary>The end of a wait-for-release loop: <paramref name="scancodes"/> are up.</summary>
    internal void Release(IReadOnlyList<int> scancodes)
    {
        foreach (var s in scancodes)
            _held[s] = false;
    }
}
