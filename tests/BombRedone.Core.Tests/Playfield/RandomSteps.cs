namespace BombRedone.Core.Tests.Playfield;

/// <summary>Counts the steps RandSeed := RandSeed · 08088405h + 1 between two seeds (docs/analyse/re-grundlagen.md D).</summary>
internal static class RandomSteps
{
    public static int Between(uint from, uint to, int limit = 1_000_000)
    {
        var seed = from;
        for (var steps = 0; steps <= limit; steps++)
        {
            if (seed == to)
                return steps;
            seed = unchecked(seed * 0x08088405u + 1);
        }

        return -1;
    }
}
