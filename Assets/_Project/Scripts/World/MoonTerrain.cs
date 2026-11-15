// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рельєф Місяця навколо бази (замість рівної площини Ground):
///  - база в центрі рівна;
///  - пологі хвилі й улоговини по всій поверхні та кілька великих пагорбів;
///  - кратери з валом: великі темні кратери з льодом на дні (точки ice_*) і менші кругом;
///  - кільце гірського хребта по краю карти — схили надто круті, вийти не можна.
/// Висоту рахує формула GroundHeight(x, z), тож світ знає висоту будь-де без фізики:
/// інженер, астронавти, роботи, тіні й точки поверхні ставлять себе на MoonTerrain.HeightAt(x, z).
/// Сітку землі будуємо кодом під час запуску (64 шматки по 60 м — малюються тільки ті, що в кадрі).
/// Кратери, пагорби й рівні місця заповнює меню Moon → Рельєф: кратери, пагорби, хребет.
/// Немає матеріалу — рельєфу немає зовсім (земля лишається рівною, як раніше).
/// </summary>
public class MoonTerrain : MonoBehaviour
{
    [Serializable]
    public class Crater
    {
        public Vector2 pos;          // центр (x, z), метри
        public float radius = 6f;    // від центру до вершини валу, м
        public float depth = 2f;     // на скільки дно нижче навколишньої землі, м
        public float rim = 0.5f;     // висота валу, м
        public bool ice;             // темний «вічно затінений» кратер з льодом на дні
    }

    [Serializable]
    public class Hill
    {
        public Vector2 pos;
        public float radius = 25f;
        public float height = 6f;
    }

    [Serializable]
    public class FlatSpot // рівне місце: посадковий майданчик, точки зразків і фактів
    {
        public Vector2 pos;
        public float radius = 5f;
    }

    public static MoonTerrain Instance { get; private set; }

    [Header("Вигляд (матеріал Settings/MoonTerrain.mat з шейдером Moon/Terrain)")]
    public Material material;

    [Header("Сітка")]
    public float halfSize = 240f;   // земля від -240 до 240 м по X і Z
    public float chunkSize = 60f;   // розмір одного шматка
    public float step = 1.5f;       // відстань між вершинами, м

    [Header("Рівна база в центрі")]
    [Tooltip("До цієї відстані від центру (по X або по Z) земля зовсім рівна — там модулі")]
    public float baseFlat = 30f;
    [Tooltip("На скільки метрів далі рельєф плавно наростає")]
    public float baseBlend = 14f;

    [Header("Хвилі й улоговини")]
    public float waveHeight = 1.4f;
    public float waveScale = 40f;
    public float rippleHeight = 0.45f;
    public float rippleScale = 12f;

    [Header("Хребет по краю карти")]
    public float ridgeRadius = 168f;  // де починається підйом (у середньому)
    public float ridgeWobble = 9f;    // наскільки край «гуляє» туди-сюди
    public float ridgeRise = 28f;     // на скількох метрах хребет набирає висоту
    public float ridgeHeight = 24f;   // середня висота хребта

    [Header("Ходьба")]
    [Tooltip("Крутіше за це (градуси) вгору пішки не піднятися: стінки хребта, великі валуни")]
    public float maxWalkSlope = 38f;

    [Header("Що на поверхні (заповнює меню)")]
    public int seed = 7;
    public List<Crater> craters = new List<Crater>();
    public List<Hill> hills = new List<Hill>();
    public List<FlatSpot> flats = new List<FlatSpot>();

    const float RimOut = 1.7f;    // вал кратера плавно сходить нанівець до 1,7 радіуса
    const float FlatBlend = 6f;   // рівне місце плавно переходить у хвилі на 6 м

    static readonly Color32 White = new Color32(255, 255, 255, 255);
    static readonly Color IceTint = new Color(0.52f, 0.6f, 0.8f);  // дно кратера з льодом: темне й холодне
    static readonly Color CraterTint = new Color(0.86f, 0.85f, 0.88f);

    float[] noiseOffsets;

    // =====================================================================
    // Висота
    // =====================================================================

    /// <summary>Висота землі в точці (x, z), метри. 0 — рівень бази.</summary>
    public static float HeightAt(float x, float z) => Instance != null ? Instance.GroundHeight(x, z) : 0f;

    /// <summary>Висота землі під точкою (y точки не враховується).</summary>
    public static float HeightAt(Vector3 p) => HeightAt(p.x, p.z);

