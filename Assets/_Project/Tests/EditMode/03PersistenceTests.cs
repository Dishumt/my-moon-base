// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Системи 03 мають переживати збереження й завантаження («Продовжити»):
/// їхній стан лежить у GameState.Sim, а не всередині систем. Збереження = JsonUtility, як у SaveSystem.
/// </summary>
public class SimPersistenceTests
{
    [SetUp]
    public void SetUp() { GameEvents.ClearAll(); }

    [TearDown]
    public void TearDown() { GameEvents.ClearAll(); }

    /// <summary>Те саме, що робить SaveSystem: у JSON і назад.</summary>
    private static GameState SaveAndLoad(GameState s)
    {
        return JsonUtility.FromJson<GameState>(JsonUtility.ToJson(s));
    }

    private static SimBalanceData Balance()
    {
        return new SimBalanceData
        {
            power = new PowerBalance
            {
                panelBlocks = 3f, panelBlocksInShade = 1f,
                shadeHoursPerDay = 6f, batteryCapacityBlockHours = 10f
            },
            robots = new RobotsBalance { count = 2, powerBlocksWhenWorking = 1f },
            construction = new ConstructionBalance { engineerNearbySpeedMultiplier = 2f },
            food = new FoodBalance
            {
                portionsPerAstronautPerDay = 1f, mealsPerDay = 3, satietyMax = 100f,
                daysToEmptyWithoutFood = 2f, satietyAfterMeal = 100f
            },
            greenhouse = new GreenhouseBalance
            {
                growthPerBlockPerDay = 1f, harvestEveryDays = 3f, portionsPerGrowth = 2f,
                botanistMultiplier = 1.5f, waterUsePerBlockPerDay = 1f
            },
            defeat = new DefeatBalance { oxygenAtOrBelow = 0f, daysWithoutFood = 2f }
        };
    }

    // ───────────────────────── Їжа ─────────────────────────

    [Test]
    public void Food_StarvationTimer_SurvivesSaveAndLoad()
    {
        var s = new GameState();
        s.Res.Food = 0f;
        s.Crew.Add(new Astronaut { Name = "A" });

        new FoodSystem(Balance()).Tick(s, 10f, new DayReport(1));
        Assert.AreEqual(10f, s.Sim.HoursWithoutFood, 0.01f);

        var loaded = SaveAndLoad(s);
        var food = new FoodSystem(Balance()); // нова система, як після запуску гри
        food.Tick(loaded, 10f, new DayReport(1));

        Assert.AreEqual(20f, food.HoursWithoutFood, 0.01f, "таймер голоду не має скидатися після «Продовжити»");
    }

    // ───────────────────────── Теплиця ─────────────────────────

    private static GreenhouseSystem GreenhouseWith(int blocks)
    {
        var power = new PowerSystem(Balance());
        power.Granted.Greenhouse = blocks;
        return new GreenhouseSystem(power, Balance());
    }

    private static GameState GreenhouseState()
    {
        var s = new GameState();
        s.Res.Water = 60f;
        s.Res.Food = 0f;
        s.Modules.Add(new ModuleState { Type = ModuleType.Greenhouse, SlotId = 0, BuildHoursLeft = 0f });
        return s;
    }

    private static void RunDay(GreenhouseSystem sys, GameState s)
    {
        var r = new DayReport(s.Day);
        sys.Tick(s, 24f, r);
        sys.OnDayEnd(s, r);
    }

    [Test]
    public void Greenhouse_Growth_SurvivesSaveAndLoad()
    {
        var s = GreenhouseState();
        var before = GreenhouseWith(2);
        RunDay(before, s); // день 1: ріст 2
        Assert.AreEqual(2f, s.Sim.GreenhouseGrowth, 0.01f);
        Assert.AreEqual(1, s.Sim.GreenhouseDaysSinceHarvest);

        var loaded = SaveAndLoad(s);
        var after = GreenhouseWith(2); // нова система
        RunDay(after, loaded); // день 2
        RunDay(after, loaded); // день 3: урожай

        // Ріст за 3 дні: 2 × 3 = 6, урожай = 6 × 2 = 12 порцій. Без збереження стану урожаю не було б узагалі.
        Assert.AreEqual(12f, loaded.Res.Food, 0.01f, "ріст до збереження не має пропадати");
    }

    // ───────────────────────── Розділи ─────────────────────────

