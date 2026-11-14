// AI-assisted: written with Claude. See docs/AI_USE.md
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Веб-збірка (задача 4 техліда).
///  1. «Налаштувати Web» — ставить налаштування для itch.io і перемикає платформу на Web:
///     Gzip + Decompression Fallback (без нього itch.io часто показує порожню сторінку),
///     розмір вікна гри 1280×720.
///  2. «Зібрати й відкрити в браузері» — збирає гру в папку Builds/Web (вона в .gitignore)
///     і відкриває її в браузері через маленький локальний сервер Unity.
/// Працює тільки в редакторі Unity (у гру не потрапляє).
/// </summary>
public static class WebBuildTool
{
    const string BuildDir = "Builds/Web";

    // Сцени, які потрапляють у гру. Гра стартує з ПЕРШОЇ.
    // Sandbox-сцени сюди не додаємо: вони тільки для перевірки.
    // Коли з'явиться сцена меню, постав її першою.
    static readonly string[] Scenes =
    {
        "Assets/_Project/Scenes/Main.unity",
    };

    [MenuItem("Moon/Веб-збірка/1. Налаштувати Web (Gzip, 1280x720)")]
    public static void Configure()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("Веб-збірка",
                "У цій Unity не встановлено модуль Web.\n\n" +
                "Unity Hub → Installs → ⚙ біля Unity 6.6 → Add modules → ✔ Web Build Support → Install. " +
                "Потім перезапусти Unity і натисни цей пункт ще раз.",
                "Добре");
            return;
        }

        // Спершу налаштування, потім перемикання: так вони точно збережуться.
        ApplySettings();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            // Unity переімпортує всі асети під Web — це може тривати кілька хвилин.
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("WebBuildTool: не вдалося перемкнути платформу на Web. Дивись червоні рядки вище.");
                return;
            }
        }

        Debug.Log("WebBuildTool: готово! Платформа — Web, стиснення Gzip + Decompression Fallback, вікно 1280×720. " +
                  "Тепер Moon → Веб-збірка → 2. Зібрати й відкрити в браузері.");
    }

    [MenuItem("Moon/Веб-збірка/2. Зібрати й відкрити в браузері")]
    public static void Build()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUtility.DisplayDialog("Веб-збірка",
                "Спершу натисни Moon → Веб-збірка → 1. Налаштувати Web і дочекайся, поки Unity перемкне платформу.",
                "Добре");
            return;
        }

        // Незбережені зміни в сцені в збірку не потраплять — пропонуємо зберегти.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (var scene in Scenes)
        {
            if (!File.Exists(scene))
            {
                Debug.LogError("WebBuildTool: сцени " + scene + " немає. Виправ список Scenes у WebBuildTool.cs.");
                return;
            }
        }

        ApplySettings(); // раптом хтось змінив налаштування вручну

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = BuildDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.AutoRunPlayer, // після збірки Unity сам відкриє гру в браузері
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("WebBuildTool: збірка не вдалася (" + summary.result + "), помилок: " + summary.totalErrors +
                           ". Дивись червоні рядки вище в Console.");
            return;
        }

        double megabytes = summary.totalSize / (1024.0 * 1024.0);
        Debug.Log("WebBuildTool: зібрано за " + summary.totalTime.TotalMinutes.ToString("0.0") + " хв, розмір " +
                  megabytes.ToString("0.0") + " МБ. Папка: " + Path.GetFullPath(BuildDir) + "\n" +
                  "Для itch.io: зайди В папку Web, виділи все (Ctrl+A) → ПКМ → Надіслати → Стиснена ZIP-папка. " +
                  "index.html має лежати в zip зверху, а не всередині ще однієї папки.");

        EditorUtility.RevealInFinder(Path.Combine(BuildDir, "index.html"));
    }

    /// <summary>Налаштування Player Settings для Web, з якими гра працює на itch.io.</summary>
    static void ApplySettings()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true; // без цього на itch.io порожня сторінка
        PlayerSettings.WebGL.dataCaching = true;           // браузер пам'ятає гру, наступний запуск швидший
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        AssetDatabase.SaveAssets();
    }
}
