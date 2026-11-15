// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Один запас (їжа, вода, кисень): скільки є, скільки веземо і на скільки днів вистачить.</summary>
public class SupplyDays
{
    public float Have;      // уже на базі
    public float Bring;     // у ракеті
    public float PerDay;    // скільки витрачається за день (0 = не витрачається)
    public float Cap;       // скільки вміщує склад/бак (0 = без меж). Більше — пропадає

    /// <summary>Скільки привеземо разом із тим, що є, без урахування бака.</summary>
    public float RawTotal => Have + Bring;
    /// <summary>Скільки реально буде на базі: більше за бак не влізе.</summary>
    public float Total => Cap > 0f ? System.Math.Min(RawTotal, System.Math.Max(Cap, Have)) : RawTotal;
    /// <summary>На скільки днів вистачить. float.PositiveInfinity — машини роблять досить.</summary>
    public float Days => PerDay <= 0.0001f ? float.PositiveInfinity : Total / PerDay;
    /// <summary>Скільки днів додає те, що в ракеті.</summary>
    public float BringDays => PerDay <= 0.0001f ? 0f : (Total - Have) / PerDay;
}

/// <summary>Ціль розділу «побудувати модуль» і чи вистачає для неї того, що вже є, плюс наборів у ракеті.</summary>
public class GoalCheck
{
    public ModuleType Module;
    public int Target;   // скільки треба
    public int Have;     // уже стоїть або будується на базі
    public int Packed;   // наборів у ракеті
    public bool Done => Have + Packed >= Target;
}

/// <summary>Підсумок для екрана запуску: дні запасів, цілі місії і порада «Центру керування».</summary>
public class MissionPlan
{
    public int Crew;
    public float DaysToNextShip;     // через скільки днів прилетить НАСТУПНИЙ корабель (після цього)
    public int ReserveDays;          // запас: стільки запізнюється корабель, що повернувся на Землю
    public int MissionDays;          // скільки днів треба протриматися = до наступного корабля + запас
    public SupplyDays Food = new SupplyDays();
    public SupplyDays Water = new SupplyDays();
    public SupplyDays Oxygen = new SupplyDays();
    public float OxygenTankMax;
    public List<GoalCheck> Goals = new List<GoalCheck>();

    public string TipKey;            // ключ поради
    public object[] TipArgs = new object[0];
    public bool TipIsWarning;        // червона/жовта порада чи зелена

    public float IceWaterPerDay;     // скільки води дає лід (добувають астронавти)

    public bool SurviveOk => Food.Days >= MissionDays && Water.Days >= MissionDays && Oxygen.Days >= MissionDays;
}

/// <summary>
/// Рахує, чи вистачить екіпажу того, що лежить на базі й у ракеті (роль 04).
/// Звичайний C#-клас: числа беремо з балансу ролі 03 (balance.json) і цілей розділу (chapter_NN.json), нічого не змінюємо.
/// </summary>
public static class MissionPlanner
{
    public static MissionPlan Build(GameState s, SupplySystem sup, bool firstMission, LaunchResult launch,
                                    SimBalanceData balance, ChapterData chapter, TasksData tasks = null)
    {
        var plan = new MissionPlan();
        var data = sup.Data;
        plan.Crew = firstMission ? data.firstMissionSeats : s.Crew.Count + s.Supply.Passengers.Count;
        plan.DaysToNextShip = DaysToNextShip(s, sup, firstMission);
        plan.ReserveDays = Math.Max(0, data.failedDelayDays);
        plan.MissionDays = Math.Max(1, (int)Math.Ceiling(plan.DaysToNextShip + plan.ReserveDays - 0.01f));

        // що в ракеті
        foreach (var line in s.Supply.Cargo)
        {
            var item = sup.Catalog.Find(line.itemId);
            if (item == null) continue;
            plan.Food.Bring += item.food * line.count;
            plan.Water.Bring += item.water * line.count;
            plan.Oxygen.Bring += item.oxygen * line.count;
        }
        plan.Food.Have = s.Res.Food;
        plan.Water.Have = s.Res.Water;
        plan.Oxygen.Have = s.Res.Oxygen;

        // скільки витрачається за день — за правилами ролі 03
        var food = balance != null ? balance.food : null;
        var life = balance != null ? balance.lifeSupport : null;
        var green = balance != null ? balance.greenhouse : null;
        plan.Food.PerDay = plan.Crew * (food != null ? food.portionsPerAstronautPerDay : 1f);
        float oxygenUse = plan.Crew * (life != null ? life.oxygenUsePerAstronautPerDay : 4f);
        float oxygenMade = s.Power.LifeSupport * (life != null ? life.oxygenMadePerBlockPerDay : 4f);
        plan.Oxygen.PerDay = Math.Max(0f, oxygenUse - oxygenMade);
        plan.Water.PerDay = s.Power.LifeSupport * (life != null ? life.waterUsePerBlockPerDay : 2f)
                          + s.Power.Greenhouse * (green != null ? green.waterUsePerBlockPerDay : 1f);
        plan.IceWaterPerDay = IceWaterPerDay(s, tasks);
        plan.Water.PerDay = Math.Max(0f, plan.Water.PerDay - plan.IceWaterPerDay);   // лід із кратерів зменшує, скільки треба везти
        plan.OxygenTankMax = life != null && life.oxygenTankMax > 0f ? life.oxygenTankMax : 100f;
        plan.Oxygen.Cap = plan.OxygenTankMax;   // кисень понад бак пропадає — і в днях його не рахуємо

        // цілі «побудувати модуль»
        if (chapter != null && chapter.goals != null)
        {
            foreach (var g in chapter.goals)
            {
                if (g == null || g.type != "BuildModule" || !Enum.TryParse(g.module, out ModuleType type)) continue;
                var check = new GoalCheck { Module = type, Target = (int)Math.Ceiling(g.target) };
                foreach (var m in s.Modules) if (m.Type == type) check.Have++;
                foreach (var line in s.Supply.Cargo)
                {
                    var item = sup.Catalog.Find(line.itemId);
                    if (item != null && item.TryGetModule(out var kit) && kit == type) check.Packed += line.count;
                }
                plan.Goals.Add(check);
            }
        }

        ChooseTip(plan, launch);
        return plan;
    }

