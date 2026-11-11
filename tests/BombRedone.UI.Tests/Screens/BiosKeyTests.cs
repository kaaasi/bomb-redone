using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Key words the window passes to the screens (#75).</summary>
public class BiosKeyTests
{
    [Theory]
    [InlineData('?', 0x003F)]  // "Random" in the computer list (O2:4C2F)
    [InlineData('!', 0x0021)]  // punctuation in names (O2:42A5)
    [InlineData('.', 0x002E)]
    [InlineData('a', 0x0061)]
    [InlineData('Z', 0x005A)]
    [InlineData('7', 0x0037)]
    [InlineData('~', 0x007E)]
    public void PrintableAsciiIsPassedAsCharacter(char typed, int expected)
    {
        Assert.Equal(expected, BiosKey.FromTypedCharacter(typed));
    }

    [Theory]
    [InlineData(' ')]     // Space comes from the key with scan code 0x39 (BiosKey.Space)
    [InlineData('\r')]    // Enter from the key
    [InlineData('\b')]    // Backspace from the key
    [InlineData('\x1B')]  // Esc from the key
    [InlineData('\x7F')]
    [InlineData('ä')]     // Unicode, not code page 437
    public void OtherCharactersAreLeftToTheKeys(char typed)
    {
        Assert.Equal(0, BiosKey.FromTypedCharacter(typed));
    }
}
