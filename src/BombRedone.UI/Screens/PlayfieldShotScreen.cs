using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;

namespace BombRedone.UI.Screens;

/// <summary>Timing of the pictures of a shot.</summary>
/// <param name="Tick">One tick of the flight. [APPROX] The original has no delay there; it ran as fast as the machine.</param>
/// <param name="Explosion">Delay(ES) after each palette step of the explosion (option ES, DS:E8C6, milliseconds).</param>
/// <param name="Fall">Delay(FS) after each row of a falling tank (option FS, DS:E8C8, milliseconds).</param>
public sealed record ShotTiming(TimeSpan Tick, TimeSpan Explosion, TimeSpan Fall)
{
    /// <summary>Timing from the options ES and FS of BOMB.CFG, 15 ms per tick.</summary>
    public static ShotTiming FromOptions(int explosionSpeed, int fallSpeed) =>
        new(TimeSpan.FromMilliseconds(15), TimeSpan.FromMilliseconds(explosionSpeed), TimeSpan.FromMilliseconds(fallSpeed));
}

/// <summary>
/// Plays a shot on the playfield (spur schuss, Teil 5): Core runs the shot, the landslide and the falling tanks at once
/// (<see cref="BallisticShot"/>, <see cref="AfterShot"/>) and reports every picture worth showing; this screen keeps those
/// pictures (frame and field, DAC) and shows them one after another. The trail, the explosion with its palette runs, the
/// crater, the landslide and the falling tanks are therefore exactly the pixels of Core. Drawing never changes the field.
/// </summary>
public sealed class PlayfieldShotScreen : IScreen
{
    private readonly PlayfieldScreen _playfield;
    private readonly List<(byte[] Pixels, byte[] Dac, TimeSpan Hold)> _frames = [];
    private readonly Func<IScreen?> _done;
    private int _index;
    private TimeSpan _shown;

    private PlayfieldShotScreen(PlayfieldScreen playfield, Func<IScreen?> done)
    {
        _playfield = playfield;
        _done = done;
    }

    /// <summary>Result of the flight.</summary>
    public ShotResult? Result { get; private set; }

    /// <summary>Number of pictures kept.</summary>
    public int FrameCount => _frames.Count;

    /// <summary>
    /// Fires the weapon of <paramref name="shooter"/> on the field of <paramref name="playfield"/>, then the landslide
    /// (<paramref name="crumble"/>) and the falling tanks; <paramref name="done"/> gives the screen after the last picture.
    /// </summary>
    public static PlayfieldShotScreen Fire(PlayfieldScreen playfield, PlayfieldTank shooter, ShotSettings settings, int crumble,
        bool fallDown, TurboPascalRandom random, ShotTiming timing, Func<IScreen?> done)
    {
        ArgumentNullException.ThrowIfNull(playfield);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(timing);
        ArgumentNullException.ThrowIfNull(done);
        var screen = new PlayfieldShotScreen(playfield, done);
        void Capture(ShotFrame frame)
        {
            playfield.RefreshField();
            var hold = frame switch
            {
                ShotFrame.Palette => timing.Explosion,
                ShotFrame.Fall => timing.Fall,
                _ => timing.Tick,
            };
            screen._frames.Add(([.. playfield.Screen.Pixels], [.. playfield.Dac], hold));
        }

        screen.Result = BallisticShot.Fire(playfield.Field, shooter, settings with { Frame = Capture }, random);
        AfterShot.Run(playfield.Field, crumble, fallDown, random, Capture);
        Capture(ShotFrame.Flight);
        return screen;
    }

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        _shown += elapsed;
        while (_index < _frames.Count && _shown >= _frames[_index].Hold)
        {
            _shown -= _frames[_index].Hold;
            _index++;
        }

        return _index >= _frames.Count ? _done() : this;
    }

    public void Draw(Surface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (_frames.Count == 0)
        {
            _playfield.Draw(surface);
            return;
        }

        var (pixels, dac, _) = _frames[Math.Min(_index, _frames.Count - 1)];
        if (surface.Width != Surface.OriginalWidth || surface.Pixels.Length != pixels.Length)
            throw new ArgumentException("The playfield draws into a 320 x 200 surface only.", nameof(surface));
        pixels.CopyTo(surface.Pixels, 0);
        MenuGraphics.ApplyDac(dac, surface);
    }
}
