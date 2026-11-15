// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Налаштувати меблі (спрайти Юлі).
/// Усі картинки з Art/Props/Furniture стають спрайтами (низ предмета — на підлозі, розмір ≈ 1,85 м)
/// і потрапляють у World → ModuleLook → Furniture. RoomBuilder ставить їх у кімнати за назвами файлів.
/// Модулі, які вже стоять у грі, оновляться після перезапуску Play.
/// </summary>
public static class FurnitureSetupTool
{
    const string Dir = "Assets/_Project/Art/Props/Furniture";
    const float PixelsPerUnit = 115f;                          // 256 px → 2,2 м полотна, сам предмет ≈ 1,85 м
    static readonly Vector2 Pivot = new Vector2(0.5f, 0.07f);  // низ предмета (≈ 238-й піксель з 256)

    [MenuItem("Moon/Налаштувати меблі (спрайти Юлі)")]
    public static void Setup()
    {
        var look = Object.FindAnyObjectByType<ModuleLook>();
        if (look == null) { Debug.LogError("FurnitureSetupTool: у сцені немає ModuleLook (об'єкт World)."); return; }

        Undo.RecordObject(look, "Furniture sprites");
        look.furniture.Clear();
        // Юля розклала меблі по підпапках (Living room, MedBay, greenhouse...). FindAssets шукає і в них.
        // Якщо дві картинки мають однакову назву (water_recycler у greenhouse і в Laboratory), стару назву лишає
        // та, що з папки вище в списку FolderPriority, а інша стає «назва_папка» (water_recycler_laboratory).
        var paths = new System.Collections.Generic.List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort((x, y) =>
        {
            int c = FolderRank(x).CompareTo(FolderRank(y));
            return c != 0 ? c : string.CompareOrdinal(x, y);
        });
        foreach (string path in paths)
        {
            Import(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (look.FurnitureInfo(name) != null)
                name += "_" + System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)).ToLowerInvariant().Replace(' ', '_');
            var item = new ModuleLook.FurnitureSprite { name = name, sprite = sprite };
            Measure(path, item);
            look.furniture.Add(item);
        }

        EditorUtility.SetDirty(look);
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
        Debug.Log("FurnitureSetupTool: готово! Меблів: " + look.furniture.Count + ". Збережи сцену (Ctrl+S).");
    }

    // Папки, чиї картинки першими отримують «чисту» назву, якщо назви збігаються
    static readonly string[] FolderPriority = { "greenhouse", "Living room", "MedBay", "bathroom", "cafeteria", "Laboratory" };

    static int FolderRank(string path)
    {
        string folder = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path));
        int i = System.Array.IndexOf(FolderPriority, folder);
        return i >= 0 ? i : FolderPriority.Length;
    }

    /// <summary>
    /// Де на картинці сам предмет (непрозорі пікселі): з цього RoomBuilder знає ширину тіні під ним.
    /// Читаємо PNG-файл напряму — це редактор, тут System.IO можна (у грі ні).
    /// </summary>
    static void Measure(string path, ModuleLook.FurnitureSprite item)
    {
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(System.IO.File.ReadAllBytes(path))) { Object.DestroyImmediate(tex); return; }
        var px = tex.GetPixels32();
        int w = tex.width, h = tex.height, xMin = w, xMax = -1;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (px[y * w + x].a <= 20) continue;
            if (x < xMin) xMin = x;
            if (x > xMax) xMax = x;
        }
        Object.DestroyImmediate(tex);
        if (xMax < xMin) return; // картинка порожня

        item.width = (xMax - xMin + 1) / PixelsPerUnit;
        item.centerX = ((xMin + xMax + 1) * 0.5f - w * 0.5f) / PixelsPerUnit;
    }

    // Ці картинки гра читає попіксельно (FurnitureLife відрізає рослини від полиці, щоб вони росли) — потрібна галочка Read/Write.
    static readonly string[] ReadableNames = { "hydroponic_rack" };

    static void Import(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        bool readable = System.Array.IndexOf(ReadableNames, System.IO.Path.GetFileNameWithoutExtension(path)) >= 0;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        bool already = imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit)
                       && settings.spriteAlignment == (int)SpriteAlignment.Custom && imp.isReadable == readable;
        if (already) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = PixelsPerUnit;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Point;                                   // піксель-арт без розмиття
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.isReadable = readable;
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = Pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
