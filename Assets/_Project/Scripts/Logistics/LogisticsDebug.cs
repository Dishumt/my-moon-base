// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Кнопки ролі 04 у DebugPanel (тільки для перевірки в Sandbox-сцені, тому тексти прямо в коді).
/// Додай цей компонент на будь-який об'єкт у сцені 04.
/// </summary>
public class LogisticsDebug : MonoBehaviour
{
    [SerializeField] LaunchScreen launchScreen;

    void OnEnable() { DebugPanel.ExtraGUI += Buttons; }
    void OnDisable() { DebugPanel.ExtraGUI -= Buttons; }

    void Buttons()
    {
        var game = GameBootstrap.Instance;
        if (game == null) return;
        var s = game.State;
        var crew = game.GetSystem<CrewSystem>();
        var supply = game.GetSystem<SupplySystem>();
        var comms = game.GetSystem<CommsSystem>();

        GUILayout.Label("--- 04 Логістика ---");
        if (launchScreen != null && GUILayout.Button("Екран запуску")) launchScreen.Open();

        var geo = s.FindCrew(CrewRole.Geologist);
        var bot = s.FindCrew(CrewRole.Botanist);
        GUILayout.BeginHorizontal();
        if (geo != null && GUILayout.Button("Геолог → лід")) Log(crew.Assign(s, geo, CrewTask.MineIce));
        if (bot != null && GUILayout.Button("Ботанік → лід")) Log(crew.Assign(s, bot, CrewTask.MineIce));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (geo != null && GUILayout.Button("Геолог → зразки")) Log(crew.Assign(s, geo, CrewTask.CollectSamples));
        if (GUILayout.Button("Усі дійшли")) foreach (var a in s.Crew) if (a.Task != CrewTask.None) a.AtTaskSite = true;
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Всі в укриття")) Debug.Log("Відкликано: " + crew.RecallAllInside(s));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Антена +1")) s.Power.Comms++;
        if (GUILayout.Button("Антена 0")) s.Power.Comms = 0;
        GUILayout.EndHorizontal();
        GUILayout.Label("Зв'язок: " + (comms.IsOpen(s) ? "Є" : "немає, ще " + comms.HoursUntilOpen(s) + " год"));

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+вода в замовл.")) Log(supply.ChangeCargo(s, "water", 1));
        if (GUILayout.Button("Відправити")) Log(supply.Send(s));
        GUILayout.EndHorizontal();
        if (geo != null && GUILayout.Button("Геолог: ліміт дози → заміна"))
        {
            geo.GoingHome = true;
            Log(crew.RequestReplacement(s, geo));
        }
        GUILayout.Label("Поставка: " + s.Supply.Status + ", старт о год " + s.Supply.NextLaunchAt +
                        " (зараз " + CommsSystem.AbsHour(s) + "), людей " + s.Supply.Passengers.Count);
        GUILayout.Label("Вода " + s.Res.Water.ToString("0.0") + "  Наука " + s.Science.ToString("0.0"));
    }

    static void Log(string err)
    {
        Debug.Log(err == null ? "OK" : "Помилка: " + err + " → " + Loc.Get(err));
    }
}
