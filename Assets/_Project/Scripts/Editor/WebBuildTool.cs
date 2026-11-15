// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Збірка для браузера (WebGL). Збирає гру в папку Builds/Web (папка в .gitignore)
/// за білд-профілем «Web - Desktop - Release» (те саме, що кнопка Build у File → Build Profiles).
/// Якщо профілю немає — збирає тільки сцену Main за налаштуваннями з Project Settings.
/// Те саме без вікна Unity (редактор має бути закритий):
///   Unity.exe -batchmode -nographics -projectPath &lt;repo&gt; -buildTarget WebGL -executeMethod WebBuildTool.Build
/// Готову папку Builds/Web можна запакувати в zip для itch.io (index.html має бути в корені архіву).
/// </summary>
public static class WebBuildTool
{
    const string MainScene = "Assets/_Project/Scenes/Main.unity";
    const string ProfilePath = "Assets/Settings/Build Profiles/Web - Desktop - Release.asset";
    public const string OutputFolder = "Builds/Web";

    [MenuItem("Moon/Збірка для браузера (WebGL)")]
    public static void Build()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
        BuildSummary summary;
        if (profile != null)
        {
            summary = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = OutputFolder,
                options = BuildOptions.None,
            }).summary;
        }
        else
        {
            summary = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None,
            }).summary;
        }
        Debug.Log("WebBuildTool: " + summary.result + ", " + (summary.totalSize / 1048576UL) + " MB, "
                  + summary.totalTime + ", errors: " + summary.totalErrors + ", folder: " + OutputFolder
                  + (profile != null ? " (profile)" : " (project settings)"));
        if (Application.isBatchMode) EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
