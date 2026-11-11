namespace BombRedone.Core.Economy;

/// <summary>The three item groups of the shop, in the order the shop lists them (BOMB.OVR 0x08BE7, O3:015B).</summary>
public enum ItemKind
{
    /// <summary>One of the 18 weapons; stock is a 16-bit count in the player record.</summary>
    Weapon = 0,

    /// <summary>One of the 2 guidance systems; stock is a 16-bit count.</summary>
    Guidance = 1,

    /// <summary>One of the 3 defense systems; stock is a 32-bit value.</summary>
    Defense = 2,
}

/// <summary>
/// One entry of the weapon and equipment table.
/// </summary>
/// <param name="Kind">Group of the item.</param>
/// <param name="Index">Index within its group (0-based, the index of the original arrays).</param>
/// <param name="Name">Long name (e.g. "Mark II Incinerator").</param>
/// <param name="ShortName">Short name shown in the shop and the weapon bar (e.g. "Mark II Incin.").</param>
/// <param name="Price">Price of one bundle in dollars; -1 for items that cannot be bought.</param>
/// <param name="Amount">Units in one bundle; 0 means the shop never offers the item.</param>
public sealed record Item(ItemKind Kind, int Index, string Name, string ShortName, int Price, int Amount)
{
    /// <summary>
    /// [ORIGINAL] O3:020B (BOMB.OVR 0x08C97), O3:0E6A (0x098F6): the shop skips every item whose bundle amount is 0,
    /// independent of the price.
    /// </summary>
    public bool ForSale => Amount != 0;

    /// <summary>
    /// The price as the purchase code uses it: the low word, sign-extended. [ORIGINAL] O3:20D2..2154 (BOMB.OVR
    /// 0x0AB5E..0x0ABE0): <c>mov ax,[price]; mov [4742],ax</c>, then <c>cwd</c> before the multiplication. All original
    /// prices are below 32768, so this equals <see cref="Price"/> for them.
    /// </summary>
    public int Price16 => (short)Price;
}
