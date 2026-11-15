using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;
using BombRedone.Formats;
using BombRedone.Tests.Support;
using BombRedone.UI.Screens;

namespace BombRedone.UI.Tests.Screens;

/// <summary>
/// The four shots of round 1 of DOSBox session 3 against the screenshots s3_03..s3_07 (spur schuss-bilder,
/// docs/analyse/feld.md section 7). Start value and set-up as in PlayfieldScreenSessionTests; options of
/// ablauf1-GAME1.TWS = BOMBCFG defaults (SS 10, trail, sound, Crumble 75, falling tanks), both players Baby Missile.
/// Angle and power were searched against the trail in the picture (the header shows the player to move, not the last shot).
/// Compared: frame and field x 8..312, y 28..192 (the turn that writes the header is not ported, runde.md 6.2).
/// </summary>
public sealed class PlayfieldShotSessionTests
{
    /// <summary>Record (Satz), angle, direction ±1, power; all with weapon 1 (Baby Missile, red field in the weapon bar).</summary>
    private static readonly (int Record, int Angle, int Direction, int Power)[] Shots =
    [
        (2, 75, -1, 200), // "Player 1": s3_03
        (1, 75, 1, 272),  // "Player 2": s3_04
        (2, 38, -1, 241), // "Player 1": s3_05
        (1, 30, 1, 272),  // "Player 2": s3_06, s3_07 (kills record 2)
    ];

    /// <summary>Wind of the round (s3_02, checked in PlayfieldSession.CreateScreen).</summary>
    private const int WindValue = -92;

    private static readonly string[] Pictures =
    [
        "s3_03_r1-spielfeld-spieler2-am-zug.png",
        "s3_04_r1-spielfeld-spieler1-am-zug.png",
        "s3_05_r1-spielfeld-men95.png",
        "s3_06_r1-spielfeld-einschlag.png",
        "s3_07_r1-spielfeld-nach-einschlag.png",
    ];

    /// <summary>Shots 1..3: frame and field equal pixel for pixel, DAC equal up to the colours of the records (R2-D4).</summary>
    [GameDataTheory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Field_after_the_shot_matches_the_screenshot(int shots)
    {
        var (pixels, palette) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath(Pictures[shots - 1]));
        var screen = Play(Shots.Take(shots));

        Assert.Empty(FieldDifferences(pixels, screen.Screen.Pixels));

        // Every colour of the field has the DAC of the picture, except the colours of the records: tank 20/21 and trail
        // 99/98 (100 − record) take their hue from the colour divisors, which the shuffle swaps with the records (R2-D4 = A,
        // records are swapped in round 3; feld.md 6.2): red and green are exchanged, the value is equal. The value of the
        // tank colours follows the energy of the record (O10:014A): in s3_05 colour 20 has 59 (energy 95), colour 21 has 62
        // (energy 99, self damage of shot 3).
        var used = FieldIndices().Select(i => (int)pixels[i]).Distinct().ToList();
        Assert.All(used.Where(c => c is not (20 or 21 or 98 or 99)), c =>
            Assert.Equal(palette.AsSpan(3 * c, 3).ToArray(), PlayfieldSession.Rgb8(screen.Dac.Slice(3 * c, 3))));
        Assert.All(used.Where(c => c is 20 or 21 or 98 or 99), c =>
        {
            var ours = PlayfieldSession.Rgb8(screen.Dac.Slice(3 * c, 3));
            Assert.Equal((palette[3 * c], palette[3 * c + 1], palette[3 * c + 2]), (ours[1], ours[0], ours[2]));
        });
    }

