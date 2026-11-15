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
    readonly RocketCatalog rockets;
    readonly LaunchCalculator calc;
    readonly CommsSystem comms;

    /// <summary>«Кубик» для жовтої зони. У тестах можна підмінити.</summary>
    public Func<float> Random01 = () => UnityEngine.Random.value;

    /// <summary>
    /// Будівництво (роль 03): набори модулів після посадки замовляють будівництво через нього.
    /// У грі береться з GameBootstrap само, у тестах можна підставити своє.
    /// </summary>
    public ConstructionSystem Construction;

    /// <summary>
    /// Чи відкритий модуль у цьому розділі (ChapterSystem від 03). Закриті набори не можна покласти в ракету.
    /// null — у грі береться з GameBootstrap, а без нього (тести) відкрито все.
    /// </summary>
    public Func<ModuleType, bool> IsModuleUnlocked;

    public SupplySystem() : this(LaunchData.Load(), CargoCatalog.Load(), RocketCatalog.Load()) { }

    public SupplySystem(LaunchData data, CargoCatalog catalog) : this(data, catalog, null) { }

    public SupplySystem(LaunchData data, CargoCatalog catalog, RocketCatalog rockets)
    {
        this.data = data ?? new LaunchData();
        this.catalog = catalog ?? new CargoCatalog();
        this.rockets = rockets ?? new RocketCatalog();
        calc = new LaunchCalculator(this.data);
        comms = new CommsSystem(this.data);
    }

    public LaunchData Data => data;
    public CargoCatalog Catalog => catalog;
    public LaunchCalculator Calculator => calc;
    public RocketCatalog Rockets => rockets;

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
        var item = catalog.Find(itemId);
        if (item == null) return "error.unknown_cargo";
        if (delta > 0 && CargoKg(s) + item.massKg * delta > MaxCargo(s) + 0.01f) return "error.hold_full";
        if (delta > 0 && item.TryGetModule(out var type) && !KitUnlocked(type)) return "error.kit_locked";
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

    // ---------- Ракета ----------

    /// <summary>
    /// Вибрана ракета. null — у rockets.json немає ракет (тоді числа з launch.json).
    /// Для поставок: якщо вибрана ракета не входить у план, береться найбільша з дозволених.
    /// </summary>
    public RocketData GetRocket(GameState s)
    {
        var r = rockets.FindOrDefault(s.Supply.RocketId);
        if (r == null || RocketAllowed(s, r.id)) return r;
        RocketData best = null;
        foreach (var x in rockets.items)
            if (RocketAllowed(s, x.id) && (best == null || x.emptyMassKg > best.emptyMassKg)) best = x;
        return best ?? r;
    }

    // ---------- План поставок ----------

    /// <summary>Вибраний план поставок. null — планів у launch.json немає.</summary>
    public SupplyPlanData GetPlan(GameState s) => data.FindPlan(s.Supply.PlanId) ?? data.FindPlan(data.defaultPlanId);

    /// <summary>Кожні скільки днів летить поставка (з плану, інакше supplyEveryDays).</summary>
    public int EveryDays(GameState s)
    {
        var p = GetPlan(s);
        return p != null && p.everyDays > 0 ? p.everyDays : data.supplyEveryDays;
    }

    /// <summary>Вибрати план. Тільки до першого старту: потім розклад уже складено.</summary>
    public string SetPlan(GameState s, string planId)
    {
        if (s.Supply.FirstMissionDone) return "error.plan_locked";
        if (data.FindPlan(planId) == null) return "error.unknown_plan";
        s.Supply.PlanId = planId;
        return null;
    }

    /// <summary>Чи можна цю ракету для поставки за планом. Перша місія — будь-яка.</summary>
    public bool RocketAllowed(GameState s, string rocketId)
    {
        if (!s.Supply.FirstMissionDone) return true;
        var p = GetPlan(s);
        return p == null || p.rockets == null || p.rockets.Count == 0 || p.rockets.Contains(rocketId);
    }

    /// <summary>Скільки кг вантажу вміщує трюм вибраної ракети (без меж, якщо не задано).</summary>
    public float MaxCargo(GameState s)
    {
        var r = GetRocket(s);
        return r != null && r.maxCargoKg > 0f ? r.maxCargoKg : float.MaxValue;
    }

    /// <summary>Скільки кг вантажу зараз у замовленні.</summary>
    public float CargoKg(GameState s) => LaunchCalculator.CargoMass(s.Supply.Cargo, catalog);

    /// <summary>Чи влізе ще одна штука цього вантажу.</summary>
    public bool Fits(GameState s, string itemId)
    {
        var item = catalog.Find(itemId);
        return item != null && CargoKg(s) + item.massKg <= MaxCargo(s) + 0.01f;
    }

    /// <summary>Найбільший бак вибраної ракети.</summary>
    public float MaxFuel(GameState s)
    {
        var r = GetRocket(s);
        return r != null ? r.maxFuelKg : data.maxFuelKg;
    }

    /// <summary>Скільки годин летить вибрана ракета.</summary>
    public int FlightHours(GameState s)
    {
        var r = GetRocket(s);
        return r != null ? r.flightHours : data.flightHours;
    }

    /// <summary>Вибрати ракету. Паливо, якщо не влазить у новий бак, зменшується до повного бака.</summary>
    public string SetRocket(GameState s, string rocketId)
    {
        if (s.Supply.FirstMissionDone && s.Supply.Status != SupplyStatus.Draft) return "error.order_locked";
        if (rockets.Find(rocketId) == null) return "error.unknown_rocket";
        if (!RocketAllowed(s, rocketId)) return "error.rocket_not_in_plan";
        var target = rockets.Find(rocketId);
        if (target.maxCargoKg > 0f && CargoKg(s) > target.maxCargoKg + 0.01f) return "error.hold_too_small";
        s.Supply.RocketId = rocketId;
        s.Supply.FuelKg = Math.Min(GetFuel(s), MaxFuel(s));
        return null;
    }

    // ---------- Паливо ----------

    public float GetFuel(GameState s)
    {
        if (s.Supply.FuelKg < 0f) s.Supply.FuelKg = Math.Min(data.startFuelKg, MaxFuel(s));
        return s.Supply.FuelKg;
    }

    public string SetFuel(GameState s, float kg)
    {
        if (s.Supply.FirstMissionDone && s.Supply.Status != SupplyStatus.Draft) return "error.order_locked";
        s.Supply.FuelKg = Math.Max(0f, Math.Min(kg, MaxFuel(s)));
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
        var r = calc.Evaluate(s.Supply.Cargo, catalog, People(s, firstMission), GetFuel(s), GetRocket(s));
        LaunchCalculator.ApplyWeather(r, s.Supply.Weather, data);   // вітер і гроза змінюють шанс
        return r;
    }

    // ---------- «Порадь мені» ----------

    /// <summary>Скільки палива треба, щоб світлофор став зеленим (із запасом greenMargin), округлено вгору до 10 кг.</summary>
    public float GreenFuel(GameState s, bool firstMission)
    {
        var r = Evaluate(s, firstMission);
        return (float)Math.Ceiling(r.FuelNeededKg * data.greenMargin / 10f) * 10f;
    }

    /// <summary>
    /// Центр керування заливає паливо рівно до зеленого: не менше (небезпечно) і не більше (зайва вага).
    /// Повертає null або ключ тексту: «навіть повний бак не підніме» (тоді заливає повний бак).
    /// Ціну (максимум 2 зірки у звіті) рахує екран запуску.
    /// </summary>
    public string AdviseFuel(GameState s, bool firstMission)
    {
        if (!CanEdit(s, firstMission)) return "error.order_locked";
        float need = GreenFuel(s, firstMission);
        float max = MaxFuel(s);
        SetFuel(s, Math.Min(need, max));
        GameEvents.RaiseStateChanged();
        return need > max ? "advise.too_heavy" : null;
    }

    // ---------- Погода ----------

    /// <summary>Новий прогноз погоди на космодромі. allowStorm = false — гроза вже минула.</summary>
    public LaunchWeather RollWeather(GameState s, bool allowStorm = true)
    {
        float x = Random01();
        float storm = allowStorm ? data.weatherStormChance : 0f;
        var w = x < storm ? LaunchWeather.Storm
              : x < storm + data.weatherWindyChance ? LaunchWeather.Windy
              : LaunchWeather.Clear;
        s.Supply.Weather = w;
        s.Supply.WeatherKnown = true;
        return w;
    }

    /// <summary>Перша місія: зробити прогноз, якщо його ще немає (кличе екран запуску під час відкриття).</summary>
    public void ForecastFirstMission(GameState s)
    {
        if (!s.Supply.FirstMissionDone && !s.Supply.WeatherKnown) RollWeather(s);
    }

    /// <summary>
    /// Перша місія: перечекати грозу. Після неї буде ясно або вітер.
    /// Ціна: частина палива в баку випаровується (stormBoilOff) — треба знову глянути на світлофор.
    /// </summary>
    public string WaitOutStorm(GameState s)
    {
        if (s.Supply.FirstMissionDone) return "error.order_locked";
        if (s.Supply.Weather != LaunchWeather.Storm) return "error.no_storm";
        s.Supply.FuelKg = GetFuel(s) * (1f - data.stormBoilOff);
        RollWeather(s, false);
        GameEvents.RaiseStateChanged();
        return null;
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
        if (!ok) RollWeather(s);                 // поки перепаковують — новий прогноз
        if (ok)
        {
            s.Supply.Weather = LaunchWeather.Clear;   // далі погоду для поставок вирішує старт за розкладом
            s.Supply.WeatherKnown = false;
            Deliver(s, null);
            s.Supply.Cargo.Clear();          // усе вивантажили — наступне замовлення починається з порожнього списку
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

    /// <summary>
    /// Коли (година від початку гри) стартує поточне замовлення.
    /// Якщо розклад уже настав, а замовлення не відправлене, корабель чекає: стартує, щойно відправиш.
    /// </summary>
    public int ExpectedLaunchAt(GameState s)
    {
        var sup = s.Supply;
        if (sup.Status == SupplyStatus.Launched || sup.Status == SupplyStatus.Returned) return sup.ArrivalAt - FlightHours(s);
        return Math.Max(Now(s), sup.NextLaunchAt);
    }

    /// <summary>Коли (година від початку гри) сяде поточне замовлення.</summary>
    public int ExpectedArrivalAt(GameState s)
    {
        var sup = s.Supply;
        if (sup.Status == SupplyStatus.Launched || sup.Status == SupplyStatus.Returned) return sup.ArrivalAt;
        return ExpectedLaunchAt(s) + FlightHours(s);
    }

    /// <summary>Чи прострочене замовлення: розклад настав, а його ще не відправили (корабель чекає).</summary>
    public bool IsLate(GameState s) => s.Supply.Status == SupplyStatus.Draft && s.Supply.Scheduled && Now(s) >= s.Supply.NextLaunchAt;

    /// <summary>
    /// На якому кроці поставка (для планшета): 0 склади список, 1 відправ у вікно зв'язку,
    /// 2 чекає старту, 3 летить, 4 сідає. Після посадки знову 0 — нове замовлення.
    /// </summary>
    public int Step(GameState s)
    {
        var sup = s.Supply;
        switch (sup.Status)
        {
            case SupplyStatus.Sent: return 2;
            case SupplyStatus.Launched:
            case SupplyStatus.Returned: return 3;
            default: return sup.Cargo.Count == 0 && sup.Passengers.Count == 0 ? 0 : 1;
        }
    }

    void ScheduleFrom(GameState s, int fromHour)
    {
        int day = fromHour / 24 + EveryDays(s);
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

        // погода на Землі: у грозу ЦУП відкладає старт (лише раз, далі летимо хоч і у вітер)
        var weather = RollWeather(s);
        if (weather == LaunchWeather.Storm && !sup.StormDelayed)
        {
            sup.StormDelayed = true;
            sup.NextLaunchAt = now + data.stormDelayHours;
            r.Add("report.supply_storm", data.stormDelayHours, "", true);
            GameEvents.RaiseStateChanged();
            return;
        }
        if (weather == LaunchWeather.Storm) sup.Weather = LaunchWeather.Windy;
        sup.StormDelayed = false;

        bool ok = LaunchCalculator.Roll(Evaluate(s, false), Random01);
        sup.Weather = LaunchWeather.Clear;
        if (ok && weather != LaunchWeather.Clear) r.Add("report.supply_weather_ok", 0f, "", false);
        sup.LastLaunchFailed = !ok;
        if (ok)
        {
            sup.Status = SupplyStatus.Launched;
            sup.ArrivalAt = now + FlightHours(s);
            r.Add("report.supply_launched");
        }
        else
        {
            sup.Status = SupplyStatus.Returned;
            sup.ArrivalAt = now + FlightHours(s) + data.failedDelayDays * 24;
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
            res.Oxygen += item.oxygen * line.count;
            res.Materials += item.materials * line.count;
            res.MedKits += item.medKits * line.count;
            s.Supply.SolarPanelKits += item.solarPanelKits * line.count;
            s.Supply.BatteryKits += item.batteryKits * line.count;
        }

        // набори модулів: будматеріали вже додані вище, тепер роботи одразу починають будувати
        foreach (var line in s.Supply.Cargo)
        {
            var item = catalog.Find(line.itemId);
            if (item == null || !item.TryGetModule(out var type)) continue;
            for (int i = 0; i < line.count; i++) BuildKit(s, r, type);
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

    // ---------- Набори модулів ----------

    /// <summary>Чи можна вже брати набір цього модуля (модуль відкритий у поточному розділі).</summary>
    public bool KitUnlocked(ModuleType type)
    {
        if (IsModuleUnlocked != null) return IsModuleUnlocked(type);
        var chapters = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ChapterSystem>() : null;
        return chapters == null || chapters.Current == null || chapters.IsModuleUnlocked(type);
    }

    /// <summary>
    /// Замовити будівництво модуля з набору у вільному квадратику біля бази.
    /// Не вийшло (модуль ще не відкритий, немає місця чи роботів) — будматеріали лишаються на складі.
    /// </summary>
    void BuildKit(GameState s, DayReport r, ModuleType type)
    {
        var construction = Construction;
        if (construction == null && GameBootstrap.Instance != null) construction = GameBootstrap.Instance.GetSystem<ConstructionSystem>();

        string name = LaunchText.Get("launch.module." + type);
        int slot = PickSlot(s);
        string err = construction == null || slot < 0 ? "error.place_taken" : construction.Order(s, type, slot);
        r?.Add(err == null ? "report.kit_building" : "report.kit_unpacked", 0f, name);
    }

    /// <summary>
    /// Вільний квадратик BaseGrid: спершу поруч із наявними модулями (щоб база була цілою), інакше перший вільний.
    /// -1 — вільних немає.
    /// </summary>
    public static int PickSlot(GameState s)
    {
        int firstFree = -1;
        for (int slot = 0; slot < BaseGrid.Count; slot++)
        {
            if (BaseGrid.CanPlace(s, slot) != null) continue;
            if (firstFree < 0) firstFree = slot;
            int c = BaseGrid.Column(slot), row = BaseGrid.Row(slot);
            if (HasModule(s, c - 1, row) || HasModule(s, c + 1, row) || HasModule(s, c, row - 1) || HasModule(s, c, row + 1)) return slot;
        }
        return firstFree;
    }

    static bool HasModule(GameState s, int column, int row)
    {
        int slot = BaseGrid.ToSlot(column, row);
        return slot >= 0 && s.FindModule(slot) != null;
    }

    public void OnDayEnd(GameState s, DayReport r) { }
}
