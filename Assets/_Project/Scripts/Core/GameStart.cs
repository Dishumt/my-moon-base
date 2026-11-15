// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Як почати гру після перезавантаження сцени.</summary>
public enum StartMode { Chapter, Continue, Demo }

/// <summary>
/// «Запит старту»: меню, вікно поразки чи перемоги кажуть, з чого почати, і перезавантажують сцену.
/// GameBootstrap читає запит в Awake і створює GameState з потрібного розділу, збереження чи демо.
///   GameStart.Begin(StartMode.Chapter, 2);   // розділ 2 з нуля
///   GameStart.Begin(StartMode.Continue);     // продовжити збереження
///   GameStart.BackToMenu();                  // головне меню
/// Поки запиту немає (перший запуск гри), показується головне меню.
/// Запит живе між перезавантаженнями сцени, а скидається тільки на новому Play.
/// </summary>
public static class GameStart
{
    const string UnlockedKey = "chapter_unlocked";
    public const int LastChapter = 3;

    /// <summary>Чи є запит (тоді меню не показуємо).</summary>
    public static bool HasRequest { get; private set; }
    public static StartMode Mode { get; private set; } = StartMode.Chapter;
    public static int Chapter { get; private set; } = 1;

    /// <summary>Записати запит і перезавантажити сцену.</summary>
    public static void Begin(StartMode mode, int chapter = 1)
    {
        HasRequest = true;
        Mode = mode;
        Chapter = Mathf.Clamp(chapter, 1, LastChapter);
        ReloadScene();
    }

    /// <summary>Записати запит без перезавантаження: гра в сцені вже створена саме так (кнопка «Грати»).</summary>
    public static void MarkStarted(int chapter)
    {
        HasRequest = true;
        Mode = StartMode.Chapter;
        Chapter = chapter;
    }

    /// <summary>Назад у головне меню.</summary>
    public static void BackToMenu()
    {
        HasRequest = false;
        ReloadScene();
    }

    static void ReloadScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // Sandbox-сцени не в списку збірки: у редакторі вантажимо їх за шляхом
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
    }

    // ---------- відкриті розділи (пам'ятаємо в PlayerPrefs) ----------

    /// <summary>Найбільший розділ, який уже можна вибрати в меню.</summary>
    public static int UnlockedChapter => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, LastChapter);

    /// <summary>Розділ пройдено: відкрити наступний.</summary>
    public static void UnlockAfter(int completedChapter)
    {
        int next = Mathf.Clamp(completedChapter + 1, 1, LastChapter);
        if (next <= UnlockedChapter) return;
        PlayerPrefs.SetInt(UnlockedKey, next);
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        HasRequest = false;
        Mode = StartMode.Chapter;
        Chapter = 1;
    }
}

/// <summary>Числа для «Demo for judges»: Resources/Data/Demo/demo.json.</summary>
[System.Serializable]
public class DemoData
{
    public int chapter = 3;
    public int startHour = 8;
    public float realSecondsPerDay = 240f;   // демо йде швидше за звичайну гру
    public ResourceState resources = new ResourceState();
    public string stormEventId = "";         // справжня подія NASA DONKI з Nasa/donki_storms.json
    public int stormInHours = 6;             // через скільки ігрових годин почнеться буря
}
