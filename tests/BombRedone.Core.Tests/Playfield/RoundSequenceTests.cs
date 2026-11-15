using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>
/// Rounds of a battle (issue #111, review #108 P3-3): shuffle before every round (runde.md section 1, S1/S3), start
/// player with the rounds as start player (+72h) kept over the battle (section 2, R4).
/// </summary>
public sealed class RoundSequenceTests
{
    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Theory]
    [InlineData(0x12345678u, 2)]
    [InlineData(0x0BADF00Du, 3)]
    [InlineData(0xCAFEBABEu, 5)]
    public void Every_record_starts_once_before_any_starts_twice(uint seed, int players)
    {
        var rounds = new RoundSequence(new PlayfieldOptions(players, Wind: 2), new TurboPascalRandom(seed));

        var starters = new HashSet<int>();
        for (var round = 0; round < players; round++)
        {
            var start = rounds.NextRound().Start;
            // The move goes to the record after the chosen one; the chosen one is the one whose counter went up.
            Assert.True(starters.Add(start.Player == 1 ? players : start.Player - 1));
        }

        Assert.All(rounds.RoundsStarted, n => Assert.Equal(1, n));
    }

    [Fact]
    public void Rounds_follow_shuffle_playfield_wind_and_start_player_with_kept_counters()
    {
        var options = new PlayfieldOptions(3, Wall: 5, Background: 9, Wind: 5);
        var rounds = new RoundSequence(options, new TurboPascalRandom(0x9E3779B9));
        var random = new TurboPascalRandom(0x9E3779B9);
        var counters = new int[3];

        for (var round = 0; round < 6; round++)
        {
            var got = rounds.NextRound();
            SeedSearch.SkipShuffle(random, 3);
            var field = Core.Playfield.Playfield.Build(options, random);
            var wind = Wind.Choose(options.Wind, random);
            var start = StartPlayer.Choose(counters, [true, true, true], random);

            Assert.Equal(Sha(field.Screen.Pixels), Sha(got.Field.Screen.Pixels));
            Assert.Equal((wind, start), (got.Wind, got.Start));
            Assert.Equal(counters, rounds.RoundsStarted);
        }
    }
}
