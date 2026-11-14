// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Поставки із Землі: розклад, замовлення, старт, прибуття.
/// Перша місія розділу 1 — той самий екран запуску, але з 4 зайнятими місцями.
/// Власник: роль 04.
/// </summary>
public class SupplySystem : ISimSystem
{
    readonly LaunchData data;
    readonly CargoCatalog catalog;
    readonly LaunchCalculator calc;
    readonly CommsSystem comms;

    /// <summary>«Кубик» для жовтої зони. У тестах можна підмінити.</summary>
    public Func<float> Random01 = () => UnityEngine.Random.value;

    public SupplySystem() : this(LaunchData.Load(), CargoCatalog.Load()) { }

    public SupplySystem(LaunchData data, CargoCatalog catalog)
    {
        this.data = data ?? new LaunchData();
        this.catalog = catalog ?? new CargoCatalog();
        calc = new LaunchCalculator(this.data);
        comms = new CommsSystem(this.data);
    }

    public LaunchData Data => data;
    public CargoCatalog Catalog => catalog;
    public LaunchCalculator Calculator => calc;

    static int Now(GameState s) => CommsSystem.AbsHour(s);

    // ---------- Чернетка (екран запуску) ----------

    /// <summary>Чи можна зараз міняти вантаж і паливо.</summary>
    public bool CanEdit(GameState s, bool firstMission)
    {
        return firstMission ? !s.Supply.FirstMissionDone : s.Supply.Status == SupplyStatus.Draft;
    }

    /// <summary>Додати (delta &gt; 0) чи прибрати (delta &lt; 0) вантаж.</summary>
    public string ChangeCargo(GameState s, string itemId, int delta)
    {
        if (s.Supply.FirstMissionDone && s.Supply.Status != SupplyStatus.Draft) return "error.order_locked";
        if (catalog.Find(itemId) == null) return "error.unknown_cargo";
        var line = s.Supply.Cargo.Find(l => l.itemId == itemId);
        if (line == null)
        {
            if (delta <= 0) return null;
            line = new CargoLine { itemId = itemId, count = 0 };
            s.Supply.Cargo.Add(line);
        }
        line.count = Math.Max(0, line.count + delta);
        if (line.count == 0) s.Supply.Cargo.Remove(line);
        return null;
    }

    public float GetFuel(GameState s)
    {
        if (s.Supply.FuelKg < 0f) s.Supply.FuelKg = data.startFuelKg;
        return s.Supply.FuelKg;
    }

    public string SetFuel(GameState s, float kg)
    {
        if (s.Supply.FirstMissionDone && s.Supply.Status != SupplyStatus.Draft) return "error.order_locked";
        s.Supply.FuelKg = Math.Max(0f, Math.Min(kg, data.maxFuelKg));
        return null;
    }

    /// <summary>Скільки людей летить: 4 у першій місії, інакше заміни з резерву.</summary>
    public int People(GameState s, bool firstMission)
    {
        return firstMission ? data.firstMissionSeats : s.Supply.Passengers.Count;
    }

    /// <summary>Розрахунок світлофора для поточного замовлення.</summary>
    public LaunchResult Evaluate(GameState s, bool firstMission)
    {
        return calc.Evaluate(s.Supply.Cargo, catalog, People(s, firstMission), GetFuel(s));
    }

    // ---------- Перша місія ----------

    /// <summary>
    /// Старт першої місії (з екіпажем). Повертає true, якщо долетіли.
    /// Невдача: вантаж лишається в чернетці — гравець перепаковує і пробує ще раз.
    /// </summary>
    public bool LaunchFirstMission(GameState s)
    {
        if (s.Supply.FirstMissionDone) return true;
        bool ok = LaunchCalculator.Roll(Evaluate(s, true), Random01);
        s.Supply.LastLaunchFailed = !ok;
        if (ok)
        {
            Deliver(s, null);
            s.Supply.FirstMissionDone = true;
            s.Supply.Deliveries = 0; // перша місія — це не поставка
            ScheduleFrom(s, Now(s));
        }
        GameEvents.RaiseStateChanged();
        return ok;
    }

    // ---------- Поставки ----------

