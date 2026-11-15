// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Тексти гри двома мовами. Ніякого тексту прямо в коді — тільки ключі:
///   label.text = Loc.Get("hud.oxygen");
///   Loc.Get("report.sick", astronaut.Name)  → підставляє ім'я замість {0}
/// Файли: Resources/Data/Text/texts_en.json і texts_uk.json, формат {"items":[{"key":"...","text":"..."}]}.
/// Ще читає таблицю ролі 04: Resources/Data/Launch/texts_launch_en.json / _uk.json (той самий формат).
/// Якщо ключ є в обох, перемагає основна таблиця Text/.
/// Якщо ключа немає — повертає "#ключ", щоб одразу було видно, що забули додати текст.
/// </summary>
public static class Loc
{
    public const string English = "en";
    public const string Ukrainian = "uk";
    const string PrefsKey = "language";

    // Dictionary тут можна: це не JSON, а внутрішній швидкий пошук
    static Dictionary<string, string> current;
    static Dictionary<string, string> fallback;  // англійська — якщо в українській бракує ключа

    /// <summary>Поточна мова: "en" або "uk".</summary>
    public static string Language { get; private set; }

    /// <summary>Подія: мову змінили — перемалюй тексти.</summary>
    public static event Action LanguageChanged;

    /// <summary>Отримати текст за ключем.</summary>
    public static string Get(string key)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(key)) return "";
        if (current.TryGetValue(key, out var text)) return text;
        if (fallback.TryGetValue(key, out text)) return text;
        return "#" + key;
    }

    /// <summary>Текст з підстановкою: "{0} is sick" + "Alex" = "Alex is sick".</summary>
    public static string Get(string key, params object[] args)
    {
        var text = Get(key);
        try { return string.Format(text, args); }
        catch (FormatException) { return text; }
    }

    /// <summary>Змінити мову і запам'ятати вибір.</summary>
    public static void SetLanguage(string language)
    {
        Language = language == Ukrainian ? Ukrainian : English;
        PlayerPrefs.SetString(PrefsKey, Language);
        PlayerPrefs.Save();
        current = LoadTable(Language);
        LanguageChanged?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (current != null) return;
        fallback = LoadTable(English);
        Language = PlayerPrefs.GetString(PrefsKey, English);
        current = Language == English ? fallback : LoadTable(Language);
    }

    static Dictionary<string, string> LoadTable(string language)
    {
        var dict = new Dictionary<string, string>();
        // Role 04 keeps its texts (launch screen, supplies, its reports and errors) in its own table.
        // Load it first: the main table wins when both have the same key.
        AddEntries(dict, "Launch/texts_launch_" + language, optional: true);
        AddEntries(dict, "Incidents/texts_incidents_" + language, optional: true); // випадкові поломки (техлід)
        AddEntries(dict, "IcePlant/texts_iceplant_" + language, optional: true);   // бур і переробник льоду (техлід)
        AddEntries(dict, "Text/texts_" + language, optional: false);
        return dict;
    }

    static void AddEntries(Dictionary<string, string> dict, string path, bool optional)
    {
        if (optional && !DataLoader.Exists(path)) return;
        var table = DataLoader.Load<TextTable>(path);
        if (table?.items == null) return;
        foreach (var entry in table.items)
        {
            if (!string.IsNullOrEmpty(entry.key)) dict[entry.key] = entry.text;
        }
    }

    /// <summary>Скинути завантажені тексти (Unity викликає перед кожним Play).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { current = null; fallback = null; LanguageChanged = null; }
}

/// <summary>Формат файлу текстів.</summary>
[Serializable]
public class TextTable
{
    public List<TextEntry> items = new List<TextEntry>();
}

[Serializable]
public class TextEntry
{
    public string key;
    public string text;
}
