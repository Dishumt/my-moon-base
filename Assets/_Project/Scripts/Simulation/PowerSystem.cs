// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Енергія бази (роль 03). Кожну ігрову годину:
///  1. рахує, скільки блоків енергії дають готові сонячні панелі (у тіні менше);
///  2. дивиться, скільки блоків роздав гравець (PowerPlan);
///  3. зайві блоки заряджають батарею, а якщо блоків бракує, їх додає батарея;
///  4. якщо не вистачає й батареї, системи отримують менше, ніж у плані.
///
/// Скільки блоків КОЖНА система реально отримала цієї години, лежить у полі Granted.
/// Інші системи (кисень, теплиця, роботи...) мають читати саме Granted, а не s.Power:
///   var power = GameBootstrap.Instance.GetSystem&lt;PowerSystem&gt;();
///   int blocks = power.Granted.LifeSupport;
/// Звичайний C#-клас (не MonoBehaviour), тому його легко перевіряти тестами.
/// </summary>
public class PowerSystem : ISimSystem
{
    private readonly PowerBalance _b;

    // Малий запас на неточність дробових чисел (float), щоб 3.0000001 не вважалося «менше за 3».
    private const float Eps = 0.0001f;

    // Номер години, з якої починається тінь, рахується від кінця доби:
    // якщо тінь 6 годин, то вона з 18:00 до 23:59.

    /// <summary>Скільки блоків кожна система отримала в останній Tick. Перераховується щогодини.</summary>
    public PowerPlan Granted = new PowerPlan();

    /// <summary>Скільки блоків дали панелі в останній Tick (для DebugPanel і Світу).</summary>
    public float LastSupplyBlocks { get; private set; }

    /// <summary>Чи була тінь в останній Tick (Світ може затемнювати модулі).</summary>
    public bool LastInShade { get; private set; }

    // Щоб не писати в звіт те саме 24 рази на день: кожну нестачу пишемо раз на день.
    private readonly HashSet<string> _reportedToday = new HashSet<string>();
    private bool _dayStarted;
    private float _batteryAtDayStart;

    /// <summary>Гра: читає числа з Resources/Data/Balance/balance.json.</summary>
    public PowerSystem() : this(DataLoader.Load<SimBalanceData>("Balance/balance")) { }

    /// <summary>Тести: числа передаємо руками.</summary>
    public PowerSystem(SimBalanceData balance)
    {
        _b = balance.power;
    }

    // ───────────────────────── Публічні допоміжні методи ─────────────────────────

    /// <summary>Чи в цю годину доби (0..23) хребет у тіні.</summary>
    public bool IsShade(int hour)
    {
        float shadeStart = 24f - _b.shadeHoursPerDay;
        return hour >= shadeStart;
    }

    /// <summary>Скільки блоків дають усі готові панелі в цю годину доби.</summary>
    public float SupplyBlocks(GameState s, int hour)
    {
        float perPanel = IsShade(hour) ? _b.panelBlocksInShade : _b.panelBlocks;
        return CountBuilt(s, ModuleType.Solar) * perPanel;
    }

    /// <summary>
    /// Максимум заряду батареї, у блоко-годинах.
    /// Одна батарея вже є на базі, і кожен збудований модуль «батарея» додає ще одну.
    /// </summary>
    public float BatteryCapacity(GameState s)
    {
        return _b.batteryCapacityBlockHours * (1 + CountBuilt(s, ModuleType.Battery));
    }

    // ───────────────────────── ISimSystem ─────────────────────────

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (hours <= 0f) return;

        if (!_dayStarted)
        {
            _batteryAtDayStart = s.Res.Battery;
            _dayStarted = true;
        }

        PowerPlan plan = s.Power;
        int demand = Pos(plan.LifeSupport) + Pos(plan.Greenhouse) + Pos(plan.Comms)
                   + Pos(plan.Shelter) + Pos(plan.Lab) + Pos(plan.Medbay) + Pos(plan.Robots);

        bool shade = IsShade(s.Hour);
        float supply = SupplyBlocks(s, s.Hour);
        float capacity = BatteryCapacity(s);
        float battery = Clamp(s.Res.Battery, 0f, capacity);

        int grantable; // скільки блоків реально можна роздати цієї години

        if (supply + Eps >= demand)
        {
            // Енергії досить: усе, що просили, віддаємо, а зайве йде в батарею.
            grantable = demand;
            battery += (supply - demand) * hours;
        }
        else
        {
            // Панелей не вистачає: додаємо блоки з батареї, скільки там є.
            float fromBattery = battery / hours;
            grantable = (int)Math.Floor(supply + fromBattery + Eps);
            if (grantable > demand) grantable = demand;
            if (grantable < 0) grantable = 0;

            float draw = Math.Max(0f, grantable - supply) * hours;
            battery -= draw;
        }

        s.Res.Battery = Clamp(battery, 0f, capacity);
        LastSupplyBlocks = supply;
        LastInShade = shade;

        Distribute(plan, grantable, r);
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        if (_dayStarted)
        {
            float delta = s.Res.Battery - _batteryAtDayStart;
            if (Math.Abs(delta) > 0.01f)
                r.Add("report.battery_change", delta);
        }

        _reportedToday.Clear();
        _dayStarted = false;
    }

    // ───────────────────────── Усередині ─────────────────────────

    /// <summary>
    /// Роздає grantable блоків за пріоритетом. Спершу те, без чого не вижити:
    /// життєзабезпечення, укриття, медмодуль, зв'язок, потім теплиця, роботи, лабораторія.
    /// </summary>
    private void Distribute(PowerPlan plan, int grantable, DayReport r)
    {
        int left = grantable;

        Granted.LifeSupport = Take(plan.LifeSupport, ref left);
        Granted.Shelter = Take(plan.Shelter, ref left);
        Granted.Medbay = Take(plan.Medbay, ref left);
        Granted.Comms = Take(plan.Comms, ref left);
        Granted.Greenhouse = Take(plan.Greenhouse, ref left);
        Granted.Robots = Take(plan.Robots, ref left);
        Granted.Lab = Take(plan.Lab, ref left);

        ReportShortage("life_support", plan.LifeSupport, Granted.LifeSupport, r);
        ReportShortage("shelter", plan.Shelter, Granted.Shelter, r);
        ReportShortage("medbay", plan.Medbay, Granted.Medbay, r);
        ReportShortage("comms", plan.Comms, Granted.Comms, r);
        ReportShortage("greenhouse", plan.Greenhouse, Granted.Greenhouse, r);
        ReportShortage("robots", plan.Robots, Granted.Robots, r);
        ReportShortage("lab", plan.Lab, Granted.Lab, r);
    }

    private static int Take(int wanted, ref int left)
    {
        int give = Math.Min(Pos(wanted), left);
        left -= give;
        return give;
    }

    private void ReportShortage(string system, int wanted, int got, DayReport r)
    {
        if (got >= Pos(wanted)) return;
        string key = "report.power_short." + system;
        if (!_reportedToday.Add(key)) return; // сьогодні вже писали
        r.Add(key, got - wanted, important: true);
    }

    private static int CountBuilt(GameState s, ModuleType type)
    {
        int n = 0;
        foreach (ModuleState m in s.Modules)
            if (m.Type == type && m.IsBuilt) n++;
        return n;
    }

    private static int Pos(int v) { return v < 0 ? 0 : v; }

    private static float Clamp(float v, float min, float max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
