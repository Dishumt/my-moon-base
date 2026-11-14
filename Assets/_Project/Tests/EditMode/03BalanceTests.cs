// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Охоронці балансу: проганяють справжні системи зі справжніх balance.json, modules.json і chapter_0N.json
/// без Unity-сцени: стан створює GameStateFactory, час веде справжній GameClock (день починається о 6:00).
/// Вони перевіряють правила з Задачі 6:
///   - нічого не робити = програти в кожному розділі;
///   - недбало (без життєзабезпечення або без теплиці) = програти;
///   - обережна гра виживає.
/// Якщо ти змінив числа в JSON і тест став червоним, значить баланс зламав одне з цих правил.
/// Видобуток льоду (роль 04) тут імітується: +10 води щодня о 12:00, де написано IcePerDay.
/// Очки науки й поставки (роль 04) тут не моделюються, тому розділи 2–3 цілком пройти ці тести не можуть.
/// </summary>
public class BalanceGuardTests
{
    private int _completedCount;
    private int _overCount;
    private string _overKey;

    [SetUp]
    public void SetUp()
    {
        GameEvents.ClearAll();
        _completedCount = 0;
        _overCount = 0;
        _overKey = null;
        GameEvents.ChapterCompleted += c => _completedCount++;
        GameEvents.GameOver += key => { _overCount++; _overKey = key; };
    }

    [TearDown]
    public void TearDown()
    {
        GameEvents.ClearAll();
    }

    // ───────────────────────── Мінімальна гра без сцени ─────────────────────────

    private class Game
    {
        public GameState S;
        public GameClock Clock;
        public ChapterSystem Chapter;
        public ConstructionSystem Build;
        public float IcePerDay; // скільки води «добуває геолог» щодня о 12:00
    }

    private static Game Start(int chapter)
    {
        var balance = DataLoader.Load<SimBalanceData>("Balance/balance");
        var s = GameStateFactory.FromChapter(
            DataLoader.Load<ChapterStartData>("Chapters/chapter_" + chapter.ToString("00")));

        var power = new PowerSystem(balance);
        var life = new LifeSupportSystem(power, balance);
        var greenhouse = new GreenhouseSystem(power, balance);
        var food = new FoodSystem(balance);
        var build = new ConstructionSystem(power, balance, BuildCostTable.Load());
        var chapterSystem = new ChapterSystem(food, build);

        // Той самий порядок систем, що в GameBootstrap (без систем ролей 04 і 06).
        var clock = new GameClock(s, balance.realSecondsPerDay);
        clock.Register(power);
        clock.Register(life);
        clock.Register(greenhouse);
        clock.Register(food);
        clock.Register(build);
        clock.Register(chapterSystem);

        chapterSystem.StartChapter(s, chapter, true);

        return new Game { S = s, Clock = clock, Chapter = chapterSystem, Build = build };
    }

    private static void OneHour(Game g)
    {
        if (g.S.Hour == 12 && g.IcePerDay > 0f) g.S.Res.Water += g.IcePerDay;
        g.Clock.AdvanceHours(1);
    }

    // Проганяє, доки не закінчиться розділ (перемога/поразка) або не мине maxDays.
    private static void RunDays(Game g, int maxDays)
    {
        for (int h = 0; h < maxDays * 24 && !g.Chapter.IsFinished; h++) OneHour(g);
    }

    // Проганяє, доки не настане початок дня untilDay (або не закінчиться розділ).
    private static void RunUntilDay(Game g, int untilDay)
    {
        for (int h = 0; h < 40 * 24 && g.S.Day < untilDay && !g.Chapter.IsFinished; h++) OneHour(g);
    }

    private static void Plan(Game g, int ls, int greenhouse, int robots)
    {
        g.S.Power.LifeSupport = ls;
        g.S.Power.Greenhouse = greenhouse;
        g.S.Power.Robots = robots;
    }

    // ───────────────────────── Нічого не робити ─────────────────────────

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DoNothing_LosesEveryChapter(int chapter)
    {
        var g = Start(chapter); // лишається початковий план енергії з JSON, гравець нічого не міняє й не будує

        RunDays(g, 15);

        Assert.AreEqual(1, _overCount, "Гра має закінчитися поразкою");
        Assert.AreEqual(0, _completedCount, "Розділ не можна пройти, нічого не роблячи");
    }

    // ───────────────────────── Розділ 1 ─────────────────────────

    [Test]
    public void Chapter1_CarefulPlay_Wins()
    {
        var g = Start(1);
        Plan(g, ls: 2, greenhouse: 0, robots: 2);
        Assert.IsNull(g.Build.Order(g.S, ModuleType.Habitat, 10));
        Assert.IsNull(g.Build.Order(g.S, ModuleType.Solar, 11));

        RunDays(g, 8);

        Assert.AreEqual(0, _overCount, "Обережна гра не має програвати, а програла: " + _overKey);
        Assert.AreEqual(1, _completedCount, "Розділ 1 має проходитися");
    }

    [Test]
    public void Chapter1_NoLifeSupport_Loses()
    {
        var g = Start(1);
        Plan(g, ls: 0, greenhouse: 0, robots: 2); // недбало: усе на будівництво, життєзабезпечення вимкнене
        g.Build.Order(g.S, ModuleType.Habitat, 10);
        g.Build.Order(g.S, ModuleType.Solar, 11);

        RunDays(g, 8);

        Assert.AreEqual(1, _overCount, "Без життєзабезпечення можна виграти розділ 1, а це недбала гра");
        Assert.AreEqual("gameover.no_oxygen", _overKey);
        Assert.AreEqual(0, _completedCount);
    }

    // ───────────────────────── Розділи 2 і 3: теплиця потрібна ─────────────────────────

    [TestCase(2)]
    [TestCase(3)]
    public void WithoutGreenhouse_CannotSurviveTheChapter(int chapter)
    {
        var g = Start(chapter);
        g.IcePerDay = 10f;
        Plan(g, ls: 3, greenhouse: 0, robots: 0);

        RunDays(g, 12);

        Assert.AreEqual(1, _overCount, "Без теплиці розділ " + chapter + " має програватися (їжа закінчується)");
        Assert.AreEqual(0, _completedCount);
    }

    [Test]
    public void Chapter2_WithGreenhouse_SurvivesSevenDays()
    {
        var g = Start(2);
        g.IcePerDay = 10f;
        Plan(g, ls: 3, greenhouse: 2, robots: 2);
        Assert.IsNull(g.Build.Order(g.S, ModuleType.Greenhouse, 10));

        RunUntilDay(g, 8); // сім днів прожито

        Assert.AreEqual(0, _overCount, "Обережна гра з теплицею програла: " + _overKey);
        Assert.AreEqual(8, g.S.Day);
    }

    [Test]
    public void Chapter3_WithGreenhouse_SurvivesEightDays()
    {
        var g = Start(3);
        g.IcePerDay = 10f;
        Plan(g, ls: 3, greenhouse: 2, robots: 2);
        Assert.IsNull(g.Build.Order(g.S, ModuleType.Shelter, 10));
        Assert.IsNull(g.Build.Order(g.S, ModuleType.Medbay, 11));

        RunUntilDay(g, 9); // вісім днів прожито

        Assert.AreEqual(0, _overCount, "Обережна гра з теплицею програла: " + _overKey);
        Assert.AreEqual(9, g.S.Day);
    }
}
