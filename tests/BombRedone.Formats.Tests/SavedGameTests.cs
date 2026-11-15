using System.Security.Cryptography;
using BombRedone.Formats;
using static BombRedone.Formats.Tests.SavedGameSynthetic;

namespace BombRedone.Formats.Tests;

/// <summary>Reader of saved games with synthetic files (no original data).</summary>
public class SavedGameTests
{
    public static TheoryData<int> TwsPyCaseIndexes() => new(Enumerable.Range(0, TwsPyCases.Length));

    [Theory]
    [MemberData(nameof(TwsPyCaseIndexes))]
    public void BuilderMatchesTwsPy(int caseIndex)
    {
        var (players, computers, sha) = TwsPyCases[caseIndex];

        Assert.Equal(sha, Convert.ToHexStringLower(SHA256.HashData(Build(players, computers))));
    }

    [Theory]
    [MemberData(nameof(TwsPyCaseIndexes))]
    public void ReadsTwsPyCases(int caseIndex)
    {
        var (players, computers, _) = TwsPyCases[caseIndex];
        var data = Build(players, computers);

        var game = SavedGame.Read(data);

        Assert.Equal(SavedGame.SizeFor(players, computers.Length), data.Length);
        Assert.Equal(players, game.Globals.PlayerCount);
        Assert.Equal(players, game.Players.Count);
        for (var i = 1; i <= players; i++)
        {
            var player = game.Players[i - 1];
            Assert.Equal(i, player.Index);
            Assert.Equal(i, player.Record.Number);
            Assert.Equal($"P{i}", player.Record.Name);
            Assert.Equal(1000 * i, player.Record.Score);
            Assert.Equal(1000 * i, player.Record.Money);
            var computer = computers.Contains(i);
            Assert.Equal(computer, player.IsComputer);
            Assert.Equal(computer ? 3 : 1, player.Control);
            if (computer)
            {
                var ai = Assert.IsType<SavedGameIntelligence>(player.Intelligence);
                Assert.Equal(2, ai.Kind);
                Assert.Equal("Lobber", ai.KindName);
                Assert.Equal(0, ai.Unknown0B);
                Assert.Equal(1, ai.Choice);
                Assert.Equal(i, ai.PlayerNumber);
            }
            else
            {
                Assert.Null(player.Intelligence);
            }
        }

        Assert.Equal(SavedGameTeamTable.Count, game.Teams.Teams.Count);
        Assert.True(game.TrailingBytes.IsEmpty);
        Assert.Equal(data, game.ToBytes());
    }

    [Theory]
    [InlineData(2, 0, 890)]
    [InlineData(10, 0, 1834)]
    [InlineData(10, 10, 1904)]
    public void SizeFollowsFormula(int players, int computers, int expected) =>
        // [ORIGINAL] spielstand.md "Länge" = 654 + 118 × players + 7 × computers (T2b, T2c).
        Assert.Equal(expected, SavedGame.SizeFor(players, computers));

    [Fact]
    public void HeaderIsThirtyBytesWithVersionTag()
    {
        Assert.Equal(SavedGame.HeaderSize, SavedGame.Header.Length);
        // Version tag of BOMB.CFG without its length byte (T4).
        Assert.True(SavedGame.Header.Slice(23, 4).SequenceEqual(GameConfig.VersionTag[1..]));
    }

