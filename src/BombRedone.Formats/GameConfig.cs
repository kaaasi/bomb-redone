using System.Buffers.Binary;

namespace BombRedone.Formats;

/// <summary>
/// Contents of <c>BOMB.CFG</c> (release 3.2), read only (decision R1-D3 = A). Layout and field names:
/// <c>docs/formate/konfiguration.md</c>.
/// </summary>
/// <remarks>
/// [ORIGINAL] Field order and sizes follow the loader in BOMB.OVR 0x6BF9 (overlay segment at file offset 0x5E8,
/// routine at segment offset 0x6611): one BlockRead per field, booleans 1 byte, numbers 2 bytes little endian.
/// The writer in BOMBCFG.EXE 0x6B86 (segment 0, offset 0x4E66) uses the same 28 sizes in the same order.
/// Property names follow the debug dump of BOMB.EXE (option "DEBUG", BOMB.OVR 0x79AE ff.), e.g. "Effsound",
/// "Crumbper". The loader checks only the version tag; it does not check any value range.
/// </remarks>
public sealed record GameConfig
{
    /// <summary>Number of bytes BOMB.EXE reads. [ORIGINAL] Sum of the 28 BlockRead sizes, BOMB.OVR 0x6D36–0x7002.</summary>
    public const int Size = 45;

    /// <summary>
    /// Version tag at offset 0: Turbo Pascal string[4] with length 4 and contents "3.2" + NUL.
    /// [ORIGINAL] BOMB.EXE 0x16446 (data segment offset 0x56), BOMBCFG.EXE 0x19EEE (offset 0x11E);
    /// compared in BOMB.OVR 0x6D3B.
    /// </summary>
    public static ReadOnlySpan<byte> VersionTag => [4, (byte)'3', (byte)'.', (byte)'2', 0];

    /// <summary>"Effsound" – sound effects on (S+/S-). Offset 5, 1 byte. [ORIGINAL] BOMB.OVR 0x6D92.</summary>
    public bool SoundEffects { get; init; }

    /// <summary>"Flysound" – sound while a shot flies (F+/F-). Offset 6, 1 byte. [ORIGINAL] BOMB.OVR 0x6DAA.</summary>
    public bool FlySound { get; init; }

    /// <summary>"WindControl" (Wn). Offset 7, 2 bytes. [ORIGINAL] BOMB.OVR 0x6DC2.</summary>
    public WindSetting Wind { get; init; }

    /// <summary>"Crumbper" – crumble percentage (Cnnn). Offset 9, 2 bytes. [ORIGINAL] BOMB.OVR 0x6DDA.</summary>
    public int CrumblePercent { get; init; }

    /// <summary>"Wallcontrol" (Rn). Offset 11, 2 bytes. [ORIGINAL] BOMB.OVR 0x6DF2.</summary>
    public WallSetting Wall { get; init; }

    /// <summary>"Inviso" – shot invisible in flight (I+/I-). Offset 13, 1 byte. [ORIGINAL] BOMB.OVR 0x6E0A.</summary>
    public bool InvisibleShot { get; init; }

    /// <summary>"Trail" – shot leaves a trace (T+/T-). Offset 14, 1 byte. [ORIGINAL] BOMB.OVR 0x6E22.</summary>
    public bool ShotTrail { get; init; }

    /// <summary>"GamesToPlay" (Gnn). Offset 15, 2 bytes. [ORIGINAL] BOMB.OVR 0x6E3A.</summary>
    public int GamesToPlay { get; init; }

    /// <summary>"FallDown" – tanks fall (D+/D-). Offset 17, 1 byte. [ORIGINAL] BOMB.OVR 0x6E52.</summary>
    public bool TanksFall { get; init; }

    /// <summary>
    /// "BackControl" (BKx): 0 none, 1..8 fixed background, 9 random, 10 user selection. Offset 18, 2 bytes.
    /// [ORIGINAL] BOMB.OVR 0x6E6A; meaning of the values: <see cref="BackgroundKind"/>.
    /// </summary>
    public int Background { get; init; }

    /// <summary>
    /// Backgrounds allowed for the user selection (BKU): bit n set = background n may be chosen (n = 0..8 from
    /// Random(9)). Offset 20, 2 bytes. No command line parameter and no name in the debug dump.
    /// [ORIGINAL] BOMB.OVR 0x6E82 (read), 0x1CB18–0x1CB33 (Random(9) repeated until the bit is set).
    /// </summary>
    public ushort UserBackgroundMask { get; init; }

