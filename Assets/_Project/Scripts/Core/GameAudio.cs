// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Звук гри. Нічого не потрібно ставити в сцену: сам створюється при старті і живе між перезавантаженнями сцени.
///  - клік по будь-якій кнопці інтерфейсу;
///  - події гри: попередження і початок бурі, вечірній звіт (з тривогою, якщо були червоні рядки),
///    запуск ракети, прибуття поставки, готовий модуль, перемога, поразка;
///  - тихий фон космічної бази (і гул, поки триває буря);
///  - клавіша M вимикає / вмикає звук, вибір запам'ятовується (PlayerPrefs).
/// Звуки малює SoundSynth кодом, тож аудіофайлів у проєкті немає.
/// З коду: GameAudio.Play(Sfx.Confirm);  GameAudio.ToggleMute();
/// </summary>
public class GameAudio : MonoBehaviour
{
    const string MutedKey = "sound_muted";

    static GameAudio instance;
    static int muted = -1; // -1 = ще не читали з PlayerPrefs

    /// <summary>Звук вимкнено (клавіша M або кнопка в меню).</summary>
    public static bool Muted
    {
        get
        {
            if (muted < 0) muted = PlayerPrefs.GetInt(MutedKey, 0) == 1 ? 1 : 0;
            return muted == 1;
        }
    }

    /// <summary>Подія: звук увімкнули чи вимкнули (кнопки в інтерфейсі оновлюють свій напис).</summary>
    public static event System.Action MuteChanged;

    readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
    readonly HashSet<VisualElement> hooked = new HashSet<VisualElement>();
    AudioSource effects, ambient, storm;
    float nextScan;

    // стежимо за станом, щоб відрізнити «новий модуль готовий» і «кисень став малий»
    GameState lastState;
    int lastBuilt;
    bool oxygenAlarmed;
    SimBalanceData balance;

