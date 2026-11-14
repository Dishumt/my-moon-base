// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Кнопки техліда в DebugPanel, щоб перевірити світ без системи будівництва (її робить 03).
/// Пишуть у GameState напряму — це тільки для перевірки, у грі так не робимо!
/// Став на об'єкт Game або World.
/// </summary>
public class WorldDebugButtons : MonoBehaviour
{
    [SerializeField] ModuleType testType = ModuleType.Habitat;
    [SerializeField] float testBuildHours = 6f;

    void OnEnable() => DebugPanel.ExtraGUI += Buttons;
    void OnDisable() => DebugPanel.ExtraGUI -= Buttons;

    void Buttons()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;
        var s = game.State;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Світ: модуль у вільну")) StartModule(s, FirstFree(s));
        if (GUILayout.Button("Світ: модуль тут")) StartModule(s, HereSlot());
        if (GUILayout.Button("Світ: -1 год будови")) foreach (var m in s.Modules) m.BuildHoursLeft = Mathf.Max(0f, m.BuildHoursLeft - 1f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Світ: добудувати все")) foreach (var m in s.Modules) m.BuildHoursLeft = 0f;
        if (GUILayout.Button("Світ: прибрати модулі")) s.Modules.Clear();
        GUILayout.EndHorizontal();
    }

    void StartModule(GameState s, int slotId)
    {
        string err = BaseGrid.CanPlace(s, slotId);
        if (err != null) { Debug.Log("WorldDebugButtons: не можна будувати — " + err); return; }
        s.Modules.Add(new ModuleState { Type = testType, SlotId = slotId, BuildHoursLeft = testBuildHours });
    }

    static int FirstFree(GameState s)
    {
        for (int id = 0; id < BaseGrid.Count; id++) if (BaseGrid.CanPlace(s, id) == null) return id;
        return -1;
    }

    /// <summary>Квадратик, де зараз стоїть інженер.</summary>
    static int HereSlot()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? BaseGrid.FromWorld(player.transform.position) : -1;
    }
}
