using System.Globalization;
using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Setup;

namespace BombRedone.Core.Tests.Battle;

/// <summary>
/// The game sequence O3:7847 against the probe (folge.py, kind "battle"): the round is a test double that sets alive,
/// score, wins and the winning team like the double of the probe; per game the order and RandSeed after the shuffle, the
/// records after the rating and the records that shop.
/// </summary>
public sealed class BattleProbeTests
{
    /// <summary>
    /// One battle. <paramref name="Rounds"/>: per game "winner;name:alive:score:wins;..." separated by "|";
    /// <paramref name="Expected"/>: per game "shuffled names;RandSeed;rated names;scores;wins;shop names" separated by "|".
    /// </summary>
    public sealed record BattleCase(int Index, int Players, int Games, string Seed, string Teams, string Rounds,
        string Expected)
    {
        public override string ToString() => $"{Index} battle {Players} players, {Games} games";
    }

    public static TheoryData<BattleCase> Cases
    {
        get
        {
            var data = new TheoryData<BattleCase>();
            foreach (var c in BattleProbeData.Battles)
                data.Add(c);
            return data;
        }
    }

    private sealed class DoubleRound(TurboPascalRandom random, string rounds) : IBattleRound
    {
        private readonly string[] _rounds = rounds.Split('|');

        public List<(string Names, string Seed)> Shuffled { get; } = [];

        public int Play(BattleRecords records, int game)
        {
            Shuffled.Add((string.Join(",", records.InOrder.Select(r => r.Name)),
                random.Seed.ToString("X8", CultureInfo.InvariantCulture)));
            var parts = _rounds[game - 1].Split(';');
            foreach (var part in parts.Skip(1))
            {
                var f = part.Split(':');
                var record = records.InOrder.Single(r => r.Name == f[0]);
                record.Alive = f[1] != "0";
                record.Account.Score += int.Parse(f[2], CultureInfo.InvariantCulture);
                record.Account.Wins += int.Parse(f[3], CultureInfo.InvariantCulture);
            }

            return int.Parse(parts[0], CultureInfo.InvariantCulture);
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Battle_matches_the_probe(BattleCase c)
    {
        var teams = c.Teams.Length == 0
            ? Enumerable.Range(1, c.Players).ToArray()
            : [.. c.Teams.Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture))];
        var records = new BattleRecords(Enumerable.Range(1, c.Players).Select(i =>
            new BattlePlayer(new PlayerAccount(i, PlayerSlot.DefaultName(i)), PlayerControl.Keyboard, null, teams[i - 1])));
        var random = new TurboPascalRandom(uint.Parse(c.Seed, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        var round = new DoubleRound(random, c.Rounds);
        var battle = new BattleSequence(records, c.Games, c.Teams.Length != 0, random, round, ItemCatalog.Original);
        var expected = c.Expected.Split('|');

        var step = battle.Start();
        for (var game = 1; game <= c.Games; game++)
        {
            var e = expected[game - 1].Split(';');
            Assert.Equal(new BattleStep(BattleStepKind.Rating, game), step);
            Assert.Equal((e[0], e[1]), round.Shuffled[game - 1]);
            Assert.Equal(e[2], string.Join(",", records.InOrder.Select(r => r.Name)));
            Assert.Equal(e[3], string.Join(",", records.InOrder.Select(r => r.Account.Score)));
            Assert.Equal(e[4], string.Join(",", records.InOrder.Select(r => r.Account.Wins)));

            step = battle.Continue();
            if (battle.TeamPlay)
            {
                Assert.Equal(new BattleStep(BattleStepKind.TeamRating, game), step);
                step = battle.Continue();
            }

            var shops = new List<string>();
            while (step.Kind == BattleStepKind.Shop)
            {
                shops.Add(records[step.Record].Name);
                step = battle.Continue();
            }

            Assert.Equal(e[5], string.Join(",", shops));
            Assert.Equal(new BattleStep(BattleStepKind.Wipe, game), step);
            step = battle.Continue();
        }

        Assert.Equal(new BattleStep(BattleStepKind.Finished, c.Games + 1), step);
        Assert.Equal(c.Games, round.Shuffled.Count);
    }
}
