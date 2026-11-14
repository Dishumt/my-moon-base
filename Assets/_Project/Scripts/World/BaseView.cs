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
    }

    /// <summary>
    /// У якому збудованому модулі стоїть точка; -1 = надворі (або в проході між модулями).
    /// Для Astronaut.InsideSlot.
    /// </summary>
    public static int SlotAt(Vector3 worldPos)
    {
        if (instance == null) return -1;
        int id = BaseGrid.FromWorld(worldPos);
        if (id < 0 || !instance.views.TryGetValue(id, out var view)) return -1;

        var m = GameBootstrap.Instance != null ? GameBootstrap.Instance.State.FindModule(id) : null;
        return m != null && m.IsBuilt && view.Contains(worldPos) ? id : -1;
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
