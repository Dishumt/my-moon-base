// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести дози щогодини (роль 06, задача 1).</summary>
public class RadiationTests
{
    RadiationBalance bal;
    RadLabDose radlab;
    RadiationSystem rad;
    GameState s;
    Astronaut geo, bot;

    [SetUp]
    public void Setup()
    {
        bal = new RadiationBalance { timeCompression = 50f, outsideMultiplier = 4f, insideMultiplier = 1f, regolithMultiplier = 0.5f, yellowPercent = 50f, redPercent = 100f };
        radlab = new RadLabDose { dailyDose_mSv = 0.5f, careerLimit_mSv = 600f };
        rad = new RadiationSystem(bal, radlab, new DonkiStormList(), new StormSchedule());

        s = new GameState { Chapter = 1 };
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 3 });
        s.Modules.Add(new ModuleState { Type = ModuleType.Lab, SlotId = 4, RegolithShield = true });
        geo = new Astronaut { Name = "G", Role = CrewRole.Geologist, InsideSlot = -1 };
        bot = new Astronaut { Name = "B", Role = CrewRole.Botanist, InsideSlot = 3 };
        s.Crew.Add(geo); s.Crew.Add(bot);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void Hours(int n, DayReport r = null)
    {
        r = r ?? new DayReport(1);
        for (int i = 0; i < n; i++) { rad.Tick(s, 1f, r); s.Hour++; if (s.Hour >= 24) { s.Hour = 0; s.Day++; } }
    }

    [Test]
    public void BaseDose_FromRadLabAndCompression()
    {
        // 0.5 мЗв/добу з 600 мЗв = 0.0833% на добу; ×50 = 4.17% на добу
        Assert.AreEqual(0.5f / 600f * 100f / 24f * 50f, rad.BasePercentPerHour, 1e-5f);
    }

    [Test]
    public void Outside_FourTimesMoreThanInside()
    {
        Hours(1);
        Assert.AreEqual(bot.Dose * 4f, geo.Dose, 1e-4f);
        Assert.Greater(bot.Dose, 0f);
    }

    [Test]
    public void Regolith_HalvesDoseInside()
    {
        geo.InsideSlot = 4; // лабораторія під реголітом
        Hours(10);
        Assert.AreEqual(bot.Dose * 0.5f, geo.Dose, 1e-4f);
    }

    [Test]
    public void Yellow_SetsInsideOnly_AndCancelsOutsideTask()
    {
        geo.Dose = 49.9f;
        geo.Task = CrewTask.MineIce;
        var r = new DayReport(1);
        Hours(1, r);
        Assert.IsTrue(geo.InsideOnly);
        Assert.IsFalse(geo.GoingHome);
        Assert.AreEqual(CrewTask.None, geo.Task);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.dose_yellow" && l.Arg == "G" && l.Important));
    }

    [Test]
    public void Red_SetsGoingHome()
    {
        geo.Dose = 99.9f;
        var r = new DayReport(1);
        Hours(1, r);
        Assert.IsTrue(geo.GoingHome);
        Assert.IsTrue(geo.InsideOnly);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.dose_red" && l.Arg == "G"));
        Assert.AreEqual(DoseZone.Red, rad.Zone(geo));
    }

    [Test]
    public void DayReport_WhoGotHowMuchAndWhy()
    {
        var r = new DayReport(1);
        Hours(24, r);
        rad.OnDayEnd(s, r);
        var geoLine = r.Lines.Find(l => l.Key == "report.dose_day" && l.Arg == "G");
        Assert.IsNotNull(geoLine);
        Assert.AreEqual(geo.Dose, geoLine.Delta, 1e-3f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.dose_why_outside" && l.Arg == "G"));
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.dose_why_inside" && l.Arg == "B"));
        Assert.AreEqual(0, s.Radiation.Today.Count, "після звіту лічильник дня скинуто");
    }

    [Test]
    public void RealData_OftenOutside_ReachesYellowIn4to5Days()
    {
        // Справжні файли з Resources/Data/Nasa/
        var realRad = new RadiationSystem(RadiationBalance.Load(), RadLabDose.Load(), new DonkiStormList(), new StormSchedule());
        var state = new GameState { Chapter = 1 };
        var a = new Astronaut { Name = "A", Role = CrewRole.Geologist };
        state.Crew.Add(a);

        int hours = 0;
        while (a.Dose < 50f && hours < 24 * 30)
        {
            a.InsideSlot = state.Hour < 12 ? -1 : 0; // пів дня надворі, пів дня всередині
            realRad.Tick(state, 1f, new DayReport(state.Day));
            hours++;
            state.Hour++;
            if (state.Hour >= 24) { state.Hour = 0; state.Day++; }
        }
        float days = hours / 24f;
        Assert.That(days, Is.InRange(4f, 5.5f), "до жовтої зони за " + days + " днів");
    }
}
