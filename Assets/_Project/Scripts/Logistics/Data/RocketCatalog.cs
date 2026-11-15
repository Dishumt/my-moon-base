// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Одна ракета з Launch/rockets.json.</summary>
[Serializable]
public class RocketData
{
    public string id;
    public string nameKey;       // ключ назви
    public string hintKey;       // ключ підказки «чим вона особлива»
    public float emptyMassKg;    // маса порожньої ракети
    public float maxFuelKg;      // найбільший бак
    public int flightHours;      // скільки годин летить поставка
    public float scale = 1f;     // розмір картинки на екрані
    public float heightInCrew = 8f; // у скільки разів ракета вища за астронавта (пусковий майданчик)
    public float doorX = 0.1f;      // люк капсули: частка ширини картинки зліва
    public float doorY = 0.28f;     // люк капсули: частка висоти картинки зверху
}

/// <summary>Увесь файл rockets.json.</summary>
[Serializable]
public class RocketCatalog
{
    public string defaultId = "medium";
    public List<RocketData> items = new List<RocketData>();

    public RocketData Find(string id) => items.Find(r => r.id == id);

    /// <summary>Ракета за id; якщо такої немає — ракета за замовчуванням або перша. null — каталог порожній.</summary>
    public RocketData FindOrDefault(string id)
    {
        var r = string.IsNullOrEmpty(id) ? null : Find(id);
        if (r == null) r = Find(defaultId);
        if (r == null && items.Count > 0) r = items[0];
        return r;
    }

    public static RocketCatalog Load() => DataLoader.Load<RocketCatalog>("Launch/rockets");
}
