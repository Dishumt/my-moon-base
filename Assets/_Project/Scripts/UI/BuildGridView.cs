// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine.UIElements;

/// <summary>
/// Building on the BUILD tab of the tablet (role 05). The MAP tab only shows the world.
/// Draws the base grid BaseGrid.Width x BaseGrid.Depth as buttons (row 0 is at the BOTTOM, like in the world:
/// closer to the camera). One square = one module, its number = BaseGrid.ToSlot(column, row).
///  - empty square  -> click -> pick a module type -> ConstructionSystem.Order(type, slot);
///  - building / built square -> click -> shows what is there.
/// Not a MonoBehaviour: TabletController creates it and gives it the "pane-build" element.
/// </summary>
public class BuildGridView
{
    // Modules the player can order. Relay and Reactor come in chapters 4-5 (no texts yet).
    static readonly ModuleType[] Buildable =
    {
        ModuleType.Habitat, ModuleType.Solar, ModuleType.Battery, ModuleType.Greenhouse,
        ModuleType.Shelter, ModuleType.Medbay, ModuleType.Antenna, ModuleType.Lab,
    };

    readonly VisualElement grid;     // rows of squares
    readonly Label slotTitle;        // "SLOT 7"
    readonly Label slotState;        // "EMPTY" / "BUILDING" / "BUILT"
    readonly Label hint;             // "CHOOSE A MODULE" or what is built
    readonly VisualElement options;  // module type buttons
    readonly Label message;          // error (red) or "Ordered!" (green)

    readonly Button[] cells = new Button[BaseGrid.Count];
    int selectedSlot = -1;

    public BuildGridView(VisualElement pane)
    {
        grid = pane.Q("build-grid");
        slotTitle = pane.Q<Label>("build-slot-title");
        slotState = pane.Q<Label>("build-slot-state");
        hint = pane.Q<Label>("build-hint");
        options = pane.Q("build-options");
        message = pane.Q<Label>("build-msg");
        if (grid == null) return;

        // rows from the top (Depth-1) down to 0, so row 0 is drawn at the bottom
        grid.Clear();
        for (int row = BaseGrid.Depth - 1; row >= 0; row--)
        {
            var line = new VisualElement();
            line.AddToClassList("grid-row");
            grid.Add(line);
            for (int col = 0; col < BaseGrid.Width; col++)
            {
                int slot = BaseGrid.ToSlot(col, row);
                var cell = new Button(() => Select(slot));
                cell.AddToClassList("grid-cell");
                line.Add(cell);
                cells[slot] = cell;
            }
        }
    }

    /// <summary>Redraw from GameState. TabletController calls it on StateChanged and language change.</summary>
    public void Refresh()
    {
        var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        if (state == null || grid == null) return;

        for (int slot = 0; slot < cells.Length; slot++)
        {
            var cell = cells[slot];
            if (cell == null) continue;
            var m = state.FindModule(slot);
            cell.EnableInClassList("grid-cell--building", m != null && !m.IsBuilt);
            cell.EnableInClassList("grid-cell--built", m != null && m.IsBuilt);
            cell.EnableInClassList("grid-cell--sel", slot == selectedSlot);
            cell.text = m == null ? "+" : Loc.Get("mod." + m.Type);
        }
        ShowSide(state);
    }

    void Select(int slot)
    {
        selectedSlot = slot;
        SetMessage("", false);
        Refresh();
    }

    // right side: what is in the selected square, and module buttons if it is empty
    void ShowSide(GameState state)
    {
        options?.Clear();
        if (selectedSlot < 0)
        {
            SetText(slotTitle, "");
            SetText(slotState, "");
            SetText(hint, Loc.Get("map.pick_first"));
            return;
        }

        SetText(slotTitle, Loc.Get("map.slot", selectedSlot));
        var m = state.FindModule(selectedSlot);
        if (m == null)
        {
            SetText(slotState, Loc.Get("map.empty"));
            SetText(hint, Loc.Get("map.choose"));
            if (options == null) return;
            foreach (var type in Buildable)
            {
                var t = type; // own copy for the lambda
                var button = new Button(() => Order(t));
                button.AddToClassList("opt");
                button.text = Loc.Get("mod." + t) + "\n<size=12>" + Loc.Get("mod." + t + ".desc") + "</size>";
                options.Add(button);
            }
        }
        else if (m.IsBuilt)
        {
            SetText(slotState, Loc.Get("map.built"));
            SetText(hint, Loc.Get("mod." + m.Type) + " — " + Loc.Get("mod." + m.Type + ".desc"));
        }
        else
        {
            SetText(slotState, Loc.Get("map.building"));
            SetText(hint, Loc.Get("mod." + m.Type) + " — " + Loc.Get("map.hours_left", m.BuildHoursLeft.ToString("0")));
        }
    }

    void Order(ModuleType type)
    {
        var construction = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ConstructionSystem>() : null;
        if (construction == null) return;

        string err = construction.Order(type, selectedSlot);
        if (err != null)
        {
            SetMessage(Loc.Get(err), true);
            return;
        }
        SetMessage(Loc.Get("map.ordered"), false);
        GameEvents.RaiseStateChanged(); // world, HUD and this grid redraw
    }

    void SetMessage(string text, bool isError)
    {
        if (message == null) return;
        message.text = text;
        message.EnableInClassList("msg--err", isError);
        message.EnableInClassList("msg--good", !isError && text.Length > 0);
    }

    static void SetText(Label label, string text)
    {
        if (label != null) label.text = text;
    }
}
