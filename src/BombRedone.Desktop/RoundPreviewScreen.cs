using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;
using BombRedone.UI;
using BombRedone.UI.Screens;

namespace BombRedone.Desktop;

/// <summary>
/// Preview of a round with shots (spur schuss): the player at turn aims with ←/→ (angle) and ↑/↓ (power ± 10), W switches
/// between Lead Ball and Baby Missile (R2-D8), Space fires; Enter builds the next round. [APPROX] Stand-in for the turn of
/// the original (O7:040C, runde.md section 6, round 3): no header update, no stock, the next active player follows.
/// </summary>
internal sealed class RoundPreviewScreen : IScreen
{
    private readonly PlayfieldScreen _playfield;
    private readonly ShotSettings _settings;
    private readonly int _crumble;
    private readonly bool _fallDown;
    private readonly TurboPascalRandom _random;
    private readonly ShotTiming _timing;
    private readonly Func<IScreen?> _nextRound;
    private int _player;

    public RoundPreviewScreen(PlayfieldScreen playfield, int startPlayer, ShotSettings settings, int crumble, bool fallDown,
        TurboPascalRandom random, ShotTiming timing, Func<IScreen?> nextRound)
    {
        _playfield = playfield;
        _player = startPlayer;
        _settings = settings;
        _crumble = crumble;
        _fallDown = fallDown;
        _random = random;
        _timing = timing;
        _nextRound = nextRound;
    }

    private PlayfieldTank Tank => _playfield.Field.Tanks[_player - 1];

    public IScreen? Update(TimeSpan elapsed, ScreenInput input)
    {
        foreach (var key in input.Keys)
        {
            switch (key)
            {
                case BiosKey.Enter:
                case BiosKey.Escape:
                    return _nextRound();
                case BiosKey.Left:
                    Aim(Math.Min(Tank.Angle + 1, 180), Tank.Power);
                    break;
                case BiosKey.Right:
                    Aim(Math.Max(Tank.Angle - 1, 0), Tank.Power);
                    break;
                case BiosKey.Up:
                    Aim(Tank.Angle, Math.Min(Tank.Power + 10, 10 * Tank.Energy));
                    break;
                case BiosKey.Down:
                    Aim(Tank.Angle, Math.Max(Tank.Power - 10, 0));
                    break;
                case 'w' or 'W':
                    Tank.Weapon = Tank.Weapon == 0 ? 1 : 0;
                    break;
                case BiosKey.Space:
                    return Fire();
            }
        }

        return this;
    }

    public void Draw(Surface surface) => _playfield.Draw(surface);

    private void Aim(int angle, int power)
    {
        var field = _playfield.Field;
        var (dx, dy) = Playfield.BarrelOffset(Tank.Angle);
        VgaDraw.CopyLine(field.Background.Pixels, field.Screen.Pixels, Tank.X, Tank.Y, Tank.X + dx * Tank.Direction, Tank.Y - dy);
        Tank.Aim(angle, power);
        field.DrawTank(Tank);
        _playfield.RefreshField();
    }

    private IScreen Fire()
    {
        var shooter = Tank;
        return PlayfieldShotScreen.Fire(_playfield, shooter, _settings, _crumble, _fallDown, _random, _timing, () =>
        {
            var tanks = _playfield.Field.Tanks;
            for (var i = 1; i <= tanks.Count; i++)
            {
                var next = (shooter.Number - 1 + i) % tanks.Count;
                if (tanks[next].Active && tanks[next].Energy > 0)
                {
                    _player = next + 1;
                    break;
                }
            }

            return this;
        });
    }
}
