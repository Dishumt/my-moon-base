// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Розкидати каміння.
///  1) Картинки Юлі з Art/Props/Moon rocks стають спрайтами (низ каменя — на землі).
///  2) На об'єкт Decor ставить MoonDecor і заповнює:
///     - лід на дні кожного кратера з льодом (замерзле озерце, кристали, руда з прожилками льоду);
///     - руду на точках зразків;
///     - ≈150 камінців, плит, валунів і маленьких кратерів купками довкола бази;
///     - 24 великі 3D-валуни — подалі від стежок, якими астронавти ходять до точок.
///  3) Ховає старі сірі кубики-заглушки на точках зразків.
/// Розстановка завжди та сама (seed). Спершу запусти Moon → Рельєф (каміння обходить кратери з льодом і базу).
/// Каміння з'являється в Play.
/// </summary>
public static class DecorSetupTool
{
    const string Dir = "Assets/_Project/Art/Props/Moon rocks";
    const float PixelsPerUnit = 80f; // 100 px каменя ≈ 1,25 м
    const int Seed = 2026;

    [MenuItem("Moon/Розкидати каміння")]
    public static void Setup()
    {
        var terrain = Object.FindAnyObjectByType<MoonTerrain>();
        if (terrain == null) { Debug.LogError("DecorSetupTool: спершу запусти меню Moon → Рельєф: кратери, пагорби, хребет."); return; }

        var go = GameObject.Find("Decor");
        if (go == null)
        {
            go = new GameObject("Decor");
            Undo.RegisterCreatedObjectUndo(go, "Decor");
        }
        go.transform.position = Vector3.zero;
        var decor = go.GetComponent<MoonDecor>();
        if (decor == null) decor = Undo.AddComponent<MoonDecor>(go);
        Undo.RecordObject(decor, "Moon decor");

        decor.sprites = ImportSprites(out var names);
        if (decor.sprites.Length == 0) { Debug.LogError("DecorSetupTool: немає картинок у " + Dir); return; }

        var markers = Object.FindObjectsByType<PoiMarker>();
        var rnd = new System.Random(Seed);
        decor.props.Clear();
        decor.boulders.Clear();
        PoiProps(decor, names, markers, rnd);
        ScatterProps(decor, names, terrain, markers, rnd);
        Boulders(decor, terrain, markers, rnd);
        HidePlaceholders(markers);

        EditorUtility.SetDirty(decor);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("DecorSetupTool: готово! Спрайтів-каменів: " + decor.props.Count + ", 3D-валунів: " + decor.boulders.Count
                  + ". Каміння з'явиться в Play. Збережи сцену (Ctrl+S).");
    }

    // ---------- Картинки ----------

    static Sprite[] ImportSprites(out List<string> names)
    {
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal);

