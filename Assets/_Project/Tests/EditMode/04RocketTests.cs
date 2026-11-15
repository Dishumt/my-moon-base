// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести вибору ракети, нових вантажів і наборів модулів (роль 04).</summary>
public class RocketTests
{
    SupplySystem sup;
    GameState s;
    DayReport r;
    LaunchData data;

    [SetUp]
    public void Setup()
    {
        data = new LaunchData { supplyEveryDays = 4, launchHour = 8, flightHours = 12, failedDelayDays = 3,
                                commsOpenHours = 30, commsClosedHours = 18, startFuelKg = 3000f };
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        cat.items.Add(new CargoItemData { id = "oxygen", massKg = 80f, oxygen = 20f });
        cat.items.Add(new CargoItemData { id = "kit_greenhouse", group = "modules", massKg = 190f, materials = 15f, module = "Greenhouse" });
        cat.items.Add(new CargoItemData { id = "kit_bad", group = "modules", massKg = 10f, materials = 5f, module = "NoSuchModule" });

        var rockets = new RocketCatalog { defaultId = "medium" };
        rockets.items.Add(new RocketData { id = "small", emptyMassKg = 600f, maxFuelKg = 3500f, flightHours = 8 });
        rockets.items.Add(new RocketData { id = "medium", emptyMassKg = 1000f, maxFuelKg = 6000f, flightHours = 12 });
        rockets.items.Add(new RocketData { id = "heavy", emptyMassKg = 2000f, maxFuelKg = 10000f, flightHours = 24 });

        sup = new SupplySystem(data, cat, rockets) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    [Test]
    public void NoRocketChosen_UsesDefault()
    {
        Assert.AreEqual("medium", sup.GetRocket(s).id);
        Assert.AreEqual(6000f, sup.MaxFuel(s), 0.01f);
    }

    [Test]
    public void HeavyRocket_IsHeavierItself()
    {
        Assert.IsNull(sup.SetRocket(s, "heavy"));
        var res = sup.Evaluate(s, false);
        Assert.AreEqual(2000f, res.TotalMassKg, 0.01f);      // порожня важка ракета
        Assert.AreEqual(3000f, res.FuelNeededKg, 0.01f);     // 1.5 × 2000
    }

    [Test]
    public void SmallerTank_CutsFuel()
    {
        sup.SetRocket(s, "heavy");
        sup.SetFuel(s, 9000f);
        sup.SetRocket(s, "small");
        Assert.AreEqual(3500f, sup.GetFuel(s), 0.01f);       // більше не влазить у малий бак
        sup.SetFuel(s, 99999f);
        Assert.AreEqual(3500f, sup.GetFuel(s), 0.01f);
    }

    [Test]
    public void UnknownRocket_IsError()
    {
        Assert.AreEqual("error.unknown_rocket", sup.SetRocket(s, "ufo"));
        Assert.AreEqual("medium", sup.GetRocket(s).id);
    }

    [Test]
    public void Supply_FlightTime_FromRocket()
    {
        sup.LaunchFirstMission(s); // порожня перша місія: зелена, ставить розклад
        sup.SetRocket(s, "heavy");
        sup.SetFuel(s, 10000f);
        Assert.IsNull(sup.Send(s));
        int launchAt = s.Supply.NextLaunchAt;
        s.Day = launchAt / 24 + 1;
        s.Hour = launchAt % 24;
        sup.Tick(s, 1f, r);
        Assert.AreEqual(SupplyStatus.Launched, s.Supply.Status);
        Assert.AreEqual(launchAt + 24, s.Supply.ArrivalAt);   // важка летить 24 год
    }

    [Test]
    public void Oxygen_IsDelivered()
    {
        sup.ChangeCargo(s, "oxygen", 2);
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual(40f, s.Res.Oxygen, 0.01f);
    }

    [Test]
    public void Kit_StartsBuilding_NextToBase()
    {
        sup.Construction = new ConstructionSystem(new PowerSystem());
        s.Robots.Add(new RobotState());
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 14 });

        sup.ChangeCargo(s, "kit_greenhouse", 1);
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));

        var gh = s.Modules.Find(m => m.Type == ModuleType.Greenhouse);
        Assert.IsNotNull(gh, "теплиця має з'явитися в списку модулів");
        Assert.AreEqual(8, gh.SlotId, "перший вільний квадратик поруч із модулем 14");
        Assert.Greater(gh.BuildHoursLeft, 0f);
    }

    [Test]
    public void Kit_WithoutRobots_LeavesMaterials()
    {
        sup.Construction = new ConstructionSystem(new PowerSystem());
        sup.ChangeCargo(s, "kit_greenhouse", 1);
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual(0, s.Modules.Count);
        Assert.AreEqual(15f, s.Res.Materials, 0.01f);
    }

    [Test]
    public void Kit_WithWrongModuleName_IsJustMaterials()
    {
        var item = new CargoItemData { module = "NoSuchModule" };
        Assert.IsFalse(item.TryGetModule(out _));
        Assert.IsTrue(new CargoItemData { module = "Lab" }.TryGetModule(out var t));
        Assert.AreEqual(ModuleType.Lab, t);
    }

    [Test]
    public void PickSlot_EmptyBase_IsFirstSlot()
    {
        Assert.AreEqual(0, SupplySystem.PickSlot(s));
    }
}
