using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Setup;
using BombRedone.Core.Turn;

namespace BombRedone.Core.Tests.Turn;

/// <summary>
/// Synthetic checks of <see cref="TurnSession"/> beyond the probe: the waits for release the host sees (O7:0EF7, O7:0E97,
/// O4:13D4) and the mouse bands of the MIRV dialog (O4:16B1 / 19FC).
/// </summary>
public sealed class TurnSessionTests
{
    private static (TurnSession Session, TurnInput Input, List<TurnPlayer> Players) Create(bool mouse = false)
    {
        var random = new TurboPascalRandom(0x12345678);
        var field = Core.Playfield.Playfield.Build(new PlayfieldOptions(2, 0, 0, 0xFFFF, 2, false), random);
        var players = field.Tanks.Select(t => new TurnPlayer(t)).ToList();
        foreach (var p in players)
            p.SetDefaultStocks();
        var input = new TurnInput { MousePresent = mouse, MouseX = 160, MouseY = 100 };
        return (new TurnSession(field, players, input, new TurnSettings()), input, players);
    }

    [Fact]
    public void Tab_waits_for_its_release_before_the_weapon_changes()
    {
        var (session, input, players) = Create();
        session.Begin(1);
        var start = players[0].Tank.Weapon;
        using var turn = session.Play(1, PlayerControl.Keyboard).GetEnumerator();
        Assert.True(turn.MoveNext());
        Assert.Equal(TurnWaitKind.Poll, turn.Current.Kind);
        input[Scancode.Tab] = true;

        // O7:10F1: the routine waits for Tab to go up; the command is applied only after O7:0EF7 returns.
        Assert.True(turn.MoveNext());
        Assert.Equal(TurnWaitKind.ReleaseKeys, turn.Current.Kind);
        Assert.Contains(Scancode.Tab, turn.Current.Keys!);
        Assert.Equal(start, players[0].Tank.Weapon);
        Assert.True(input[Scancode.Tab]);

        input[Scancode.Tab] = false;
        while (turn.MoveNext() && turn.Current.Kind != TurnWaitKind.Poll)
        {
        }

        Assert.NotEqual(start, players[0].Tank.Weapon);
        Assert.Equal([TurnCommand.NextWeapon], session.Commands);
    }

    [Fact]
    public void An_arrow_without_Alt_does_not_wait()
    {
        var (session, input, _) = Create();
        session.Begin(1);
        using var turn = session.Play(1, PlayerControl.Keyboard).GetEnumerator();
        Assert.True(turn.MoveNext());
        input[Scancode.Left] = true;
        var kinds = new List<TurnWaitKind>();
        while (turn.MoveNext() && turn.Current.Kind != TurnWaitKind.Poll)
            kinds.Add(turn.Current.Kind);
        Assert.DoesNotContain(TurnWaitKind.ReleaseKeys, kinds);
        Assert.True(input[Scancode.Left]);
    }

    [Fact]
    public void Fire_with_the_mouse_waits_until_both_buttons_are_up()
    {
        var (session, input, _) = Create(mouse: true);
        session.Begin(1);
        using var turn = session.Play(1, PlayerControl.Mouse).GetEnumerator();
        Assert.True(turn.MoveNext());
        input.MouseButtons = 3;
        var sawRelease = false;
        while (turn.MoveNext())
        {
            if (turn.Current.Kind == TurnWaitKind.ReleaseButtons)
            {
                sawRelease = true;
                Assert.Equal(3, input.MouseButtons);
                input.MouseButtons = 0;
            }
        }

        Assert.True(sawRelease);
        Assert.Equal(TurnCommand.Fire, session.Commands[^1]);
    }

    /// <summary>
    /// [ORIGINAL] O4:16B1 (BOMB.OVR 0x123B6 ff.): y &lt; 89 → 0, 91 &lt; y &lt; 99 → 1, 101 &lt; y &lt; 109 → 2,
    /// 111 &lt; y &lt; 119 → 3, y &gt; 121 (O4:1769 cmp ax,79h) → 4; the gaps keep the pattern (0 at the first poll).
    /// </summary>
    [Theory]
    [InlineData(88, 0)]
    [InlineData(89, 0)]
    [InlineData(91, 0)]
    [InlineData(92, 1)]
    [InlineData(98, 1)]
    [InlineData(99, 0)]
    [InlineData(101, 0)]
    [InlineData(102, 2)]
    [InlineData(108, 2)]
    [InlineData(109, 0)]
    [InlineData(111, 0)]
    [InlineData(112, 3)]
    [InlineData(118, 3)]
    [InlineData(119, 0)]
    [InlineData(121, 0)]
    [InlineData(122, 4)]
    public void The_mirv_pattern_follows_the_mouse_bands(int y, int pattern)
    {
        Assert.Equal((pattern, 0), Mirv(y, 80));
    }

    /// <summary>[ORIGINAL] O4:19FC: y &lt; 89 → 0, 91 &lt; y &lt; 99 → 1, y &gt; 101 → 2.</summary>
    [Theory]
    [InlineData(88, 0)]
    [InlineData(89, 0)]
    [InlineData(91, 0)]
    [InlineData(92, 1)]
    [InlineData(98, 1)]
    [InlineData(99, 0)]
    [InlineData(101, 0)]
    [InlineData(102, 2)]
    public void The_mirv_detonation_follows_the_mouse_bands(int y, int detonation)
    {
        Assert.Equal((0, detonation), Mirv(80, y));
    }

    private static (int Pattern, int Detonation) Mirv(int y1, int y2)
    {
        var (session, input, _) = Create(mouse: true);
        foreach (var w in session.MirvSetup(1, PlayerControl.Mouse))
        {
            if (w.Kind == TurnWaitKind.ReleaseButtons)
                input.MouseButtons = 0;
            if (w.Kind != TurnWaitKind.Poll)
                continue;
            input.MouseY = w.Site == TurnPollSite.MirvMouse1 ? y1 : y2;
            input.MouseButtons = 1;
        }

        return session.MirvChoice!.Value;
    }
}
