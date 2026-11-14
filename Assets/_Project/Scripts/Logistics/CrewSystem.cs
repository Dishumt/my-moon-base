// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Завдання екіпажу: хто що робить і що це дає (лід → вода, зразки і лабораторія → наука).
/// Спеціаліст працює ×2, хворий (Sick) ×0.5, у тяжкому стані (Severe) — не працює.
/// Власник: роль 04.
/// </summary>
public class CrewSystem : ISimSystem
{
    readonly TasksData tasks;

    // підсумки дня для вечірнього звіту
    float waterToday, scienceToday;

    public CrewSystem() : this(TasksData.Load()) { }
    public CrewSystem(TasksData tasks) { this.tasks = tasks ?? new TasksData(); }

    /// <summary>Чи це завдання надворі.</summary>
    public bool IsOutsideTask(CrewTask task)
    {
        var t = tasks.Find(task);
        return t != null && t.outside;
    }

    /// <summary>Чи цей астронавт — спеціаліст для завдання.</summary>
    public bool IsSpecialist(Astronaut a, CrewTask task)
    {
        var t = tasks.Find(task);
        return t != null && !string.IsNullOrEmpty(t.specialist) && t.specialist == a.Role.ToString();
    }

    /// <summary>
    /// Дати завдання астронавту. targetId — id точки або номер слота.
    /// Повертає null, якщо все добре, або ключ тексту з причиною (для Loc.Get).
    /// Цей метод викликає Інтерфейс (05).
    /// </summary>
    public string Assign(Astronaut astronaut, CrewTask task, string targetId)
    {
        if (astronaut == null) return "error.not_found";
        if (astronaut.Role == CrewRole.Engineer) return "crew.err_you";
        if (astronaut.Health == HealthStatus.Severe && task != CrewTask.Medbay) return "crew.err_severe";
        if (astronaut.InsideOnly && IsOutsideTask(task)) return "crew.err_inside";
        if (astronaut.Task == task && astronaut.TaskTarget == (targetId ?? "")) return "crew.already";

        astronaut.Task = task;
        astronaut.TaskTarget = targetId ?? "";
        astronaut.AtTaskSite = false;
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>
    /// Те саме, але якщо targetId порожній — сама знайде місце:
    /// для льоду/зразків — точку з запасом, для інших — слот потрібного модуля.
    /// </summary>
    public string Assign(GameState s, Astronaut astronaut, CrewTask task, string targetId = null)
    {
        if (string.IsNullOrEmpty(targetId)) targetId = DefaultTarget(s, task);
        if (targetId == null) return "crew.err_no_place";
        if (task == CrewTask.Lab && s.Modules.Find(m => m.Type == ModuleType.Lab && m.BuildHoursLeft <= 0) == null)
            return "crew.err_no_lab";
        return Assign(astronaut, task, targetId);
    }

    /// <summary>Куди йти за замовчуванням. null — такого місця немає.</summary>
    public string DefaultTarget(GameState s, CrewTask task)
    {
        switch (task)
        {
            case CrewTask.MineIce: return PoiWithAmount(s, PoiType.Ice);
            case CrewTask.CollectSamples: return PoiWithAmount(s, PoiType.Sample);
            case CrewTask.Greenhouse: return Slot(s, ModuleType.Greenhouse);
            case CrewTask.Lab: return Slot(s, ModuleType.Lab);
            case CrewTask.Heal:
            case CrewTask.Medbay: return Slot(s, ModuleType.Medbay);
            case CrewTask.Shelter: return Slot(s, ModuleType.Shelter) ?? Slot(s, ModuleType.Habitat);
            case CrewTask.Rest: return Slot(s, ModuleType.Habitat) ?? "";
            default: return "";
        }
    }

    static string PoiWithAmount(GameState s, PoiType type)
    {
        var p = s.Pois.Find(x => x.Type == type && x.Amount > 0f);
        return p?.Id;
    }

    static string Slot(GameState s, ModuleType type)
    {
        var m = s.Modules.Find(x => x.Type == type && x.BuildHoursLeft <= 0);
        return m != null ? m.SlotId.ToString() : null;
    }

    /// <summary>Усіх, хто надворі чи має завдання надворі, — в укриття (для бурі). Повертає скільки людей відкликано.</summary>
    public int RecallAllInside(GameState s)
    {
        string target = Slot(s, ModuleType.Shelter) ?? Slot(s, ModuleType.Habitat) ?? "";
        int count = 0;
        foreach (var a in s.Crew)
        {
            if (a.Role == CrewRole.Engineer) continue; // інженером керує гравець
            if (a.Health == HealthStatus.Severe) continue; // і так лежить у медмодулі
            if (!IsOutsideTask(a.Task) && !a.IsOutside) continue;
            a.Task = CrewTask.Shelter;
            a.TaskTarget = target;
            a.AtTaskSite = false;
            count++;
        }
        GameEvents.RaiseStateChanged();
        return count;
    }

    /// <summary>
    /// Попросити заміну з резерву (та сама роль) у наступну поставку. Людина займає 150 кг.
    /// Повертає null або ключ тексту з причиною.
    /// </summary>
    public string RequestReplacement(GameState s, Astronaut astronaut)
    {
        if (astronaut == null) return "error.not_found";
        var sup = s.Supply;
        if (sup.Status == SupplyStatus.Launched || sup.Status == SupplyStatus.Returned)
            return "error.order_locked";
        var reserve = s.Reserve.Find(r => r.Role == astronaut.Role && !sup.Passengers.Contains(r.Name));
        if (reserve == null)
        {
            bool already = s.Reserve.Exists(r => r.Role == astronaut.Role && sup.Passengers.Contains(r.Name));
            return already ? "crew.err_already_requested" : "crew.err_no_reserve";
        }
        sup.Passengers.Add(reserve.Name);
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Скасувати заміну (прибрати людину з поставки).</summary>
    public string CancelReplacement(GameState s, string reserveName)
    {
        var sup = s.Supply;
        if (sup.Status == SupplyStatus.Launched || sup.Status == SupplyStatus.Returned)
            return "error.order_locked";
        sup.Passengers.Remove(reserveName);
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Швидкість роботи цієї людини на завданні, на день.</summary>
    public float RatePerDay(Astronaut a, CrewTask task)
    {
        var t = tasks.Find(task);
        if (t == null || t.perDay <= 0f) return 0f;
        if (a.Health == HealthStatus.Severe) return 0f;
        float rate = t.perDay;
        if (IsSpecialist(a, task)) rate *= tasks.specialistMultiplier;
        if (a.Health == HealthStatus.Sick) rate *= tasks.sickMultiplier;
        return rate;
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        foreach (var a in s.Crew)
        {
            if (!a.AtTaskSite) continue;
            float amount = RatePerDay(a, a.Task) * hours / 24f;
            if (amount <= 0f) continue;

            switch (a.Task)
            {
                case CrewTask.MineIce:
                {
                    var poi = s.FindPoi(a.TaskTarget);
                    if (poi == null || poi.Type != PoiType.Ice || poi.Amount <= 0f) break;
                    float got = Math.Min(amount, poi.Amount);
                    poi.Amount -= got;
                    s.Res.Water += got;
                    waterToday += got;
                    if (poi.Amount <= 0f) r.Add("report.ice_empty", 0f, poi.Id, true);
                    break;
                }
                case CrewTask.CollectSamples:
                {
                    var poi = s.FindPoi(a.TaskTarget);
                    if (poi == null || poi.Type != PoiType.Sample || poi.Amount <= 0f) break;
                    float got = Math.Min(amount, poi.Amount);
                    poi.Amount -= got;
                    s.Science += got;
                    scienceToday += got;
                    if (poi.Amount <= 0f) r.Add("report.samples_empty", 0f, poi.Id);
                    break;
                }
                case CrewTask.Lab:
                    if (tasks.labNeedsPower && s.Power.Lab <= 0) break;
                    s.Science += amount;
                    scienceToday += amount;
                    break;
            }
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        if (waterToday > 0f) r.Add("report.ice_mined", waterToday);
        if (scienceToday > 0f) r.Add("report.science_gained", scienceToday);
        waterToday = 0f;
        scienceToday = 0f;
    }
}
