namespace BombRedone.Audio;

/// <summary>
/// One tone of a sound effect: frequency and duration. Placeholder type of the audio layer; sound output
/// follows after round 1 (decision E5 = C, user, 2026-11-11).
/// </summary>
public readonly record struct Tone
{
    public Tone(int frequencyHz, int durationMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frequencyHz);
        ArgumentOutOfRangeException.ThrowIfNegative(durationMs);
        FrequencyHz = frequencyHz;
        DurationMs = durationMs;
    }

    /// <summary>Frequency in hertz; 0 is a pause.</summary>
    public int FrequencyHz { get; }

    public int DurationMs { get; }

    public bool IsPause => FrequencyHz == 0;
}
