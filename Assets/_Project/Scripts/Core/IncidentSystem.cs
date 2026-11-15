// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Випадкові поломки на базі: «зламався унітаз», «пил на сонячній панелі», «мікрометеорит пробив модуль».
/// Поки поломка є, база щогодини втрачає ресурс (воду, енергію батареї чи кисень).
/// Полагодити може тільки гравець: дійти інженером до модуля з червоним знаком і тримати F (це робить IncidentView у Світі).
/// За ремонт — очки науки і справжній факт NASA.
/// Числа — у Resources/Data/Incidents/incidents.json, тексти — у texts_incidents_en/uk.json там само.
/// Звичайний C#-клас (не MonoBehaviour), тож працює з паузою годинника, як усі системи.
/// Поломка не зберігається в GameState: після «Продовжити» її просто немає (так простіше і не ламає збереження).
/// </summary>
public class IncidentSystem : ISimSystem
{
    /// <summary>Модулі-кімнати, куди може влучити мікрометеорит (module = "AnyRoom" у JSON).</summary>
    static readonly ModuleType[] Rooms = { ModuleType.Habitat, ModuleType.Greenhouse, ModuleType.Shelter, ModuleType.Medbay, ModuleType.Lab };

    readonly IncidentTable table;
    readonly Random rng = new Random();
    int hoursSinceLast;
    float lostToday;

    /// <summary>Поточна поломка або null.</summary>
    public IncidentData Active { get; private set; }
    /// <summary>Номер квадратика модуля, де поломка (-1, якщо поломки немає).</summary>
    public int ActiveSlot { get; private set; } = -1;
    /// <summary>Скільки ресурсу вже втрачено через поточну поломку.</summary>
    public float LostSoFar { get; private set; }
    /// <summary>Остання полагоджена поломка (для напису «Полагоджено!» і факту NASA).</summary>
    public IncidentData LastFixed { get; private set; }
    /// <summary>Скільки разів лагодили (Світ бачить, що число змінилось, і показує факт).</summary>
    public int FixCount { get; private set; }

    public IncidentSystem()
    {
        if (DataLoader.Exists(Path)) table = DataLoader.Load<IncidentTable>(Path);
        if (table != null) hoursSinceLast = table.minHoursBetween; // перша поломка може статися одразу після firstHour
    }

    const string Path = "Incidents/incidents";

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (table == null || table.items == null || table.items.Count == 0) return;
        if (!s.Supply.FirstMissionDone) return; // поки екіпаж летить, ламатися нічому

        if (Active != null)
        {
            var m = s.FindModule(ActiveSlot);
            if (m == null || !m.IsBuilt) { Clear(); return; } // модуль прибрали — поломки більше немає
            float lost = Take(s.Res, Active.resource, Active.lossPerHour * hours);
            LostSoFar += lost;
            lostToday += lost;
            return;
        }

        hoursSinceLast += (int)Math.Ceiling(hours);
        if (hoursSinceLast < table.minHoursBetween) return;
        if (s.Hour < table.dayStartHour || s.Hour >= table.dayEndHour) return; // уночі екіпаж спить — і ми теж
        if (s.Day == 1 && s.Hour < table.firstHour) return;                    // перші години — спокійно роздивитися
        if (rng.NextDouble() >= table.chancePerHour * hours) return;

