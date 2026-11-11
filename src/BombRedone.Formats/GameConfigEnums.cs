namespace BombRedone.Formats;

/// <summary>
/// Wind setting (field <c>WindControl</c>). [ORIGINAL] BOMB.DOC, command line parameter "Wn" (0..5);
/// BOMB.OVR 0x7255 (command line "W" stores the number).
/// </summary>
public enum WindSetting
{
    None = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Gale = 4,
    Random = 5,
}

/// <summary>
/// Wall behaviour (field <c>Wallcontrol</c>). [ORIGINAL] BOMB.DOC, command line parameter "Rn" (0..5);
/// BOMB.OVR 0x72C6 ("RR" stores 5), 0x72AF ("R+" stores 2 = Elastic, formerly BOUNCY).
/// </summary>
public enum WallSetting
{
    None = 0,
    Sticky = 1,
    Elastic = 2,
    Accelerate = 3,
    Warp = 4,
    Random = 5,
}

/// <summary>How the background of a round is chosen, derived from <see cref="GameConfig.Background"/>.</summary>
public enum BackgroundKind
{
    /// <summary>Value 0: no background ("BKN", "BK-"). [ORIGINAL] BOMB.OVR 0x71EA.</summary>
    None,

    /// <summary>
    /// Values 1..8: always background n ("BKA".."BKH" store letter - 0x40). [ORIGINAL] BOMB.OVR 0x71AD–0x71C3.
    /// Negative values land here too, because the round setup compares signed ([ORIGINAL] BOMB.OVR 0x1CAED jg).
    /// </summary>
    Fixed,

    /// <summary>Value 9: random background each round ("BKR", "BK+"). [ORIGINAL] BOMB.OVR 0x720F, 0x1CAFC.</summary>
    Random,

    /// <summary>
    /// Value 10: random among the backgrounds selected in <see cref="GameConfig.UserBackgroundMask"/> ("BKU").
    /// [ORIGINAL] BOMB.OVR 0x7226, 0x1CB11–0x1CB33.
    /// </summary>
    User,

    /// <summary>Any other value; the loader does not check the range. [ORIGINAL] BOMB.OVR 0x6E6A (read without check).</summary>
    Unknown,
}
