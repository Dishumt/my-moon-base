// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Лід → вода і кисень, як планує NASA для бази біля південного полюса (ISRU — «жити з того, що є на місці»):
///  1) БУР стоїть на кратері з льодом і добуває крижаний ґрунт (з геологом поруч — удвічі швидше);
///  2) сіра труба несе лід до ПЕРЕРОБНИКА біля бази, там лід гріють → вода;
///  3) з переробника виходять ДВІ ТРУБИ: синя (вода) і блакитна (кисень). Частину води переробник
///     розщеплює струмом на кисень (електроліз). Гравець сам тягне кожну трубу інженером до модуля:
///     вода — у житловий, теплицю чи медмодуль; кисень — у житловий, укриття, медмодуль чи лабораторію.
///     Без жодної підключеної труби переробник стоїть.
/// Ціна: будматеріали на бур, переробник і кожну трубу (довша труба — дорожча), і заряд батареї, поки вони працюють.
/// Числа — Resources/Data/IcePlant/ice_plant.json, тексти — texts_iceplant_en/uk.json там само.
/// Світ (IcePlantView) малює все в 3D, пише EngineerNear* і викликає Order*/ConnectPipe.
/// </summary>
public class IcePlantSystem : ISimSystem
{
    const string Path = "IcePlant/ice_plant";

    static readonly ModuleType[] WaterTargets = { ModuleType.Habitat, ModuleType.Greenhouse, ModuleType.Medbay };
    static readonly ModuleType[] OxygenTargets = { ModuleType.Habitat, ModuleType.Shelter, ModuleType.Medbay, ModuleType.Lab };

    public IcePlantBalance B { get; }

    float waterToday, oxygenToday, iceToday;
    bool noPowerToday, noPipesToday, fullToday;

    /// <summary>Чи бур зараз копає (для анімації у Світі).</summary>
    public bool DrillWorking { get; private set; }
    /// <summary>Чи переробник зараз працює (для анімації у Світі).</summary>
    public bool ProcessorWorking { get; private set; }

    public IcePlantSystem()
    {
        B = DataLoader.Exists(Path) ? DataLoader.Load<IcePlantBalance>(Path) : new IcePlantBalance();
    }

    // ---------- дії гравця (null = добре, інакше ключ тексту з причиною) ----------

