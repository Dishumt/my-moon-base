// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Один астронавт. Хто що пише в ці поля — див. CLAUDE.md, розділ «Архітектура».</summary>
[Serializable]
public class Astronaut
{
    public string Name;
    public CrewRole Role;

    public CrewTask Task = CrewTask.None;
    public string TaskTarget = "";   // id точки (PoiState.Id) або номер слота модуля, куди йти
    public bool AtTaskSite;          // пише Світ (01): чи дійшов до місця завдання

    public int InsideSlot = -1;      // пише Світ (01): у якому модулі людина; -1 = надворі

    public float Satiety = 100f;     // ситість 0..100 (рахує 03)
    public HealthStatus Health = HealthStatus.Ok; // здоров'я (рахує 06)
    public float Dose;               // доза радіації, % від ліміту (рахує 06)
    public bool InsideOnly;          // жовта зона дози: тільки завдання всередині
    public bool GoingHome;           // досяг ліміту: летить додому з найближчою поставкою

    /// <summary>Чи людина зараз надворі.</summary>
    public bool IsOutside => InsideSlot < 0;
}