    /// <summary>Напрямок «вгору» від землі в точці (для тіней, що лежать на схилі).</summary>
    public static Vector3 NormalAt(float x, float z)
    {
        if (Instance == null) return Vector3.up;
        const float e = 0.5f;
        float hx = HeightAt(x + e, z) - HeightAt(x - e, z);
        float hz = HeightAt(x, z + e) - HeightAt(x, z - e);
        return new Vector3(-hx, 2f * e, -hz).normalized;
    }

    /// <summary>Поворот плоскої тіні-плями, щоб вона лежала на схилі (пляма — спрайт, повернутий на 90° по X).</summary>
    public static Quaternion FlatOnGround(Vector3 p) =>
        Quaternion.FromToRotation(Vector3.up, NormalAt(p.x, p.z)) * Quaternion.Euler(90f, 0f, 0f);

    /// <summary>Висота землі, рахує формула (у редакторі теж працює — для меню розстановки каміння).</summary>
    public float GroundHeight(float x, float z)
    {
        if (noiseOffsets == null) InitNoise();

        float h = 0f;
        float m = BaseMask(x, z);
        if (m > 0f)
        {
            float w = waveHeight * Signed(x / waveScale + noiseOffsets[0], z / waveScale + noiseOffsets[1])
                    + rippleHeight * Signed(x / rippleScale + noiseOffsets[2], z / rippleScale + noiseOffsets[3]);
            foreach (var f in flats)
            {
                float d = Dist(f.pos, x, z);
                if (d < f.radius + FlatBlend) w *= Smooth01((d - f.radius) / FlatBlend);
            }
            h = w;
            foreach (var hl in hills) h += HillAt(hl, x, z);
            h *= m;
        }
        foreach (var c in craters) h += CraterAt(c, x, z);
        h += RidgeAt(x, z);
        return h;
    }

    /// <summary>
    /// Чи можна пішки пройти з from у бік to (не надто круто вгору). Униз — завжди можна.
    /// Стінки хребта й великих валунів крутіші за maxWalkSlope — туди не піднятися.
    /// </summary>
    public static bool CanWalk(Vector3 from, Vector3 to)
    {
        if (Instance == null) return true;
        float run = new Vector2(to.x - from.x, to.z - from.z).magnitude;
        if (run < 0.0001f) return true;
        float rise = HeightAt(to) - HeightAt(from);
        return rise <= run * Mathf.Tan(Instance.maxWalkSlope * Mathf.Deg2Rad);
    }

    /// <summary>Наскільки далеко від центру можна зайти (трохи за підніжжя хребта). Запобіжник для інженера.</summary>
    public static float MaxRadius => Instance != null ? Instance.ridgeRadius + Instance.ridgeWobble + Instance.ridgeRise * 0.5f : 1e6f;

