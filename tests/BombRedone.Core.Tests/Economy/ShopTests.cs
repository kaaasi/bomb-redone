using BombRedone.Core.Economy;

namespace BombRedone.Core.Tests.Economy;

public class ShopTests
{
    private static readonly ItemCatalog Catalog = ItemCatalog.Original;

    private static PlayerAccount Player(int score, int spent = 0) => new(1, "Javier") { Score = score, Spent = spent };

    private static ShopOffer Offer(Shop shop, string shortName) => shop.Offers.Single(o => o.Item.ShortName == shortName);

    [Fact]
    public void MoneyIsScoreMinusSpent()
    {
        Assert.Equal(2930, Player(12930, 10000).Money);
    }

    [Fact]
    public void MinimumPriceIsTheCheapestItemForSale() => Assert.Equal(3000, Shop.MinimumPrice(Catalog));

    [Theory]
    [InlineData(2999, false)]
    [InlineData(3000, true)]
    public void PlayerVisitsTheShopOnlyWithEnoughMoney(int score, bool visits) =>
        Assert.Equal(visits, Shop.Visits(Player(score), Catalog));

    [Fact]
    public void OffersAreTheAffordableItemsForSaleInTableOrder()
    {
        var shop = new Shop(Player(5260), Catalog);
        Assert.Equal(["Incinerator", "Mark II Incin.", "CRI 4/D", "Sonic Blaster", "Ball 'O' Dirt", "Xplosive Dirt"],
            shop.Offers.Select(o => o.Item.ShortName));
    }

    [Fact]
    public void ItemsInStockAreOfferedEvenIfTooExpensive()
    {
        var player = Player(3000);
        player.SetStock(ItemKind.Defense, 2, 1);
        player.SetStock(ItemKind.Weapon, 0, 5); // Lead Ball: in stock but never for sale
        var shop = new Shop(player, Catalog);
        Assert.Equal(["Incinerator", "Repulser"], shop.Offers.Select(o => o.Item.ShortName));
    }

    [Fact]
    public void FullTableNeedsTwentyThousandDollars()
    {
        var shop = new Shop(Player(20000), Catalog);
        Assert.Equal(21, shop.Offers.Count);
    }

    [Fact]
    public void SelectReservesMoneyAndDeselectReturnsIt()
    {
        var shop = new Shop(Player(10000), Catalog);
        var incinerator = Offer(shop, "Incinerator");
        Assert.True(shop.Select(incinerator));
        Assert.True(shop.Select(incinerator));
        Assert.Equal((2, 6000, 4000, 10000), (incinerator.Quantity, shop.Pending, shop.Unallocated, shop.CurrentlyHave));
        Assert.True(shop.Deselect(incinerator));
        Assert.Equal((1, 3000), (incinerator.Quantity, shop.Pending));
    }

    [Fact]
    public void SelectStopsWhenMoneyIsUsedUp()
    {
        var shop = new Shop(Player(7000), Catalog);
        var incinerator = Offer(shop, "Incinerator");
        Assert.True(shop.Select(incinerator));
        Assert.True(shop.Select(incinerator));
        Assert.False(shop.Select(incinerator));
        Assert.False(shop.CanAfford(incinerator));
        Assert.Equal((2, 1000), (incinerator.Quantity, shop.Unallocated));
    }

    [Fact]
    public void SelectStopsAtNineBundles()
    {
        var shop = new Shop(Player(100000), Catalog);
        var incinerator = Offer(shop, "Incinerator");
        for (var i = 0; i < Shop.MaxQuantity; i++)
            Assert.True(shop.Select(incinerator));
        Assert.False(shop.Select(incinerator));
        Assert.Equal(9, incinerator.Quantity);
    }

    [Fact]
    public void DeselectBelowZeroDoesNothing()
    {
        var shop = new Shop(Player(10000), Catalog);
        Assert.False(shop.Deselect(Offer(shop, "Incinerator")));
        Assert.Equal(0, shop.Pending);
    }

    [Fact]
    public void PurchaseAddsStockAndRebuildsTheOffers()
    {
        var player = Player(10000);
        var shop = new Shop(player, Catalog);
        shop.Select(Offer(shop, "Incinerator"));
        shop.Select(Offer(shop, "Incinerator"));
        shop.Select(Offer(shop, "Mark II Incin."));

        Assert.Equal(3, shop.Purchase());

        Assert.Equal(200, player.Stock(ItemKind.Weapon, 1));
        Assert.Equal(10, player.Stock(ItemKind.Weapon, 2));
        Assert.Equal((10000, 0, 0), (shop.Spent, shop.Pending, shop.Unallocated));
        // Spent goes back into the record only on Done.
        Assert.Equal(0, player.Spent);
        // Nothing affordable left, but both bought items are in stock now.
        Assert.Equal(["Incinerator", "Mark II Incin."], shop.Offers.Select(o => o.Item.ShortName));
        Assert.All(shop.Offers, o => Assert.Equal(0, o.Quantity));
    }

    [Fact]
    public void PurchaseWithoutSelectionKeepsTheOffers()
    {
        var shop = new Shop(Player(10000), Catalog);
        var before = shop.Offers;
        Assert.Equal(0, shop.Purchase());
        Assert.Same(before, shop.Offers);
    }

    [Fact]
    public void DoneBuysTheSelectionAndWritesSpentBack()
    {
        var player = Player(30000, 5000);
        var shop = new Shop(player, Catalog);
        shop.Select(Offer(shop, "Energy Shield"));
        Assert.Equal(1, shop.Done());
        Assert.True(shop.Finished);
        Assert.Equal((25000, 5000), (player.Spent, player.Money));
        Assert.Equal(1000, player.Stock(ItemKind.Defense, 0));
        Assert.False(shop.Select(Offer(shop, "Incinerator")));
    }

    [Fact]
    public void WeaponStockWrapsLikeASixteenBitWord()
    {
        var player = Player(100000);
        player.SetStock(ItemKind.Weapon, 5, 32000);
        var shop = new Shop(player, Catalog);
        shop.Select(Offer(shop, "Laser"));
        shop.Done();
        Assert.Equal(unchecked((short)33000), player.Stock(ItemKind.Weapon, 5));
    }

    [Fact]
    public void DefenseStockIsThirtyTwoBit()
    {
        var player = Player(100000);
        player.SetStock(ItemKind.Defense, 0, 40000);
        var shop = new Shop(player, Catalog);
        shop.Select(Offer(shop, "Energy Shield"));
        shop.Done();
        Assert.Equal(41000, player.Stock(ItemKind.Defense, 0));
    }

    [Fact]
    public void PlayerNumberIsOneToTen()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerAccount(0, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerAccount(11, "x"));
    }
}
