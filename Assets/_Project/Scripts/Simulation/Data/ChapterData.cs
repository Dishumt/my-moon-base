// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

// Класи для Resources/Data/Chapters/chapter_01..03.json.
// Читати: var c = DataLoader.Load<ChapterData>("Chapters/chapter_01");
// Поля, що закінчуються на _note, — пояснення для людей. У коді їх не використовуємо.

[Serializable]
public class ChapterData
{
    public int chapter;
    public string titleKey;            // ключ тексту назви розділу
    public string descriptionKey;      // ключ тексту опису розділу
    public ChapterStartValues start;     // стартові ресурси
    public ChapterStartModule[] startModules; // модулі, що стоять на початку
    public string[] unlockedModules;   // назви з enum ModuleType, які можна замовляти
    public string[] unlockedSystems;   // назви систем, що працюють у розділі
    public ChapterGoalData[] goals;    // цілі: розділ пройдено, коли виконано всі
}

[Serializable]
public class ChapterStartValues
{
    public float water;
    public float food;
    public float oxygen;
    public float battery;
    public float materials;
    public int medKits;
    public float science;
}

[Serializable]
public class ChapterStartModule
{
    public string type; // назва з enum ModuleType
    public int slot;    // номер слота
}

[Serializable]
public class ChapterGoalData
{
    public string id;       // коротке ім'я цілі
    public string type;     // SurviveDays, BuildModule, WaterAtLeast, ScienceAtLeast
    public float target;    // скільки треба (днів, штук, води, очок науки)
    public string module;   // для BuildModule: назва з enum ModuleType
    public string titleKey; // ключ тексту для інтерфейсу
}
