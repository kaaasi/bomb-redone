namespace BombRedone.Core.Economy;

/// <summary>One tile of the shop: an item and the number of bundles selected but not yet bought.</summary>
public sealed class ShopOffer
{
    internal ShopOffer(Item item) => Item = item;

    public Item Item { get; }

    /// <summary>Bundles selected with + (tile record +1Ch, LongInt; 0..9).</summary>
    public int Quantity { get; internal set; }
}

/// <summary>
/// Rules of the shop between two games, without screen and input: which items are offered, selecting and deselecting
/// bundles, Purchase and Done.
/// </summary>
/// <remarks>
/// [ORIGINAL] O3:0E1F (BOMB.OVR 0x098AB) with the tile builder O3:015B (BOMB.OVR 0x08BE7). Working variables of the
/// original: <c>DS:3FE2</c> money spent (copy of record +17h, written back only on Done, O3:23C5), <c>DS:3FDA</c> money
/// of the selected but not yet bought bundles, tile records at <c>DS:3BF8 + 32 × n</c> (n = 1..count). There is no
/// selling: "-" only deselects a bundle selected before (BOMB.DOC "BUYING EQUIPMENT").
/// The computer players' decisions (U129:018D, option "Computers Can Buy") are not part of this class.
/// </remarks>
public sealed class Shop
{
    /// <summary>
    /// [ORIGINAL] O3:1D0F (BOMB.OVR 0x0A79B): <c>cmp word [es:di+1C],9; jc</c> – at most 9 bundles of one item per
    /// selection.
    /// </summary>
    public const int MaxQuantity = 9;

    private readonly ItemCatalog _catalog;
    private List<ShopOffer> _offers = [];

    public Shop(PlayerAccount account, ItemCatalog catalog)
    {
        Account = account;
        _catalog = catalog;
        Spent = account.Spent;
        BuildOffers();
    }

    public PlayerAccount Account { get; }

    /// <summary>Offered items in tile order (weapons, guidance, defenses); rebuilt after each purchase.</summary>
    public IReadOnlyList<ShopOffer> Offers => _offers;

    /// <summary>Money spent including the purchases of this visit (DS:3FE2).</summary>
    public int Spent { get; private set; }

    /// <summary>Price of the selected bundles (DS:3FDA).</summary>
    public int Pending { get; private set; }

    /// <summary>"YOU CURRENTLY HAVE:" – score minus spent. [ORIGINAL] O3:11A8 (BOMB.OVR 0x09C34).</summary>
    public int CurrentlyHave => unchecked(Account.Score - Spent);

    /// <summary>"YOU HAVE UNALLOCATED:" – score minus spent minus selected. [ORIGINAL] O3:1135 (BOMB.OVR 0x09BC1).</summary>
    public int Unallocated => unchecked(Account.Score - Spent - Pending);

    /// <summary>Set by <see cref="Done"/>; the visit is over.</summary>
    public bool Finished { get; private set; }

    /// <summary>
    /// Cheapest item for sale. [ORIGINAL] O3:0E3D..0F05 (BOMB.OVR 0x098C9): start 7FFFh, per group take the low word
    /// of every price below the current minimum whose bundle amount is not 0.
    /// </summary>
    public static int MinimumPrice(ItemCatalog catalog)
    {
        int minimum = short.MaxValue;
        foreach (var item in catalog.Items)
        {
            if (minimum > item.Price && item.ForSale)
                minimum = item.Price16;
        }

        return minimum;
    }

    /// <summary>
    /// Whether a player gets the shop after a game: money at least the cheapest price. [ORIGINAL] O3:0F65..0F90
    /// (BOMB.OVR 0x099F1): <c>score − spent &lt; minimum</c> skips the player. Computer players additionally need
    /// U129:018D and "Computers Can Buy" (O3:0F3B, not modelled here).
    /// </summary>
    public static bool Visits(PlayerAccount account, ItemCatalog catalog) => account.Money >= MinimumPrice(catalog);

