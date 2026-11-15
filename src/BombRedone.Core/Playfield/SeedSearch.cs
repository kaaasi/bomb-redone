using BombRedone.Core.Numerics;

namespace BombRedone.Core.Playfield;

/// <summary>What a picture of the original shows about a playfield (all parts optional).</summary>
/// <param name="TankX">x of the tanks from left to right (record +0Fh of players 1..N).</param>
/// <param name="TerrainTop">Pairs (x, y) of the topmost terrain pixel of a column (colour 2), e.g. read from a screenshot.</param>
public sealed record PlayfieldTarget(IReadOnlyList<int>? TankX = null, IReadOnlyList<(int X, int Y)>? TerrainTop = null)
{
    public bool Matches(Playfield field)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (TankX is not null && !TankX.SequenceEqual(field.Tanks.Select(t => t.X)))
            return false;
        if (TerrainTop is not null)
        {
            foreach (var (x, y) in TerrainTop)
            {
                if (Playfield.TerrainTop(field, x) != y)
                    return false;
            }
        }

        return true;
    }
}

/// <summary>A start value that reproduces the target.</summary>
/// <param name="StepsBeforeShuffle">Random calls between Randomize and the shuffle (menu, intelligences: runde.md section 4).</param>
/// <param name="SeedBeforeShuffle">RandSeed before the shuffle.</param>
/// <param name="SeedAfterShuffle">RandSeed after the shuffle, the input of <see cref="Playfield.Build"/>.</param>
public readonly record struct SeedCandidate(int StepsBeforeShuffle, uint SeedBeforeShuffle, uint SeedAfterShuffle);

/// <summary>
/// Search for the start value of the random generator behind a picture of the original (R2-D3, docs/analyse/runde.md
/// section 4 "Vorschlag"): from RandSeed after Randomize try k = 0..K further steps (main menu, intelligences), then
/// the steps of the shuffle and the set-up of the playfield, and compare with the picture.
/// </summary>
public static class SeedSearch
{
    /// <summary>
    /// Advances <paramref name="random"/> like the shuffle O3:2417 (runde.md section 1, S3): Random(20) + 10 swaps, each
    /// a = Random(N) + 1 and b = Random(N) + 1 repeated until b ≠ a. Only the steps are taken; the records are not swapped
    /// (R2-D4 = A: the order of the records follows with the game sequence in round 3).
    /// </summary>
    /// <returns>Number of swaps.</returns>
    public static int SkipShuffle(TurboPascalRandom random, int players)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (players < 2)
            throw new ArgumentOutOfRangeException(nameof(players));
        var swaps = random.Next(20) + 10;
        for (var i = 0; i < swaps; i++)
        {
            var a = random.Next(players) + 1;
            int b;
            do
            {
                b = random.Next(players) + 1;
            }
            while (b == a);
        }

        return swaps;
    }

    /// <summary>All k in 0..<paramref name="maxSteps"/> whose playfield matches <paramref name="target"/>.</summary>
    public static IEnumerable<SeedCandidate> Search(uint seedAfterRandomize, int maxSteps, PlayfieldOptions options,
        PlayfieldTarget target)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSteps);
        var seed = seedAfterRandomize;
        for (var k = 0; k <= maxSteps; k++)
        {
            var random = new TurboPascalRandom(seed);
            SkipShuffle(random, options.Players);
            var afterShuffle = random.Seed;
            if (target.Matches(Playfield.Build(options, random)))
                yield return new SeedCandidate(k, seed, afterShuffle);
            seed = unchecked(seed * 0x08088405u + 1);
        }
    }

    /// <summary>
    /// Start value search over a time window (Review #123 P2-1): for every DOS time from <paramref name="from"/> to
    /// <paramref name="to"/> (hundredths, both included) RandSeed after Randomize, then k = 0..<paramref name="maxSteps"/>
    /// steps, the shuffle, the cheap <paramref name="prefilter"/> and only then the playfield against
    /// <paramref name="target"/>. Hundredths 0..99 are all tried, although DOS derives them from the 18.2 Hz tick.
    /// </summary>
    /// <param name="windOption">Wind option DS:E8A8 for the wind part of the prefilter.</param>
    public static IEnumerable<ClockCandidate> SearchWindow(DosTime from, DosTime to, int maxSteps, PlayfieldOptions options,
        int windOption, PlayfieldPrefilter prefilter, PlayfieldTarget target)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(prefilter);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(maxSteps);
        options.Validate();
        for (var ticks = from.Ticks; ticks <= to.Ticks; ticks++)
        {
            var time = DosTime.FromTicks(ticks);
            var seed = time.Seed;
            for (var k = 0; k <= maxSteps; k++)
            {
                var random = new TurboPascalRandom(seed);
                SkipShuffle(random, options.Players);
                var afterShuffle = random.Seed;
                if (prefilter.Passes(afterShuffle, options, windOption)
                    && target.Matches(Playfield.Build(options, new TurboPascalRandom(afterShuffle))))
                    yield return new ClockCandidate(time, new SeedCandidate(k, seed, afterShuffle));
                seed = unchecked(seed * 0x08088405u + 1);
            }
        }
    }
}

