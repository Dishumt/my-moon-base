// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести поставок (роль 04).</summary>
public class SupplyTests
{
    SupplySystem sup;
    GameState s;
    DayReport r;
    LaunchData data;

    [SetUp]
    public void Setup()
    {
        data = new LaunchData { supplyEveryDays = 4, launchHour = 8, flightHours = 12, failedDelayDays = 3,
                                commsOpenHours = 30, commsClosedHours = 18 };
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        cat.items.Add(new CargoItemData { id = "medkits", massKg = 20f, medKits = 1 });
        sup = new SupplySystem(data, cat) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void RunHours(int n)
    {
        for (int i = 0; i < n; i++)
        {
            sup.Tick(s, 1f, r);
            s.Hour++;
            if (s.Hour >= 24) { s.Hour = 0; s.Day++; }
        }
    }

    [Test]
    public void FirstMission_Green_AddsCargoAndSchedules()
    {
        sup.ChangeCargo(s, "water", 2);
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual(40f, s.Res.Water, 0.01f);
        Assert.IsTrue(s.Supply.FirstMissionDone);
        Assert.AreEqual(4 * 24 + 8, s.Supply.NextLaunchAt);
    }

    [Test]
    public void FirstMission_Red_KeepsCargoForRepack()
    {
        sup.ChangeCargo(s, "water", 2);
        sup.SetFuel(s, 100f);
        Assert.IsFalse(sup.LaunchFirstMission(s));
        Assert.AreEqual(2, s.Supply.Count("water"));
        Assert.IsFalse(s.Supply.FirstMissionDone);
    }

    [Test]
    public void FirstMission_Counts4Seats()
    {
        Assert.AreEqual(600f, sup.Evaluate(s, true).PeopleKg, 0.01f);
        Assert.AreEqual(0f, sup.Evaluate(s, false).PeopleKg, 0.01f);
    }

    [Test]
    public void Send_OnlyWhenCommsOpen()
    {
        s.Power.Comms = 0;
        Assert.AreEqual("error.comms_closed", sup.Send(s));
        s.Power.Comms = 1;
        Assert.IsNull(sup.Send(s));
        Assert.AreEqual(SupplyStatus.Sent, s.Supply.Status);
    }

    [Test]
    public void SentOrder_LaunchesOnSchedule_AndArrives()
    {
        sup.ChangeCargo(s, "medkits", 2);
        sup.SetFuel(s, 6000f);
        sup.Send(s);
        int arrived = 0;
        GameEvents.SupplyArrived += () => arrived++;

        RunHours(4 * 24 + 8);          // ще не стартувала
        Assert.AreEqual(SupplyStatus.Sent, s.Supply.Status);
        RunHours(1);                   // старт
        Assert.AreEqual(SupplyStatus.Launched, s.Supply.Status);
        RunHours(12);                  // летить 12 годин
        Assert.AreEqual(1, arrived);
        Assert.AreEqual(2, s.Res.MedKits);
        Assert.AreEqual(SupplyStatus.Draft, s.Supply.Status);
    }

    [Test]
    public void FailedLaunch_Arrives3DaysLater()
    {
        sup.ChangeCargo(s, "water", 40); // 4000 кг
        sup.SetFuel(s, 100f);            // червоний
        sup.Send(s);
        RunHours(4 * 24 + 9);
        Assert.AreEqual(SupplyStatus.Returned, s.Supply.Status);
        Assert.AreEqual(s.Supply.ArrivalAt, 4 * 24 + 8 + 12 + 72);
        RunHours(12);
        Assert.AreEqual(0f, s.Res.Water, 0.01f); // ще не прийшло
        RunHours(72);
        Assert.AreEqual(800f, s.Res.Water, 0.01f);
    }

    [Test]
    public void NotSent_WaitsAndReports()
    {
        RunHours(4 * 24 + 20);
        Assert.AreEqual(SupplyStatus.Draft, s.Supply.Status);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.supply_not_sent"));
        // відправили пізніше — стартує одразу
        s.Hour = 0; s.Day = 7; // година 144, вікно відкрите (144 % 48 = 0)
        Assert.IsNull(sup.Send(s));
        RunHours(1);
        Assert.AreEqual(SupplyStatus.Launched, s.Supply.Status);
    }

    [Test]
    public void Arrival_SwapsCrew()
    {
        var sick = new Astronaut { Name = "Geo1", Role = CrewRole.Geologist, GoingHome = true };
        s.Crew.Add(sick);
        s.Reserve.Add(new Astronaut { Name = "Geo2", Role = CrewRole.Geologist });
        var crew = new CrewSystem(new TasksData());
        Assert.IsNull(crew.RequestReplacement(s, sick));
        Assert.AreEqual(150f, sup.Evaluate(s, false).PeopleKg, 0.01f);

        sup.SetFuel(s, 6000f);
        sup.Send(s);
        RunHours(4 * 24 + 8 + 1 + 12);

        Assert.IsFalse(s.Crew.Exists(a => a.Name == "Geo1"));
        Assert.IsTrue(s.Crew.Exists(a => a.Name == "Geo2"));
        Assert.AreEqual(0, s.Reserve.Count);
    }
}
