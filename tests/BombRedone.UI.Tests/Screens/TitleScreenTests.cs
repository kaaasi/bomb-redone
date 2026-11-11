using BombRedone.Formats;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

public class TitleScreenTests
{
    private const int PixelCount = 64000;
    private static readonly TimeSpan Pass = TitleScreen.OverlayPassDuration;
    private static readonly TimeSpan Cycle = TitleScreen.CycleStepDuration;
    private static readonly TimeSpan RevealTotal = Pass * TitleScreen.RevealPassCount;

    private static readonly ScreenInput Key = new(KeyPressed: true);
    private static readonly ScreenInput Shifts = new(BothShiftsDown: true);

    private sealed class NextScreen : IScreen
    {
        public IScreen? Update(TimeSpan elapsed, ScreenInput input) => this;

        public void Draw(Surface surface) { }
    }

    /// <summary>Synthetic title file: every pixel has <paramref name="fill"/>, palette entry i = (i % 64, 1, 2).</summary>
    private static byte[] Image(byte fill, Action<byte[]>? edit = null, byte paletteGreen = 1)
    {
        var data = new byte[TitleImages.RecordLength];
        Array.Fill(data, fill, 0, PixelCount);
        for (var i = 0; i < 256; i++)
        {
            data[PixelCount + i * 3] = (byte)(i % 64);
            data[PixelCount + i * 3 + 1] = paletteGreen;
            data[PixelCount + i * 3 + 2] = 2;
        }
        edit?.Invoke(data);
        return data;
    }

    private static (TitleScreen Screen, NextScreen Next) Create(byte[]? first, byte[]? second, byte[]? previousDac = null)
    {
        var next = new NextScreen();
        return (new TitleScreen(new TitleImages(first, second), () => next, previousDac), next);
    }

    private static Surface Draw(TitleScreen screen)
    {
        var surface = new Surface();
        screen.Draw(surface);
        return surface;
    }

    private static uint Abgr(int r6, int g6, int b6) =>
        0xFF000000u | ((uint)Palette.Widen(b6) << 16) | ((uint)Palette.Widen(g6) << 8) | Palette.Widen(r6);

    [Fact]
    public void WithoutSecondFileTheTitleIsSkippedAtOnce()
    {
        foreach (var first in new[] { null, Image(7) })
        {
            var (screen, next) = Create(first, null);
            Assert.Equal(TitlePhase.Finished, screen.Phase);
            Assert.Same(next, screen.Update(TimeSpan.Zero, ScreenInput.None));
        }
    }

    [Fact]
    public void FirstImageIsShownWithThePaletteOfTheSecond()
    {
        var (screen, _) = Create(Image(7, paletteGreen: 30), Image(0, paletteGreen: 5));
        var surface = Draw(screen);

        Assert.All(surface.Pixels, p => Assert.Equal(7, p));
        Assert.Equal(Abgr(7, 5, 2), surface.Palette[7]);
        for (var i = 0; i < 255; i++)
            Assert.Equal(Abgr(i % 64, 5, 2), surface.Palette[i]);
        // Entry 255 is not part of the palette calls (count byte 255) and keeps the previous DAC value.
        Assert.Equal(Abgr(0, 0, 0), surface.Palette[255]);
    }

    [Fact]
    public void EntryTwoHundredFiftyFiveKeepsThePreviousDac()
    {
        var previous = new byte[768];
        previous[765] = 10;
        previous[766] = 20;
        previous[767] = 30;
        var (screen, _) = Create(Image(1), Image(0), previous);
        Assert.Equal(Abgr(10, 20, 30), Draw(screen).Palette[255]);
    }

    [Fact]
    public void WithoutFirstFileTheBaseImageIsIndexZero()
    {
        var (screen, _) = Create(null, Image(0, d => d[100] = 135));
        Assert.All(Draw(screen).Pixels, p => Assert.Equal(0, p));

        screen.Update(Pass, ScreenInput.None);
        var surface = Draw(screen);
        Assert.Equal(135, surface.Pixels[100]);
        Assert.Equal(0, surface.Pixels[101]);
    }

