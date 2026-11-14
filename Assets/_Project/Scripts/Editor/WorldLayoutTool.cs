// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Розставити базу і поверхню (відкрита сцена).
/// Один клік замість сотні:
///  - земля стає 400×400 м (від центру до краю ≈ 40 с ходьби);
///  - 12 слотів модулів сіткою навколо центру (наявні слоти з тими самими номерами просто пересуваються);
///  - точки PoiMarker для кожної точки з chapter_01.json, з простими заглушками-кубами і циліндрами.
/// Потім усе можна посунути руками. Можна скасувати через Ctrl+Z.
/// Точки, які вже є в сцені (за PoiId), не чіпає.
/// </summary>
public static class WorldLayoutTool
{
    const int SlotCount = 12;
    const float SlotSpacing = 8f;   // модуль 6 м + 2 м прохід

    [MenuItem("Moon/Розставити базу і поверхню (відкрита сцена)")]
    public static void Layout()
    {
        Undo.SetCurrentGroupName("Moon layout");
        int group = Undo.GetCurrentGroup();

        ResizeGround();
        LayoutSlots();
        LayoutPois();

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("WorldLayoutTool: готово! Перевір сцену і збережи (Ctrl+S).");
    }

    static void ResizeGround()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) { Debug.LogWarning("WorldLayoutTool: немає об'єкта Ground."); return; }
        Undo.RecordObject(ground.transform, "Ground");
        ground.transform.localScale = new Vector3(40f, 1f, 40f); // Plane 10 м × 40 = 400 м
    }

    static void LayoutSlots()
    {
        var parent = GetOrCreate("Slots").transform;

        // Сітка 4 × 3 з центром у (0,0); інженер стартує в проході між модулями.
        for (int id = 0; id < SlotCount; id++)
        {
            int col = id % 4, row = id / 4;
            var pos = new Vector3((col - 1.5f) * SlotSpacing, 0f, (row - 1f) * SlotSpacing + SlotSpacing * 0.5f);

            var slot = FindSlot(id);
            if (slot == null)
            {
                var go = new GameObject("Slot " + id);
                Undo.RegisterCreatedObjectUndo(go, "Slot");
                go.transform.SetParent(parent, false);
                slot = Undo.AddComponent<ModuleSlot>(go);
                slot.slotId = id;
            }
            else
            {
                Undo.RecordObject(slot.gameObject, "Slot");
                slot.gameObject.name = "Slot " + id;
                Undo.SetTransformParent(slot.transform, parent, "Slot");
            }
            Undo.RecordObject(slot.transform, "Slot");
            slot.transform.position = pos;
        }
    }

    static ModuleSlot FindSlot(int id)
    {
        foreach (var s in Object.FindObjectsByType<ModuleSlot>(FindObjectsSortMode.None))
            if (s.slotId == id) return s;
        return null;
    }

    static void LayoutPois()
    {
        var data = DataLoader.Load<ChapterStartData>("Chapters/chapter_01");
        if (data == null || data.pois == null) { Debug.LogWarning("WorldLayoutTool: не прочитав chapter_01.json"); return; }

        var parent = GetOrCreate("Pois").transform;
        var existing = Object.FindObjectsByType<PoiMarker>(FindObjectsSortMode.None);

        int sample = 0, fact = 0, other = 0;
        foreach (var p in data.pois)
        {
            bool found = false;
            foreach (var m in existing) if (m.poiId == p.id) found = true;
            if (found) continue;

            System.Enum.TryParse(p.type, out PoiType type);

            // Що цінніше — то далі від бази (GAME.md). Кут розкидає точки довкола.
            float dist, angle;
            switch (type)
            {
                case PoiType.Ice: dist = 140f; angle = 200f; break;
                case PoiType.Sample: dist = 70f + 25f * sample; angle = 60f + 110f * sample++; break;
                case PoiType.LandingPad: dist = 35f; angle = 270f; break;
                case PoiType.Fact: dist = 50f + 30f * fact; angle = 20f + 140f * fact++; break;
                default: dist = 60f; angle = 90f * other++; break;
            }
            float rad = angle * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);

            var go = new GameObject("POI " + p.id);
            Undo.RegisterCreatedObjectUndo(go, "POI");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var marker = go.AddComponent<PoiMarker>();
            marker.poiId = p.id;
            marker.type = type;
            AddPlaceholder(go.transform, type, marker);
        }
    }

    /// <summary>Прості заглушки, поки 02 не намалює спрайти.</summary>
    static void AddPlaceholder(Transform parent, PoiType type, PoiMarker marker)
    {
        var ground = GameObject.Find("Ground");
        var mat = ground != null ? ground.GetComponent<Renderer>().sharedMaterial : null;

        switch (type)
        {
            case PoiType.Ice:
                // Темний кратер з холодним світлом
                Prim(PrimitiveType.Cylinder, parent, "Dark Crater", new Vector3(0f, 0.01f, 0f), new Vector3(18f, 0.01f, 18f), GetDarkMaterial(mat));
                var lightGo = new GameObject("Cold Light");
                lightGo.transform.SetParent(parent, false);
                lightGo.transform.localPosition = new Vector3(0f, 4f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.55f, 0.75f, 1f);
                l.range = 14f;
                l.intensity = 4f;
                marker.discoverRadius = 9f;
                break;
            case PoiType.Sample:
                Prim(PrimitiveType.Cube, parent, "Rock A", new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.7f), mat);
                Prim(PrimitiveType.Cube, parent, "Rock B", new Vector3(1.0f, 0.2f, 0.6f), new Vector3(0.5f, 0.4f, 0.5f), mat);
                break;
            case PoiType.LandingPad:
                Prim(PrimitiveType.Cylinder, parent, "Pad", new Vector3(0f, 0.05f, 0f), new Vector3(12f, 0.05f, 12f), mat);
                marker.discoverRadius = 8f;
                break;
            case PoiType.Fact:
                Prim(PrimitiveType.Cylinder, parent, "Sign Pole", new Vector3(0f, 1f, 0f), new Vector3(0.15f, 1f, 0.15f), mat);
                break;
        }
    }

    const string DarkMatPath = "Assets/_Project/Settings/CraterDark.mat";

    /// <summary>Темний матеріал для кратера (у вічній тіні). Створюється один раз у Settings/.</summary>
    static Material GetDarkMaterial(Material source)
    {
        var dark = AssetDatabase.LoadAssetAtPath<Material>(DarkMatPath);
        if (dark != null || source == null) return dark != null ? dark : source;
        dark = new Material(source) { color = new Color(0.12f, 0.13f, 0.16f) };
        AssetDatabase.CreateAsset(dark, DarkMatPath);
        return dark;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // по заглушках можна ходити
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject GetOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;
        go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        return go;
    }
}
