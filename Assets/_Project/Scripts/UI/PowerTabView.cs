// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// POWER tab: the main choice of the game. Solar panels give a few power blocks; the player shares them
/// between the systems with − / +. Every block given to one system is a block another system does not get.
///  - free blocks charge the battery; in the ridge shadow the panels give less and the battery helps;
///  - next to each system: what its blocks give (oxygen, food, building...) and a warning if it gets less.
/// Changes only the plan s.Power; PowerSystem (role 03) shares the real energy every hour.
/// Not a MonoBehaviour: TabletController creates it with the "pane-power" element.
/// </summary>
public class PowerTabView
{
    // same order as PowerSystem gives blocks when energy is short: the most important first
    static readonly string[] Systems = { "LifeSupport", "Shelter", "Medbay", "Comms", "Greenhouse", "Robots", "Lab" };

    class Row
    {
        public VisualElement Root, Pips;
        public Label Effect;
    }

    readonly SimBalanceData balance;
    readonly int maxBlocks;
    readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
    readonly Label sunLabel, shadeLabel, freeLabel, freeHint, batteryLabel, batteryHint, oxygenForecast, foodForecast, message;
    readonly VisualElement root, batteryBar;
    const int BatterySegments = 12;

    public PowerTabView(VisualElement pane)
    {
        balance = DataLoader.Load<SimBalanceData>("Balance/balance");
        maxBlocks = Mathf.Max(1, DataLoader.Load<PowerUiBalance>("Balance/balance").power.maxBlocksPerSystem);

        UiKit.Show(pane.Q("pane-power-stub"), false);
        pane.style.alignItems = Align.Stretch;
        pane.style.justifyContent = Justify.FlexStart;
        root = UiKit.Box(pane, "gt-pane");

        // left: where the energy comes from
        var side = UiKit.Box(root, "gt-side");
        UiKit.Label(side, "gt-head", "").name = "pw-solar-head";
        sunLabel = UiKit.Label(side, "gt-big gt-big--en");
        shadeLabel = UiKit.Label(side, "gt-text");
        UiKit.Label(side, "gt-head").name = "pw-free-head";
        freeLabel = UiKit.Label(side, "gt-big");
        freeHint = UiKit.Label(side, "gt-text");
        UiKit.Label(side, "gt-head").name = "pw-battery-head";
        batteryLabel = UiKit.Label(side, "gt-big gt-big--en");
        batteryBar = UiKit.Box(side, "gt-bar");
        for (int i = 0; i < BatterySegments; i++) UiKit.Box(batteryBar, "gt-bar__seg");
        batteryHint = UiKit.Label(side, "gt-text");
        UiKit.Label(side, "gt-head").name = "pw-forecast-head";
        oxygenForecast = UiKit.Label(side, "gt-text");
        foodForecast = UiKit.Label(side, "gt-text");

        // right: one row per system
        var main = UiKit.Box(root, "gt-main");
        UiKit.Label(main, "gt-head").name = "pw-systems-head";
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.style.flexGrow = 1;
        main.Add(scroll);
        foreach (var id in Systems) rows[id] = MakeRow(scroll, id);
        message = UiKit.Label(main, "gt-msg");
    }

    Row MakeRow(VisualElement parent, string id)
    {
        var row = new Row { Root = UiKit.Box(parent, "pw-row") };
        var name = UiKit.Box(row.Root, "pw-name");
        UiKit.Label(name, "pw-title").name = "pw-title-" + id;
        UiKit.Label(name, "pw-desc").name = "pw-desc-" + id;
        UiKit.Button(row.Root, "pw-pm", () => Change(id, -1), "−").name = "pw-minus-" + id;
        row.Pips = UiKit.Box(row.Root, "pw-pips");
        for (int i = 0; i < maxBlocks; i++) UiKit.Box(row.Pips, "pw-pip");
        UiKit.Button(row.Root, "pw-pm", () => Change(id, +1), "+").name = "pw-plus-" + id;
        row.Effect = UiKit.Label(row.Root, "pw-effect");
        return row;
    }

    // ---------- player actions ----------

    void Change(string id, int delta)
    {
        var s = UiKit.State;
        if (s == null) return;
        int now = Get(s.Power, id);
        string err = null;
        if (delta > 0 && now >= maxBlocks) err = Loc.Get("power.err_max", maxBlocks);
        else if (delta > 0 && FreeBlocks(s) <= 0) err = Loc.Get("power.err_no_free");
        else if (delta < 0 && now <= 0) err = Loc.Get("power.err_zero");

        if (err != null)
        {
            SetMessage(err, true);
            return;
        }
        Set(s.Power, id, now + delta);
        SetMessage("", false);
        GameEvents.RaiseStateChanged(); // HUD, world and this tab redraw
    }

    void SetMessage(string text, bool isError)
    {
        UiKit.SetText(message, text);
        message.EnableInClassList("gt-msg--err", isError);
    }

    // ---------- numbers ----------