    [Fact]
    public void RevealCopiesTheColoursInTheOriginalOrder()
    {
        var expected = new List<int[]>();
        for (var c = 135; c <= 154; c++) expected.Add([c]);
        for (var c = 16; c <= 20; c++) expected.Add([c]);
        for (var c = 115; c <= 124; c++) expected.Add([c]);
        for (var c = 155; c <= 164; c++) expected.Add([c, c + 20]);

        Assert.Equal(45, TitleScreen.RevealStepCount);
        Assert.Equal(55, TitleScreen.RevealPassCount);
        for (var s = 0; s < expected.Count; s++)
            Assert.Equal(expected[s], TitleScreen.RevealColors(s));
    }

    [Fact]
    public void EachStepCopiesOnlyItsColoursAndKeepsTheRest()
    {
        // Pixel 1 + c holds colour c in the overlay for every c; all other overlay pixels are 0.
        var (screen, _) = Create(Image(1), Image(0, d =>
        {
            for (var c = 0; c < 256; c++) d[1000 + c] = (byte)c;
        }));

        var revealed = new HashSet<int>();
        for (var s = 0; s < TitleScreen.RevealStepCount; s++)
        {
            var colors = TitleScreen.RevealColors(s);
            screen.Update(Pass * colors.Count - TimeSpan.FromTicks(1), ScreenInput.None);
            Assert.Equal(s, screen.RevealStepsDone);
            screen.Update(TimeSpan.FromTicks(1), ScreenInput.None);
            Assert.Equal(s + 1, screen.RevealStepsDone);
            revealed.UnionWith(colors);

            var pixels = Draw(screen).Pixels;
            for (var c = 0; c < 256; c++)
                Assert.Equal(revealed.Contains(c) ? c : 1, pixels[1000 + c]);
        }

        // Colours outside the list (here 0, 15, 21, 233, ...) never appear; the base image stays elsewhere.
        var final = Draw(screen).Pixels;
        Assert.Equal(1, final[1000]);
        Assert.Equal(1, final[1000 + 233]);
        Assert.Equal(1, final[999]);
        Assert.Equal(TitlePhase.Cycle, screen.Phase);
    }

    [Fact]
    public void PixelZeroIsNeverCopied()
    {
        var (screen, _) = Create(Image(1), Image(135));
        screen.Update(Pass, ScreenInput.None);
        var pixels = Draw(screen).Pixels;
        Assert.Equal(1, pixels[0]);
        Assert.Equal(135, pixels[1]);
        Assert.Equal(135, pixels[PixelCount - 1]);
    }

    [Fact]
    public void RevealTakesFiftyFivePassesThenTheCyclingStarts()
    {
        var (screen, _) = Create(Image(1), Image(0));
        screen.Update(RevealTotal - TimeSpan.FromTicks(1), ScreenInput.None);
        Assert.Equal(TitlePhase.Reveal, screen.Phase);
        Assert.Equal(0, screen.CycleStep);

        screen.Update(TimeSpan.FromTicks(1), ScreenInput.None);
        Assert.Equal(TitlePhase.Cycle, screen.Phase);
        Assert.Equal(1, screen.CycleStep);
    }

    [Fact]
    public void BothShiftsEndTheRevealAtTheFirstCheck()
    {
        var (screen, next) = Create(Image(1), Image(0));
        Assert.Same(next, screen.Update(TimeSpan.Zero, Shifts));
        Assert.Equal(TitlePhase.Finished, screen.Phase);
        Assert.Equal(0, screen.RevealStepsDone);
    }

    [Fact]
    public void BothShiftsOrBothButtonsEndTheRevealAfterTheNextStep()
    {
        foreach (var abort in new[] { Shifts, new ScreenInput(Mouse: MouseButtons.Left | MouseButtons.Right) })
        {
            var (screen, next) = Create(Image(1), Image(0));
            Assert.Same(screen, screen.Update(Pass * 2, ScreenInput.None));
            Assert.Same(screen, screen.Update(Pass - TimeSpan.FromTicks(1), abort));
            Assert.Same(next, screen.Update(TimeSpan.FromTicks(1), abort));
            Assert.Equal(3, screen.RevealStepsDone);
        }
    }

