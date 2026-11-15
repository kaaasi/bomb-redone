using BombRedone.Core.Playfield;

namespace BombRedone.Core.Turn;

/// <summary>Commands of the input routine O7:0EF7 (docs/analyse/runde.md section 6.2).</summary>
public enum TurnCommand
{
    None = 0,

    /// <summary>Angle a := a + r (r = direction).</summary>
    AnglePlus = 1,

    /// <summary>Angle a := a − r.</summary>
    AngleMinus = 2,

    PowerDown = 3,
    PowerUp = 4,
    Fire = 5,
    FlipDirection = 6,
    PreviousWeapon = 7,
    NextWeapon = 8,
    ToggleHerb = 9,
    ToggleVick = 10,
}

/// <summary>Where the turn waits for the host (the poll sites of the probe, docs/analyse/zug.md section 2).</summary>
public enum TurnPollSite
{
    /// <summary>Entry of the input routine O7:0EF7.</summary>
    Input,

    /// <summary>One pass of the cheat loop O7:0FA6.</summary>
    Cheat,

    /// <summary>Tank info O9:0866: until a key is in the BIOS buffer.</summary>
    InfoKey,

    /// <summary>Tank info O9:0875: until a mouse button is down.</summary>
    InfoMouse,

    /// <summary>Target selection O9:0B33: until a key is in the BIOS buffer.</summary>
    TargetKey,

    /// <summary>Target selection O9:0CE4: one pass of the click loop.</summary>
    TargetMouse,

    /// <summary>MIRV dialog, page 1 (pattern) with keys O4:15BE / mouse O4:16B1.</summary>
    MirvKey1,
    MirvMouse1,

    /// <summary>MIRV dialog, page 2 (detonation) with keys O4:18E8 / mouse O4:19FC.</summary>
    MirvKey2,
    MirvMouse2,
}

public enum TurnWaitKind
{
    /// <summary>The host updates <see cref="TurnInput"/> (one step of the input), then resumes.</summary>
    Poll,

    /// <summary>Crt.Delay(<see cref="TurnWait.Value"/> ms).</summary>
    Delay,

    /// <summary>Crt.Sound(<see cref="TurnWait.Value"/> Hz).</summary>
    Sound,

    /// <summary>Crt.NoSound.</summary>
    NoSound,
}

/// <summary>What the turn coroutine yields to its host.</summary>
public readonly record struct TurnWait(TurnWaitKind Kind, TurnPollSite Site = TurnPollSite.Input, int Value = 0)
{
    public static TurnWait Poll(TurnPollSite site) => new(TurnWaitKind.Poll, site);

    public static TurnWait Delay(int ms) => new(TurnWaitKind.Delay, Value: ms);

    public static TurnWait Sound(int hz) => new(TurnWaitKind.Sound, Value: hz);

    public static TurnWait NoSound { get; } = new(TurnWaitKind.NoSound);
}

/// <summary>Options the turn reads.</summary>
/// <param name="Sound">Sound effects (DS:E8B3).</param>
/// <param name="Delay">TS, the delay after every input poll in ms (DS:E8C4).</param>
/// <param name="TeamPlay">Team play (DS:E8BC): the tank info shows the team name.</param>
public sealed record TurnSettings(bool Sound = false, int Delay = 8, bool TeamPlay = false);

/// <summary>
/// The player record fields of the turn beyond <see cref="PlayfieldTank"/> (docs/formate/spielstand.md): stocks,
/// guidance and target, the turn counter. Kept here until the game flow merges the record (spur runde).
/// </summary>
public sealed class TurnPlayer
{
    public const int Weapons = 18, Laser = 5, Mirv = 7;

