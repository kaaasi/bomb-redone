using BombRedone.Core.Economy;

namespace BombRedone.Core.Tests.Economy;

public class ItemCatalogTests
{
    [Fact]
    public void OriginalTableHasTheGroupSizesOfTheManual()
    {
        var catalog = ItemCatalog.Original;
        Assert.Equal(18, catalog.Weapons.Count);
        Assert.Equal(2, catalog.Guidance.Count);
        Assert.Equal(3, catalog.Defenses.Count);
        Assert.Equal(23, catalog.Items.Count);
    }

    [Fact]
    public void ItemsAreInShopOrder()
    {
        var items = ItemCatalog.Original.Items;
        Assert.All(items.Take(18), i => Assert.Equal(ItemKind.Weapon, i.Kind));
        Assert.All(items.Skip(18).Take(2), i => Assert.Equal(ItemKind.Guidance, i.Kind));
        Assert.All(items.Skip(20), i => Assert.Equal(ItemKind.Defense, i.Kind));
        Assert.Equal(Enumerable.Range(0, 18), items.Take(18).Select(i => i.Index));
    }

    [Fact]
    public void OnlyLeadBallAndHandGrenadeAreNotForSale()
    {
        var notForSale = ItemCatalog.Original.Items.Where(i => !i.ForSale).Select(i => i.ShortName);
        Assert.Equal(["Lead Ball", "Hand Grenade"], notForSale);
    }

    // Shop panel of the short video, docs/anforderungen/bildschirme.md section 4 (TW 00100): name, amount, price.
    [Theory]
    [InlineData("Incinerator", 100, 3000)]
    [InlineData("Mark II Incin.", 10, 4000)]
    [InlineData("20 Kilo Nuke", 2, 7000)]
    [InlineData("5 Meg Nuke", 1, 10000)]
    [InlineData("Laser", 1000, 10000)]
    [InlineData("CRI 4/D", 5, 5000)]
    [InlineData("MIRV", 3, 8500)]
    [InlineData("CRI 8/D", 3, 10000)]
    [InlineData("CRI 20/ND", 3, 10000)]
    [InlineData("Sonic Blaster", 10, 4500)]
    [InlineData("Ball 'O' Dirt", 10, 4000)]
    [InlineData("Mark II Dirt", 7, 6000)]
    [InlineData("Pressured Dirt", 3, 8000)]
    [InlineData("High Pres. Dirt", 1, 10000)]
    [InlineData("Xplosive Dirt", 10, 4500)]
    [InlineData("Inertia Damp.", 100, 10000)]
    public void TableMatchesTheShopPanelOfTheVideo(string shortName, int amount, int price)
    {
        var item = Assert.Single(ItemCatalog.Original.Items, i => i.ShortName == shortName);
        Assert.Equal(amount, item.Amount);
        Assert.Equal(price, item.Price);
    }

    [Fact]
    public void ParseReadsPascalStringsAndNumbersAtTheTableOffsets()
    {
        var exe = SyntheticExe();
        var catalog = ItemCatalog.Parse(exe);
        var guide = catalog.Get(ItemKind.Guidance, 1);
        Assert.Equal("G1", guide.Name);
        Assert.Equal("g1", guide.ShortName);
        Assert.Equal(1234567, guide.Price);
        Assert.Equal(65535, guide.Amount);
        Assert.Equal(-1, catalog.Get(ItemKind.Weapon, 0).Price);
    }

    [Fact]
    public void ParseRejectsATooShortFile()
    {
        var exe = SyntheticExe();
        Assert.Throws<FormatException>(() => ItemCatalog.Parse(exe.AsSpan(0, exe.Length - 1)));
    }

    private static byte[] SyntheticExe()
    {
        var table = ItemCatalog.DefenseTable;
        var exe = new byte[ItemCatalog.DataSegmentFileOffset + table.Amounts + 2 * table.Count];
        var g = ItemCatalog.GuidanceTable;
        var ds = ItemCatalog.DataSegmentFileOffset;
        WriteString(exe, ds + g.Names + 256, "G1");
        WriteString(exe, ds + g.ShortNames + 256, "g1");
        BitConverter.GetBytes(1234567).CopyTo(exe, ds + g.Prices + 4);
        BitConverter.GetBytes((ushort)65535).CopyTo(exe, ds + g.Amounts + 2);
        BitConverter.GetBytes(-1).CopyTo(exe, ds + ItemCatalog.WeaponTable.Prices);
        return exe;
    }

    private static void WriteString(byte[] target, int offset, string text)
    {
        target[offset] = (byte)text.Length;
        System.Text.Encoding.Latin1.GetBytes(text).CopyTo(target, offset + 1);
    }
}
