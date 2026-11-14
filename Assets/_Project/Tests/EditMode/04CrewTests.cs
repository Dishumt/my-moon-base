// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести завдань екіпажу (роль 04).</summary>
public class CrewTests
{
    CrewSystem crew;
    GameState s;
    Astronaut geo, bot, eng;

    [SetUp]
    public void Setup()
    {
        var t = new TasksData { specialistMultiplier = 2f, sickMultiplier = 0.5f, labNeedsPower = true };
        t.items.Add(new TaskRateData { task = "MineIce", specialist = "Geologist", perDay = 6f, outside = true });
        t.items.Add(new TaskRateData { task = "CollectSamples", specialist = "Geologist", perDay = 1.5f, outside = true });
        t.items.Add(new TaskRateData { task = "Lab", specialist = "", perDay = 2f });
        crew = new CrewSystem(t);

        s = new GameState();
        s.Pois.Add(new PoiState { Id = "ice_01", Type = PoiType.Ice, Amount = 100f });
        s.Pois.Add(new PoiState { Id = "sample_01", Type = PoiType.Sample, Amount = 10f });
        geo = new Astronaut { Name = "G", Role = CrewRole.Geologist };
        bot = new Astronaut { Name = "B", Role = CrewRole.Botanist };
        eng = new Astronaut { Name = "E", Role = CrewRole.Engineer };
        s.Crew.Add(geo); s.Crew.Add(bot); s.Crew.Add(eng);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void Day() { for (int i = 0; i < 24; i++) crew.Tick(s, 1f, new DayReport(1)); }

    [Test]
    public void Assign_SetsTaskAndResetsSite()
    {
        geo.AtTaskSite = true;
        Assert.IsNull(crew.Assign(geo, CrewTask.MineIce, "ice_01"));
        Assert.AreEqual(CrewTask.MineIce, geo.Task);
        Assert.AreEqual("ice_01", geo.TaskTarget);
        Assert.IsFalse(geo.AtTaskSite);
    }

    [Test]
    public void Assign_InsideOnly_NoOutsideTasks()
    {
        geo.InsideOnly = true;
        Assert.AreEqual("crew.err_inside", crew.Assign(geo, CrewTask.MineIce, "ice_01"));
        Assert.IsNull(crew.Assign(geo, CrewTask.Lab, "3"));
    }

    [Test]
    public void Assign_Severe_OnlyMedbay()
    {
        geo.Health = HealthStatus.Severe;
        Assert.AreEqual("crew.err_severe", crew.Assign(geo, CrewTask.Lab, "3"));
        Assert.IsNull(crew.Assign(geo, CrewTask.Medbay, "4"));
    }

    [Test]
    public void Assign_EngineerIsPlayer()
    {
        Assert.AreEqual("crew.err_you", crew.Assign(eng, CrewTask.MineIce, "ice_01"));
    }

    [Test]
    public void Geologist_Mines12PerDay_Others6()
    {
        crew.Assign(geo, CrewTask.MineIce, "ice_01");
        geo.AtTaskSite = true;
        Day();
        Assert.AreEqual(12f, s.Res.Water, 0.01f);
        Assert.AreEqual(88f, s.FindPoi("ice_01").Amount, 0.01f);

        geo.Task = CrewTask.None;
        crew.Assign(bot, CrewTask.MineIce, "ice_01");
        bot.AtTaskSite = true;
        Day();
        Assert.AreEqual(18f, s.Res.Water, 0.01f);
    }

    [Test]
    public void NotAtSite_NoWork()
    {
        crew.Assign(geo, CrewTask.MineIce, "ice_01");
        Day();
        Assert.AreEqual(0f, s.Res.Water, 0.01f);
    }

    [Test]
    public void Sick_HalfSpeed()
    {
        geo.Health = HealthStatus.Sick;
        crew.Assign(geo, CrewTask.MineIce, "ice_01");
        geo.AtTaskSite = true;
        Day();
        Assert.AreEqual(6f, s.Res.Water, 0.01f);
    }

    [Test]
    public void IceRunsOut()
    {
        s.FindPoi("ice_01").Amount = 5f;
        crew.Assign(geo, CrewTask.MineIce, "ice_01");
        geo.AtTaskSite = true;
        Day();
        Assert.AreEqual(5f, s.Res.Water, 0.01f);
        Assert.AreEqual(0f, s.FindPoi("ice_01").Amount, 0.01f);
    }

    [Test]
    public void Samples_GiveScience()
    {
        crew.Assign(geo, CrewTask.CollectSamples, "sample_01");
        geo.AtTaskSite = true;
        Day();
        Assert.AreEqual(3f, s.Science, 0.01f);
    }

    [Test]
    public void Lab_NeedsPower()
    {
        crew.Assign(bot, CrewTask.Lab, "3");
        bot.AtTaskSite = true;
        Day();
        Assert.AreEqual(0f, s.Science, 0.01f);
        s.Power.Lab = 1;
        Day();
        Assert.AreEqual(2f, s.Science, 0.01f);
    }

    [Test]
    public void RecallAllInside_MovesOutsideWorkers()
    {
        s.Modules.Add(new ModuleState { Type = ModuleType.Shelter, SlotId = 7 });
        crew.Assign(geo, CrewTask.MineIce, "ice_01");
        crew.Assign(bot, CrewTask.Lab, "3");
        bot.InsideSlot = 3;
        Assert.AreEqual(1, crew.RecallAllInside(s));
        Assert.AreEqual(CrewTask.Shelter, geo.Task);
        Assert.AreEqual("7", geo.TaskTarget);
        Assert.AreEqual(CrewTask.Lab, bot.Task);
    }

    [Test]
    public void RequestReplacement_SameRoleFromReserve()
    {
        s.Reserve.Add(new Astronaut { Name = "R", Role = CrewRole.Botanist });
        Assert.AreEqual("crew.err_no_reserve", crew.RequestReplacement(s, geo));
        Assert.IsNull(crew.RequestReplacement(s, bot));
        Assert.Contains("R", s.Supply.Passengers);
        Assert.AreEqual("crew.err_already_requested", crew.RequestReplacement(s, bot));
    }
}