        var sprites = new List<Sprite>();
        names = new List<string>();
        foreach (var path in paths)
        {
            Import(path, BottomPivot(path));
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) continue;
            sprites.Add(s);
            names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }
        return sprites.ToArray();
    }

    /// <summary>Точка опори — трохи вище найнижчого пікселя каменя: низ каменя трохи «в землі», не висить.</summary>
    static float BottomPivot(string path)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return 0.2f; }
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height, low = -1;
        for (int y = 0; y < h && low < 0; y++)          // у текстурі y = 0 — нижній рядок
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 20) { low = y; break; }
        Object.DestroyImmediate(tex);
        return low < 0 ? 0.2f : (low + 3f) / h;
    }

    static void Import(string path, float pivotY)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        var pivot = new Vector2(0.5f, pivotY);
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        bool already = imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit)
                       && settings.spriteAlignment == (int)SpriteAlignment.Custom && Vector2.Distance(settings.spritePivot, pivot) < 0.001f
                       && imp.filterMode == FilterMode.Point;
        if (already) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PixelsPerUnit;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Point;                                   // піксель-арт без розмиття
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }

    // ---------- Камені на точках ----------

    static void PoiProps(MoonDecor d, List<string> names, PoiMarker[] markers, System.Random rnd)
    {
        foreach (var m in markers)
        {
            var c = new Vector2(m.transform.position.x, m.transform.position.z);
            switch (m.type)
            {
                case PoiType.Ice: // на дні темного кратера: замерзле озерце, кристали льоду, руда з прожилками льоду
                    Add(d, names, "crater_with_ice_crystals", c, 2.2f, rnd, m.poiId, false);
                    Add(d, names, "frozen_ice_crater", c + Ring(rnd, 3.2f, 4f), 1.7f, rnd, m.poiId, false);
                    for (int i = 0; i < 5; i++) Add(d, names, "blue_ice_crystals", c + Ring(rnd, 2f, 5f), Range(rnd, 0.8f, 1.3f), rnd, m.poiId, true);
                    for (int i = 0; i < 2; i++) Add(d, names, "ice_veined_ore", c + Ring(rnd, 3.5f, 5.5f), Range(rnd, 0.9f, 1.2f), rnd, m.poiId, true);
                    break;
                case PoiType.Sample: // руда, яку збирає геолог
                    Add(d, names, rnd.NextDouble() < 0.5 ? "pale_mineral_ore" : "orange_metal_ore", c, 1.4f, rnd, m.poiId, true);
                    Add(d, names, "orange_metal_ore", c + Ring(rnd, 1.6f, 2.4f), Range(rnd, 0.8f, 1f), rnd, m.poiId, true);
                    Add(d, names, "pale_mineral_ore", c + Ring(rnd, 1.6f, 2.6f), Range(rnd, 0.7f, 1f), rnd, m.poiId, true);
                    for (int i = 0; i < 3; i++) Add(d, names, "lunar_pebble_small", c + Ring(rnd, 1.2f, 3f), Range(rnd, 0.8f, 1.2f), rnd, m.poiId, false);
                    break;
                case PoiType.Fact:
                    Add(d, names, "small_rock_cluster", c + Ring(rnd, 1.5f, 2.5f), 1f, rnd, m.poiId, true);
                    break;
            }
        }
    }

    // ---------- Каміння навколо (купками) ----------

    // Що частіше: дрібне — часто, скелі й великі кратери — рідко
    static readonly (string name, float weight, float minScale, float maxScale, bool shadow)[] Scatter =
    {
        ("lunar_pebble_small", 30f, 0.8f, 1.3f, false),
        ("small_rock_cluster", 22f, 0.8f, 1.2f, true),
        ("flat_rock_slab", 10f, 0.9f, 1.4f, false),
        ("pockmarked_moon_rock", 9f, 0.8f, 1.2f, true),
        ("low_boulder", 9f, 0.9f, 1.3f, true),
        ("rounded_boulder", 8f, 0.9f, 1.3f, true),
        ("empty_crater_small", 7f, 1.3f, 1.9f, false),
        ("empty_crater_large", 3f, 1.6f, 2.2f, false),
        ("jagged_rock_spire", 2f, 1.1f, 1.5f, true),
    };

    static void ScatterProps(MoonDecor d, List<string> names, MoonTerrain t, PoiMarker[] markers, System.Random rnd)
    {
        float total = 0f;
        foreach (var s in Scatter) total += s.weight;
        float edge = t.ridgeRadius - t.ridgeWobble - 3f;

        int placed = 0;
        for (int tries = 0; tries < 400 && placed < 150; tries++)
        {
            Vector2 center = Polar(rnd, 36f, edge - 3f);
            if (!FreeForDecor(t, markers, center, 4f)) continue;
            int count = rnd.Next(2, 6); // купка з 2–5 каменів
            for (int i = 0; i < count && placed < 150; i++)
            {
                Vector2 p = center + Ring(rnd, 0.4f, 3.2f);
                if (!FreeForDecor(t, markers, p, 3f)) continue;
                float roll = (float)rnd.NextDouble() * total;
                foreach (var s in Scatter)
                {
                    roll -= s.weight;
                    if (roll > 0f) continue;
                    Add(d, names, s.name, p, Range(rnd, s.minScale, s.maxScale), rnd, "", s.shadow);
                    placed++;
                    break;
                }
            }
        }
    }

    /// <summary>Не в базі, не на точках, не на дні кратерів з льодом (там свій лід) і не на хребті.</summary>
    static bool FreeForDecor(MoonTerrain t, PoiMarker[] markers, Vector2 p, float poiGap)
    {
        if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) < t.baseFlat + 3f) return false;
        if (p.magnitude > t.ridgeRadius - t.ridgeWobble - 2f) return false;
        foreach (var m in markers)
        {
            var q = new Vector2(m.transform.position.x, m.transform.position.z);
            float gap = m.type == PoiType.LandingPad ? 11f : poiGap;
            if ((q - p).sqrMagnitude < gap * gap) return false;
        }
        foreach (var c in t.craters)
            if (c.ice && (c.pos - p).magnitude < c.radius * 1.1f) return false;
        return true;
    }

    // ---------- 3D-валуни ----------

    static void Boulders(MoonDecor d, MoonTerrain t, PoiMarker[] markers, System.Random rnd)
    {
        // Стежки астронавтів: від центру бази прямо до кожної точки — там валунів не ставимо
        var targets = new List<Vector2>();
        foreach (var m in markers) targets.Add(new Vector2(m.transform.position.x, m.transform.position.z));

        float edge = t.ridgeRadius - t.ridgeWobble - 10f;
        for (int tries = 0; tries < 1500 && d.boulders.Count < 24; tries++)
        {
            Vector2 p = Polar(rnd, 44f, edge);
            float w = Range(rnd, 1.4f, 3.2f);
            if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) < t.baseFlat + t.baseBlend) continue;
            if (!FreeForDecor(t, markers, p, 8f)) continue;
            bool onPath = false;
            foreach (var q in targets) if (DistToSegment(p, Vector2.zero, q) < w * 0.5f + 4f) { onPath = true; break; }
            if (onPath) continue;
            if (d.boulders.Exists(b => (b.pos - p).magnitude < (b.size.x + w) * 0.5f + 2.5f)) continue;
            bool inBowl = false;
            foreach (var c in t.craters) if ((c.pos - p).magnitude < c.radius * 0.8f) { inBowl = true; break; }
            if (inBowl) continue;

            float g = Range(rnd, 0.5f, 0.66f);
            d.boulders.Add(new MoonDecor.Boulder
            {
                pos = p,
                size = new Vector3(w, Range(rnd, 0.8f, 1.8f), w * Range(rnd, 0.7f, 1.1f)),
                yaw = Range(rnd, 0f, 360f),
                seed = rnd.Next(1, 100000),
                color = new Color(g, g * 0.97f, g * 0.99f), // сірий реголіт з ледь теплим відтінком
            });
        }
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float k = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return (a + ab * k - p).magnitude;
    }

    /// <summary>Старі сірі кубики на точках зразків — тепер там руда Юлі.</summary>
    static void HidePlaceholders(PoiMarker[] markers)
    {
        foreach (var m in markers)
        {
            if (m.type != PoiType.Sample) continue;
            foreach (var name in new[] { "Rock A", "Rock B" })
            {
                var t = m.transform.Find(name);
                if (t == null || !t.gameObject.activeSelf) continue;
                Undo.RecordObject(t.gameObject, "Hide placeholder");
                t.gameObject.SetActive(false);
            }
        }
    }

    // ---------- Дрібниці ----------

    static void Add(MoonDecor d, List<string> names, string name, Vector2 pos, float scale, System.Random rnd, string poiId, bool shadow)
    {
        int i = names.IndexOf(name);
        if (i < 0) return; // такої картинки немає — пропускаємо
        d.props.Add(new MoonDecor.Prop { pos = pos, sprite = i, scale = scale, flip = rnd.NextDouble() < 0.5, poiId = poiId ?? "", shadow = shadow });
    }

    static float Range(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    static Vector2 Ring(System.Random rnd, float minR, float maxR)
    {
        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
        float r = Range(rnd, minR, maxR);
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    static Vector2 Polar(System.Random rnd, float minR, float maxR)
    {
        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Range(rnd, minR * minR, maxR * maxR)); // рівномірно по площі, а не густо біля центру
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }
}
