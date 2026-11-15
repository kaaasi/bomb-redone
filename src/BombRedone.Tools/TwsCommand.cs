using BombRedone.Core.Economy;
using BombRedone.Formats;

namespace BombRedone.Tools;

/// <summary>
/// <c>bombtool tws &lt;file&gt;</c>: prints the fields of a saved game (release 3.2) with file offsets
/// (layout: docs/formate/spielstand.md). Records are listed in file order (#71: as stored).
/// </summary>
internal static class TwsCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 1 || args[0] is "-h" or "--help")
        {
            Console.Error.WriteLine("Usage: bombtool tws <GAME.TWS>");
            return 2;
        }

        SavedGame game;
        try
        {
            game = SavedGame.Load(args[0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SavedGameException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        foreach (var line in Lines(game, Path.GetFullPath(args[0])))
            Console.WriteLine(line);
        return 0;
    }

    /// <summary>The printed lines (separate for tests).</summary>
    internal static IEnumerable<string> Lines(SavedGame game, string path)
    {
        var g = game.Globals;
        var computers = game.Players.Count(p => p.IsComputer);
        yield return $"Saved game {path} (release 3.2, {g.PlayerCount} players, {computers} computers, " +
                     $"{SavedGame.SizeFor(g.PlayerCount, computers) + game.TrailingBytes.Length} bytes)";
        yield return "Globals";
        foreach (var (offset, name, value) in Globals(g))
            yield return $"  {offset,4}  {name,-26} {value}";

        var pos = SavedGame.HeaderSize + SavedGame.GlobalsSize;
        foreach (var p in game.Players)
        {
            var r = p.Record;
            yield return $"Player record {p.Index} at offset {pos}";
            yield return Field(pos + 0x00, "Number", $"{r.Number}");
            yield return Field(pos + 0x01, "Name", $"\"{r.Name}\"");
            yield return Field(pos + 0x13, "Score", $"{r.Score}");
            yield return Field(pos + 0x17, "Spent", $"{r.Spent} (money {r.Money})");
            yield return Field(pos + 0x1D, "TurretAngle", $"{r.TurretAngle}");
            yield return Field(pos + 0x1F, "TurretDirection", $"{r.TurretDirection}");
            yield return Field(pos + 0x23, "TakesTurns", $"{r.TakesTurns}");
            yield return Field(pos + 0x28, "OtherPlayer", $"{r.OtherPlayer}");
            yield return Field(pos + 0x2A, "Stock", Stock(r));
            yield return Field(pos + 0x60, "ColorDivisors", $"{r.ColorDivisor(0)}, {r.ColorDivisor(1)}, {r.ColorDivisor(2)}");
            yield return Field(pos + 0x66, "Team", $"{r.Team}");
            yield return Field(pos + 0x6A, "GamesWon", $"{r.GamesWon}");
            pos += SavedGamePlayerRecord.Size;
            yield return Field(pos, "Control", p.Control switch
            {
                1 => "1 (keyboard)",
                2 => "2 (mouse)",
                3 => "3 (computer)",
                var c => $"{c} (unknown)",
            });
            pos += SavedGamePlayer.ControlSize;
            if (p.Intelligence is { } ai)
            {
                yield return Field(pos, "Intelligence", $"kind {ai.Kind} ({ai.KindName ?? "unknown"}), +0Bh {ai.Unknown0B}, " +
                                                        $"choice {ai.Choice}, player {ai.PlayerNumber}");
                pos += SavedGameIntelligence.Size;
            }
        }

        yield return $"Team table at offset {pos} ({SavedGameTeamTable.Size} bytes)";
        if (!g.TeamPlay)
        {
            // Without team play the table is not filled (stale memory possible); only its raw size is shown.
            var nonZero = game.Teams.Raw.Span.ToArray().Count(b => b != 0);
            yield return $"  not used (team play off), {nonZero} non-zero bytes kept raw";
        }
        else
        {
            foreach (var t in game.Teams.Teams)
            {
                yield return Field(pos + t.Number * SavedGameTeam.Size, $"Team {t.Number}",
                    $"\"{t.Name}\", members {t.MemberCount}, all computers {OnOff(t.AllComputers)}");
            }
        }

        if (!game.TrailingBytes.IsEmpty)
            yield return $"{game.TrailingBytes.Length} bytes after the team table (not read by the original)";
    }

    private static IEnumerable<(int Offset, string Name, string Value)> Globals(SavedGameGlobals g)
    {
        yield return (30, "Unknown47D8", $"{g.Unknown47D8}");
        yield return (32, "PlayerCount", $"{g.PlayerCount}");
        yield return (34, "CurrentGame", $"{g.CurrentGame}");
        yield return (36, "RoundWind", Named(g.RoundWind));
        yield return (38, "RoundPlayerHelper", $"{g.RoundPlayerHelper}");
        yield return (40, "Unknown47E8", $"{g.Unknown47E8}");
        yield return (42, "RoundPass", $"{g.RoundPass}");
        yield return (44, "RoundWall", Named(g.RoundWall));
        yield return (46, "PlayerToMove", $"{g.PlayerToMove}");
        yield return (47, "AllComputers", OnOff(g.AllComputers));
        yield return (48, "RoundWallStopsOrBounces", OnOff(g.RoundWallStopsOrBounces));
        yield return (49, "RoundWallWarps", OnOff(g.RoundWallWarps));
        yield return (50, "WindValue", $"{g.WindValue}");
        yield return (52, "CrumblePercent", $"{g.CrumblePercent}");
        yield return (54, "GamesToPlay", $"{g.GamesToPlay}");
        yield return (56, "Wind", Named(g.Wind));
        yield return (58, "Wall", Named(g.Wall));
        yield return (60, "Background", $"{g.Background}");
        yield return (62, "UserBackgroundMask", $"0x{g.UserBackgroundMask:X4}");
        yield return (64, "FlySound", OnOff(g.FlySound));
        yield return (65, "SoundEffects", OnOff(g.SoundEffects));
        yield return (66, "InvisibleShot", OnOff(g.InvisibleShot));
        yield return (67, "ShotTrail", OnOff(g.ShotTrail));
        yield return (68, "TanksFall", OnOff(g.TanksFall));
        yield return (69, "ComputersBuy", OnOff(g.ComputersBuy));
        yield return (70, "TeamPlay", OnOff(g.TeamPlay));
    }

    /// <summary>Non-zero stock entries with the short names of the shop (ItemCatalog.Original), or "none".</summary>
    private static string Stock(SavedGamePlayerRecord r)
    {
        var catalog = ItemCatalog.Original;
        var parts = new List<string>();
        for (var k = 0; k < SavedGamePlayerRecord.GuidanceCount; k++)
            if (r.Guidance(k) != 0) parts.Add($"{catalog.Guidance[k].ShortName} {r.Guidance(k)}");
        for (var k = 0; k < SavedGamePlayerRecord.WeaponCount; k++)
            if (r.Weapon(k) != 0) parts.Add($"{catalog.Weapons[k].ShortName} {r.Weapon(k)}");
        for (var k = 0; k < SavedGamePlayerRecord.DefenseCount; k++)
            if (r.Defense(k) != 0) parts.Add($"{catalog.Defenses[k].ShortName} {r.Defense(k)}");
        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string Field(int offset, string name, string value) => $"  {offset,4}  {name,-26} {value}";

    private static string OnOff(bool value) => value ? "yes" : "no";

    private static string Named<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? $"{Convert.ToInt32(value)} ({value})" : $"{Convert.ToInt32(value)} (unknown)";
}
