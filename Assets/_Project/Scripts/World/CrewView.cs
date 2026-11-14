// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Показує екіпаж і роботів: для кожного астронавта з GameState.Crew створює AstronautView,
/// для кожного робота з GameState.Robots — RobotView. Хто зник зі стану — прибирає.
/// Став на об'єкт World (поруч з BaseView і CrewLook).
/// </summary>
public class CrewView : MonoBehaviour
{
    readonly Dictionary<Astronaut, AstronautView> crew = new Dictionary<Astronaut, AstronautView>();
    readonly Dictionary<RobotState, RobotView> robots = new Dictionary<RobotState, RobotView>();
    readonly List<Astronaut> goneCrew = new List<Astronaut>();
    readonly List<RobotState> goneRobots = new List<RobotState>();

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;
        var s = game.State;

        // ----- Астронавти -----
        for (int i = 0; i < s.Crew.Count; i++)
        {
            var a = s.Crew[i];
            if (crew.ContainsKey(a)) continue;
            var go = new GameObject("Astronaut " + a.Name);
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<AstronautView>();
            view.Init(a, i);
            crew[a] = view;
        }
        goneCrew.Clear();
        foreach (var pair in crew) if (!s.Crew.Contains(pair.Key)) goneCrew.Add(pair.Key);
        foreach (var a in goneCrew) { Destroy(crew[a].gameObject); crew.Remove(a); }

        // ----- Роботи -----
        for (int i = 0; i < s.Robots.Count; i++)
        {
            var r = s.Robots[i];
            if (robots.ContainsKey(r)) continue;
            var go = new GameObject("Robot " + (i + 1));
            go.transform.SetParent(transform, false);
            var view = go.AddComponent<RobotView>();
            view.Init(r, i);
            robots[r] = view;
        }
        goneRobots.Clear();
        foreach (var pair in robots) if (!s.Robots.Contains(pair.Key)) goneRobots.Add(pair.Key);
        foreach (var r in goneRobots) { Destroy(robots[r].gameObject); robots.Remove(r); }
    }
}
