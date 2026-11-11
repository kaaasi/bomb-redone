namespace BombRedone.Core.Setup;

/// <summary>
/// Who controls a player: the word array DS:E5A6 (index = player number) of the original.
/// [ORIGINAL] O2:0216 (BOMB.OVR 0x007FE) sets 1 for every player; O2:509C (BOMB.OVR 0x05684) sets 0 for players above
/// the player count; the player selection O2:52DD sets 1, 2 or 3 (BOMB.OVR 0x05F40, 0x05F8C, 0x05FD8).
/// </summary>
public enum PlayerControl
{
    /// <summary>Not playing (players above the count chosen in the main menu).</summary>
    None = 0,

    /// <summary>"KEYBOARD".</summary>
    Keyboard = 1,

    /// <summary>"MOUSE".</summary>
    Mouse = 2,

    /// <summary>"COMPUTER".</summary>
    Computer = 3,
}
