// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Здоров'я й ліки. Власник: роль 06.
///
/// Щогодини (Tick), починаючи з розділу illnessFromChapter (3):
///  1. Кожен здоровий може захворіти (шанс з radiation_balance.json); ×2, якщо ситість &lt; 25 або доза жовта.
///  2. Хворому ЗАВЖДИ потрібні ліки: якщо MedKits &gt; 0 і на базі є медик не у тяжкому стані —
///     медик дає ліки (−1 MedKit), і людина одужує за recoverHours (тяжкий — за severeRecoverHours).
///  3. Немає ліків: Sick через sickToSevereHours стає Severe, якщо медик не доглядає
///     (медик на завданні Heal і медмодуль збудований та має енергію).
///  4. Severe: Task = Medbay (лежить і не працює). Гірше не стає. НІХТО НЕ ПОМИРАЄ.
/// Хворий (Sick) працює вдвічі повільніше — це рахує CrewSystem (04).
/// </summary>
public class HealthSystem : ISimSystem
{
    readonly RadiationBalance bal;

    /// <summary>«Кубик» 0..1 для шансу захворіти. У тестах можна підмінити.</summary>
    public Func<float> Random01 = () => UnityEngine.Random.value;

    public HealthSystem() : this(RadiationBalance.Load()) { }

    public HealthSystem(RadiationBalance balance)
    {
        bal = balance ?? new RadiationBalance();
    }

    HealthBalance H => bal.health;

    // ───────────────────────── Для інтерфейсу і DebugPanel ─────────────────────────

    /// <summary>Чи медик зараз доглядає хворих (не дає стану погіршуватися).</summary>
    public bool IsMedicCaring(GameState s)
    {
        var medic = s.Crew.Find(a => a.Role == CrewRole.Medic && a.Health != HealthStatus.Severe);
        if (medic == null || medic.Task != CrewTask.Heal) return false;
        if (s.Power.Medbay <= 0) return false;
        return FindMedbay(s) != null;
    }

    /// <summary>Чи на базі є медик, який може дати ліки.</summary>
    public static bool HasWorkingMedic(GameState s)
    {
        return s.Crew.Exists(a => a.Role == CrewRole.Medic && a.Health != HealthStatus.Severe);
    }

    /// <summary>Скільки хворих чекають на ліки.</summary>
    public int WaitingForMedicine(GameState s)
    {
        int n = 0;
        foreach (var p in s.Radiation.Patients) if (!p.Treated) n++;
        return n;
    }

    /// <summary>Захворіти зараз (для DebugPanel і тестів). Повертає null або ключ тексту з причиною.</summary>
    public string MakeSick(GameState s, Astronaut a, DayReport r = null)
    {
        if (a == null) return "error.not_found";
        if (a.Health != HealthStatus.Ok) return "error.already_sick";
        a.Health = HealthStatus.Sick;
        Patient(s, a.Name);
        r?.Add("report.sick", 0f, a.Name, true);
        GameEvents.RaiseStateChanged();
        return null;
    }

    // ───────────────────────── Щогодини ─────────────────────────

    public void Tick(GameState s, float hours, DayReport r)
    {
        CleanUp(s);

        if (s.Chapter >= bal.illnessFromChapter)
        {
            foreach (var a in s.Crew)
            {
                if (a.Health != HealthStatus.Ok || a.GoingHome) continue;
                if (Random01() < SickChance(a) * hours) MakeSick(s, a, r);
            }
        }

        bool medicReady = HasWorkingMedic(s);
        bool caring = IsMedicCaring(s);

        foreach (var a in s.Crew)
        {
            if (a.Health == HealthStatus.Ok) continue;
            var p = Patient(s, a.Name);

            // Ліки: є аптечка і є кому дати
            if (!p.Treated && s.Res.MedKits > 0 && medicReady)
            {
                s.Res.MedKits--;
                p.Treated = true;
                p.RecoverHoursLeft = a.Health == HealthStatus.Severe ? H.severeRecoverHours : H.recoverHours;
                r.Add("report.medkit_given", -1f, a.Name);
                GameEvents.RaiseStateChanged();
            }

            if (p.Treated)
            {
                p.RecoverHoursLeft -= hours;
                if (p.RecoverHoursLeft <= 0f)
                {
                    Recover(s, a, r);
                    continue;
                }
            }
            else if (a.Health == HealthStatus.Sick && !caring)
            {
                p.HoursUntreated += hours;
                if (p.HoursUntreated >= H.sickToSevereHours)
                {
                    a.Health = HealthStatus.Severe;
                    r.Add("report.severe", 0f, a.Name, true);
                    GameEvents.RaiseStateChanged();
                }
            }

            if (a.Health == HealthStatus.Severe) SendToMedbay(s, a);
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        int waiting = WaitingForMedicine(s);
        if (waiting > 0 && s.Res.MedKits <= 0) r.Add("report.waiting_medkits", waiting, waiting.ToString(), true);
        else if (waiting > 0 && !HasWorkingMedic(s)) r.Add("report.no_medic", waiting, waiting.ToString(), true);
    }

    // ───────────────────────── Допоміжне ─────────────────────────

    /// <summary>Шанс захворіти за годину для цієї людини.</summary>
    public float SickChance(Astronaut a)
    {
        float chance = H.sickChancePerHour;
        if (a.Satiety < H.hungrySatietyBelow || a.Dose >= bal.yellowPercent) chance *= H.riskMultiplier;
        return chance;
    }

    static PatientState Patient(GameState s, string name)
    {
        var p = s.Radiation.Patients.Find(x => x.Name == name);
        if (p == null)
        {
            p = new PatientState { Name = name };
            s.Radiation.Patients.Add(p);
        }
        return p;
    }

    /// <summary>Прибрати зі списку тих, хто вже здоровий або полетів додому.</summary>
    static void CleanUp(GameState s)
    {
        s.Radiation.Patients.RemoveAll(p =>
        {
            var a = s.Crew.Find(x => x.Name == p.Name);
            return a == null || a.Health == HealthStatus.Ok;
        });
    }

    static void Recover(GameState s, Astronaut a, DayReport r)
    {
        a.Health = HealthStatus.Ok;
        if (a.Task == CrewTask.Medbay)
        {
            a.Task = CrewTask.None;
            a.TaskTarget = "";
            a.AtTaskSite = false;
        }
        s.Radiation.Patients.RemoveAll(p => p.Name == a.Name);
        r.Add("report.recovered", 0f, a.Name);
        GameEvents.RaiseStateChanged();
    }

    static void SendToMedbay(GameState s, Astronaut a)
    {
        var medbay = FindMedbay(s);
        string target = medbay != null ? medbay.SlotId.ToString() : "";
        if (a.Task == CrewTask.Medbay && a.TaskTarget == target) return;
        a.Task = CrewTask.Medbay;
        a.TaskTarget = target;
        a.AtTaskSite = false;
        GameEvents.RaiseStateChanged();
    }

    static ModuleState FindMedbay(GameState s) => s.Modules.Find(m => m.Type == ModuleType.Medbay && m.IsBuilt);
}