    /// <summary>
    /// "ForceMouse" – mouse setting given explicitly; M+ and M- both set it. Offset 22, 1 byte.
    /// [ORIGINAL] BOMB.OVR 0x6E9A; 0x75C2/0x75DD (M-/M+ store 1).
    /// </summary>
    public bool ForceMouse { get; init; }

    /// <summary>"Mousein" – mouse enabled (M+/M-). Offset 23, 1 byte. [ORIGINAL] BOMB.OVR 0x6EB2.</summary>
    public bool MouseEnabled { get; init; }

    /// <summary>"Fastpal" – direct video port access (DV+/DV-, older H+/H-). Offset 24, 1 byte. [ORIGINAL] BOMB.OVR 0x6ECA.</summary>
    public bool DirectVideo { get; init; }

    /// <summary>"Xkey" – extended keyboard (X+/X-). Offset 25, 1 byte. [ORIGINAL] BOMB.OVR 0x6EE2.</summary>
    public bool ExtendedKeyboard { get; init; }

    /// <summary>"Palette" – palette rotation (P+/P-). Offset 26, 1 byte. [ORIGINAL] BOMB.OVR 0x6EFA.</summary>
    public bool PaletteRotation { get; init; }

    /// <summary>"CrudeCycle" – crude video manipulation (CV+/CV-, CRUDE/NOCRUDE). Offset 27, 1 byte. [ORIGINAL] BOMB.OVR 0x6F12.</summary>
    public bool CrudeVideo { get; init; }

    /// <summary>"Pspeed" – palette speed (0 fastest); no command line parameter. Offset 28, 2 bytes. [ORIGINAL] BOMB.OVR 0x6F2A.</summary>
    public int PaletteSpeed { get; init; }

    /// <summary>"CompuBuy" – computers can buy weapons (B+/B-). Offset 30, 1 byte. [ORIGINAL] BOMB.OVR 0x6F42.</summary>
    public bool ComputersBuy { get; init; }

    /// <summary>"TurretSpeed" (TSnn). Offset 31, 2 bytes. [ORIGINAL] BOMB.OVR 0x6F5A.</summary>
    public int TurretSpeed { get; init; }

    /// <summary>"FallSpeed" (FSnn). Offset 33, 2 bytes. [ORIGINAL] BOMB.OVR 0x6F72.</summary>
    public int FallSpeed { get; init; }

    /// <summary>"ExplodeSpeed" (ESnn). Offset 35, 2 bytes. [ORIGINAL] BOMB.OVR 0x6F8A.</summary>
    public int ExplosionSpeed { get; init; }

    /// <summary>"ShotSpeed" (SSnn). Offset 37, 2 bytes. [ORIGINAL] BOMB.OVR 0x6FA2.</summary>
    public int ShotSpeed { get; init; }

    /// <summary>"LaserSpeed" (LSnn). Offset 39, 2 bytes. [ORIGINAL] BOMB.OVR 0x6FBA.</summary>
    public int LaserSpeed { get; init; }

    /// <summary>
    /// "Video Interrupt" – palette rotation uses the video retrace interrupt (PV, true) or the timer interrupt
    /// (PT, false). Offset 41, 1 byte. [ORIGINAL] BOMB.OVR 0x6FD2; 0x76D4/0x76EF (PV stores 1, PT stores 0).
    /// </summary>
    public bool PaletteUsesRetrace { get; init; }

    /// <summary>"Team Play" (TEAM). Offset 42, 1 byte. [ORIGINAL] BOMB.OVR 0x6FEA.</summary>
    public bool TeamPlay { get; init; }

    /// <summary>
    /// "Boss Screen Memory Required" as stored, in KiB. Offset 43, 2 bytes. [ORIGINAL] BOMB.OVR 0x7002.
    /// Effective value: <see cref="BossScreenMemoryBytes"/>.
    /// </summary>
    public ushort BossScreenMemoryKiB { get; init; }

    /// <summary>
    /// Bytes BOMB.EXE uses after loading: 16-bit shift left by 10, then capped at 64000 (0xFA00).
    /// [ORIGINAL] BOMB.OVR 0x7014–0x702C (shl ax,10; cmp 0xFA00; jna).
    /// </summary>
    public int BossScreenMemoryBytes => Math.Min((int)(ushort)(BossScreenMemoryKiB << 10), 0xFA00);