    /// <summary>
    /// Через скільки днів сяде наступний корабель, тобто той, що прилетить ПІСЛЯ цього.
    /// Розклад той самий, що в SupplySystem: кожні supplyEveryDays днів о launchHour, плюс час польоту.
    /// Перша місія: наступний корабель — перша поставка. Поставка: цей вантаж має протримати до наступної.
    /// </summary>
    public static float DaysToNextShip(GameState s, SupplySystem sup, bool firstMission)
    {
        var data = sup.Data;
        int now = CommsSystem.AbsHour(s);
        int flight = sup.FlightHours(s);
        int from = firstMission ? now : sup.ExpectedArrivalAt(s);         // коли сяде цей корабель
        int nextLaunch = (from / 24 + sup.EveryDays(s)) * 24 + data.launchHour;
        if (!firstMission && nextLaunch < from) nextLaunch += 24;
        int nextArrival = nextLaunch + flight;
        return Math.Max(0f, (nextArrival - now) / 24f);
    }

    /// <summary>Одна найважливіша порада: спершу те, що не дасть долетіти, потім те, без чого не вижити, потім цілі.</summary>
    static void ChooseTip(MissionPlan p, LaunchResult launch)
    {
        int need = p.MissionDays;
        if (launch != null && launch.Light == LaunchLight.Red) { Tip(p, "tip.too_heavy", true); return; }
        if (p.Oxygen.Days < need) { Tip(p, "tip.oxygen_low", true, Whole(p.Oxygen.Days), need); return; }
        if (p.Water.Days < need) { Tip(p, "tip.water_low", true, Whole(p.Water.Days), need); return; }
        if (p.Food.Days < need) { Tip(p, "tip.food_low", true, Whole(p.Food.Days), need); return; }
        foreach (var g in p.Goals)
            if (!g.Done) { Tip(p, "tip.goal", true, LaunchText.Get("launch.module." + g.Module), g.Target - g.Have - g.Packed); return; }
        if (p.Oxygen.RawTotal > p.OxygenTankMax + 0.01f) { Tip(p, "tip.oxygen_full", true, p.OxygenTankMax.ToString("0")); return; }
        if (launch != null && launch.Light == LaunchLight.Yellow) { Tip(p, "tip.risky", true, launch.RiskPercent); return; }
        Tip(p, "tip.all_good", false);
    }

    static void Tip(MissionPlan p, string key, bool warning, params object[] args)
    {
        p.TipKey = key;
        p.TipArgs = args;
        p.TipIsWarning = warning;
    }

    /// <summary>Скільки води за день дають ті, хто зараз добуває лід (як у CrewSystem: спеціаліст ×2, хворий ×0.5).</summary>
    public static float IceWaterPerDay(GameState s, TasksData tasks)
    {
        var rate = tasks != null ? tasks.Find(CrewTask.MineIce) : null;
        if (rate == null) return 0f;
        float total = 0f;
        foreach (var a in s.Crew)
        {
            if (a.Task != CrewTask.MineIce) continue;
            float k = a.Role.ToString() == rate.specialist ? tasks.specialistMultiplier : 1f;
            if (a.Health == HealthStatus.Sick) k *= tasks.sickMultiplier;
            if (a.Health == HealthStatus.Severe) k = 0f;
            total += rate.perDay * k;
        }
        return total;
    }

    /// <summary>
    /// Найменша ракета, легша за вибрану, яка теж підняла б цей вантаж на зеленому світлі (з повним баком).
    /// null — вибрана ракета вже найменша з можливих. Для зірочки «нічого зайвого».
    /// </summary>
    public static RocketData SmallerRocketThatFits(GameState s, SupplySystem sup, bool firstMission)
    {
        var current = sup.GetRocket(s);
        if (current == null) return null;
        float cargo = LaunchCalculator.CargoMass(s.Supply.Cargo, sup.Catalog);
        int people = sup.People(s, firstMission);
        RocketData best = null;
        foreach (var r in sup.Rockets.items)
        {
            if (r == current || r.emptyMassKg >= current.emptyMassKg) continue;
            if (r.maxCargoKg > 0f && cargo > r.maxCargoKg) continue;          // не влізе в трюм
            if (!sup.RocketAllowed(s, r.id)) continue;
            var res = sup.Calculator.Evaluate(cargo, people, r.maxFuelKg, r);
            if (res.Light == LaunchLight.Green && (best == null || r.emptyMassKg < best.emptyMassKg)) best = r;
        }
        return best;
    }

    static string Whole(float days) => float.IsInfinity(days) ? "∞" : Math.Floor(days).ToString("0");
}
