using System.Security.Cryptography;
using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>
/// Rounds of a battle (issue #111, review #108 P3-3): shuffle before every round (runde.md section 1, S1/S3), start
/// player with the rounds as start player (+72h) kept over the battle (section 2, R4); the shuffle swaps the records, so
/// the counters move with the player (issue #71, R2-D4 = A).
/// </summary>
public sealed class RoundSequenceTests
{
    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Theory]
    [InlineData(0x12345678u, 2)]
    [InlineData(0x0BADF00Du, 3)]
    [InlineData(0xCAFEBABEu, 5)]
    public void Every_player_starts_once_before_any_starts_twice(uint seed, int players)
    {
        var rounds = new RoundSequence(new PlayfieldOptions(players, Wind: 2), new TurboPascalRandom(seed));

        var starters = new HashSet<int>();
        for (var round = 0; round < players; round++)
        {
            var start = rounds.NextRound().Start;
            // Random(N) = s chooses record s + 1, whose counter goes up; with all records active it also moves first.
            var chosen = rounds.Records[start.Player];
            Assert.True(starters.Add(chosen.Account.Number));
        }

        Assert.All(rounds.RoundsStarted, n => Assert.Equal(1, n));
    }

    [Fact]
    public void Rounds_follow_shuffle_playfield_wind_and_start_player_with_kept_counters()
    {
        var options = new PlayfieldOptions(3, Wall: 5, Background: 9, Wind: 5);
        var rounds = new RoundSequence(options, new TurboPascalRandom(0x9E3779B9));
        var random = new TurboPascalRandom(0x9E3779B9);
        var records = new BattleRecords(Enumerable.Range(1, 3).Select(n =>
            new BattlePlayer(new PlayerAccount(n, PlayerSlot.DefaultName(n)), PlayerControl.Keyboard, null, n)));
        var counters = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0 };

        for (var round = 0; round < 6; round++)
        {
            var got = rounds.NextRound();
            records.Shuffle(random);
            var field = Core.Playfield.Playfield.Build(options, random);
            var wind = Wind.Choose(options.Wind, random);
            var byRecord = records.InOrder.Select(r => counters[r.Account.Number]).ToArray();
            var start = StartPlayer.Choose(byRecord, [true, true, true], random);
            for (var i = 0; i < 3; i++)
                counters[records.InOrder[i].Account.Number] = byRecord[i];

            Assert.Equal(Sha(field.Screen.Pixels), Sha(got.Field.Screen.Pixels));
            Assert.Equal((wind, start), (got.Wind, got.Start));
            Assert.Equal(records.InOrder.Select(r => r.Account.Number), rounds.Records.InOrder.Select(r => r.Account.Number));
            Assert.Equal(byRecord, rounds.RoundsStarted);
        }
    }

    [Fact]
    public void Counters_move_with_the_player_when_the_shuffle_swaps_the_records()
    {
        // Issue #71: before, the counters stayed with the position while the shuffle only drew its random numbers.
        var options = new PlayfieldOptions(4, Wind: 2);
        var rounds = new RoundSequence(options, new TurboPascalRandom(0x12345678));

        rounds.NextRound();
        var chosen = rounds.Records.InOrder.Single(r => r.RoundsStarted == 1);
        var order = rounds.Records.InOrder.Select(r => r.Account.Number).ToArray();
        rounds.NextRound();

        Assert.NotEqual(order, rounds.Records.InOrder.Select(r => r.Account.Number));
        Assert.True(chosen.RoundsStarted >= 1);
        Assert.Equal(2, rounds.Records.InOrder.Sum(r => r.RoundsStarted));
    }
}
