// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Місце на базі, де можна збудувати модуль. Розстав 10–12 таких точок у сцені.
/// SlotId — номер, за яким логіка (GameState.Modules[i].SlotId) знаходить це місце.
/// У кожного слота має бути СВІЙ номер: 0, 1, 2, ...
/// Разом з ним автоматично додається ModuleView, який малює модуль.
/// </summary>
[RequireComponent(typeof(ModuleView))]
public class ModuleSlot : MonoBehaviour
{
    [Tooltip("Номер слота. Не повторюй номери!")]
    public int slotId;

    [Tooltip("Розмір модуля в метрах: X — ширина, Y — глибина")]
    public Vector2 size = new Vector2(6f, 6f);

    static readonly List<ModuleSlot> all = new List<ModuleSlot>();

    /// <summary>Усі слоти в сцені.</summary>
    public static IReadOnlyList<ModuleSlot> All => all;

    /// <summary>Знайти слот за номером або null.</summary>
    public static ModuleSlot Find(int id)
    {
        foreach (var s in all) if (s.slotId == id) return s;
        return null;
    }

    void OnEnable()
    {
        if (Find(slotId) != null) Debug.LogWarning("ModuleSlot: номер " + slotId + " повторюється! Об'єкт " + name);
        all.Add(this);
    }

    void OnDisable() => all.Remove(this);

    /// <summary>Чи точка (у світі) стоїть усередині прямокутника цього слота.</summary>
    public bool Contains(Vector3 worldPos)
    {
        Vector3 p = transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(p.x) < size.x * 0.5f && Mathf.Abs(p.z) < size.y * 0.5f;
    }

    /// <summary>У якому слоті стоїть точка; -1 = надворі. Для Astronaut.InsideSlot.</summary>
    public static int SlotAt(Vector3 worldPos, bool onlyBuilt = true)
    {
        var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        foreach (var s in all)
        {
            if (!s.Contains(worldPos)) continue;
            if (!onlyBuilt) return s.slotId;
            var m = state != null ? state.FindModule(s.slotId) : null;
            if (m != null && m.IsBuilt) return s.slotId;
        }
        return -1;
    }

    // Малює рамку слота у вікні Scene, щоб було видно, куди ставити.
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.8f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(0f, 0.05f, 0f), new Vector3(size.x, 0.1f, size.y));
    }
}
