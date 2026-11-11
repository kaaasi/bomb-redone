namespace BombRedone.Formats;

/// <summary>
/// Access to the original files of Tank Wars 3.2 (folder <c>gamedata</c>, never part of this repository).
/// </summary>
public sealed class GameData
{
    /// <summary>Name of the folder that holds the original files.</summary>
    public const string FolderName = "gamedata";

    /// <summary>File whose presence identifies the folder.</summary>
    public const string MarkerFile = "BOMB.EXE";

    /// <summary>
    /// Files of the original package. [ORIGINAL] BOMB.TXT, section "FILES" (13 entries).
    /// </summary>
    public static readonly IReadOnlyList<string> PackageFiles =
    [
        "BOMB.EXE", "BOMB.OVR", "BOMB.TXT", "BOMB.DOC", "REVISION.HST", "BOMB1.RAW", "BOMB2.RAW",
        "BOMBCFG.EXE", "BOMB.ICO", "BOMBCFG.ICO", "BOMB.ICP", "BOMBCFG.ICP", "BOMBCFG.HLP",
    ];

    public GameData(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
            throw new DirectoryNotFoundException($"Original data folder not found: {rootDirectory}");
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    /// <summary>
    /// Resolves a file of the original by its DOS name, ignoring case (DOS names are case-insensitive,
    /// the host file system may not be). Returns null if the file does not exist.
    /// </summary>
    public string? Find(string dosName)
    {
        var direct = Path.Combine(RootDirectory, dosName);
        if (File.Exists(direct))
            return direct;
        return Directory.EnumerateFiles(RootDirectory)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), dosName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Like <see cref="Find"/>, but fails if the file is missing.</summary>
    public string Require(string dosName) => Find(dosName)
        ?? throw new FileNotFoundException($"Original file missing in {RootDirectory}: {dosName}", dosName);

    /// <summary>Names from <see cref="PackageFiles"/> that are not present.</summary>
    public IReadOnlyList<string> MissingPackageFiles() => PackageFiles.Where(f => Find(f) is null).ToList();

    /// <summary>
    /// Finds the original files: the explicit path if given, otherwise a folder <c>gamedata</c> containing
    /// <c>BOMB.EXE</c> in the current directory, the application directory or one of their parents.
    /// </summary>
    public static GameData Locate(string? explicitPath = null)
    {
        if (!string.IsNullOrEmpty(explicitPath))
            return new GameData(Path.GetFullPath(explicitPath));

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, FolderName);
                if (Directory.Exists(candidate) && new GameData(candidate).Find(MarkerFile) is not null)
                    return new GameData(candidate);
            }
        }

        throw new DirectoryNotFoundException(
            $"Original data not found: no folder \"{FolderName}\" with {MarkerFile} in the current directory, " +
            "the application directory or their parents. See README (section \"Originaldateien bereitstellen\").");
    }
}
