using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

public class ScreenHostTests
{
    private sealed class Recorder(Func<IScreen?> after) : IScreen
    {
        public List<(TimeSpan Elapsed, ScreenInput Input)> Updates { get; } = [];

        public int Draws { get; private set; }

        public IScreen? Update(TimeSpan elapsed, ScreenInput input)
        {
            Updates.Add((elapsed, input));
            return after();
        }

        public void Draw(Surface surface) => Draws++;
    }

    [Fact]
    public void PassesTimeAndInputToTheCurrentScreen()
    {
        Recorder? first = null;
        first = new Recorder(() => first);
        var host = new ScreenHost(first);
        var input = new ScreenInput(KeyPressed: true, Mouse: MouseButtons.Right);
        host.Update(TimeSpan.FromMilliseconds(16), input);
        host.Draw(new Surface());
        Assert.Equal([(TimeSpan.FromMilliseconds(16), input)], first.Updates);
        Assert.Equal(1, first.Draws);
        Assert.Same(first, host.Current);
    }

    [Fact]
    public void SwitchesAndTheNewScreenStartsWithTheNextUpdate()
    {
        Recorder? second = null;
        second = new Recorder(() => second);
        var first = new Recorder(() => second);
        var host = new ScreenHost(first);

        host.Update(TimeSpan.FromMilliseconds(5), ScreenInput.None);
        Assert.Same(second, host.Current);
        Assert.Empty(second.Updates);

        host.Update(TimeSpan.FromMilliseconds(7), ScreenInput.None);
        Assert.Single(first.Updates);
        Assert.Equal(TimeSpan.FromMilliseconds(7), Assert.Single(second.Updates).Elapsed);
    }

    [Fact]
    public void NullQuits()
    {
        var host = new ScreenHost(new Recorder(() => null));
        Assert.True(host.IsRunning);
        host.Update(TimeSpan.Zero, ScreenInput.None);
        Assert.False(host.IsRunning);
        host.Update(TimeSpan.Zero, ScreenInput.None);
        host.Draw(new Surface());
    }

    [Fact]
    public void RejectsNegativeTime()
    {
        var host = new ScreenHost(new PlaceholderScreen());
        Assert.Throws<ArgumentOutOfRangeException>(() => host.Update(TimeSpan.FromTicks(-1), ScreenInput.None));
    }

    [Fact]
    public void PlaceholderStaysAndDrawsBlack()
    {
        var placeholder = new PlaceholderScreen();
        Assert.Same(placeholder, placeholder.Update(TimeSpan.FromSeconds(1), new ScreenInput(KeyPressed: true)));
        var surface = new Surface();
        surface.Clear(5);
        surface.Palette[5] = 0xFFFFFFFF;
        placeholder.Draw(surface);
        Assert.All(surface.Pixels, p => Assert.Equal(0, p));
        Assert.Equal(0xFF000000u, surface.Palette[0]);
        Assert.Equal(0u, surface.Palette[5]);
    }

    [Fact]
    public void MouseHoldsNeedsEveryButton()
    {
        var input = new ScreenInput(Mouse: MouseButtons.Left);
        Assert.True(input.MouseHolds(MouseButtons.Left));
        Assert.False(input.MouseHolds(MouseButtons.Left | MouseButtons.Right));
        Assert.True(input.MouseHolds(MouseButtons.None));
    }
}
