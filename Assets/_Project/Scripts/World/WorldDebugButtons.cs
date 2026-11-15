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
        if (GUILayout.Button("Світ: прибрати модулі")) { s.Modules.Clear(); s.Tunnels.Clear(); }
        if (GUILayout.Button("Світ: 2 кімнати + тунель")) InstantTunnel(s);   // роль 04: перевірка тунелів
        if (GUILayout.Button("Світ: реголіт кімнатам ✓/✗")) ToggleRegolith(s);   // роль 04: насип росте/зникає за ~3 с
        GUILayout.EndHorizontal();

        // ----- Живі меблі: тип нового модуля, енергія кімнатам, аптечки -----
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Світ: тип " + testType + " →")) testType = NextRoomType(testType);
        if (GUILayout.Button(savedPlan == null ? "Енергія кімнатам: 0" : "Енергія кімнатам: назад")) TogglePower(game);
        if (GUILayout.Button("Енергія кімнатам +1")) AddRoomPower(game);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button(ModulePower.DebugOutage ? "Аварія: прибрати" : "Аварія: немає енергії")) ModulePower.DebugOutage = !ModulePower.DebugOutage;
        GUILayout.EndHorizontal();
        GUILayout.Label("Світ бачить енергію (отримано/план): " + RoomPowerText());

        // ----- Ефекти світу: буря, тінь від хребта -----
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Буря: " + StormNames[StormFx.DebugMode] + " →")) StormFx.DebugMode = (StormFx.DebugMode + 1) % StormNames.Length;
        if (GUILayout.Button("Тінь: до 17:00"))
        {
            int hours = (17 - s.Hour + 24) % 24;
            game.Clock.AdvanceHours(hours == 0 ? 24 : hours); // о 17:00 починає сутеніти, о 18:00 — тінь від хребта
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Аптечки -1")) s.Res.MedKits = Mathf.Max(0, s.Res.MedKits - 1);
        if (GUILayout.Button("Аптечки +1")) s.Res.MedKits++;
        GUILayout.Label("аптечок: " + s.Res.MedKits + ", кисню: " + Mathf.RoundToInt(s.Res.Oxygen), GUILayout.Width(170));
        GUILayout.EndHorizontal();

        // ----- Екіпаж: перемкнути завдання (поки CrewSystem.Assign від 04 не готовий) -----
        foreach (var a in s.Crew)
        {
            GUILayout.BeginHorizontal();
            string where = a.InsideSlot >= 0 ? "у модулі " + a.InsideSlot : "надворі";
            GUILayout.Label($"{a.Name}: {a.Task} {(a.AtTaskSite ? "✓" : "…")} {where}", GUILayout.Width(230));
            if (GUILayout.Button("завдання →")) NextTask(a);
            GUILayout.EndHorizontal();
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Екіпаж: тест іконок")) TestIcons(s);
        if (GUILayout.Button("Роботам енергія +1")) s.Power.Robots += 1;
        GUILayout.EndHorizontal();
    }

    static readonly string[] StormNames = { "як у грі", "попередження", "буря" };

    static readonly ModuleType[] RoomCycle =
    {
        ModuleType.Habitat, ModuleType.Greenhouse, ModuleType.Medbay, ModuleType.Lab, ModuleType.Shelter,
    };

    static ModuleType NextRoomType(ModuleType t)
    {
        int i = System.Array.IndexOf(RoomCycle, t);
        return RoomCycle[(i + 1) % RoomCycle.Length];
    }

    PowerPlan savedPlan; // план енергії до натискання «Енергія кімнатам: 0»

    /// <summary>
    /// Забрати енергію в усіх кімнат (житловий, теплиця, медмодуль, лабораторія, укриття) або повернути як було.
    /// Міняємо і план гравця, і Granted, щоб меблі відреагували одразу, не чекаючи наступної години.
    /// </summary>
    void TogglePower(GameBootstrap game)
    {
        var s = game.State;
        var power = game.GetSystem<PowerSystem>();
        if (savedPlan == null)
        {
            savedPlan = new PowerPlan();
            CopyRooms(s.Power, savedPlan);
            CopyRooms(new PowerPlan(), s.Power);
            if (power != null) CopyRooms(new PowerPlan(), power.Granted);
        }
        else
        {
            CopyRooms(savedPlan, s.Power);
            if (power != null) CopyRooms(savedPlan, power.Granted);
            savedPlan = null;
        }
        GameEvents.RaiseStateChanged();
    }

    /// <summary>+1 блок кожній кімнаті (і в плані, і в Granted — щоб меблі відреагували одразу). Батарея може не витягнути — це лише перевірка.</summary>
    static void AddRoomPower(GameBootstrap game)
    {
        var power = game.GetSystem<PowerSystem>();
        foreach (var p in new[] { game.State.Power, power != null ? power.Granted : null })
        {
            if (p == null) continue;
            p.LifeSupport++;
            p.Greenhouse++;
            p.Medbay++;
            p.Lab++;
            p.Shelter++;
        }
        GameEvents.RaiseStateChanged();
    }

    static readonly ModuleType[] RoomsForText = { ModuleType.Habitat, ModuleType.Greenhouse, ModuleType.Medbay, ModuleType.Lab, ModuleType.Shelter };
    static readonly string[] RoomShort = { "Жит", "Тепл", "Мед", "Лаб", "Укр" };

    /// <summary>Рядок для перевірки: що бачать меблі й світло в кожній кімнаті.</summary>
    static string RoomPowerText()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < RoomsForText.Length; i++)
        {
            var t = RoomsForText[i];
            var st = ModulePower.StateOf(t);
            string mark = st == RoomPower.On ? "" : (st == RoomPower.Standby ? " сон" : " АВАРІЯ");
            sb.Append(RoomShort[i]).Append(' ').Append(ModulePower.Blocks(t)).Append('/').Append(ModulePower.Planned(t)).Append(mark);
            if (i < RoomsForText.Length - 1) sb.Append(" · ");
        }
        return sb.ToString();
    }

    static void CopyRooms(PowerPlan from, PowerPlan to)
    {
        to.LifeSupport = from.LifeSupport;
        to.Greenhouse = from.Greenhouse;
        to.Medbay = from.Medbay;
        to.Lab = from.Lab;
        to.Shelter = from.Shelter;
    }

    static readonly CrewTask[] TaskCycle =
    {
        CrewTask.None, CrewTask.Rest, CrewTask.MineIce, CrewTask.CollectSamples,
        CrewTask.Greenhouse, CrewTask.Lab, CrewTask.Medbay, CrewTask.Heal, CrewTask.Shelter,
    };

    /// <summary>Наступне завдання по колу. TaskTarget порожній — світ сам знайде перше відповідне місце.</summary>
    static void NextTask(Astronaut a)
    {
        int i = System.Array.IndexOf(TaskCycle, a.Task);
        a.Task = TaskCycle[(i + 1) % TaskCycle.Length];
        a.TaskTarget = "";
        a.AtTaskSite = false;
        GameEvents.RaiseStateChanged();
    }

    /// <summary>По черзі вмикає «погані» стани, щоб побачити іконки над головами, і вимикає їх.</summary>
    static void TestIcons(GameState s)
    {
        bool on = s.Crew.Count > 0 && s.Crew[0].Health == HealthStatus.Ok;
        for (int i = 0; i < s.Crew.Count; i++)
        {
            var a = s.Crew[i];
            a.Health = on && i == 0 ? HealthStatus.Sick : (on && i == 1 ? HealthStatus.Severe : HealthStatus.Ok);
            a.InsideOnly = on && i == 2;
            a.Satiety = on && i == 3 ? 10f : Mathf.Max(a.Satiety, 60f);
        }
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

    /// <summary>
    /// Перевірка тунелів (роль 04), без витрат: готовий тунель між першою парою сусідніх готових кімнат у ряду.
    /// Якщо такої пари немає — ставить у вільні сусідні квадратики два готові житлові модулі й з'єднує їх.
    /// </summary>
    static void InstantTunnel(GameState s)
    {
        for (int slot = 0; slot < BaseGrid.Count; slot++)
        {
            if (BaseGrid.CanConnect(s, slot) != null) continue;
            s.Tunnels.Add(new TunnelState { SlotA = slot, BuildHoursLeft = 0f });
            GameEvents.RaiseStateChanged();
            Debug.Log("Тунель: з'єднано слоти " + slot + " і " + (slot + 1));
            return;
        }
        // пари кімнат немає — ставимо дві готові кімнати (спершу ряди ближче до середини бази)
        foreach (int row in new[] { 1, 2, 0, 3, 4, 5 })
            for (int col = 0; col < BaseGrid.Width - 1; col++)
            {
                int a = BaseGrid.ToSlot(col, row);
                if (!FreeOrRoom(s, a) || !FreeOrRoom(s, a + 1) || s.FindTunnel(a) != null) continue;
                foreach (int slot in new[] { a, a + 1 })
                {
                    var m = s.FindModule(slot);
                    if (m == null) s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = slot, BuildHoursLeft = 0f });
                    else m.BuildHoursLeft = 0f;
                }
                s.Tunnels.Add(new TunnelState { SlotA = a, BuildHoursLeft = 0f });
                GameEvents.RaiseStateChanged();
                Debug.Log("Тунель: поставлено кімнати в слотах " + a + " і " + (a + 1) + " та з'єднано тунелем");
                return;
            }
        Debug.Log("Тунель: немає місця для двох кімнат поруч.");
    }

    static bool FreeOrRoom(GameState s, int slot)
    {
        var m = s.FindModule(slot);
        return m == null || ModuleKinds.IsRoom(m.Type);
    }

    /// <summary>Перевірка насипу (роль 04): усім готовим кімнатам вмикає реголіт, а якщо всі вже засипані — вимикає.</summary>
    static void ToggleRegolith(GameState s)
    {
        bool anyBare = false;
        foreach (var m in s.Modules) if (m.IsBuilt && ModuleKinds.IsRoom(m.Type) && !m.RegolithShield) anyBare = true;
        foreach (var m in s.Modules) if (m.IsBuilt && ModuleKinds.IsRoom(m.Type)) m.RegolithShield = anyBare;
        GameEvents.RaiseStateChanged();
    }
}