    /// <summary>Whether the selected money still allows one more bundle of <paramref name="offer"/>.</summary>
    /// <remarks>
    /// [ORIGINAL] O3:121C..1326 (BOMB.OVR 0x09CA8): the cursor box is drawn in colour 0 instead of 11 if the price is
    /// greater than the unallocated money; O3:1D7B (0x0A807) uses the same comparison before selecting.
    /// </remarks>
    public bool CanAfford(ShopOffer offer) => offer.Item.Price16 <= Unallocated;

    /// <summary>
    /// "+", "=", Space or the right mouse button on a tile: selects one more bundle.
    /// [ORIGINAL] O3:1CF1..1DC2 (BOMB.OVR 0x0A77D): only below <see cref="MaxQuantity"/> and if the price (low word,
    /// sign-extended) does not exceed the unallocated money.
    /// </summary>
    /// <returns>True if the quantity changed.</returns>
    public bool Select(ShopOffer offer)
    {
        if (Finished || offer.Quantity >= MaxQuantity || !CanAfford(offer))
            return false;
        Pending = unchecked(Pending + offer.Item.Price16);
        offer.Quantity++;
        return true;
    }

    /// <summary>
    /// "-" or the left mouse button on a tile: deselects one bundle. [ORIGINAL] O3:1B33..1BE5 (BOMB.OVR 0x0A5BF): only if
    /// the quantity is above 0; subtracts the full 32-bit price.
    /// </summary>
    /// <returns>True if the quantity changed.</returns>
    public bool Deselect(ShopOffer offer)
    {
        if (Finished || offer.Quantity <= 0)
            return false;
        offer.Quantity--;
        Pending = unchecked(Pending - offer.Item.Price);
        return true;
    }

    /// <summary>
    /// "Purchase" (P or Enter): buys every selected bundle. [ORIGINAL] O3:2080..2282 (BOMB.OVR 0x0AB0C): the selected
    /// money is reset, spent grows by price × quantity, stock by amount × quantity; if anything was bought the tiles are
    /// rebuilt with the new money (O3:2269 calls O3:015B), which also resets all quantities.
    /// </summary>
    /// <returns>Number of bundles bought.</returns>
    public int Purchase() => Finished ? 0 : Buy(rebuild: true);

    /// <summary>
    /// "Done" (D or Esc): buys the selected bundles like Purchase and ends the visit; only now the spent money goes back
    /// into the player record. [ORIGINAL] O3:2064 sets DS:4751, O3:23C5 (BOMB.OVR 0x0AE51) writes DS:3FE2 to +17h.
    /// </summary>
    /// <returns>Number of bundles bought.</returns>
    public int Done()
    {
        if (Finished)
            return 0;
        var bought = Buy(rebuild: false);
        Account.Spent = Spent;
        Finished = true;
        return bought;
    }

    private int Buy(bool rebuild)
    {
        var total = 0;
        Pending = 0;
        foreach (var offer in _offers)
        {
            if (offer.Quantity <= 0)
                continue;
            var item = offer.Item;
            Spent = unchecked(Spent + item.Price16 * offer.Quantity);
            Account.AddStock(item.Kind, item.Index, unchecked(item.Amount * offer.Quantity));
            total += offer.Quantity;
        }

        if (total != 0 && rebuild)
            BuildOffers();
        return total;
    }

    /// <summary>
    /// [ORIGINAL] O3:015B (BOMB.OVR 0x08BE7): per group in index order a tile for every item that is for sale and either
    /// in stock (&gt; 0) or affordable with score − spent (without the selected money).
    /// </summary>
    private void BuildOffers()
    {
        var available = unchecked(Account.Score - Spent);
        _offers = [.. _catalog.Items
            .Where(item => (Account.Stock(item) > 0 || available >= item.Price) && item.ForSale)
            .Select(item => new ShopOffer(item))];
    }
}
