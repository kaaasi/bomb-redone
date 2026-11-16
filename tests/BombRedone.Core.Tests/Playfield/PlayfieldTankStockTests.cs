using BombRedone.Core.Economy;
using BombRedone.Core.Numerics;
using BombRedone.Core.Shot;

namespace BombRedone.Core.Tests.Playfield;

/// <summary>Stock of a tank: taken from the account at the start of the round and given back at its end (spur waffen).</summary>
public sealed class PlayfieldTankStockTests
{
    private static Core.Playfield.Playfield Field() =>
        Core.Playfield.Playfield.Build(new Core.Playfield.PlayfieldOptions(2), new TurboPascalRandom(0x12345678));

    [Fact]
    public void Stock_goes_from_the_account_to_the_tank_and_back()
    {
        var account = new PlayerAccount(1, "Player 1");
        account.SetStock(ItemKind.Weapon, 1, 30);
        account.SetStock(ItemKind.Weapon, 5, 400);
        account.SetStock(ItemKind.Guidance, 0, 2);
        account.SetStock(ItemKind.Guidance, 1, 3);
        account.SetStock(ItemKind.Defense, 0, 1000);
        account.SetStock(ItemKind.Defense, 2, 4);
        var tank = Field().Tanks[0];

        tank.TakeStock(account);

        Assert.Equal((30, 400, 2, 3), (tank.Stock[1], tank.Stock[5], tank.Herbs, tank.Vicks));
        Assert.Equal([1000, 0, 4], tank.Defense);

        tank.Stock[1] = 29;
        tank.Herbs = 1;
        tank.Defense[0] = 600;
        tank.ReturnStock(account);

        Assert.Equal((29, 1, 3, 600), (account.Stock(ItemKind.Weapon, 1), account.Stock(ItemKind.Guidance, 0),
            account.Stock(ItemKind.Guidance, 1), account.Stock(ItemKind.Defense, 0)));
    }

    [Fact]
    public void A_shot_uses_one_unit_of_the_weapon_and_of_each_guidance()
    {
        var field = Field();
        var tank = field.Tanks[0];
        tank.Aim(45, 262);
        tank.Weapon = 1;
        tank.Stock[1] = 30;
        tank.Guidance = 3;
        tank.GuidanceTarget = 2;
        (tank.Herbs, tank.Vicks) = (2, 5);

        WeaponShot.Fire(field, tank, new ShotSettings(10, 0), new TurboPascalRandom(1));

        Assert.Equal((29, 1, 4), (tank.Stock[1], tank.Herbs, tank.Vicks));
    }

    [Fact]
    public void The_laser_takes_its_power_from_the_stock()
    {
        var field = Field();
        var tank = field.Tanks[0];
        tank.Aim(45, 500);
        tank.Weapon = 5;
        tank.Stock[5] = 120;

        WeaponShot.Fire(field, tank, new ShotSettings(10, 0), new TurboPascalRandom(1));

        Assert.Equal((120, 0), (tank.Power, tank.Stock[5]));
    }
}
