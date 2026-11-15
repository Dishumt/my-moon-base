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

/// <summary>Тести планувальника місії: дні запасів, цілі, порада (роль 04).</summary>
public class MissionPlannerTests
{
    SupplySystem sup;
    GameState s;
    SimBalanceData bal;
    ChapterData chapter;

    [SetUp]
    public void Setup()
    {
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "food", massKg = 50f, food = 10f });
        cat.items.Add(new CargoItemData { id = "oxygen", massKg = 80f, oxygen = 20f });
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        cat.items.Add(new CargoItemData { id = "kit_habitat", massKg = 260f, materials = 20f, module = "Habitat" });
        sup = new SupplySystem(new LaunchData(), cat) { IsModuleUnlocked = t => t == ModuleType.Habitat };

        s = new GameState { Day = 1, Hour = 0 };
        s.Res.Food = 40f; s.Res.Water = 60f; s.Res.Oxygen = 70f;
        s.Power.LifeSupport = 2;

        bal = new SimBalanceData
        {
            food = new FoodBalance { portionsPerAstronautPerDay = 1f },
            lifeSupport = new LifeSupportBalance { oxygenUsePerAstronautPerDay = 4f, oxygenMadePerBlockPerDay = 4f, waterUsePerBlockPerDay = 2f, oxygenTankMax = 100f },
            greenhouse = new GreenhouseBalance { waterUsePerBlockPerDay = 1f }
        };
        chapter = new ChapterData
        {
            goals = new[]
            {
                new ChapterGoalData { type = "SurviveDays", target = 5 },
                new ChapterGoalData { type = "BuildModule", target = 1, module = "Habitat" }
            }
        };
    }

    MissionPlan Plan() => MissionPlanner.Build(s, sup, true, sup.Evaluate(s, true), bal, chapter);

    [Test]
    public void Days_FromBalance()
    {
        var p = Plan();
        Assert.AreEqual(4, p.Crew);
        // зараз 0 год; наступний корабель стартує на 4-й день о 8:00 (104 год) і летить 12 год → 116 год ≈ 4.83 дн.
        Assert.AreEqual(116f / 24f, p.DaysToNextShip, 0.01f);
        Assert.AreEqual(3, p.ReserveDays);
        Assert.AreEqual(8, p.MissionDays);   // 4.83 + 3 запасу → 8 днів
        Assert.AreEqual(10f, p.Food.Days, 0.01f);      // 40 їжі / (4 людини × 1)
        Assert.AreEqual(8.75f, p.Oxygen.Days, 0.01f);  // 70 / (16 дихають − 8 робить машина)
        Assert.AreEqual(15f, p.Water.Days, 0.01f);     // 60 / (2 блоки × 2)
    }

    [Test]
    public void Cargo_AddsDays()
    {
        sup.ChangeCargo(s, "food", 2);
        var p = Plan();
        Assert.AreEqual(20f, p.Food.Bring, 0.01f);
        Assert.AreEqual(5f, p.Food.BringDays, 0.01f);
    }

    [Test]
    public void LowFood_IsTheTip()
    {
        s.Res.Food = 4f;
        Assert.AreEqual("tip.food_low", Plan().TipKey);
    }

    [Test]
    public void MissingGoal_IsTheTip_UntilKitPacked()
    {
        sup.SetFuel(s, 6000f); // зелене світло, щоб порада була про ціль, а не про паливо
        Assert.AreEqual("tip.goal", Plan().TipKey);
        sup.ChangeCargo(s, "kit_habitat", 1);
        var p = Plan();
        Assert.IsTrue(p.Goals[0].Done);
        Assert.AreEqual("tip.all_good", p.TipKey);
    }

    [Test]
    public void OxygenOverTank_IsTheTip()
    {
        sup.ChangeCargo(s, "kit_habitat", 1);
        sup.ChangeCargo(s, "oxygen", 3); // 70 + 60 > 100
        sup.SetFuel(s, 6000f);
        Assert.AreEqual("tip.oxygen_full", Plan().TipKey);
    }

    [Test]
    public void LockedKit_CannotBePacked()
    {
        sup.Catalog.items.Add(new CargoItemData { id = "kit_lab", massKg = 130f, module = "Lab" });
        Assert.AreEqual("error.kit_locked", sup.ChangeCargo(s, "kit_lab", 1));
        Assert.IsNull(sup.ChangeCargo(s, "kit_habitat", 1));
    }

    [Test]
    public void IceMining_LowersWaterNeed()
    {
        var tasks = new TasksData { specialistMultiplier = 2f, sickMultiplier = 0.5f };
        tasks.items.Add(new TaskRateData { task = "MineIce", specialist = "Geologist", perDay = 6f });
        s.Crew.Add(new Astronaut { Name = "Nora", Role = CrewRole.Geologist, Task = CrewTask.MineIce });
        var p = MissionPlanner.Build(s, sup, true, sup.Evaluate(s, true), bal, chapter, tasks);
        Assert.AreEqual(12f, p.IceWaterPerDay, 0.01f);          // геолог ×2
        Assert.IsTrue(float.IsInfinity(p.Water.Days));          // 4 витрачаємо, 12 добуваємо — вистачить
    }

    [Test]
    public void LifeSupportMakesEnough_IsEndless()
    {
        s.Power.LifeSupport = 4; // 16 робить = 16 дихають
        Assert.IsTrue(float.IsInfinity(Plan().Oxygen.Days));
    }
}

