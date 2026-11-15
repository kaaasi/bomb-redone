namespace BombRedone.Formats;

/// <summary>
/// Team table DS:4838 at the end of a saved game: 11 entries of 53 bytes (teams 0..10). [ORIGINAL] T7e (BOMB.OVR 0x0C9FC).
/// </summary>
/// <remarks>
/// The game writes the table always, also without team play. Without team play it is never filled and holds whatever
/// was in memory (stale text of an earlier program in the reference recordings, review #123 P3-2), so the typed
/// entries only mean something if <see cref="SavedGameGlobals.TeamPlay"/> is set; the raw bytes are kept either way.
/// </remarks>
public sealed class SavedGameTeamTable
{
    /// <summary>Number of entries (teams 0..10). [ORIGINAL] T7e.</summary>
    public const int Count = 11;

    /// <summary>Size of the table. [ORIGINAL] T7e: one BlockWrite of 583 bytes.</summary>
    public const int Size = Count * SavedGameTeam.Size;

    private readonly byte[] _raw;

    internal SavedGameTeamTable(byte[] raw)
    {
        _raw = raw;
        var teams = new SavedGameTeam[Count];
        for (var t = 0; t < Count; t++)
            teams[t] = new SavedGameTeam(t, raw.AsSpan(t * SavedGameTeam.Size, SavedGameTeam.Size).ToArray());
        Teams = teams;
    }

    /// <summary>The 583 bytes as stored.</summary>
    public ReadOnlyMemory<byte> Raw => _raw;

    /// <summary>Entries 0..10 (index = team number).</summary>
    public IReadOnlyList<SavedGameTeam> Teams { get; }
}

/// <summary>
/// One entry DS:4838 + 53·t: name String[13] at +0, "all computers" at +0Eh, member count at +0Fh; the rest is raw.
/// Fields as in <c>Core/Setup/TeamRecord</c> (spielerauswahl.md §8, O2:35CF, BOMB.OVR 0x03BB7).
/// </summary>
public sealed class SavedGameTeam
{
    /// <summary>Entry size. [ORIGINAL] T7e.</summary>
    public const int Size = 53;

    private readonly byte[] _raw;

    internal SavedGameTeam(int number, byte[] raw)
    {
        Number = number;
        _raw = raw;
    }

    /// <summary>Team number 0..10.</summary>
    public int Number { get; }

    /// <summary>The 53 bytes as stored.</summary>
    public ReadOnlyMemory<byte> Raw => _raw;

    /// <summary>+00h, String[13]: team name.</summary>
    public string Name => SavedGame.PascalString(_raw, 0x00, 13);

    /// <summary>+0Eh: no member is a human player.</summary>
    public bool AllComputers => SavedGame.Flag(_raw, 0x0E);

    /// <summary>+0Fh, Word: number of members.</summary>
    /// <remarks>[APPROX] Read signed; the sign handling of the field is not traced.</remarks>
    public short MemberCount => SavedGame.Int16(_raw, 0x0F);
}
