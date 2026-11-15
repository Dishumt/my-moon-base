// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// HUD over the game (UI/Game/Hud.uxml): supplies at the top, day and time with ⏸ / ×1 / ×2 / ×4,
/// goals of the chapter on the left, link with Earth and the TABLET button at the bottom.
/// Reads GameState and the systems; the only actions are the speed buttons and opening the tablet.
/// Put it on a GameObject with UIDocument (Source Asset = UI/Game/Hud.uxml). Menu: Moon → Інтерфейс гри в Main.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class HudController : MonoBehaviour
{
    static readonly float[] Speeds = { 0f, 1f, 2f, 4f };

    VisualElement root, goalList;
    Label day, chapter, walk, demo, link;
    Button tablet;
    readonly Button[] speedButtons = new Button[4];
    SimBalanceData balance;
    bool bound;
    float nextClockText;

    void OnEnable()
    {
        GameEvents.StateChanged += Refresh;
        Loc.LanguageChanged += Refresh;
        Bind(logErrors: false);
    }

    void Start()
    {
        if (!bound) Bind(logErrors: true);
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.StateChanged -= Refresh;
        Loc.LanguageChanged -= Refresh;
        bound = false;
    }

    void Bind(bool logErrors)
    {
        if (bound) return;
        root = GetComponent<UIDocument>().rootVisualElement;
        if (root?.Q("hud") == null)
        {
            if (logErrors) Debug.LogError("HudController: 'hud' not found. Put UI/Game/Hud.uxml into Source Asset of this UIDocument.");
            return;
        }
        bound = true;
        root.pickingMode = PickingMode.Ignore;
        balance = DataLoader.Load<SimBalanceData>("Balance/balance");

        day = root.Q<Label>("hud-day");
        chapter = root.Q<Label>("hud-chapter");
        goalList = root.Q("hud-goal-list");
        walk = root.Q<Label>("hud-walk");
        demo = root.Q<Label>("hud-demo");
        link = root.Q<Label>("hud-link");
        tablet = root.Q<Button>("hud-tablet");
        if (tablet != null) tablet.clicked += () => { if (TabletController.Instance != null) TabletController.Instance.Open(); };

        for (int i = 0; i < Speeds.Length; i++)
        {
            float speed = Speeds[i]; // own copy for the lambda
            speedButtons[i] = root.Q<Button>("spd-" + (int)speed);
            if (speedButtons[i] != null) speedButtons[i].clicked += () => SetSpeed(speed);
        }
    }

    void SetSpeed(float speed)
    {
        var clock = GameBootstrap.Instance != null ? GameBootstrap.Instance.Clock : null;
        if (clock == null) return;
        if (speed <= 0f) clock.TogglePause();
        else clock.SetSpeed(speed);
    }

    void Update()
    {
        // the clock in the corner moves every quarter of a game hour, not only when the hour changes
        if (!bound || Time.unscaledTime < nextClockText) return;
        nextClockText = Time.unscaledTime + 0.25f;
        UpdateClockText();
    }

    void UpdateClockText()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || game.Clock == null || day == null) return;
        int minutes = Mathf.FloorToInt(game.Clock.HourProgress * 4f) * 15;
        string text = Loc.Get("hud.day", game.State.Day) + " · " + game.State.Hour.ToString("00") + ":" + minutes.ToString("00");
        var power = game.GetSystem<PowerSystem>();
        if (power != null && power.IsShade(game.State.Hour)) text += " · " + Loc.Get("hud.shadow");
        UiKit.SetText(day, text);
    }

    void Refresh()
    {
        if (!bound) return;
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || game.Clock == null) return;
        var s = game.State;
        var power = game.GetSystem<PowerSystem>();
        var chapters = game.GetSystem<ChapterSystem>();

        // supplies
        var ls = balance.lifeSupport;
        float oxygenMax = ls != null && ls.oxygenTankMax > 0f ? ls.oxygenTankMax : 100f;
        float oxygenPct = s.Res.Oxygen / oxygenMax * 100f;
        float waterStart = balance.start != null && balance.start.water > 0f ? balance.start.water : 60f;
        float batteryMax = power != null ? power.BatteryCapacity(s) : 0f;
        Chip("oxygen", Loc.Get("res.oxygen"), UiKit.Num(oxygenPct) + "%",
             ls != null && oxygenPct < ls.oxygenAlarmPercent, oxygenPct < 10f);
        Chip("water", Loc.Get("res.water"), UiKit.Num(s.Res.Water),
             ls != null && s.Res.Water / waterStart * 100f < ls.waterAlarmPercent, s.Res.Water < 1f);
        Chip("food", Loc.Get("res.food"), UiKit.Num(s.Res.Food), s.Res.Food < s.Crew.Count * 2f, s.Res.Food < 1f);
        Chip("battery", Loc.Get("hud.battery"), UiKit.Num(s.Res.Battery) + "/" + UiKit.Num(batteryMax),
             batteryMax > 0f && s.Res.Battery < batteryMax * 0.2f, false);
        Chip("materials", Loc.Get("hud.materials"), UiKit.Num(s.Res.Materials), false, false);
        Chip("medkits", Loc.Get("hud.medkits"), s.Res.MedKits.ToString(), false, false);

        // time and speed
        UpdateClockText();
        var clock = game.Clock;
        for (int i = 0; i < speedButtons.Length; i++)
        {
            var b = speedButtons[i];
            if (b == null) continue;
            bool paused = clock.UserPaused;
            b.EnableInClassList("hud-spd--paused", i == 0 && paused);
            b.EnableInClassList("hud-spd--on", i > 0 && !paused && Mathf.Approximately(clock.Speed, Speeds[i]));
        }
        if (speedButtons[0] != null) speedButtons[0].tooltip = Loc.Get("hud.pause");

        // goals of the chapter
        if (chapters != null && chapter != null && goalList != null)
        {
            string title = chapters.Current != null ? Loc.Get(chapters.Current.titleKey) : "";
            UiKit.SetText(chapter, Loc.Get("hud.chapter", s.Chapter, title));
            goalList.Clear();
            foreach (var g in chapters.GetProgress(s))
            {
                var line = UiKit.Label(goalList, "hud-goal" + (g.Done ? " hud-goal--done" : ""), GoalText(g));
                line.pickingMode = PickingMode.Ignore;
            }
        }

        // link with Earth: only when the chapter has comms
        var comms = game.GetSystem<CommsSystem>();
        bool hasComms = comms != null && chapters != null && chapters.IsSystemUnlocked("Comms");
        UiKit.Show(link, hasComms);
        if (hasComms)
        {
            bool open = comms.IsOpen(s);
            UiKit.SetText(link, Loc.Get(open ? "hud.comms_on" : "hud.comms_off"));
            link.EnableInClassList("hud-link--ok", open);
        }

        UiKit.SetText(tablet, Loc.Get("hud.tablet") + "  [TAB]");
        UiKit.SetText(walk, Loc.Get("hud.walk"));
        UiKit.Show(demo, game.IsDemo);
        if (game.IsDemo) UiKit.SetText(demo, Loc.Get("hud.demo"));
    }

    /// <summary>"✓ Build: HABITAT (1/1)".</summary>
    public static string GoalText(GoalProgress g)
    {
        string arg = !string.IsNullOrEmpty(g.Module) ? Loc.Get("mod." + g.Module) : UiKit.Num(g.Target);
        string progress = " (" + UiKit.Num(Mathf.Min(g.Current, g.Target)) + "/" + UiKit.Num(g.Target) + ")";
        return (g.Done ? "✓ " : "• ") + Loc.Get(g.TitleKey, arg) + progress;
    }

    void Chip(string id, string key, string value, bool low, bool bad)
    {
        UiKit.SetText(root.Q<Label>("h-" + id + "-k"), key);
        UiKit.SetText(root.Q<Label>("h-" + id + "-v"), value);
        var chip = root.Q("h-" + id);
        chip?.EnableInClassList("hud-chip--low", low && !bad);
        chip?.EnableInClassList("hud-chip--bad", bad);
    }
}
