using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>Playfield screen with fonts without visible strokes: pixels of the core, weapon bar, palette.</summary>
public sealed class PlayfieldScreenTests
{
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private static PlayfieldScreen Create(int players = 3, int wall = 1, int background = 2)
    {
        var random = new TurboPascalRandom(0x00000001);
        var field = Playfield.Build(new PlayfieldOptions(players, wall, background), random);
        var names = Enumerable.Range(1, players).Select(n => "Player " + n).ToArray();
        return new PlayfieldScreen(Fonts, field, Wind.Choose(2, random), names);
    }

    [Fact]
    public void Without_glyphs_the_field_below_the_header_is_the_core_screen()
    {
        var screen = Create();
        for (var y = 28; y < 200; y++)
        {
            for (var x = 7; x < 320; x++)
                Assert.Equal(screen.Field.Screen[x, y], screen.Screen[x, y]);
        }
    }

    [Fact]
    public void Weapon_bar_has_twenty_fields_in_colours_100_to_119()
    {
        var screen = Create();
        for (var i = 0; i < PlayfieldScreen.WeaponFields; i++)
        {
            var left = 15 * i + 10;
            Assert.Equal(100 + i, screen.Screen[left + 7, 18]);
            Assert.Equal(100 + i, screen.Screen[left + 14, 22]);
            Assert.Equal((0, 0, 0), (screen.Dac[3 * (100 + i)], screen.Dac[3 * (100 + i) + 1], screen.Dac[3 * (100 + i) + 2]));
        }

        // The bottom edge (y 18 + 10) lies outside the field's viewport.
        Assert.NotEqual(100, screen.Screen[17, 28]);
    }

    [Fact]
    public void Single_pixel_weapon_is_one_dot_in_the_middle()
    {
        var screen = Create();
        Assert.Equal(100, screen.Screen[10 + 7, 18 + 5]);
        Assert.Equal(0, screen.Screen[10 + 6, 18 + 5]);
        Assert.Equal(0, screen.Screen[10 + 7, 18 + 4]);
    }

    [Fact]
    public void Written_DAC_entries_add_the_weapon_colours_to_those_of_the_core()
    {
        var screen = Create();
        var entries = screen.WrittenDacEntries();
        var core = screen.Field.Palette.WrittenEntries();
        Assert.Equal(core.Length + 4 * PlayfieldScreen.WeaponFields, entries.Length);
    }

    [Fact]
    public void Drawing_copies_pixels_and_palette_without_changing_the_screen()
    {
        var screen = Create();
        var before = screen.Screen.Pixels.ToArray();
        var target = new Surface();
        screen.Draw(target);
        Assert.Equal(before, target.Pixels);
        Assert.Equal(screen.Screen.Palette, target.Palette);
        Assert.Equal(before, screen.Screen.Pixels);
    }

    [Theory]
    [InlineData(WallKind.None, "")]
    [InlineData(WallKind.Sticky, "STICKEY")]
    [InlineData(WallKind.Elastic, "ELASTIC")]
    [InlineData(WallKind.Accelerate, "ACCELERATE")]
    [InlineData(WallKind.Warp, "WARP")]
    public void Wall_labels_are_the_texts_of_the_original(WallKind wall, string label) =>
        Assert.Equal(label, PlayfieldScreen.WallLabel(wall));

    [Fact]
    public void A_key_switches_to_the_next_screen_only_with_a_callback()
    {
        var random = new TurboPascalRandom(7);
        var field = Playfield.Build(new PlayfieldOptions(2), random);
        var still = new PlayfieldScreen(Fonts, field, new Wind(0, 0), ["A", "B"]);
        Assert.Same(still, still.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)));

        var calls = 0;
        var moving = new PlayfieldScreen(Fonts, field, new Wind(0, 0), ["A", "B"], () =>
        {
            calls++;
            return null;
        });
        Assert.Same(moving, moving.Update(TimeSpan.FromSeconds(1), ScreenInput.None));
        Assert.Null(moving.Update(TimeSpan.Zero, new ScreenInput(KeyPressed: true)));
        Assert.Equal(1, calls);
    }
}
