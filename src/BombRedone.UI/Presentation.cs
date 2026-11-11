namespace BombRedone.UI;

/// <summary>Rectangle of the scaled image inside the window, in window pixels.</summary>
public readonly record struct PresentationRect(int X, int Y, int Width, int Height, int Scale);

/// <summary>
/// Places the frame buffer in a window: whole-number scaling, centered, optionally stretched to 4:3.
/// </summary>
/// <remarks>
/// Decision E6 = B (user, 2026-11-11). The 4:3 correction is [APPROX]: mode 13h was shown on 4:3 monitors,
/// so its pixels are 1.2 times as high as wide; how the original looked on a given monitor is not documented
/// in the original files.
/// </remarks>
public static class Presentation
{
    /// <summary>Height factor of a 320x200 image on a 4:3 display: (4/3) / (320/200) = 6/5.</summary>
    public const int AspectNumerator = 6;

    public const int AspectDenominator = 5;

    public static PresentationRect Fit(int windowWidth, int windowHeight, int surfaceWidth, int surfaceHeight,
        bool aspectCorrection)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(surfaceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(surfaceHeight);

        var unitHeight = aspectCorrection ? surfaceHeight * AspectNumerator : surfaceHeight * AspectDenominator;
        // Largest whole-number scale that fits; never below 1 so that a tiny window still shows the image.
        var scale = Math.Max(1, Math.Min(windowWidth / surfaceWidth, windowHeight * AspectDenominator / unitHeight));
        var width = surfaceWidth * scale;
        var height = unitHeight * scale / AspectDenominator;
        return new PresentationRect((windowWidth - width) / 2, (windowHeight - height) / 2, width, height, scale);
    }
}
