// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести тунелів між кімнатами (роль 04): де можна прокласти, ціна, роботи будують, «з'єднано».</summary>
public class TunnelTests
{
    const string CostsJson = @"{
        ""regolith"": { ""type"": ""RegolithShield"", ""buildRobotHours"": 12, ""materials"": 0 },
        ""tunnel"":   { ""type"": ""Tunnel"", ""buildRobotHours"": 6, ""materials"": 10 },
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
        s.Res.Materials = 50f;
        s.Robots.Add(new RobotState());
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 0 });
        s.Modules.Add(new ModuleState { Type = ModuleType.Lab, SlotId = 1 });
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    [Test]
    public void CanConnect_Rules()
    {
        Assert.IsNull(BaseGrid.CanConnect(s, 0));                                    // житло ↔ лабораторія
        Assert.AreEqual("error.tunnel_rooms", BaseGrid.CanConnect(s, 1));            // праворуч від лабораторії порожньо
        Assert.AreEqual("error.tunnel_place", BaseGrid.CanConnect(s, BaseGrid.Width - 1)); // край ряду
        s.Modules.Add(new ModuleState { Type = ModuleType.Solar, SlotId = 2 });
        Assert.AreEqual("error.tunnel_rooms", BaseGrid.CanConnect(s, 1));            // панель — не кімната
        s.FindModule(1).BuildHoursLeft = 5f;
        Assert.AreEqual("error.tunnel_not_built", BaseGrid.CanConnect(s, 0));
    }

    [Test]
    public void Order_TakesMaterials_AndRobotGoes()
    {
        Assert.IsNull(build.OrderTunnel(s, 0));
        Assert.AreEqual(40f, s.Res.Materials, 0.01f);
        Assert.AreEqual(1, s.Tunnels.Count);
        Assert.AreEqual(RobotTask.Tunnel, s.Robots[0].Task);
        Assert.AreEqual(0, s.Robots[0].TargetSlot);
        Assert.AreEqual("error.tunnel_exists", build.OrderTunnel(s, 0));
    }

    [Test]
    public void Order_NoMaterials_Refused()
    {
        s.Res.Materials = 5f;
        Assert.AreEqual("error.no_materials", build.OrderTunnel(s, 0));
        Assert.AreEqual(0, s.Tunnels.Count);
    }

    [Test]
    public void Robots_BuildIt_ThenRoomsAreConnected()
    {
        build.OrderTunnel(s, 0);
        Assert.IsFalse(s.HasBuiltTunnel(0, 1));
        for (int h = 0; h < 6; h++) build.Tick(s, 1f, r);
        Assert.IsTrue(s.FindTunnel(0).IsBuilt);
        Assert.IsTrue(s.HasBuiltTunnel(1, 0));                                       // порядок не важливий
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.tunnel_built"));
        Assert.AreEqual(RobotTask.Idle, s.Robots[0].Task);                           // робот вільний
    }

    [Test]
    public void HasBuiltTunnel_FalseForNonNeighbours()
    {
        Assert.IsFalse(s.HasBuiltTunnel(0, 2));
        Assert.IsFalse(s.HasBuiltTunnel(0, 1));
    }
}
