// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Каталог модулів з Resources/Data/Balance/modules.json (власник: роль 03).
/// Читаємо так: var m = DataLoader.Load&lt;ModuleCatalog&gt;("Balance/modules");
/// Ціна модуля: m.Find(ModuleType.Greenhouse).materials
/// </summary>
[Serializable]
public class ModuleCatalog
{
    public ModuleCost regolith = new ModuleCost();
    public List<ModuleCost> items = new List<ModuleCost>();

    /// <summary>Знайти ціну модуля за типом. Повертає null, якщо такого модуля немає в JSON.</summary>
    public ModuleCost Find(ModuleType type)
    {
        foreach (var item in items)
        {
            if (item.TryGetType(out var t) && t == type) return item;
        }
        return null;
    }
}

/// <summary>Скільки коштує одне будівництво: години роботи робота і будматеріали.</summary>
[Serializable]
public class ModuleCost
{
    /// <summary>
    /// Назва з enum ModuleType текстом, наприклад "Greenhouse".
    /// Це string, бо JsonUtility не вміє читати enum із тексту — перетворюємо в TryGetType.
    /// </summary>
    public string type;
    public float buildRobotHours;
    public int materials;

    /// <summary>Перетворити текст "type" у ModuleType. false, якщо назву написано з помилкою.</summary>
    public bool TryGetType(out ModuleType result)
    {
        return Enum.TryParse(type, out result);
    }
}
