// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Налаштувати екіпаж і роботів.
///  1) На об'єкт World додає CrewLook (вигляд екіпажу) і CrewView (створює астронавтів і роботів у грі).
///  2) Картинки всіх спеціальностей з Art/Characters стають спрайтами і потрапляють у CrewLook.
///  3) Іконки стану з Art/Icons/Status_64 (голод, хвороба, радіація) — теж у CrewLook.
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
                  + ". Збережи сцену (Ctrl+S).");
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
}
