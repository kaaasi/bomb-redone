using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>
/// Impact of a ballistic weapon. [ORIGINAL] O6:00E0 (BOMB.OVR 0x1900C), docs/analyse/runde.md sections 5.5 and 5.6:
/// Lead Ball (Q10) and the explosives {1, 2, 3, 4, 7, 11} (K1–K4). The other weapons (CRI, Sonic Blaster, Dirt, K5–K8)
/// are not ported (round 3).
/// </summary>
public static class Impact
{
    /// <summary>First DAC entry of the explosion colours (O6:023B).</summary>
    public const int ExplosionColor = 130;

    /// <summary>Viewport of the playfield during the explosion (O6:024D: SetViewPort(11, 31, 309, 189, ClipOn)).</summary>
    public static readonly Viewport Field = new(11, 31, 309, 189, true);

    // [ORIGINAL] Real48 constants of O6:00E0.
    private static readonly Real48 Eleven = Real48.FromHex("840000000030");      // O6:0273
    private static readonly Real48 ThirtyOne = Real48.FromHex("850000000078");   // O6:0292
    private static readonly Real48 OneAndHalf = Real48.FromHex("810000000040");  // O6:0708
    private static readonly Real48 OnePointEight = Real48.FromHex("816666666666"); // O6:072E
    private static readonly Real48 PointSeven = Real48.FromHex("803333333333");  // O6:07C6

    /// <summary>Whether the impact of <paramref name="weapon"/> is ported: Lead Ball or an explosive.</summary>
    public static bool IsSupported(int weapon) => weapon == 0 || ExplosionRadius(weapon) > 0;

    /// <summary>
    /// Radius DS:47D8 of the explosives. [ORIGINAL] set O6:0000 = {1, 2, 3, 4, 7, 11} and O6:01A4–01E6: 10, 15, 30, 60,
    /// 15 (MIRV), 2 (Hand Grenade); 0 for every other weapon.
    /// </summary>
    public static int ExplosionRadius(int weapon) => weapon switch
    {
        1 => 10,
        2 or 7 => 15,
        3 => 30,
        4 => 60,
        11 => 2,
        _ => 0,
    };

    /// <summary>Runs O6:00E0(shooter, x, y, colour) for the weapon of <paramref name="shooter"/>.</summary>
    public static void Run(Playfield.Playfield field, PlayfieldTank shooter, int color, Real48 x, Real48 y,
        ShotSettings settings, TurboPascalRandom random)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(shooter);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        if (shooter.Weapon == 0)
        {
            // O6:010F–017D (Q10): 5 damage on a tank, 10 points per energy lost for another team.
            if (color is >= 20 and <= 29)
                HitTank(field, shooter, field.Tanks[color - 20], 5, random);
            return;
        }

        var radius = ExplosionRadius(shooter.Weapon);
        if (radius == 0)
            throw new NotSupportedException($"Impact of weapon {shooter.Weapon} is not ported.");
        Explode(field, shooter, color, x, y, radius, settings.Sound, random);
    }

    private static void Explode(Playfield.Playfield field, PlayfieldTank shooter, int color, Real48 x, Real48 y,
        int radius, bool sound, TurboPascalRandom random)
    {
        var screen = field.Screen.Pixels.AsSpan();
        var step = 63 / radius;

        // O6:01F7–0249 (K1): red ramp 130 + j = (R − j) · step.
        var ramp = new byte[radius + 1];
        for (var j = 0; j <= radius; j++)
            ramp[j] = (byte)((radius - j) * step);
        SetPal(field, ramp);

        // O6:0265–035C: centre relative to the field window; three rings per radius.
        x -= Eleven;
        y -= ThirtyOne;
        var cx = (short)x.Trunc();
        var cy = (short)y.Trunc();
        for (var i = 0; i <= radius; i++)
        {
            if (sound)
                random.Next(500);
            var c = (byte)(ExplosionColor + i);
            VgaDraw.Ellipse(screen, Field, cx + 1, cy, i, i, c);
            VgaDraw.Ellipse(screen, Field, cx - 1, cy, i, i, c);
            VgaDraw.Ellipse(screen, Field, cx, cy, i, i, c);
        }

        // O6:035E–051B: three runs over the ramp (each step SetPal and Delay(ES), tones only with sound).
        for (var i = 0; i <= radius; i++)
        {
            for (var j = i; j <= radius; j++)
            {
                if (sound)
                    random.Next(500);
                ramp[j] = (byte)((radius - j + i) * step);
            }

            SetPal(field, ramp);
        }

        for (var i = 0; i <= radius; i++)
        {
            for (var j = i; j >= 0; j--)
            {
                if (sound)
                    random.Next(500);
                ramp[j] = (byte)((radius - i + j) * step);
            }

            SetPal(field, ramp);
        }

        for (var i = 0; i <= radius; i++)
        {
            for (var j = i; j <= radius; j++)
            {
                if (sound)
                    random.Next(250);
                ramp[j] = (byte)((j - i) * step);
            }

            SetPal(field, ramp);
        }

        // O6:0522–058C (K2): crater, the same rings copied from the background buffer.
        var background = field.Background.Pixels;
        for (var i = 0; i <= radius; i++)
        {
            VgaDraw.CopyEllipse(background, screen, Field, cx + 1, cy, i, i);
            VgaDraw.CopyEllipse(background, screen, Field, cx - 1, cy, i, i);
            VgaDraw.CopyEllipse(background, screen, Field, cx, cy, i, i);
        }

        // O6:058E–05FC (K3): direct hit.
        if (color is >= 20 and <= 29)
            HitTank(field, shooter, field.Tanks[color - 20], 200, random);

        // O6:0616–084B (K4): area damage on every active record, distance with the Real48 of the original.
        x += Eleven;
        y += ThirtyOne;
        var r = Real48.FromInt32(radius);
        foreach (var tank in field.Tanks)
        {
            if (!tank.Active)
                continue;
            var damage = 0;
            var d = Real48.Distance(x - Real48.FromInt32(tank.X), y - Real48.FromInt32(tank.Y));
            if (Real48.Compare(r * OneAndHalf, d) >= 0)
                damage += (r * OnePointEight - d).Trunc();
            if (Real48.Compare(r, d) > 0)
                damage = damage + 10 + (r * OnePointEight - d).Trunc() * 2;
            if (Real48.Compare(r * PointSeven, d) > 0)
                damage += 150;
            HitTank(field, shooter, tank, (short)damage, random);
        }
    }

    /// <summary>O10:0000 with shield, then O10:018B for the shooter if the tank belongs to another team.</summary>
    private static void HitTank(Playfield.Playfield field, PlayfieldTank shooter, PlayfieldTank target, int amount,
        TurboPascalRandom random)
    {
        var lost = Damage.Apply(field, target, amount, Damage.Shield, random);
        var points = unchecked((short)(lost * 10));
        if (target.Team != shooter.Team)
            Damage.AddPoints(shooter, points);
    }

    /// <summary>SetPal(130, R + 1, buffer DS:E5C4): only the red component is set, green and blue stay 0.</summary>
    private static void SetPal(Playfield.Playfield field, byte[] ramp)
    {
        for (var j = 0; j < ramp.Length; j++)
            field.Palette.Set(ExplosionColor + j, ramp[j], 0, 0);
    }
}
