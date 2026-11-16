using BombRedone.Core.Shot;

namespace BombRedone.Core.Round;

/// <summary>
/// Options of the round loop. <paramref name="Crumble"/> is the option Crumble (DS:E8A4, percent), <paramref name="FallDown"/>
/// the option "Fall down" (DS:E8B8), <paramref name="Sound"/> DS:E8B3 (the pictures then draw extra Random numbers).
/// </summary>
public sealed record RoundSettings(int SubSteps, int Wind, int Crumble, bool FallDown, bool Sound = false, bool Trail = false,
    Action<ShotFrame>? Frame = null)
{
    internal ShotSettings Shot => new(SubSteps, Wind, Trail, Sound, Frame, Crumble);
}

/// <summary>What the turn decided beyond the record fields of the tank (angle, power, weapon, guidance, target).</summary>
public readonly record struct TurnOutcome(MirvPattern Pattern = MirvPattern.Random, MirvDetonation Detonation = MirvDetonation.Impact);

/// <summary>Points of the round loop at which <see cref="RoundPlay.Play"/> reports (the measuring points of tod.py).</summary>
public enum RoundPointKind
{
    /// <summary>Z1 is done; the turn of <see cref="RoundPlay.ToMove"/> comes next (O4:4960, the call of O7:040C).</summary>
    TurnStart,

    /// <summary>The turn is decided (after Z1 and the turn, before the weapon; tod.py "turn", O4:4960).</summary>
    Turn,

    /// <summary>After the weapon, all tanks, Crumble and falling (tod.py "post", O4:4A0B).</summary>
    AfterShot,

    /// <summary>
    /// A record died: +23h is 0 and the name goes into the header (O4:4BB8–4C07); next comes Random(7) of the picture (O4:4C10,
    /// where tod.py --death-seed sets RandSeed).
    /// </summary>
    DeathStart,

    /// <summary>After one death (tod.py "death", O4:4D50).</summary>
    Death,

    /// <summary>After a lightning (tod.py "bolt", O4:4D9F).</summary>
    Lightning,

    /// <summary>The round goes on with the next turn (tod.py "next", O4:4E68).</summary>
    Continue,

    /// <summary>Round over (tod.py "end", return of O4:44A7).</summary>
    End,

    /// <summary>The turn source had no turn left (tod.py "exhausted").</summary>
    Exhausted,
}

/// <param name="Turn">0-based number of the turn in this round.</param>
/// <param name="Player">Victim of a death.</param>
/// <param name="Choice">Random(7) of a death (picture, <see cref="DeathExplosion"/>).</param>
public readonly record struct RoundPoint(RoundPointKind Kind, int Turn, int Player = 0, int Choice = -1);

/// <summary>
/// Shot log DS:4A80 (O4:4A0B–4B47): per record (7DAh bytes) entry +5Eh (the shot count after Z1) of 10 bytes: angle +1Dh,
/// power +1Bh, direction +1Fh, Trunc(x) of the shot state (32767 if x &gt; 32767), Trunc(y). The computers read it (round 4).
/// </summary>
public sealed class ShotLog
{
    /// <summary>Bytes per record (7DAh: 201 entries of 10 bytes).</summary>
    public const int RecordSize = 0x7DA;

    private readonly byte[] _bytes;

    public ShotLog(int players) => _bytes = new byte[RecordSize * players];

    /// <summary>The log of records 1..N as in memory from DS:4A80 + 7DAh (compared with the probe).</summary>
    public ReadOnlySpan<byte> Bytes => _bytes;

    internal void Write(int record, int entry, int angle, int power, int direction, int x, int y)
    {
        var at = (record - 1) * RecordSize + 10 * entry;
        Span<int> words = [angle, power, direction, x, y];
        for (var i = 0; i < words.Length; i++)
        {
            var a = at + 2 * i;
            if (a < 0 || a + 1 >= _bytes.Length)
                continue;  // [APPROX] entries past 200 would overwrite the next record or other data in the original
            _bytes[a] = (byte)words[i];
            _bytes[a + 1] = (byte)(words[i] >> 8);
        }
    }
}
