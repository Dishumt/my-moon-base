// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Будівництво (роль 03). Гравець замовляє модуль, роботи його будують.
///
///  Order(state, type, slotId)            — замовити модуль у слот.
///  ShieldWithRegolith(state, slotId)     — окреме замовлення: засипати збудований модуль реголітом.
///
/// Обидва методи повертають null, якщо все гаразд, або КЛЮЧ ТЕКСТУ з причиною відмови
/// (наприклад "build.err.no_materials"); інтерфейс (роль 05) показує його через Loc.Get.
///
/// Щогодини (Tick) кожен робот, якому дали енергію (PowerSystem.Granted.Robots) і в якого є завдання,
/// зменшує BuildHoursLeft на 1 годину роботи (×2, якщо біля модуля EngineerNearby).
/// Два роботи на одному модулі працюють удвічі швидше. Коли модуль готовий, роботи йдуть до наступного замовлення.
/// Має стояти в порядку систем ПІСЛЯ PowerSystem.
/// </summary>
public class ConstructionSystem : ISimSystem
{
    private const float Eps = 0.0001f;

    private PowerSystem _power;
    private readonly BuildCostTable _costs;
    private readonly float _engineerMultiplier;
    private readonly float _powerPerWorkingRobot;

    // Скільки годин роботи лишилось до кінця засипання: слот → години.
    private readonly Dictionary<int, float> _shieldHoursLeft = new Dictionary<int, float>();

    private readonly HashSet<string> _reportedToday = new HashSet<string>();

    /// <summary>
    /// Які модулі відкриті в поточному розділі. Це заповнить ChapterSystem (Задача 5).
    /// Поки там null, дозволено все.
    /// </summary>
    public Func<ModuleType, bool> IsUnlocked;

    /// <summary>Саме так створює систему GameBootstrap: ціни з modules.json, PowerSystem знаходиться сам.</summary>
    public ConstructionSystem() : this((PowerSystem)null) { }

    /// <summary>Гра: ціни з modules.json, числа з balance.json.</summary>
    public ConstructionSystem(PowerSystem power)
        : this(power, BuildCostTable.Load()) { }

    /// <summary>Гра: читає числа з Resources/Data/Balance/balance.json, ціни передаються готовою таблицею.</summary>
    public ConstructionSystem(PowerSystem power, BuildCostTable costs)
        : this(power, DataLoader.Load<SimBalanceData>("Balance/balance"), costs) { }

    /// <summary>Тести: числа передаємо руками.</summary>
    public ConstructionSystem(PowerSystem power, SimBalanceData balance, BuildCostTable costs)
    {
        _power = power;
        _costs = costs;
        _engineerMultiplier = balance.construction.engineerNearbySpeedMultiplier;
        _powerPerWorkingRobot = balance.robots.powerBlocksWhenWorking;
    }

    // ───────────────────────── Замовлення ─────────────────────────

    /// <summary>Замовити модуль. null = прийнято; інакше ключ тексту з причиною.</summary>
    public string Order(GameState s, ModuleType type, int slotId)
    {
        if (IsUnlocked != null && !IsUnlocked(type)) return "build.err.locked";
        if (s.FindModule(slotId) != null) return "build.err.slot_busy";

        BuildCost cost;
        if (!_costs.TryGet(type, out cost)) return "build.err.unknown_module";

        if (s.Robots.Count == 0) return "build.err.no_robots";
        if (s.Res.Materials + Eps < cost.Materials) return "build.err.no_materials";

        s.Res.Materials -= cost.Materials;
        s.Modules.Add(new ModuleState
        {
            Type = type,
            SlotId = slotId,
            BuildHoursLeft = cost.BuildHours
        });

        AssignWork(s);
        return null;
    }

    /// <summary>Засипати збудований модуль реголітом. null = прийнято; інакше ключ тексту.</summary>
    public string ShieldWithRegolith(GameState s, int slotId)
    {
        ModuleState m = s.FindModule(slotId);
        if (m == null) return "build.err.no_module";
        if (!m.IsBuilt) return "build.err.not_built";
        if (m.RegolithShield) return "build.err.already_shielded";
        if (_shieldHoursLeft.ContainsKey(slotId)) return "build.err.shield_in_progress";
        if (s.Robots.Count == 0) return "build.err.no_robots";
        if (s.Res.Materials + Eps < _costs.Regolith.Materials) return "build.err.no_materials";

        s.Res.Materials -= _costs.Regolith.Materials;
        _shieldHoursLeft[slotId] = _costs.Regolith.BuildHours;

        AssignWork(s);
        return null;
    }


    // PowerSystem: або переданий у конструктор, або береться із GameBootstrap (він створює системи без аргументів).
    private PowerSystem Power
    {
        get
        {
            if (_power == null && GameBootstrap.Instance != null)
                _power = GameBootstrap.Instance.GetSystem<PowerSystem>();
            return _power;
        }
    }

    // ───────────────────────── ISimSystem ─────────────────────────

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (hours <= 0f) return;

        RestoreShieldOrders(s);
        FreeRobotsWithDeadTargets(s);
        AssignWork(s);

        // Скільки роботів можуть працювати: їм дали енергію (по блоку на кожного).
        int grantedRobots = Power != null ? Power.Granted.Robots : 0;
        int canWork = _powerPerWorkingRobot > Eps
            ? (int)Math.Floor(grantedRobots / _powerPerWorkingRobot + Eps)
            : int.MaxValue;

