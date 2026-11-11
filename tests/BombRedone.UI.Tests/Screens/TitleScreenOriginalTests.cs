using System.Security.Cryptography;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Title sequence with the original files: every state against the images decoded by <see cref="RawImage"/>.</summary>
public class TitleScreenOriginalTests
{
    private static string Sha(ReadOnlySpan<byte> data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static (TitleScreen Screen, RawImage First, RawImage Second) Load()
    {
        var data = TestGameData.Load();
        var first = RawImage.Load(data.Require("BOMB1.RAW"));
        var second = RawImage.Load(data.Require("BOMB2.RAW"));
        return (new TitleScreen(TitleImages.Load(data), () => new PlaceholderScreen()), first, second);
    }

    private static Surface Draw(IScreen screen)
    {
        var surface = new Surface();
        screen.Draw(surface);
        return surface;
    }

    /// <summary>BOMB1 with every BOMB2 pixel of the given colours copied over it, except pixel 0.</summary>
    private static byte[] Composite(RawImage first, RawImage second, IEnumerable<int> colors)
    {
        var set = colors.ToHashSet();
        var result = first.GetPixels();
        for (var i = 1; i < result.Length; i++)
        {
            if (set.Contains(second.Pixels[i]))
                result[i] = second.Pixels[i];
        }
        return result;
    }

    [GameDataFact]
    public void StartShowsTheFirstImageWithThePaletteOfTheSecond()
    {
        var (screen, first, second) = Load();
        var surface = Draw(screen);

        Assert.Equal(first.Pixels.ToArray(), surface.Pixels);
        Assert.StartsWith("e54ecb608342", Sha(surface.Pixels));
        var expected = second.Palette.ToAbgr();
        Assert.Equal(expected[..255], surface.Palette[..255]);
        Assert.Equal(0xFF000000u, surface.Palette[255]);
    }

    [GameDataFact]
    public void EveryRevealStepMatchesTheDecodedImages()
    {
        var (screen, first, second) = Load();
        var revealed = new List<int>();
        for (var s = 0; s < TitleScreen.RevealStepCount; s++)
        {
            var colors = TitleScreen.RevealColors(s);
            screen.Update(TitleScreen.OverlayPassDuration * colors.Count, ScreenInput.None);
            revealed.AddRange(colors);
            Assert.Equal(Composite(first, second, revealed), Draw(screen).Pixels);
        }

        var final = Draw(screen);
        Assert.StartsWith("b75cd2b71b6b", Sha(final.Pixels));
        // BOMB2 uses only colours 0 and 15 outside the reveal list, so nothing else of it is ever shown.
        var hidden = second.Pixels.ToArray().Where(p => !revealed.Contains(p)).Distinct().Order().ToArray();
        Assert.Equal([0, 15], hidden);
        Assert.Equal(TitlePhase.Cycle, screen.Phase);
    }

    [GameDataFact]
    public void CyclingChangesOnlyColours135To154AndKeyLeadsToThePlaceholder()
    {
        var (screen, _, second) = Load();
        screen.Update(TitleScreen.OverlayPassDuration * TitleScreen.RevealPassCount, ScreenInput.None);
        var pixels = Draw(screen).Pixels;
        var palette = second.Palette.ToAbgr();

        for (var step = 0; step < 25; step++)
        {
            var drawn = Draw(screen);
            Assert.Equal(pixels, drawn.Pixels);
            for (var i = 0; i < 255; i++)
            {
                if (i is < TitleScreen.CycleFirstColor or >= TitleScreen.CycleFirstColor + TitleScreen.CycleColorCount)
                    Assert.Equal(palette[i], drawn.Palette[i]);
            }
            screen.Update(TitleScreen.CycleStepDuration, ScreenInput.None);
        }

        Assert.Same(screen, screen.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)));
        Assert.IsType<PlaceholderScreen>(screen.Update(TitleScreen.CycleStepDuration, ScreenInput.None));
    }

    /// <summary>#47: the cycling waits Delay(60): mov ax,3Ch; push ax; call far Delay at BOMB.OVR 0x004B2.</summary>
    [GameDataFact]
    public void CyclingDelayIsSixtyMillisecondsInTheOverlay()
    {
        var overlay = File.ReadAllBytes(TestGameData.Load().Require("BOMB.OVR"));
        Assert.Equal(new byte[] { 0xB8, 60, 0x00, 0x50, 0x9A }, overlay.AsSpan(0x004B2, 5).ToArray());
        Assert.Equal(TimeSpan.FromMilliseconds(60), TitleScreen.CycleStepDuration);
    }
}
