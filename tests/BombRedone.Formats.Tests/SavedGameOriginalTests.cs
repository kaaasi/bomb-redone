using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Formats.Tests;

/// <summary>
/// Reader of saved games against the four files of DOSBox session 3 (reference repository,
/// <c>original/spielstaende/</c>, never in this repository; decision R2-D10 = A).
/// </summary>
/// <remarks>
/// Expected values come from the screenshots of session 3 (reference repository <c>bilder/runde-1/</c>), read by eye:
/// <c>ablauf1-GAME1.TWS</c> was saved at the status screen after game 1 (<c>s3_08</c>, after loading again <c>s3_20</c>),
/// <c>ablauf1-GAME2.TWS</c> after game 2 (<c>s3_12</c>); the shop screens <c>s3_09</c> (player 2 after game 1) and
/// <c>s3_17</c> (player 1 after game 2) show money and stock. The <c>frisch-*</c> files have no screenshots: structure
/// and round trip only.
/// </remarks>
public class SavedGameOriginalTests
{
    public static TheoryData<string> Files() =>
        new("ablauf1-GAME1.TWS", "ablauf1-GAME2.TWS", "frisch-GAME1.TWS", "frisch-GAME2.TWS");

    /// <summary>
    /// Finds a file of the reference repository: <c>BOMB_SPIELSTAENDE</c>, the folder <c>spielstaende</c> next to the
    /// (resolved) <c>gamedata</c> folder, or <c>~/referenz/original/spielstaende</c> (as GameConfigOriginalTests).
    /// </summary>
    private static string PathOf(string name)
    {
        var gamedata = new DirectoryInfo(TestGameData.Load().RootDirectory);
        var resolved = gamedata.ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo ?? gamedata;
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("BOMB_SPIELSTAENDE"),
            resolved.Parent is { } parent ? Path.Combine(parent.FullName, "spielstaende") : null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "referenz", "original", "spielstaende"),
        };
        foreach (var dir in candidates.Where(d => !string.IsNullOrEmpty(d)))
        {
            var path = Path.Combine(dir!, name);
            if (File.Exists(path))
                return path;
        }

        throw new FileNotFoundException(
            $"{name} not found (BOMB_SPIELSTAENDE, spielstaende next to gamedata, ~/referenz/original/spielstaende). " +
            "Load it with tools/claude/fetch-reference.sh (without arguments).");
    }

    [GameDataTheory]
    [MemberData(nameof(Files))]
    public void ReadsAndRoundTrips(string name)
    {
        var bytes = File.ReadAllBytes(PathOf(name));

        var game = SavedGame.Read(bytes);

        // Two human players, no computer: 890 bytes, no bytes after the team table.
        Assert.Equal(SavedGame.SizeFor(2, 0), bytes.Length);
        Assert.Equal(2, game.Globals.PlayerCount);
        Assert.All(game.Players, p => Assert.Equal(1, p.Control));
        Assert.All(game.Players, p => Assert.Null(p.Intelligence));
        Assert.True(game.TrailingBytes.IsEmpty);
        Assert.Equal(bytes, game.ToBytes());
    }

    /// <summary>The options in all four equal the defaults of BOMBCFG.EXE (observation; the BOMB.CFG of session 3 is not noted).</summary>
    [GameDataTheory]
    [MemberData(nameof(Files))]
    public void OptionsAreBombCfgDefaults(string name)
    {
        var g = SavedGame.Load(PathOf(name)).Globals;
        var d = GameConfig.BombCfgDefaults;

        Assert.Equal(d.CrumblePercent, g.CrumblePercent);
        Assert.Equal(d.GamesToPlay, g.GamesToPlay);
        Assert.Equal(d.Wind, g.Wind);
        Assert.Equal(d.Wall, g.Wall);
        Assert.Equal(d.Background, g.Background);
        Assert.Equal(d.UserBackgroundMask, g.UserBackgroundMask);
        Assert.Equal(d.FlySound, g.FlySound);
        Assert.Equal(d.SoundEffects, g.SoundEffects);
        Assert.Equal(d.InvisibleShot, g.InvisibleShot);
        Assert.Equal(d.ShotTrail, g.ShotTrail);
        Assert.Equal(d.TanksFall, g.TanksFall);
        Assert.Equal(d.ComputersBuy, g.ComputersBuy);
        Assert.Equal(d.TeamPlay, g.TeamPlay);
        // Wind option Normal and walls None: no Random(5), so the round values equal the options (T7a, T7b).
        Assert.Equal(g.Wind, g.RoundWind);
        Assert.Equal(g.Wall, g.RoundWall);
        Assert.False(g.RoundWallStopsOrBounces || g.RoundWallWarps);
    }

    /// <summary>
    /// After game 1 (s3_08, s3_20): "1 Game Played out of 10"; Player 2 first with 1 win and score 4490, Player 1 with
    /// 0 wins and 50. Shop of Player 2 (s3_09): 4490 dollars, Incinerator 28. Round 1 had "Wind: 92 ←" (s3_02) and
    /// Player 2 "Power: 272", "Direction (→)" (s3_05); Player 1 "Direction (←)" (s3_02).
    /// </summary>
    [GameDataFact]
    public void Game1MatchesScreenshots()
    {
        var game = SavedGame.Load(PathOf("ablauf1-GAME1.TWS"));

        Assert.Equal(1, game.Globals.CurrentGame);
        Assert.Equal(10, game.Globals.GamesToPlay);
        Assert.Equal(-92, game.Globals.WindValue);
        var (first, second) = (game.Players[0].Record, game.Players[1].Record);
        Assert.Equal(("Player 2", 1, 4490), (first.Name, (int)first.GamesWon, first.Score));
        Assert.Equal(("Player 1", 0, 50), (second.Name, (int)second.GamesWon, second.Score));
        Assert.Equal(4490, first.Money);
        Assert.Equal(28, first.Weapon(1)); // Incinerator
        Assert.Equal(1, first.TurretDirection);
        Assert.Equal(-1, second.TurretDirection);
        // Observation, not typed: record +1Bh holds the power shown in s3_05.
        Assert.Equal(272, BitConverter.ToInt16(first.Raw.Span[0x1B..]));
    }

    /// <summary>
    /// After game 2 (s3_12): "2 Games Played out of 10"; Player 2 1 win, 4830; Player 1 1 win, 4540. Player 2 bought
    /// Ball 'O' Dirt for 4000 after game 1 (s3_10: 490 dollars left). Shop of Player 1 (s3_17): 4540 dollars,
    /// Incinerator 25.
    /// </summary>
    [GameDataFact]
    public void Game2MatchesScreenshots()
    {
        var game = SavedGame.Load(PathOf("ablauf1-GAME2.TWS"));

        Assert.Equal(2, game.Globals.CurrentGame);
        var (first, second) = (game.Players[0].Record, game.Players[1].Record);
        Assert.Equal(("Player 2", 1, 4830), (first.Name, (int)first.GamesWon, first.Score));
        Assert.Equal(("Player 1", 1, 4540), (second.Name, (int)second.GamesWon, second.Score));
        Assert.Equal(4000, first.Spent);
        Assert.Equal(10, first.Weapon(13)); // Ball 'O' Dirt, bundle of 10
        Assert.Equal(4540, second.Money);
        Assert.Equal(25, second.Weapon(1)); // Incinerator
    }

    /// <summary>
    /// Records as stored (#71, R2-D4): all four files list Player 2 first (the higher score), and record +00h is the position
    /// in the file, not the number in the name; the team word (+66h, initially the player number) and the colour divisors
    /// moved with the player. Only described here, not interpreted.
    /// </summary>
    [GameDataTheory]
    [MemberData(nameof(Files))]
    public void RecordsAreStoredSortedByScore(string name)
    {
        var players = SavedGame.Load(PathOf(name)).Players;

        Assert.Equal(["Player 2", "Player 1"], players.Select(p => p.Record.Name));
        Assert.Equal([1, 2], players.Select(p => (int)p.Record.Number));
        Assert.Equal([2, 1], players.Select(p => (int)p.Record.Team));
        Assert.True(players[0].Record.Score >= players[1].Record.Score);
    }

    /// <summary>
    /// Without team play the table is not filled: zero in the fresh session, stale memory of an earlier program in
    /// ablauf1 (review #123 P3-2); kept raw either way.
    /// </summary>
    [GameDataFact]
    public void TeamTableWithoutTeamPlayIsUnused()
    {
        var fresh = SavedGame.Load(PathOf("frisch-GAME1.TWS"));
        var stale = SavedGame.Load(PathOf("ablauf1-GAME1.TWS"));

        Assert.All(fresh.Teams.Raw.ToArray(), b => Assert.Equal(0, b));
        Assert.Contains(stale.Teams.Raw.ToArray(), b => b != 0);
        Assert.False(stale.Globals.TeamPlay);
    }
}
