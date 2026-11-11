using BombRedone.Audio;
using BombRedone.Tests.Support;

namespace BombRedone.Audio.Tests;

public class ToneTests
{
    [Fact]
    public void ZeroFrequencyIsAPause()
    {
        Assert.True(new Tone(0, 50).IsPause);
        Assert.False(new Tone(440, 50).IsPause);
    }

    [Fact]
    public void NegativeValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tone(-1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Tone(440, -1));
    }

    [GameDataFact]
    public void OriginalDocumentationDescribesSoundOptions()
    {
        // Anchors the audio layer to the original until sound data is analysed (after round 1).
        var text = File.ReadAllText(TestGameData.Load().Require("BOMB.DOC"));
        Assert.Contains("SOUND OPTIONS", text);
    }
}
