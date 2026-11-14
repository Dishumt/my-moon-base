// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Стартові поля з Chapters/chapter_NN.json, які читає ядро, щоб створити GameState.
/// Файли розділів належать ролі 03: вона додає туди свої поля (цілі тощо) і читає їх своїм класом.
/// Назви полів тут = ключі в JSON.
/// </summary>
[Serializable]
public class ChapterStartData
{
    public int chapter = 1;
    public string titleKey;                  // ключ тексту з назвою розділу
    public int startHour = 6;                // о котрій годині починається перший день
    public ResourceState resources = new ResourceState();
    public PowerPlan power = new PowerPlan();
    public List<CrewStart> crew = new List<CrewStart>();
    public List<CrewStart> reserve = new List<CrewStart>();
    public int robots = 2;
    public List<PoiStart> pois = new List<PoiStart>();
}

[Serializable]
public class CrewStart
{
    public string name;
    public string role;  // текстом: "Engineer", "Medic", "Botanist", "Geologist"
}

[Serializable]
public class PoiStart
{
    public string id;
    public string type;  // текстом: "Ice", "Sample", "Fact", "LandingPad"
    public float amount;
}
