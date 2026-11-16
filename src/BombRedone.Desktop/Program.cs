using BombRedone.Desktop;

using (var window = new DesktopGame())
    window.Run();

// [ORIGINAL] O3[1] prints the end result of the last battle in text mode when the program ends.
foreach (var line in DesktopGame.Results)
    Console.WriteLine(line.Text);
