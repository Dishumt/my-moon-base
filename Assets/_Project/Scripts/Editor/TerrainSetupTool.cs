// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Рельєф: кратери, пагорби, хребет.
///  1) ставить маркери точок з усіх розділів, яких ще немає (WorldLayoutTool);
///  2) створює матеріал Settings/MoonTerrain.mat (шейдер Moon/Terrain, текстура — та сама, що на Ground);
///  3) на об'єкт Terrain ставить MoonTerrain і заповнює: великі кратери з льодом на точках ice_*,
///     10–12 менших кратерів, 4 великі пагорби, рівні місця біля майданчика й точок;
///  4) ховає старі пласкі «темні кратери»-заглушки на точках льоду (тепер там справжні кратери).
/// Розстановка завжди та сама (seed), тож можна запускати знову — нічого не «стрибне».
/// Сам рельєф з'являється тільки в Play (його будує MoonTerrain), у редакторі лишається рівна Ground.
/// </summary>
public static class TerrainSetupTool
{
    const string MatPath = "Assets/_Project/Settings/MoonTerrain.mat";

    [MenuItem("Moon/Рельєф: кратери, пагорби, хребет")]
    public static void Setup()
    {
        WorldLayoutTool.LayoutPois();

        var mat = GetOrCreateMaterial();
        if (mat == null) return;

        var go = GameObject.Find("Terrain");
        if (go == null)
        {
            go = new GameObject("Terrain");
            Undo.RegisterCreatedObjectUndo(go, "Terrain");
        }
        var t = go.GetComponent<MoonTerrain>();
        if (t == null) t = Undo.AddComponent<MoonTerrain>(go);
        Undo.RecordObject(t, "Moon terrain");
        t.material = mat;

        var markers = Object.FindObjectsByType<PoiMarker>();
        Generate(t, markers);
        HidePlaceholders(markers);

        EditorUtility.SetDirty(t);
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("TerrainSetupTool: готово! Кратерів: " + t.craters.Count + " (з льодом: " + t.craters.FindAll(c => c.ice).Count
                  + "), пагорбів: " + t.hills.Count + ". Рельєф з'явиться в Play. Збережи сцену (Ctrl+S).");
    }

    // ---------- Матеріал ----------

