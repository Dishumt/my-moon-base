// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Start menu (UI/Game/StartMenu.uxml): PLAY · CONTINUE · chapters · DEMO FOR JUDGES · NASA DATA · EN / UK,
/// and "Space weather today" — a real NASA DONKI answer from SpaceWeatherFetcher (role 06) with a LIVE / ARCHIVE badge.
/// Shown only on the first start of the scene. After "Next chapter", "Try again" and so on (GameStart request)
/// the menu stays hidden and the game starts at once.
/// After the start: if the first mission has not flown yet (chapter 1), opens the launch screen of role 04.
/// Put it on a GameObject with UIDocument (Source Asset = UI/Game/StartMenu.uxml, Sort Order 30).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class StartMenu : MonoBehaviour
{
    /// <summary>The menu is on the screen now.</summary>
    public static bool IsShown { get; private set; }

    VisualElement root, stage;
    Label message, weatherTitle, weatherText, weatherBadge;
    NasaDataView nasaData;
    bool bound, modalPushed;

    void OnEnable()
    {
        Loc.LanguageChanged += ApplyTexts;
        SpaceWeatherFetcher.Updated += OnWeather;
        Bind(false);
    }

    void OnDisable()
    {
        Loc.LanguageChanged -= ApplyTexts;
        SpaceWeatherFetcher.Updated -= OnWeather;
        PopModal();
        IsShown = false;
        bound = false;
    }

    IEnumerator Start()
    {
        if (!bound) Bind(true);
        if (GameStart.HasRequest)
        {
            Hide();
            yield return null; // let the launch screen get ready first
            OpenLaunchIfNeeded();
            yield break;
        }

        // first start: show the menu and ask NASA about today's space weather
        IsShown = true;
        UiKit.Show(stage, true);
        if (!modalPushed && GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null)
        {
            GameBootstrap.Instance.Clock.PushModal();
            modalPushed = true;
        }
        if (SpaceWeatherFetcher.Latest != null) OnWeather(SpaceWeatherFetcher.Latest);
        else if (FindAnyObjectByType<SpaceWeatherFetcher>() == null) gameObject.AddComponent<SpaceWeatherFetcher>();
        ApplyTexts();
    }

    void Bind(bool logErrors)
    {
        if (bound) return;
        root = GetComponent<UIDocument>().rootVisualElement;
        stage = root?.Q("menu-stage");
        if (stage == null)
        {
            if (logErrors) Debug.LogError("StartMenu: 'menu-stage' not found. Put UI/Game/StartMenu.uxml into Source Asset of this UIDocument.");
            return;
        }
        bound = true;
        root.pickingMode = PickingMode.Ignore;
        message = root.Q<Label>("menu-msg");
        weatherTitle = root.Q<Label>("menu-weather-title");
        weatherText = root.Q<Label>("menu-weather-text");
        weatherBadge = root.Q<Label>("menu-weather-badge");

        On("menu-play", Play);
        On("menu-continue", () => GameStart.Begin(StartMode.Continue));
        On("menu-demo", () => GameStart.Begin(StartMode.Demo));
        var dataStage = root.Q("data-stage");
        if (dataStage != null)
        {
            nasaData = new NasaDataView(dataStage, null);
            On("menu-data", () => nasaData.Show());
        }
        for (int n = 1; n <= GameStart.LastChapter; n++)
        {
            int chapter = n; // own copy for the lambda
            On("menu-ch" + n, () => PickChapter(chapter));
        }
        On("menu-en", () => Loc.SetLanguage(Loc.English));
        On("menu-uk", () => Loc.SetLanguage(Loc.Ukrainian));
        if (!IsShown) UiKit.Show(stage, false);
    }

    void On(string name, System.Action action)
    {
        var button = root.Q<Button>(name);
        if (button != null) button.clicked += action;
    }

    // ---------- buttons ----------

    void Play()
    {
        var s = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        // the scene already made a fresh chapter 1: just start it; otherwise reload for chapter 1
        if (s == null || s.Chapter != 1 || s.Day != 1 || GameBootstrap.Instance.IsDemo)
        {
            GameStart.Begin(StartMode.Chapter, 1);
            return;
        }
        GameStart.MarkStarted(1);
        Hide();
        OpenLaunchIfNeeded();
    }

    void PickChapter(int chapter)
    {
        if (chapter > GameStart.UnlockedChapter)
        {
            UiKit.SetText(message, Loc.Get("menu.locked", chapter - 1));
            return;
        }
        if (chapter == 1) Play();
        else GameStart.Begin(StartMode.Chapter, chapter);
    }

    void Hide()
    {
        IsShown = false;
        UiKit.Show(stage, false);
        PopModal();
        GameEvents.RaiseStateChanged();
    }

    void PopModal()
    {
        if (!modalPushed) return;
        modalPushed = false;
        if (GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null) GameBootstrap.Instance.Clock.PopModal();
    }

    /// <summary>Chapter 1 starts on Earth: the first mission (launch screen of role 04) must fly first.</summary>
    static void OpenLaunchIfNeeded()
    {
        var s = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        if (s == null || s.Supply.FirstMissionDone) return;
        if (LaunchScreen.Instance != null && !LaunchScreen.Instance.IsOpen) LaunchScreen.Instance.Open();
    }

    // ---------- texts ----------

    void ApplyTexts()
    {
        if (!bound) return;
        SetText("menu-title", Loc.Get("menu.title"));
        SetText("menu-sub", Loc.Get("menu.sub"));
        SetText("menu-play", Loc.Get("menu.play"));
        SetText("menu-continue", Loc.Get("menu.continue"));
        UiKit.Show(root.Q("menu-continue"), SaveSystem.HasSave());
        SetText("menu-demo", Loc.Get("menu.demo"));
        SetText("menu-data", Loc.Get("menu.data"));
        int unlocked = GameStart.UnlockedChapter;
        for (int n = 1; n <= GameStart.LastChapter; n++)
        {
            var b = root.Q<Button>("menu-ch" + n);
            if (b == null) continue;
            var data = DataLoader.Load<ChapterStartData>("Chapters/chapter_" + n.ToString("00"));
            b.text = Loc.Get("menu.chapter", n) + "\n<size=12>" + Loc.Get(data.titleKey) + "</size>";
            b.EnableInClassList("menu-ch--locked", n > unlocked);
        }
        root.Q("menu-en")?.EnableInClassList("menu-lang-btn--on", Loc.Language == Loc.English);
        root.Q("menu-uk")?.EnableInClassList("menu-lang-btn--on", Loc.Language == Loc.Ukrainian);
        SetText("menu-credit", Loc.Get("menu.credit"));
        UiKit.SetText(message, "");
        UiKit.SetText(weatherTitle, Loc.Get("weather.title"));
        if (SpaceWeatherFetcher.Latest != null) OnWeather(SpaceWeatherFetcher.Latest);
        else UiKit.SetText(weatherText, Loc.Get("menu.weather_wait"));
    }

    void OnWeather(SpaceWeatherResult r)
    {
        if (!bound || r == null) return;
        UiKit.SetText(weatherText, Loc.Get(r.TextKey, r.ClassType, r.TimeUtc));
        UiKit.SetText(weatherBadge, r.Badge);
        weatherBadge?.EnableInClassList("menu-badge--live", r.Source == SpaceWeatherSource.Live);
    }

    void SetText(string name, string text) => UiKit.SetText(root.Q<TextElement>(name), text);
}