    public TurnPlayer(PlayfieldTank tank, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(tank);
        Tank = tank;
        Name = name ?? "Player " + tank.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public PlayfieldTank Tank { get; }

    public int Number => Tank.Number;

    /// <summary>Name (+01h).</summary>
    public string Name { get; set; }

    /// <summary>Team name shown in the tank info with team play.</summary>
    public string TeamName { get; set; } = "";

    /// <summary>Weapon stocks (+2Eh, Word[18]).</summary>
    public short[] WeaponStock { get; } = new short[Weapons];

    /// <summary>Guidance stocks (+2Ah Herb, +2Ch Vick).</summary>
    public short[] GuidanceStock { get; } = new short[2];

    /// <summary>Guidance switched on for the shot (+26h): bit 0 Herb, bit 1 Vick.</summary>
    public int Guidance { get; set; }

    /// <summary>Target of the guidance (+28h), a player number.</summary>
    public int Target { get; set; }

    /// <summary>Turns taken (+70h).</summary>
    public int Turns { get; set; }

    /// <summary>Money spent (+17h); cash = score − spent.</summary>
    public int Spent { get; set; }

    /// <summary>Games won (+6Ah).</summary>
    public int Wins { get; set; }

    /// <summary>
    /// Stocks of a new player record. [ORIGINAL] measured with the probe (zug.py, records after O2:0216): Lead Ball 100,
    /// weapon 1 30, weapon 11 100, everything else 0; chosen weapon 1.
    /// </summary>
    public void SetDefaultStocks()
    {
        Array.Clear(WeaponStock);
        WeaponStock[0] = 100;
        WeaponStock[1] = 30;
        WeaponStock[11] = 100;
        Array.Clear(GuidanceStock);
        Tank.Weapon = 1;
    }
}

/// <summary>
/// The screen side of the turn (BGI text, windows); the UI implements it (Teil 3). Core calls it in the order of the
/// original; field pixels and the DAC are drawn by Core itself.
/// </summary>
public interface ITurnView
{
    /// <summary>O4:482C–4910: name bar and wind line of the header.</summary>
    void TurnStarted(TurnPlayer player);

    /// <summary>O7:0424–0537: the fields angle, power, direction and energy.</summary>
    void DrawFields(TurnPlayer player);

    void DrawAngle(TurnPlayer player);

    void DrawPower(TurnPlayer player);

    void DrawDirection(TurnPlayer player);

    /// <summary>O9:0040 window for <paramref name="shown"/> (the screen is saved first).</summary>
    void ShowTankInfo(TurnPlayer shown, IReadOnlyList<TurnPlayer> players);

    /// <summary>O9:08AD: the screen is restored.</summary>
    void CloseTankInfo();

    /// <summary>O9:09FC / 0C0F: the bar of the target selection (keyboard: with the current target).</summary>
    void ShowTargetBar(TurnPlayer shooter, bool mouse);

    /// <summary>O9:0ABD: "Current Target:" with the target's name.</summary>
    void DrawTarget(TurnPlayer shooter, TurnPlayer target);

    /// <summary>O9:0DB4: the screen is restored.</summary>
    void CloseTargetBar();

    /// <summary>O4:13F3 / 17A2: a page of the MIRV dialog (1 pattern, 2 detonation).</summary>
    void ShowMirvPage(int page);

    /// <summary>O4:19C4 / 1AB9: the screen is restored.</summary>
    void CloseMirv();
}

/// <summary>A view that draws nothing (tests of Core, the computer).</summary>
public sealed class NullTurnView : ITurnView
{
    public static NullTurnView Instance { get; } = new();

    public void TurnStarted(TurnPlayer player) { }

    public void DrawFields(TurnPlayer player) { }

    public void DrawAngle(TurnPlayer player) { }

    public void DrawPower(TurnPlayer player) { }

    public void DrawDirection(TurnPlayer player) { }

    public void ShowTankInfo(TurnPlayer shown, IReadOnlyList<TurnPlayer> players) { }

    public void CloseTankInfo() { }

    public void ShowTargetBar(TurnPlayer shooter, bool mouse) { }

    public void DrawTarget(TurnPlayer shooter, TurnPlayer target) { }

    public void CloseTargetBar() { }

    public void ShowMirvPage(int page) { }

    public void CloseMirv() { }
}
