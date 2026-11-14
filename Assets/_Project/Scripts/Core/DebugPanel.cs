// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Панель для розробників (не для гравця!), тому тексти тут прямо в коді — це дозволено.
/// Показує стан гри і кнопки «+1 година», «+1 день», «Пауза».
/// Свої тестові кнопки можна додати зі свого скрипта в Sandbox-сцені:
///   void OnEnable()  { DebugPanel.ExtraGUI += MyButtons; }
///   void OnDisable() { DebugPanel.ExtraGUI -= MyButtons; }
///   void MyButtons() { if (GUILayout.Button("Замовити теплицю")) { ... } }
/// </summary>
public class DebugPanel : MonoBehaviour
{
    /// <summary>Сюди підписуються ролі, щоб додати свої кнопки.</summary>
    public static event Action ExtraGUI;

    [SerializeField] bool visible = true;
    [SerializeField] float scale = 1.3f;   // більше — крупніший шрифт

    Vector2 scroll;
    readonly StringBuilder sb = new StringBuilder();

    void OnGUI()
    {
        var game = GameBootstrap.Instance;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        if (GUI.Button(new Rect(10, 10, 90, 28), visible ? "Debug -" : "Debug +")) visible = !visible;
        if (!visible) return;

        float height = Screen.height / scale - 60;
        GUILayout.BeginArea(new Rect(10, 42, 380, height), GUI.skin.box);

        if (game == null || game.State == null)
        {
            GUILayout.Label("Немає GameBootstrap у сцені");
            GUILayout.EndArea();
            return;
        }

        var s = game.State;
        var clock = game.Clock;

        // --- Кнопки часу ---
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 година")) clock.AdvanceHours(1);
        if (GUILayout.Button("+1 день")) clock.AdvanceHours(24);
        if (GUILayout.Button(clock.UserPaused ? "Далі" : "Пауза")) clock.TogglePause();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("×1")) clock.SetSpeed(1f);
        if (GUILayout.Button("×2")) clock.SetSpeed(2f);
        if (GUILayout.Button("Мова: " + Loc.Language)) Loc.SetLanguage(Loc.Language == Loc.English ? Loc.Ukrainian : Loc.English);
        GUILayout.EndHorizontal();

        ExtraGUI?.Invoke();

        // --- Стан гри ---
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label(BuildText(s, clock));
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    string BuildText(GameState s, GameClock clock)
    {
        sb.Clear();
        string status = clock.IsPaused ? "ПАУЗА" : "×" + clock.Speed;
        sb.AppendLine($"День {s.Day}, {s.Hour:00}:00   [{status}]");
        sb.AppendLine($"Розділ {s.Chapter}   Наука {s.Science:0}");

        var r = s.Res;
        sb.AppendLine($"\nБатарея {r.Battery:0.0}  Вода {r.Water:0.0}  Кисень {r.Oxygen:0.0}");
        sb.AppendLine($"Їжа {r.Food:0.0}  Матеріали {r.Materials:0}  Аптечки {r.MedKits}");

        var p = s.Power;
        sb.AppendLine($"\nЕнергія (всього роздано {p.Total()}):");
        sb.AppendLine($" Життєзаб {p.LifeSupport}  Теплиця {p.Greenhouse}  Антена {p.Comms}");
        sb.AppendLine($" Укриття {p.Shelter}  Лаб {p.Lab}  Мед {p.Medbay}  Роботи {p.Robots}");

        sb.AppendLine("\nЕкіпаж:");
        foreach (var a in s.Crew)
        {
            string where = a.IsOutside ? "надворі" : "слот " + a.InsideSlot;
            sb.AppendLine($" {a.Name} ({a.Role}) — {a.Task} {a.TaskTarget}");
            sb.AppendLine($"   ситість {a.Satiety:0}  {a.Health}  доза {a.Dose:0}%  {where}");
        }

        sb.AppendLine($"\nМодулі ({s.Modules.Count}):");
        foreach (var m in s.Modules)
        {
            string build = m.IsBuilt ? "готовий" : $"будується, ще {m.BuildHoursLeft:0} год";
            sb.AppendLine($" {m.Type} у слоті {m.SlotId}: {build}{(m.RegolithShield ? ", реголіт" : "")}");
        }

        int working = s.Robots.FindAll(x => x.Working).Count;
        int found = s.Pois.FindAll(x => x.Discovered).Count;
        sb.AppendLine($"\nРоботи: {s.Robots.Count} (працюють {working})   Точки: знайдено {found}/{s.Pois.Count}");

        sb.AppendLine($"\nЗвіт за сьогодні ({clock.Report.Lines.Count}):");
        foreach (var line in clock.Report.Lines)
        {
            sb.AppendLine($" {(line.Important ? "!" : "-")} {line.Key} {line.Delta:+0.#;-0.#;}  {line.Arg}");
        }
        return sb.ToString();
    }
}