    int SunBlocks(GameState s) => Mathf.FloorToInt(CountBuilt(s, ModuleType.Solar) * balance.power.panelBlocks + 0.001f);
    int FreeBlocks(GameState s) => SunBlocks(s) - s.Power.Total();

    static int CountBuilt(GameState s, ModuleType type)
    {
        int n = 0;
        foreach (var m in s.Modules) if (m.Type == type && m.IsBuilt) n++;
        return n;
    }

    static ModuleType? ModuleFor(string id)
    {
        switch (id)
        {
            case "Greenhouse": return ModuleType.Greenhouse;
            case "Shelter": return ModuleType.Shelter;
            case "Lab": return ModuleType.Lab;
            case "Medbay": return ModuleType.Medbay;
            default: return null; // life support, robots and the antenna work without their own module
        }
    }

    // ---------- redraw ----------

    public void Refresh()
    {
        var s = UiKit.State;
        var power = UiKit.Sys<PowerSystem>();
        if (s == null || power == null) return;
        var chapters = UiKit.Sys<ChapterSystem>();

        // texts that do not depend on numbers (they change with the language)
        SetNamed("pw-solar-head", Loc.Get("power.solar") + " · " + CountBuilt(s, ModuleType.Solar));
        SetNamed("pw-free-head", Loc.Get("power.free"));
        SetNamed("pw-battery-head", Loc.Get("power.battery"));
        SetNamed("pw-forecast-head", Loc.Get("power.forecast"));
        SetNamed("pw-systems-head", Loc.Get("power.grid"));

        // where the energy comes from
        bool shade = power.IsShade(s.Hour);
        int sun = SunBlocks(s);
        int inShade = Mathf.FloorToInt(CountBuilt(s, ModuleType.Solar) * balance.power.panelBlocksInShade + 0.001f);
        int shadeStart = 24 - Mathf.RoundToInt(balance.power.shadeHoursPerDay);
        UiKit.SetText(sunLabel, shade ? Loc.Get("power.shadow", Loc.Get("power.n_blocks", inShade))
                                      : Loc.Get("power.sun", Loc.Get("power.n_blocks", sun)));
        UiKit.SetText(shadeLabel, Loc.Get("power.shade_info", shadeStart.ToString("00") + ":00", inShade, sun));

        int free = FreeBlocks(s);
        UiKit.SetText(freeLabel, free.ToString());
        freeLabel.EnableInClassList("gt-big--warn", free < 0);
        freeLabel.EnableInClassList("gt-big--ok", free > 0);
        UiKit.SetText(freeHint, free > 0 ? Loc.Get("power.charge_hint") : free < 0 ? Loc.Get("power.over_hint") : Loc.Get("power.steady_hint"));

        // battery
        float battery = s.Res.Battery, capacity = power.BatteryCapacity(s);
        UiKit.SetText(batteryLabel, UiKit.Num(battery) + " / " + UiKit.Num(capacity));
        int filled = capacity > 0f ? Mathf.RoundToInt(battery / capacity * BatterySegments) : 0;
        bool low = capacity > 0f && battery / capacity < 0.25f;
        for (int i = 0; i < batteryBar.childCount; i++)
        {
            batteryBar[i].EnableInClassList("gt-bar__seg--on", i < filled && !low);
            batteryBar[i].EnableInClassList("gt-bar__seg--low", i < filled && low);
        }
        float flow = power.SupplyBlocks(s, s.Hour) - s.Power.Total(); // blocks per hour: + charges, − drains
        string flowText;
        if (flow < -0.01f) flowText = Loc.Get("power.draining", UiKit.Num(-flow)) + " · " + Loc.Get("power.drain_hint", UiKit.Num(battery / -flow));
        else if (flow > 0.01f && battery < capacity - 0.01f) flowText = Loc.Get("power.charging", UiKit.Num(flow));
        else flowText = Loc.Get("power.steady");
        UiKit.SetText(batteryHint, flowText);
        batteryHint.EnableInClassList("gt-text--warn", flow < -0.01f);

        // forecast: oxygen and food per day with this plan
        int crew = s.Crew.Count;
        float oxygenMade = s.Power.LifeSupport * balance.lifeSupport.oxygenMadePerBlockPerDay;
        float oxygenUsed = crew * balance.lifeSupport.oxygenUsePerAstronautPerDay;
        UiKit.SetText(oxygenForecast, Forecast("oxygen", s.Res.Oxygen, oxygenMade - oxygenUsed));
        oxygenForecast.EnableInClassList("gt-text--warn", oxygenMade < oxygenUsed);
        float foodMade = CountBuilt(s, ModuleType.Greenhouse) > 0
            ? s.Power.Greenhouse * balance.greenhouse.growthPerBlockPerDay * balance.greenhouse.portionsPerGrowth : 0f;
        float foodEaten = crew * balance.food.portionsPerAstronautPerDay;
        UiKit.SetText(foodForecast, Forecast("food", s.Res.Food, foodMade - foodEaten));
        foodForecast.EnableInClassList("gt-text--warn", foodMade < foodEaten);

        // systems
        foreach (var id in Systems)
        {
            var row = rows[id];
            var module = ModuleFor(id);
            int plan = Get(s.Power, id);
            bool visible = plan > 0 || chapters == null
                           || (id == "Comms" ? chapters.IsSystemUnlocked("Comms")
                              : module == null || chapters.IsModuleUnlocked(module.Value));
            UiKit.Show(row.Root, visible);
            if (!visible) continue;

            SetNamed("pw-title-" + id, Loc.Get("sys." + id));
            SetNamed("pw-desc-" + id, Loc.Get("sys." + id + ".desc"));
            int granted = Get(power.Granted, id);
            bool short_ = granted < plan;
            row.Root.EnableInClassList("pw-row--short", short_);
            for (int i = 0; i < row.Pips.childCount; i++)
            {
                row.Pips[i].EnableInClassList("pw-pip--on", i < plan && i < granted);
                row.Pips[i].EnableInClassList("pw-pip--short", i < plan && i >= granted);
            }

            bool hasModule = module == null || CountBuilt(s, module.Value) > 0;
            string effect = Effect(s, id, plan, hasModule, module, out int mood);
            if (short_) { effect = Loc.Get("power.short", granted) + " " + effect; mood = -1; }
            UiKit.SetText(row.Effect, effect);
            row.Effect.EnableInClassList("pw-effect--bad", mood < 0);
            row.Effect.EnableInClassList("pw-effect--good", mood > 0);
        }
    }

