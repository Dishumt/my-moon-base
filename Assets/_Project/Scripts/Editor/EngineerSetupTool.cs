// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Налаштувати інженера (спрайти й анімація).
///  1) Картинки інженера з Art/Characters стають спрайтами: ноги внизу, розмір ≈ 1.7 м.
///  2) На інженері (тег Player) ховає кубик і створює дочірній об'єкт Sprite (SpriteRenderer + Billboard).
///  3) Заповнює в EngineerController набори кадрів «у скафандрі» і «без скафандра» за назвами файлів.
/// Можна запускати знову, коли арт додасть нові кадри.
/// </summary>
public static class EngineerSetupTool
{
    const string SuitDir = "Assets/_Project/Art/Characters/engineer_orange_outdoor";
    const string IndoorDir = "Assets/_Project/Art/Characters/engineer";
    const float PixelsPerUnit = 72f;            // персонаж ≈ 120 px → ≈ 1.7 м
    static readonly Vector2 Pivot = new Vector2(0.5f, 0.03f); // точка між ногами (ноги на 124-му пікселі з 128)

    [MenuItem("Moon/Налаштувати інженера (спрайти й анімація)")]
    public static void Setup()
    {
        ImportAsSprites(SuitDir);
        ImportAsSprites(IndoorDir);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) { Debug.LogError("EngineerSetupTool: у сцені немає об'єкта з тегом Player."); return; }
        var ctrl = player.GetComponent<EngineerController>();
        if (ctrl == null) { Debug.LogError("EngineerSetupTool: на Player немає EngineerController."); return; }

        Undo.RecordObject(ctrl, "Engineer sprites");
        var sr = GetOrCreateSprite(player.transform);

        var so = new SerializedObject(ctrl);
        so.FindProperty("spriteRenderer").objectReferenceValue = sr;
        so.ApplyModifiedProperties();

        // У скафандрі: front.png, back.png, walk_front_01..04, walk_back_.., walk_left_.., walk_right_..
        ctrl.suit = new EngineerController.SpriteSet
        {
            idleFront = Load(SuitDir, "front"),
            idleBack = Load(SuitDir, "back"),
            walkFront = Frames(SuitDir, "walk_front_"),
            walkBack = Frames(SuitDir, "walk_back_"),
            walkLeft = Frames(SuitDir, "walk_left_"),
            walkRight = Frames(SuitDir, "walk_right_"),
        };
        // Без скафандра: engineer_front, engineer_back (або старі *_128), engineer_front_walk_.., engineer_back_walk_..,
        // engineer_left_profile_walk_.. (вправо — дзеркально)
        ctrl.indoor = new EngineerController.SpriteSet
        {
            idleFront = Load(IndoorDir, "engineer_front") ?? Load(IndoorDir, "engineer_front_128"),
            idleBack = Load(IndoorDir, "engineer_back") ?? Load(IndoorDir, "engineer_back_128"),
            walkFront = Frames(IndoorDir, "engineer_front_walk_"),
            walkBack = Frames(IndoorDir, "engineer_back_walk_"),
            walkLeft = Frames(IndoorDir, "engineer_left_profile_walk_"),
            walkRight = new Sprite[0],
        };
        sr.sprite = ctrl.suit.idleFront;

        EditorUtility.SetDirty(ctrl);
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("EngineerSetupTool: готово! Скафандр: " + ctrl.suit.walkFront.Length + " кадрів уперед, без скафандра: "
                  + ctrl.indoor.walkFront.Length + ". Збережи сцену (Ctrl+S).");
    }

    /// <summary>Усі PNG у папці → Sprite з ногами внизу.</summary>
    static void ImportAsSprites(string dir)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            bool already = imp.textureType == TextureImporterType.Sprite && settings.spriteAlignment == (int)SpriteAlignment.Custom
                           && Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit)
                           && imp.wrapMode == TextureWrapMode.Clamp && imp.filterMode == FilterMode.Point;
            if (already) continue;

            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = PixelsPerUnit;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;      // край картинки не «перетікає» на протилежний бік
            imp.filterMode = FilterMode.Point;         // піксель-арт без розмиття
            imp.textureCompression = TextureImporterCompression.Uncompressed; // без бруду від стиснення
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = Pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(settings);
            imp.SaveAndReimport();
        }
    }

    static SpriteRenderer GetOrCreateSprite(Transform player)
    {
        // Кубик-заглушку ховаємо (колайдер руху — це CharacterController, він лишається)
        var mesh = player.GetComponent<MeshRenderer>();
        if (mesh != null) { Undo.RecordObject(mesh, "Hide cube"); mesh.enabled = false; }

        var sr = player.GetComponentInChildren<SpriteRenderer>();
        if (sr == null)
        {
            var go = new GameObject("Sprite");
            Undo.RegisterCreatedObjectUndo(go, "Engineer sprite");
            go.transform.SetParent(player, false);
            sr = go.AddComponent<SpriteRenderer>();
        }
        // Ноги спрайта — на землі: інженер стоїть центром на висоті 0.5 (половина кубика)
        Undo.RecordObject(sr.transform, "Engineer sprite");
        sr.transform.localPosition = new Vector3(0f, -player.position.y, 0f);
        if (sr.GetComponent<Billboard>() == null) Undo.AddComponent<Billboard>(sr.gameObject);
        return sr;
    }

    static Sprite Load(string dir, string name) => AssetDatabase.LoadAssetAtPath<Sprite>(dir + "/" + name + ".png");

    /// <summary>Кадри name01, name02 ... поки файли є.</summary>
    static Sprite[] Frames(string dir, string prefix)
    {
        var list = new List<Sprite>();
        for (int i = 1; i <= 16; i++)
        {
            var s = Load(dir, prefix + i.ToString("00"));
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }
}
