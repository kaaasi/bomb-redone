using BombRedone.Formats;

// bombtool: inspection of the original files. Commands are added per work package.
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Usage: bombtool info [gamedata]");
    Console.WriteLine("       bombtool export [gamedata] [outdir]");
    return 0;
}

switch (args[0])
{
    case "info":
        return Info(args.ElementAtOrDefault(1));
    case "export":
        return BombRedone.Tools.ExportCommand.Run(args.ElementAtOrDefault(1), args.ElementAtOrDefault(2));
    case "cfg": return BombRedone.Tools.ConfigCommand.Run(args[1..]);
    default:
        Console.Error.WriteLine($"Unknown command: {args[0]}");
        return 2;
}

static int Info(string? path)
{
    GameData data;
    try
    {
        data = GameData.Locate(path);
    }
    catch (DirectoryNotFoundException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }

    Console.WriteLine($"Original data: {data.RootDirectory}");
    foreach (var name in GameData.PackageFiles)
    {
        var file = data.Find(name);
        Console.WriteLine(file is null ? $"  {name,-14} missing" : $"  {name,-14} {new FileInfo(file).Length,8} bytes");
    }

    var missing = data.MissingPackageFiles();
    Console.WriteLine(missing.Count == 0 ? "Package complete." : $"{missing.Count} file(s) missing.");
    return missing.Count == 0 ? 0 : 1;
}