    [Fact]
    public void KeysOneShiftOrOneButtonDoNotEndTheReveal()
    {
        var inputs = new[]
        {
            Key, new ScreenInput(Mouse: MouseButtons.Left), new ScreenInput(Mouse: MouseButtons.Right),
        };
        var (screen, _) = Create(Image(1), Image(0));
        foreach (var input in inputs)
            Assert.Same(screen, screen.Update(Pass, input));
        Assert.Equal(TitlePhase.Reveal, screen.Phase);
    }

    [Fact]
    public void KeysPressedDuringTheRevealAreDiscarded()
    {
        var (screen, _) = Create(Image(1), Image(0));
        screen.Update(RevealTotal - Pass, Key);
        screen.Update(Pass, Key); // the update that ends the reveal: before the flush
        Assert.Equal(TitlePhase.Cycle, screen.Phase);
        Assert.Same(screen, screen.Update(Cycle * 3, ScreenInput.None));
        Assert.Equal(4, screen.CycleStep);
    }

    [Fact]
    public void KeyEndsTheCyclingAtTheNextCheck()
    {
        var (screen, next) = Create(Image(1), Image(0));
        screen.Update(RevealTotal, ScreenInput.None);
        Assert.Same(screen, screen.Update(Cycle / 2, Key));
        // Buffered: the key counts at the check after Delay(60), even without a new press.
        Assert.Same(screen, screen.Update(Cycle / 2 - TimeSpan.FromTicks(1), ScreenInput.None));
        Assert.Same(next, screen.Update(TimeSpan.FromTicks(1), ScreenInput.None));
        Assert.Equal(1, screen.CycleStep);
    }

    [Theory]
    [InlineData(MouseButtons.Left)]
    [InlineData(MouseButtons.Right)]
    [InlineData(MouseButtons.Left | MouseButtons.Right)]
    public void MouseButtonEndsTheCycling(MouseButtons buttons)
    {
        var (screen, next) = Create(Image(1), Image(0));
        screen.Update(RevealTotal + Cycle * 5, ScreenInput.None);
        Assert.Same(screen, screen.Update(Cycle - TimeSpan.FromTicks(1), new ScreenInput(Mouse: buttons)));
        Assert.Same(next, screen.Update(TimeSpan.FromTicks(1), new ScreenInput(Mouse: buttons)));
    }

    [Fact]
    public void BothShiftsDoNotEndTheCycling()
    {
        var (screen, _) = Create(Image(1), Image(0));
        screen.Update(RevealTotal, ScreenInput.None);
        Assert.Same(screen, screen.Update(Cycle * 10, Shifts));
        Assert.Equal(TitlePhase.Cycle, screen.Phase);
    }

    [Fact]
    public void CycleTableMatchesTheOriginalFormula()
    {
        var table = TitleScreen.BuildCycleTable();
        Assert.Equal(120, table.Length);
        byte[] expectedRed =
        [
            6, 12, 18, 25, 31, 37, 44, 50, 56, 63, 69, 75, 81, 88, 94, 100, 107, 113, 119, 0,
            6, 12, 18, 25, 31, 37, 44, 50, 56, 63, 69, 75, 81, 88, 94, 100, 107, 113, 119, 0,
        ];
        for (var e = 0; e < 40; e++)
        {
            Assert.Equal(expectedRed[e], table[e * 3]);
            Assert.Equal(0, table[e * 3 + 1]);
            Assert.Equal(0, table[e * 3 + 2]);
        }
    }

