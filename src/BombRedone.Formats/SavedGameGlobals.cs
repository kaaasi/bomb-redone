namespace BombRedone.Formats;

/// <summary>
/// The 26 global values of a saved game (file offset 30, 41 bytes). Names and evidence: <c>docs/formate/spielstand.md</c>,
/// table "Globale Werte".
/// </summary>
/// <remarks>
/// [ORIGINAL] Order and sizes of the BlockWrite calls in O3:3B54 (T2b, BOMB.OVR 0x0C6FC). Two-byte fields are read as
/// signed Turbo Pascal Integer except <see cref="UserBackgroundMask"/>; the options from <see cref="CrumblePercent"/> on are
/// the same variables the BOMB.CFG loader fills (T7d, BOMB.OVR 0x06BF9), with the meaning of <see cref="GameConfig"/>.
/// </remarks>
public sealed class SavedGameGlobals
{
    /// <summary>Size of the block. [ORIGINAL] T2b.</summary>
    public const int Size = 41;

    private readonly byte[] _raw;

    internal SavedGameGlobals(byte[] raw) => _raw = raw;

    /// <summary>The 41 bytes as stored.</summary>
    public ReadOnlyMemory<byte> Raw => _raw;

    /// <summary>DS:47D8, file offset 30: meaning not traced (used only in O6, set to 2, 4, 10, 15, 30, 60). Raw value.</summary>
    public short Unknown47D8 => SavedGame.Int16(_raw, 0);

    /// <summary>DS:47DA, offset 32: number of players, bound of the player loop. [ORIGINAL] T2c.</summary>
    public short PlayerCount => SavedGame.Int16(_raw, 2);

    /// <summary>DS:47E2, offset 34: current game of the battle (shop only while ≠ <see cref="GamesToPlay"/>). [ORIGINAL] T6a.</summary>
    public short CurrentGame => SavedGame.Int16(_raw, 4);

    /// <summary>DS:47E4, offset 36: wind setting of this round (option, Random(5) for Random). [ORIGINAL] T7a.</summary>
    public WindSetting RoundWind => (WindSetting)SavedGame.Int16(_raw, 6);

    /// <summary>DS:47E6, offset 38: helper value of the round (record +28h of a player, then a record index). [ORIGINAL] T7f.</summary>
    public short RoundPlayerHelper => SavedGame.Int16(_raw, 8);

    /// <summary>DS:47E8, offset 40: no direct access in the code; raw value.</summary>
    public short Unknown47E8 => SavedGame.Int16(_raw, 10);

    /// <summary>DS:47EA, offset 42: pass counter of the round (1, +1 per pass over all players). [ORIGINAL] T7c.</summary>
    public short RoundPass => SavedGame.Int16(_raw, 12);

    /// <summary>DS:47EC, offset 44: wall setting of this round (option, Random(5) for Random). [ORIGINAL] T7b.</summary>
    public WallSetting RoundWall => (WallSetting)SavedGame.Int16(_raw, 14);

    /// <summary>DS:47EE, offset 46 (byte): player whose turn it is. [ORIGINAL] T7c.</summary>
    public byte PlayerToMove => _raw[16];

    /// <summary>DS:4812, offset 47: all players are computers (START of the player selection). spielerauswahl.md §3.</summary>
    public bool AllComputers => SavedGame.Flag(_raw, 17);

    /// <summary>DS:4814, offset 48: wall of this round is 1, 2 or 3 (sticky, elastic, accelerating). [ORIGINAL] T7b.</summary>
    public bool RoundWallStopsOrBounces => SavedGame.Flag(_raw, 18);

    /// <summary>DS:4815, offset 49: wall of this round is 4 (warp). [ORIGINAL] T7b.</summary>
    public bool RoundWallWarps => SavedGame.Flag(_raw, 19);

    /// <summary>DS:E8A2, offset 50: wind value of the round, signed, −30000..30000; not in BOMB.CFG. [ORIGINAL] T7a, T7d.</summary>
    public short WindValue => SavedGame.Int16(_raw, 20);

    /// <summary>DS:E8A4, offset 52: crumble percent (<see cref="GameConfig.CrumblePercent"/>). [ORIGINAL] T7d.</summary>
    public short CrumblePercent => SavedGame.Int16(_raw, 22);

    /// <summary>DS:E8A6, offset 54: games per battle (<see cref="GameConfig.GamesToPlay"/>). [ORIGINAL] T7d.</summary>
    public short GamesToPlay => SavedGame.Int16(_raw, 24);

    /// <summary>DS:E8A8, offset 56: wind option (<see cref="GameConfig.Wind"/>). [ORIGINAL] T7d.</summary>
    public WindSetting Wind => (WindSetting)SavedGame.Int16(_raw, 26);

    /// <summary>DS:E8AA, offset 58: wall option (<see cref="GameConfig.Wall"/>). [ORIGINAL] T7d.</summary>
    public WallSetting Wall => (WallSetting)SavedGame.Int16(_raw, 28);

    /// <summary>DS:E8AC, offset 60: background option 0..10 (<see cref="GameConfig.Background"/>). [ORIGINAL] T7d.</summary>
    public short Background => SavedGame.Int16(_raw, 30);

    /// <summary>DS:E8AE, offset 62: background mask (<see cref="GameConfig.UserBackgroundMask"/>). [ORIGINAL] T7d.</summary>
    public ushort UserBackgroundMask => SavedGame.UInt16(_raw, 32);

    /// <summary>DS:E8B2, offset 64: sound while a shot flies. [ORIGINAL] T7d.</summary>
    public bool FlySound => SavedGame.Flag(_raw, 34);

    /// <summary>DS:E8B3, offset 65: sound effects. [ORIGINAL] T7d.</summary>
    public bool SoundEffects => SavedGame.Flag(_raw, 35);

    /// <summary>DS:E8B4, offset 66: shot invisible in flight (Inviso). [ORIGINAL] T7d.</summary>
    public bool InvisibleShot => SavedGame.Flag(_raw, 36);

    /// <summary>DS:E8B5, offset 67: shot leaves a trace. [ORIGINAL] T7d.</summary>
    public bool ShotTrail => SavedGame.Flag(_raw, 37);

    /// <summary>DS:E8B8, offset 68: tanks fall. [ORIGINAL] T7d.</summary>
    public bool TanksFall => SavedGame.Flag(_raw, 38);

    /// <summary>DS:E8BA, offset 69: computers can buy weapons. [ORIGINAL] T7d.</summary>
    public bool ComputersBuy => SavedGame.Flag(_raw, 39);

    /// <summary>DS:E8BC, offset 70: team play. [ORIGINAL] T7d.</summary>
    public bool TeamPlay => SavedGame.Flag(_raw, 40);
}
