namespace BombRedone.Core;

/// <summary>Fixed limits of the original game.</summary>
public static class GameLimits
{
    /// <summary>[ORIGINAL] BOMB.TXT, "DESCRIPTION": "This is a game for 1 to 10 players."</summary>
    public const int MinPlayers = 1;

    /// <summary>[ORIGINAL] BOMB.TXT, "DESCRIPTION": "This is a game for 1 to 10 players."</summary>
    public const int MaxPlayers = 10;

    /// <summary>[ORIGINAL] BOMB.TXT, "DESCRIPTION": "7 computer intelligences".</summary>
    public const int ComputerIntelligences = 7;

    public static bool IsValidPlayerCount(int players) => players is >= MinPlayers and <= MaxPlayers;
}
