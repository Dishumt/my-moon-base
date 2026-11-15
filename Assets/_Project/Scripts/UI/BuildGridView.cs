// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine.UIElements;

/// <summary>
/// Building on the BUILD tab of the tablet (role 05). The MAP tab only shows the world.
/// Draws the base grid BaseGrid.Width x BaseGrid.Depth as buttons (row 0 is at the BOTTOM, like in the world:
/// closer to the camera). One square = one module, its number = BaseGrid.ToSlot(column, row).
///  - empty square  -> click -> pick a module type -> ConstructionSystem.Order(state, type, slot);
///  - building / built square -> click -> shows what is there;
///  - built square without regolith -> "COVER WITH REGOLITH" -> ConstructionSystem.ShieldWithRegolith (less radiation inside);
///  - built room -> "TUNNEL" to the nearest room in the same row (also across several empty squares) ->
///    ConstructionSystem.OrderTunnel (role 04): the crew walks between them inside, without suits and without radiation.
///    "=" on a square = tunnel (to the right of a room, or an empty square under a tunnel).
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
    readonly BuildCostTable costs = BuildCostTable.Load(); // materials and robot hours from modules.json (role 03)
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
            var tunnel = state.FindTunnel(slot);   // тунель праворуч від цього квадратика
            if (m != null && tunnel != null) cell.text += tunnel.IsBuilt ? " =" : " ~";
            var under = m == null ? state.FindTunnelCovering(slot) : null;   // порожній квадратик під тунелем
            if (under != null) cell.text = under.IsBuilt ? "=" : "~";
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
        var covering = m == null ? state.FindTunnelCovering(selectedSlot) : null;
        if (covering != null)   // тут пролягає тунель — будувати не можна
        {
            SetText(slotState, Loc.Get(covering.IsBuilt ? "map.tunnel_here" : "map.tunnel_here_building"));
            SetText(hint, Loc.Get("error.place_tunnel"));
            return;
        }
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
                string cost = costs.TryGet(t, out var c)
                    ? "  ·  " + Loc.Get("map.cost", UiKit.Num(c.Materials), UiKit.Num(c.BuildHours)) : "";
                button.text = Loc.Get("mod." + t) + cost + "\n<size=12>" + Loc.Get("mod." + t + ".desc") + "</size>";
                options.Add(button);
            }
        }
        else if (m.IsBuilt)
        {
            SetText(slotState, Loc.Get("map.built") + "  ·  " + Loc.Get(m.RegolithShield ? "map.shield_yes" : "map.shield_no"));
            SetText(hint, Loc.Get("mod." + m.Type) + " — " + Loc.Get("mod." + m.Type + ".desc") + TunnelInfo(state));
            if (options == null) return;
            if (ModuleKinds.IsRoom(m.Type))   // тунель — тільки від кімнати (у сонячну панель не зайдеш)
            {
                AddTunnelButton(state, +1, "map.tunnel_right");   // до найближчої кімнати праворуч у ряду
                AddTunnelButton(state, -1, "map.tunnel_left");    // ...і ліворуч
            }
            if (m.RegolithShield) return;
            var shield = new Button(Shield);
            shield.AddToClassList("opt");
            shield.text = Loc.Get("map.shield_btn") + "\n<size=12>"
                        + Loc.Get("map.shield_cost", UiKit.Num(costs.Regolith.BuildHours)) + " " + Loc.Get("map.regolith") + "</size>";
            options.Add(shield);
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

        string err = construction.Order(GameBootstrap.Instance.State, type, selectedSlot);
        if (err != null)
        {
            SetMessage(Loc.Get(err), true);
            return;
        }
        SetMessage(Loc.Get("map.ordered"), false);
        GameEvents.RaiseStateChanged(); // world, HUD and this grid redraw
    }

    void Shield()
    {
        var construction = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ConstructionSystem>() : null;
        if (construction == null) return;

        string err = construction.ShieldWithRegolith(GameBootstrap.Instance.State, selectedSlot);
        SetMessage(Loc.Get(err ?? "map.shield_ordered"), err != null);
        GameEvents.RaiseStateChanged();
    }

    // ---------- Тунелі (роль 04) ----------

    /// <summary>
    /// Кнопка «тунель до найближчої кімнати» праворуч (dir = 1) чи ліворуч (-1) у тому ж ряду — можна крізь кілька
    /// порожніх квадратиків, ціна росте з довжиною. Якщо поки не можна — кнопка сіра і пише чому.
    /// Тунель уже є або далі край сітки — кнопки немає.
    /// </summary>
    void AddTunnelButton(GameState state, int dir, string key)
    {
        int other = BaseGrid.NextModuleInRow(state, selectedSlot, dir);
        if (other < 0 && (dir > 0 ? BaseGrid.Column(selectedSlot) >= BaseGrid.Width - 1 : BaseGrid.Column(selectedSlot) == 0)) return;
        int slotA = dir > 0 ? selectedSlot : other;
        int length = other < 0 ? 1 : System.Math.Abs(other - selectedSlot);
        string err = other < 0 ? "error.tunnel_no_neighbor" : BaseGrid.CanConnect(state, slotA, length);
        if (err == "error.tunnel_place" || err == "error.tunnel_exists") return;

        var button = new Button(() => OrderTunnel(slotA, length));
        button.AddToClassList("opt");
        string target = other >= 0 ? " " + Loc.Get("mod." + state.FindModule(other).Type) + " (" + Loc.Get("map.tunnel_cells", length) + ")" : "";
        button.text = Loc.Get(key) + target + "  ·  "
                    + Loc.Get("map.cost", UiKit.Num(costs.Tunnel.Materials * length), UiKit.Num(costs.Tunnel.BuildHours * length))
                    + "\n<size=12>" + Loc.Get(err ?? "map.tunnel_desc") + "</size>";
        button.SetEnabled(err == null);
        options.Add(button);
    }

    /// <summary>Рядок про тунелі вибраного модуля: праворуч/ліворуч — готовий чи будується.</summary>
    string TunnelInfo(GameState state)
    {
        string text = "";
        foreach (var (t, side) in new[] { (state.FindTunnel(selectedSlot), "map.side_right"), (state.FindTunnelEndingAt(selectedSlot), "map.side_left") })
        {
            if (t == null) continue;
            text += "\n" + (t.IsBuilt ? Loc.Get("map.tunnel_built", Loc.Get(side))
                                      : Loc.Get("map.tunnel_building", Loc.Get(side), t.BuildHoursLeft.ToString("0")));
        }
        return text;
    }

    void OrderTunnel(int slotA, int length)
    {
        var construction = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ConstructionSystem>() : null;
        if (construction == null) return;
        string err = construction.OrderTunnel(GameBootstrap.Instance.State, slotA, length);
        SetMessage(Loc.Get(err ?? "map.tunnel_ordered"), err != null);
        GameEvents.RaiseStateChanged();
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
