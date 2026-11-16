using System.Buffers.Binary;
using System.Security.Cryptography;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;
using BombRedone.Core.Shot;

namespace BombRedone.Core.Tests.Shot;

/// <summary>
/// All weapons against the original code run in the probe (spur waffen, docs/analyse/feld.md section 8). Expected values
/// from <c>python3 -I tools/reverse-engineering/feld_schuss.py waffen</c> and <c>waffen-cs</c>: the playfield of a feld.py
/// case up to the start player, then one shot through the entry of O4:44A7 (laser O4:3426, MIRV O4:13D4, else O4:01B7).
/// Compared: RandSeed before and after the shot, the flight path and its steps (O4:01B7 only; laser and MIRV have none),
/// the impacts O6:00E0 and the damage O10:0000 in call order, frame and field of the screen, the background buffer, the
/// written DAC entries, energy, points, shield, inertia, repulser and hits of every record, and power, Herb, Vick and the
/// stock of the fired weapon of every record (#163). Only the cases of the parts ported so far run
/// (<see cref="IsPorted"/>); the others follow with the next parts.
/// </summary>
public sealed class WeaponProbeTests
{
    public sealed record WeaponCase(
        int Nr, string Seed, int Players, int Wall, int Background, int WindOption, int Shooter, int Angle, int Power,
        int Weapon, int Guidance, int Target, int Pattern, int Detonation, bool Sound, int Crumble, string? Defense,
        int? Stock, int SubSteps, int Wind, string SeedBefore, int Steps, string Path, string? Impacts, string? Damage,
        string SeedAfter, int RandomN, int RandomReal, string ScreenField, string BackgroundBuffer, string DacSet,
        string Records, string? PostSeed, string? PostScreenField, string? PostBackground, bool SeedSet)
    {
        public override string ToString() => $"weapon shot {Nr}";
    }

    /// <summary>
    /// Whether the case only needs what is ported: since part 5 every case of the list.
    /// </summary>
    public static bool IsPorted(WeaponCase c) => true;

    /// <summary>The ported cases that continue like O4:49CB (all of them with Crumble 0 in weapon_shots()).</summary>
    public static TheoryData<WeaponCase> PostCases
    {
        get
        {
            var data = new TheoryData<WeaponCase>();
            foreach (var c in WeaponProbeData.All.Where(c => IsPorted(c) && c.PostSeed is not null))
                data.Add(c);
            return data;
        }
    }

    /// <summary>"2:50,0,0 1:30,0,0" → player, shield, inertia, repulser.</summary>
    internal static (int Player, int Shield, int Inertia, int Repulser)[] ParseDefense(string? defense) =>
        defense is null
            ? []
            : defense.Split(' ').Select(item =>
            {
                var parts = item.Split(':');
                var v = parts[1].Split(',').Select(int.Parse).ToArray();
                return (int.Parse(parts[0]), v[0], v[1], v[2]);
            }).ToArray();

    public static TheoryData<WeaponCase> Cases
    {
        get
        {
            var data = new TheoryData<WeaponCase>();
            foreach (var c in WeaponProbeData.All.Where(IsPorted))
                data.Add(c);
            return data;
        }
    }

    private static string Sha(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>Records the calls in the format of the probe columns impacts and damage.</summary>
    private sealed class Trace : IShotTrace
    {
        public List<string> Impacts { get; } = [];

        public List<string> Damages { get; } = [];

        private static string Hex(Real48 value)
        {
            var bytes = new byte[6];
            value.WriteBytes(bytes);
            return Convert.ToHexStringLower(bytes);
        }

        public void Impact(PlayfieldTank shooter, int color, Real48 x, Real48 y) =>
            Impacts.Add($"{color},{Hex(x)},{Hex(y)},{shooter.Number}");

        public void Damage(PlayfieldTank tank, int amount, int defense, int lost) =>
            Damages.Add($"{(short)amount}/{defense & 0xFF}/{tank.Number}/{lost}");
    }

    private sealed record TracedShot(Core.Playfield.Playfield Field, TurboPascalRandom Random, string SeedBefore, int Steps,
        string Path, Trace Trace);

    private static (Core.Playfield.Playfield Field, TurboPascalRandom Random, string SeedBefore) Fire(WeaponCase c)
    {
        var shot = FireTraced(c);
        return (shot.Field, shot.Random, shot.SeedBefore);
    }

    private static TracedShot FireTraced(WeaponCase c)
    {
        var random = new TurboPascalRandom(Convert.ToUInt32(c.Seed, 16));
        var field = Core.Playfield.Playfield.Build(
            new PlayfieldOptions(c.Players, c.Wall, c.Background, 0xFFFF, c.WindOption, false), random);
        Wind.Choose(c.WindOption, random);
        StartPlayer.Choose(new int[c.Players], Enumerable.Repeat(true, c.Players).ToArray(), random);
        var seedBefore = random.Seed.ToString("X8");
        if (c.SeedSet)
            random = new TurboPascalRandom(Convert.ToUInt32(c.SeedBefore, 16));  // the probe sets RandSeed before the shot

        // the stocks of new records as in the probe (O2:0216, TurnPlayer.SetDefaultStocks): Lead Ball 100, weapon 1 30,
        // weapon 11 100
        foreach (var tank in field.Tanks)
            (tank.Stock[0], tank.Stock[1], tank.Stock[11]) = (100, 30, 100);
        var shooter = field.Tanks[c.Shooter - 1];
        shooter.Aim(c.Angle, c.Power);
        shooter.Weapon = c.Weapon;
        shooter.Stock[c.Weapon] = c.Stock ?? (c.Weapon == 5 ? c.Power : Math.Max(1, shooter.Stock[c.Weapon]));
        shooter.Guidance = c.Guidance;
        shooter.GuidanceTarget = c.Guidance == 0 ? 0 : c.Target;
        // the probe gives the shooter at least one Herb / Vick for the guidance it uses (feld_schuss.py run_shot)
        if ((c.Guidance & 1) != 0)
            shooter.Herbs = Math.Max(1, shooter.Herbs);
        if ((c.Guidance & 2) != 0)
            shooter.Vicks = Math.Max(1, shooter.Vicks);
        foreach (var (player, shield, inertia, repulser) in ParseDefense(c.Defense))
        {
            var tank = field.Tanks[player - 1];
            tank.Defense[Damage.Shield] = shield;
            tank.Defense[Damage.Inertia] = inertia;
            tank.Defense[Damage.Repulser] = repulser;
        }

        field.Palette.ClearWritten();
        var trace = new Trace();
        field.Trace = trace;
        var settings = new ShotSettings(c.SubSteps, c.Wind, Sound: c.Sound, Crumble: c.Crumble);
        using var path = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var steps = 0;
        if (c.Weapon is 5 or 7)
        {
            WeaponShot.Fire(field, shooter, settings, random, (MirvPattern)c.Pattern, (MirvDetonation)c.Detonation);
        }
        else
        {
            // the same entry WeaponShot.Fire takes, with the steps of the loop head O4:060A as in ShotProbeTests
            var record = new byte[30];
            BallisticShot.Fire(field, shooter, settings, random, step =>
            {
                step.X.WriteBytes(record);
                step.Y.WriteBytes(record.AsSpan(6));
                step.Vx.WriteBytes(record.AsSpan(12));
                step.Vy.WriteBytes(record.AsSpan(18));
                BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(24), (short)step.SubStep);
                BinaryPrimitives.WriteInt32LittleEndian(record.AsSpan(26), step.Ticks);
                path.AppendData(record);
                steps++;
            });
        }

        field.Trace = null;
        return new TracedShot(field, random, seedBefore, steps, Convert.ToHexStringLower(path.GetHashAndReset()), trace);
    }

