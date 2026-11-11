using BombRedone.Formats;
using Xunit.Sdk;

namespace BombRedone.Tests.Support;

/// <summary>
/// Access of the tests to the original files (folder <c>gamedata</c>, never in the repository).
/// </summary>
/// <remarks>
/// If the files are missing, tests marked with <see cref="GameDataFactAttribute"/> are reported as
/// <b>skipped</b> (not as passed). With <c>BOMB_REQUIRE_GAMEDATA=1</c> they run anyway and fail in
/// <see cref="Load"/>, so an explicitly requested run with original data cannot stay silently empty.
/// With <c>BOMB_SKIP_GAMEDATA=1</c> existing original files count as missing: this reproduces the run
/// "without data" (as in CI) locally without moving the folder (<c>tools/pruefen.sh</c>).
/// </remarks>
public static class TestGameData
{
    /// <summary>Environment variable of the mandatory run with original data.</summary>
    public const string RequireVariable = "BOMB_REQUIRE_GAMEDATA";

    /// <summary>Environment variable that makes existing original data count as missing.</summary>
    public const string SkipVariable = "BOMB_SKIP_GAMEDATA";

    /// <summary>Trait "Category" under which these tests appear in the test report.</summary>
    public const string Category = "OriginalData";

    /// <summary>Start of every message about missing data; <c>tools/pruefen.sh</c> matches this text.</summary>
    public const string MissingMessage = "Original data missing";

    private static readonly Lazy<GameData?> Data = new(() =>
    {
        if (Environment.GetEnvironmentVariable(SkipVariable) == "1")
            return null;
        try { return GameData.Locate(); }
        catch (DirectoryNotFoundException) { return null; }
    });

    public static bool Required => Environment.GetEnvironmentVariable(RequireVariable) == "1";

    public static bool Available => Data.Value is not null;

    /// <summary>Reason for skipping, or null if the tests are to run.</summary>
    public static string? SkipReason => Available || Required
        ? null
        : $"{MissingMessage} (folder \"{GameData.FolderName}\") - skipped. Mandatory run: {RequireVariable}=1";

    /// <summary>Original files; fails if they are missing (reachable in the mandatory run only).</summary>
    public static GameData Load() => Data.Value ?? throw new InvalidOperationException(
        $"{MissingMessage} (folder \"{GameData.FolderName}\"), but {RequireVariable}=1 demands a run with original data.");
}

/// <summary>
/// Test that needs the original files: without data a real "Skipped", in the mandatory run a failure.
/// Carries the trait <c>Category=OriginalData</c> for the separate test report.
/// </summary>
/// <remarks>
/// The trait comes from the built-in <c>Xunit.Sdk.TraitDiscoverer</c> via the (defaulted) constructor
/// arguments, so this file works unchanged in several test projects.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscoverer("Xunit.Sdk.FactDiscoverer", "xunit.execution.{Platform}")]
[TraitDiscoverer("Xunit.Sdk.TraitDiscoverer", "xunit.core")]
public sealed class GameDataFactAttribute : FactAttribute, ITraitAttribute
{
    public GameDataFactAttribute(string name = "Category", string value = TestGameData.Category)
    {
        Name = name;
        Value = value;
        if (TestGameData.SkipReason is { } reason)
            Skip = reason;
    }

    public string Name { get; }
    public string Value { get; }
}

/// <summary>Like <see cref="GameDataFactAttribute"/>, for tests with data rows (<c>[InlineData]</c>).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscoverer("Xunit.Sdk.TheoryDiscoverer", "xunit.execution.{Platform}")]
[TraitDiscoverer("Xunit.Sdk.TraitDiscoverer", "xunit.core")]
public sealed class GameDataTheoryAttribute : TheoryAttribute, ITraitAttribute
{
    public GameDataTheoryAttribute(string name = "Category", string value = TestGameData.Category)
    {
        Name = name;
        Value = value;
        if (TestGameData.SkipReason is { } reason)
            Skip = reason;
    }

    public string Name { get; }
    public string Value { get; }
}
