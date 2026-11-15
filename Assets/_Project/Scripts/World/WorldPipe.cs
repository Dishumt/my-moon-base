// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Труба або кабель, що лежить на землі: циліндри між точками і кульки-сфери на стиках.
/// Коли щось тече (вода, кисень, лід, енергія), по ній біжать світлі кульки від початку до кінця.
/// Користуються IcePlantView (труби) і PowerCablesView (кабелі енергії).
/// </summary>
public class WorldPipe
{
    public GameObject Root;
    readonly List<Vector3> pts;
    readonly float[] at;   // відстань від початку до кожної точки
    readonly Transform[] blobs;
    readonly float length;
    float phase;
    Color shownBlobColor;

    public WorldPipe(string name, List<Vector3> points, float radius, Color color, Color blobColor, float blobSpacing = 4f)
    {
        pts = points;
        Root = new GameObject(name);
        var mat = ModuleLook.Instance.wallMaterial;
        at = new float[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            if (i > 0) at[i] = at[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
            ModuleParts.Sphere(Root.transform, "Joint", mat, pts[i], Vector3.one * radius * 2.3f, collider: false, tint: color);
            if (i == 0) continue;
            Vector3 a = pts[i - 1], b = pts[i];
            float len = Vector3.Distance(a, b);
            if (len < 0.01f) continue;
            var seg = ModuleParts.Cylinder(Root.transform, "Segment", mat, (a + b) * 0.5f, new Vector3(radius * 2f, len * 0.5f, radius * 2f),
                                           collider: false, tint: color);
            seg.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }
        length = pts.Count > 0 ? at[pts.Count - 1] : 0f;
        int n = Mathf.Clamp(Mathf.RoundToInt(length / Mathf.Max(0.5f, blobSpacing)), 1, 40);
        blobs = new Transform[n];
        shownBlobColor = blobColor;
        for (int i = 0; i < n; i++)
            blobs[i] = ModuleParts.Sphere(Root.transform, "Flow", mat, pts.Count > 0 ? pts[0] : Vector3.zero, Vector3.one * radius * 2.8f,
                                          collider: false, tint: blobColor).transform;
    }

    /// <summary>Кульки біжать (flowing) або сховані. speed — метрів за секунду.</summary>
    public void Animate(bool flowing, float speed)
    {
        if (Root == null || blobs.Length == 0 || length < 0.01f) return;
        if (flowing) phase += Time.deltaTime * speed;
        for (int i = 0; i < blobs.Length; i++)
        {
            if (blobs[i] == null) continue;
            if (blobs[i].gameObject.activeSelf != flowing) blobs[i].gameObject.SetActive(flowing);
            if (!flowing) continue;
            float d = Mathf.Repeat(phase + i * (length / blobs.Length), length);
            blobs[i].position = PointAt(d) + Vector3.up * 0.05f;
        }
    }

    /// <summary>Перефарбувати кульки (наприклад, червоні — аварія енергії).</summary>
    public void SetFlowColor(Color c)
    {
        if (c == shownBlobColor) return;
        shownBlobColor = c;
        foreach (var b in blobs)
            if (b != null) ModuleParts.SetColor(b.GetComponent<Renderer>(), c);
    }

    Vector3 PointAt(float d)
    {
        for (int i = 1; i < pts.Count; i++)
        {
            if (d > at[i]) continue;
            float seg = at[i] - at[i - 1];
            return seg < 0.001f ? pts[i] : Vector3.Lerp(pts[i - 1], pts[i], (d - at[i - 1]) / seg);
        }
        return pts[pts.Count - 1];
    }

    public void Destroy()
    {
        if (Root != null) Object.Destroy(Root);
        Root = null;
    }

    // ---------- точки на землі ----------

    /// <summary>Пряма від a до b по рельєфу.</summary>
    public static List<Vector3> Straight(Vector3 a, Vector3 b, float lift = 0.2f) => Densify(new List<Vector3> { a, b }, lift);

    /// <summary>Точки кожні 2 м, кожна — на висоті рельєфу + lift (труба лежить на землі).</summary>
    public static List<Vector3> Densify(List<Vector3> pts, float lift = 0.2f)
    {
        var result = new List<Vector3>();
        if (pts == null || pts.Count == 0) return result;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            a.y = b.y = 0f;
            int n = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f));
            for (int k = 0; k < n; k++) result.Add(OnGround(Vector3.Lerp(a, b, k / (float)n), lift));
        }
        result.Add(OnGround(pts[pts.Count - 1], lift));
        return result;
    }

    static Vector3 OnGround(Vector3 p, float lift)
    {
        p.y = MoonTerrain.HeightAt(p) + lift;
        return p;
    }

    /// <summary>Довжина ламаної, метри.</summary>
    public static float PathLength(List<Vector3> pts)
    {
        float len = 0f;
        for (int i = 0; i < pts.Count - 1; i++) len += Vector3.Distance(pts[i], pts[i + 1]);
        return len;
    }
}
