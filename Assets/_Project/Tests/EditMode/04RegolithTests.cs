// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести засипання реголітом (роль 04): тільки кімнати, поступ для насипу в світі й відсотків у планшеті.</summary>
public class RegolithTests
{
    const string CostsJson = @"{
        ""regolith"": { ""type"": ""RegolithShield"", ""buildRobotHours"": 12, ""materials"": 0 },
        ""items"": [ { ""type"": ""Habitat"", ""buildRobotHours"": 24, ""materials"": 20 } ]
    }";

    GameState s;
    ConstructionSystem build;
    DayReport r;

    [SetUp]
    public void Setup()
    {
        var balance = new SimBalanceData
        {
            construction = new ConstructionBalance { engineerNearbySpeedMultiplier = 2f },
            robots = new RobotsBalance { powerBlocksWhenWorking = 0f }   // енергію не рахуємо: роботи працюють завжди
        };
        build = new ConstructionSystem(null, balance, BuildCostTable.FromJson(CostsJson));
        s = new GameState();
        s.Robots.Add(new RobotState());
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 0 });
        s.Modules.Add(new ModuleState { Type = ModuleType.Solar, SlotId = 1 });
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    [Test]
    public void OnlyRooms_CanBeCovered()
    {
        Assert.AreEqual("build.err.shield_rooms_only", build.ShieldWithRegolith(s, 1));   // сонячна панель
        Assert.IsNull(build.ShieldWithRegolith(s, 0));                                     // житло
    }

    [Test]
    public void Progress_GrowsWithRobotWork_ThenDone()
    {
        Assert.AreEqual(0f, build.ShieldProgress(s, 0), 0.001f);
        Assert.IsNull(build.ShieldWithRegolith(s, 0));
        Assert.IsTrue(build.IsShielding(0));

        for (int h = 0; h < 6; h++) build.Tick(s, 1f, r);
        Assert.AreEqual(0.5f, build.ShieldProgress(s, 0), 0.001f);                        // 6 з 12 годин

        for (int h = 0; h < 6; h++) build.Tick(s, 1f, r);
        Assert.IsTrue(s.FindModule(0).RegolithShield);
        Assert.IsFalse(build.IsShielding(0));
        Assert.AreEqual(1f, build.ShieldProgress(s, 0), 0.001f);
    }
}
