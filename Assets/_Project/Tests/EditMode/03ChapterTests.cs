// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using NUnit.Framework;

/// <summary>
/// Тести розділів, перемоги й поразки. Розділи в тестах збираються руками,
/// а один тест читає справжні chapter_01..03.json і шукає в них помилки в назвах.
/// </summary>
public class ChapterSystemTests
{
    private int _completedCount;
    private int _completedChapter;
    private int _overCount;
    private string _overKey;

    [SetUp]
    public void SetUp()
    {
        GameEvents.ClearAll();
        _completedCount = 0;
        _completedChapter = -1;
        _overCount = 0;
        _overKey = null;
        GameEvents.ChapterCompleted += c => { _completedCount++; _completedChapter = c; };
        GameEvents.GameOver += key => { _overCount++; _overKey = key; };
    }

    [TearDown]
    public void TearDown()
    {
        GameEvents.ClearAll();
    }

    // ───────────────────────── Збирання тестових даних ─────────────────────────

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
            defeat = new DefeatBalance { oxygenAtOrBelow = 0f, daysWithoutFood = 2f }
        };
    }

    private static ChapterGoalData Goal(string id, string type, float target, string module = "")
    {
        return new ChapterGoalData
        {
            id = id, type = type, target = target, module = module, titleKey = "goal." + id
        };
    }

    private static ChapterData MakeChapter(params ChapterGoalData[] goals)
    {
        return new ChapterData
        {
            chapter = 1,
            titleKey = "chapter.1.title",
            descriptionKey = "chapter.1.description",
            resources = new ResourceState
            {
                Water = 60f, Food = 40f, Oxygen = 100f, Battery = 10f,
                Materials = 40f, MedKits = 0
            },
            startScience = 0f,
            startModules = new[]
            {
                new ChapterStartModule { type = "Solar", slot = 0 },
                new ChapterStartModule { type = "Solar", slot = 1 }
            },
            unlockedModules = new[] { "Habitat", "Solar" },
            unlockedSystems = new[] { "Power", "LifeSupport" },
            goals = goals
        };
    }

    private class Rig
    {
        public GameState S;
        public FoodSystem Food;
        public ConstructionSystem Build;
        public ChapterSystem Chapter;
    }

    private static Rig MakeRig(ChapterData chapter)
    {
        var bal = Balance();
        var power = new PowerSystem(bal);

        var costs = new BuildCostTable();
        costs.Set(ModuleType.Habitat, 24f, 20f);
        costs.Set(ModuleType.Greenhouse, 18f, 15f);

        var s = new GameState();
        s.Res.Oxygen = 100f;
        s.Res.Water = 60f;
        s.Res.Food = 40f;
        s.Res.Materials = 100f;
        s.Crew.Add(new Astronaut { Name = "A" });
        s.Robots.Add(new RobotState());

        var food = new FoodSystem(bal);
        var build = new ConstructionSystem(power, bal, costs);
        var chapterSystem = new ChapterSystem(food, build, bal, n => chapter);
        chapterSystem.StartChapter(s, 1, false);

        return new Rig { S = s, Food = food, Build = build, Chapter = chapterSystem };
    }

    private static void BuildHabitat(GameState s, int slot = 5)
    {
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = slot, BuildHoursLeft = 0f });
    }

    // ───────────────────────── Старт розділу ─────────────────────────

    [Test]
    public void StartChapter_ApplyStart_SetsResourcesAndStartModules()
    {
        var rig = MakeRig(MakeChapter(Goal("g", "SurviveDays", 3)));
        var s = new GameState();
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 1, BuildHoursLeft = 0f });

        rig.Chapter.StartChapter(s, 1, true);

        Assert.AreEqual(60f, s.Res.Water, 0.001f);
        Assert.AreEqual(40f, s.Res.Food, 0.001f);
        Assert.AreEqual(100f, s.Res.Oxygen, 0.001f);
        Assert.AreEqual(10f, s.Res.Battery, 0.001f);
        Assert.AreEqual(40f, s.Res.Materials, 0.001f);
        Assert.AreEqual(2, s.Modules.Count);
        Assert.AreEqual(ModuleType.Solar, s.FindModule(0).Type);
        Assert.IsTrue(s.FindModule(0).IsBuilt);
        Assert.AreEqual(ModuleType.Habitat, s.FindModule(1).Type); // зайнятий слот не чіпаємо
    }

    [Test]
    public void StartChapter_WithoutApplyStart_KeepsTheBase()
    {
        var rig = MakeRig(MakeChapter(Goal("g", "SurviveDays", 3)));
        var s = new GameState();
        s.Res.Water = 7f;

        rig.Chapter.StartChapter(s, 1, false);

        Assert.AreEqual(7f, s.Res.Water, 0.001f);
        Assert.AreEqual(0, s.Modules.Count);
    }

    [Test]
    public void UnlockedModules_LimitConstruction()
    {
        var rig = MakeRig(MakeChapter(Goal("g", "SurviveDays", 3)));

        Assert.AreEqual("build.err.locked", rig.Build.Order(rig.S, ModuleType.Greenhouse, 9));
        Assert.IsNull(rig.Build.Order(rig.S, ModuleType.Habitat, 9));

        Assert.IsTrue(rig.Chapter.IsModuleUnlocked(ModuleType.Solar));
        Assert.IsFalse(rig.Chapter.IsModuleUnlocked(ModuleType.Battery));
        Assert.IsTrue(rig.Chapter.IsSystemUnlocked("Power"));
        Assert.IsFalse(rig.Chapter.IsSystemUnlocked("Radiation"));
    }

    // ───────────────────────── Поразка ─────────────────────────

    [Test]
    public void ZeroOxygen_GameOver_OnceWithReason()
    {
        var rig = MakeRig(MakeChapter(Goal("g", "SurviveDays", 3)));
        rig.S.Res.Oxygen = 0f;
        var r = new DayReport(1);

        rig.Chapter.Tick(rig.S, 1f, r);
        rig.Chapter.Tick(rig.S, 1f, r);

        Assert.AreEqual(1, _overCount);
        Assert.AreEqual("gameover.no_oxygen", _overKey);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "gameover.no_oxygen" && l.Important));
        Assert.IsTrue(rig.Chapter.IsFinished);
    }

    [Test]
    public void TwoDaysWithoutFood_GameOver_ButNotBefore()
    {
        var rig = MakeRig(MakeChapter(Goal("g", "SurviveDays", 3)));
        rig.S.Res.Food = 0f;
        var r = new DayReport(1);

        rig.Food.Tick(rig.S, 47f, r);
        rig.Chapter.Tick(rig.S, 1f, r);
        Assert.AreEqual(0, _overCount);

        rig.Food.Tick(rig.S, 1f, r); // тепер 48 годин
        rig.Chapter.Tick(rig.S, 1f, r);

        Assert.AreEqual(1, _overCount);
        Assert.AreEqual("gameover.no_food", _overKey);
    }

    [Test]
    public void Defeat_BeatsVictory_InTheSameHour()
    {
        var rig = MakeRig(MakeChapter(Goal("habitat", "BuildModule", 1, "Habitat")));
        BuildHabitat(rig.S);
        rig.S.Res.Oxygen = 0f;

        rig.Chapter.Tick(rig.S, 1f, new DayReport(1));

        Assert.AreEqual(1, _overCount);
        Assert.AreEqual(0, _completedCount);
    }

    // ───────────────────────── Цілі й перемога ─────────────────────────

    [Test]
    public void AllGoalsDone_ChapterCompleted_Once()
    {
        var rig = MakeRig(MakeChapter(
            Goal("survive", "SurviveDays", 3),
            Goal("habitat", "BuildModule", 1, "Habitat")));
        var r = new DayReport(1);

        rig.Chapter.Tick(rig.S, 1f, r);
        Assert.AreEqual(0, _completedCount);

        BuildHabitat(rig.S);
        rig.Chapter.Tick(rig.S, 1f, r);
        Assert.AreEqual(0, _completedCount); // житло є, а днів ще мало

        rig.S.Day = 4; // минуло 3 дні від початку розділу
        rig.Chapter.Tick(rig.S, 1f, r);
        rig.Chapter.Tick(rig.S, 1f, r);

        Assert.AreEqual(1, _completedCount);
        Assert.AreEqual(1, _completedChapter);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.chapter_completed"));
    }

    [Test]
    public void OneGoalMissing_ChapterNotCompleted()
    {
        var rig = MakeRig(MakeChapter(
            Goal("survive", "SurviveDays", 3),
            Goal("habitat", "BuildModule", 1, "Habitat")));
        rig.S.Day = 10; // днів досить, а житла немає

        rig.Chapter.Tick(rig.S, 1f, new DayReport(1));

        Assert.AreEqual(0, _completedCount);
    }

    [Test]
    public void UnfinishedModule_DoesNotCountAsBuilt()
    {
        var rig = MakeRig(MakeChapter(Goal("habitat", "BuildModule", 1, "Habitat")));
        rig.S.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 5, BuildHoursLeft = 10f });

        rig.Chapter.Tick(rig.S, 1f, new DayReport(1));

        Assert.AreEqual(0, _completedCount);
    }

    [Test]
    public void WaterGoal_StaysDone_EvenIfWaterDropsLater()
    {
        var rig = MakeRig(MakeChapter(
            Goal("water", "WaterAtLeast", 45f),
            Goal("survive", "SurviveDays", 3)));
        var r = new DayReport(1);

        rig.S.Res.Water = 50f;
        rig.Chapter.Tick(rig.S, 1f, r);   // вода досягла цілі
        rig.S.Res.Water = 10f;            // потім витратили
        rig.S.Day = 4;
        rig.Chapter.Tick(rig.S, 1f, r);

        Assert.AreEqual(1, _completedCount);
    }

    [Test]
    public void ScienceGoal_Works()
    {
        var rig = MakeRig(MakeChapter(Goal("science", "ScienceAtLeast", 10f)));

        rig.S.Science = 9f;
        rig.Chapter.Tick(rig.S, 1f, new DayReport(1));
        Assert.AreEqual(0, _completedCount);

        rig.S.Science = 10f;
        rig.Chapter.Tick(rig.S, 1f, new DayReport(1));
        Assert.AreEqual(1, _completedCount);
    }

    [Test]
    public void GetProgress_ShowsCurrentAndTarget()
    {
        var rig = MakeRig(MakeChapter(
            Goal("survive", "SurviveDays", 5),
            Goal("habitat", "BuildModule", 1, "Habitat")));
        rig.S.Day = 3; // прожито 2 дні

        var list = rig.Chapter.GetProgress(rig.S);

        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(2f, list[0].Current, 0.001f);
        Assert.AreEqual(5f, list[0].Target, 0.001f);
        Assert.IsFalse(list[0].Done);
        Assert.AreEqual("Habitat", list[1].Module);
        Assert.AreEqual(0f, list[1].Current, 0.001f);
    }

    // ───────────────────────── Справжні файли розділів ─────────────────────────

    [Test]
    public void RealChapterFiles_AreValid()
    {
        string[] goalTypes = { "SurviveDays", "BuildModule", "WaterAtLeast", "ScienceAtLeast" };

        for (int n = 1; n <= 3; n++)
        {
            ChapterData c = DataLoader.Load<ChapterData>("Chapters/chapter_" + n.ToString("00"));
            string name = "chapter_0" + n;

            Assert.IsNotNull(c, name + " не прочитався");
            Assert.AreEqual(n, c.chapter, name + ": поле chapter");
            Assert.IsNotNull(c.resources, name + ": немає resources");
            Assert.Greater(c.resources.Oxygen, 0f, name + ": кисню на старті 0, буде миттєва поразка");

            // Той самий файл читає ядро: перевіряємо, що з нього виходить робочий стан гри.
            ChapterStartData core = DataLoader.Load<ChapterStartData>("Chapters/chapter_" + n.ToString("00"));
            GameState fresh = GameStateFactory.FromChapter(core);
            Assert.AreEqual(4, fresh.Crew.Count, name + ": у файлі має бути 4 астронавти (crew)");
            Assert.AreEqual(2, fresh.Robots.Count, name + ": у файлі має бути 2 роботи (robots)");
            Assert.AreEqual(c.resources.Oxygen, fresh.Res.Oxygen, 0.001f, name + ": resources не дійшли до ядра");
            Assert.Greater(fresh.Power.LifeSupport, 0, name + ": початковий план енергії без життєзабезпечення");
            Assert.IsNotEmpty(c.goals, name + ": немає цілей");
            Assert.IsNotEmpty(c.unlockedModules, name + ": немає відкритих модулів");

            foreach (string m in c.unlockedModules)
                Assert.IsTrue(Enum.IsDefined(typeof(ModuleType), m), name + ": невідомий модуль " + m);

            foreach (ChapterStartModule sm in c.startModules)
                Assert.IsTrue(Enum.IsDefined(typeof(ModuleType), sm.type), name + ": невідомий стартовий модуль " + sm.type);

            foreach (ChapterGoalData g in c.goals)
            {
                Assert.Contains(g.type, goalTypes, name + ": невідомий тип цілі " + g.type);
                Assert.Greater(g.target, 0f, name + ": target цілі " + g.id);
                if (g.type == "BuildModule")
                {
                    Assert.IsTrue(Enum.IsDefined(typeof(ModuleType), g.module), name + ": невідомий модуль цілі " + g.module);
                    // Модуль, який треба збудувати, має бути відкритий у цьому розділі.
                    Assert.Contains(g.module, c.unlockedModules, name + ": ціль " + g.id + " просить закритий модуль");
                }
            }
        }
    }
}
