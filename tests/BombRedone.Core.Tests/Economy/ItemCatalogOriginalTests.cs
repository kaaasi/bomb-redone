using BombRedone.Core.Economy;
using BombRedone.Tests.Support;

namespace BombRedone.Core.Tests.Economy;

public class ItemCatalogOriginalTests
{
    [GameDataFact]
    public void BuiltInTableEqualsTheTableInBombExe()
    {
        var exe = File.ReadAllBytes(TestGameData.Load().Require("BOMB.EXE"));
        var parsed = ItemCatalog.Parse(exe);
        Assert.Equal(ItemCatalog.Original.Items, parsed.Items);
    }

    [GameDataFact]
    public void ManualNamesEighteenWeaponsTwoGuidanceAndThreeDefenses()
    {
        var doc = File.ReadAllText(TestGameData.Load().Require("BOMB.DOC"));
        Assert.Contains("18 weapons, 2 guidance", doc);
        Assert.Contains("systems, and 3 defenses", doc);
        var parsed = ItemCatalog.Parse(File.ReadAllBytes(TestGameData.Load().Require("BOMB.EXE")));
        Assert.Equal((18, 2, 3), (parsed.Weapons.Count, parsed.Guidance.Count, parsed.Defenses.Count));
    }
}
