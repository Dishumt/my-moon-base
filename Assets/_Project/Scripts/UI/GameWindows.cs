// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Windows over the game (UI/Game/Windows.uxml), one at a time; while a window is open the game is paused:
///  - evening report (GameEvents.DayEnded): what changed today and WHY, alarms in red;
///  - chapter complete (GameEvents.ChapterCompleted): what the player learned, next chapter / play again / menu;
///  - mission stopped (GameEvents.GameOver): the reason, what happened today, try again / menu.
/// Put it on a GameObject with UIDocument (Source Asset = UI/Game/Windows.uxml, Sort Order 20).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameWindows : MonoBehaviour
{
    /// <summary>A window is open now (the tablet does not open over it).</summary>
    public static bool IsShown { get; private set; }

    enum Kind { Report, Won, Lost }

    class Pending
    {
        public Kind Kind;
        public DayReport Report;
        public string Key;
        public int Chapter;
    }

    // report lines with a number, and the unit shown next to the number
    static readonly Dictionary<string, string> Units = new Dictionary<string, string>
    {
        { "report.oxygen_made", "res.oxygen" }, { "report.oxygen_used", "res.oxygen" },
        { "report.water_used_lifesupport", "res.water" }, { "report.ice_mined", "res.water" },
        { "report.food_eaten", "res.food" }, { "report.harvest", "res.food" },
        { "report.battery_change", "hud.battery" }, { "report.science_gained", "hud.science" },
        { "report.medkit_given", "hud.medkits" }, { "report.dose_day", "" },
    };

    readonly Queue<Pending> queue = new Queue<Pending>();
    Pending current;
    bool finished;      // the chapter is won or lost: no more evening reports
    bool modalPushed;

    VisualElement root, stage, window;
    Label title, sub, goals;
    ScrollView lines;
    Button b1, b2, b3;
    Action a1, a2, a3;
    bool bound;

    void OnEnable()
    {
        GameEvents.DayEnded += OnDayEnded;
        GameEvents.ChapterCompleted += OnChapterCompleted;
        GameEvents.GameOver += OnGameOver;
        Loc.LanguageChanged += Redraw;
        Bind(false);
    }

    void Start()
    {
        if (!bound) Bind(true);
    }

    void OnDisable()
    {
        GameEvents.DayEnded -= OnDayEnded;
        GameEvents.ChapterCompleted -= OnChapterCompleted;
        GameEvents.GameOver -= OnGameOver;
        Loc.LanguageChanged -= Redraw;
        PopModal();
        IsShown = false;
        bound = false;
    }

    void Bind(bool logErrors)
    {
        if (bound) return;
        root = GetComponent<UIDocument>().rootVisualElement;
        stage = root?.Q("win-stage");
        if (stage == null)
        {
            if (logErrors) Debug.LogError("GameWindows: 'win-stage' not found. Put UI/Game/Windows.uxml into Source Asset of this UIDocument.");
            return;
        }
        bound = true;
        root.pickingMode = PickingMode.Ignore;
        window = root.Q("win");
        title = root.Q<Label>("win-title");
        sub = root.Q<Label>("win-sub");
        goals = root.Q<Label>("win-goals");
        lines = root.Q<ScrollView>("win-lines");
        b1 = root.Q<Button>("win-b1");
        b2 = root.Q<Button>("win-b2");
        b3 = root.Q<Button>("win-b3");
        if (b1 != null) b1.clicked += () => a1?.Invoke();
        if (b2 != null) b2.clicked += () => a2?.Invoke();
        if (b3 != null) b3.clicked += () => a3?.Invoke();
        UiKit.Show(stage, false);
        if (current != null) Redraw();
    }

    // ---------- events ----------

    void OnDayEnded(DayReport r)
    {
        if (finished || r == null) return;
        Enqueue(new Pending { Kind = Kind.Report, Report = r });
    }

    void OnChapterCompleted(int chapter)
    {
        if (finished) return;
        finished = true;
        GameStart.UnlockAfter(chapter);
        SaveSystem.Delete(); // "Continue" must not bring back a finished chapter
        ShowNow(new Pending { Kind = Kind.Won, Chapter = chapter });
    }

    void OnGameOver(string reasonKey)
    {
        if (finished) return;
        finished = true;
        SaveSystem.Delete();
        var clock = GameBootstrap.Instance != null ? GameBootstrap.Instance.Clock : null;
        ShowNow(new Pending { Kind = Kind.Lost, Key = reasonKey, Report = clock?.Report });
    }

    void Enqueue(Pending p)
    {
        if (current == null) Show(p);
        else queue.Enqueue(p);
    }

    void ShowNow(Pending p)
    {
        queue.Clear();
        Show(p);
    }

    // ---------- show / close ----------

    void Show(Pending p)
    {
        current = p;
        IsShown = true;
        if (!modalPushed && GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null)
        {
            GameBootstrap.Instance.Clock.PushModal();
            modalPushed = true;
        }
        if (TabletController.Instance != null && TabletController.Instance.IsOpen) TabletController.Instance.Close();
        Redraw();
        UiKit.Show(stage, true);
    }

    void CloseCurrent()
    {
        current = null;
        if (queue.Count > 0)
        {
            Show(queue.Dequeue());
            return;
        }
        UiKit.Show(stage, false);
        IsShown = false;
        PopModal();
        GameEvents.RaiseStateChanged();
    }

    void PopModal()
    {
        if (!modalPushed) return;
        modalPushed = false;
        if (GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null) GameBootstrap.Instance.Clock.PopModal();
    }

    // ---------- drawing ----------

    void Redraw()
    {
        if (!bound || current == null) return;
        var s = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        bool demo = GameBootstrap.Instance != null && GameBootstrap.Instance.IsDemo;
        lines.Clear();
        window.EnableInClassList("win--bad", current.Kind == Kind.Lost);
        window.EnableInClassList("win--good", current.Kind == Kind.Won);
        title.EnableInClassList("win-title--bad", current.Kind == Kind.Lost);
        title.EnableInClassList("win-title--good", current.Kind == Kind.Won);

        switch (current.Kind)
        {
            case Kind.Report:
                UiKit.SetText(title, Loc.Get("report.title") + " · " + Loc.Get("hud.day", current.Report.Day));
                UiKit.SetText(sub, Loc.Get("win.report_sub"));
                AddLines(current.Report);
                Buttons(Loc.Get("win.next_day"), CloseCurrent, null, null, null, null);
                break;

            case Kind.Won:
            {
                int n = current.Chapter;
                UiKit.SetText(title, Loc.Get("win.won_title", n));
                UiKit.SetText(sub, Loc.Get("win.learned." + n));
                bool last = n >= GameStart.LastChapter;
                if (demo)
                    Buttons(Loc.Get("win.menu"), GameStart.BackToMenu, Loc.Get("win.replay"), () => GameStart.Begin(StartMode.Demo), null, null);
                else if (last)
                    Buttons(Loc.Get("win.menu"), GameStart.BackToMenu, Loc.Get("win.replay"), () => GameStart.Begin(StartMode.Chapter, n), null, null);
                else
                    Buttons(Loc.Get("win.next_chapter"), () => GameStart.Begin(StartMode.Chapter, n + 1),
                            Loc.Get("win.replay"), () => GameStart.Begin(StartMode.Chapter, n),
                            Loc.Get("win.menu"), GameStart.BackToMenu);
                if (last && !demo) AddText(Loc.Get("win.final"), false);
                break;
            }

            case Kind.Lost:
            {
                UiKit.SetText(title, Loc.Get("win.lost_title"));
                UiKit.SetText(sub, Loc.Get(current.Key));
                AddText(Loc.Get("win.tip." + current.Key), false);
                if (current.Report != null) AddLines(current.Report);
                int chapter = s != null ? s.Chapter : 1;
                Buttons(Loc.Get("win.retry"), () => GameStart.Begin(demo ? StartMode.Demo : StartMode.Chapter, chapter),
                        Loc.Get("win.menu"), GameStart.BackToMenu, null, null);
                break;
            }
        }

        // goals of the chapter: how close the player is
        var chapters = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ChapterSystem>() : null;
        if (chapters != null && s != null)
        {
            var list = chapters.GetProgress(s);
            var parts = new List<string>();
            foreach (var g in list) parts.Add(HudController.GoalText(g));
            UiKit.SetText(goals, Loc.Get("win.goals") + "\n" + string.Join("\n", parts));
        }
        else UiKit.SetText(goals, "");
    }

    void Buttons(string t1, Action f1, string t2, Action f2, string t3, Action f3)
    {
        SetButton(b1, t1, f1, ref a1);
        SetButton(b2, t2, f2, ref a2);
        SetButton(b3, t3, f3, ref a3);
    }

    static void SetButton(Button b, string text, Action action, ref Action slot)
    {
        slot = action;
        if (b == null) return;
        UiKit.Show(b, action != null);
        if (action != null) b.text = text;
    }

    void AddLines(DayReport report)
    {
        if (report.Lines.Count == 0)
        {
            AddText(Loc.Get("win.quiet_day"), false);
            return;
        }
        // alarms first, then the rest in the order the systems wrote them
        foreach (var line in report.Lines) if (line.Important) AddLine(line);
        foreach (var line in report.Lines) if (!line.Important) AddLine(line);
    }

    void AddLine(ReportLine line)
    {
        var row = AddText(LineText(line), line.Important);
        if (!Units.TryGetValue(line.Key, out var unitKey) || Mathf.Abs(line.Delta) < 0.05f) return;
        string unit = line.Key == "report.dose_day" ? "%" : " " + Loc.Get(unitKey);
        bool good = line.Delta > 0f && line.Key != "report.dose_day";
        var delta = UiKit.Label(row, "win-line__delta " + (good ? "win-line__delta--up" : "win-line__delta--down"),
                                (line.Delta > 0f ? "+" : "−") + UiKit.Num(Mathf.Abs(line.Delta)) + unit);
        delta.pickingMode = PickingMode.Ignore;
    }

    VisualElement AddText(string text, bool bad)
    {
        var row = UiKit.Box(lines, "win-line" + (bad ? " win-line--bad" : ""));
        UiKit.Label(row, "win-line__text", text);
        return row;
    }

    /// <summary>Report line → sentence. {0} is the word (a name, a module) or the number; module names are translated.</summary>
    public static string LineText(ReportLine line)
    {
        string arg = line.Arg ?? "";
        if (arg.Length > 0 && !char.IsDigit(arg[0]) && Enum.TryParse(arg, out ModuleType module)) arg = Loc.Get("mod." + module);
        string number = UiKit.Num(Mathf.Abs(line.Delta));
        return Loc.Get(line.Key, arg.Length > 0 ? arg : number, number);
    }
}