    private static ChapterData Chapter(int number, params ChapterGoalData[] goals)
    {
        return new ChapterData
        {
            chapter = number,
            titleKey = "chapter.title",
            descriptionKey = "chapter.description",
            unlockedModules = new[] { "Habitat", "Solar" },
            unlockedSystems = new[] { "Power" },
            goals = goals
        };
    }

    private static ChapterGoalData Goal(string id, string type, float target)
    {
        return new ChapterGoalData { id = id, type = type, target = target, titleKey = "goal." + id };
    }

    private static ChapterSystem NewChapterSystem(params ChapterData[] chapters)
    {
        var bal = Balance();
        var power = new PowerSystem(bal);
        var construction = new ConstructionSystem(power, bal, new BuildCostTable());
        return new ChapterSystem(new FoodSystem(bal), construction, bal,
            n => chapters[n - 1]);
    }

    private static GameState ChapterState()
    {
        var s = new GameState();
        s.Res.Oxygen = 100f;
        s.Res.Food = 40f;
        s.Crew.Add(new Astronaut { Name = "A" });
        return s;
    }

    [Test]
    public void Chapter_SurviveDays_SurvivesSaveAndLoad()
    {
        var chapter = Chapter(1, Goal("live", "SurviveDays", 7));
        var s = ChapterState();
        var before = NewChapterSystem(chapter);
        before.StartChapter(s, 1, true);

        s.Day = 4; // три дні розділу позаду
        Assert.AreEqual(3f, before.GetProgress(s)[0].Current, 0.01f);

        var loaded = SaveAndLoad(s);
        var after = NewChapterSystem(chapter); // нова система
        after.StartChapter(loaded, 1, false);  // так GameBootstrap запускає розділ після «Продовжити»

        Assert.AreEqual(3f, after.GetProgress(loaded)[0].Current, 0.01f,
            "«прожити N днів» не має починатися спочатку після «Продовжити»");
    }

    [Test]
    public void Chapter_DoneGoal_StaysDone_AfterSaveAndLoad()
    {
        var chapter = Chapter(1, Goal("water", "WaterAtLeast", 50));
        var s = ChapterState();
        s.Res.Water = 80f;
        var before = NewChapterSystem(chapter);
        before.StartChapter(s, 1, true);
        before.Tick(s, 1f, new DayReport(1));
        Assert.IsTrue(before.IsFinished || before.GetProgress(s)[0].Done);

        s.Res.Water = 5f; // воду витратили, але ціль уже зарахована
        var loaded = SaveAndLoad(s);
        var after = NewChapterSystem(chapter);
        after.StartChapter(loaded, 1, false);

        Assert.IsTrue(after.GetProgress(loaded)[0].Done, "зарахована ціль не має зникати після «Продовжити»");
    }

    [Test]
    public void Chapter_NewChapter_StartsCountingFromToday()
    {
        var c1 = Chapter(1, Goal("live", "SurviveDays", 7));
        var c2 = Chapter(2, Goal("live", "SurviveDays", 7));
        var s = ChapterState();
        var sys = NewChapterSystem(c1, c2);
        sys.StartChapter(s, 1, true);

        s.Day = 9;
        sys.StartChapter(s, 2, false); // перехід у наступний розділ, а не «Продовжити»

        Assert.AreEqual(0f, sys.GetProgress(s)[0].Current, 0.01f);
    }

    [Test]
    public void Chapter_Retry_StartsCountingFromToday()
    {
        var c1 = Chapter(1, Goal("live", "SurviveDays", 7));
        var s = ChapterState();
        var sys = NewChapterSystem(c1);
        sys.StartChapter(s, 1, true);

        s.Day = 6;
        sys.StartChapter(s, 1, true); // повтор розділу після поразки

        Assert.AreEqual(0f, sys.GetProgress(s)[0].Current, 0.01f);
    }

    // ───────────────────────── Старі збереження ─────────────────────────

    [Test]
    public void OldSaveWithoutSim_StillLoads_WithEmptySimState()
    {
        var loaded = JsonUtility.FromJson<GameState>("{\"Day\":3,\"Chapter\":2}");

        Assert.IsNotNull(loaded.Sim);
        Assert.AreEqual(0, loaded.Sim.ChapterStartDay);
        Assert.AreEqual(0f, loaded.Sim.HoursWithoutFood);
        Assert.IsNotNull(loaded.Sim.GoalsDone);
    }
}
