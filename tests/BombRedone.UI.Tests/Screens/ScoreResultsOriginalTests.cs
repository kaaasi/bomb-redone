using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Texts of the status screen and the end result against the Pascal strings in BOMB.OVR (overlay O3 at 0x08A8C).</summary>
public class ScoreResultsOriginalTests
{
    private const int Overlay3 = 0x08A8C;

    private static string PascalString(byte[] file, int offset) =>
        new(file.AsSpan(offset + 1, file[offset]).ToArray().Select(b => b == 0xC4 ? '─' : (char)b).ToArray());

    [GameDataTheory]
    [InlineData(0x74E4, "   And just in case you missed them:")]
    [InlineData(0x750A, ScoreResults.ResultsHeading)]
    [InlineData(0x7520, ScoreResults.ColumnHeading)]
    [InlineData(0x74DD, "Played")]
    [InlineData(0x5A73, "Played out of ")]
    [InlineData(0x5A64, " Games ")]
    [InlineData(0x5A6C, " Game ")]
    [InlineData(0x5AA1, "Continue")]
    [InlineData(0x5AAA, "Exit")]
    public void TextsAreThoseOfTheOverlay(int offset, string text)
    {
        var ovr = File.ReadAllBytes(TestGameData.Load().Require("BOMB.OVR"));
        Assert.Equal(text, PascalString(ovr, Overlay3 + offset));
    }

    [GameDataFact]
    public void RuleIsASpaceAndThirtyTwoLines()
    {
        var ovr = File.ReadAllBytes(TestGameData.Load().Require("BOMB.OVR"));
        Assert.Equal(ScoreResults.Rule, PascalString(ovr, Overlay3 + 0x7540));
    }
}
