// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;

/// <summary>
/// "What should I do now?" — one hint for the HUD, picked from the state of the base.
/// The most urgent problem first (storm, oxygen, robots without power...), then the next goal of the chapter.
/// The hint disappears as soon as the player has done what it says. Plain C# class: easy to test.
/// </summary>
public static class Coach
{
    public struct Hint
    {
        public string Key; // text key
        public string Arg; // word for {0}
        public bool Urgent;
    }

    public static Hint? Pick(GameState s, ChapterSystem chapters, SimBalanceData balance, float clockSpeed)
    {
        if (s == null) return null;

        // 1. solar storm: everybody inside, the shelter needs power
        if (s.Radiation.Phase != StormPhase.None)
        {
            bool someoneOut = s.Crew.Exists(a => a.Role != CrewRole.Engineer && a.IsOutside && a.Health != HealthStatus.Severe);
            bool shelterBuilt = s.Modules.Exists(m => m.Type == ModuleType.Shelter && m.IsBuilt);
            if (someoneOut || (shelterBuilt && s.Power.Shelter <= 0))
                return new Hint { Key = "hint.storm", Urgent = true };
        }

        // 2. oxygen: life support makes less than the crew breathes
        if (balance != null && balance.lifeSupport != null)
        {
            float made = s.Power.LifeSupport * balance.lifeSupport.oxygenMadePerBlockPerDay;
            float used = s.Crew.Count * balance.lifeSupport.oxygenUsePerAstronautPerDay;
            float alarm = balance.lifeSupport.oxygenTankMax * balance.lifeSupport.oxygenAlarmPercent / 100f;
            if (made + 0.01f < used) return new Hint { Key = "hint.oxygen", Urgent = s.Res.Oxygen < alarm * 1.5f };
        }

        // 3. something is being built, but the robots have no power
        bool building = s.Modules.Exists(m => !m.IsBuilt);
        if (building && s.Power.Robots <= 0) return new Hint { Key = "hint.robots", Urgent = true };

        // 4. the next module the chapter goals ask for
        var goals = chapters != null ? chapters.GetProgress(s) : new List<GoalProgress>();
        foreach (var g in goals)
        {
            if (g.Done || string.IsNullOrEmpty(g.Module)) continue;
            int have = s.Modules.FindAll(m => m.Type.ToString() == g.Module).Count; // built + being built
            if (have < g.Target) return new Hint { Key = "hint.build", Arg = Loc.Get("mod." + g.Module) };
        }

        // 5. water is low: mine ice (when the chapter has ice mining)
        bool canMine = chapters != null && chapters.IsSystemUnlocked("IceMining");
        bool mining = s.Crew.Exists(a => a.Task == CrewTask.MineIce);
        float waterLow = balance != null && balance.start != null && balance.lifeSupport != null
            ? balance.start.water * balance.lifeSupport.waterAlarmPercent / 100f * 2f : 0f; // twice the alarm level
        if (canMine && !mining && s.Res.Water < waterLow) return new Hint { Key = "hint.ice" };

        // 6. food: the greenhouse stands, but it has no power
        bool greenhouse = s.Modules.Exists(m => m.Type == ModuleType.Greenhouse && m.IsBuilt);
        if (greenhouse && s.Power.Greenhouse <= 0) return new Hint { Key = "hint.greenhouse" };

        // 7. science goal: nobody collects samples or works in the lab
        foreach (var g in goals)
        {
            if (g.Done || g.Id != "science") continue;
            bool doingScience = s.Crew.Exists(a => a.Task == CrewTask.CollectSamples || a.Task == CrewTask.Lab);
            if (!doingScience) return new Hint { Key = "hint.science" };
        }

        // 8. all good: time can go faster
        if (clockSpeed < 1.5f) return new Hint { Key = "hint.speed" };
        return null;
    }
}
