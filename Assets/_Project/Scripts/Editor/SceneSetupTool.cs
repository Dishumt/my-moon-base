// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Меню Unity: Moon → Створити сцени (Main + Sandbox).
/// Створює Main.unity і Sandbox-сцени для ролей 02–06 з камерою, світлом, землею,
/// GameBootstrap і DebugPanel. Сцени, які вже існують, НЕ перезаписує.
/// Працює тільки в редакторі Unity (у гру не потрапляє).
/// </summary>
public static class SceneSetupTool
{
    const string ScenesDir = "Assets/_Project/Scenes";
    const string SandboxDir = ScenesDir + "/Sandbox";
    const string SettingsDir = "Assets/_Project/Settings";
    const string ThemePath = SettingsDir + "/GameRuntimeTheme.tss";
    const string PanelSettingsPath = SettingsDir + "/GamePanelSettings.asset";
    const string GroundMaterialPath = SettingsDir + "/MoonGround.mat";

    [MenuItem("Moon/Створити сцени (Main + Sandbox)")]
    public static void CreateAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(SandboxDir);
        Directory.CreateDirectory(SettingsDir);

        var panel = GetOrCreatePanelSettings();
        var ground = GetOrCreateGroundMaterial();

        string main = ScenesDir + "/Main.unity";
        CreateScene(main, isMain: true, withUI: true, panel, ground);
        var paths = new List<string> { main };

        // назва сцени, чи потрібен UIDocument
        var sandboxes = new (string name, bool ui)[]
        {
            ("02-Art", false), ("03-Simulation", false), ("04-Logistics", true), ("05-UI", true), ("06-Radiation", false),
        };
        foreach (var sb in sandboxes)
        {
            string path = SandboxDir + "/" + sb.name + ".unity";
            CreateScene(path, isMain: false, withUI: sb.ui, panel, ground);
            paths.Add(path);
        }

        // Build Settings: Main перша (з неї стартує веб-збірка)
        var buildScenes = new List<EditorBuildSettingsScene>();
        foreach (var p in paths) buildScenes.Add(new EditorBuildSettingsScene(p, true));
        EditorBuildSettings.scenes = buildScenes.ToArray();

        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(main);
        Debug.Log("SceneSetupTool: готово! Сцени лежать у " + ScenesDir);
    }

    static void CreateScene(string path, bool isMain, bool withUI, PanelSettings panel, Material groundMat)
    {
        if (File.Exists(path))
        {
            Debug.Log("SceneSetupTool: сцена вже є, пропускаю: " + path);
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Камера: перспективна, дивиться згори під кутом 45°
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var camera = cam.AddComponent<Camera>();
        camera.fieldOfView = 40f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.02f, 0.05f); // космос
        cam.AddComponent<AudioListener>();
        cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -12f), Quaternion.Euler(45f, 0f, 0f));

        // Сонце: низько над горизонтом, як на полюсі Місяця
        var sun = new GameObject("Sun");
        var light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(25f, -40f, 0f);

        // Земля: площина 50×50
        var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundGo.name = "Ground";
        groundGo.transform.localScale = new Vector3(5f, 1f, 5f);
        if (groundMat != null) groundGo.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

        if (isMain)
        {
            var engineer = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engineer.name = "Engineer (заглушка)";
            engineer.transform.position = new Vector3(0f, 0.5f, 0f);
        }

        // Гра: стан, годинник, системи + панель розробника
        var game = new GameObject("Game");
        game.AddComponent<GameBootstrap>();
        game.AddComponent<DebugPanel>();

        if (withUI)
        {
            var ui = new GameObject("UI");
            var doc = ui.AddComponent<UIDocument>();
            doc.panelSettings = panel;
        }

        EditorSceneManager.SaveScene(scene, path);
        Debug.Log("SceneSetupTool: створено " + path);
    }

    static PanelSettings GetOrCreatePanelSettings()
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null) return existing;

        // Стандартна тема UI Toolkit (така сама, як створює сам Unity)
        if (!File.Exists(ThemePath))
        {
            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\nVisualElement {}\n");
            AssetDatabase.ImportAsset(ThemePath);
        }

        var panel = ScriptableObject.CreateInstance<PanelSettings>();
        panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1280, 720);
        panel.match = 0.5f;
        AssetDatabase.CreateAsset(panel, PanelSettingsPath);
        return panel;
    }

    static Material GetOrCreateGroundMaterial()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return null;
        var mat = new Material(shader) { color = new Color(0.45f, 0.45f, 0.47f) }; // сірий реголіт
        AssetDatabase.CreateAsset(mat, GroundMaterialPath);
        return mat;
    }
}
