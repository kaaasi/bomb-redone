using BombRedone.Core.Graphics;
using BombRedone.Core.Numerics;
using BombRedone.Core.Playfield;

namespace BombRedone.Core.Shot;

/// <summary>
/// Impact of a ballistic weapon. [ORIGINAL] O6:00E0 (BOMB.OVR 0x1900C), docs/analyse/runde.md sections 5.5 and 5.6:
/// Lead Ball (Q10), the explosives {1, 2, 3, 4, 7, 11} (K1–K4), CRI {6, 8, 9, 10} (K5), Sonic Blaster 12 (K6), Dirt
/// {13..16} (K7) and Xplosive Dirt 17 (K8): every weapon that reaches O6.
/// </summary>
public static partial class Impact
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
    private static readonly Real48 Two = Real48.FromHex("820000000000");         // O6:127F
    private static readonly Real48 Hundred = Real48.FromHex("870000000048");     // O6:1493

    /// <summary>Colour of the terrain.</summary>
    private const byte Terrain = 2;

    /// <summary>[ORIGINAL] O8:09F7 (BOMB.OVR 0x1CAB7): the playfield sets SetAspectRatio(1, 1), so circles are round.</summary>
    private const int Aspect = 1;

    /// <summary>Whether the impact of <paramref name="weapon"/> is ported: Lead Ball or an explosive.</summary>
    public static bool IsSupported(int weapon) => weapon is 0 or (>= 12 and <= 17) || IsCri(weapon) || ExplosionRadius(weapon) > 0;

    /// <summary>
    /// Radius DS:47D8 of the Dirt balls. [ORIGINAL] O6:0FE2–1011 (BOMB.OVR 0x19F0E): 10, 15, 30, 60 for 13..16.
    /// </summary>
    public static int DirtRadius(int weapon) => weapon switch
    {
        13 => 10,
        14 => 15,
        15 => 30,
        16 => 60,
        _ => 0,
    };

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
        field.Trace?.Impact(shooter, color, x, y);
        if (shooter.Weapon == 0)
        {
            // O6:010F–017D (Q10): 5 damage on a tank, 10 points per energy lost for another team.
            if (color is >= 20 and <= 29)
                HitTank(field, shooter, field.Tanks[color - 20], 5, random);
            return;
        }

        if (ExplosionRadius(shooter.Weapon) == 0 && IsCri(shooter.Weapon))
        {
            Cri(field, shooter, color, x, y, settings, random);
            return;
        }

        if (shooter.Weapon == 12)
        {
            Sonic(field, x, y, settings, random);
            return;
        }

        if (DirtRadius(shooter.Weapon) > 0)
        {
            Dirt(field, x, y, DirtRadius(shooter.Weapon));
            return;
        }

        if (shooter.Weapon == 17)
        {
            XplosiveDirt(field, x, y, settings, random);
            return;
        }

        var radius = ExplosionRadius(shooter.Weapon);
        if (radius == 0)
            throw new NotSupportedException($"Impact of weapon {shooter.Weapon} is not ported.");
        Explode(field, shooter, color, x, y, radius, settings.Sound, random, settings.Frame);
    }

    private static void Explode(Playfield.Playfield field, PlayfieldTank shooter, int color, Real48 x, Real48 y,
        int radius, bool sound, TurboPascalRandom random, Action<ShotFrame>? frame)
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

        frame?.Invoke(ShotFrame.Explosion);

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
            frame?.Invoke(ShotFrame.Palette);
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
            frame?.Invoke(ShotFrame.Palette);
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
            frame?.Invoke(ShotFrame.Palette);
        }

        // O6:0522–058C (K2): crater, the same rings copied from the background buffer.
        var background = field.Background.Pixels;
        for (var i = 0; i <= radius; i++)
        {
            VgaDraw.CopyEllipse(background, screen, Field, cx + 1, cy, i, i);
            VgaDraw.CopyEllipse(background, screen, Field, cx - 1, cy, i, i);
            VgaDraw.CopyEllipse(background, screen, Field, cx, cy, i, i);
        }

        frame?.Invoke(ShotFrame.Crater);

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

    /// <summary>
    /// Dirt ball. [ORIGINAL] O6:1014–1178 (BOMB.OVR 0x19F40): SetColor(2), SetViewPort(11, 31, 309, 189, ClipOn), the
    /// impact moved into the window (x − 11, y − 31 in Real48), then for i = 0..R three Graph circles of radius i around
    /// (Trunc(x + 1), Trunc(y)), (Trunc(x − 1), Trunc(y)) and (Trunc(x), Trunc(y)). No damage, no Random.
    /// </summary>
    private static void Dirt(Playfield.Playfield field, Real48 x, Real48 y, int radius)
    {
        x -= Eleven;
        y -= ThirtyOne;
        var cy = (short)y.Trunc();
        var right = (short)(x + Real48.One).Trunc();
        var left = (short)(x - Real48.One).Trunc();
        var middle = (short)x.Trunc();
        var screen = field.Screen.Pixels.AsSpan();
        for (var i = 0; i <= radius; i++)
        {
            BgiDraw.Circle(screen, IndexBuffer.Width, IndexBuffer.Height, Field, right, cy, i, Terrain, Aspect, Aspect);
            BgiDraw.Circle(screen, IndexBuffer.Width, IndexBuffer.Height, Field, left, cy, i, Terrain, Aspect, Aspect);
            BgiDraw.Circle(screen, IndexBuffer.Width, IndexBuffer.Height, Field, middle, cy, i, Terrain, Aspect, Aspect);
        }
    }

    /// <summary>
    /// Xplosive Dirt. [ORIGINAL] O6:1197–14BC (BOMB.OVR 0x1A0C3): m = Random(100) + 300 grains, k = Random(8) + 3 rings;
    /// first all positions (ring i, grain j: r = Random(20 · i), x = Trunc(Random(r + 1) − r / 2) + Trunc(x), y = Trunc(y)
    /// − r + 2, window coordinates), then all grains PutPixel colour 2 in the viewport 11..309 × 31..189, with sound
    /// Random(200) per grain. At the end Random:Real · 100 &lt; 2 · Crumble → landslide U431:0496.
    /// </summary>
    private static void XplosiveDirt(Playfield.Playfield field, Real48 x, Real48 y, ShotSettings settings,
        TurboPascalRandom random)
    {
        var grains = random.Next(100) + 300;
        var rings = random.Next(8) + 3;
        x -= Eleven;
        y -= ThirtyOne;
        var px = new short[rings + 1, grains + 1];
        var py = new short[rings + 1, grains + 1];
        for (var i = 1; i <= rings; i++)
        {
            for (var j = 1; j <= grains; j++)
            {
                var r = (short)random.Next((ushort)(20 * i));
                var half = Real48.FromInt32(r) / Two;
                var dx = (short)(Real48.FromInt32(random.Next((ushort)(r + 1))) - half).Trunc();
                px[i, j] = (short)(dx + x.Trunc());
                py[i, j] = (short)(y.Trunc() - r + 2);
            }
        }

        var screen = field.Screen.Pixels.AsSpan();
        for (var i = 1; i <= rings; i++)
        {
            for (var j = 1; j <= grains; j++)
            {
                BgiDraw.PutPixel(screen, IndexBuffer.Width, IndexBuffer.Height, Field, px[i, j], py[i, j], Terrain);
                if (settings.Sound)
                    random.Next(200);
            }
        }

        settings.Frame?.Invoke(ShotFrame.Explosion);
        if (Real48.Compare(random.NextReal() * Hundred, Real48.FromInt32(2 * settings.Crumble)) < 0)
        {
            AfterShot.Landslide(field.Screen.Pixels, field.Background.Pixels);
            settings.Frame?.Invoke(ShotFrame.Landslide);
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
