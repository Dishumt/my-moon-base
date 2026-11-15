// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Запускає гру: створює GameState з розділу, годинник і всі системи в правильному порядку.
/// Стоїть у кожній сцені (Main і Sandbox) на об'єкті "Game".
/// Інші скрипти беруть усе через GameBootstrap.Instance:
///   var s = GameBootstrap.Instance.State;
///   GameBootstrap.Instance.Clock.Pause();
///   GameBootstrap.Instance.GetSystem&lt;ConstructionSystem&gt;().Order(...);
/// </summary>
[DefaultExecutionOrder(-100)] // запускається раніше за інші скрипти
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    [Tooltip("Файл розділу в Resources/Data/, без .json")]
    [SerializeField] string chapterFile = "Chapters/chapter_01";

    [Tooltip("Продовжити збережену гру, якщо вона є")]
    [SerializeField] bool continueFromSave = false;

    [Tooltip("Зберігати гру наприкінці кожного дня")]
    [SerializeField] bool autoSave = true;

    public GameState State { get; private set; }
    public GameClock Clock { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("GameBootstrap: у сцені два GameBootstrap, зайвий вимкнено.");
            enabled = false;
            return;
        }
        Instance = this;

        // Запит старту з меню чи вікна перемоги (GameStart) важливіший за поля в інспекторі.
        DemoData demo = null;
        bool fromSave = continueFromSave;
        string file = chapterFile;
        if (GameStart.HasRequest)
        {
            fromSave = GameStart.Mode == StartMode.Continue;
            int chapter = GameStart.Chapter;
            if (GameStart.Mode == StartMode.Demo)
            {
                demo = DataLoader.Load<DemoData>("Demo/demo");
                chapter = demo.chapter;
            }
            file = "Chapters/chapter_" + chapter.ToString("00");
        }
        IsDemo = demo != null;

        State = fromSave ? SaveSystem.Load() : null;
        bool freshGame = State == null;
        if (State == null) State = GameStateFactory.FromChapter(DataLoader.Load<ChapterStartData>(file));

        var balance = DataLoader.Load<CoreBalance>("Balance/balance");
        float secondsPerDay = demo != null && demo.realSecondsPerDay > 0f ? demo.realSecondsPerDay : balance.realSecondsPerDay;
        Clock = new GameClock(State, secondsPerDay);
        RegisterSystems();

        // 03: відкрити модулі розділу (які можна замовляти). Для нової гри ChapterSystem ще й ставить
        // стартові модулі (панелі тощо), бо GameStateFactory їх не створює.
        var chapters = GetSystem<ChapterSystem>();
        if (chapters != null) chapters.StartChapter(State, State.Chapter, freshGame);

        // Перша місія з Землі (екран запуску 04) — тільки на початку розділу 1.
        // Розділи 2–3 і демо починаються вже на Місяці.
        if (freshGame && (State.Chapter > 1 || demo != null)) State.Supply.FirstMissionDone = true;
        if (demo != null) ApplyDemo(demo);

        GameEvents.DayEnded += OnDayEnded;
        GameEvents.HourPassed += OnHourPassed;
    }

    /// <summary>Чи це «Demo for judges» (тоді не зберігаємо гру).</summary>
    public bool IsDemo { get; private set; }

    /// <summary>Демо: щедрі запаси і справжня буря NASA DONKI за хвилину після старту.</summary>
    void ApplyDemo(DemoData demo)
    {
        State.Hour = Mathf.Clamp(demo.startHour, 0, 23);
        if (demo.resources != null)
        {
            State.Res.Battery = demo.resources.Battery;
            State.Res.Water = demo.resources.Water;
            State.Res.Oxygen = demo.resources.Oxygen;
            State.Res.Food = demo.resources.Food;
            State.Res.Materials = demo.resources.Materials;
            State.Res.MedKits = demo.resources.MedKits;
        }
        var radiation = GetSystem<RadiationSystem>();
        if (radiation != null && !string.IsNullOrEmpty(demo.stormEventId))
        {
            string err = radiation.ForceStorm(State, demo.stormEventId, demo.stormInHours, Clock.Report);
            if (err != null) Debug.LogWarning("GameBootstrap: демо-буря не почалась: " + err);
        }
    }

    /// <summary>Порядок систем — як у CLAUDE.md. Не міняй без техліда!</summary>
    void RegisterSystems()
    {
        // Деякі системи 03 читають інші системи (енергію, їжу, будівництво),
        // тому створюємо їх спочатку, а реєструємо в тому самому порядку.
        var power = new PowerSystem();
        var food = new FoodSystem();
        var construction = new ConstructionSystem(power);

        Clock.Register(power);                          // 03
        Clock.Register(new LifeSupportSystem(power));   // 03
        Clock.Register(new GreenhouseSystem(power));    // 03
        Clock.Register(food);                           // 03
        Clock.Register(construction);                   // 03
        Clock.Register(new CrewSystem());         // 04
        Clock.Register(new RadiationSystem());    // 06
        Clock.Register(new HealthSystem());       // 06
        Clock.Register(new SupplySystem());       // 04
        Clock.Register(new CommsSystem());        // 04
        Clock.Register(new ChapterSystem(food, construction)); // 03
        Clock.Register(new IncidentSystem());     // 01: випадкові поломки (унітаз, пил, мікрометеорит)
    }

    /// <summary>Знайти систему за типом, наприклад GetSystem&lt;CrewSystem&gt;().</summary>
    public T GetSystem<T>() where T : class, ISimSystem
    {
        foreach (var system in Clock.Systems)
        {
            if (system is T found) return found;
        }
        return null;
    }

    void Update()
    {
        Clock.Update(Time.deltaTime);
    }

    void OnDayEnded(DayReport report) => SaveNow();

    /// <summary>
    /// Ще й тричі на день (6:00, 12:00, 18:00): якщо закрити вкладку браузера посеред дня, пропаде не більше пів дня.
    /// О 24:00 не зберігаємо тут: день ще не перемкнувся, це робить OnDayEnded.
    /// </summary>
    void OnHourPassed()
    {
        if (State != null && State.Hour < 24 && State.Hour % 6 == 0) SaveNow();
    }

    /// <summary>
    /// Зберегти гру зараз (кінець дня, меню паузи). Демо не зберігаємо; пройдений чи програний розділ теж
    /// (інакше «Продовжити» поверне його).
    /// </summary>
    public void SaveNow()
    {
        if (State == null || Clock == null) return;
        var chapters = GetSystem<ChapterSystem>();
        if (autoSave && !IsDemo && (chapters == null || !chapters.IsFinished)) SaveSystem.Save(State);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        GameEvents.DayEnded -= OnDayEnded;
        GameEvents.HourPassed -= OnHourPassed;
        Instance = null;
    }
}