    // ---------- старт ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameAudio>();
    }

    /// <summary>Зіграти звук. Нічого не робить, якщо звуку немає (наприклад, у тестах).</summary>
    public static void Play(Sfx sfx)
    {
        if (instance == null || Muted) return;
        instance.PlayOne(sfx);
    }

    /// <summary>Вимкнути звук, якщо він увімкнений, і навпаки.</summary>
    public static void ToggleMute()
    {
        muted = Muted ? 0 : 1;
        PlayerPrefs.SetInt(MutedKey, muted);
        PlayerPrefs.Save();
        Apply();
        if (muted == 0) Play(Sfx.Toggle);
        MuteChanged?.Invoke();
    }

    static void Apply() => AudioListener.volume = Muted ? 0f : 1f;

    void Awake()
    {
        effects = NewSource("Effects", false, 0.8f);
        ambient = NewSource("Ambient", true, 0.22f);
        storm = NewSource("Storm", true, 0f);
        Apply();
    }

    AudioSource NewSource(string sourceName, bool loop, float volume)
    {
        var go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 0f; // 2D-звук: однаково чутно з будь-якого місця
        return source;
    }

    void OnEnable()
    {
        GameEvents.StormWarning += OnStormWarning;
        GameEvents.StormStarted += OnStormStarted;
        GameEvents.DayEnded += OnDayEnded;
        GameEvents.ChapterCompleted += OnChapterCompleted;
        GameEvents.GameOver += OnGameOver;
        GameEvents.LaunchFinished += OnLaunchFinished;
        GameEvents.SupplyArrived += OnSupplyArrived;
        GameEvents.StateChanged += OnStateChanged;
        GameEvents.HourPassed += OnHourPassed;
    }

    void OnDisable()
    {
        GameEvents.StormWarning -= OnStormWarning;
        GameEvents.StormStarted -= OnStormStarted;
        GameEvents.DayEnded -= OnDayEnded;
        GameEvents.ChapterCompleted -= OnChapterCompleted;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.LaunchFinished -= OnLaunchFinished;
        GameEvents.SupplyArrived -= OnSupplyArrived;
        GameEvents.StateChanged -= OnStateChanged;
        GameEvents.HourPassed -= OnHourPassed;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ---------- щокадру ----------

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.mKey.wasPressedThisFrame) ToggleMute();

        // фон: стартує після першого кадру (потрібен хоч один клік, щоб браузер дозволив звук — Unity робить це сам)
        if (!ambient.isPlaying) StartLoop(ambient, Sfx.Ambient);

        UpdateStormLoop();

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1.5f;
            HookButtons();
        }
    }

    void StartLoop(AudioSource source, Sfx sfx)
    {
        source.clip = ClipFor(sfx);
        source.Play();
    }

    /// <summary>Поки триває буря — тихий гул, який плавно наростає й затихає.</summary>
    void UpdateStormLoop()
    {
        var game = GameBootstrap.Instance;
        bool active = game != null && game.State != null && game.State.Radiation != null && game.State.Radiation.StormActive;
        float want = active ? 0.45f : 0f;
        storm.volume = Mathf.MoveTowards(storm.volume, want, Time.unscaledDeltaTime * 0.4f);
        if (storm.volume > 0.001f && !storm.isPlaying) StartLoop(storm, Sfx.StormLoop);
        else if (storm.volume <= 0.001f && storm.isPlaying) storm.Stop();
    }

    // ---------- клік по кнопках інтерфейсу ----------

    /// <summary>Знаходить усі UIDocument у сцені й вішає на них «слухача» кліків (раз на півтори секунди — нові теж).</summary>
    void HookButtons()
    {
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var root = doc.rootVisualElement;
            if (root == null || !hooked.Add(root)) continue;
            root.RegisterCallback<ClickEvent>(OnUiClick, TrickleDown.TrickleDown);
        }
        if (hooked.Count > 64) hooked.RemoveWhere(e => e == null || e.panel == null); // старі після перезавантаження сцени
    }

    void OnUiClick(ClickEvent evt)
    {
        var button = FindButton(evt.target as VisualElement);
        if (button == null || !button.enabledInHierarchy) return;
        string id = button.name ?? "";
        bool big = id == "menu-play" || id == "menu-demo" || id == "win-b1" || id == "launch-button";
        PlayOne(big ? Sfx.Confirm : Sfx.Click);
    }

    static Button FindButton(VisualElement element)
    {
        while (element != null)
        {
            if (element is Button button) return button;
            element = element.parent;
        }
        return null;
    }

    // ---------- події гри ----------

    void OnStormWarning(string id) => PlayOne(Sfx.StormWarning);

    void OnStormStarted(string id) => PlayOne(Sfx.StormStart); // камеру трусить StormFx

    void OnDayEnded(DayReport report)
    {
        bool alarm = report != null && report.Lines.Exists(l => l.Important);
        PlayOne(alarm ? Sfx.Alarm : Sfx.Report);
    }

    void OnChapterCompleted(int chapter) => PlayOne(Sfx.Win);
    void OnGameOver(string reason) => PlayOne(Sfx.Lose);
    void OnLaunchFinished(bool success) { if (!success) PlayOne(Sfx.Error); } // сумний злет уже зіграв екран запуску
    void OnSupplyArrived() => PlayOne(Sfx.Arrival);

    /// <summary>Новий готовий модуль: порівнюємо кількість збудованих з минулою.</summary>
    void OnStateChanged()
    {
        var game = GameBootstrap.Instance;
        var s = game != null ? game.State : null;
        if (s == null) return;
        int built = CountBuilt(s);
        if (!ReferenceEquals(s, lastState))
        {
            lastState = s;      // нова гра чи завантаження: нічого не вважаємо «новим»
            lastBuilt = built;
            oxygenAlarmed = false;
            return;
        }
        if (built > lastBuilt) PlayOne(Sfx.Built);
        lastBuilt = built;
    }

    /// <summary>Кисню стало замало: один раз пищимо, поки гравець не виправить.</summary>
    void OnHourPassed()
    {
        var game = GameBootstrap.Instance;
        var s = game != null ? game.State : null;
        if (s == null) return;
        if (balance == null) balance = DataLoader.Load<SimBalanceData>("Balance/balance");
        if (balance.lifeSupport == null || balance.lifeSupport.oxygenTankMax <= 0f) return;
        float percent = s.Res.Oxygen / balance.lifeSupport.oxygenTankMax * 100f;
        float alarm = balance.lifeSupport.oxygenAlarmPercent;
        if (!oxygenAlarmed && percent < alarm)
        {
            oxygenAlarmed = true;
            PlayOne(Sfx.Alarm);
        }
        else if (oxygenAlarmed && percent > alarm * 1.2f) oxygenAlarmed = false;
    }

    static int CountBuilt(GameState s)
    {
        int n = 0;
        foreach (var m in s.Modules) if (m.IsBuilt) n++;
        return n;
    }

    // ---------- відтворення ----------

    void PlayOne(Sfx sfx)
    {
        if (Muted || effects == null) return;
        effects.PlayOneShot(ClipFor(sfx));
    }

    AudioClip ClipFor(Sfx sfx)
    {
        if (clips.TryGetValue(sfx, out var clip) && clip != null) return clip;
        float[] samples = SoundSynth.Make(sfx);
        clip = AudioClip.Create("sfx_" + sfx, samples.Length, 1, SoundSynth.Rate, false);
        clip.SetData(samples, 0);
        clips[sfx] = clip;
        return clip;
    }
}
