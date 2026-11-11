using BombRedone.Formats;
using BombRedone.Tests.Support;
using Xunit.Sdk;

namespace BombRedone.Formats.Tests;

/// <summary>
/// Access of the tests to the reference font of DOSBox 0.74-3 (reference repository, folder <c>schriften/</c>; never in
/// this repository). Treated like original data: missing means <b>skipped</b>, and <c>BOMB_SKIP_GAMEDATA=1</c> hides it.
/// </summary>
/// <remarks>
/// Like the original data, <c>BOMB_REQUIRE_GAMEDATA=1</c> demands the font (tools/claude/fetch-reference.sh fetches
/// <c>schriften/</c> by default, issue #60); <c>BOMB_REQUIRE_REFERENCE_FONT=1</c> demands it on its own.
/// </remarks>
public static class TestReferenceFont
{
    /// <summary>Environment variable that makes a missing reference font a failure.</summary>
    public const string RequireVariable = "BOMB_REQUIRE_REFERENCE_FONT";

    /// <summary>Start of every message about the missing reference font.</summary>
    public const string MissingMessage = "Reference font missing";

    private static readonly Lazy<string?> FontPath = new(() =>
        Environment.GetEnvironmentVariable(TestGameData.SkipVariable) == "1"
            ? null
            : BitmapFontSource.FindReference(BitmapFontSource.DefaultSearchDirectories()));

    public static bool Required =>
        Environment.GetEnvironmentVariable(RequireVariable) == "1" ||
        Environment.GetEnvironmentVariable(TestGameData.RequireVariable) == "1";

    public static string? SkipReason => FontPath.Value is not null || Required
        ? null
        : $"{MissingMessage} ({BitmapFontSource.LocalReferenceFolder}/{BitmapFontSource.ReferenceFolder}/" +
          $"{BitmapFontSource.ReferenceFileName}) - skipped. Mandatory run: {RequireVariable}=1";

    /// <summary>
    /// Path of the reference font; fails if it is missing (reachable in the mandatory run only). The message starts like a
    /// missing original file, so the mandatory run of tools/pruefen.sh counts it as the wanted failure.
    /// </summary>
    public static string Path => FontPath.Value ?? throw new InvalidOperationException(
        $"{TestGameData.MissingMessage}: {MissingMessage}, but {RequireVariable}=1 or {TestGameData.RequireVariable}=1 " +
        "demands it.");
}

/// <summary>Test that needs the reference font; skipped without it (see <see cref="TestReferenceFont"/>).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
[XunitTestCaseDiscoverer("Xunit.Sdk.FactDiscoverer", "xunit.execution.{Platform}")]
[TraitDiscoverer("Xunit.Sdk.TraitDiscoverer", "xunit.core")]
public sealed class ReferenceFontFactAttribute : FactAttribute, ITraitAttribute
{
    public ReferenceFontFactAttribute(string name = "Category", string value = "ReferenceFont")
    {
        Name = name;
        Value = value;
        if (TestReferenceFont.SkipReason is { } reason)
            Skip = reason;
    }

    public string Name { get; }
    public string Value { get; }
}
