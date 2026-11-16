using BombRedone.Core.Graphics;

namespace BombRedone.Core.Turn;

/// <summary>The colours of the weapon bar (docs/analyse/runde.md section 6.1, Z2).</summary>
public static class WeaponBar
{
    /// <summary>Field i of the bar has colour 100 + i (weapons 0..17, Herb 118, Vick 119).</summary>
    public const int ColorBase = 100, HerbColor = 118, VickColor = 119;

    /// <summary>Left edge of field i (DS:E874 + 2i = 15 i + 10; field 20 is the right end of Vick).</summary>
    public static int FieldLeft(int i) => 15 * i + 10;

    /// <summary>
    /// [ORIGINAL] O4:0000 (BOMB.OVR 0x10D05): DAC 100 + w white with stock, otherwise black, the chosen weapon red;
    /// Herb (118) and Vick (119) white with stock, otherwise black and the guidance bit is cleared; red while switched on.
    /// </summary>
    public static void SetColors(VgaPalette palette, TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(player);
        for (var w = 0; w < TurnPlayer.Weapons; w++)
        {
            if (player.Tank.Stock[w] > 0)
                palette.Set(ColorBase + w, 63, 63, 63);
            else
                palette.Set(ColorBase + w, 0, 0, 0);
            if (player.Tank.Weapon == w)
                palette.Set(ColorBase + w, 63, 0, 0);
        }

        if (player.GuidanceStock(0) > 0)
        {
            palette.Set(HerbColor, 63, 63, 63);
        }
        else
        {
            palette.Set(HerbColor, 0, 0, 0);
            player.Tank.Guidance &= 2;
        }

        if (player.GuidanceStock(1) > 0)
        {
            palette.Set(VickColor, 63, 63, 63);
        }
        else
        {
            palette.Set(VickColor, 0, 0, 0);
            player.Tank.Guidance &= 1;
        }

        GuidanceRed(palette, player);
    }

    /// <summary>[ORIGINAL] O7:070F–07C7: Herb and Vick white with stock, otherwise black (bits stay), red while on.</summary>
    public static void SetGuidanceColors(VgaPalette palette, TurnPlayer player)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(player);
        palette.Set(HerbColor, player.GuidanceStock(0) > 0 ? 63 : 0, player.GuidanceStock(0) > 0 ? 63 : 0,
            player.GuidanceStock(0) > 0 ? 63 : 0);
        palette.Set(VickColor, player.GuidanceStock(1) > 0 ? 63 : 0, player.GuidanceStock(1) > 0 ? 63 : 0,
            player.GuidanceStock(1) > 0 ? 63 : 0);
        GuidanceRed(palette, player);
    }

    private static void GuidanceRed(VgaPalette palette, TurnPlayer player)
    {
        if ((player.Tank.Guidance & 1) == 1)
            palette.Set(HerbColor, 63, 0, 0);
        if ((player.Tank.Guidance & 2) == 2)
            palette.Set(VickColor, 63, 0, 0);
    }
}

/// <summary>Who moves next (docs/analyse/runde.md section 2, R5).</summary>
public static class TurnOrder
{
    /// <summary>
    /// [ORIGINAL] O4:4770–4792 (BOMB.OVR 0x15475): player + 1, after N again 1 with pass + 1 (DS:47EA), until a record
    /// with +23h ≠ 0.
    /// </summary>
    /// <param name="active">+23h of records 1..N (index 0 = record 1); at least one must be true.</param>
    public static (int Player, int Pass) Next(int player, int pass, IReadOnlyList<bool> active)
    {
        ArgumentNullException.ThrowIfNull(active);
        if (!active.Contains(true))
            throw new ArgumentException("Needs an active record.", nameof(active));
        do
        {
            player++;
            if (player > active.Count)
            {
                player = 1;
                pass++;
            }
        }
        while (!active[player - 1]);

        return (player, pass);
    }
}
