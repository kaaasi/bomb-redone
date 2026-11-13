namespace BombRedone.Core.Playfield;

/// <summary>Wall of a round (DS:47EC, docs/analyse/runde.md section 3.1).</summary>
public enum WallKind
{
    None = 0,
    Sticky = 1,
    Elastic = 2,
    Accelerate = 3,
    Warp = 4,
}

/// <summary>
/// Options of the game that go into the set-up of a playfield (docs/analyse/runde.md sections 2 and 3). Numbers as in
/// BOMB.CFG and the option variables: wall DS:E8AA (0..4, 5 = random), background DS:E8AC (0..8, 9 = random,
/// 10 = random from <see cref="BackgroundMask"/> DS:E8AE), wind DS:E8A8 (0..4, 5 = random), invisible shots DS:E8B4.
/// </summary>
public sealed record PlayfieldOptions(
    int Players,
    int Wall = 0,
    int Background = 0,
    int BackgroundMask = 0xFFFF,
    int Wind = 0,
    bool InvisibleShots = false)
{
    public const int RandomWall = 5;
    public const int RandomBackground = 9;
    public const int UserBackground = 10;
    public const int RandomWind = 5;

    /// <summary>Checks the ranges the original accepts (docs/formate/konfiguration.md).</summary>
    public void Validate()
    {
        if (Players is < 2 or > GameLimits.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(Players));
        if (Wall is < 0 or > RandomWall)
            throw new ArgumentOutOfRangeException(nameof(Wall));
        if (Background is < 0 or > UserBackground)
            throw new ArgumentOutOfRangeException(nameof(Background));
        if (Background == UserBackground && (BackgroundMask & 0x1FF) == 0)
            throw new ArgumentException("USER needs at least one of the backgrounds 0..8 in the mask.", nameof(BackgroundMask));
        if (Wind is < 0 or > RandomWind)
            throw new ArgumentOutOfRangeException(nameof(Wind));
    }
}