        TryStart(s, r, null);
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        if (Active != null && lostToday > 0.05f)
            r.Add("incident." + Active.id + ".still", -lostToday, arg: UiNum(lostToday), important: true);
        lostToday = 0f;
    }

    /// <summary>
    /// Почати поломку зараз (для DebugPanel і демо). id = null — випадкова з тих, для яких є модуль.
    /// Повертає null, якщо почалась, або ключ тексту з причиною.
    /// </summary>
    public string TryStart(GameState s, DayReport r, string id)
    {
        if (table == null || table.items == null) return "incident.error_no_data";
        if (Active != null) return "incident.error_busy";

        var options = new List<IncidentData>();
        foreach (var item in table.items)
        {
            if (id != null && item.id != id) continue;
            if (FindSlot(s, item) >= 0) options.Add(item);
        }
        if (options.Count == 0) return "incident.error_no_module";

        Active = options[rng.Next(options.Count)];
        ActiveSlot = FindSlot(s, Active);
        LostSoFar = 0f;
        hoursSinceLast = 0;
        r?.Add("incident." + Active.id + ".report", 0f, important: true);
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Гравець полагодив (Світ кличе, коли інженер дотримав F до кінця). Дає науку.</summary>
    public void Fix(GameState s, DayReport r)
    {
        if (Active == null) return;
        s.Science += Active.science;
        r?.Add("incident.fixed_report", Active.science, arg: UiNum(Active.science));
        LastFixed = Active;
        FixCount++;
        Clear();
        GameEvents.RaiseStateChanged();
    }

    /// <summary>Скільки секунд тримати F для поточної поломки.</summary>
    public float FixSeconds => Active != null ? Math.Max(0.5f, Active.fixSeconds) : 1f;

    void Clear()
    {
        Active = null;
        ActiveSlot = -1;
        LostSoFar = 0f;
        hoursSinceLast = 0;
    }

    /// <summary>Квадратик збудованого модуля, де може статися ця поломка, або -1. Для «AnyRoom» — випадкова кімната.</summary>
    int FindSlot(GameState s, IncidentData item)
    {
        var slots = new List<int>();
        foreach (var m in s.Modules)
        {
            if (!m.IsBuilt) continue;
            bool fits = item.module == "AnyRoom"
                ? Array.IndexOf(Rooms, m.Type) >= 0
                : m.Type.ToString() == item.module;
            if (fits) slots.Add(m.SlotId);
        }
        return slots.Count == 0 ? -1 : slots[rng.Next(slots.Count)];
    }

    /// <summary>Забрати ресурс (не нижче нуля). Повертає, скільки реально забрали.</summary>
    static float Take(ResourceState res, string resource, float amount)
    {
        if (amount <= 0f) return 0f;
        float before, after;
        switch (resource)
        {
            case "Water": before = res.Water; res.Water = Math.Max(0f, res.Water - amount); after = res.Water; break;
            case "Oxygen": before = res.Oxygen; res.Oxygen = Math.Max(0f, res.Oxygen - amount); after = res.Oxygen; break;
            case "Battery": before = res.Battery; res.Battery = Math.Max(0f, res.Battery - amount); after = res.Battery; break;
            case "Food": before = res.Food; res.Food = Math.Max(0f, res.Food - amount); after = res.Food; break;
            default: return 0f;
        }
        return before - after;
    }

    static string UiNum(float v) => Math.Abs(v - Math.Round(v)) < 0.05f ? Math.Round(v).ToString("0") : v.ToString("0.0");
}

/// <summary>Файл Resources/Data/Incidents/incidents.json.</summary>
[Serializable]
public class IncidentTable
{
    public float chancePerHour = 0.12f;  // шанс поломки за годину (коли можна)
    public int minHoursBetween = 10;     // скільки годин спокою після поломки
    public int firstHour = 10;           // у день 1 — не раніше цієї години
    public int dayStartHour = 7;         // поломки бувають тільки вдень
    public int dayEndHour = 20;
    public List<IncidentData> items = new List<IncidentData>();
}

/// <summary>Одна поломка. Тексти: incident.&lt;id&gt;.title / .hint / .fact / .report / .still.</summary>
[Serializable]
public class IncidentData
{
    public string id;          // toilet, panel_dust, meteor
    public string module;      // тип модуля з ModuleType (Habitat, Solar...) або AnyRoom
    public string resource;    // Water, Oxygen, Battery, Food
    public float lossPerHour;  // скільки ресурсу втрачається щогодини, поки не полагодиш
    public float fixSeconds;   // скільки реальних секунд тримати F
    public float science;      // нагорода за ремонт
}
