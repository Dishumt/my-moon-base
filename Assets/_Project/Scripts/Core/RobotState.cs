// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Робот-будівельник. Світ (01) веде його до TargetSlot.</summary>
[Serializable]
public class RobotState
{
    public RobotTask Task = RobotTask.Idle;
    public int TargetSlot = -1;      // слот, де працювати; -1 = стоїть біля ангара
    public bool Working;             // чи зараз працює (має енергію і завдання)
}