    /// <summary>Classification of <see cref="Background"/>.</summary>
    public BackgroundKind BackgroundKind => Background switch
    {
        // [ORIGINAL] BOMB.OVR 0x1CAED: every value <= 8 (signed) selects background n; 0 is "none".
        0 => BackgroundKind.None,
        <= 8 => BackgroundKind.Fixed,
        9 => BackgroundKind.Random,
        10 => BackgroundKind.User,
        _ => BackgroundKind.Unknown,
    };

    /// <summary>Reads a <c>BOMB.CFG</c> file.</summary>
    public static GameConfig Load(string path) => Read(File.ReadAllBytes(path));

    /// <summary>
    /// Reads the 45 bytes of a release 3.2 <c>BOMB.CFG</c>. Bytes beyond 45 are ignored like in the original,
    /// which reads exactly 45 bytes and closes the file ([ORIGINAL] BOMB.OVR 0x7032).
    /// </summary>
    /// <exception cref="GameConfigException">Fewer than 45 bytes, or a version tag other than 3.2.</exception>
    public static GameConfig Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
        {
            // [APPROX] The original does not check the length: BlockRead runs with $I- and without a result
            // variable (BOMB.OVR 0x6D92 ff.), so a short file leaves fields unread instead of failing. Rejecting
            // it is the safer import behaviour.
            throw new GameConfigException(GameConfigError.TooShort,
                $"BOMB.CFG too short: {data.Length} bytes, release 3.2 needs {Size}.");
        }

        if (!data[..5].SequenceEqual(VersionTag))
        {
            // [ORIGINAL] BOMB.OVR 0x6D3B: string compare with the constant; on mismatch BOMB.EXE prints
            // "An invalid BOMB.CFG has been found." and halts. Older tags (2.5, 3.0) are converted by BOMBCFG.EXE only.
            throw new GameConfigException(GameConfigError.WrongVersion,
                $"BOMB.CFG version tag {Convert.ToHexString(data[..5])} is not release 3.2 ({Convert.ToHexString(VersionTag)}).");
        }

        return new GameConfig
        {
            SoundEffects = Flag(data, 5),
            FlySound = Flag(data, 6),
            Wind = (WindSetting)Integer(data, 7),
            CrumblePercent = Integer(data, 9),
            Wall = (WallSetting)Integer(data, 11),
            InvisibleShot = Flag(data, 13),
            ShotTrail = Flag(data, 14),
            GamesToPlay = Integer(data, 15),
            TanksFall = Flag(data, 17),
            Background = Integer(data, 18),
            UserBackgroundMask = Word(data, 20),
            ForceMouse = Flag(data, 22),
            MouseEnabled = Flag(data, 23),
            DirectVideo = Flag(data, 24),
            ExtendedKeyboard = Flag(data, 25),
            PaletteRotation = Flag(data, 26),
            CrudeVideo = Flag(data, 27),
            PaletteSpeed = Integer(data, 28),
            ComputersBuy = Flag(data, 30),
            TurretSpeed = Integer(data, 31),
            FallSpeed = Integer(data, 33),
            ExplosionSpeed = Integer(data, 35),
            ShotSpeed = Integer(data, 37),
            LaserSpeed = Integer(data, 39),
            PaletteUsesRetrace = Flag(data, 41),
            TeamPlay = Flag(data, 42),
            BossScreenMemoryKiB = Word(data, 43),
        };
    }

    // [APPROX] Turbo Pascal Boolean; how BOMB.EXE treats values other than 0 and 1 is not traced.
    private static bool Flag(ReadOnlySpan<byte> data, int offset) => data[offset] != 0;

    // [ORIGINAL] Turbo Pascal Integer (signed): the debug dump sign-extends these fields with cwd before
    // Write(LongInt) (BOMB.OVR 0x79AE ff., e.g. 0x7A47 mov ax,[0xe8a8]; cwd), and the round setup compares
    // BackControl signed (BOMB.OVR 0x1CAED cmp word [0xe8ac],8; jg).
    private static short Integer(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);

    // Unsigned word: BossScreenMemoryKiB is compared unsigned ([ORIGINAL] BOMB.OVR 0x7024 jna); for
    // UserBackgroundMask only single bits 0..8 are tested (BOMB.OVR 0x1CB2B–0x1CB33), so the sign does not matter.
    private static ushort Word(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
}
