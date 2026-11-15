using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// Playfield screen against the DOSBox screenshot s3_02 (session 3, round 1 before the first shot; spur gelaende-bilder,
/// docs/analyse/feld.md section 6). Start value from the window search: Randomize at 17:12:39.69, one menu step, RandSeed
/// 4983853D before the shuffle (PlayfieldSession). The picture is read from the reference repository (never committed,
/// AGENTS.md 2.1).
/// </summary>
public sealed class PlayfieldScreenSessionTests
{
    private const string PictureName = "s3_02_r1-spielfeld-vor-erstem-schuss.png";

    /// <summary>
    /// Header values the turn draws in colour 14 (O7:040C, docs/analyse/runde.md section 6.1, not implemented in round 2):
    /// angle, power, direction arrow and the value after "Men". The wind value is drawn with the header and is equal
    /// (review #128 P3-3). (x1, y1, x2, y2), both corners included.
    /// </summary>
    private static readonly (int X1, int Y1, int X2, int Y2)[] TurnValues =
    [
        (56, 0, 69, 6), (56, 10, 79, 16), (290, 1, 302, 5), (248, 10, 270, 16),
    ];

    [GameDataFact]
    public void Round_one_of_session_three_matches_the_screenshot()
    {
        var (pixels, palette) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath(PictureName));
        var screen = PlayfieldSession.CreateScreen();
        var ours = screen.Screen.Pixels;

        var differences = Enumerable.Range(0, 320 * 200).Where(i => pixels[i] != ours[i]).ToList();

        // Frame, background, terrain, tanks, labels of the header and the weapon bar are equal pixel for pixel; only the
        // values the turn writes are missing.
        Assert.All(differences, i =>
        {
            Assert.Equal((14, 0), (pixels[i], ours[i]));
            Assert.Contains(TurnValues, r => i % 320 >= r.X1 && i % 320 <= r.X2 && i / 320 >= r.Y1 && i / 320 <= r.Y2);
        });
        Assert.Equal(248, differences.Count);

        // DAC of all colours the picture uses (DOSBox writes 6 bits as v << 2 | v >> 4), including the gradient 30..59 of
        // the background (no rotation step yet). Not equal: tank colours 20/21 follow the record that holds the player's
        // colour divisors, which the shuffle swaps (R2-D4 = A: records are swapped in round 3); of the weapon colours
        // 100..119 the turn sets 100, 101 and 111 (review #128 P3-3), the others are equal.
        foreach (var index in pixels.Distinct().Where(i => i is not (20 or 21) && !IsTurnColor(i)))
        {
            for (var c = 0; c < 3; c++)
            {
                var v = screen.Dac[3 * index + c];
                Assert.Equal(palette[3 * index + c], (byte)((v << 2) | (v >> 4)));
            }
        }

        Assert.Equal(palette.AsSpan(3 * 20, 3).ToArray(), PlayfieldSession.Rgb8(screen.Dac.Slice(3 * 21, 3)));
        Assert.Equal(palette.AsSpan(3 * 21, 3).ToArray(), PlayfieldSession.Rgb8(screen.Dac.Slice(3 * 20, 3)));
    }

    private static bool IsTurnColor(byte index) =>
        index is PlayfieldScreen.WeaponColorBase or PlayfieldScreen.WeaponColorBase + 1 or PlayfieldScreen.WeaponColorBase + 11;
}
