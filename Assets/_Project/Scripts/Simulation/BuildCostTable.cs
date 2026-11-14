// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Скільки коштує модуль: годин роботи робота і будматеріалів.</summary>
public struct BuildCost
{
    public float BuildHours; // скільки годин роботи робота треба
    public float Materials;  // скільки будматеріалів знімається при замовленні

    public BuildCost(float buildHours, float materials)
    {
        BuildHours = buildHours;
        Materials = materials;
    }
}

/// <summary>
/// Таблиця цін: тип модуля → ціна. Наповнюється числами з Resources/Data/Balance/modules.json,
/// а ConstructionSystem тільки читає. У тестах таблицю можна зібрати руками (Set) або з тексту JSON (FromJson).
/// </summary>
public class BuildCostTable
{
    private readonly Dictionary<ModuleType, BuildCost> _costs = new Dictionary<ModuleType, BuildCost>();

    /// <summary>Ціна засипання модуля реголітом (стартово 12 годин / 0 матеріалів).</summary>
    public BuildCost Regolith = new BuildCost(12f, 0f);

    public void Set(ModuleType type, float buildHours, float materials)
    {
        _costs[type] = new BuildCost(buildHours, materials);
    }

    public bool TryGet(ModuleType type, out BuildCost cost)
    {
        return _costs.TryGetValue(type, out cost);
    }

    // ───────────────────────── Читання modules.json ─────────────────────────
    // Ці два класи повторюють форму файлу (поля _note та інші зайві поля пропускаються).

    [Serializable]
    public class JsonItem
    {
        public string type;           // назва з enum ModuleType, наприклад "Solar"
        public float buildRobotHours; // годин роботи робота
        public float materials;       // будматеріали
    }

    [Serializable]
    public class JsonFile
    {
        public JsonItem regolith;
        public JsonItem[] items;
    }

    /// <summary>Гра: читає Resources/Data/Balance/modules.json.</summary>
    public static BuildCostTable Load()
    {
        return FromFile(DataLoader.Load<JsonFile>("Balance/modules"));
    }

    /// <summary>Тести: те саме, але з готового тексту JSON.</summary>
    public static BuildCostTable FromJson(string json)
    {
        return FromFile(JsonUtility.FromJson<JsonFile>(json));
    }

    private static BuildCostTable FromFile(JsonFile file)
    {
        var table = new BuildCostTable();

        if (file == null)
        {
            Debug.LogError("modules.json не вдалося прочитати: ціни модулів порожні.");
            return table;
        }

        if (file.regolith != null)
            table.Regolith = new BuildCost(file.regolith.buildRobotHours, file.regolith.materials);

        if (file.items == null) return table;

        foreach (JsonItem item in file.items)
        {
            ModuleType type;
            if (Enum.TryParse(item.type, out type) && Enum.IsDefined(typeof(ModuleType), type))
                table.Set(type, item.buildRobotHours, item.materials);
            else
                Debug.LogError("modules.json: невідомий тип модуля \"" + item.type
                    + "\". Має бути назва з enum ModuleType (Habitat, Solar, Battery, ...).");
        }

        return table;
    }
}
