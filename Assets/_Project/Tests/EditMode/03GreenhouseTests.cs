// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Тести теплиці. Числа тут свої, не з balance.json:
/// +1 росту на блок за день, вода 1 на блок за день, урожай кожні 3 дні,
/// ріст × 2 порції, ботанік × 1.5.
/// </summary>
public class GreenhouseSystemTests
{
    private static SimBalanceData Balance()
    {
        return new SimBalanceData
        {
            power = new PowerBalance
            {
                panelBlocks = 3f, panelBlocksInShade = 1f,
                shadeHoursPerDay = 6f, batteryCapacityBlockHours = 10f
            },
            greenhouse = new GreenhouseBalance
            {
                growthPerBlockPerDay = 1f,
                harvestEveryDays = 3f,
                portionsPerGrowth = 2f,
                botanistMultiplier = 1.5f,
                waterUsePerBlockPerDay = 1f
            }
        };
    }

    private static GameState State(float water, bool moduleBuilt = true)
    {
        var s = new GameState();
        s.Res.Water = water;
        s.Res.Food = 0f;
        s.Modules.Add(new ModuleState
        {
            Type = ModuleType.Greenhouse,
            SlotId = 0,
            BuildHoursLeft = moduleBuilt ? 0f : 5f
        });
        return s;
    }

    // Система з уже виданими блоками енергії (замість справжньої PowerSystem).
    private static GreenhouseSystem Make(int grantedBlocks)
    {
        var power = new PowerSystem(Balance());
        power.Granted.Greenhouse = grantedBlocks;
        return new GreenhouseSystem(power, Balance());
    }

    // Проганяє N повних діб: 24 години + кінець дня.
    private static DayReport RunDays(GreenhouseSystem sys, GameState s, int days)
    {
        var r = new DayReport(1);
        for (int d = 0; d < days; d++)
        {
            sys.Tick(s, 24f, r);
            sys.OnDayEnd(s, r);
        }
        return r;
    }

    [Test]
    public void ThreeDays_ThreeBlocks_HarvestEighteenPortions_WaterSpent()
    {
        var s = State(60f);
        var sys = Make(3);

        var r = RunDays(sys, s, 3);

        Assert.AreEqual(18f, s.Res.Food, 0.01f); // ріст 3 × 3 дні = 9, × 2 порції
        Assert.AreEqual(51f, s.Res.Water, 0.01f); // 3 блоки × 3 дні
        Assert.AreEqual(18f, r.Lines.Find(l => l.Key == "report.harvest").Delta, 0.01f);
        Assert.AreEqual(0f, sys.Growth, 0.001f);   // ріст почався заново
    }

    [Test]
    public void NoHarvest_BeforeThirdDay()
    {
        var s = State(60f);
        var sys = Make(3);

        RunDays(sys, s, 2);

        Assert.AreEqual(0f, s.Res.Food, 0.001f);
        Assert.AreEqual(6f, sys.Growth, 0.01f);
    }

    [Test]
    public void BotanistOnGreenhouse_HarvestIsOneAndHalfTimesBigger()
    {
        var s = State(60f);
        s.Crew.Add(new Astronaut { Name = "Bot", Role = CrewRole.Botanist, Task = CrewTask.Greenhouse });
        var sys = Make(3);

        RunDays(sys, s, 3);

        Assert.AreEqual(27f, s.Res.Food, 0.01f); // 18 × 1.5
    }

    [Test]
    public void BotanistDoingSomethingElse_NoBonus()
    {
        var s = State(60f);
        s.Crew.Add(new Astronaut { Name = "Bot", Role = CrewRole.Botanist, Task = CrewTask.Rest });
        var sys = Make(3);

        RunDays(sys, s, 3);

        Assert.AreEqual(18f, s.Res.Food, 0.01f);
    }

    [Test]
    public void NoWater_NothingGrows_AndAlarmIsWritten()
    {
        var s = State(0f);
        var sys = Make(3);

        var r = RunDays(sys, s, 3);

        Assert.AreEqual(0f, s.Res.Food, 0.001f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.greenhouse_no_water" && l.Important));
    }

    [Test]
    public void LittleWater_OnlyPartOfBlocksGrow()
    {
        var s = State(1.5f); // 3 блоки хочуть 3 води, є 1.5
        var sys = Make(3);

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(1.5f, sys.Growth, 0.01f);
        Assert.AreEqual(0f, s.Res.Water, 0.01f);
    }

    [Test]
    public void NoBuiltGreenhouse_NothingGrows_WaterUntouched()
    {
        var s = State(60f, moduleBuilt: false);
        var sys = Make(3);

        var r = RunDays(sys, s, 3);

        Assert.AreEqual(0f, s.Res.Food, 0.001f);
        Assert.AreEqual(60f, s.Res.Water, 0.001f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.greenhouse_no_module"));
    }

    [Test]
    public void NoPower_NothingGrows()
    {
        var s = State(60f);
        var sys = Make(0);

        RunDays(sys, s, 3);

        Assert.AreEqual(0f, s.Res.Food, 0.001f);
        Assert.AreEqual(60f, s.Res.Water, 0.001f);
    }
}
