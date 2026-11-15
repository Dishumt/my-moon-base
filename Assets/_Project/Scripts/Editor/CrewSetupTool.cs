// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Налаштувати екіпаж і роботів.
///  1) На об'єкт World додає CrewLook (вигляд екіпажу) і CrewView (створює астронавтів і роботів у грі).
///  2) Картинки всіх спеціальностей з Art/Characters стають спрайтами і потрапляють у CrewLook.
///  3) Іконки стану з Art/Icons/Status_64 (голод, хвороба, радіація, енергія) — теж у CrewLook.
///  4) Кадри робота з Art/Characters/robot (їде, будує, засипає реголітом, спить) — у CrewLook.
/// Можна запускати знову, коли арт додасть нові кадри.
/// </summary>
public static class CrewSetupTool
{
    const string Chars = "Assets/_Project/Art/Characters/";
    const string Icons = "Assets/_Project/Art/Icons/Status_64";
    const float IconPixelsPerUnit = 140f; // іконка 64 px ≈ 0,45 м над головою

    // спеціальність, папка «без скафандра», префікс файлів, папка «у скафандрі»
    static readonly (CrewRole role, string indoorDir, string prefix, string suitDir)[] Roles =
    {
        (CrewRole.Engineer, "engineer", "engineer", "engineer_orange_outdoor"),
        (CrewRole.Medic, "doctor", "medic", "doctor_red_outdoor"),
        (CrewRole.Botanist, "botanist", "botanist", "botanist_green_outdoor"),
        (CrewRole.Geologist, "geologist", "geologist", "geologist_brown_outdoor"),
    };

    [MenuItem("Moon/Налаштувати екіпаж і роботів")]
    public static void Setup()
    {
        var world = GameObject.Find("World");
        if (world == null) { Debug.LogError("CrewSetupTool: у сцені немає об'єкта World."); return; }

        var look = world.GetComponent<CrewLook>();
        if (look == null) look = Undo.AddComponent<CrewLook>(world);
        if (world.GetComponent<CrewView>() == null) Undo.AddComponent<CrewView>(world);
        Undo.RecordObject(look, "Crew sprites");

        look.roles.Clear();
        foreach (var r in Roles)
        {
            string indoor = Chars + r.indoorDir, suit = Chars + r.suitDir;
            EngineerSetupTool.ImportAsSprites(indoor);
            EngineerSetupTool.ImportAsSprites(suit);
            string p = r.prefix + "_";
            look.roles.Add(new CrewLook.RoleLook
            {
                role = r.role,
                suit = new EngineerController.SpriteSet
                {
                    idleFront = EngineerSetupTool.Load(suit, "front"),
                    idleBack = EngineerSetupTool.Load(suit, "back"),
                    walkFront = EngineerSetupTool.Frames(suit, "walk_front_"),
                    walkBack = EngineerSetupTool.Frames(suit, "walk_back_"),
                    walkLeft = EngineerSetupTool.Frames(suit, "walk_left_"),
                    walkRight = EngineerSetupTool.Frames(suit, "walk_right_"),
                    pushFrames = new[] { 1, 2 },
                    airFrames = new[] { 0, 3 },
                },
                indoor = new EngineerController.SpriteSet
                {
                    // «стоїть» — кадр ходьби з ногами на землі (окремі картинки намальовані в іншому масштабі)
                    idleFront = EngineerSetupTool.Load(indoor, p + "front_walk_02") ?? EngineerSetupTool.Load(indoor, p + "front"),
                    idleBack = EngineerSetupTool.Load(indoor, p + "back_walk_02") ?? EngineerSetupTool.Load(indoor, p + "back"),
                    walkFront = EngineerSetupTool.Frames(indoor, p + "front_walk_"),
                    walkBack = EngineerSetupTool.Frames(indoor, p + "back_walk_"),
                    walkLeft = EngineerSetupTool.Frames(indoor, p + "left_profile_walk_"),
                    walkRight = new Sprite[0],
                    pushFrames = new[] { 0, 2 },
                    airFrames = new[] { 1, 3 },
                },
            });
        }

        ImportIcons();
        SetupRobot(look);
        look.iconHungry = Icon("satiety");
        look.iconSick = Icon("health");
        look.iconSevere = Icon("health_cross");
        look.iconDoseYellow = Icon("dose_yellow");
        look.iconDoseRed = Icon("dose_red");

        EditorUtility.SetDirty(look);
        EditorSceneManager.MarkSceneDirty(world.scene);
        int frames = 0;
        foreach (var r in look.roles) frames += r.suit.walkFront.Length + r.indoor.walkFront.Length;
        Debug.Log("CrewSetupTool: готово! Спеціальностей: " + look.roles.Count + ", кадрів «вперед»: " + frames
                  + ", кадрів робота: " + RobotFrameCount(look) + ". Збережи сцену (Ctrl+S).");
    }