    float BaseMask(float x, float z)
    {
        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)); // база квадратна
        return Smooth01((d - baseFlat) / baseBlend);
    }

    static float HillAt(Hill hl, float x, float z)
    {
        float dx = x - hl.pos.x, dz = z - hl.pos.y;
        float t2 = (dx * dx + dz * dz) / (hl.radius * hl.radius);
        if (t2 >= 1f) return 0f;
        float k = 1f - t2;
        return hl.height * k * k; // плавний горб без різкого краю
    }

    /// <summary>Профіль кратера: чаша (дно → вал), далі вал плавно сходить на землю.</summary>
    static float CraterAt(Crater c, float x, float z)
    {
        float dx = x - c.pos.x, dz = z - c.pos.y;
        float outer = c.radius * RimOut;
        float d2 = dx * dx + dz * dz;
        if (d2 >= outer * outer) return 0f;
        float t = Mathf.Sqrt(d2) / c.radius;
        if (t < 1f) return -c.depth + (c.depth + c.rim) * Smooth01(t);
        return c.rim * (1f - Smooth01((t - 1f) / (RimOut - 1f)));
    }

    float RidgeAt(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        if (r < ridgeRadius - ridgeWobble - 1f) return 0f;

        // Шум по колу (через cos/sin кута — щоб не було шва): край хребта «гуляє», висота має вершини й сідловини
        float a = Mathf.Atan2(z, x);
        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        float r0 = ridgeRadius + ridgeWobble * Signed(ca * 1.7f + noiseOffsets[4], sa * 1.7f + noiseOffsets[5]);
        float t = (r - r0) / ridgeRise;
        if (t <= 0f) return 0f;

        float peaks = 0.7f + 0.3f * Signed(ca * 4.5f + noiseOffsets[6], sa * 4.5f + noiseOffsets[7])
                           + 0.12f * Signed(ca * 11f + noiseOffsets[8], sa * 11f + noiseOffsets[9]);
        float h = ridgeHeight * peaks * Smooth01(t);
        if (t > 1f) h += 3f * Signed(x / 18f + noiseOffsets[10], z / 18f + noiseOffsets[11]) * Mathf.Clamp01(t - 1f); // нерівні вершини
        return h;
    }

    /// <summary>Колір вершини: дно кратерів з льодом темне й холодне (туди ніколи не заглядає Сонце).</summary>
    Color32 TintAt(float x, float z)
    {
        Color c = Color.white;
        foreach (var cr in craters)
        {
            float dx = x - cr.pos.x, dz = z - cr.pos.y;
            float reach = cr.radius * 1.15f;
            float d2 = dx * dx + dz * dz;
            if (d2 >= reach * reach) continue;
            float k = 1f - Smooth01(Mathf.Sqrt(d2) / reach); // 1 у центрі, 0 біля валу
            c = Color.Lerp(c, cr.ice ? IceTint : CraterTint, cr.ice ? Mathf.Sqrt(k) : k);
        }
        return c == Color.white ? White : (Color32)c;
    }

    // =====================================================================
    // Побудова сітки
    // =====================================================================

    void Awake()
    {
        if (material == null)
        {
            Debug.LogWarning("MoonTerrain: немає матеріалу — земля лишається рівною. Запусти меню Moon → Рельєф: кратери, пагорби, хребет.");
            return;
        }
        Instance = this;
        InitNoise();
        Build();
        HideFlatGround();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void InitNoise()
    {
        var rnd = new System.Random(seed);
        noiseOffsets = new float[12];
        for (int i = 0; i < noiseOffsets.Length; i++) noiseOffsets[i] = 10f + (float)rnd.NextDouble() * 150f;
    }

    /// <summary>Шум від -1 до 1 (Perlin — плавні «хвилі»).</summary>
    static float Signed(float u, float v) => Mathf.PerlinNoise(u, v) * 2f - 1f;

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static float Dist(Vector2 p, float x, float z)
    {
        float dx = x - p.x, dz = z - p.y;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void Build()
    {
        int chunks = Mathf.CeilToInt(halfSize * 2f / chunkSize);
        int n = Mathf.Max(2, Mathf.RoundToInt(chunkSize / step) + 1); // вершин уздовж сторони шматка
        float s = chunkSize / (n - 1);
        int w = n + 2;                                                // з рамкою — для нормалей на краях

        var heights = new float[w * w];
        var verts = new Vector3[n * n];
        var normals = new Vector3[n * n];
        var colors = new Color32[n * n];
        var tris = new int[(n - 1) * (n - 1) * 6];
        int ti = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int k = j * n + i;
                tris[ti++] = k; tris[ti++] = k + n; tris[ti++] = k + 1;          // за годинниковою стрілкою, якщо дивитись згори
                tris[ti++] = k + 1; tris[ti++] = k + n; tris[ti++] = k + n + 1;
            }

        for (int cz = 0; cz < chunks; cz++)
            for (int cx = 0; cx < chunks; cx++)
            {
                float x0 = -halfSize + cx * chunkSize, z0 = -halfSize + cz * chunkSize;
                for (int j = -1; j <= n; j++)
                    for (int i = -1; i <= n; i++)
                        heights[(j + 1) * w + (i + 1)] = GroundHeight(x0 + i * s, z0 + j * s);

                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int k = j * n + i, hk = (j + 1) * w + (i + 1);
                        verts[k] = new Vector3(i * s, heights[hk], j * s);
                        normals[k] = new Vector3(heights[hk - 1] - heights[hk + 1], 2f * s, heights[hk - w] - heights[hk + w]).normalized;
                        colors[k] = TintAt(x0 + i * s, z0 + j * s);
                    }

                var mesh = new Mesh { name = "Moon Terrain " + cx + "_" + cz };
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.colors32 = colors;
                mesh.triangles = tris;
                mesh.RecalculateBounds();

                var go = new GameObject("Terrain Chunk " + cx + "_" + cz);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(x0, 0f, z0);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
    }

    /// <summary>Стара рівна площина Ground більше не потрібна (у редакторі вона лишається, щоб було на чому стояти).</summary>
    static void HideFlatGround()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) return;
        var r = ground.GetComponent<Renderer>();
        if (r != null) r.enabled = false;
        var c = ground.GetComponent<Collider>();
        if (c != null) c.enabled = false;
    }
}
