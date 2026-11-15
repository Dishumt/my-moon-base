// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Каміння на поверхні Місяця:
///  - великі 3D-валуни (гранчасті, у мультяшному стилі модулів, з контуром) — їх треба обійти або перестрибнути,
///    на них можна вилізти (у них є колайдер);
///  - дрібні камінці, плити, руда, лід і маленькі кратери — спрайти Юлі (Art/Props/Moon rocks), стоять купками.
/// На точках поверхні — «свої» камені: лід на дні кратерів з льодом, руда на точках зразків.
/// Список заповнює меню Moon → Розкидати каміння; самі об'єкти з'являються в Play (стають на рельєф MoonTerrain).
/// </summary>
public class MoonDecor : MonoBehaviour
{
    [Serializable]
    public class Boulder
    {
        public Vector2 pos;
        public Vector3 size = new Vector3(2f, 1.2f, 1.8f); // ширина, висота, глибина, м
        public float yaw;
        public int seed;
        public Color color = new Color(0.6f, 0.58f, 0.6f);
    }

    [Serializable]
    public class Prop
    {
        public Vector2 pos;
        public int sprite;      // номер у sprites
        public float scale = 1f;
        public bool flip;
        public string poiId;    // камінь точки поверхні (ховається разом з нею, поки її немає в розділі); "" — просто декор
        public bool shadow;     // тінь-пляма під ним (камені, руда); кратерам і плитам не треба
    }

    [Header("Картинки Юлі (заповнює меню)")]
    public Sprite[] sprites = new Sprite[0];

    [Header("Що де лежить (заповнює меню)")]
    public List<Boulder> boulders = new List<Boulder>();
    public List<Prop> props = new List<Prop>();

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    // Спрайти каміння: на них не діє світло Сонця, тож темнішають самі разом з вулицею (тінь хребта, інженер у модулі)
    readonly List<SpriteRenderer> propRenderers = new List<SpriteRenderer>();
    float appliedBrightness = 1f;

    void Update()
    {
        float b = OutdoorDarkening.Brightness;
        if (Mathf.Abs(b - appliedBrightness) < 0.005f) return;
        appliedBrightness = b;
        var c = new Color(b, b, b, 1f);
        foreach (var r in propRenderers) if (r != null) r.color = c;
    }

    void Start() // після MoonTerrain.Awake (рельєф готовий) і після PoiMarker.OnEnable (точки знайдуться)
    {
        var look = ModuleLook.Instance;
        var mat = look != null ? look.wallMaterial : null;
        if (mat != null) foreach (var b in boulders) SpawnBoulder(b, mat);
        else if (boulders.Count > 0) Debug.LogWarning("MoonDecor: немає ModuleLook.wallMaterial — валунів не буде.");

        foreach (var p in props) SpawnProp(p);
    }

    // ---------- 3D-валун ----------

    void SpawnBoulder(Boulder b, Material mat)
    {
        var go = new GameObject("Boulder");
        go.transform.SetParent(transform, false);
        float ground = Mathf.Min(MoonTerrain.HeightAt(b.pos.x, b.pos.y),
                                 MoonTerrain.HeightAt(b.pos.x + b.size.x * 0.4f, b.pos.y),
                                 MoonTerrain.HeightAt(b.pos.x - b.size.x * 0.4f, b.pos.y));
        go.transform.position = new Vector3(b.pos.x, ground - b.size.y * 0.18f, b.pos.y); // трохи вкопаний — не «висить» на схилі
        go.transform.rotation = Quaternion.Euler(0f, b.yaw, 0f);

        var mesh = RockMesh(b.seed, b.size);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColor, b.color);
        r.SetPropertyBlock(block);
        var col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true; // опукла форма — інженер на неї вилазить і з неї стрибає
    }

    /// <summary>
    /// Гранчастий камінь: ікосаедр, поділений один раз (42 вершини), кожну вершину трохи посунуто (seed),
    /// низ сплющений. Кожна грань — окремий трикутник зі своєю нормаллю: так видно грані (мультяшний стиль).
    /// </summary>
    static Mesh RockMesh(int seed, Vector3 size)
    {
        var rnd = new System.Random(seed);
        var pts = new List<Vector3>();
        var faces = new List<int>();
        Icosphere(pts, faces);

        for (int i = 0; i < pts.Count; i++)
        {
            Vector3 v = pts[i] * (0.78f + 0.34f * (float)rnd.NextDouble()); // нерівний, як справжній камінь
            v.y = Mathf.Max(v.y, -0.35f);                                    // пласкіший низ
            pts[i] = new Vector3(v.x * size.x * 0.5f, (v.y + 0.35f) * size.y / 1.35f, v.z * size.z * 0.5f);
        }

        var verts = new Vector3[faces.Count];
        var tris = new int[faces.Count];
        for (int i = 0; i < faces.Count; i++)
        {
            verts[i] = pts[faces[i]];
            tris[i] = i;
        }
        var mesh = new Mesh { name = "Boulder " + seed };
        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals(); // вершини не спільні — кожна грань пласка
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Ікосаедр (20 граней), кожна грань поділена на 4 — куля з 80 трикутників радіусом 1.</summary>
    static void Icosphere(List<Vector3> pts, List<int> faces)
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        Vector3[] v =
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        foreach (var p in v) pts.Add(p.normalized);

        var mid = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (mid.TryGetValue(key, out int i)) return i;
            pts.Add(((pts[a] + pts[b]) * 0.5f).normalized);
            mid[key] = pts.Count - 1;
            return pts.Count - 1;
        }

        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            faces.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
    }

    // ---------- Спрайт Юлі ----------

    void SpawnProp(Prop p)
    {
        if (p.sprite < 0 || p.sprite >= sprites.Length || sprites[p.sprite] == null) return;
        Transform parent = transform;
        if (!string.IsNullOrEmpty(p.poiId))
        {
            var marker = PoiMarker.Find(p.poiId);
            if (marker != null) parent = marker.transform; // ховається разом з точкою, поки її немає в розділі
        }

        var go = new GameObject("Rock " + sprites[p.sprite].name);
        go.transform.SetParent(parent, false);
        float y = MoonTerrain.HeightAt(p.pos.x, p.pos.y) + 0.02f;
        if (parent == transform) go.transform.position = new Vector3(p.pos.x, y, p.pos.y);
        else
        {
            // Точка сама стає на рельєф у своєму Start (може бути і до, і після цього) — рахуємо від її кінцевої висоти
            Vector3 m = parent.position;
            go.transform.localPosition = new Vector3(p.pos.x - m.x, y - MoonTerrain.HeightAt(m), p.pos.y - m.z);
        }
        go.transform.localScale = Vector3.one * p.scale; // до Billboard: він запам'ятовує масштаб в Awake
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprites[p.sprite];
        sr.flipX = p.flip;
        propRenderers.Add(sr);
        // Картинки намальовані «спереду й трохи згори», як меблі, — стоять паралельно екрану
        go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;

        if (p.shadow)
        {
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(go.transform.parent, false);
            sh.transform.localPosition = go.transform.localPosition + Vector3.up * 0.03f;
            sh.transform.rotation = MoonTerrain.FlatOnGround(new Vector3(p.pos.x, 0f, p.pos.y));
            float w = sr.sprite.bounds.size.x * p.scale * 0.75f;
            sh.transform.localScale = new Vector3(w, w * 0.45f, 1f);
            var ssr = sh.AddComponent<SpriteRenderer>();
            ssr.sprite = WorldSprites.Blob;
            ssr.color = new Color(0f, 0f, 0f, 0.32f);
            ssr.sortingOrder = -2;
        }
    }
}
