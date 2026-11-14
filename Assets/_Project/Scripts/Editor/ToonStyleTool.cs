// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Мультяшний стиль модулів (toon).
/// Створює два матеріали з шейдером Moon/Toon (3 рівні кольору + темний контур) і ставить їх у ModuleLook:
///  - Settings/ToonSolid.mat — для стін, підлоги, оболонок, меблів, обладнання;
///  - Settings/ToonGlass.mat — прозорий: скло теплиці й купола, риштування, оболонка, поки зникає.
/// Повернути як було: перетягни ModuleWall і ModuleRoof назад у World → ModuleLook.
/// </summary>
public static class ToonStyleTool
{
    const string SolidPath = "Assets/_Project/Settings/ToonSolid.mat";
    const string GlassPath = "Assets/_Project/Settings/ToonGlass.mat";

    [MenuItem("Moon/Мультяшний стиль модулів (toon)")]
    public static void Apply()
    {
        var shader = Shader.Find("Moon/Toon");
        if (shader == null) { Debug.LogError("ToonStyleTool: шейдер Moon/Toon не знайдено (Settings/Shaders/MoonToon.shader)."); return; }

        var look = Object.FindAnyObjectByType<ModuleLook>();
        if (look == null) { Debug.LogError("ToonStyleTool: у сцені немає ModuleLook (об'єкт World)."); return; }

        float sun = SunBrightness();
        var solid = GetOrCreate(SolidPath, shader);
        solid.SetFloat("_LightRef", sun);
        solid.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        solid.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        solid.SetFloat("_ZWrite", 1f);
        solid.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        solid.SetOverrideTag("RenderType", "Opaque");
        EditorUtility.SetDirty(solid);

        var glass = GetOrCreate(GlassPath, shader);
        glass.SetFloat("_LightRef", sun);
        glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0f);
        glass.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        glass.SetOverrideTag("RenderType", "Transparent");
        EditorUtility.SetDirty(glass);

        Undo.RecordObject(look, "Toon materials");
        look.wallMaterial = solid;
        look.roofMaterial = glass;
        EditorUtility.SetDirty(look);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
        Debug.Log("ToonStyleTool: готово! Модулі тепер мультяшні. Збережи сцену (Ctrl+S).");
    }

    static Material GetOrCreate(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) { mat.shader = shader; return mat; }
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Яскравість сонця (Sun) у сцені — щоб шейдер знав, що таке «звичайне» світло.</summary>
    static float SunBrightness()
    {
        foreach (var l in Object.FindObjectsByType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            Color c = l.color.linear * l.intensity;
            return Mathf.Max(0.05f, c.r * 0.299f + c.g * 0.587f + c.b * 0.114f);
        }
        return 1.2f;
    }
}
