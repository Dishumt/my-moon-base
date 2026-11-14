// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Перевіряє, що balance.json і modules.json читаються у класи з Scripts/Simulation/Data/.</summary>
public class BalanceDataTests03
{
    [Test]
    public void Balance_LoadsNumbers()
    {
        var b = DataLoader.Load<BalanceData>("Balance/balance");
        Assert.Greater(b.realSecondsPerDay, 0f, "realSecondsPerDay");
        Assert.Greater(b.power.panelBlocks, 0f, "power.panelBlocks");
        Assert.Greater(b.lifeSupport.oxygenTankMax, 0f, "lifeSupport.oxygenTankMax");
        Assert.Greater(b.start.water, 0f, "start.water");
        Assert.Greater(b.food.mealsPerDay, 0, "food.mealsPerDay");
        Assert.Greater(b.greenhouse.harvestEveryDays, 0f, "greenhouse.harvestEveryDays");
        Assert.Greater(b.robots.count, 0, "robots.count");
    }

    [Test]
    public void Modules_EveryTypeIsValidModuleType()
    {
        var m = DataLoader.Load<ModuleCatalog>("Balance/modules");
        Assert.IsNotEmpty(m.items, "items порожній");
        foreach (var item in m.items)
            Assert.IsTrue(item.TryGetType(out _), "Невідомий type у modules.json: " + item.type);
    }

    [Test]
    public void Modules_FindGreenhouse()
    {
        var m = DataLoader.Load<ModuleCatalog>("Balance/modules");
        var g = m.Find(ModuleType.Greenhouse);
        Assert.IsNotNull(g);
        Assert.Greater(g.buildRobotHours, 0f);
    }
}
