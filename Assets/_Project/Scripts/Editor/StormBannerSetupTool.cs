// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Меню Unity: Moon → Напис «Увага, буря».
/// Підключає PanelSettings гри (Settings/GamePanelSettings.asset) до World → ModuleLook → uiPanel:
/// з ними StormFx показує вгорі екрана напис «Сонячна буря через N год!» / «Всі в укриття!» (UI Toolkit, шрифт з кирилицею).
/// </summary>
public static class StormBannerSetupTool
{
    const string PanelPath = "Assets/_Project/Settings/GamePanelSettings.asset";

    [MenuItem("Moon/Напис «Увага, буря»")]
    public static void Setup()
    {
        var look = Object.FindAnyObjectByType<ModuleLook>();
        if (look == null) { Debug.LogError("StormBannerSetupTool: у сцені немає ModuleLook (об'єкт World)."); return; }
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (panel == null) { Debug.LogError("StormBannerSetupTool: немає " + PanelPath); return; }

        Undo.RecordObject(look, "Storm banner panel");
        look.uiPanel = panel;
        EditorUtility.SetDirty(look);
        EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
        Debug.Log("StormBannerSetupTool: готово! Напис про бурю підключено. Збережи сцену (Ctrl+S).");
    }
}
