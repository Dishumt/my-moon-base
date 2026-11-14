// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Один рядок прогресу цілі для інтерфейсу (роль 05).</summary>
public class GoalProgress
{
    public string Id;
    public string TitleKey; // ключ тексту цілі
    public string Module;   // для BuildModule: яка назва модуля (інакше порожньо)
    public float Current;   // скільки зараз
    public float Target;    // скільки треба
    public bool Done;       // чи вже виконано
}

/// <summary>
/// Розділи, перемога й поразка (роль 03).
///
/// Щогодини (Tick):
///  1. Поразка: кисень 0 або 2 доби без їжі → GameEvents.GameOver(ключ причини) і рядок у звіті.
///  2. Цілі: коли виконано ВСІ цілі розділу → GameEvents.ChapterCompleted(номер).
///     Виконана ціль запам'ятовується (вода піднялась і впала — ціль уже зарахована).
///
/// StartChapter завантажує chapter_0N.json, відкриває модулі (construction.IsUnlocked)
/// і за потреби ставить стартові ресурси та модулі.
/// Має стояти в порядку систем ОСТАННЬОЮ (після Power, LifeSupport, Food, Greenhouse, Construction).
/// </summary>
public class ChapterSystem : ISimSystem
{
    private const float Eps = 0.0001f;

    private readonly FoodSystem _food;
    private readonly ConstructionSystem _construction;
    private readonly DefeatBalance _defeat;
    private readonly Func<int, ChapterData> _loader;

    private ChapterData _chapter;
    private int _startDay;       // з якого дня почався розділ
    private bool _finished;      // розділ уже пройдено або програно: більше подій не шлемо
    private readonly HashSet<int> _doneGoals = new HashSet<int>();
    private readonly HashSet<ModuleType> _unlockedModules = new HashSet<ModuleType>();
    private readonly HashSet<string> _unlockedSystems = new HashSet<string>();

    /// <summary>Поточний розділ (дані з JSON) або null, якщо ще не стартував.</summary>
    public ChapterData Current { get { return _chapter; } }

    /// <summary>Чи розділ уже закінчився (пройдено чи програно).</summary>
    public bool IsFinished { get { return _finished; } }

    /// <summary>Гра: числа з balance.json, розділи з Resources/Data/Chapters/chapter_0N.json.</summary>
    public ChapterSystem(FoodSystem food, ConstructionSystem construction)
        : this(food, construction,
               DataLoader.Load<SimBalanceData>("Balance/balance"),
               n => DataLoader.Load<ChapterData>("Chapters/chapter_" + n.ToString("00"))) { }

    /// <summary>Тести: числа й розділи передаємо руками.</summary>
    public ChapterSystem(FoodSystem food, ConstructionSystem construction,
                         SimBalanceData balance, Func<int, ChapterData> chapterLoader)
    {
        _food = food;
        _construction = construction;
        _defeat = balance.defeat;
        _loader = chapterLoader;
    }

    // ───────────────────────── Початок розділу ─────────────────────────

    /// <summary>
    /// Почати розділ. applyStart = true для нової гри або повтору розділу:
    /// тоді ставляться стартові ресурси й модулі. Для переходу з розділу в розділ — false (база лишається).
    /// </summary>
    public void StartChapter(GameState s, int chapter, bool applyStart)
    {
        s.Chapter = chapter;
        _chapter = _loader(chapter);
        _startDay = s.Day;
        _finished = false;
        _doneGoals.Clear();
        _unlockedModules.Clear();
        _unlockedSystems.Clear();

        if (_chapter == null)
        {
            Debug.LogError("Розділ " + chapter + " не вдалося прочитати. Модулі не обмежені.");
            _construction.IsUnlocked = null;
            return;
        }

        if (_chapter.unlockedModules != null)
            foreach (string name in _chapter.unlockedModules)
            {
                ModuleType type;
                if (TryParseModule(name, out type)) _unlockedModules.Add(type);
                else Debug.LogError("chapter_" + chapter + ": невідомий модуль \"" + name + "\" в unlockedModules.");
            }

        if (_chapter.unlockedSystems != null)
            foreach (string name in _chapter.unlockedSystems)
                _unlockedSystems.Add(name);

        _construction.IsUnlocked = IsModuleUnlocked;

        if (applyStart) ApplyStart(s);
    }

    /// <summary>Чи можна замовляти цей модуль у поточному розділі.</summary>
    public bool IsModuleUnlocked(ModuleType type)
    {
        return _unlockedModules.Contains(type);
    }

    /// <summary>Чи працює ця система ("Greenhouse", "Radiation"...) у поточному розділі.</summary>
    public bool IsSystemUnlocked(string systemName)
    {
        return _unlockedSystems.Contains(systemName);
    }

