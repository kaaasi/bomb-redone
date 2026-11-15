using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Playfield;

/// <summary>Set-up of one round: playfield, wind and start player.</summary>
public sealed record RoundSetup(Playfield Field, Wind Wind, StartPlayer Start);

/// <summary>
/// Rounds of a battle in the order of the game sequence O3:7847 (docs/analyse/runde.md sections 1 and 2): before every
/// round (one round per game of the battle, S1) the shuffle O3:2417, then the round start O4:44A7 with playfield, wind
/// and start player.
/// </summary>
/// <remarks>
/// [ORIGINAL] The shuffle swaps the records (<see cref="BattleRecords.Shuffle"/>, issue #71, R2-D4 = A), so the rounds as
/// start player (+72h, R4) move with the player and are kept for the whole battle (issue #111). [APPROX] The random calls
/// between two rounds (computer players) are missing until round 4.
/// </remarks>
public sealed class RoundSequence
{
    private readonly PlayfieldOptions _options;
    private readonly TurboPascalRandom _random;

    /// <summary>Rounds with the default records Player 1..N (keyboard).</summary>
    public RoundSequence(PlayfieldOptions options, TurboPascalRandom random)
        : this(options, DefaultRecords(options), random)
    {
    }

    /// <param name="records">The records of the battle; the shuffle reorders them.</param>
    public RoundSequence(PlayfieldOptions options, BattleRecords records, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(random);
        if (records.Count != options.Players)
            throw new ArgumentException("One record per player.", nameof(records));
        _options = options;
        Records = records;
        _random = random;
    }

    /// <summary>The records in their current order.</summary>
    public BattleRecords Records { get; }

    /// <summary>Rounds as start player (+72h) of records 1..N (index 0 = record 1), in the current record order.</summary>
    public IReadOnlyList<int> RoundsStarted => [.. Records.InOrder.Select(r => r.RoundsStarted)];

    /// <summary>Shuffle, playfield, wind and start player of the next round.</summary>
    public RoundSetup NextRound()
    {
        Records.Shuffle(_random);
        var field = Playfield.Build(_options, _random);
        var wind = Wind.Choose(_options.Wind, _random);
        var counters = Records.InOrder.Select(r => r.RoundsStarted).ToArray();
        var start = StartPlayer.Choose(counters, Enumerable.Repeat(true, _options.Players).ToArray(), _random);
        for (var i = 0; i < counters.Length; i++)
            Records.InOrder[i].RoundsStarted = counters[i];
        return new RoundSetup(field, wind, start);
    }

    private static BattleRecords DefaultRecords(PlayfieldOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new BattleRecords(Enumerable.Range(1, options.Players).Select(n =>
            new BattlePlayer(new PlayerAccount(n, PlayerSlot.DefaultName(n)), PlayerControl.Keyboard, null, n)));
    }
}