    /// <summary>Замовити бур на кратері з льодом poiId. Ціна — будматеріали.</summary>
    public string OrderDrill(GameState s, string poiId)
    {
        var p = s.IcePlant;
        if (p.DrillOrdered) return "iceplant.err_already";
        var poi = s.FindPoi(poiId);
        if (poi == null || poi.Type != PoiType.Ice) return "iceplant.err_no_ice";
        if (s.Res.Materials < B.drillMaterials) return "iceplant.err_materials";
        s.Res.Materials -= B.drillMaterials;
        p.DrillOrdered = true;
        p.DrillPoiId = poiId;
        p.DrillHoursLeft = B.drillBuildHours;
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Замовити переробник біля бази. Ціна — будматеріали.</summary>
    public string OrderProcessor(GameState s)
    {
        var p = s.IcePlant;
        if (p.ProcessorOrdered) return "iceplant.err_already";
        if (s.Res.Materials < B.processorMaterials) return "iceplant.err_materials";
        s.Res.Materials -= B.processorMaterials;
        p.ProcessorOrdered = true;
        p.ProcessorHoursLeft = B.processorBuildHours;
        GameEvents.RaiseStateChanged();
        return null;
    }

    /// <summary>Скільки будматеріалів коштує труба такої довжини (метри).</summary>
    public int PipeCost(float meters) => Math.Max(1, (int)Math.Ceiling(meters / Math.Max(1f, B.pipeMetersPerMaterial)));

    /// <summary>Чи можна вести цю трубу в модуль такого типу.</summary>
    public static bool CanFeed(PipeKind kind, ModuleType type)
        => Array.IndexOf(kind == PipeKind.Water ? WaterTargets : OxygenTargets, type) >= 0;

    /// <summary>Підключити трубу до модуля в квадратику slot. meters — довжина труби (рахує Світ).</summary>
    public string ConnectPipe(GameState s, PipeKind kind, int slot, float meters)
    {
        var p = s.IcePlant;
        if (!p.ProcessorBuilt) return "iceplant.err_no_processor";
        var m = s.FindModule(slot);
        if (m == null || !m.IsBuilt) return "iceplant.err_no_module";
        if (!CanFeed(kind, m.Type)) return kind == PipeKind.Water ? "iceplant.err_water_target" : "iceplant.err_oxygen_target";
        int cost = PipeCost(meters);
        if (s.Res.Materials < cost) return "iceplant.err_materials";
        s.Res.Materials -= cost;
        if (kind == PipeKind.Water) p.WaterSlot = slot; else p.OxygenSlot = slot;
        GameEvents.RaiseStateChanged();
        return null;
    }

    // ---------- щогодини ----------

    public void Tick(GameState s, float hours, DayReport r)
    {
        var p = s.IcePlant;
        DrillWorking = false;
        ProcessorWorking = false;

        // Будівництво: з інженером поруч — удвічі швидше (як у модулів)
        if (p.DrillOrdered && !p.DrillBuilt)
        {
            p.DrillHoursLeft -= hours * (p.EngineerNearDrill ? 2f : 1f);
            if (p.DrillHoursLeft <= 0f) { p.DrillHoursLeft = 0f; p.DrillBuilt = true; r.Add("iceplant.report_drill_built"); }
        }
        if (p.ProcessorOrdered && !p.ProcessorBuilt)
        {
            p.ProcessorHoursLeft -= hours * (p.EngineerNearProcessor ? 2f : 1f);
            if (p.ProcessorHoursLeft <= 0f) { p.ProcessorHoursLeft = 0f; p.ProcessorBuilt = true; r.Add("iceplant.report_processor_built"); }
        }

        // Труби, чий модуль зник, від'єднуються
        if (p.WaterSlot >= 0 && !Fits(s, PipeKind.Water, p.WaterSlot)) p.WaterSlot = -1;
        if (p.OxygenSlot >= 0 && !Fits(s, PipeKind.Oxygen, p.OxygenSlot)) p.OxygenSlot = -1;

        // 1) Бур копає лід у бункер (бункер обмежений)
        if (p.DrillBuilt)
        {
            if (p.Ice >= B.iceStorageMax - 0.01f) fullToday = true;
            else if (!TakeBattery(s, B.drillBatteryPerHour * hours)) noPowerToday = true;
            else
            {
                float rate = B.icePerHour * (CrewAtDrill(s) ? B.crewBonus : 1f);
                float mined = Math.Min(rate * hours, B.iceStorageMax - p.Ice);
                p.Ice += mined;
                iceToday += mined;
                DrillWorking = mined > 0f;
            }
        }

        // 2) Переробник: лід → вода; якщо підключено трубу кисню — частину води розщеплює на кисень
        if (p.ProcessorBuilt && p.DrillBuilt && p.Ice > 0f)
        {
            bool water = p.WaterSlot >= 0, oxygen = p.OxygenSlot >= 0;
            if (!water && !oxygen) noPipesToday = true;
            else if (!TakeBattery(s, B.processorBatteryPerHour * hours)) noPowerToday = true;
            else
            {
                float ice = Math.Min(p.Ice, B.processIcePerHour * hours);
                p.Ice -= ice;
                float madeWater = ice * B.waterPerIce;                       // розтопили
                float toOxygen = oxygen ? (water ? madeWater * 0.5f : madeWater) : 0f; // скільки води на електроліз
                float o2 = toOxygen * B.oxygenPerWater;
                float w = madeWater - toOxygen;
                s.Res.Water += w;
                s.Res.Oxygen += o2;
                waterToday += w;
                oxygenToday += o2;
                ProcessorWorking = ice > 0f;
            }
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        if (iceToday > 0.05f) r.Add("iceplant.report_ice", iceToday, arg: Num(iceToday));
        if (waterToday > 0.05f) r.Add("iceplant.report_water", waterToday, arg: Num(waterToday));
        if (oxygenToday > 0.05f) r.Add("iceplant.report_oxygen", oxygenToday, arg: Num(oxygenToday));
        if (noPipesToday) r.Add("iceplant.report_no_pipes", important: true);
        if (noPowerToday) r.Add("iceplant.report_no_power", important: true);
        if (fullToday && !s.IcePlant.ProcessorBuilt) r.Add("iceplant.report_full", important: true);
        waterToday = oxygenToday = iceToday = 0f;
        noPowerToday = noPipesToday = fullToday = false;
    }

    // ---------- допоміжне ----------

    static bool Fits(GameState s, PipeKind kind, int slot)
    {
        var m = s.FindModule(slot);
        return m != null && m.IsBuilt && CanFeed(kind, m.Type);
    }

    /// <summary>Хтось із завданням «Лід» дійшов до кратера (геолог обслуговує бур).</summary>
    static bool CrewAtDrill(GameState s)
    {
        foreach (var a in s.Crew)
            if (a.Task == CrewTask.MineIce && a.AtTaskSite) return true;
        return false;
    }

    static bool TakeBattery(GameState s, float amount)
    {
        if (amount <= 0f) return true;
        if (s.Res.Battery < amount) return false;
        s.Res.Battery -= amount;
        return true;
    }

    static string Num(float v) => Math.Abs(v - Math.Round(v)) < 0.05f ? Math.Round(v).ToString("0") : v.ToString("0.0");
}

/// <summary>Яка труба: вода чи кисень.</summary>
public enum PipeKind { Water, Oxygen }

/// <summary>Стан бура, переробника і труб. Зберігається разом з грою (GameState.IcePlant).</summary>
[Serializable]
public class IcePlantState
{
    public bool DrillOrdered, DrillBuilt;
    public string DrillPoiId = "";
    public float DrillHoursLeft;
    public bool ProcessorOrdered, ProcessorBuilt;
    public float ProcessorHoursLeft;
    public float Ice;               // лід у бункері, чекає на переробку
    public int WaterSlot = -1;      // куди підключена труба води (-1 = нікуди)
    public int OxygenSlot = -1;     // куди підключена труба кисню
    public bool EngineerNearDrill;      // пише Світ (01): інженер біля будови бура
    public bool EngineerNearProcessor;  // пише Світ (01): інженер біля будови переробника
}

/// <summary>Файл Resources/Data/IcePlant/ice_plant.json.</summary>
[Serializable]
public class IcePlantBalance
{
    public int drillMaterials = 10;
    public float drillBuildHours = 8f;
    public int processorMaterials = 10;
    public float processorBuildHours = 8f;
    public float pipeMetersPerMaterial = 10f;
    public float icePerHour = 0.25f;
    public float crewBonus = 2f;
    public float iceStorageMax = 10f;
    public float processIcePerHour = 0.5f;
    public float waterPerIce = 1f;
    public float oxygenPerWater = 2f;
    public float drillBatteryPerHour = 0.2f;
    public float processorBatteryPerHour = 0.2f;
}
