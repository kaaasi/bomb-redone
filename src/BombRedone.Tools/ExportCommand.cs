using BombRedone.Formats;

namespace BombRedone.Tools;

/// <summary>
/// <c>bombtool export [gamedata] [outdir]</c>: writes the title images as PNG (default folder <c>export/</c>,
/// ignored by git; exported originals must never be committed).
/// </summary>
public static class ExportCommand
{
    /// <summary>Title images of the original: [ORIGINAL] BOMB.TXT file list.</summary>
    public static readonly IReadOnlyList<string> TitleImages = ["BOMB1.RAW", "BOMB2.RAW"];

    public const string DefaultOutputFolder = "export";

    public static int Run(string? gamedataPath, string? outputFolder)
    {
        GameData data;
        try
        {
            data = GameData.Locate(gamedataPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var folder = outputFolder ?? DefaultOutputFolder;
        var failed = false;
        foreach (var name in TitleImages)
        {
            var path = data.Find(name);
            if (path is null)
            {
                Console.Error.WriteLine($"{name}: missing in {data.RootDirectory}");
                failed = true;
                continue;
            }

            try
            {
                var image = RawImage.Load(path);
                Directory.CreateDirectory(folder);
                var target = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + ".png");
                File.WriteAllBytes(target, ExportPng.Encode(image));
                Console.WriteLine($"{name}: {RawImage.Width}x{RawImage.Height}, {image.UsedColorCount()} colors -> {target}");
            }
            catch (FormatException ex)
            {
                Console.Error.WriteLine($"{name}: {ex.Message}");
                failed = true;
            }
        }
        return failed ? 1 : 0;
    }
}