    /// <summary>What the blocks of this system give. mood: +1 good, 0 neutral, −1 a problem.</summary>
    string Effect(GameState s, string id, int plan, bool hasModule, ModuleType? module, out int mood)
    {
        mood = 0;
        if (!hasModule && module != null)
        {
            mood = plan > 0 ? -1 : 0;
            return Loc.Get("power.no_module", Loc.Get("mod." + module.Value));
        }
        if (plan <= 0)
        {
            mood = id == "LifeSupport" ? -1 : 0;
            return Loc.Get("out.off");
        }
        switch (id)
        {
            case "LifeSupport":
            {
                float made = plan * balance.lifeSupport.oxygenMadePerBlockPerDay;
                float used = s.Crew.Count * balance.lifeSupport.oxygenUsePerAstronautPerDay;
                mood = made + 0.01f >= used ? 1 : -1;
                return Loc.Get("power.fx.LifeSupport", UiKit.Num(made), UiKit.Num(used));
            }
            case "Greenhouse":
                mood = 1;
                return Loc.Get("power.fx.Greenhouse", UiKit.Num(plan * balance.greenhouse.growthPerBlockPerDay * balance.greenhouse.portionsPerGrowth));
            case "Robots":
                mood = 1;
                return Loc.Get("power.fx.Robots", Mathf.Min(plan, s.Robots.Count));
            case "Shelter":
            {
                var radiation = UiKit.Sys<RadiationSystem>();
                int need = radiation != null ? radiation.ShelterPowerNeeded(s) : 0;
                if (need > 0)
                {
                    mood = plan >= need ? 1 : -1;
                    return Loc.Get("storm.shelter_need", need);
                }
                mood = 1;
                return Loc.Get("power.fx.Shelter");
            }
            default:
                mood = 1;
                return Loc.Get("power.fx." + id);
        }
    }

    static string Forecast(string what, float have, float perDay)
    {
        if (perDay < -0.01f) return Loc.Get("fc." + what + ".days", UiKit.Num(have / -perDay));
        if (perDay > 0.01f) return Loc.Get("fc." + what + ".grow");
        return Loc.Get("fc." + what + ".steady");
    }

    void SetNamed(string name, string text) => UiKit.SetText(root.Q<Label>(name), text);

    // ---------- PowerPlan by name ----------

    static int Get(PowerPlan p, string id)
    {
        switch (id)
        {
            case "LifeSupport": return p.LifeSupport;
            case "Greenhouse": return p.Greenhouse;
            case "Comms": return p.Comms;
            case "Shelter": return p.Shelter;
            case "Lab": return p.Lab;
            case "Medbay": return p.Medbay;
            case "Robots": return p.Robots;
            default: return 0;
        }
    }

    static void Set(PowerPlan p, string id, int value)
    {
        value = Math.Max(0, value);
        switch (id)
        {
            case "LifeSupport": p.LifeSupport = value; break;
            case "Greenhouse": p.Greenhouse = value; break;
            case "Comms": p.Comms = value; break;
            case "Shelter": p.Shelter = value; break;
            case "Lab": p.Lab = value; break;
            case "Medbay": p.Medbay = value; break;
            case "Robots": p.Robots = value; break;
        }
    }

    /// <summary>The part of balance.json this tab reads besides SimBalanceData (owner of the file: role 03).</summary>
    [Serializable]
    public class PowerUiBalance
    {
        public PowerUiPart power = new PowerUiPart();
    }

    [Serializable]
    public class PowerUiPart
    {
        public int maxBlocksPerSystem = 4;
    }
}