    [Fact]
    public void GlobalsAtDocumentedOffsets()
    {
        var data = Build(2);
        WriteInt16(data, 30, 10);
        WriteInt16(data, 34, 2);
        WriteInt16(data, 36, 5);
        WriteInt16(data, 38, 21280);
        WriteInt16(data, 40, -1);
        WriteInt16(data, 42, 3);
        WriteInt16(data, 44, 4);
        data[46] = 2;
        data[47] = 1;
        data[48] = 1;
        data[49] = 1;
        WriteInt16(data, 50, -92);
        WriteInt16(data, 52, 75);
        WriteInt16(data, 54, 10);
        WriteInt16(data, 56, 2);
        WriteInt16(data, 58, 3);
        WriteInt16(data, 60, 9);
        WriteInt16(data, 62, 0xFFFF);
        for (var o = 64; o <= 70; o++)
            data[o] = 1;

        var g = SavedGame.Read(data).Globals;

        Assert.Equal(10, g.Unknown47D8);
        Assert.Equal(2, g.CurrentGame);
        Assert.Equal(WindSetting.Random, g.RoundWind);
        Assert.Equal(21280, g.RoundPlayerHelper);
        Assert.Equal(-1, g.Unknown47E8);
        Assert.Equal(3, g.RoundPass);
        Assert.Equal(WallSetting.Warp, g.RoundWall);
        Assert.Equal(2, g.PlayerToMove);
        Assert.True(g.AllComputers && g.RoundWallStopsOrBounces && g.RoundWallWarps);
        Assert.Equal(-92, g.WindValue);
        Assert.Equal(75, g.CrumblePercent);
        Assert.Equal(10, g.GamesToPlay);
        Assert.Equal(WindSetting.Normal, g.Wind);
        Assert.Equal(WallSetting.Accelerate, g.Wall);
        Assert.Equal(9, g.Background);
        Assert.Equal(0xFFFF, g.UserBackgroundMask);
        Assert.True(g.FlySound && g.SoundEffects && g.InvisibleShot && g.ShotTrail && g.TanksFall && g.ComputersBuy && g.TeamPlay);
        Assert.Equal(data.AsSpan(30, SavedGame.GlobalsSize).ToArray(), g.Raw.ToArray());
    }

