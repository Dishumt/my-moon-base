// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Читає JSON-файли з Assets/_Project/Resources/Data/.
/// Приклад: var b = DataLoader.Load&lt;CoreBalance&gt;("Balance/balance");
/// Шлях пишемо БЕЗ ".json". Клас T має бути [Serializable], поля — public, і назви полів = ключі в JSON.
/// </summary>
public static class DataLoader
{
    const string Root = "Data/";

    /// <summary>Завантажити файл. Якщо файлу немає — пише помилку в Console і повертає порожній об'єкт.</summary>
    public static T Load<T>(string path) where T : class, new()
    {
        var asset = Resources.Load<TextAsset>(Root + path);
        if (asset == null)
        {
            Debug.LogError("DataLoader: не знайдено файл Resources/" + Root + path + ".json");
            return new T();
        }

        var data = JsonUtility.FromJson<T>(asset.text);
        return data ?? new T();
    }

    /// <summary>Чи є такий файл.</summary>
    public static bool Exists(string path)
    {
        return Resources.Load<TextAsset>(Root + path) != null;
    }
}
