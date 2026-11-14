// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Модуль бази, збудований або той, що будується.</summary>
[Serializable]
public class ModuleState
{
    public ModuleType Type;
    public int SlotId;               // номер квадратика сітки бази (BaseGrid), 0..BaseGrid.Count-1
    public float BuildHoursLeft;     // скільки годин роботи роботів лишилось; 0 = готово
    public bool RegolithShield;      // засипаний реголітом (менше радіації всередині)
    public bool EngineerNearby;      // пише Світ (01): інженер стоїть біля будови

    /// <summary>Чи модуль уже збудований.</summary>
    public bool IsBuilt => BuildHoursLeft <= 0f;
}
