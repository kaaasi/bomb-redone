using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>Set-up of one round: playfield, wind and start player.</summary>
public sealed record RoundSetup(Playfield Field, Wind Wind, StartPlayer Start);

/// <summary>
/// Rounds of a battle in the order of the game sequence O3:7847 (docs/analyse/runde.md sections 1 and 2): before every
/// round (one round per game of the battle, S1) the shuffle O3:2417, then the round start O4:44A7 with playfield, wind
/// and start player.
/// </summary>
/// <remarks>
/// [ORIGINAL] The rounds as start player (+72h, R4) are kept for the whole battle, so the start player rule sees the
/// earlier rounds (issue #111). [APPROX] The records are not swapped (R2-D4 = A), so the counters stay with the position
/// instead of the player; the random calls between two rounds (rating, shop, computer players) are missing until the game
/// sequence follows in round 3.
/// </remarks>
public sealed class RoundSequence
{
    private readonly PlayfieldOptions _options;
    private readonly TurboPascalRandom _random;
    private readonly int[] _roundsStarted;

    public RoundSequence(PlayfieldOptions options, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(random);
        _options = options;
        _random = random;
        _roundsStarted = new int[options.Players];
    }

    /// <summary>Rounds as start player (+72h) of records 1..N (index 0 = record 1).</summary>
    public IReadOnlyList<int> RoundsStarted => _roundsStarted;

    /// <summary>Shuffle steps, playfield, wind and start player of the next round.</summary>
    public RoundSetup NextRound()
    {
        SeedSearch.SkipShuffle(_random, _options.Players);
        var field = Playfield.Build(_options, _random);
        var wind = Wind.Choose(_options.Wind, _random);
        var start = StartPlayer.Choose(_roundsStarted, Enumerable.Repeat(true, _options.Players).ToArray(), _random);
        return new RoundSetup(field, wind, start);
    }
}
