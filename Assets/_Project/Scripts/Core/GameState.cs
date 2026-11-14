// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// ЄДИНЕ місце, де зберігається стан гри. Ніхто не тримає в себе копій ресурсів:
/// системи змінюють цей об'єкт, Світ і Інтерфейс його читають.
/// [Serializable] означає, що Unity вміє перетворити клас у JSON (так працює збереження).
/// </summary>
[Serializable]
public class GameState
{
    public int Day = 1;              // номер дня, починаючи з 1
    public int Hour;                 // година 0..23
    public int Chapter = 1;          // номер розділу
    public float Science;            // очки науки

    public ResourceState Res = new ResourceState();
    public PowerPlan Power = new PowerPlan();

    public List<Astronaut> Crew = new List<Astronaut>();     // хто на базі
    public List<Astronaut> Reserve = new List<Astronaut>();  // хто чекає на Землі
    public List<ModuleState> Modules = new List<ModuleState>();
    public List<RobotState> Robots = new List<RobotState>();
    public List<PoiState> Pois = new List<PoiState>();

    public SupplyState Supply = new SupplyState();           // поля додає роль 04
    public CommsState Comms = new CommsState();              // поля додає роль 04
    public RadiationState Radiation = new RadiationState();  // поля додає роль 06

    /// <summary>Модуль у слоті або null, якщо слот порожній.</summary>
    public ModuleState FindModule(int slotId)
    {
        return Modules.Find(m => m.SlotId == slotId);
    }

    /// <summary>Точка за id або null.</summary>
    public PoiState FindPoi(string id)
    {
        return Pois.Find(p => p.Id == id);
    }

    /// <summary>Перший астронавт з такою роллю або null.</summary>
    public Astronaut FindCrew(CrewRole role)
    {
        return Crew.Find(a => a.Role == role);
    }
}
