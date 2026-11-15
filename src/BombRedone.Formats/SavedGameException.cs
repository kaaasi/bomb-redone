namespace BombRedone.Formats;

/// <summary>Why a saved game could not be read.</summary>
public enum SavedGameError
{
    /// <summary>The 30-byte header differs (the original reports "Save File is not valid!").</summary>
    InvalidHeader,

    /// <summary>The file ends before the last part the original reads.</summary>
    TooShort,

    /// <summary>The player count is outside 1..10.</summary>
    PlayerCountOutOfRange,
}

/// <summary>A saved game that the reader does not accept.</summary>
public sealed class SavedGameException(SavedGameError error, string message) : FormatException(message)
{
    public SavedGameError Error { get; } = error;
}