    [Fact]
    public void RecordFieldsAtDocumentedOffsets()
    {
        // Player 2 is a computer, so the record of player 3 starts 7 bytes later.
        var data = Build(3, 2);
        int At(int field) => RecordOffset(3, field, computersBefore: 1);
        WriteInt32(data, At(0x13), 4490);
        WriteInt32(data, At(0x17), 4000);
        WriteInt16(data, At(0x1D), 45);
        WriteInt16(data, At(0x1F), -1);
        data[At(0x23)] = 1;
        WriteInt16(data, At(0x28), 2);
        WriteInt16(data, At(0x2A), 3);
        WriteInt16(data, At(0x2C), 4);
        WriteInt16(data, At(0x2E), 99);
        WriteInt16(data, At(0x2E + 2 * 17), -5);
        WriteInt32(data, At(0x52), 100_000);
        WriteInt32(data, At(0x5A), 7);
        WriteInt16(data, At(0x60), 11);
        WriteInt16(data, At(0x64), 13);
        WriteInt16(data, At(0x66), 2);
        WriteInt16(data, At(0x6A), 6);

        var r = SavedGame.Read(data).Players[2].Record;

        Assert.Equal(4490, r.Score);
        Assert.Equal(4000, r.Spent);
        Assert.Equal(490, r.Money);
        Assert.Equal(45, r.TurretAngle);
        Assert.Equal(-1, r.TurretDirection);
        Assert.Equal(1, r.TakesTurns);
        Assert.Equal(2, r.OtherPlayer);
        Assert.Equal(3, r.Guidance(0));
        Assert.Equal(4, r.Guidance(1));
        Assert.Equal(99, r.Weapon(0));
        Assert.Equal(-5, r.Weapon(17));
        Assert.Equal(100_000, r.Defense(0));
        Assert.Equal(7, r.Defense(2));
        Assert.Equal(11, r.ColorDivisor(0));
        Assert.Equal(13, r.ColorDivisor(2));
        Assert.Equal(2, r.Team);
        Assert.Equal(6, r.GamesWon);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Weapon(18));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Guidance(-1));
    }

    [Fact]
    public void NameLongerThanCapacityIsCut()
    {
        var data = Build(2);
        var at = RecordOffset(1, 1);
        data[at] = 20;
        for (var k = 1; k <= 20; k++)
            data[at + k] = (byte)('A' + k - 1);

        Assert.Equal("ABCDEFGHIJKLM", SavedGame.Read(data).Players[0].Record.Name);
    }

    [Fact]
    public void TeamEntries()
    {
        var data = Build(2);
        var table = data.Length - SavedGameTeamTable.Size;
        var team1 = table + SavedGameTeam.Size;
        data[team1] = 6;
        "Team 1"u8.CopyTo(data.AsSpan(team1 + 1));
        data[team1 + 0x0E] = 1;
        WriteInt16(data, team1 + 0x0F, 2);

        var teams = SavedGame.Read(data).Teams.Teams;

        Assert.Equal(1, teams[1].Number);
        Assert.Equal("Team 1", teams[1].Name);
        Assert.True(teams[1].AllComputers);
        Assert.Equal(2, teams[1].MemberCount);
        Assert.Equal("", teams[0].Name);
        Assert.False(teams[0].AllComputers);
    }

    /// <summary>
    /// Unknown fields, stale memory in the team table (as in the reference files of session 3) and extra bytes after the
    /// table come back unchanged.
    /// </summary>
    [Fact]
    public void RoundTripKeepsAllBytes()
    {
        var data = Build(3, 2);
        var random = new Random(78);
        var noise = new byte[data.Length - SavedGame.HeaderSize];
        random.NextBytes(noise);
        noise.CopyTo(data, SavedGame.HeaderSize);
        WriteInt16(data, 32, 3);
        WriteInt16(data, RecordOffset(1, SavedGamePlayerRecord.Size), 1);
        WriteInt16(data, RecordOffset(2, SavedGamePlayerRecord.Size), 3);
        WriteInt16(data, RecordOffset(3, SavedGamePlayerRecord.Size, computersBefore: 1), 2);
        var extended = data.Concat(new byte[] { 1, 2, 3 }).ToArray();

        var game = SavedGame.Read(extended);

        Assert.Equal(extended, game.ToBytes());
        Assert.Equal([1, 2, 3], game.TrailingBytes.ToArray());
        Assert.Equal([1, 3, 2], game.Players.Select(p => (int)p.Control));
        Assert.Equal(data.AsSpan(data.Length - SavedGameTeamTable.Size).ToArray(), game.Teams.Raw.ToArray());
    }

    [Fact]
    public void RejectsOtherHeader()
    {
        var data = Build(2);
        data[25] = (byte)'0'; // "V3.0"

        var ex = Assert.Throws<SavedGameException>(() => SavedGame.Read(data));
        Assert.Equal(SavedGameError.InvalidHeader, ex.Error);
        Assert.Equal(SavedGameError.InvalidHeader, Assert.Throws<SavedGameException>(() => SavedGame.Read(data.AsSpan(0, 29))).Error);
    }

    [Theory]
    [InlineData(70)]   // inside the globals
    [InlineData(71)]   // before record 1
    [InlineData(188)]  // inside the control word of player 1
    [InlineData(312)]  // inside the intelligence of player 2
    [InlineData(1014)] // last byte of the team table missing
    public void RejectsShortFile(int length)
    {
        var data = Build(3, 2);

        var ex = Assert.Throws<SavedGameException>(() => SavedGame.Read(data.AsSpan(0, length)));
        Assert.Equal(SavedGameError.TooShort, ex.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void RejectsPlayerCountOutsideRange(int count)
    {
        var data = Build(2);
        WriteInt16(data, 32, count);

        var ex = Assert.Throws<SavedGameException>(() => SavedGame.Read(data));
        Assert.Equal(SavedGameError.PlayerCountOutOfRange, ex.Error);
    }

    [Fact]
    public void UnknownIntelligenceKindHasNoName()
    {
        var data = Build(2, 1);
        WriteInt16(data, RecordOffset(1, SavedGamePlayerRecord.Size + SavedGamePlayer.ControlSize), 8);

        var ai = SavedGame.Read(data).Players[0].Intelligence!;
        Assert.Equal(8, ai.Kind);
        Assert.Null(ai.KindName);
    }
}
