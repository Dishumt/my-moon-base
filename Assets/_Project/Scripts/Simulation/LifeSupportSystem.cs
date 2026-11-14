// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Життєзабезпечення (роль 03): робить кисень з води й енергії, як система на МКС.
/// Щогодини:
///  1. бере з PowerSystem.Granted.LifeSupport, скільки блоків енергії реально отримало;
///  2. кожен блок робить кисень, але ТІЛЬКИ поки є вода (і витрачає її);
///  3. екіпаж дихає (кожен астронавт витрачає кисень);
///  4. кисень лишається між 0 і баком;
///  5. кисень або вода майже закінчилися → тривога в DayReport з причиною.
/// Числа беремо з balance.json (там вони «на день», у коді ділимо на 24).
/// Має стояти в порядку систем ПІСЛЯ PowerSystem.
/// </summary>
public class LifeSupportSystem : ISimSystem
{
    private const float Eps = 0.0001f;

    private readonly PowerSystem _power;
    private readonly LifeSupportBalance _b;
    private readonly float _startWater; // від стартової води рахуємо «20% води»

    // Скільки за день зроблено/витрачено, щоб пояснити це у вечірньому звіті.
    private float _oxygenMadeToday;
    private float _oxygenUsedByCrewToday;
    private float _waterUsedToday;

    // Щоб не писати ту саму тривогу 24 рази на день.
    private readonly HashSet<string> _reportedToday = new HashSet<string>();

    /// <summary>Гра: читає числа з Resources/Data/Balance/balance.json.</summary>
    public LifeSupportSystem(PowerSystem power)
        : this(power, DataLoader.Load<SimBalanceData>("Balance/balance")) { }

    /// <summary>Тести: числа передаємо руками.</summary>
    public LifeSupportSystem(PowerSystem power, SimBalanceData balance)
    {
        _power = power;
        _b = balance.lifeSupport;
        _startWater = balance.start != null ? balance.start.water : 0f;
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (hours <= 0f) return;

        float dayPart = hours / 24f; // яка частина доби минула
        float tank = _b.oxygenTankMax;

        float blocks = Math.Max(0, _power.Granted.LifeSupport);
        float crewUse = s.Crew.Count * _b.oxygenUsePerAstronautPerDay * dayPart;

        // Скільки блоків реально працює: обмежує вода.
        float waterPerBlock = _b.waterUsePerBlockPerDay * dayPart;
        float workingBlocks = blocks;
        if (waterPerBlock > Eps)
            workingBlocks = Math.Min(blocks, Math.Max(0f, s.Res.Water) / waterPerBlock);

        // Скільки кисню можна зробити: не більше, ніж влізе в бак (воду дарма не палимо).
        float oxygenPerBlock = _b.oxygenMadePerBlockPerDay * dayPart;
        float potential = workingBlocks * oxygenPerBlock;
        float room = Math.Max(0f, tank - (s.Res.Oxygen - crewUse));
        float made = Math.Min(potential, room);

        float usedBlocks = oxygenPerBlock > Eps ? made / oxygenPerBlock : 0f;
        float waterUsed = Math.Min(usedBlocks * waterPerBlock, Math.Max(0f, s.Res.Water));

        float oxygenBefore = s.Res.Oxygen;
        float available = oxygenBefore + made;               // що є для дихання
        float crewBreathed = Math.Min(crewUse, available);   // більше, ніж є, не вдихнеш
        float oxygenAfter = Clamp(available - crewBreathed, 0f, tank);

        s.Res.Oxygen = oxygenAfter;
        s.Res.Water = Math.Max(0f, s.Res.Water - waterUsed);

        _oxygenMadeToday += made;
        _oxygenUsedByCrewToday += crewBreathed;
        _waterUsedToday += waterUsed;

        CheckAlarms(s, r, blocks, workingBlocks, made, crewUse, crewBreathed < crewUse - Eps);
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // Вечірній звіт: що зроблено, що витрачено.
        if (_oxygenMadeToday > 0.01f) r.Add("report.oxygen_made", _oxygenMadeToday);
        if (_oxygenUsedByCrewToday > 0.01f) r.Add("report.oxygen_used", -_oxygenUsedByCrewToday);
        if (_waterUsedToday > 0.01f) r.Add("report.water_used_lifesupport", -_waterUsedToday);

        _oxygenMadeToday = 0f;
        _oxygenUsedByCrewToday = 0f;
        _waterUsedToday = 0f;
        _reportedToday.Clear();
    }

    // ───────────────────────── Усередині ─────────────────────────

    private void CheckAlarms(GameState s, DayReport r, float blocks, float workingBlocks,
                             float made, float crewUse, bool ranOut)
    {
        float oxygenLimit = _b.oxygenTankMax * _b.oxygenAlarmPercent / 100f;
        float waterLimit = _startWater * _b.waterAlarmPercent / 100f;

        if (s.Res.Oxygen < oxygenLimit)
        {
            string reason;
            if (blocks < Eps) reason = "no_power";                      // блоки не отримали енергії
            else if (workingBlocks < blocks - Eps) reason = "no_water"; // води не вистачає
            else if (made < crewUse - Eps) reason = "crew_uses_more";   // екіпаж дихає більше, ніж виходить зробити
            else reason = "refilling";                                  // вже росте, але ще мало

            float percent = s.Res.Oxygen / _b.oxygenTankMax * 100f;
            AddOnce(r, "report.oxygen_low." + reason, percent.ToString("0"), true);
        }

        if (ranOut)
            AddOnce(r, "report.oxygen_empty", "", true);

        if (s.Res.Water < waterLimit)
        {
            float percent = _startWater > Eps ? s.Res.Water / _startWater * 100f : 0f;
            AddOnce(r, "report.water_low", percent.ToString("0"), true);
        }
    }

    private void AddOnce(DayReport r, string key, string arg, bool important)
    {
        if (!_reportedToday.Add(key)) return;
        r.Add(key, 0f, arg, important);
    }

    private static float Clamp(float v, float min, float max)
    {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }
}
