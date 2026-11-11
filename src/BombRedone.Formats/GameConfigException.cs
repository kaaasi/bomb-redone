namespace BombRedone.Formats;

/// <summary>Why a <c>BOMB.CFG</c> could not be read.</summary>
public enum GameConfigError
{
    /// <summary>Fewer bytes than the 45 that BOMB.EXE reads.</summary>
    TooShort,

    /// <summary>The 5-byte version tag is not the one of release 3.2.</summary>
    WrongVersion,
}

/// <summary>A <c>BOMB.CFG</c> that the reader does not accept.</summary>
public sealed class GameConfigException(GameConfigError error, string message) : FormatException(message)
{
    public GameConfigError Error { get; } = error;
}
