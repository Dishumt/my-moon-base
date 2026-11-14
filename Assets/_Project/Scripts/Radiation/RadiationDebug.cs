// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Кнопки ролі 06 у DebugPanel (тільки для перевірки в Sandbox-сцені, тому тексти прямо в коді).
/// У сцені 06-Radiation з'являється САМ (сцену редагувати не треба). В іншу сцену можна додати руками.
/// </summary>
public class RadiationDebug : MonoBehaviour
{
    const string SceneName = "06-Radiation";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (SceneManager.GetActiveScene().name != SceneName) return;
        if (FindAnyObjectByType<RadiationDebug>() != null) return;
        new GameObject("RadiationDebug (06)").AddComponent<RadiationDebug>();
    }

    void OnEnable() { DebugPanel.ExtraGUI += Buttons; }
    void OnDisable() { DebugPanel.ExtraGUI -= Buttons; }

    void Buttons()
    {
        var game = GameBootstrap.Instance;
        if (game == null) return;
        var s = game.State;
        var rad = game.GetSystem<RadiationSystem>();
        var health = game.GetSystem<HealthSystem>();
        if (rad == null || health == null) return;
        var report = game.Clock.Report;

        GUILayout.Label("--- 06 Радіація і здоров'я ---");
        GUILayout.Label(StormText(s, rad));

        // База для перевірки: житло, укриття, медмодуль
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Збудувати житло+укриття+мед")) BuildTestModules(s);
        var hab = s.Modules.Find(m => m.Type == ModuleType.Habitat);
        if (hab != null && GUILayout.Button(hab.RegolithShield ? "Житло: реголіт ✓" : "Житло: реголіт ✗"))
            hab.RegolithShield = !hab.RegolithShield;
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Укриття: " + s.Power.Shelter + " бл.");
        if (GUILayout.Button("−")) s.Power.Shelter = Mathf.Max(0, s.Power.Shelter - 1);
        if (GUILayout.Button("+")) s.Power.Shelter++;
        GUILayout.Label("Мед: " + s.Power.Medbay + " бл.");
        if (GUILayout.Button("− ")) s.Power.Medbay = Mathf.Max(0, s.Power.Medbay - 1);
        if (GUILayout.Button("+ ")) s.Power.Medbay++;
        GUILayout.EndHorizontal();

        // Де кожен астронавт (у грі це пише Світ, тут — ми руками)
        foreach (var a in s.Crew)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{a.Name} {a.Dose:0.0}% {rad.Zone(a)}", GUILayout.Width(150));
            if (GUILayout.Button("надворі")) a.InsideSlot = -1;
            if (GUILayout.Button("житло")) a.InsideSlot = SlotOf(s, ModuleType.Habitat);
            if (GUILayout.Button("укриття")) a.InsideSlot = SlotOf(s, ModuleType.Shelter);
            GUILayout.EndHorizontal();
        }

        // Бурі: по одній справжній події кожної сили
        GUILayout.BeginHorizontal();
        GUILayout.Label("Буря через 2 год:");
        for (int strength = 1; strength <= 3; strength++)
        {
            if (GUILayout.Button("сила " + strength))
            {
                var ev = rad.Events.items.Find(e => e.strength == strength);
                if (ev == null) Debug.Log("Немає події сили " + strength);
                else Log(rad.ForceStorm(s, ev.id, 2, report));
            }
        }
        GUILayout.EndHorizontal();

        // Здоров'я
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Хтось захворів"))
        {
            var a = s.Crew.Find(x => x.Health == HealthStatus.Ok && x.Role != CrewRole.Medic);
            Log(health.MakeSick(s, a, report));
        }
        if (GUILayout.Button("+1 аптечка")) s.Res.MedKits++;
        if (GUILayout.Button("Аптечки = 0")) s.Res.MedKits = 0;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        var medic = s.FindCrew(CrewRole.Medic);
        if (medic != null && GUILayout.Button(medic.Task == CrewTask.Heal ? "Медик лікує ✓" : "Медик → Лікувати"))
        {
            medic.Task = medic.Task == CrewTask.Heal ? CrewTask.None : CrewTask.Heal;
            medic.TaskTarget = medic.Task == CrewTask.Heal ? SlotOf(s, ModuleType.Medbay).ToString() : "";
        }
        if (GUILayout.Button("Усі голодні (ситість 10)")) foreach (var a in s.Crew) a.Satiety = 10f;
        GUILayout.EndHorizontal();

        if (s.Chapter < rad.Balance.illnessFromChapter)
            GUILayout.Label($"(Хвороби вмикаються з розділу {rad.Balance.illnessFromChapter}; зараз розділ {s.Chapter})");
    }

    static string StormText(GameState s, RadiationSystem rad)
    {
        var st = s.Radiation;
        var ev = rad.CurrentEvent(s);
        string what = ev != null ? $"{ev.classType} {ev.Day} ({ev.type})" : "";
        switch (st.Phase)
        {
            case StormPhase.Warning:
                return $"Буря через {st.HoursUntilStorm(s)} год: {what}, сила {st.StormStrength}, укриттю треба {rad.ShelterPowerNeeded(s)} бл.";
            case StormPhase.Active:
                return $"БУРЯ йде до {st.StormEndAt % 24:00}:00: {what}, сила {st.StormStrength}";
            default:
                return $"Бурі немає. Минуло бур: {st.StormsSurvived}. Доза/год всередині: {rad.BasePercentPerHour:0.00}%";
        }
    }

    static int SlotOf(GameState s, ModuleType type)
    {
        var m = s.Modules.Find(x => x.Type == type && x.IsBuilt);
        return m != null ? m.SlotId : -1;
    }

    static void BuildTestModules(GameState s)
    {
        foreach (var type in new[] { ModuleType.Habitat, ModuleType.Shelter, ModuleType.Medbay })
        {
            if (s.Modules.Exists(m => m.Type == type)) continue;
            for (int slot = 0; slot < BaseGrid.Count; slot++)
            {
                if (s.FindModule(slot) != null) continue;
                s.Modules.Add(new ModuleState { Type = type, SlotId = slot, BuildHoursLeft = 0f });
                break;
            }
        }
        GameEvents.RaiseStateChanged();
    }

    static void Log(string err)
    {
        Debug.Log(err == null ? "OK" : "Не вийшло: " + Loc.Get(err) + " (" + err + ")");
    }
}
