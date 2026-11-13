using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>Wind of a round: level DS:47E4 (0..4) and value DS:E8A2.</summary>
public readonly record struct Wind(int Level, int Value)
{
    /// <summary>[ORIGINAL] O4:44E5: span S of the levels 0..4.</summary>
    public static readonly IReadOnlyList<int> Spans = [0, 20, 100, 300, 4000];

    // [ORIGINAL] O4:455C–45C3: thresholds 0.1, 0.1, 0.05, 0.05 (Real48 constants, slightly above the decimal value).
    private static readonly Real48[] Thresholds =
    [
        Real48.FromHex("7DCDCCCCCC4C"), Real48.FromHex("7DCDCCCCCC4C"),
        Real48.FromHex("7CCDCCCCCC4C"), Real48.FromHex("7CCDCCCCCC4C"),
    ];

    /// <summary>
    /// [ORIGINAL] O4:44CC–45F1 (docs/analyse/runde.md section 2, R2, R3): level = option or Random(5); w = S div 2 −
    /// Random(S) until |w| ≥ S div 10; four times Random:Real against 0.1/0.1/0.05/0.05, each hit w := w · 4 in
    /// 16 bits (overflow without error, #98); then clamped to −30000..30000.
    /// </summary>
    public static Wind Choose(int option, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (option is < 0 or > PlayfieldOptions.RandomWind)
            throw new ArgumentOutOfRangeException(nameof(option));
        var level = option == PlayfieldOptions.RandomWind ? random.Next(5) : option;
        var span = Spans[level];
        short value;
        do
        {
            var r = random.Next(span);
            value = (short)(span / 2 - r);
        }
        while (Math.Abs((int)value) < span / 10);

        foreach (var threshold in Thresholds)
        {
            if (random.NextReal() < threshold)
                value = unchecked((short)(value << 2));
        }

        value = Math.Clamp(value, (short)-30000, (short)30000);
        return new Wind(level, value);
    }
}

/// <summary>Who moves first: record number DS:47EE and pass counter DS:47EA.</summary>
public readonly record struct StartPlayer(int Player, int Pass)
{
    /// <summary>
    /// [ORIGINAL] O4:46FF–4799 (docs/analyse/runde.md section 2, R4, R5): s = Random(N) until record s + 1 has started no
    /// more rounds (+72h) than any other; its counter + 1, pass 1; the first move goes to the next active record after
    /// s (s + 1, wrapping to 1 with pass + 1, skipping records with +23h = 0).
    /// </summary>
    /// <param name="roundsStarted">+72h of records 1..N (index 0 = record 1); the chosen entry is incremented.</param>
    /// <param name="active">+23h of records 1..N; at least one must be true.</param>
    public static StartPlayer Choose(IList<int> roundsStarted, IReadOnlyList<bool> active, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(roundsStarted);
        ArgumentNullException.ThrowIfNull(active);
        ArgumentNullException.ThrowIfNull(random);
        var n = roundsStarted.Count;
        if (n < 1 || active.Count != n || !active.Contains(true))
            throw new ArgumentException("Needs one entry per record and an active record.", nameof(active));

        int s;
        bool fewest;
        do
        {
            s = random.Next(n);
            fewest = true;
            for (var j = 0; j < n; j++)
            {
                if (roundsStarted[j] < roundsStarted[s])
                    fewest = false;
            }
        }
        while (!fewest);

        roundsStarted[s]++;
        var pass = 1;
        var player = s;
        do
        {
            player++;
            if (player > n)
            {
                player = 1;
                pass++;
            }
        }
        while (!active[player - 1]);

        return new StartPlayer(player, pass);
    }
}