    static Material GetOrCreateMaterial()
    {
        var shader = Shader.Find("Moon/Terrain");
        if (shader == null) { Debug.LogError("TerrainSetupTool: немає шейдера Moon/Terrain (Settings/Shaders/MoonTerrain.shader)."); return null; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        else mat.shader = shader;

        // Та сама текстура реголіту, що на рівній землі, і такий самий розмір плитки
        var ground = GameObject.Find("Ground");
        var groundRenderer = ground != null ? ground.GetComponent<Renderer>() : null; // без «?.»: у редакторі Unity відсутній компонент — не справжній null
        var gm = groundRenderer != null ? groundRenderer.sharedMaterial : null;
        if (gm != null && gm.HasProperty("_BaseMap") && gm.GetTexture("_BaseMap") != null)
        {
            mat.SetTexture("_BaseMap", gm.GetTexture("_BaseMap"));
            float tiling = gm.GetTextureScale("_BaseMap").x;
            float groundSize = ground.transform.lossyScale.x * 10f; // Plane — 10 м
            if (tiling > 0.01f) mat.SetFloat("_TileSize", groundSize / tiling);
        }
        else Debug.LogWarning("TerrainSetupTool: на Ground немає текстури — постав її в Base Map матеріалу MoonTerrain.");

        // Звичайна яскравість Сонця: тоді рівна земля така сама світла, як була
        var dark = Object.FindAnyObjectByType<OutdoorDarkening>();
        var sun = dark != null ? dark.GetComponent<Light>() : null;
        if (sun != null)
        {
            Color c = (QualitySettings.activeColorSpace == ColorSpace.Linear ? sun.color.linear : sun.color) * sun.intensity;
            mat.SetFloat("_LightRef", Mathf.Max(0.05f, 0.299f * c.r + 0.587f * c.g + 0.114f * c.b));
        }

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ---------- Розстановка ----------

    static void Generate(MoonTerrain t, PoiMarker[] markers)
    {
        var rnd = new System.Random(t.seed);
        t.craters.Clear();
        t.hills.Clear();
        t.flats.Clear();

        var pois = new List<Vector2>();
        foreach (var m in markers)
        {
            var p = new Vector2(m.transform.position.x, m.transform.position.z);
            pois.Add(p);
            switch (m.type)
            {
                case PoiType.Ice: // лід — на дні великого темного кратера (так і в науці: лід у вічно затінених кратерах)
                    t.craters.Add(new MoonTerrain.Crater { pos = p, radius = Range(rnd, 14f, 16.5f), depth = 5f, rim = 1.3f, ice = true });
                    break;
                case PoiType.LandingPad:
                    t.flats.Add(new MoonTerrain.FlatSpot { pos = p, radius = 10f });
                    break;
                default:
                    t.flats.Add(new MoonTerrain.FlatSpot { pos = p, radius = 5f });
                    break;
            }
        }

        float outerLimit = t.ridgeRadius - t.ridgeWobble - 6f; // далі — підніжжя хребта
        float baseEdge = t.baseFlat + t.baseBlend;

        // Великі пагорби (5–8 м) — на них можна вилізти й роздивитися околиці
        for (int tries = 0; tries < 400 && t.hills.Count < 4; tries++)
        {
            float r = Range(rnd, 26f, 34f);
            Vector2 p = Polar(rnd, 75f, outerLimit - r * 0.6f);
            if (Square(p) < baseEdge + r * 0.7f) continue;
            if (Near(pois, p, r + 12f)) continue;
            if (t.craters.Exists(c => (c.pos - p).magnitude < r + c.radius * 1.7f + 4f)) continue;
            if (t.hills.Exists(h => (h.pos - p).magnitude < r + h.radius)) continue;
            t.hills.Add(new MoonTerrain.Hill { pos = p, radius = r, height = Range(rnd, 5f, 8f) });
        }

        // Менші кратери (діаметр 7–16 м) з валом — пологі, у них можна спуститися й вийти пішки
        int small = 0;
        for (int tries = 0; tries < 800 && small < 12; tries++)
        {
            float r = Range(rnd, 3.5f, 8f);
            float outer = r * 1.7f;
            Vector2 p = Polar(rnd, baseEdge + outer, outerLimit - outer);
            if (Square(p) < baseEdge + outer) continue;
            if (Near(pois, p, outer + 8f)) continue;
            if (t.craters.Exists(c => (c.pos - p).magnitude < (r + c.radius) * 1.7f + 2f)) continue;
            float depth = r * 0.32f;
            t.craters.Add(new MoonTerrain.Crater { pos = p, radius = r, depth = depth, rim = depth * 0.28f });
            small++;
        }
    }

    /// <summary>Старі пласкі «темні кратери» на точках льоду встромлялися б у схили справжнього кратера — ховаємо.</summary>
    static void HidePlaceholders(PoiMarker[] markers)
    {
        foreach (var m in markers)
        {
            if (m.type != PoiType.Ice) continue;
            var dark = m.transform.Find("Dark Crater");
            if (dark == null || !dark.gameObject.activeSelf) continue;
            Undo.RecordObject(dark.gameObject, "Hide placeholder");
            dark.gameObject.SetActive(false);
        }
    }

    static float Range(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    static Vector2 Polar(System.Random rnd, float minR, float maxR)
    {
        float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
        float r = Range(rnd, minR, Mathf.Max(minR, maxR));
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
    }

    static float Square(Vector2 p) => Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)); // відстань від центру по X або Z

    static bool Near(List<Vector2> points, Vector2 p, float dist)
    {
        foreach (var q in points) if ((q - p).sqrMagnitude < dist * dist) return true;
        return false;
    }
}