/// <summary>Тест для зірочки «ракета не завелика» (роль 04).</summary>
public class SmallerRocketTests
{
    [Test]
    public void LightCargo_SmallerRocketFits_HeavyCargo_DoesNot()
    {
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "materials", massKg = 200f, materials = 20f });
        var rockets = new RocketCatalog { defaultId = "heavy" };
        rockets.items.Add(new RocketData { id = "small", emptyMassKg = 600f, maxFuelKg = 3500f, flightHours = 8 });
        rockets.items.Add(new RocketData { id = "heavy", emptyMassKg = 2000f, maxFuelKg = 10000f, flightHours = 24 });
        var sup = new SupplySystem(new LaunchData(), cat, rockets);
        var s = new GameState();

        // 4 людини (600) + 200 вантажу: малій треба 1.5 × 1400 × 1.1 = 2310 ≤ 3500 → зелена
        sup.ChangeCargo(s, "materials", 1);
        Assert.AreEqual("small", MissionPlanner.SmallerRocketThatFits(s, sup, true).id);

        // + 2000 кг: малій треба 1.5 × 3400 × 1.1 = 5610 > 3500 → не підходить
        sup.ChangeCargo(s, "materials", 10);
        Assert.IsNull(MissionPlanner.SmallerRocketThatFits(s, sup, true));
    }
}

/// <summary>Тести кроків і часу поставки для вкладки планшета (роль 04).</summary>
public class SupplyScheduleTests
{
    SupplySystem sup;
    GameState s;

    [SetUp]
    public void Setup()
    {
        var data = new LaunchData { supplyEveryDays = 3, launchHour = 8, flightHours = 12, failedDelayDays = 3,
                                    commsOpenHours = 30, commsClosedHours = 18 };
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        sup = new SupplySystem(data, cat) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
        sup.LaunchFirstMission(s); // ставить розклад: старт на 3-й день о 8:00 = 80 год
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    [Test]
    public void Steps_FollowTheOrder()
    {
        Assert.AreEqual(0, sup.Step(s));              // порожньо — склади список
        sup.ChangeCargo(s, "water", 1);
        Assert.AreEqual(1, sup.Step(s));              // є вантаж — відправ
        Assert.IsNull(sup.Send(s));
        Assert.AreEqual(2, sup.Step(s));              // чекає старту
        Assert.IsNull(sup.CancelSend(s));
        Assert.AreEqual(1, sup.Step(s));              // скасували — знову можна міняти
    }

    [Test]
    public void ArrivalTime_FromSchedule()
    {
        Assert.AreEqual(80, sup.ExpectedLaunchAt(s));
        Assert.AreEqual(92, sup.ExpectedArrivalAt(s)); // 80 + 12 год польоту
        Assert.IsFalse(sup.IsLate(s));
    }

    [Test]
    public void NotSentInTime_IsLate_AndLaunchesWhenSent()
    {
        s.Day = 4; s.Hour = 10;                        // 82 год: розклад минув
        Assert.IsTrue(sup.IsLate(s));
        Assert.AreEqual(82, sup.ExpectedLaunchAt(s));  // корабель чекає: стартує, щойно відправиш
        Assert.AreEqual(94, sup.ExpectedArrivalAt(s));
    }
}

/// <summary>Тести плану поставок: частота і дозволені ракети (роль 04).</summary>
public class SupplyPlanTests
{
    SupplySystem sup;
    GameState s;

    [SetUp]
    public void Setup()
    {
        var data = new LaunchData { supplyEveryDays = 3, launchHour = 8, flightHours = 12, defaultPlanId = "normal" };
        data.supplyPlans.Add(new SupplyPlanData { id = "often", everyDays = 2, rockets = new System.Collections.Generic.List<string> { "small" } });
        data.supplyPlans.Add(new SupplyPlanData { id = "normal", everyDays = 3, rockets = new System.Collections.Generic.List<string> { "small", "medium" } });
        data.supplyPlans.Add(new SupplyPlanData { id = "rare", everyDays = 4, rockets = new System.Collections.Generic.List<string> { "small", "medium", "heavy" } });
        var rockets = new RocketCatalog { defaultId = "medium" };
        rockets.items.Add(new RocketData { id = "small", emptyMassKg = 600f, maxFuelKg = 3500f, flightHours = 8 });
        rockets.items.Add(new RocketData { id = "medium", emptyMassKg = 1000f, maxFuelKg = 6000f, flightHours = 12 });
        rockets.items.Add(new RocketData { id = "heavy", emptyMassKg = 2000f, maxFuelKg = 10000f, flightHours = 24 });
        sup = new SupplySystem(data, new CargoCatalog(), rockets) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    [Test]
    public void PlanSetsTheSchedule()
    {
        Assert.IsNull(sup.SetPlan(s, "often"));
        sup.LaunchFirstMission(s);
        Assert.AreEqual(2 * 24 + 8, s.Supply.NextLaunchAt);   // кожні 2 дні
        Assert.AreEqual("error.plan_locked", sup.SetPlan(s, "rare"));
    }

    [Test]
    public void OftenPlan_OnlySmallRocket()
    {
        sup.SetPlan(s, "often");
        sup.SetRocket(s, "heavy");                 // перша місія — будь-яка ракета
        sup.SetFuel(s, 10000f);                    // повний бак — зелене світло
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual("small", sup.GetRocket(s).id);  // для поставок — найбільша з плану
        Assert.AreEqual("error.rocket_not_in_plan", sup.SetRocket(s, "medium"));
    }

    [Test]
    public void RarePlan_AllowsHeavy()
    {
        sup.SetPlan(s, "rare");
        sup.LaunchFirstMission(s);
        Assert.IsNull(sup.SetRocket(s, "heavy"));
        Assert.AreEqual(4 * 24 + 8, s.Supply.NextLaunchAt);
    }
}
