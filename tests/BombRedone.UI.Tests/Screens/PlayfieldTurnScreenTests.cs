using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Setup;
using BombRedone.Core.Shot;
using BombRedone.Core.Turn;
using BombRedone.Formats;
using BombRedone.UI.Screens;
using BombRedone.UI.Tests.Text;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// The host of the turn (Desktop) with synthetic fonts: frames of 16 ms, held keys as the keyboard handler's table.
/// Checks the timing of the polls (delay TS per poll), the waits for release, the shot and the next player.
/// </summary>
public sealed class PlayfieldTurnScreenTests
{
    private static readonly MenuFonts Fonts = new(
        BgiFont.Parse(SyntheticFont.Build("EURO", 32, [new SyntheticFont.Glyph(4)])),
        BgiFont.Parse(SyntheticFont.Build("LITT", 32, [new SyntheticFont.Glyph(4)])),
        new BitmapFont(new byte[256 * 8]));

    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private sealed class Rig
    {
        public readonly bool[] Held = new bool[Scancode.Count];
        public readonly List<TurnPlayer> Players;
        public readonly PlayfieldTurnScreen Screen;
        public int NextRounds;

        public Rig(params PlayerControl[] controls)
        {
            var random = new TurboPascalRandom(0x12345678);
            var field = Playfield.Build(new PlayfieldOptions(controls.Length, 0, 0, 0xFFFF, 2, false), random);
            var wind = Wind.Choose(2, random);
            var names = Enumerable.Range(1, controls.Length).Select(n => "Player " + n).ToArray();
            var playfield = new PlayfieldScreen(Fonts, field, wind, names);
            var view = new PlayfieldTurnView(playfield, Fonts, wind);
            Players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
            foreach (var p in Players)
                p.SetDefaultStocks();
            Screen = new PlayfieldTurnScreen(playfield, view, Players, controls, 1, 1, () => (bool[])Held.Clone(),
                new ShotSettings(10, wind.Value, false, false), 0, false, random,
                new ShotTiming(TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero), () =>
                {
                    NextRounds++;
                    return null;
                });
        }

        public IScreen? Run(int frames, ScreenInput input = default)
        {
            IScreen? current = Screen;
            for (var i = 0; i < frames && current is not null; i++)
                current = current.Update(Frame, input);
            return current;
        }
    }

    [Fact]
    public void A_held_arrow_repeats_about_every_delay()
    {
        var rig = new Rig(PlayerControl.Keyboard, PlayerControl.Keyboard);
        var start = rig.Players[0].Tank.Angle;
        rig.Held[Scancode.Right] = true;
        rig.Run(10);
        var turned = start - rig.Players[0].Tank.Angle;

        // 160 ms at TS = 8 ms per poll: about 20 steps.
        Assert.InRange(turned, 15, 21);
    }

    [Fact]
    public void With_Alt_an_arrow_turns_once_until_it_is_released()
    {
        var rig = new Rig(PlayerControl.Keyboard, PlayerControl.Keyboard);
        var start = rig.Players[0].Tank.Angle;
        rig.Held[Scancode.Alt] = true;
        rig.Held[Scancode.Left] = true;
        rig.Run(10);
        Assert.Equal(start + 1, rig.Players[0].Tank.Angle);
        rig.Held[Scancode.Left] = false;
        rig.Run(1);
        rig.Held[Scancode.Left] = true;
        rig.Run(1);
        Assert.Equal(start + 2, rig.Players[0].Tank.Angle);
    }

    [Fact]
    public void Fire_shoots_and_passes_the_turn()
    {
        var rig = new Rig(PlayerControl.Keyboard, PlayerControl.Keyboard);
        rig.Held[Scancode.Space] = true;
        var screen = rig.Run(1);
        Assert.IsType<PlayfieldShotScreen>(screen);
        Assert.Equal(29, rig.Players[0].WeaponStock[1]);
        rig.Held[Scancode.Space] = false;
        var after = rig.Run(1000, default);
        Assert.Equal(1, rig.Players[1].Turns);
        Assert.Equal(0, rig.NextRounds);
        _ = after;
    }

    [Fact]
    public void F2_asks_for_the_next_round()
    {
        var rig = new Rig(PlayerControl.Keyboard, PlayerControl.Keyboard);
        rig.Held[PlayfieldTurnScreen.NextRoundKey] = true;
        Assert.Null(rig.Run(1));
        Assert.Equal(1, rig.NextRounds);
    }

    [Fact]
    public void A_mouse_click_on_the_angle_field_turns_once_per_poll()
    {
        var rig = new Rig(PlayerControl.Mouse, PlayerControl.Keyboard);
        var start = rig.Players[0].Tank.Angle;
        rig.Run(1, new ScreenInput(Mouse: MouseButtons.Left) { MouseX = 120, MouseY = 4 });
        Assert.True(rig.Players[0].Tank.Angle > start);
    }
}
