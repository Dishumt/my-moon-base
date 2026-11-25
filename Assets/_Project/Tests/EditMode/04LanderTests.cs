// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести корабля поставки на Місяці: коли спускається, стоїть і злітає (роль 04).</summary>
public class LanderTests
{
    SupplySystem sup;
    GameState s;
    DayReport r;

    [SetUp]
    public void Setup()
    {
        var data = new LaunchData { supplyEveryDays = 4, launchHour = 8, flightHours = 12, failedDelayDays = 3,
                                    commsOpenHours = 30, commsClosedHours = 18, startFuelKg = 6000f, maxFuelKg = 6000f };
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        sup = new SupplySystem(data, cat) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    /// <summary>Так само, як GameClock: спершу Tick для поточної години, потім година минає.</summary>
    void RunHours(int n)
    {
        for (int i = 0; i < n; i++)
        {
            sup.Tick(s, 1f, r);
            s.Hour++;
            if (s.Hour >= 24) { s.Hour = 0; s.Day++; }
        }
    }

    int Abs => CommsSystem.AbsHour(s);

    LanderPhase PhaseAt(float now) => LanderTimeline.Phase(s.Supply, now, out _);

    [Test]
    public void NewGame_NoLander()
    {
        Assert.AreEqual(LanderPhase.None, PhaseAt(0.5f));
    }

    [Test]
    public void FirstMission_LanderStandsOnPad_ThenLeaves()
    {
        sup.ChangeCargo(s, "water", 1);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual(LanderPhase.Landed, PhaseAt(Abs + 0.1f));
        Assert.AreEqual(LanderPhase.Ascending, PhaseAt(Abs + LanderTimeline.StayHours + 0.5f));
        Assert.AreEqual(LanderPhase.None, PhaseAt(Abs + LanderTimeline.StayHours + LanderTimeline.AscentHours + 0.1f));
    }

    [Test]
    public void Delivery_DescendsInLastHour_TouchesDownWhenCargoArrives()
    {
        sup.ChangeCargo(s, "water", 1);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        RunHours(10);                                    // перший корабель уже відлетів
        sup.ChangeCargo(s, "water", 1);
        Assert.IsNull(sup.Send(s));
        RunHours(s.Supply.NextLaunchAt - Abs + 1);       // стартувала поставка
        Assert.AreEqual(SupplyStatus.Launched, s.Supply.Status);

        int arrival = s.Supply.ArrivalAt;
        Assert.AreEqual(LanderPhase.None, PhaseAt(arrival - 0.1f), "ще високо — не видно");
        Assert.AreEqual(LanderPhase.Descending, LanderTimeline.Phase(s.Supply, arrival + 0.5f, out float t));
        Assert.AreEqual(0.5f, t, 0.001f);

        float waterBefore = s.Res.Water;
        RunHours(arrival - Abs + 1);                     // Tick години arrival — вантаж вивантажили
        Assert.AreEqual(SupplyStatus.Draft, s.Supply.Status);
        Assert.Greater(s.Res.Water, waterBefore);
        Assert.AreEqual(arrival + 1, s.Supply.LanderLandedAt, "сів саме тоді, коли закінчився спуск");
        Assert.AreEqual(LanderPhase.Landed, PhaseAt(arrival + 1.01f));
    }

    [Test]
    public void FailedLaunch_StillLandsLater()
    {
        s.Supply.FirstMissionDone = true;
        s.Supply.Status = SupplyStatus.Returned;
        s.Supply.ArrivalAt = 50;
        Assert.AreEqual(LanderPhase.None, PhaseAt(40f));
        Assert.AreEqual(LanderPhase.Descending, PhaseAt(50.2f));
    }

    [Test]
    public void IncomingDescent_WinsOverPreviousLiftoff()
    {
        s.Supply.LanderLanded = true;
        s.Supply.LanderLandedAt = 10;
        s.Supply.Status = SupplyStatus.Launched;
        s.Supply.ArrivalAt = 14;                          // спуск 14..15 — у той самий час, що й зліт попереднього
        Assert.AreEqual(LanderPhase.Descending, PhaseAt(14.5f));
    }

    [Test]
    public void Preview_ShowsWholeTrip_WithoutChangingState()
    {
        Assert.AreEqual(LanderPhase.Descending, LanderTimeline.Preview(5f, 5.5f, out _));
        Assert.AreEqual(LanderPhase.Landed, LanderTimeline.Preview(5f, 7f, out _));
        Assert.AreEqual(LanderPhase.Ascending, LanderTimeline.Preview(5f, 6f + LanderTimeline.StayHours + 0.5f, out _));
        Assert.AreEqual(LanderPhase.None, LanderTimeline.Preview(5f, 20f, out _));
        Assert.IsFalse(s.Supply.LanderLanded);
    }
}