    // ───────────────────────── Прогрес цілей ─────────────────────────

    /// <summary>Список цілей з прогресом для планшета.</summary>
    public List<GoalProgress> GetProgress(GameState s)
    {
        var list = new List<GoalProgress>();
        if (_chapter == null || _chapter.goals == null) return list;

        for (int i = 0; i < _chapter.goals.Length; i++)
        {
            ChapterGoalData g = _chapter.goals[i];
            float current = CurrentValue(s, g);
            list.Add(new GoalProgress
            {
                Id = g.id,
                TitleKey = g.titleKey,
                Module = g.module ?? "",
                Current = current,
                Target = g.target,
                Done = _doneGoals.Contains(i) || current + Eps >= g.target
            });
        }
        return list;
    }

    // ───────────────────────── ISimSystem ─────────────────────────

    public void Tick(GameState s, float hours, DayReport r)
    {
        if (hours <= 0f) return;

        if (_chapter == null) StartChapter(s, s.Chapter, false);
        if (_finished || _chapter == null) return;

        // 1. Поразка важливіша за перемогу.
        string lost = CheckDefeat(s);
        if (lost != null)
        {
            _finished = true;
            r.Add(lost, 0f, "", important: true); // звіт пояснює, що пішло не так
            GameEvents.RaiseGameOver(lost);
            return;
        }

        // 2. Цілі.
        if (_chapter.goals == null || _chapter.goals.Length == 0) return;

        bool allDone = true;
        for (int i = 0; i < _chapter.goals.Length; i++)
        {
            if (!_doneGoals.Contains(i))
            {
                ChapterGoalData g = _chapter.goals[i];
                if (CurrentValue(s, g) + Eps >= g.target) _doneGoals.Add(i);
            }
            if (!_doneGoals.Contains(i)) allDone = false;
        }

        if (allDone)
        {
            _finished = true;
            r.Add("report.chapter_completed", 0f, _chapter.chapter.ToString());
            GameEvents.RaiseChapterCompleted(_chapter.chapter);
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // Раз на день нічого окремо не рахуємо: усе перевіряється щогодини.
    }

    // ───────────────────────── Усередині ─────────────────────────

    // Повертає ключ причини поразки або null.
    private string CheckDefeat(GameState s)
    {
        if (s.Res.Oxygen <= _defeat.oxygenAtOrBelow + Eps)
            return "gameover.no_oxygen";

        if (s.Crew.Count > 0 && _food.HoursWithoutFood + Eps >= _defeat.daysWithoutFood * 24f)
            return "gameover.no_food";

        return null;
    }

    private float CurrentValue(GameState s, ChapterGoalData g)
    {
        switch (g.type)
        {
            case "SurviveDays":
                return Math.Max(0, s.Day - _startDay);

            case "BuildModule":
                ModuleType type;
                if (!TryParseModule(g.module, out type)) return 0f;
                int built = 0;
                foreach (ModuleState m in s.Modules)
                    if (m.Type == type && m.IsBuilt) built++;
                return built;

            case "WaterAtLeast":
                return s.Res.Water;

            case "ScienceAtLeast":
                return s.Science;

            default:
                Debug.LogError("Невідомий тип цілі \"" + g.type + "\" (ціль " + g.id + ").");
                return 0f;
        }
    }

    private void ApplyStart(GameState s)
    {
        ChapterStartData st = _chapter.start;
        if (st != null)
        {
            s.Res.Water = st.water;
            s.Res.Food = st.food;
            s.Res.Oxygen = st.oxygen;
            s.Res.Battery = st.battery;
            s.Res.Materials = st.materials;
            s.Res.MedKits = st.medKits;
            s.Science = st.science;
        }

        if (_chapter.startModules == null) return;

        foreach (ChapterStartModule sm in _chapter.startModules)
        {
            ModuleType type;
            if (!TryParseModule(sm.type, out type))
            {
                Debug.LogError("chapter_" + _chapter.chapter + ": невідомий модуль \"" + sm.type + "\" в startModules.");
                continue;
            }
            if (s.FindModule(sm.slot) != null) continue; // слот уже зайнятий

            s.Modules.Add(new ModuleState { Type = type, SlotId = sm.slot, BuildHoursLeft = 0f });
        }
    }

    private static bool TryParseModule(string name, out ModuleType type)
    {
        type = ModuleType.Habitat;
        if (string.IsNullOrEmpty(name)) return false;
        return Enum.TryParse(name, out type) && Enum.IsDefined(typeof(ModuleType), type);
    }
}