/// <summary>
/// Cheap checks of a seed after the shuffle, in the order the original calls Random (docs/analyse/runde.md section 4
/// no. 2–6), before the playfield is drawn: background number, height of the flat left margin, x of the first and the
/// last tank, wind value. Each part is optional.
/// </summary>
/// <param name="Background">Background shown (0..8).</param>
/// <param name="MarginHeight">Height h of the flat left margin (topmost terrain pixel at y = 190 − h for x = 11..).</param>
/// <param name="FirstTankX">x of tank 1 (Random(28) + 16).</param>
/// <param name="LastTankX">x of tank N (305 − Random(28)).</param>
/// <param name="WindValue">Wind value DS:E8A2 (needs the wind option).</param>
public sealed record PlayfieldPrefilter(
    int? Background = null,
    int? MarginHeight = null,
    int? FirstTankX = null,
    int? LastTankX = null,
    int? WindValue = null)
{
    // [ORIGINAL] Terrain thresholds O8:0EA6 / O8:0EC4, same constants as Playfield.DrawTerrain.
    private static readonly Real48 SlopeChange = Real48.FromHex("7ECDCCCCCC4C");
    private static readonly Real48 DirectionChange = Real48.FromHex("7DCDCCCCCC4C");

    /// <summary>
    /// Advances a copy of RandSeed like <see cref="Playfield.Build"/> and <see cref="Wind.Choose"/> without drawing and
    /// stops at the first part that differs. A seed that passes still needs the full comparison (frame of the pixels).
    /// </summary>
    public bool Passes(uint seedAfterShuffle, PlayfieldOptions options, int windOption)
    {
        ArgumentNullException.ThrowIfNull(options);
        var random = new TurboPascalRandom(seedAfterShuffle);
        if (options.Wall == PlayfieldOptions.RandomWall)
            random.Next(5);

        var number = options.Background;
        if (number == PlayfieldOptions.RandomBackground)
        {
            number = random.Next(9);
        }
        else if (number == PlayfieldOptions.UserBackground)
        {
            do
            {
                number = random.Next(9);
            }
            while (((1 << number) & options.BackgroundMask) <= 0);
        }

        if (Background is { } background && background != number)
            return false;
        SkipBackground(number, random);
        random.NextReal();

        // Terrain (O8:0BEC): Random(2), Random(150), two Random:Real per inner step.
        var direction = 1 - random.Next(2) * 2;
        var height = random.Next(150);
        if (MarginHeight is { } margin && margin != height)
            return false;
        if (FirstTankX is null && LastTankX is null && WindValue is null)
            return true;
        SkipTerrain(options.Players, direction, height, random);

        var first = random.Next(28) + 16;
        var last = 305 - random.Next(28);
        if (FirstTankX is { } x1 && x1 != first || LastTankX is { } xn && xn != last)
            return false;
        return WindValue is not { } wind || Wind.Choose(windOption, random).Value == wind;
    }

    /// <summary>Random calls of background 0..8 between its choice and the Random:Real of the rotation (runde.md section 3.3).</summary>
    internal static void SkipBackground(int number, TurboPascalRandom random)
    {
        var steps = number switch
        {
            1 => 3 * (random.Next(500) + 250),
            2 or 3 or 4 or 8 => 9,
            6 => 9 + 299 * 159,
            7 => 10,
            _ => 0,
        };
        for (var i = 0; i < steps; i++)
            random.Next(1);
    }

    /// <summary>The steps of Playfield.DrawTerrain without the columns: margins without Random, inside two Random:Real.</summary>
    private static void SkipTerrain(int players, int direction, int height, TurboPascalRandom random)
    {
        var x = 11;
        var slope = 0;
        var margin = 60 - (players - 2) * 7;
        while (x < 310)
        {
            if (margin + 11 <= x && 309 - margin >= x)
            {
                var sign = slope > 0 ? 1 : -1;
                switch (Math.Abs(slope))
                {
                    case 0:
                        x += 2;
                        break;
                    case 1:
                        x += 2;
                        height += sign;
                        break;
                    case 2:
                        x += 2;
                        height += 2 * sign;
                        break;
                    case 3:
                        x += 1;
                        height += 2 * sign;
                        break;
                    case 4:
                        height += 2 * sign;
                        break;
                }

                if (random.NextReal() < SlopeChange)
                    slope += direction;
                if (random.NextReal() < DirectionChange || Math.Abs(slope) > 4)
                    direction = -direction;
                slope = Math.Clamp(slope, -4, 4);
                if (height <= 20)
                    direction = 1;
                if (height >= 140)
                    direction = -1;
                if (height <= 3)
                    slope = 1;
                if (height >= 155)
                    slope = -1;
            }

            x++;
        }
    }
}

/// <summary>
/// Time of day as DOS returns it (INT 21h AH=2Ch) and Randomize stores it: RandSeed low word = hours · 256 + minutes,
/// high word = seconds · 256 + hundredths ([ORIGINAL] BOMB.EXE 0x15A7C, 12CE:16FC; TurboPascalRandom.FromClock).
/// </summary>
public readonly record struct DosTime(int Hour, int Minute, int Second, int Hundredths) : IComparable<DosTime>
{
    /// <summary>Hundredths since midnight.</summary>
    public int Ticks => ((Hour * 60 + Minute) * 60 + Second) * 100 + Hundredths;

    /// <summary>RandSeed after Randomize at this time.</summary>
    public uint Seed => ((uint)((Second << 8) | Hundredths) << 16) | (uint)((Hour << 8) | Minute);

    public static DosTime FromTicks(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ticks, 24 * 60 * 60 * 100);
        return new DosTime(ticks / 360000, ticks / 6000 % 60, ticks / 100 % 60, ticks % 100);
    }

    public int CompareTo(DosTime other) => Ticks.CompareTo(other.Ticks);

    public override string ToString() => $"{Hour:00}:{Minute:00}:{Second:00}.{Hundredths:00}";
}

/// <summary>A start value found in a time window.</summary>
public readonly record struct ClockCandidate(DosTime Time, SeedCandidate Candidate);
