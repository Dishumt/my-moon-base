// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Точка на поверхні Місяця: кратер з льодом, місце для зразків, посадковий майданчик, факт.
/// PoiId має збігатися з "id" у chapter_NN.json (наприклад "ice_crater_1").
/// Коли інженер підходить ближче, ніж Discover Radius, світ ставить PoiState.Discovered = true.
/// </summary>
public class PoiMarker : MonoBehaviour
{
    [Tooltip("Такий самий id, як у chapter_01.json → pois")]
    public string poiId;

    [Tooltip("Тип точки — тільки для кольору рамки в Scene. Справжній тип береться з JSON.")]
    public PoiType type = PoiType.Sample;

    [Tooltip("З якої відстані (метри) інженер «помічає» точку")]
    public float discoverRadius = 6f;

    [Tooltip("Що ввімкнути, коли точку знайдено (наприклад, іконка або сяйво). Можна лишити порожнім.")]
    [SerializeField] GameObject showWhenDiscovered;

    static readonly List<PoiMarker> all = new List<PoiMarker>();
    static Transform engineer;

    /// <summary>Усі точки в сцені.</summary>
    public static IReadOnlyList<PoiMarker> All => all;

    /// <summary>Знайти точку за id або null. Знадобиться астронавтам (TaskTarget).</summary>
    public static PoiMarker Find(string id)
    {
        foreach (var p in all) if (p.poiId == id) return p;
        return null;
    }

    PoiState poi;
    bool warned;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;

        if (poi == null)
        {
            poi = game.State.FindPoi(poiId);
            if (poi == null)
            {
                if (!warned) Debug.LogWarning("PoiMarker: у розділі немає точки з id '" + poiId + "' (об'єкт " + name + ")");
                warned = true;
                return;
            }
        }

        if (showWhenDiscovered != null && showWhenDiscovered.activeSelf != poi.Discovered)
            showWhenDiscovered.SetActive(poi.Discovered);

        if (poi.Discovered) return;

        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            engineer = go.transform;
        }

        Vector3 d = engineer.position - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude <= discoverRadius * discoverRadius)
        {
            poi.Discovered = true;           // Світ пише в GameState — так домовлено в CLAUDE.md
            GameEvents.RaiseStateChanged();  // інтерфейс оновиться (наприклад, карта в планшеті)
        }
    }

    // Коло радіуса у вікні Scene: кольором видно тип точки.
    void OnDrawGizmos()
    {
        switch (type)
        {
            case PoiType.Ice: Gizmos.color = Color.cyan; break;
            case PoiType.Sample: Gizmos.color = new Color(1f, 0.6f, 0.2f); break;
            case PoiType.Fact: Gizmos.color = Color.yellow; break;
            default: Gizmos.color = Color.white; break;
        }
        const int steps = 32;
        Vector3 prev = transform.position + new Vector3(discoverRadius, 0.1f, 0f);
        for (int i = 1; i <= steps; i++)
        {
            float a = i * Mathf.PI * 2f / steps;
            Vector3 next = transform.position + new Vector3(Mathf.Cos(a) * discoverRadius, 0.1f, Mathf.Sin(a) * discoverRadius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
