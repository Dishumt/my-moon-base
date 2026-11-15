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
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Import(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            look.furniture.Add(new ModuleLook.FurnitureSprite { name = System.IO.Path.GetFileNameWithoutExtension(path), sprite = sprite });
        }

        EditorUtility.SetDirty(look);
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
        Debug.Log("FurnitureSetupTool: готово! Меблів: " + look.furniture.Count + ". Збережи сцену (Ctrl+S).");
    }

    static void Import(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        bool already = imp.textureType == TextureImporterType.Sprite && Mathf.Approximately(imp.spritePixelsPerUnit, PixelsPerUnit)
                       && settings.spriteAlignment == (int)SpriteAlignment.Custom;
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
        settings.spritePivot = Pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();
    }
}
