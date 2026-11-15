// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Тексти екрана запуску. Спершу питаємо Loc (головні файли ролі 05).
/// Якщо там ключа ще немає (Loc повертає "#ключ"), беремо з Launch/texts_launch_en.json / _uk.json (роль 04).
/// Так екран одразу показує нормальні слова, а 05 може будь-коли перенести ключі у свої файли.
/// </summary>
public static class LaunchText
{
    static Dictionary<string, string> en, uk;

    public static string Get(string key)
    {
        string text = Loc.Get(key);
        if (string.IsNullOrEmpty(key) || !text.StartsWith("#")) return text;

        EnsureLoaded();
        if (Loc.Language == Loc.Ukrainian && uk.TryGetValue(key, out var t)) return t;
        if (en.TryGetValue(key, out t)) return t;
        return text; // "#ключ" — видно, що текст забули
    }

    public static string Get(string key, params object[] args)
    {
        string text = Get(key);
        try { return string.Format(text, args); }
        catch (FormatException) { return text; }
    }

    static void EnsureLoaded()
    {
        if (en != null) return;
        en = LoadTable("Launch/texts_launch_en");
        uk = LoadTable("Launch/texts_launch_uk");
    }

    static Dictionary<string, string> LoadTable(string path)
    {
        var dict = new Dictionary<string, string>();
        if (!DataLoader.Exists(path)) return dict;
        var table = DataLoader.Load<TextTable>(path);
        if (table?.items == null) return dict;
        foreach (var e in table.items)
            if (!string.IsNullOrEmpty(e.key)) dict[e.key] = e.text;
        return dict;
    }

    /// <summary>Скинути перед кожним Play (як Loc).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { en = null; uk = null; }
}
