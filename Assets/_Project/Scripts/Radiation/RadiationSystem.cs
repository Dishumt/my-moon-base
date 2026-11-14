// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;

/// <summary>Зона дози: зелена (все добре), жовта (тільки всередині), червона (летить додому).</summary>
public enum DoseZone { Green, Yellow, Red }

/// <summary>
/// Радіація: доза щогодини і сонячні бурі зі справжніх подій NASA DONKI.
/// Власник: роль 06.
///
/// Щогодини (Tick):
///  1. Розклад бур (storm_schedule.json): за warningHours до бурі — GameEvents.StormWarning,
///     на старті — GameEvents.StormStarted, потім кілька годин бурі.
///  2. Кожен астронавт отримує дозу (у % від ліміту NASA на кар'єру):
///     звичайна година: базова (radlab_dose.json) × стиснення часу × (надворі 4 / всередині 1) × (реголіт 0.5);
///     буря: надворі — велика, в іншому модулі — середня, в укритті з енергією — мала.
///  3. Пороги: ≥50% → InsideOnly (тільки всередині), ≥100% → GoingHome (летить додому, це забирає 04).
/// Наприкінці дня (OnDayEnd) — у звіт: хто скільки отримав і чому.
/// </summary>
public class RadiationSystem : ISimSystem
{
    readonly RadiationBalance bal;
    readonly RadLabDose radlab;
    readonly DonkiStormList events;
    readonly StormSchedule schedule;
    readonly Func<CrewTask, bool> isOutsideTask;

    /// <summary>Гра: усі числа й дані з Resources/Data/Nasa/ і Launch/tasks.json.</summary>
    public RadiationSystem()
        : this(RadiationBalance.Load(), RadLabDose.Load(), DonkiStormList.Load(), StormSchedule.Load(), OutsideTasksFromData()) { }

    /// <summary>Тести: дані передаємо руками.</summary>
    public RadiationSystem(RadiationBalance balance, RadLabDose radlab, DonkiStormList events,
                           StormSchedule schedule, Func<CrewTask, bool> isOutsideTask = null)
    {
        bal = balance ?? new RadiationBalance();
        this.radlab = radlab ?? new RadLabDose();
        this.events = events ?? new DonkiStormList();
        this.schedule = schedule ?? new StormSchedule();
        this.isOutsideTask = isOutsideTask ?? (t => t == CrewTask.MineIce || t == CrewTask.CollectSamples);
    }

    static Func<CrewTask, bool> OutsideTasksFromData()
    {
        var tasks = TasksData.Load();
        return t => { var d = tasks.Find(t); return d != null && d.outside; };
    }

    public RadiationBalance Balance => bal;
    public RadLabDose RadLab => radlab;
    public DonkiStormList Events => events;
    public StormSchedule Schedule => schedule;

    /// <summary>Година від початку гри (День 1, 00:00 = 0).</summary>
    public static int AbsHour(GameState s) => (s.Day - 1) * 24 + s.Hour;

    /// <summary>Звичайна доза за годину всередині без реголіту, % ліміту (уже зі стисненням часу).</summary>
    public float BasePercentPerHour
    {
        get
        {
            if (radlab.careerLimit_mSv <= 0f) return 0f;
            return radlab.dailyDose_mSv / radlab.careerLimit_mSv * 100f / 24f * bal.timeCompression;
        }
    }

    // ───────────────────────── Для інтерфейсу (05) ─────────────────────────

    /// <summary>Зона дози астронавта: для смужки і кольору.</summary>
    public DoseZone Zone(Astronaut a)
    {
        if (a.Dose >= bal.redPercent) return DoseZone.Red;
        if (a.Dose >= bal.yellowPercent) return DoseZone.Yellow;
        return DoseZone.Green;
    }

    /// <summary>Подія DONKI за id (дата, клас, джерело — для попередження і екрана «Дані NASA»).</summary>
    public DonkiStorm FindEvent(string id) => events.Find(id);

    /// <summary>Поточна або наступна подія (під час попередження чи бурі), або null.</summary>
    public DonkiStorm CurrentEvent(GameState s) => s.Radiation.Phase == StormPhase.None ? null : events.Find(s.Radiation.StormId);

    /// <summary>Скільки блоків енергії потрібно укриттю для поточної/наступної бурі (0 — бурі немає).</summary>
    public int ShelterPowerNeeded(GameState s)
    {
        if (s.Radiation.Phase == StormPhase.None) return 0;
        return StormBalance.ByStrength(bal.storm.shelterPowerNeeded, s.Radiation.StormStrength);
    }

