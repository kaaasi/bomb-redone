namespace BombRedone.Formats;

/// <summary>One player of a saved game: record, control word and, for computers, the intelligence.</summary>
public sealed class SavedGamePlayer
{
    /// <summary>Size of the control word DS:E5A6 + 2i. [ORIGINAL] T2c.</summary>
    public const int ControlSize = 2;

    /// <summary>Control word of a computer player; only then the intelligence follows. [ORIGINAL] T2c (BOMB.OVR 0x0C971).</summary>
    public const short ComputerControl = 3;

    private readonly byte[] _control;

    internal SavedGamePlayer(int index, SavedGamePlayerRecord record, byte[] control, SavedGameIntelligence? intelligence)
    {
        Index = index;
        Record = record;
        _control = control;
        Intelligence = intelligence;
    }

    /// <summary>Position in the file, 1..player count (index i of DS:402E + 74h·i and DS:E5A6 + 2i).</summary>
    public int Index { get; }

    /// <summary>Player record DS:402E + 74h·i.</summary>
    public SavedGamePlayerRecord Record { get; }

    /// <summary>
    /// Control DS:E5A6 + 2i: 1 keyboard, 2 mouse, 3 computer (as <c>Core/Setup/PlayerControl</c>; spielerauswahl.md).
    /// Other values are kept as read.
    /// </summary>
    public short Control => SavedGame.Int16(_control, 0);

    /// <summary>The two bytes of the control word as stored.</summary>
    public ReadOnlyMemory<byte> RawControl => _control;

    /// <summary>True if <see cref="Control"/> is 3; then <see cref="Intelligence"/> is set.</summary>
    public bool IsComputer => Control == ComputerControl;

    /// <summary>The stored fields of the computer intelligence, or null for a human player.</summary>
    public SavedGameIntelligence? Intelligence { get; }
}

/// <summary>
/// Player record of 0x74 bytes (DS:402E + 74h·i). Typed properties only for fields with known meaning
/// (<c>docs/formate/spielstand.md</c>, "Spielersatz"); all others through <see cref="Raw"/>.
/// </summary>
/// <remarks>
/// [ORIGINAL] Field widths as accessed by the code: score and spent money LongInt (status screen O3:5AEF, shop O3:0F65),
/// stock Word[2], Word[18], LongInt[3] (O3:2195, 0x21D6, 0x221B), see <c>Core/Economy/PlayerAccount</c>.
/// </remarks>
public sealed class SavedGamePlayerRecord
{
    /// <summary>Record size. [ORIGINAL] index arithmetic <c>mul 74h</c> (T7f) and BlockWrite size (T2b).</summary>
    public const int Size = 0x74;

    /// <summary>Capacity of the name (String[13]). [ORIGINAL] spielerauswahl.md §1 (O2:0078).</summary>
    public const int NameCapacity = 13;

    /// <summary>Number of guidance entries at +2Ah (ItemCatalog.GuidanceTable).</summary>
    public const int GuidanceCount = 2;

    /// <summary>Number of weapon entries at +2Eh (ItemCatalog.WeaponTable).</summary>
    public const int WeaponCount = 18;

    /// <summary>Number of defense entries at +52h (ItemCatalog.DefenseTable).</summary>
    public const int DefenseCount = 3;

    private readonly byte[] _raw;

    internal SavedGamePlayerRecord(byte[] raw) => _raw = raw;

    /// <summary>The 116 bytes as stored.</summary>
    public ReadOnlyMemory<byte> Raw => _raw;

    /// <summary>+00h, byte: player number. spielerauswahl.md §1 (O2:0216).</summary>
    public byte Number => _raw[0x00];

    /// <summary>+01h, String[13]: name; for computers also the name of the intelligence. spielerauswahl.md §1, T5d.</summary>
    public string Name => SavedGame.PascalString(_raw, 0x01, NameCapacity);

    /// <summary>+13h, LongInt: score. wertung-einkauf.md §2.</summary>
    public int Score => SavedGame.Int32(_raw, 0x13);

    /// <summary>+17h, LongInt: money spent in the shop. wertung-einkauf.md §2.</summary>
    public int Spent => SavedGame.Int32(_raw, 0x17);

    /// <summary>Money = score − spent, 32 bit wrapping as in the shop (O3:0F65).</summary>
    public int Money => unchecked(Score - Spent);

    /// <summary>+1Dh, Word: turret angle used to draw the tank. spielerauswahl.md §7.</summary>
    /// <remarks>[APPROX] Read signed; the sign handling of the field is not traced.</remarks>
    public short TurretAngle => SavedGame.Int16(_raw, 0x1D);

