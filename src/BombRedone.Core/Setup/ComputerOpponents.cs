namespace BombRedone.Core.Setup;

/// <summary>
/// Names of the computer opponents. The original identifies a computer player only by its name: O2:0078
/// (BOMB.OVR 0x00660) compares the player name with the names 1..7 and installs the matching intelligence.
/// </summary>
public static class ComputerOpponents
{
    /// <summary>Index of the entry "Random": resolved to one of the seven intelligences when the game starts.</summary>
    public const int Random = 0;

    /// <summary>Number of real intelligences (names 1..7).</summary>
    public const int IntelligenceCount = 7;

    /// <summary>
    /// [ORIGINAL] Pascal strings at DS:007C + 256 · k (BOMB.EXE 0x1646C + 256 · k, k = 0..7); the same order as the
    /// selection list O2:481B and BOMB.DOC "PLAYER SELECTION" (without "Random").
    /// </summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        "Random", "Mr. Stupid", "Lobber", "Rifleman", "Lob & Shoot", "Windless Wit", "Twanger", "Wind Master",
    ];

    /// <summary>File offset of name <paramref name="index"/> in BOMB.EXE (length byte of the Pascal string).</summary>
    public static int FileOffset(int index) => 0x1646C + 256 * index;

    /// <summary>Index 0..7 of the name, or null; the comparison is exact like the string compare of O2:0078.</summary>
    public static int? Find(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        for (var i = 0; i < Names.Count; i++)
        {
            if (string.Equals(Names[i], name, StringComparison.Ordinal))
                return i;
        }

        return null;
    }
}
