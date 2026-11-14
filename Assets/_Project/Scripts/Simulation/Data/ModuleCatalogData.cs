// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

// Класи для Resources/Data/Balance/modules.json.
// Читати: var m = DataLoader.Load<ModuleCatalogData>("Balance/modules");
// type — рядок з назвою значення enum ModuleType (наприклад "Greenhouse").
// Поля, що закінчуються на _note, — це пояснення для людей. У коді їх не використовуємо.

[Serializable]
public class ModuleCatalogData
{
    public string _ai;
    public string _note;
    public ModuleDef regolith;
    public List<ModuleDef> items;
}

[Serializable]
public class ModuleDef
{
    public string type;
    public string _note;
    public float buildRobotHours;
    public string buildRobotHours_note;
    public float materials;
    public string materials_note;
}
