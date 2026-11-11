namespace BombRedone.Core.Economy;

/// <summary>
/// Score, money and stock of one player: the economic part of the original player record. The player itself is only a
/// number and a name here (no dependency on the player setup).
/// </summary>
/// <remarks>
/// [ORIGINAL] Player records of 0x74 bytes at DS:402E + 0x74 × number (number 1..10; record 0 serves as swap buffer,
/// O3:0000, BOMB.OVR 0x08A8C). Fields used here, offsets within the record:
/// <list type="bullet">
/// <item><c>+13h</c> LongInt score (status screen O3:5AEF reads <c>[di+4041]</c>, BOMB.OVR 0x0E57B ff.).</item>
/// <item><c>+17h</c> LongInt money already spent; money = score − spent (shop O3:0F65, BOMB.OVR 0x099F1; BOMB.DOC
/// "BUYING EQUIPMENT": "Money is directly related to score").</item>
/// <item><c>+2Ah</c> Word[2] guidance stock, <c>+2Eh</c> Word[18] weapon stock, <c>+52h</c> LongInt[3] defense stock
/// (shop O3:015B and purchase O3:2195, 0x21D6, 0x221B; BOMB.OVR 0x08BE7, 0x0AC21, 0x0AC62, 0x0ACA7).</item>
/// </list>
/// </remarks>
public sealed class PlayerAccount
{
    private readonly short[] _weapons = new short[ItemCatalog.WeaponTable.Count];
    private readonly short[] _guidance = new short[ItemCatalog.GuidanceTable.Count];
    private readonly int[] _defenses = new int[ItemCatalog.DefenseTable.Count];

    /// <param name="number">Player number 1..10 (index of the player record).</param>
    /// <param name="name">Name shown on the screens.</param>
    public PlayerAccount(int number, string name)
    {
        if (number is < GameLimits.MinPlayers or > GameLimits.MaxPlayers)
            throw new ArgumentOutOfRangeException(nameof(number), number, "Player numbers are 1 to 10.");
        Number = number;
        Name = name;
    }

    public int Number { get; }

    public string Name { get; set; }

    /// <summary>Score (record +13h).</summary>
    public int Score { get; set; }

    /// <summary>Money spent in the shop so far (record +17h).</summary>
    public int Spent { get; set; }

    /// <summary>Money the player can spend: score minus spent, as the shop computes it (32-bit, wraps like LongInt).</summary>
    public int Money => unchecked(Score - Spent);

    /// <summary>Stock of an item: weapons and guidance as signed 16-bit counts, defenses as 32-bit values.</summary>
    public int Stock(ItemKind kind, int index) => kind switch
    {
        ItemKind.Weapon => _weapons[index],
        ItemKind.Guidance => _guidance[index],
        ItemKind.Defense => _defenses[index],
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <inheritdoc cref="Stock(ItemKind, int)"/>
    public int Stock(Item item) => Stock(item.Kind, item.Index);

    /// <summary>Sets the stock; weapon and guidance counts keep only the low 16 bits like the original Word fields.</summary>
    public void SetStock(ItemKind kind, int index, int value)
    {
        switch (kind)
        {
            case ItemKind.Weapon: _weapons[index] = unchecked((short)value); break;
            case ItemKind.Guidance: _guidance[index] = unchecked((short)value); break;
            case ItemKind.Defense: _defenses[index] = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    /// <summary>
    /// Adds to the stock with the width of the field. [ORIGINAL] O3:2195/21D6 <c>add [es:di+2E/2A],dx</c> (16 bit),
    /// O3:221B <c>add/adc [es:di+52/54]</c> (32 bit).
    /// </summary>
    public void AddStock(ItemKind kind, int index, int units) =>
        SetStock(kind, index, unchecked(Stock(kind, index) + units));
}