        var touchedModules = new List<ModuleState>();
        var touchedShields = new List<int>();
        bool someoneWithoutPower = false;

        foreach (RobotState robot in s.Robots)
        {
            bool build = robot.Task == RobotTask.Build;
            bool shield = robot.Task == RobotTask.Shield;
            if ((!build && !shield) || robot.TargetSlot < 0)
            {
                robot.Working = false;
                continue;
            }

            if (canWork <= 0)
            {
                robot.Working = false;
                someoneWithoutPower = true;
                continue;
            }

            canWork--;
            robot.Working = true;

            if (build)
            {
                ModuleState m = s.FindModule(robot.TargetSlot);
                float speed = m.EngineerNearby ? _engineerMultiplier : 1f;
                m.BuildHoursLeft = Math.Max(0f, m.BuildHoursLeft - hours * speed);
                if (!touchedModules.Contains(m)) touchedModules.Add(m);
            }
            else
            {
                _shieldHoursLeft[robot.TargetSlot] -= hours;
                if (!touchedShields.Contains(robot.TargetSlot)) touchedShields.Add(robot.TargetSlot);
            }
        }

        if (someoneWithoutPower)
            AddOnce(r, "report.robots_no_power", "", false);

        // Що закінчилось у цю годину.
        foreach (ModuleState m in touchedModules)
        {
            if (!m.IsBuilt) continue;
            r.Add("report.module_built", 0f, m.Type.ToString());
            FreeRobots(s, RobotTask.Build, m.SlotId);
        }

        foreach (int slot in touchedShields)
        {
            if (_shieldHoursLeft[slot] > Eps) continue;
            _shieldHoursLeft.Remove(slot);

            ModuleState m = s.FindModule(slot);
            if (m != null)
            {
                m.RegolithShield = true;
                r.Add("report.module_shielded", 0f, m.Type.ToString());
            }
            FreeRobots(s, RobotTask.Shield, slot);
        }

        AssignWork(s); // вільні роботи беруться за наступне замовлення
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        _reportedToday.Clear();
    }

    // ───────────────────────── Усередині ─────────────────────────

    /// <summary>Вільні роботи (Idle) йдуть до замовлення, де зараз найменше роботів.</summary>
    private void AssignWork(GameState s)
    {
        var pending = new List<KeyValuePair<RobotTask, int>>();
        foreach (ModuleState m in s.Modules)
            if (!m.IsBuilt) pending.Add(new KeyValuePair<RobotTask, int>(RobotTask.Build, m.SlotId));
        foreach (int slot in _shieldHoursLeft.Keys)
            pending.Add(new KeyValuePair<RobotTask, int>(RobotTask.Shield, slot));

        if (pending.Count == 0) return;

        foreach (RobotState robot in s.Robots)
        {
            if (robot.Task != RobotTask.Idle) continue;

            KeyValuePair<RobotTask, int> best = pending[0];
            int bestCount = int.MaxValue;
            foreach (var p in pending)
            {
                int count = CountRobots(s, p.Key, p.Value);
                if (count < bestCount)
                {
                    bestCount = count;
                    best = p;
                }
            }

            robot.Task = best.Key;
            robot.TargetSlot = best.Value;
        }
    }

    // Після завантаження гри роботи вже мають завдання «засипати», а годин у системі нема: ставимо повні.
    private void RestoreShieldOrders(GameState s)
    {
        foreach (RobotState robot in s.Robots)
        {
            if (robot.Task != RobotTask.Shield || robot.TargetSlot < 0) continue;
            if (_shieldHoursLeft.ContainsKey(robot.TargetSlot)) continue;

            ModuleState m = s.FindModule(robot.TargetSlot);
            if (m != null && m.IsBuilt && !m.RegolithShield)
                _shieldHoursLeft[robot.TargetSlot] = _costs.Regolith.BuildHours;
        }
    }

    // Робот стоїть біля модуля, якого вже нема або який уже готовий: відпускаємо.
    private void FreeRobotsWithDeadTargets(GameState s)
    {
        foreach (RobotState robot in s.Robots)
        {
            if (robot.Task == RobotTask.Idle) continue;

            ModuleState m = s.FindModule(robot.TargetSlot);
            bool dead = m == null
                || (robot.Task == RobotTask.Build && m.IsBuilt)
                || (robot.Task == RobotTask.Shield && !_shieldHoursLeft.ContainsKey(robot.TargetSlot));

            if (dead) Release(robot);
        }
    }

    private static void FreeRobots(GameState s, RobotTask task, int slot)
    {
        foreach (RobotState robot in s.Robots)
            if (robot.Task == task && robot.TargetSlot == slot)
                Release(robot);
    }

    private static void Release(RobotState robot)
    {
        robot.Task = RobotTask.Idle;
        robot.TargetSlot = -1;
        robot.Working = false;
    }

    private static int CountRobots(GameState s, RobotTask task, int slot)
    {
        int n = 0;
        foreach (RobotState robot in s.Robots)
            if (robot.Task == task && robot.TargetSlot == slot) n++;
        return n;
    }

    private void AddOnce(DayReport r, string key, string arg, bool important)
    {
        if (!_reportedToday.Add(key + "|" + arg)) return;
        r.Add(key, 0f, arg, important);
    }
}
