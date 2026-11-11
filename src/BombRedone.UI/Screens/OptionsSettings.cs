using BombRedone.Formats;

namespace BombRedone.UI.Screens;

/// <summary>
/// The game settings the "Game Options" screen (O2:11CC) shows and changes. In the original these are global variables
/// that BOMB.CFG fills (loader O2:6611, BOMB.OVR 0x06BF9) and the rounds read later; here one object is shared by the
/// options screen and the screens that follow.
/// </summary>
/// <remarks>
/// Values are stored as the original stores them: numbers without range check (BOMB.CFG is not checked either), the
/// options screen limits them only when they are changed. [ORIGINAL] The defaults are the values O2:6611 sets before it
/// reads BOMB.CFG (BOMB.OVR 0x06C14–0x06CA4, offsets O2:662C–66BC given per property).
/// </remarks>
public sealed class OptionsSettings
{
    /// <summary>"Crumble Percent" (DS:E8A4, "Crumbper"); default 10 (O2:662C).</summary>
    public int CrumblePercent { get; set; } = 10;

    /// <summary>"Walls" (DS:E8AA, "Wallcontrol"): 0..5, names from O2:0F81; default 0 (O2:6634).</summary>
    public WallSetting Walls { get; set; }

    /// <summary>"Trail Visible" (DS:E8B5, "Trail"); default off (O2:6642).</summary>
    public bool TrailVisible { get; set; }

    /// <summary>"Inviso" (DS:E8B4): the screen shows the opposite as "Ball Visible"; default off (O2:663D).</summary>
    public bool InvisibleShot { get; set; }

    /// <summary>"Tanks Fall" (DS:E8B8, "FallDown"); default on (O2:664D).</summary>
    public bool TanksFall { get; set; } = true;

    /// <summary>"Computer Can Buy" (DS:E8BA, "CompuBuy"); default on (O2:6658).</summary>
    public bool ComputersBuy { get; set; } = true;

    /// <summary>"Background" (DS:E8AC, "BackControl"): 0..10, names from O2:0E35; default 0 (O2:665F).</summary>
    public int Background { get; set; }

    /// <summary>"Wind Force" (DS:E8A8, "WindControl"): 0..5, names from O2:1042; default 2 (O2:6662).</summary>
    public WindSetting Wind { get; set; } = WindSetting.Normal;

    /// <summary>"Team Play" (DS:E8BC); default off (O2:66BC).</summary>
    public bool TeamPlay { get; set; }

    /// <summary>
    /// The settings at program start: from BOMB.CFG, or without it the defaults of BOMBCFG.EXE
    /// (<see cref="GameConfig.BombCfgDefaults"/>, decision R1-D5 = C; the original halts without BOMB.CFG).
    /// </summary>
    public static OptionsSettings ForConfig(GameConfig? config) => FromConfig(config ?? GameConfig.BombCfgDefaults);

    /// <summary>The settings BOMB.CFG gives (same variables as the loader O2:6611 fills).</summary>
    public static OptionsSettings FromConfig(GameConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new OptionsSettings
        {
            CrumblePercent = config.CrumblePercent,
            Walls = config.Wall,
            TrailVisible = config.ShotTrail,
            InvisibleShot = config.InvisibleShot,
            TanksFall = config.TanksFall,
            ComputersBuy = config.ComputersBuy,
            Background = config.Background,
            Wind = config.Wind,
            TeamPlay = config.TeamPlay,
        };
    }

    /// <summary>[ORIGINAL] O2:0007 (BOMB.OVR 0x005EF): a non-zero byte is "YES", zero "NO".</summary>
    public static string YesNo(bool value) => value ? "YES" : "NO";

    /// <summary>
    /// [ORIGINAL] O2:0F81 (BOMB.OVR 0x01569), strings O2:0F61–0F7D. [APPROX] Other values leave the function result
    /// unset in the original (a string of undefined contents); here the empty string.
    /// </summary>
    public static string WallName(WallSetting value) => (int)value switch
    {
        0 => "NO",
        1 => "STICK",
        2 => "ELASTIC",
        3 => "ACCEL",
        4 => "WARP",
        5 => "RND",
        _ => "",
    };

    /// <summary>
    /// [ORIGINAL] O2:0E35 (BOMB.OVR 0x0141D), strings O2:0DEF–0E30. [APPROX] Other values: empty string (see
    /// <see cref="WallName"/>).
    /// </summary>
    public static string BackgroundName(int value) => value switch
    {
        0 => "None",
        1 => "Stars",
        2 => "Bars",
        3 => "Circles",
        4 => "Spread",
        5 => "Sky",
        6 => "Static",
        7 => "Rectangle",
        8 => "Asterisk",
        9 => "RND",
        10 => "USER",
        _ => "",
    };

    /// <summary>
    /// [ORIGINAL] O2:1042 (BOMB.OVR 0x0162A), strings O2:1026–103E. [APPROX] Other values: empty string (see
    /// <see cref="WallName"/>).
    /// </summary>
    public static string WindName(WindSetting value) => (int)value switch
    {
        0 => "None",
        1 => "Low",
        2 => "Norm",
        3 => "High",
        4 => "Gale",
        5 => "RND",
        _ => "",
    };
}
