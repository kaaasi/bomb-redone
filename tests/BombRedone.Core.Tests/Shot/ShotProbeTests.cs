using System.Buffers.Binary;
using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;

namespace BombRedone.Core.Tests.Shot;

/// <summary>
/// The shot O4:01B7 with impact O6:00E0 against the original code run in the probe (docs/analyse/feld.md section 5).
/// Expected values from <c>python3 -I tools/reverse-engineering/feld_schuss.py hashes</c> (analyse/feld/schuss-hashes.tsv):
/// the playfield of a feld.py case up to the start player, then one shot. Compared: every state at the loop head (SHA-256
/// over x, y, vx, vy, sub-step, ticks), the end of the flight, RandSeed, the written DAC entries, the background buffer,
/// frame and field of the screen (x 8..312, y 28..192) and energy, points, shield and hits of every record.
/// </summary>
public sealed class ShotProbeTests
{
    public sealed record ShotCase(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, bool Invisible,
        int Shooter, int Angle, int Power, int Weapon, int SubSteps, bool Trail, int Wind, string? ShotSeed, bool Sound,
        int Steps, string Path, string EndX, string EndY, string EndVx, string EndVy, int Ix, int Iy, int Hit, bool Exploded,
        string SeedAfter, string BackgroundBuffer, string DacSet, string Records, string ScreenField)
    {
        public override string ToString() => $"shot {Nr}";
    }

    public static readonly TheoryData<ShotCase> Cases = ShotProbeData.Cases;

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static (Core.Playfield.Playfield Field, ShotResult Result, string Path, uint Seed) Fire(ShotCase c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, c.Invisible), random);
        Wind.Choose(c.WindOption, random);
        StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);
        if (c.ShotSeed is not null)
            random = new TurboPascalRandom(Convert.ToUInt32(c.ShotSeed, 16));

        var shooter = field.Tanks[c.Shooter - 1];
        shooter.Aim(c.Angle, c.Power);
        shooter.Weapon = c.Weapon;
        field.Palette.ClearWritten();
        using var path = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var record = new byte[30];
        var result = BallisticShot.Fire(field, shooter, new ShotSettings(c.SubSteps, c.Wind, c.Trail, c.Sound), random, step =>
        {
            step.X.WriteBytes(record);
            step.Y.WriteBytes(record.AsSpan(6));
            step.Vx.WriteBytes(record.AsSpan(12));
            step.Vy.WriteBytes(record.AsSpan(18));
            BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(24), (short)step.SubStep);
            BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(26), step.Ticks);
            path.AppendData(record);
        });
        return (field, result, Convert.ToHexStringLower(path.GetHashAndReset()), random.Seed);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Flight_matches_the_original(ShotCase c)
    {
        var (_, result, path, _) = Fire(c);

        Assert.Equal(c.Steps, result.Steps);
        Assert.Equal(c.Path, path);
        Assert.Equal(
            (c.EndX, c.EndY, c.EndVx, c.EndVy, c.Ix, c.Iy, c.Hit, c.Exploded),
            (result.X.ToHex().ToLowerInvariant(), result.Y.ToHex().ToLowerInvariant(), result.Vx.ToHex().ToLowerInvariant(),
                result.Vy.ToHex().ToLowerInvariant(), result.Ix, result.Iy, result.Hit, result.Exploded));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void State_after_the_shot_matches_the_original(ShotCase c)
    {
        var (field, _, _, seed) = Fire(c);

        Assert.Equal(c.SeedAfter, seed.ToString("X8"));
        Assert.Equal(c.Records, string.Join(" ", field.Tanks.Select(t => $"{t.Energy},{t.Score},{t.Defense[Damage.Shield]},{t.Hits}")));
        Assert.Equal(c.DacSet, Sha(field.Palette.WrittenEntries()));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pixels_after_the_shot_match_the_original(ShotCase c)
    {
        var (field, _, _, _) = Fire(c);

        Assert.Equal(c.BackgroundBuffer, Sha(field.Background.Pixels));
        var crop = new byte[305 * 165];
        for (var y = 28; y <= 192; y++)
            field.Screen.Pixels.AsSpan(y * 320 + 8, 305).CopyTo(crop.AsSpan((y - 28) * 305));
        Assert.Equal(c.ScreenField, Sha(crop));
    }
}
