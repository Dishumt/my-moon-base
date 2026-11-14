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
        if (GUILayout.Button("Світ: почати модуль")) StartModule(s);
        if (GUILayout.Button("Світ: -1 год будови")) foreach (var m in s.Modules) m.BuildHoursLeft = Mathf.Max(0f, m.BuildHoursLeft - 1f);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Світ: добудувати все")) foreach (var m in s.Modules) m.BuildHoursLeft = 0f;
        if (GUILayout.Button("Світ: прибрати модулі")) s.Modules.Clear();
        GUILayout.EndHorizontal();
    }

    void StartModule(GameState s)
    {
        foreach (var slot in ModuleSlot.All)
        {
            if (s.FindModule(slot.slotId) != null) continue;
            s.Modules.Add(new ModuleState { Type = testType, SlotId = slot.slotId, BuildHoursLeft = testBuildHours });
            return;
        }
        Debug.Log("WorldDebugButtons: вільних слотів немає.");
    }
}
