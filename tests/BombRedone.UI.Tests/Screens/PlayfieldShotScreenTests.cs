using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Playback of a shot on the playfield screen (spur schuss, Teil 5), fonts without visible strokes.</summary>
public sealed class PlayfieldShotScreenTests
{
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private static readonly ShotTiming Timing = new(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(20));

    /// <summary>Shot 3 of the probe list (feld_schuss.py): seed 12345678, 2 players, Baby Missile hits tank 2.</summary>
    private static (PlayfieldScreen Screen, PlayfieldShotScreen Shot, Surface Before) Fire(Func<IScreen?> done)
    {
        var random = new TurboPascalRandom(0x12345678);
        var field = Playfield.Build(new PlayfieldOptions(2), random);
        var wind = Wind.Choose(2, random);
        StartPlayer.Choose(new int[2], [true, true], random);
        var screen = new PlayfieldScreen(Fonts, field, wind, ["Player 1", "Player 2"]);
        var before = new Surface();
        screen.Draw(before);
        var shooter = field.Tanks[0];
        shooter.Aim(45, 270);
        shooter.Weapon = 1;
        var shot = PlayfieldShotScreen.Fire(screen, shooter, new ShotSettings(10, wind.Value), 0, true, random, Timing, done);
        return (screen, shot, before);
    }

    [Fact]
    public void Last_picture_is_the_field_of_the_probe_after_falling()
    {
        var (screen, shot, before) = Fire(() => null);
        var surface = new Surface();
        while (shot.Update(TimeSpan.FromMilliseconds(5), ScreenInput.None) == shot)
        {
        }

        shot.Draw(surface);

        // analyse/feld/schuss-post-hashes.tsv, case 0 (shot 3, Crumble 0, falling): frame and field x 8..312, y 28..192.
        var crop = new byte[305 * 165];
        for (var y = 28; y <= 192; y++)
            surface.Pixels.AsSpan(y * 320 + 8, 305).CopyTo(crop.AsSpan((y - 28) * 305));
        Assert.Equal("c3c9935cf213c426b71cbaabd4dc21d1d2e3eb4c40519f8119410265692e3136", Convert.ToHexStringLower(SHA256.HashData(crop)));
        Assert.Equal((297, 114), (screen.Field.Tanks[1].X, screen.Field.Tanks[1].Y));

        // Header and weapon bar (rows 0..27) and the wall label column stay as they were.
        Assert.True(surface.Pixels.AsSpan(0, 28 * 320).SequenceEqual(before.Pixels.AsSpan(0, 28 * 320)));
    }

    [Fact]
    public void Pictures_are_held_by_kind_and_then_the_next_screen_follows()
    {
        var next = new PlaceholderScreen();
        var (_, shot, _) = Fire(() => next);

        // 733 loop steps with SS 10, the explosion (11 rings: 3 · 11 palette steps), falling rows, the final picture.
        Assert.True(shot.FrameCount > 33 + 60, $"frames {shot.FrameCount}");
        Assert.Same(shot, shot.Update(TimeSpan.FromMilliseconds(9), ScreenInput.None));
        Assert.Same(next, shot.Update(TimeSpan.FromSeconds(60), ScreenInput.None));
    }

    [Fact]
    public void Explosion_palette_ends_black_and_the_rings_were_red_in_between()
    {
        var (_, shot, _) = Fire(() => null);
        var surface = new Surface();
        var reds = new HashSet<int>();
        while (shot.Update(TimeSpan.FromMilliseconds(5), ScreenInput.None) == shot)
        {
            shot.Draw(surface);
            reds.Add((int)(surface.Palette[Impact.ExplosionColor] & 0xFF));
        }

        shot.Draw(surface);
        Assert.Contains(reds, r => r > 0);
        Assert.Equal(0u, surface.Palette[Impact.ExplosionColor] & 0xFF);
    }
}
