namespace BombRedone.Formats;

/// <summary>The stroked fonts linked into <c>BOMB.EXE</c> and their file offsets.</summary>
/// <remarks>
/// [ORIGINAL] docs/analyse/re-grundlagen.md section 4, findings F1-F4: LITT at BOMB.EXE 0x08B90, EURO at 0x09F9B,
/// TINY at 0x0C0AE (start of the "PK\x08\x08 BGI" signature). The program registers further fonts (LCOM, SCRI, SIMP,
/// TSCR, F6) that are not linked in and never used.
/// </remarks>
public static class BgiFontLibrary
{
    /// <summary>Program file that contains the fonts.</summary>
    public const string ProgramFile = "BOMB.EXE";

    /// <summary>Name of the SmallFont of the Graph unit (font number 2).</summary>
    public const string Small = "LITT";

    public const string Euro = "EURO";

    public const string Tiny = "TINY";

    /// <summary>Names and file offsets of the embedded fonts in BOMB.EXE.</summary>
    public static readonly IReadOnlyDictionary<string, int> ProgramOffsets = new Dictionary<string, int>
    {
        [Small] = 0x08B90,
        [Euro] = 0x09F9B,
        [Tiny] = 0x0C0AE,
    };

    /// <summary>Reads one embedded font from the program file contents at its known offset.</summary>
    /// <exception cref="FormatException">The font is not at its offset (different program version) or damaged.</exception>
    public static BgiFont Parse(ReadOnlySpan<byte> program, string name)
    {
        if (!ProgramOffsets.TryGetValue(name, out var offset))
            throw new ArgumentException($"No embedded font named {name}.", nameof(name));
        if (offset >= program.Length || BgiFont.FindEmbedded(program[offset..], name) != 0)
            throw new FormatException($"{ProgramFile}: font {name} not found at 0x{offset:X5}.");
        return BgiFont.Parse(program[offset..]);
    }

    /// <summary>Reads all embedded fonts from the original files.</summary>
    public static IReadOnlyDictionary<string, BgiFont> Load(GameData gameData)
    {
        ArgumentNullException.ThrowIfNull(gameData);
        var program = File.ReadAllBytes(gameData.Require(ProgramFile));
        return ProgramOffsets.Keys.ToDictionary(name => name, name => Parse(program, name));
    }
}
