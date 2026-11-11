using System.Text;

namespace BombRedone.Core.Economy;

/// <summary>
/// The weapon and equipment table: 18 weapons, 2 guidance systems, 3 defense systems (BOMB.DOC, "WEAPONS": "There are
/// currently 18 weapons, 2 guidance systems, and 3 defenses available for purchase").
/// </summary>
/// <remarks>
/// [ORIGINAL] Typed constants in the data segment 14D5 of BOMB.EXE (DS:0000 = file offset 0x163F0). Per group four
/// parallel arrays: long names (string[255], 256 bytes each), short names (string[255]), prices (LongInt) and bundle
/// amounts (Word). The shop O3:0E1F reads exactly these arrays (<c>[di+2C7C]</c>, <c>[di+2CC4]</c>, <c>[di+30E8]</c>,
/// <c>[di+30F0]</c>, <c>[di+36F4]</c>, <c>[di+3700]</c>, BOMB.OVR 0x098AB ff.) and draws the short names
/// (<c>[di+1A7C]</c>, <c>[di+2EE8]</c>, BOMB.OVR 0x08E98, 0x0901F).
/// </remarks>
public sealed class ItemCatalog
{
    /// <summary>File offset of DS:0000 in BOMB.EXE (segment 14D5, load image at 0x16A0).</summary>
    public const int DataSegmentFileOffset = 0x163F0;

    /// <summary>Location of the four arrays of one group in the data segment.</summary>
    /// <param name="Count">Number of items.</param>
    /// <param name="Names">DS offset of the long names.</param>
    /// <param name="ShortNames">DS offset of the short names.</param>
    /// <param name="Prices">DS offset of the prices (LongInt).</param>
    /// <param name="Amounts">DS offset of the bundle amounts (Word).</param>
    public readonly record struct TableLayout(int Count, int Names, int ShortNames, int Prices, int Amounts);

    /// <summary>[ORIGINAL] BOMB.EXE 0x16C6C (names), 0x17E6C (short names), 0x1906C (prices), 0x190B4 (amounts).</summary>
    public static readonly TableLayout WeaponTable = new(18, 0x087C, 0x1A7C, 0x2C7C, 0x2CC4);

    /// <summary>[ORIGINAL] BOMB.EXE 0x190D8, 0x192D8, 0x194D8, 0x194E0.</summary>
    public static readonly TableLayout GuidanceTable = new(2, 0x2CE8, 0x2EE8, 0x30E8, 0x30F0);

    /// <summary>[ORIGINAL] BOMB.EXE 0x194E4, 0x197E4, 0x19AE4, 0x19AF0.</summary>
    public static readonly TableLayout DefenseTable = new(3, 0x30F4, 0x33F4, 0x36F4, 0x3700);

    private const int NameSize = 256;

    /// <summary>
    /// The table of release 3.2 as built-in values. [ORIGINAL] read from the arrays above (BOMB.EXE, offsets per row
    /// in <see cref="WeaponTable"/>, <see cref="GuidanceTable"/>, <see cref="DefenseTable"/>); the test
    /// <c>ItemCatalogOriginalTests</c> compares it with the file. The spellings ("Ball o Compresed Dirt",
    /// "Absorbtion") are those of the original.
    /// </summary>
    public static ItemCatalog Original { get; } = new(
    [
        new(ItemKind.Weapon, 0, "Lead Ball", "Lead Ball", -1, 0),
        new(ItemKind.Weapon, 1, "Incinerator", "Incinerator", 3000, 100),
        new(ItemKind.Weapon, 2, "Mark II Incinerator", "Mark II Incin.", 4000, 10),
        new(ItemKind.Weapon, 3, "20 Kiloton Nuke", "20 Kilo Nuke", 7000, 2),
        new(ItemKind.Weapon, 4, "5 Megaton Nuke", "5 Meg Nuke", 10000, 1),
        new(ItemKind.Weapon, 5, "Laser Blaster", "Laser", 10000, 1000),
        new(ItemKind.Weapon, 6, "Chain Reaction Inducer 4/D", "CRI 4/D", 5000, 5),
        new(ItemKind.Weapon, 7, "MIRV (5 Mark IIs inside)", "MIRV", 8500, 3),
        new(ItemKind.Weapon, 8, "Chain Reaction Inducer 8/D", "CRI 8/D", 10000, 3),
        new(ItemKind.Weapon, 9, "Chain Reaction Inducer 20/ND", "CRI 20/ND", 10000, 3),
        new(ItemKind.Weapon, 10, "Chain Reaction Inducer 20/D", "CRI 20/D", 15000, 1),
        new(ItemKind.Weapon, 11, "Hand Grenade", "Hand Grenade", -1, 0),
        new(ItemKind.Weapon, 12, "Sonic Blaster", "Sonic Blaster", 4500, 10),
        new(ItemKind.Weapon, 13, "Ball o Dirt", "Ball 'O' Dirt", 4000, 10),
        new(ItemKind.Weapon, 14, "Ball o Dirt Mark II", "Mark II Dirt", 6000, 7),
        new(ItemKind.Weapon, 15, "Ball o Compresed Dirt", "Pressured Dirt", 8000, 3),
        new(ItemKind.Weapon, 16, "Ball o Super-Compressed Dirt", "High Pres. Dirt", 10000, 1),
        new(ItemKind.Weapon, 17, "Explosive Dirt", "Xplosive Dirt", 4500, 10),
        new(ItemKind.Guidance, 0, "Herb's Guidance System", "Herb's Guide", 20000, 5),
        new(ItemKind.Guidance, 1, "Vick's Guidance System", "Vick's Guide", 20000, 5),
        new(ItemKind.Defense, 0, "Energy Absorbtion Shield", "Energy Shield", 20000, 1000),
        new(ItemKind.Defense, 1, "Inertia Dampener", "Inertia Damp.", 10000, 100),
        new(ItemKind.Defense, 2, "Repulser", "Repulser", 15000, 1),
    ]);