    static void ImportIcons()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Icons }))
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
            if (imp == null || (imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, IconPixelsPerUnit)))
                continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = IconPixelsPerUnit;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
    }

    static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "/" + name + ".png");

    // ---------- Робот ----------

    const string RobotDir = "Assets/_Project/Art/Characters/robot";
    const float RobotPixelsPerUnit = 85f; // кадр 128 px, сам робот ≈ 95 px → ≈ 1,1 м (людина ≈ 1,6 м)

    static void SetupRobot(CrewLook look)
    {
        look.robotDriveAway = RobotFrames("drive_away_");
        look.robotDriveToward = RobotFrames("drive_toward_");
        look.robotLeft = RobotFrames("walk_left_");
        look.robotRight = RobotFrames("walk_right_");
        look.robotBuild = RobotFrames("construction_");
        look.robotShield = RobotFrames("cleaning_");
        look.robotSleep = RobotFrames("sleep_");
        look.iconNoEnergy = Icon("energy");
    }

    static int RobotFrameCount(CrewLook look)
    {
        int n = 0;
        foreach (var a in new[] { look.robotDriveAway, look.robotDriveToward, look.robotLeft, look.robotRight,
                                  look.robotBuild, look.robotShield, look.robotSleep })
            if (a != null) n += a.Length;
        return n;
    }

    /// <summary>
    /// Кадри однієї анімації робота. Усі кадри групи отримують однакову точку опори — найнижчий піксель коліс у групі:
    /// так колеса стоять на землі, а підстрибування, яке намалювала Юля, зберігається.
    /// </summary>
    static Sprite[] RobotFrames(string prefix)
    {
        var paths = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { RobotDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileName(path).StartsWith(prefix)) paths.Add(path);
        }
        if (paths.Count == 0) return new Sprite[0];
        paths.Sort(System.StringComparer.Ordinal);

        float pivotY = 1f;
        foreach (var path in paths) pivotY = Mathf.Min(pivotY, LowestOpaqueRow(path));
        foreach (var path in paths) ImportRobotFrame(path, new Vector2(0.5f, pivotY));
        return EngineerSetupTool.Frames(RobotDir, prefix);
    }

    /// <summary>Найнижчий непрозорий рядок картинки, частка висоти знизу (0 — самий низ). Читаємо PNG напряму (це редактор).</summary>
    static float LowestOpaqueRow(string path)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return 0f; }
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height, low = -1;
        for (int y = 0; y < h && low < 0; y++)           // у текстурі y = 0 — нижній рядок
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 20) { low = y; break; }
        Object.DestroyImmediate(tex);
        return low < 0 ? 0f : (float)low / h;
    }

    static void ImportRobotFrame(string path, Vector2 pivot)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        bool already = imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, RobotPixelsPerUnit)
                       && settings.spriteAlignment == (int)SpriteAlignment.Custom && Vector2.Distance(settings.spritePivot, pivot) < 0.001f
                       && imp.filterMode == FilterMode.Point;
        if (already) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = RobotPixelsPerUnit;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Point;                                 // піксель-арт без розмиття
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
