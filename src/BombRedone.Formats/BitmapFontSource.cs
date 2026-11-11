using System.Security.Cryptography;

namespace BombRedone.Formats;

/// <summary>Where the DefaultFont in use comes from.</summary>
public enum BitmapFontOrigin
{
    /// <summary>VGA font of DOSBox 0.74-3 from the reference repository (what the original draws under DOSBox).</summary>
    Reference,

    /// <summary>Built-in public-domain font (<see cref="BitmapFontSubstitute"/>).</summary>
    Substitute,
}

/// <summary>DefaultFont chosen by <see cref="BitmapFontSource"/>.</summary>
/// <param name="Font">The font.</param>
/// <param name="Origin">Reference or substitute.</param>
/// <param name="Path">File the reference font was read from; null for the substitute.</param>
public sealed record BitmapFontSelection(BitmapFont Font, BitmapFontOrigin Origin, string? Path);

/// <summary>
/// Chooses the 8 x 8 DefaultFont (decision R1-schrift-1 = D): the reference font of DOSBox 0.74-3 if it is present,
/// otherwise the built-in substitute.
/// </summary>
/// <remarks>
/// The reference font is GPL licensed and lives, like the original files, only in the private repository
/// kaaasi/bomb-referenz (folder <c>schriften/</c>). It is found the same way as <see cref="GameData.Locate"/> finds the
/// original files: a folder <c>referenz/schriften</c> (protected folder in the repository root) in the current
/// directory, the application directory or one of their parents, or <c>schriften</c> in the clone of the reference
/// repository (<c>$REFERENZ</c>, default <c>~/referenz</c>, as used by tools/claude/fetch-reference.sh).
/// A file with the right name but a different SHA-256 is an error, never silently replaced by the substitute.
/// </remarks>
public static class BitmapFontSource
{
    /// <summary>File name of the reference font in the reference repository.</summary>
    public const string ReferenceFileName = "dosbox-0.74-3-vga-8x8.bin";

    /// <summary>SHA-256 of the reference font (array int10_font_08 of DOSBox 0.74-3, 2048 bytes).</summary>
    public const string ReferenceSha256 = "75c79a7e7fa423dda67ec6d6d76cec86b63f85677726368750c75b0920ddf319";

    /// <summary>Folder of the reference font below the root of the reference repository.</summary>
    public const string ReferenceFolder = "schriften";

    /// <summary>Protected folder in the root of the code repository that may hold a copy of the reference data.</summary>
    public const string LocalReferenceFolder = "referenz";

    /// <summary>
    /// Folders searched for <see cref="ReferenceFileName"/>, in order: <c>referenz/schriften</c> in the current
    /// directory, the application directory and their parents, then <c>$REFERENZ/schriften</c> or
    /// <c>~/referenz/schriften</c>.
    /// </summary>
    public static IReadOnlyList<string> DefaultSearchDirectories()
    {
        var result = new List<string>();
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                result.Add(Path.Combine(dir.FullName, LocalReferenceFolder, ReferenceFolder));
        }

        var clone = Environment.GetEnvironmentVariable("REFERENZ");
        if (string.IsNullOrEmpty(clone))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            clone = string.IsNullOrEmpty(home) ? null : Path.Combine(home, LocalReferenceFolder);
        }
        if (clone is not null)
            result.Add(Path.Combine(clone, ReferenceFolder));
        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Path of the reference font in the first of <paramref name="searchDirectories"/> that has it, or null.</summary>
    public static string? FindReference(IEnumerable<string> searchDirectories) =>
        searchDirectories.Select(d => Path.Combine(d, ReferenceFileName)).FirstOrDefault(File.Exists);

    /// <summary>Reads and checks the reference font.</summary>
    /// <exception cref="FormatException">The file is not the expected reference font (size or SHA-256).</exception>
    public static BitmapFont ReadReference(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (hash != ReferenceSha256)
            throw new FormatException(
                $"{path} is not the reference font of DOSBox 0.74-3: SHA-256 {hash}, expected {ReferenceSha256}.");
        return new BitmapFont(bytes);
    }

    /// <summary>The reference font if one of <paramref name="searchDirectories"/> has it, otherwise the substitute.</summary>
    public static BitmapFontSelection Load(IEnumerable<string> searchDirectories) =>
        FindReference(searchDirectories) is { } path
            ? new BitmapFontSelection(ReadReference(path), BitmapFontOrigin.Reference, path)
            : new BitmapFontSelection(BitmapFontSubstitute.Create(), BitmapFontOrigin.Substitute, null);

    /// <summary><see cref="Load(IEnumerable{string})"/> with <see cref="DefaultSearchDirectories"/>.</summary>
    public static BitmapFontSelection Load() => Load(DefaultSearchDirectories());
}
