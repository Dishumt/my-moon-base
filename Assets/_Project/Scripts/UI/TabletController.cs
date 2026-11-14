// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Engineer tablet (role 05). Put it on the GameObject with UIDocument whose Source Asset is UI/Tablet/Tablet.uxml.
///  - Tab opens / closes the tablet, Esc, the X button or a click on the dark backdrop closes it;
///  - while the tablet is open the game is paused (Clock.PushModal / PopModal);
///  - 5 tabs switch the panes (for now every pane is a placeholder);
///  - the strip at the bottom shows water, oxygen and food from GameState.
/// Other scripts (for example the HUD "Tablet" button) call TabletController.Instance.Open().
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TabletController : MonoBehaviour
{
    public static TabletController Instance { get; private set; }

    static readonly string[] TabIds = { "map", "power", "crew", "supply", "comms" };
    const int Segments = 20;            // squares in every resource bar
    const long CloseAnimationMs = 280;  // same as transition-duration of .tablet in Theme.uss

    [Tooltip("Which tab is shown first: map, power, crew, supply or comms")]
    [SerializeField] string startTab = "power";

    [Tooltip("Food bar turns orange below this percent. TODO: ask role 03 to add it to balance.json")]
    [SerializeField] float foodLowPercent = 25f;

    public bool IsOpen { get; private set; }
    public string CurrentTab { get; private set; }

    VisualElement root, stage, tablet;
    Label dayLabel;
    VisualElement pausedBox;
    readonly Dictionary<string, Button> tabButtons = new Dictionary<string, Button>();
    readonly Dictionary<string, VisualElement> panes = new Dictionary<string, VisualElement>();
    TabletBalance balance;

    // ---------- start / stop ----------

    bool bound; // true when the buttons are connected to the .uxml

    void OnEnable()
    {
        // UIDocument builds its elements in its own OnEnable. If it has not done it yet, Start will try again.
        Bind(logErrors: false);
    }

    void Start()
    {
        if (!bound) Bind(logErrors: true);
    }

    void Bind(bool logErrors)
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        stage = root?.Q("tablet-stage");
        tablet = root?.Q("tablet");
        if (stage == null || tablet == null)
        {
            if (logErrors)
                Debug.LogError("TabletController: 'tablet-stage' not found. Put UI/Tablet/Tablet.uxml into Source Asset of this UIDocument.");
            return;
        }
        bound = true;

        Instance = this;
        balance = DataLoader.Load<TabletBalance>("Balance/balance");

        dayLabel = root.Q<Label>("t-day");
        pausedBox = root.Q("t-paused");

        tabButtons.Clear();
        panes.Clear();
        foreach (var id in TabIds)
        {
            string tabId = id; // own copy for the lambda below
            var button = root.Q<Button>("tab-" + id);
            if (button != null)
            {
                button.clicked += () => ShowTab(tabId);
                tabButtons[id] = button;
            }
            var pane = root.Q("pane-" + id);
            if (pane != null) panes[id] = pane;
        }

        var close = root.Q<Button>("tablet-close");
        if (close != null) close.clicked += Close;
        root.Q("backdrop")?.RegisterCallback<ClickEvent>(_ => Close());

        BuildSegments("water");
        BuildSegments("oxygen");
        BuildSegments("food");

        GameEvents.StateChanged += Refresh;
        Loc.LanguageChanged += ApplyTexts;

        IsOpen = false;
        stage.style.display = DisplayStyle.None;
        tablet.RemoveFromClassList("tablet--open");

        ApplyTexts();
        ShowTab(startTab);
    }

    void OnDisable()
    {
        if (!bound) return;
        bound = false;
        GameEvents.StateChanged -= Refresh;
        Loc.LanguageChanged -= ApplyTexts;

        // the game must not stay paused if this object is switched off while open
        if (IsOpen) GameBootstrap.Instance?.Clock?.PopModal();
        IsOpen = false;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (!bound || keyboard == null) return;

        if (keyboard.tabKey.wasPressedThisFrame) Toggle();
        else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame) Close();
    }

    // ---------- open / close ----------

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    public void Open()
    {
        if (!bound || IsOpen) return;
        IsOpen = true;
        GameBootstrap.Instance?.Clock?.PushModal();

        stage.style.display = DisplayStyle.Flex;
        // add the class one frame later, so the fade-in animation plays
        tablet.schedule.Execute(() => { if (IsOpen) tablet.AddToClassList("tablet--open"); });

        Refresh();
        GameEvents.RaiseStateChanged(); // HUD and others: "the game is paused now"
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        GameBootstrap.Instance?.Clock?.PopModal();

        tablet.RemoveFromClassList("tablet--open");
        // hide completely after the fade-out, so the stage does not catch mouse clicks
        stage.schedule.Execute(() => { if (!IsOpen) stage.style.display = DisplayStyle.None; })
                      .StartingIn(CloseAnimationMs);

        GameEvents.RaiseStateChanged();
    }

    // ---------- tabs ----------

    public void ShowTab(string id)
    {
        if (!panes.ContainsKey(id)) id = TabIds[0];
        CurrentTab = id;

        foreach (var pair in tabButtons) pair.Value.EnableInClassList("tab--on", pair.Key == id);
        foreach (var pair in panes) pair.Value.style.display = pair.Key == id ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ---------- texts ----------

    void ApplyTexts()
    {
        SetText("t-title", Loc.Get("tablet.title"));
        SetText("t-paused-label", Loc.Get("tablet.paused"));
        SetText("tablet-close", "X");

        foreach (var id in TabIds)
        {
            if (tabButtons.TryGetValue(id, out var button)) button.text = Loc.Get("tab." + id);
            // placeholder until the real tab is made: "POWER — This is not ready yet."
            SetText("pane-" + id + "-stub", Loc.Get("tab." + id) + " — " + Loc.Get("error.not_implemented"));
        }

        SetText("r-water-label", Loc.Get("res.water"));
        SetText("r-oxygen-label", Loc.Get("res.oxygen"));
        SetText("r-food-label", Loc.Get("res.food"));

        Refresh();
    }

    void SetText(string name, string text)
    {
        var element = root.Q<TextElement>(name);
        if (element != null) element.text = text;
    }

    // ---------- numbers from GameState ----------

    void Refresh()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;
        var s = game.State;

        if (dayLabel != null) dayLabel.text = Loc.Get("tablet.day", s.Day, s.Hour.ToString("00") + ":00");
        if (pausedBox != null)
            pausedBox.style.display = game.Clock != null && game.Clock.IsPaused ? DisplayStyle.Flex : DisplayStyle.None;

        var ls = balance.lifeSupport;
        var start = balance.start;
        SetBar("water", s.Res.Water, start.water, ls.waterAlarmPercent);
        SetBar("oxygen", s.Res.Oxygen, ls.oxygenTankMax, ls.oxygenAlarmPercent);
        SetBar("food", s.Res.Food, start.food, foodLowPercent);
    }

    void BuildSegments(string kind)
    {
        var segs = root.Q("r-" + kind + "-segs");
        if (segs == null) return;
        segs.Clear();
        for (int i = 0; i < Segments; i++)
        {
            var seg = new VisualElement();
            seg.AddToClassList("res__seg");
            segs.Add(seg);
        }
    }

    /// <summary>Fill the bar: value out of max; below lowPercent the bar turns orange.</summary>
    void SetBar(string kind, float value, float max, float lowPercent)
    {
        float percent = max > 0f ? value / max * 100f : 0f;
        bool low = percent < lowPercent;
        int filled = Mathf.Clamp(Mathf.RoundToInt(percent / 100f * Segments), 0, Segments);

        root.Q("r-" + kind)?.EnableInClassList("res--low", low);
        SetText("r-" + kind + "-value", Mathf.RoundToInt(value).ToString());

        var segs = root.Q("r-" + kind + "-segs");
        if (segs == null) return;
        for (int i = 0; i < segs.childCount; i++)
        {
            var seg = segs[i];
            bool on = i < filled;
            seg.EnableInClassList("res__seg--" + kind, on && !low);
            seg.EnableInClassList("res__seg--low", on && low);
        }
    }

    // ---------- the part of balance.json the tablet reads (owner of the file: role 03) ----------

    [Serializable]
    public class TabletBalance
    {
        public LifeSupportPart lifeSupport = new LifeSupportPart();
        public StartPart start = new StartPart();
    }

    [Serializable]
    public class LifeSupportPart
    {
        public float oxygenTankMax = 100f;
        public float oxygenAlarmPercent = 25f;
        public float waterAlarmPercent = 20f;
    }

    [Serializable]
    public class StartPart
    {
        public float water = 60f;
        public float food = 40f;
    }
}