    /// <summary>Відправити замовлення на Землю. Тільки у вікно зв'язку.</summary>
    public string Send(GameState s)
    {
        if (s.Supply.Status != SupplyStatus.Draft) return "error.order_already_sent";
        if (!comms.IsOpen(s)) return "error.comms_closed";
        GetFuel(s);
        s.Supply.Status = SupplyStatus.Sent;
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Скасувати відправлене (поки не стартувало) — знову чернетка.</summary>
    public string CancelSend(GameState s)
    {
        if (s.Supply.Status != SupplyStatus.Sent) return "error.order_locked";
        s.Supply.Status = SupplyStatus.Draft;
        return null;
    }

    /// <summary>Година наступного старту за розкладом.</summary>
    public int NextLaunchAt(GameState s) => s.Supply.NextLaunchAt;

    void ScheduleFrom(GameState s, int fromHour)
    {
        int day = fromHour / 24 + data.supplyEveryDays;
        s.Supply.NextLaunchAt = day * 24 + data.launchHour;
        s.Supply.Scheduled = true;
        s.Supply.MissedReported = false;
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        var sup = s.Supply;
        int now = Now(s);
        if (!sup.Scheduled) ScheduleFrom(s, now);

        switch (sup.Status)
        {
            case SupplyStatus.Draft:
                if (now >= sup.NextLaunchAt && !sup.MissedReported)
                {
                    // розклад настав, а замовлення не відправлене — корабель чекає
                    r.Add("report.supply_not_sent", 0f, "", true);
                    sup.MissedReported = true;
                }
                break;

            case SupplyStatus.Sent:
                if (now >= sup.NextLaunchAt) Launch(s, r, now);
                break;

            case SupplyStatus.Launched:
            case SupplyStatus.Returned:
                if (now >= sup.ArrivalAt) Arrive(s, r, now);
                break;
        }
    }

    void Launch(GameState s, DayReport r, int now)
    {
        var sup = s.Supply;
        bool ok = LaunchCalculator.Roll(Evaluate(s, false), Random01);
        sup.LastLaunchFailed = !ok;
        if (ok)
        {
            sup.Status = SupplyStatus.Launched;
            sup.ArrivalAt = now + data.flightHours;
            r.Add("report.supply_launched");
        }
        else
        {
            sup.Status = SupplyStatus.Returned;
            sup.ArrivalAt = now + data.flightHours + data.failedDelayDays * 24;
            r.Add("report.supply_returned", data.failedDelayDays, "", true);
        }
        GameEvents.RaiseStateChanged();
    }

    void Arrive(GameState s, DayReport r, int now)
    {
        Deliver(s, r);
        var sup = s.Supply;
        sup.Deliveries++;
        sup.LastArrivalDay = s.Day;
        sup.Cargo.Clear();
        sup.Passengers.Clear();
        sup.Status = SupplyStatus.Draft;
        ScheduleFrom(s, now);
        r.Add("report.supply_arrived");
        GameEvents.RaiseSupplyArrived();
        GameEvents.RaiseStateChanged();
    }

    /// <summary>Вивантажити: ресурси в GameState, люди в Crew, ті, хто GoingHome, відлітають.</summary>
    void Deliver(GameState s, DayReport r)
    {
        var res = s.Res;
        foreach (var line in s.Supply.Cargo)
        {
            var item = catalog.Find(line.itemId);
            if (item == null) continue;
            res.Water += item.water * line.count;
            res.Food += item.food * line.count;
            res.Materials += item.materials * line.count;
            res.MedKits += item.medKits * line.count;
            s.Supply.SolarPanelKits += item.solarPanelKits * line.count;
            s.Supply.BatteryKits += item.batteryKits * line.count;
        }

        // хто досяг ліміту — летить додому тим самим кораблем
        for (int i = s.Crew.Count - 1; i >= 0; i--)
        {
            var a = s.Crew[i];
            if (!a.GoingHome) continue;
            s.Crew.RemoveAt(i);
            s.Supply.WentHome.Add(a.Name);
            r?.Add("report.went_home", 0f, a.Name);
        }

        // заміни з резерву
        foreach (var name in s.Supply.Passengers)
        {
            var a = s.Reserve.Find(x => x.Name == name);
            if (a == null) continue;
            s.Reserve.Remove(a);
            a.Task = CrewTask.None;
            a.TaskTarget = "";
            a.AtTaskSite = false;
            a.GoingHome = false;
            s.Crew.Add(a);
            r?.Add("report.crew_arrived", 0f, a.Name);
        }
    }

    public void OnDayEnd(GameState s, DayReport r) { }
}
