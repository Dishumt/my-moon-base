// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Теплиця (роль 03). Їй потрібні енергія І вода:
///  - блоки енергії беремо з PowerSystem.Granted.Greenhouse;
///  - кожен блок п'є воду; якщо води мало, росте лише та частина блоків, на яку її вистачає;
///  - кожен працюючий блок дає +1 росту за день; якщо ботанік працює в теплиці (Task == Greenhouse), ріст ×1.5;
///  - кожні 3 дні (OnDayEnd) урожай = ріст × 2 порції йде в Res.Food, і ріст починається спочатку.
/// Щоб теплиця росла, на базі має бути хоч одна ГОТОВА теплиця (модуль Greenhouse).
/// Має стояти в порядку систем ПІСЛЯ PowerSystem і LifeSupportSystem (життєзабезпечення першим бере воду).
/// </summary>
public class GreenhouseSystem : ISimSystem
{
    private const float Eps = 0.0001f;

    private PowerSystem _power;
    private readonly GreenhouseBalance _b;

    /// <summary>Скільки росту накопичилося від останнього врожаю.</summary>
    public float Growth { get; private set; }

    /// <summary>Скільки днів минуло від останнього врожаю.</summary>
    public int DaysSinceHarvest { get; private set; }

    private readonly HashSet<string> _reportedToday = new HashSet<string>();

    /// <summary>Саме так створює систему GameBootstrap: PowerSystem знаходиться сам.</summary>
    public GreenhouseSystem() : this((PowerSystem)null) { }

    /// <summary>Гра: читає числа з Resources/Data/Balance/balance.json.</summary>
    public GreenhouseSystem(PowerSystem power)
        : this(power, DataLoader.Load<SimBalanceData>("Balance/balance")) { }

    /// <summary>Тести: числа передаємо руками.</summary>
    public GreenhouseSystem(PowerSystem power, SimBalanceData balance)
    {
        _power = power;
        _b = balance.greenhouse;
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

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (hours <= 0f) return;

        float blocks = Power != null ? Math.Max(0, Power.Granted.Greenhouse) : 0f;
        if (blocks < Eps) return;

        // Без готової теплиці енергія пропадає марно.
        if (!HasBuiltGreenhouse(s))
        {
            AddOnce(r, "report.greenhouse_no_module", false);
            return;
        }

        float dayPart = hours / 24f;

        // Скільки блоків реально росте: обмежує вода.
        float waterPerBlock = _b.waterUsePerBlockPerDay * dayPart;
        float working = blocks;
        if (waterPerBlock > Eps)
            working = Math.Min(blocks, Math.Max(0f, s.Res.Water) / waterPerBlock);

        if (working < blocks - Eps)
            AddOnce(r, "report.greenhouse_no_water", true);

        s.Res.Water = Math.Max(0f, s.Res.Water - working * waterPerBlock);

        float multiplier = BotanistWorks(s) ? _b.botanistMultiplier : 1f;
        Growth += working * _b.growthPerBlockPerDay * dayPart * multiplier;
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        DaysSinceHarvest++;

        if (DaysSinceHarvest >= _b.harvestEveryDays - Eps)
        {
            float portions = Growth * _b.portionsPerGrowth;
            if (portions > 0f)
            {
                s.Res.Food += portions;
                r.Add("report.harvest", portions);
            }
            Growth = 0f;
            DaysSinceHarvest = 0;
        }

        _reportedToday.Clear();
    }

    // ───────────────────────── Усередині ─────────────────────────

    private static bool HasBuiltGreenhouse(GameState s)
    {
        return s.Modules.Exists(m => m.Type == ModuleType.Greenhouse && m.IsBuilt);
    }

    private static bool BotanistWorks(GameState s)
    {
        return s.Crew.Exists(a => a.Role == CrewRole.Botanist && a.Task == CrewTask.Greenhouse);
    }

    private void AddOnce(DayReport r, string key, bool important)
    {
        if (!_reportedToday.Add(key)) return;
        r.Add(key, 0f, "", important);
    }
}