    /// <summary>+1Fh, Word: direction factor used to draw the tank. spielerauswahl.md §7.</summary>
    /// <remarks>[APPROX] Read signed; the sign handling of the field is not traced.</remarks>
    public short TurretDirection => SavedGame.Int16(_raw, 0x1F);

    /// <summary>+23h, byte: ≠ 0 while the player takes turns in the game. [ORIGINAL] T7c.</summary>
    public byte TakesTurns => _raw[0x23];

    /// <summary>+28h, Word: number of another player (copied to DS:47E6). [ORIGINAL] T7f.</summary>
    public short OtherPlayer => SavedGame.Int16(_raw, 0x28);

    /// <summary>+2Ah + 2k: guidance stock k (signed 16 bit as in <c>PlayerAccount</c>).</summary>
    public short Guidance(int index) => SavedGame.Int16(_raw, 0x2A + 2 * Check(index, GuidanceCount));

    /// <summary>+2Eh + 2k: weapon stock k (signed 16 bit as in <c>PlayerAccount</c>).</summary>
    public short Weapon(int index) => SavedGame.Int16(_raw, 0x2E + 2 * Check(index, WeaponCount));

    /// <summary>+52h + 4k: defense stock k (LongInt).</summary>
    public int Defense(int index) => SavedGame.Int32(_raw, 0x52 + 4 * Check(index, DefenseCount));

    /// <summary>+60h + 2k, k = 0..2: divisors of the player colour. spielerauswahl.md §1–2.</summary>
    /// <remarks>[APPROX] Read signed; the sign handling of the field is not traced.</remarks>
    public short ColorDivisor(int index) => SavedGame.Int16(_raw, 0x60 + 2 * Check(index, 3));

    /// <summary>+66h, Word: team. spielerauswahl.md §6.</summary>
    public short Team => SavedGame.Int16(_raw, 0x66);

    /// <summary>+6Ah, Word: games won. <c>Core/Economy/PlayerAccount.Wins</c> (status screen O3:5B9D).</summary>
    public short GamesWon => SavedGame.Int16(_raw, 0x6A);

    private static int Check(int index, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, count);
        return index;
    }
}

/// <summary>
/// The 7 stored bytes of a computer intelligence (object of unit U129, pointer DS:3BE8 + 4i).
/// </summary>
/// <remarks>
/// [ORIGINAL] U129:00C6 (BOMB.EXE 0x029F6) writes Self+00h (2 bytes), +0Bh (1), +0Eh (2), +10h (2); the loader reads the
/// kind into a local variable, calls the constructor of that kind and then U129:0135 (BOMB.EXE 0x02A65), which
/// overwrites the other three fields (T3b, T3e, T5a). Loading therefore consumes <c>Random</c> (Init draws
/// <see cref="Choice"/> anew before it is overwritten, T5c); this reader does not model it.
/// </remarks>
public sealed class SavedGameIntelligence
{
    /// <summary>Stored size. [ORIGINAL] T5a.</summary>
    public const int Size = 7;

    /// <summary>
    /// Names of the kinds 1..7 (index 0 = kind 1). [ORIGINAL] T5d: Pascal strings DS:007C + 256k, compared by O2:0078
    /// (BOMB.OVR 0x00660).
    /// </summary>
    public static IReadOnlyList<string> KindNames { get; } =
        ["Mr. Stupid", "Lobber", "Rifleman", "Lob & Shoot", "Windless Wit", "Twanger", "Wind Master"];

    private readonly byte[] _raw;

    internal SavedGameIntelligence(byte[] raw) => _raw = raw;

    /// <summary>The 7 bytes as stored.</summary>
    public ReadOnlyMemory<byte> Raw => _raw;

    /// <summary>Self+00h: kind 1..7 (<see cref="KindNames"/>). Other values: the original leaves the pointer nil (T3e).</summary>
    public short Kind => SavedGame.Int16(_raw, 0);

    /// <summary>Name of <see cref="Kind"/>, or null outside 1..7.</summary>
    public string? KindName => Kind is >= 1 and <= 7 ? KindNames[Kind - 1] : null;

    /// <summary>Self+0Bh: meaning not traced (Init sets 0). Raw value.</summary>
    public byte Unknown0B => _raw[2];

    /// <summary>
    /// Self+0Eh: drawn on creation as Random(4)+1 until it is in the set of the kind (kinds 3 and 6: 1..4, others 1, 2, 4);
    /// purpose not traced. [ORIGINAL] T5c.
    /// </summary>
    public short Choice => SavedGame.Int16(_raw, 3);

    /// <summary>Self+10h: player number, index of the record the intelligence reads. [ORIGINAL] T5b.</summary>
    public short PlayerNumber => SavedGame.Int16(_raw, 5);
}
