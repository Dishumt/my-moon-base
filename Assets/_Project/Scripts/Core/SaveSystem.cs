// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Збереження гри. У браузері (WebGL) не можна писати файли, тому зберігаємо
/// весь GameState як JSON-рядок у PlayerPrefs (це маленьке сховище браузера).
/// </summary>
public static class SaveSystem
{
    const string SaveKey = "moon_save_v1"; // змінимо на v2, якщо формат збереження зламається

    public static bool HasSave() => PlayerPrefs.HasKey(SaveKey);

    public static void Save(GameState state)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(state));
        PlayerPrefs.Save();
    }

    /// <summary>Завантажити гру або null, якщо збереження немає чи воно зіпсоване.</summary>
    public static GameState Load()
    {
        if (!HasSave()) return null;
        try { return JsonUtility.FromJson<GameState>(PlayerPrefs.GetString(SaveKey)); }
        catch (System.Exception e)
        {
            Debug.LogWarning("SaveSystem: не вдалося прочитати збереження: " + e.Message);
            return null;
        }
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
}
