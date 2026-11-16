using BombRedone.Core.Battle;
using BombRedone.Core.Economy;
using BombRedone.Core.Setup;
using BombRedone.Formats;
using BombRedone.Tests.Support;

namespace BombRedone.Core.Tests.Battle;

/// <summary>
/// Order of the records in the saved games of DOSBox session 3 (reference repository <c>original/spielstaende/</c>, never
/// in this repository): both <c>ablauf1-*</c> files were saved at the status screen, after the sort of the rating, so
/// the records are already in rating order ("Player 2" first, spielstand.md "Beobachtungen") and the record number +00h
/// is the position (O3:0000 writes it, S4).
/// </summary>
public sealed class BattleSavedGameTests
{
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

        throw new FileNotFoundException($"{name} not found (BOMB_SPIELSTAENDE, spielstaende next to gamedata, " +
            "~/referenz/original/spielstaende). Load it with tools/claude/fetch-reference.sh (without arguments).");
    }

    [GameDataTheory]
    [InlineData("ablauf1-GAME1.TWS")]
    [InlineData("ablauf1-GAME2.TWS")]
    public void Saved_records_are_in_rating_order_with_the_position_as_number(string name)
    {
        var game = SavedGame.Load(PathOf(name));
        var records = new BattleRecords(game.Players.Select(p => new BattlePlayer(
            new PlayerAccount(p.Index, p.Record.Name) { Score = p.Record.Score, Wins = p.Record.GamesWon },
            (PlayerControl)p.Control, null, p.Record.Team)));
        var before = records.InOrder.ToArray();

        records.SortForRating();

        Assert.Equal(before, records.InOrder);
        Assert.Equal("Player 2", records[1].Name);
        Assert.Equal(Enumerable.Range(1, game.Players.Count), game.Players.Select(p => (int)p.Record.Number));
    }
}
