// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Меню Unity: Moon → Інтерфейс гри в Main.
/// Ставить у відкриту сцену весь інтерфейс гри (кожен — окремий об'єкт з UIDocument і своїм скриптом):
///   Tablet       — планшет (Tablet.uxml, Sort Order 0) + TabletController;
///   LaunchScreen — екран запуску ролі 04 (LaunchScreen.uxml, Sort Order 10); картинки ракет, вантажів і скафандрів
///                  копіюються з компонента в сцені 04-Earth; Open On Start вимкнено — його відкриває меню старту;
///   HUD          — запаси, час, швидкість, цілі (Hud.uxml, Sort Order -10) + HudController;
///   Windows      — вечірній звіт, перемога, поразка (Windows.uxml, Sort Order 20) + GameWindows;
///   Start Menu   — головне меню і «Demo for judges» (StartMenu.uxml, Sort Order 30) + StartMenu.
/// Якщо об'єкт уже є, лише оновлює його налаштування (нічого не дублює). Потім треба зберегти сцену (Ctrl+S).
/// </summary>
public static class GameUiSetupTool
{
    const string PanelPath = "Assets/_Project/Settings/GamePanelSettings.asset";
    const string TabletUxml = "Assets/_Project/UI/Tablet/Tablet.uxml";
    const string LaunchUxml = "Assets/_Project/UI/Launch/LaunchScreen.uxml";
    const string HudUxml = "Assets/_Project/UI/Game/Hud.uxml";
    const string WindowsUxml = "Assets/_Project/UI/Game/Windows.uxml";
    const string MenuUxml = "Assets/_Project/UI/Game/StartMenu.uxml";
    const string EarthScene = "Assets/_Project/Scenes/Sandbox/Earth/04-Earth.unity";

    [MenuItem("Moon/Інтерфейс гри в Main")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("GameUiSetupTool: спершу зупини Play.");
            return;
        }
        var scene = SceneManager.GetActiveScene();
        if (Object.FindAnyObjectByType<GameBootstrap>() == null)
        {
            Debug.LogError("GameUiSetupTool: у сцені немає GameBootstrap (об'єкт Game). Відкрий сцену Main.");
            return;
        }
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (panel == null)
        {
            Debug.LogError("GameUiSetupTool: немає " + PanelPath);
            return;
        }

        var log = new StringBuilder();
        Ensure<TabletController>("Tablet", TabletUxml, 0f, panel, log, out _);
        var launch = Ensure<LaunchScreen>("LaunchScreen", LaunchUxml, 10f, panel, log, out bool launchIsNew);
        if (launch != null)
        {
            if (launchIsNew) CopyLaunchPictures(launch, log);
            SetBool(launch, "openOnStart", false);
        }
        Ensure<HudController>("HUD", HudUxml, -10f, panel, log, out _);
        Ensure<GameWindows>("Windows", WindowsUxml, 20f, panel, log, out _);
        Ensure<StartMenu>("Start Menu", MenuUxml, 30f, panel, log, out _);

        // напис «Увага, буря» теж бере ці PanelSettings
        if (Object.FindAnyObjectByType<ModuleLook>() != null) StormBannerSetupTool.Setup();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("GameUiSetupTool: готово! Збережи сцену (Ctrl+S) і натисни Play.\n" + log);
    }

    /// <summary>Знайти об'єкт зі скриптом T або створити його: UIDocument (PanelSettings, uxml, Sort Order) + T.</summary>
    static T Ensure<T>(string name, string uxmlPath, float sortOrder, PanelSettings panel, StringBuilder log, out bool created)
        where T : MonoBehaviour
    {
        created = false;
        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
        if (uxml == null)
        {
            log.AppendLine("  ✗ " + name + ": немає файлу " + uxmlPath);
            return null;
        }

        var component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        GameObject go;
        if (component != null)
        {
            go = component.gameObject;
            log.AppendLine("  • " + name + ": уже є (" + go.name + "), оновив налаштування");
        }
        else
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Game UI " + name);
            created = true;
            log.AppendLine("  + " + name + ": створено");
        }

        var doc = go.GetComponent<UIDocument>();
        if (doc == null) doc = Undo.AddComponent<UIDocument>(go);
        Undo.RecordObject(doc, "Game UI " + name);
        doc.panelSettings = panel;
        doc.visualTreeAsset = uxml;
        doc.sortingOrder = sortOrder;
        EditorUtility.SetDirty(doc);

        if (component == null) component = Undo.AddComponent<T>(go);
        EditorUtility.SetDirty(component);
        return component;
    }

    /// <summary>Картинки ракет, іконки вантажів і скафандри лежать у полях компонента в сцені 04-Earth — копіюємо їх.</summary>
    static void CopyLaunchPictures(LaunchScreen target, StringBuilder log)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EarthScene) == null)
        {
            log.AppendLine("  ! немає сцени " + EarthScene + ": екран запуску буде без картинок ракет");
            return;
        }
        var main = SceneManager.GetActiveScene();
        var earth = EditorSceneManager.OpenScene(EarthScene, OpenSceneMode.Additive);
        try
        {
            LaunchScreen source = null;
            foreach (var root in earth.GetRootGameObjects())
            {
                source = root.GetComponentInChildren<LaunchScreen>(true);
                if (source != null) break;
            }
            if (source != null && ComponentUtility.CopyComponent(source) && ComponentUtility.PasteComponentValues(target))
                log.AppendLine("  + LaunchScreen: картинки ракет, вантажів і скафандрів скопійовано з 04-Earth");
            else
                log.AppendLine("  ! у 04-Earth не знайшов Launch Screen: картинки ракет не скопійовано");
        }
        finally
        {
            EditorSceneManager.CloseScene(earth, true);
            SceneManager.SetActiveScene(main);
        }
    }

    static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedProperties();
    }
}