    private static string? Joined(List<string> items) => items.Count == 0 ? null : string.Join(" ", items);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Flight_impacts_and_damage_match_the_original(WeaponCase c)
    {
        var shot = FireTraced(c);
        if (c.PostSeed is not null)
        {
            // the probe keeps recording through its post phase (falling O7:0020: inertia damage)
            shot.Field.Trace = shot.Trace;
            AfterShot.Run(shot.Field, 0, true, shot.Random);
            shot.Field.Trace = null;
        }

        Assert.Equal(c.Steps, shot.Steps);
        Assert.Equal(c.Path, shot.Path);
        Assert.Equal(c.Impacts, Joined(shot.Trace.Impacts));
        Assert.Equal(c.Damage, Joined(shot.Trace.Damages));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Power_guidance_and_stock_after_the_shot_match_the_original(WeaponCase c)
    {
        var shot = FireTraced(c);

        Assert.Equal(WeaponProbeData.StockAfter[c.Nr], string.Join(" ", shot.Field.Tanks.Select(t =>
            $"{t.Power},{t.Herbs},{t.Vicks},{t.Stock[c.Weapon]}")));
    }

    [Theory]
    [MemberData(nameof(PostCases))]
    public void Landslide_and_falling_after_the_shot_match_the_original(WeaponCase c)
    {
        var (field, random, _) = Fire(c);
        AfterShot.Run(field, 0, true, random);

        Assert.Equal(c.PostSeed, random.Seed.ToString("X8"));
        Assert.Equal(c.PostBackground, Sha(field.Background.Pixels));
        Assert.Equal(c.PostScreenField, ShotProbeTests.FieldHash(field));
    }

    [Fact]
    public void The_probe_list_covers_every_weapon()
    {
        Assert.Equal(186, WeaponProbeData.All.Length);
        Assert.Equal(WeaponProbeData.All.Length, WeaponProbeData.StockAfter.Length);
        Assert.Equal(Enumerable.Range(0, WeaponProbeData.All.Length), WeaponProbeData.All.Select(c => c.Nr));
        Assert.Equal(Enumerable.Range(0, 18), WeaponProbeData.All.Select(c => c.Weapon).Distinct().Order());
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Random_numbers_match_the_original(WeaponCase c)
    {
        var (_, random, seedBefore) = Fire(c);

        if (!c.SeedSet)
            Assert.Equal(c.SeedBefore, seedBefore);
        Assert.Equal(c.SeedAfter, random.Seed.ToString("X8"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Records_after_the_shot_match_the_original(WeaponCase c)
    {
        var (field, _, _) = Fire(c);

        Assert.Equal(c.Records, string.Join(" ", field.Tanks.Select(t =>
            $"{t.Energy},{t.Score},{t.Defense[Damage.Shield]},{t.Defense[Damage.Inertia]},{t.Defense[Damage.Repulser]},{t.Hits}")));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Pixels_and_palette_after_the_shot_match_the_original(WeaponCase c)
    {
        var (field, _, _) = Fire(c);

        Assert.Equal(c.DacSet, Sha(field.Palette.WrittenEntries()));
        Assert.Equal(c.BackgroundBuffer, Sha(field.Background.Pixels));
        Assert.Equal(c.ScreenField, ShotProbeTests.FieldHash(field));
    }
}
