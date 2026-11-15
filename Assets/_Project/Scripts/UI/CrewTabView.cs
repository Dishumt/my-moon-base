// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine.UIElements;

/// <summary>
/// CREW tab: a card for every astronaut — fullness, health, radiation dose, where they are and what they do.
/// The engineer is the player (walks with W A S D), the others get jobs: CrewSystem.Assign (role 04).
/// "Everyone into the shelter" calls CrewSystem.RecallAllInside (for a solar storm).
/// Not a MonoBehaviour: TabletController creates it with the "pane-crew" element.
/// </summary>
public class CrewTabView
{
    // jobs that can be given, and which chapter system must be open for each
    static readonly (CrewTask task, string system)[] Jobs =
    {
        (CrewTask.Rest, ""),
        (CrewTask.MineIce, "IceMining"),
        (CrewTask.CollectSamples, "Exploration"),
        (CrewTask.Greenhouse, "Greenhouse"),
        (CrewTask.Lab, "Lab"),
        (CrewTask.Heal, "Health"),
    };

    readonly VisualElement root, list;
    readonly Label head, message, stormNote;
    readonly Button allInside;
    readonly Dictionary<string, VisualElement> cards = new Dictionary<string, VisualElement>();
    string lastRoster = "";

    public CrewTabView(VisualElement pane)
    {
        UiKit.Show(pane.Q("pane-crew-stub"), false);
        pane.style.alignItems = Align.Stretch;
        pane.style.justifyContent = Justify.FlexStart;
        root = UiKit.Box(pane, "gt-pane");
        root.style.flexDirection = FlexDirection.Column;

        var top = UiKit.Box(root, "row-between");
        head = UiKit.Label(top, "gt-head");
        allInside = UiKit.Button(top, "cr-all", RecallAll);
        stormNote = UiKit.Label(root, "gt-note gt-note--fact");
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        root.Add(scroll);
        list = scroll.contentContainer;
        message = UiKit.Label(root, "gt-msg");
    }

    // ---------- player actions ----------

    void Assign(Astronaut a, CrewTask task)
    {
        var s = UiKit.State;
        var crew = UiKit.Sys<CrewSystem>();
        if (s == null || crew == null) return;
        string err = crew.Assign(s, a, task);
        if (err != null) SetMessage(Loc.Get(err, a.Name), true);
        else SetMessage(Loc.Get("crew.assigned", a.Name), false);
        GameEvents.RaiseStateChanged();
    }

    void RecallAll()
    {
        var s = UiKit.State;
        var crew = UiKit.Sys<CrewSystem>();
        if (s == null || crew == null) return;
        int n = crew.RecallAllInside(s);
        SetMessage(Loc.Get("crew.recalled", n), false);
    }

    void SetMessage(string text, bool isError)
    {
        UiKit.SetText(message, text);
        message.EnableInClassList("gt-msg--err", isError);
    }

    // ---------- redraw ----------

    public void Refresh()
    {
        var s = UiKit.State;
        if (s == null) return;
        var crew = UiKit.Sys<CrewSystem>();
        var chapters = UiKit.Sys<ChapterSystem>();
        var radiation = UiKit.Sys<RadiationSystem>();

        UiKit.SetText(head, Loc.Get("crew.member") + " · " + s.Crew.Count);
        UiKit.SetText(allInside, Loc.Get("crew.all_inside"));
        bool storm = s.Radiation.Phase != StormPhase.None;
        UiKit.Show(stormNote, storm);
        if (storm) UiKit.SetText(stormNote, Loc.Get("crew.storm_note"));

        // rebuild the cards only when the people change (somebody flew home or arrived)
        string roster = "";
        foreach (var a in s.Crew) roster += a.Name + "|";
        if (roster != lastRoster)
        {
            lastRoster = roster;
            list.Clear();
            cards.Clear();
            foreach (var a in s.Crew) cards[a.Name] = UiKit.Box(list, "cr-card");
        }

        foreach (var a in s.Crew)
        {
            if (!cards.TryGetValue(a.Name, out var card)) continue;
            FillCard(card, s, a, crew, chapters, radiation);
        }
    }

    void FillCard(VisualElement card, GameState s, Astronaut a, CrewSystem crew, ChapterSystem chapters, RadiationSystem radiation)
    {
        card.Clear();
        bool player = a.Role == CrewRole.Engineer;
        var zone = radiation != null ? radiation.Zone(a) : DoseZone.Green;
        bool trouble = a.Health != HealthStatus.Ok || zone != DoseZone.Green || a.Satiety < 30f;
        card.EnableInClassList("cr-card--warn", trouble);

        var headRow = UiKit.Box(card, "cr-head");
        UiKit.Label(headRow, "cr-name", a.Name);
        UiKit.Label(headRow, "cr-role", Loc.Get("role." + a.Role.ToString().ToLowerInvariant()).ToUpperInvariant());
        if (player) UiKit.Label(headRow, "cr-chip cr-chip--acc", Loc.Get("chip.you"));
        UiKit.Label(headRow, "cr-chip " + (a.Health == HealthStatus.Ok ? "cr-chip--ok" : a.Health == HealthStatus.Sick ? "cr-chip--warn" : "cr-chip--bad"),
                    Loc.Get("health." + a.Health));
        UiKit.Label(headRow, "cr-chip " + (zone == DoseZone.Green ? "cr-chip--ok" : zone == DoseZone.Yellow ? "cr-chip--warn" : "cr-chip--bad"),
                    Loc.Get("dose.zone." + zone.ToString().ToLowerInvariant()));
        if (a.Satiety < 30f) UiKit.Label(headRow, "cr-chip cr-chip--warn", Loc.Get("chip.hungry"));
        UiKit.Label(headRow, "cr-chip", a.IsOutside ? Loc.Get("crew.where_out") : Loc.Get("crew.where_in"));

        string stats = Loc.Get("crew.satiety") + " " + UiKit.Num(a.Satiety) + "%  ·  "
                     + Loc.Get("crew.dose") + " " + UiKit.Num(a.Dose) + "%";
        if (!player) stats += "  ·  " + Loc.Get("crew.task") + ": " + TaskName(a.Task);
        UiKit.Label(card, "cr-stats", stats);

        if (player)
        {
            UiKit.Label(card, "cr-stats", Loc.Get("crew.you_note"));
            return;
        }

        var tasks = UiKit.Box(card, "cr-tasks");
        foreach (var (task, system) in Jobs)
        {
            if (!JobOpen(s, task, system, chapters)) continue;
            var t = task; // own copy for the lambda
            var button = UiKit.Button(tasks, "cr-task", () => Assign(a, t), TaskName(task));
            bool best = crew != null && crew.IsSpecialist(a, task);
            if (best) button.text += " ★";
            button.EnableInClassList("cr-task--best", best);
            button.EnableInClassList("cr-task--on", a.Task == task);
            if (crew != null && crew.IsOutsideTask(task)) button.tooltip = Loc.Get("crew.outside_job");
        }
    }

    static bool JobOpen(GameState s, CrewTask task, string system, ChapterSystem chapters)
    {
        if (chapters == null || system.Length == 0) return true;
        if (task == CrewTask.Lab) return chapters.IsModuleUnlocked(ModuleType.Lab);
        return chapters.IsSystemUnlocked(system);
    }

    static string TaskName(CrewTask task)
    {
        return Loc.Get("task." + task);
    }
}
