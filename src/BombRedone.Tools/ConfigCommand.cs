using BombRedone.Formats;

namespace BombRedone.Tools;

/// <summary><c>bombtool cfg &lt;file&gt;</c>: prints the fields of a BOMB.CFG (release 3.2).</summary>
internal static class ConfigCommand
{
    public static int Run(string[] args)
    {
        if (args.Length != 1 || args[0] is "-h" or "--help")
        {
            Console.Error.WriteLine("Usage: bombtool cfg <BOMB.CFG>");
            return 2;
        }

        GameConfig config;
        try
        {
            config = GameConfig.Load(args[0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or GameConfigException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine($"BOMB.CFG {Path.GetFullPath(args[0])} (release 3.2, {GameConfig.Size} bytes)");
        foreach (var (offset, name, value) in Fields(config))
            Console.WriteLine($"  {offset,2}  {name,-28} {value}");
        return 0;
    }

    /// <summary>Offset, name and printable value of every field, in file order.</summary>
    internal static IEnumerable<(int Offset, string Name, string Value)> Fields(GameConfig c)
    {
        yield return (5, "SoundEffects", OnOff(c.SoundEffects));
        yield return (6, "FlySound", OnOff(c.FlySound));
        yield return (7, "Wind", Named((int)c.Wind, Enum.IsDefined(c.Wind) ? c.Wind.ToString() : null));
        yield return (9, "CrumblePercent", $"{c.CrumblePercent}");
        yield return (11, "Wall", Named((int)c.Wall, Enum.IsDefined(c.Wall) ? c.Wall.ToString() : null));
        yield return (13, "InvisibleShot", OnOff(c.InvisibleShot));
        yield return (14, "ShotTrail", OnOff(c.ShotTrail));
        yield return (15, "GamesToPlay", $"{c.GamesToPlay}");
        yield return (17, "TanksFall", OnOff(c.TanksFall));
        yield return (18, "Background", Named(c.Background, c.BackgroundKind switch
        {
            BackgroundKind.Fixed => $"background {c.Background}",
            BackgroundKind.Unknown => null,
            var kind => kind.ToString(),
        }));
        yield return (20, "UserBackgroundMask", $"0x{c.UserBackgroundMask:X4}");
        yield return (22, "ForceMouse", OnOff(c.ForceMouse));
        yield return (23, "MouseEnabled", OnOff(c.MouseEnabled));
        yield return (24, "DirectVideo", OnOff(c.DirectVideo));
        yield return (25, "ExtendedKeyboard", OnOff(c.ExtendedKeyboard));
        yield return (26, "PaletteRotation", OnOff(c.PaletteRotation));
        yield return (27, "CrudeVideo", OnOff(c.CrudeVideo));
        yield return (28, "PaletteSpeed", $"{c.PaletteSpeed}");
        yield return (30, "ComputersBuy", OnOff(c.ComputersBuy));
        yield return (31, "TurretSpeed", $"{c.TurretSpeed}");
        yield return (33, "FallSpeed", $"{c.FallSpeed}");
        yield return (35, "ExplosionSpeed", $"{c.ExplosionSpeed}");
        yield return (37, "ShotSpeed", $"{c.ShotSpeed}");
        yield return (39, "LaserSpeed", $"{c.LaserSpeed}");
        yield return (41, "PaletteUsesRetrace", c.PaletteUsesRetrace ? "yes (PV, video retrace)" : "no (PT, timer)");
        yield return (42, "TeamPlay", OnOff(c.TeamPlay));
        yield return (43, "BossScreenMemoryKiB", $"{c.BossScreenMemoryKiB} (used: {c.BossScreenMemoryBytes} bytes)");
    }

    private static string OnOff(bool value) => value ? "yes" : "no";

    private static string Named(int value, string? name) => name is null ? $"{value} (unknown)" : $"{value} ({name})";
}