    [Fact]
    public void CyclingShiftsTheRedTableThroughColours135To154AndWraps()
    {
        var table = TitleScreen.BuildCycleTable();
        var (screen, _) = Create(Image(1), Image(0));
        screen.Update(RevealTotal, ScreenInput.None);

        for (var step = 1; step <= 25; step++)
        {
            var expected = (step - 1) % 20 + 1;
            Assert.Equal(expected, screen.CycleStep);
            var dac = screen.GetDac();
            for (var j = 0; j < 20; j++)
            {
                Assert.Equal(table[(expected - 1 + j) * 3] & 0x3F, dac[(135 + j) * 3]);
                Assert.Equal(0, dac[(135 + j) * 3 + 1]);
            }
            // Neighbours keep the palette of BOMB2.
            Assert.Equal(134 % 64, dac[134 * 3]);
            Assert.Equal(155 % 64, dac[155 * 3]);
            screen.Update(Cycle, ScreenInput.None);
        }
    }

    [Fact]
    public void FinishRestoresThePreviousDacButKeepsThePixels()
    {
        var previous = new byte[768];
        for (var i = 0; i < 768; i++) previous[i] = (byte)(i % 50);
        var (screen, _) = Create(Image(1), Image(0, d => d[5] = 135), previous);
        screen.Update(RevealTotal, ScreenInput.None);
        screen.Update(Cycle, Key);
        Assert.Equal(TitlePhase.Finished, screen.Phase);
        Assert.Equal(previous, screen.GetDac());
        Assert.Equal(135, Draw(screen).Pixels[5]);
    }

    [Fact]
    public void FinishedScreenAlwaysReturnsTheSameSuccessor()
    {
        var created = 0;
        var screen = new TitleScreen(TitleImages.None, () => { created++; return new NextScreen(); });
        var first = screen.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.Same(first, screen.Update(TimeSpan.Zero, ScreenInput.None));
        Assert.Equal(1, created);
    }

    [Fact]
    public void DrawingDoesNotChangeTheState()
    {
        var (a, _) = Create(Image(1), Image(0, d => d[7] = 140));
        var (b, _) = Create(Image(1), Image(0, d => d[7] = 140));
        a.Update(Pass * 7, ScreenInput.None);
        b.Update(Pass * 7, ScreenInput.None);
        for (var i = 0; i < 5; i++) Draw(a);
        a.Update(RevealTotal, ScreenInput.None);
        b.Update(RevealTotal, ScreenInput.None);
        Assert.Equal(Draw(b).Pixels, Draw(a).Pixels);
        Assert.Equal(Draw(b).Palette, Draw(a).Palette);
        Assert.Equal(b.CycleStep, a.CycleStep);
    }

    [Fact]
    public void SameInputsGiveTheSameSequenceRegardlessOfTheFrameSplit()
    {
        var (a, _) = Create(Image(1), Image(0, d => d[9] = 160));
        var (b, _) = Create(Image(1), Image(0, d => d[9] = 160));
        a.Update(RevealTotal + Cycle * 7, ScreenInput.None);
        var frame = TimeSpan.FromSeconds(1.0 / 60);
        var total = TimeSpan.Zero;
        while (total + frame <= RevealTotal + Cycle * 7)
        {
            b.Update(frame, ScreenInput.None);
            total += frame;
        }
        b.Update(RevealTotal + Cycle * 7 - total, ScreenInput.None);
        Assert.Equal(a.CycleStep, b.CycleStep);
        Assert.Equal(a.GetDac(), b.GetDac());
        Assert.Equal(Draw(a).Pixels, Draw(b).Pixels);
    }

    [Fact]
    public void PaletteValuesAboveSixBitsAreMaskedLikeTheDac()
    {
        var (screen, _) = Create(Image(1), Image(0, d => d[PixelCount + 3] = 0x7F));
        Assert.Equal(0x3F, screen.GetDac()[3]);
    }

    [Fact]
    public void RejectsWrongSurfaceSizeAndNegativeTime()
    {
        var (screen, _) = Create(Image(1), Image(0));
        Assert.Throws<ArgumentException>(() => screen.Draw(new Surface(640, 400)));
        Assert.Throws<ArgumentOutOfRangeException>(() => screen.Update(TimeSpan.FromTicks(-1), ScreenInput.None));
        Assert.Throws<ArgumentException>(() => Create(Image(1), Image(0), new byte[10]));
    }
}
