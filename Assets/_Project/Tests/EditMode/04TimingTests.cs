// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Тести часу логістики (роль 04): усе рахується від довжини розділу (LogisticsTime).
/// Останній тест читає СПРАВЖНІ launch.json і rockets.json: якщо хтось змінить темп чи числа так,
/// що поставка не встигає за розділ, — тест стане червоним.
/// </summary>
public class TimingTests
{
    float chapterDays;
    SupplySystem sup;
    LaunchData data;
    GameState s;

    [SetUp]
    public void Setup()
    {
        chapterDays = 2f;
        LogisticsTime.ChapterDaysOverride = () => chapterDays;
        data = new LaunchData
        {
            defaultPlanId = "normal", failedDelayShare = 0.5f, stormDelayShare = 0.25f,
            commsCyclesPerChapter = 4f, commsOpenShare = 0.65f
        };
        data.supplyPlans.Add(new SupplyPlanData { id = "often", shipsPerChapter = 3f, rockets = new List<string> { "small" } });
        data.supplyPlans.Add(new SupplyPlanData { id = "normal", shipsPerChapter = 2f, rockets = new List<string> { "small", "medium" } });
        data.supplyPlans.Add(new SupplyPlanData { id = "rare", shipsPerChapter = 1.5f, rockets = new List<string> { "small", "medium", "heavy" } });
        var rockets = new RocketCatalog { defaultId = "medium" };
        rockets.items.Add(new RocketData { id = "small", emptyMassKg = 600f, maxFuelKg = 3500f, flightShare = 0.2f });
        rockets.items.Add(new RocketData { id = "medium", emptyMassKg = 1000f, maxFuelKg = 6000f, flightShare = 0.3f });
        rockets.items.Add(new RocketData { id = "heavy", emptyMassKg = 2000f, maxFuelKg = 10000f, flightShare = 0.45f });
        sup = new SupplySystem(data, new CargoCatalog(), rockets) { Random01 = () => 0.5f };
        s = new GameState { Day = 1, Hour = 6 };
        s.Power.Comms = 1;
    }

    [TearDown]
    public void Clean()
    {
        LogisticsTime.ChapterDaysOverride = null;
        GameEvents.ClearAll();
    }

    [Test]
    public void Period_FollowsChapterLength()
    {
        Assert.AreEqual(24, sup.PeriodHours(s));          // 2 дні / 2 кораблі
        chapterDays = 3f;
        Assert.AreEqual(36, sup.PeriodHours(s));          // розділ довший — поставки рідші, нічого не міняли
        sup.SetPlan(s, "often");
        Assert.AreEqual(24, sup.PeriodHours(s));          // 3 дні / 3 кораблі
    }

    [Test]
    public void Flight_And_Delays_AreSharesOfPeriod()
    {
        Assert.AreEqual(7, sup.FlightHours(s));           // 24 × 0.3 = 7.2 → 7
        Assert.AreEqual(12, sup.FailedDelayHours(s));     // 24 × 0.5
        Assert.AreEqual(6, sup.StormDelayHours(s));       // 24 × 0.25
        chapterDays = 4f;
        Assert.AreEqual(14, sup.FlightHours(s));          // 48 × 0.3 = 14.4 → 14
    }

    [Test]
    public void Schedule_And_Planner_Agree()
    {
        int now = CommsSystem.AbsHour(s);
        float plannerDays = MissionPlanner.DaysToNextShip(s, sup, true);
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.AreEqual(now + 24, s.Supply.NextLaunchAt);
        Assert.AreEqual((24 + 7) / 24f, plannerDays, 0.001f);   // порадник рахує той самий розклад
    }

    [Test]
    public void Comms_CycleFollowsChapter()
    {
        var comms = new CommsSystem(data);
        Assert.AreEqual(8, comms.BaseOpenHours());        // цикл 48/4 = 12 год: 8 є
        Assert.AreEqual(4, comms.ClosedHours());          // 4 немає
        chapterDays = 4f;
        Assert.AreEqual(16, comms.BaseOpenHours());
        Assert.AreEqual(8, comms.ClosedHours());
    }

    [Test]
    public void OldFields_StillWork_WhenSharesAreZero()
    {
        var old = new LaunchData { supplyEveryDays = 4, launchHour = 8, flightHours = 12, failedDelayDays = 3 };
        var oldSup = new SupplySystem(old, new CargoCatalog());
        Assert.AreEqual(96, oldSup.PeriodHours(s));
        Assert.AreEqual(12, oldSup.FlightHours(s));
        Assert.AreEqual(72, oldSup.FailedDelayHours(s));
        Assert.AreEqual(4 * 24 + 8, oldSup.NextLaunchFrom(s, 6));
    }

    /// <summary>Справжні дані: за будь-якої довжини розділу перший корабель після висадки встигає сісти до кінця розділу.</summary>
    [Test]
    public void RealData_FirstShipLandsWithinChapter_AnyPace()
    {
        var real = LaunchData.Load();
        var rockets = RocketCatalog.Load();
        Assert.IsNotNull(real);
        Assert.IsNotNull(rockets);
        var realSup = new SupplySystem(real, new CargoCatalog(), rockets);
        var realComms = new CommsSystem(real);

        foreach (float days in new[] { 1f, 2f, 3f, 5f, 8f })
        {
            chapterDays = days;
            int chapterHours = (int)(days * 24f);
            Assert.Greater(realComms.BaseOpenHours(), 0, "зв'язок має бути хоч трохи, розділ " + days);
            foreach (var plan in real.supplyPlans)
            {
                var state = new GameState { Day = 1, Hour = 0 };
                realSup.SetPlan(state, plan.id);
                int period = realSup.PeriodHours(state);
                foreach (var r in rockets.items)
                {
                    if (plan.rockets != null && plan.rockets.Count > 0 && !plan.rockets.Contains(r.id)) continue;
                    int flight = realSup.FlightHoursOf(state, r);
                    Assert.LessOrEqual(period + flight, chapterHours,
                        $"розділ {days} дн., план {plan.id}, ракета {r.id}: корабель не встигає ({period}+{flight} год)");
                }
            }
        }
    }
}
