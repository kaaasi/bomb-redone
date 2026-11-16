using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>
/// Optional observer of the calls a shot makes, at the places the probe <c>feld_schuss.py</c> records them (#163): the
/// entry of the impact O6:00E0 and the return of the damage O10:0000. It only watches and must not change the playfield.
/// </summary>
public interface IShotTrace
{
    /// <summary>O6:00E0(shooter, x, y, colour) is entered (<see cref="Impact.Run"/>).</summary>
    void Impact(PlayfieldTank shooter, int color, Real48 x, Real48 y);

    /// <summary>
    /// O10:0000(record, defence, amount) returns <paramref name="lost"/> energy (<see cref="Shot.Damage.Apply"/>).
    /// </summary>
    void Damage(PlayfieldTank tank, int amount, int defense, int lost);
}
