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

        State = continueFromSave ? SaveSystem.Load() : null;
        if (State == null) State = GameStateFactory.FromChapter(DataLoader.Load<ChapterStartData>(chapterFile));

        var balance = DataLoader.Load<CoreBalance>("Balance/balance");
        Clock = new GameClock(State, balance.realSecondsPerDay);
        RegisterSystems();

        GameEvents.DayEnded += OnDayEnded;
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

    void OnDayEnded(DayReport report)
    {
        if (autoSave) SaveSystem.Save(State);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        GameEvents.DayEnded -= OnDayEnded;
        Instance = null;
    }
}