    /// <summary>Чи укриття збудоване й має достатньо енергії для поточної/наступної бурі.</summary>
    public bool IsShelterReady(GameState s)
    {
        return FindShelter(s) != null && s.Power.Shelter >= Math.Max(1, ShelterPowerNeeded(s));
    }

    /// <summary>
    /// Запустити бурю вручну (DebugPanel, «Demo for judges»): попередження зараз, буря через hoursUntil годин.
    /// Повертає null або ключ тексту з причиною.
    /// </summary>
    public string ForceStorm(GameState s, string eventId, int hoursUntil, DayReport r = null)
    {
        if (s.Radiation.Phase != StormPhase.None) return "error.storm_busy";
        var ev = events.Find(eventId);
        if (ev == null) return "error.not_found";
        BeginWarning(s, ev, AbsHour(s) + Math.Max(0, hoursUntil), r);
        return null;
    }

    // ───────────────────────── Щогодини ─────────────────────────

    public void Tick(GameState s, float hours, DayReport r)
    {
        var st = s.Radiation;
        SyncChapter(s);
        UpdateStorm(s, r);

        if (s.Chapter < bal.doseFromChapter) return;

        foreach (var a in s.Crew)
        {
            float perHour = HourlyDose(s, a, out DoseKind kind);
            float dose = perHour * hours;
            if (dose <= 0f) continue;

            a.Dose += dose;
            AddToday(st, a.Name, kind, dose);
            CheckThresholds(a, r);
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        foreach (var d in s.Radiation.Today)
        {
            if (d.Total <= 0f) continue;
            r.Add("report.dose_day", d.Total, d.Name);
            r.Add(ReasonKey(d), 0f, d.Name);
        }
        s.Radiation.Today.Clear();
    }

    // ───────────────────────── Доза ─────────────────────────

    /// <summary>Де людина отримала дозу (для звіту «чому»).</summary>
    public enum DoseKind { Outside, Inside, Shielded, StormOutside, StormModule, StormShelter }

    /// <summary>Доза за 1 годину для астронавта зараз, % ліміту.</summary>
    public float HourlyDose(GameState s, Astronaut a, out DoseKind kind)
    {
        ModuleState module = a.IsOutside ? null : s.FindModule(a.InsideSlot);
        bool shielded = module != null && module.IsBuilt && module.RegolithShield;

        if (s.Radiation.Phase == StormPhase.Active)
        {
            int strength = s.Radiation.StormStrength;
            if (a.IsOutside)
            {
                kind = DoseKind.StormOutside;
                return StormBalance.ByStrength(bal.storm.outsidePerHour, strength);
            }
            if (IsPoweredShelter(s, module))
            {
                kind = DoseKind.StormShelter;
                return StormBalance.ByStrength(bal.storm.shelterPerHour, strength);
            }
            kind = DoseKind.StormModule;
            float m = StormBalance.ByStrength(bal.storm.modulePerHour, strength);
            return shielded ? m * bal.regolithMultiplier : m;
        }

        float baseDose = BasePercentPerHour;
        if (a.IsOutside)
        {
            kind = DoseKind.Outside;
            return baseDose * bal.outsideMultiplier;
        }
        float inside = baseDose * bal.insideMultiplier;
        if (shielded)
        {
            kind = DoseKind.Shielded;
            return inside * bal.regolithMultiplier;
        }
        kind = DoseKind.Inside;
        return inside;
    }

    bool IsPoweredShelter(GameState s, ModuleState module)
    {
        if (module == null || module.Type != ModuleType.Shelter || !module.IsBuilt) return false;
        return s.Power.Shelter >= Math.Max(1, ShelterPowerNeeded(s));
    }

    static ModuleState FindShelter(GameState s) => s.Modules.Find(m => m.Type == ModuleType.Shelter && m.IsBuilt);

    void CheckThresholds(Astronaut a, DayReport r)
    {
        if (!a.InsideOnly && a.Dose >= bal.yellowPercent)
        {
            a.InsideOnly = true;
            r.Add("report.dose_yellow", a.Dose, a.Name, true);
            // Жовта зона: завдання надворі скасовується (інженером керує гравець сам)
            if (a.Role != CrewRole.Engineer && isOutsideTask(a.Task))
            {
                a.Task = CrewTask.None;
                a.TaskTarget = "";
                a.AtTaskSite = false;
            }
            GameEvents.RaiseStateChanged();
        }
        if (!a.GoingHome && a.Dose >= bal.redPercent)
        {
            a.GoingHome = true;
            r.Add("report.dose_red", a.Dose, a.Name, true);
            GameEvents.RaiseStateChanged();
        }
    }

    static void AddToday(RadiationState st, string name, DoseKind kind, float dose)
    {
        var d = st.Today.Find(x => x.Name == name);
        if (d == null)
        {
            d = new DoseToday { Name = name };
            st.Today.Add(d);
        }
        d.Total += dose;
        switch (kind)
        {
            case DoseKind.Outside: d.Outside += dose; break;
            case DoseKind.Inside: d.Inside += dose; break;
            case DoseKind.Shielded: d.Shielded += dose; break;
            case DoseKind.StormOutside: d.StormOutside += dose; break;
            case DoseKind.StormModule: d.StormModule += dose; break;
            case DoseKind.StormShelter: d.StormShelter += dose; break;
        }
    }

    /// <summary>Головна причина дози за день — ключ тексту для звіту.</summary>
    public static string ReasonKey(DoseToday d)
    {
        string key = "report.dose_why_inside";
        float max = d.Inside;
        Pick(d.Shielded, "report.dose_why_shielded", ref key, ref max);
        Pick(d.Outside, "report.dose_why_outside", ref key, ref max);
        Pick(d.StormShelter, "report.dose_why_storm_shelter", ref key, ref max);
        Pick(d.StormModule, "report.dose_why_storm_module", ref key, ref max);
        Pick(d.StormOutside, "report.dose_why_storm_outside", ref key, ref max);
        return key;
    }

    static void Pick(float value, string k, ref string key, ref float max)
    {
        if (value > max) { max = value; key = k; }
    }

    // ───────────────────────── Бурі ─────────────────────────

    /// <summary>Новий розділ — розклад бур починається спочатку.</summary>
    void SyncChapter(GameState s)
    {
        var st = s.Radiation;
        if (st.TrackedChapter == s.Chapter) return;
        bool chapterChanged = st.TrackedChapter != 0;
        st.TrackedChapter = s.Chapter;
        st.ChapterStartDay = s.Day;
        st.NextStormIndex = 0;
        if (chapterChanged)
        {
            // буря з минулого розділу (якщо була) закінчується
            st.Phase = StormPhase.None;
            st.StormId = "";
        }
    }

    void UpdateStorm(GameState s, DayReport r)
    {
        var st = s.Radiation;
        int now = AbsHour(s);

        // 1. Буря закінчилась
        if (st.Phase == StormPhase.Active && now >= st.StormEndAt)
        {
            r.Add("report.storm_ended", 0f, st.StormId);
            st.Phase = StormPhase.None;
            st.StormId = "";
            st.StormsSurvived++;
            GameEvents.RaiseStateChanged();
        }

        // 2. Час попередити про наступну бурю з розкладу
        if (st.Phase == StormPhase.None)
        {
            var list = schedule.For(s.Chapter);
            while (st.NextStormIndex < list.Count)
            {
                var next = list[st.NextStormIndex];
                int startAt = (st.ChapterStartDay + next.day - 1 - 1) * 24 + next.hour;
                if (now < startAt - bal.storm.warningHours) break; // ще рано

                st.NextStormIndex++;
                var ev = events.Find(next.eventId);
                if (ev == null)
                {
                    Debug.LogWarning("RadiationSystem: у donki_storms.json немає події " + next.eventId);
                    continue;
                }
                // якщо гравець «проспав» час (завантаження, перемотка) — все одно даємо попередження
                BeginWarning(s, ev, Math.Max(startAt, now + bal.storm.minWarningHours), r);
                break;
            }
        }

        // 3. Буря почалась
        if (st.Phase == StormPhase.Warning && now >= st.StormStartAt)
        {
            st.Phase = StormPhase.Active;
            st.StormEndAt = st.StormStartAt + StormBalance.ByStrength(bal.storm.durationHours, st.StormStrength);
            r.Add("report.storm_started", st.StormStrength, st.StormId, true);
            GameEvents.RaiseStormStarted(st.StormId);
            GameEvents.RaiseStateChanged();
        }
    }

    void BeginWarning(GameState s, DonkiStorm ev, int startAt, DayReport r)
    {
        var st = s.Radiation;
        st.Phase = StormPhase.Warning;
        st.StormId = ev.id;
        st.StormStrength = Math.Max(1, Math.Min(3, ev.strength));
        st.StormStartAt = startAt;
        st.StormEndAt = startAt;
        r?.Add("report.storm_warning", startAt - AbsHour(s), ev.id, true);
        GameEvents.RaiseStormWarning(ev.id);
        GameEvents.RaiseStateChanged();
    }
}