    /// <summary>
    /// Shot 4 hits record 2 (energy 0). Flight, trail, crater and the fall of the tank are equal; the only differences are
    /// terrain and the bottom row of the dead tank that the death explosion (O5, runde.md 5.9, round 3) removed: there the
    /// picture shows background (30..59) where we still have terrain (2) or tank colour 21.
    /// </summary>
    [GameDataFact]
    public void Fourth_shot_matches_up_to_the_death_explosion()
    {
        var screen = Play(Shots);
        var tanks = screen.Field.Tanks;
        Assert.Equal((95, 0), (tanks[0].Energy, tanks[1].Energy));
        Assert.Equal((277, 150), (tanks[1].X, tanks[1].Y));

        foreach (var (picture, count) in new[] { (Pictures[3], 371), (Pictures[4], 1371) })
        {
            var (pixels, _) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath(picture));
            var differences = FieldDifferences(pixels, screen.Screen.Pixels);
            Assert.All(differences, i =>
            {
                Assert.InRange(pixels[i], (byte)30, (byte)59);
                Assert.Contains(screen.Screen.Pixels[i], new byte[] { Playfield.TerrainColor, 21 });
                Assert.InRange(i / 320, 141, 189);
                Assert.InRange(i % 320, 211, 309);
            });
            Assert.Equal(count, differences.Count);
        }
    }

    /// <summary>The parameters are determined: one step in angle or power changes the trail of each shot.</summary>
    [GameDataFact]
    public void Neighbouring_angles_and_powers_do_not_match()
    {
        for (var n = 1; n <= 3; n++)
        {
            var (pixels, _) = PlayfieldSession.ReadPicture(PlayfieldSession.PicturePath(Pictures[n - 1]));
            var (record, angle, direction, power) = Shots[n - 1];
            foreach (var (da, dp) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                var shots = Shots.Take(n - 1).Append((record, angle + da, direction, power + dp));
                Assert.NotEmpty(FieldDifferences(pixels, Play(shots).Screen.Pixels));
            }
        }
    }

    /// <summary>
    /// Round 1 from the start value, start player, then the shots in order like O4:44A7 (weapon O4:01B7, U431:07BA,
    /// Crumble O7:03C1, falling O7:0020). Between the shots only what the turn changes on the field: [APPROX] the turn
    /// O7:040C (runde.md 6.2, not ported) erases the barrel from the buffer and draws the tank anew when the angle changes;
    /// here once from the old to the new angle (the steps in between leave no trace, s3_05).
    /// </summary>
    private static PlayfieldScreen Play(IEnumerable<(int Record, int Angle, int Direction, int Power)> shots)
    {
        var screen = PlayfieldSession.CreateScreen(out var random);
        var field = screen.Field;
        var start = StartPlayer.Choose(new int[2], [true, true], random);
        Assert.Equal(2, start.Player);
        var config = GameConfig.BombCfgDefaults;
        foreach (var (record, angle, direction, power) in shots)
        {
            var tank = field.Tanks[record - 1];
            Assert.Equal(direction, tank.Direction);
            if (tank.Angle != angle)
            {
                var (dx, dy) = Playfield.BarrelOffset(tank.Angle);
                VgaDraw.CopyLine(field.Background.Pixels, field.Screen.Pixels, tank.X, tank.Y, tank.X + dx * tank.Direction,
                    tank.Y - dy);
                tank.Aim(angle, power);
                field.DrawTank(tank);
            }

            tank.Aim(angle, power);
            tank.Weapon = 1;
            BallisticShot.Fire(field, tank, new ShotSettings(config.ShotSpeed, WindValue, config.ShotTrail, config.SoundEffects), random);
            AfterShot.Run(field, config.CrumblePercent, config.TanksFall, random);
        }

        screen.RefreshField();
        return screen;
    }

    private static IEnumerable<int> FieldIndices()
    {
        for (var y = PlayfieldScreen.FieldTop; y <= PlayfieldScreen.FieldBottom; y++)
        {
            for (var x = PlayfieldScreen.FieldLeft; x <= PlayfieldScreen.FieldRight; x++)
                yield return y * 320 + x;
        }
    }

    private static List<int> FieldDifferences(byte[] pixels, ReadOnlySpan<byte> ours)
    {
        var result = new List<int>();
        foreach (var i in FieldIndices())
        {
            if (pixels[i] != ours[i])
                result.Add(i);
        }

        return result;
    }
}
