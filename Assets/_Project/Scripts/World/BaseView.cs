// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Показує базу: для кожного модуля в GameState.Modules створює ModuleView у його квадратику (BaseGrid).
/// Модуль зник зі стану — прибирає його зі світу.
/// Став на об'єкт World (поруч з ModuleLook). У вікні Scene малює сітку бази.
/// </summary>
public class BaseView : MonoBehaviour
{
    readonly Dictionary<int, ModuleView> views = new Dictionary<int, ModuleView>(); // номер квадратика → модуль у світі
    readonly List<int> toRemove = new List<int>();
    readonly Dictionary<int, TunnelView> tunnels = new Dictionary<int, TunnelView>(); // лівий модуль → тунель (роль 04)

    static BaseView instance;

    void Awake() => instance = this;

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;

        // Нові модулі → створити
        foreach (var m in game.State.Modules)
        {
            if (!BaseGrid.IsValid(m.SlotId) || views.ContainsKey(m.SlotId)) continue;
            var go = new GameObject("Module @" + m.SlotId);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<ModuleView>();
            view.Init(m.SlotId);
            views[m.SlotId] = view;
        }

        // Модулі, яких уже немає в стані → прибрати
        toRemove.Clear();
        foreach (var pair in views)
            if (game.State.FindModule(pair.Key) == null) toRemove.Add(pair.Key);
        foreach (int id in toRemove)
        {
            Destroy(views[id].gameObject);
            views.Remove(id);
        }

        // Тунелі між кімнатами: нові → створити, зниклі → прибрати
        foreach (var t in game.State.Tunnels)
        {
            if (tunnels.ContainsKey(t.SlotA)) continue;
            var go = new GameObject("Tunnel");
            go.transform.SetParent(transform, false);
            var tv = go.AddComponent<TunnelView>();
            tv.Init(t.SlotA);
            tunnels[t.SlotA] = tv;
        }
        toRemove.Clear();
        foreach (var pair in tunnels)
            if (game.State.FindTunnel(pair.Key) == null) toRemove.Add(pair.Key);
        foreach (int id in toRemove)
        {
            Destroy(tunnels[id].gameObject);
            tunnels.Remove(id);
        }
    }

    /// <summary>Модуль у світі за номером квадратика або null.</summary>
    public static ModuleView Find(int slotId)
    {
        if (instance == null || !instance.views.TryGetValue(slotId, out var v)) return null;
        return v;
    }

    /// <summary>Усі модулі, що зараз є у світі.</summary>
    public static IEnumerable<ModuleView> All => instance != null ? instance.views.Values : (IEnumerable<ModuleView>)new ModuleView[0];

    /// <summary>Перший готовий модуль-кімната такого типу або null.</summary>
    public static ModuleView FindRoom(ModuleType type)
    {
        foreach (var v in All) if (v.IsEnterableRoom && v.Type == type) return v;
        return null;
    }

    /// <summary>Шлях по землі в обхід модулів (для астронавтів і роботів).</summary>
    public static List<Vector3> PathTo(Vector3 from, Vector3 to, float margin = 0.15f)
    {
        var blockers = new List<Rect>();
        foreach (var v in All) v.AddBlockers(blockers, margin);
        if (instance != null)   // тунелі теж заважають ходити надворі
            foreach (int a in instance.tunnels.Keys)
                if (TunnelView.Footprint(a, out var tr))
                    blockers.Add(Rect.MinMaxRect(tr.xMin - margin, tr.yMin - margin, tr.xMax + margin, tr.yMax + margin));
        float half = Mathf.Max(BaseGrid.Width, BaseGrid.Depth) * BaseGrid.CellSize / 2f + 8f; // база + запас
        var area = new Rect(-half, -half, 2 * half, 2 * half);
        var path2 = BasePathfinder.FindPath(new Vector2(from.x, from.z), new Vector2(to.x, to.z), blockers, area);
        var path = new List<Vector3>(path2.Count);
        foreach (var p in path2) path.Add(new Vector3(p.x, 0f, p.y));
        return path;
    }

    /// <summary>
    /// У якому збудованому модулі стоїть точка; -1 = надворі (або в проході між модулями).
    /// У готовому тунелі — номер його лівого модуля (людина всередині, не надворі).
    /// Для Astronaut.InsideSlot.
    /// </summary>
    public static int SlotAt(Vector3 worldPos)
    {
        if (instance == null) return -1;
        int tunnel = TunnelAt(worldPos);
        if (tunnel >= 0) return tunnel;   // у готовому тунелі — це «всередині» (лівий модуль тунелю)
        int id = BaseGrid.FromWorld(worldPos);
        if (id < 0 || !instance.views.TryGetValue(id, out var view)) return -1;

        var m = GameBootstrap.Instance != null ? GameBootstrap.Instance.State.FindModule(id) : null;
        return m != null && m.IsBuilt && view.Contains(worldPos) ? id : -1;
    }

    /// <summary>Якщо точка в готовому тунелі — номер його лівого модуля, інакше -1.</summary>
    static int TunnelAt(Vector3 worldPos)
    {
        var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        if (state == null) return -1;
        foreach (int a in instance.tunnels.Keys)
        {
            var t = state.FindTunnel(a);
            if (t == null || !t.IsBuilt || !TunnelView.Footprint(a, out var r)) continue;
            if (r.Contains(new Vector2(worldPos.x, worldPos.z))) return a;
        }
        return -1;
    }

    // Сітка бази у вікні Scene (у грі не видно).
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
        for (int id = 0; id < BaseGrid.Count; id++)
        {
            Vector3 c = BaseGrid.ToWorld(id) + Vector3.up * 0.05f;
            Gizmos.DrawWireCube(c, new Vector3(BaseGrid.CellSize, 0.1f, BaseGrid.CellSize));
        }
    }
}
