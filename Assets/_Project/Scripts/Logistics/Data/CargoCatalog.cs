// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Один вид вантажу з Launch/cargo_items.json.</summary>
[Serializable]
public class CargoItemData
{
    public string id;
    public string nameKey;      // ключ тексту для Loc.Get
    public float massKg;        // маса одного місця
    public float water;         // що дає базі після прибуття
    public float food;
    public float materials;
    public int medKits;
    public int solarPanelKits;
    public int batteryKits;
    public int crewSeats;       // місце для людини
}

/// <summary>Увесь файл cargo_items.json.</summary>
[Serializable]
public class CargoCatalog
{
    public List<CargoItemData> items = new List<CargoItemData>();

    public CargoItemData Find(string id) => items.Find(i => i.id == id);

    public static CargoCatalog Load() => DataLoader.Load<CargoCatalog>("Launch/cargo_items");
}

/// <summary>Рядок замовлення: який вантаж і скільки штук.</summary>
[Serializable]
public class CargoLine
{
    public string itemId;
    public int count;
}