    public ItemCatalog(IReadOnlyList<Item> items)
    {
        var all = items;
        Weapons = [.. all.Where(i => i.Kind == ItemKind.Weapon).OrderBy(i => i.Index)];
        Guidance = [.. all.Where(i => i.Kind == ItemKind.Guidance).OrderBy(i => i.Index)];
        Defenses = [.. all.Where(i => i.Kind == ItemKind.Defense).OrderBy(i => i.Index)];
        Items = [.. Weapons, .. Guidance, .. Defenses];
    }

    /// <summary>All items in shop order: weapons, guidance systems, defense systems.</summary>
    public IReadOnlyList<Item> Items { get; }

    public IReadOnlyList<Item> Weapons { get; }

    public IReadOnlyList<Item> Guidance { get; }

    public IReadOnlyList<Item> Defenses { get; }

    /// <summary>Item of a group by its original index.</summary>
    public Item Get(ItemKind kind, int index) => kind switch
    {
        ItemKind.Weapon => Weapons[index],
        ItemKind.Guidance => Guidance[index],
        ItemKind.Defense => Defenses[index],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Reads the table from the bytes of BOMB.EXE (no file access here; the caller loads the file).
    /// </summary>
    /// <exception cref="FormatException">The file is too short for the table.</exception>
    public static ItemCatalog Parse(ReadOnlySpan<byte> bombExe)
    {
        var items = new List<Item>();
        Read(bombExe, ItemKind.Weapon, WeaponTable, items);
        Read(bombExe, ItemKind.Guidance, GuidanceTable, items);
        Read(bombExe, ItemKind.Defense, DefenseTable, items);
        return new ItemCatalog(items);
    }

    private static void Read(ReadOnlySpan<byte> exe, ItemKind kind, TableLayout table, List<Item> items)
    {
        var end = DataSegmentFileOffset + table.Amounts + 2 * table.Count;
        if (exe.Length < end)
            throw new FormatException($"BOMB.EXE is too short for the {kind} table ({exe.Length} < {end} bytes).");

        for (var i = 0; i < table.Count; i++)
        {
            var name = PascalString(exe, DataSegmentFileOffset + table.Names + NameSize * i);
            var shortName = PascalString(exe, DataSegmentFileOffset + table.ShortNames + NameSize * i);
            var price = BitConverter.ToInt32(exe.Slice(DataSegmentFileOffset + table.Prices + 4 * i, 4));
            var amount = BitConverter.ToUInt16(exe.Slice(DataSegmentFileOffset + table.Amounts + 2 * i, 2));
            items.Add(new Item(kind, i, name, shortName, price, amount));
        }
    }

    private static string PascalString(ReadOnlySpan<byte> data, int offset) =>
        Encoding.Latin1.GetString(data.Slice(offset + 1, data[offset]));
}
