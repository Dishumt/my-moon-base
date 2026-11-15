// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Меню паузи, екран «Як грати» і гарячі клавіші. Живе на тому самому об'єкті, що й GameWindows
/// (той самий UIDocument: у Windows.uxml є блоки pause-stage і help-stage), додається самé при старті сцени.
///  - Esc: меню паузи (продовжити · як грати · звук · мова · почати розділ спочатку · головне меню);
///  - H: «Як грати» (вперше показується само, коли гравець уперше потрапив на Місяць);
///  - P: пауза, 1 / 2 / 3: швидкість ×1 / ×2 / ×4, M: звук (це робить GameAudio);
///  - поки меню відкрите, гра стоїть (Clock.PushModal / PopModal).
/// Небезпечні кнопки (почати спочатку, у меню) просять другий клік протягом 3 секунд.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameMenu : MonoBehaviour
{
    public static GameMenu Instance { get; private set; }

    /// <summary>Відкрите меню паузи або довідка (планшет і вечірній звіт тоді не відкриваються).</summary>
    public static bool IsShown { get; private set; }

    const string HelpSeenKey = "help_seen";
    const int HelpLines = 7;
    const float ArmSeconds = 3f;

    enum Page { None, Pause, Help }

    Page page = Page.None;
    bool helpFromPause;     // довідку відкрили з меню паузи: «Зрозуміло» повертає в меню
    bool modalPushed, bound, helpChecked;
    string armed;           // кнопка, що чекає на другий клік
    float armedUntil;

    VisualElement root, pauseStage, helpStage;
    Label pauseTitle, pauseSub, helpTitle;
    ScrollView helpLines;
    Button resume, help, sound, lang, restart, menu, helpClose;

    // ---------- підключення до сцени ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    /// <summary>Знайти вікна гри (GameWindows) і додати на їхній об'єкт меню, якщо його ще немає.</summary>
    static void Attach()
    {
        var windows = FindAnyObjectByType<GameWindows>();
        if (windows != null && windows.GetComponent<GameMenu>() == null) windows.gameObject.AddComponent<GameMenu>();
    }

    void OnEnable()
    {
        Instance = this;
        Loc.LanguageChanged += Redraw;
        GameAudio.MuteChanged += Redraw;
        Bind(false);
    }

    void Start()
    {
        if (!bound) Bind(true);
    }

    void OnDisable()
    {
        Loc.LanguageChanged -= Redraw;
        GameAudio.MuteChanged -= Redraw;
        PopModal();
        IsShown = false;
        page = Page.None;
        bound = false;
        if (Instance == this) Instance = null;
    }

    void Bind(bool logErrors)
    {
        if (bound) return;
        root = GetComponent<UIDocument>().rootVisualElement;
        pauseStage = root?.Q("pause-stage");
        helpStage = root?.Q("help-stage");
        if (pauseStage == null || helpStage == null)
        {
            if (logErrors) Debug.LogError("GameMenu: 'pause-stage' / 'help-stage' not found in Windows.uxml.");
            return;
        }
        bound = true;
        pauseTitle = root.Q<Label>("pause-title");
        pauseSub = root.Q<Label>("pause-sub");
        helpTitle = root.Q<Label>("help-title");
        helpLines = root.Q<ScrollView>("help-lines");
        resume = Hook("pause-resume", Back);
        help = Hook("pause-help", () => OpenHelp(true));
        sound = Hook("pause-sound", GameAudio.ToggleMute);
        lang = Hook("pause-lang", () => Loc.SetLanguage(Loc.Language == Loc.English ? Loc.Ukrainian : Loc.English));
        restart = Hook("pause-restart", () => Danger("restart", Restart));
        menu = Hook("pause-menu", () => Danger("menu", ToMainMenu));
        helpClose = Hook("help-close", Back);
        UiKit.Show(pauseStage, false);
        UiKit.Show(helpStage, false);
    }

    Button Hook(string name, Action action)
    {
        var button = root.Q<Button>(name);
        if (button != null) button.clicked += action;
        return button;
    }

    // ---------- клавіші ----------

    void Update()
    {
        if (!bound) return;
        var k = Keyboard.current;
        if (k == null) return;

        if (page != Page.None)
        {
            if (k.escapeKey.wasPressedThisFrame || (page == Page.Help && k.hKey.wasPressedThisFrame)) Back();
            if (armed != null && Time.unscaledTime > armedUntil) { armed = null; Redraw(); }
            return;
        }

        var tablet = TabletController.Instance;
        if (UiKit.Blocked || (tablet != null && (tablet.IsOpen || tablet.LastCloseFrame == Time.frameCount))) return;

        if (!helpChecked) MaybeShowFirstHelp();
        if (page != Page.None) return;

        if (k.escapeKey.wasPressedThisFrame) OpenPause();
        else if (k.hKey.wasPressedThisFrame) OpenHelp(false);
        else if (k.pKey.wasPressedThisFrame) Clock()?.TogglePause();
        else if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) Clock()?.SetSpeed(1f);
        else if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) Clock()?.SetSpeed(2f);
        else if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) Clock()?.SetSpeed(4f);
    }

    static GameClock Clock() => GameBootstrap.Instance != null ? GameBootstrap.Instance.Clock : null;

    /// <summary>Перший раз на Місяці: сама показує «Як грати» (потім можна відкрити клавішею H).</summary>
    void MaybeShowFirstHelp()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || !GameStart.HasRequest) return; // меню ще не пройдено
        helpChecked = true;
        if (PlayerPrefs.GetInt(HelpSeenKey, 0) == 1) return;
        PlayerPrefs.SetInt(HelpSeenKey, 1);
        PlayerPrefs.Save();
        OpenHelp(false);
    }

    // ---------- відкрити / закрити ----------

    /// <summary>Меню паузи (кнопка MENU у HUD і клавіша Esc).</summary>
    public void OpenPause()
    {
        if (!bound || page != Page.None || UiKit.Blocked) return;
        GameBootstrap.Instance?.SaveNow(); // меню — найкраще місце зберегти: «Головне меню» нічого не втратить
        Push();
        armed = null;
        page = Page.Pause;
        Redraw();
    }

    /// <summary>«Як грати» (кнопка HELP у HUD і клавіша H).</summary>
    public void OpenHelp(bool fromPause)
    {
        if (!bound) return;
        if (!fromPause && (page != Page.None || UiKit.Blocked)) return;
        helpFromPause = fromPause;
        Push();
        page = Page.Help;
        Redraw();
    }

    void Back()
    {
        if (page == Page.Help && helpFromPause)
        {
            page = Page.Pause;
            helpFromPause = false;
            Redraw();
            return;
        }
        page = Page.None;
        armed = null;
        UiKit.Show(pauseStage, false);
        UiKit.Show(helpStage, false);
        IsShown = false;
        PopModal();
        GameEvents.RaiseStateChanged();
    }

    void Push()
    {
        IsShown = true;
        var clock = Clock();
        if (!modalPushed && clock != null)
        {
            clock.PushModal();
            modalPushed = true;
        }
        if (TabletController.Instance != null && TabletController.Instance.IsOpen) TabletController.Instance.Close();
    }

    void PopModal()
    {
        if (!modalPushed) return;
        modalPushed = false;
        Clock()?.PopModal();
    }

    // ---------- дії ----------

    /// <summary>Небезпечна дія: перший клік «зводить» кнопку, другий виконує.</summary>
    void Danger(string id, Action action)
    {
        if (armed == id)
        {
            armed = null;
            action();
            return;
        }
        armed = id;
        armedUntil = Time.unscaledTime + ArmSeconds;
        Redraw();
    }

    void Restart()
    {
        var game = GameBootstrap.Instance;
        int chapter = game != null && game.State != null ? game.State.Chapter : 1;
        bool demo = game != null && game.IsDemo;
        GameStart.Begin(demo ? StartMode.Demo : StartMode.Chapter, chapter);
    }

    void ToMainMenu()
    {
        GameBootstrap.Instance?.SaveNow();
        GameStart.BackToMenu();
    }

    // ---------- малювання ----------

    void Redraw()
    {
        if (!bound) return;
        UiKit.Show(pauseStage, page == Page.Pause);
        UiKit.Show(helpStage, page == Page.Help);
        bool demo = GameBootstrap.Instance != null && GameBootstrap.Instance.IsDemo;

        UiKit.SetText(pauseTitle, Loc.Get("pause.title"));
        UiKit.SetText(pauseSub, Loc.Get(demo ? "pause.sub_demo" : "pause.sub"));
        UiKit.SetText(resume, Loc.Get("pause.resume"));
        UiKit.SetText(help, Loc.Get("pause.help"));
        UiKit.SetText(sound, Loc.Get(GameAudio.Muted ? "pause.sound_off" : "pause.sound_on"));
        UiKit.SetText(lang, Loc.Get("pause.lang"));
        UiKit.SetText(restart, Loc.Get(armed == "restart" ? "pause.sure" : "pause.restart"));
        UiKit.SetText(menu, Loc.Get(armed == "menu" ? "pause.sure" : "pause.menu"));
        restart?.EnableInClassList("pause-btn--danger", armed == "restart");
        menu?.EnableInClassList("pause-btn--danger", armed == "menu");

        UiKit.SetText(helpTitle, Loc.Get("help.title"));
        UiKit.SetText(helpClose, Loc.Get("help.close"));
        if (page == Page.Help && helpLines != null)
        {
            helpLines.Clear();
            for (int i = 1; i <= HelpLines; i++)
            {
                var row = UiKit.Box(helpLines, "win-line");
                UiKit.Label(row, "win-line__text", Loc.Get("help.l" + i));
            }
        }
    }
}
