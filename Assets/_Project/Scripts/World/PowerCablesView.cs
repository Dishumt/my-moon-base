// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Кабелі енергії на землі бази, щоб дитина бачила, звідки береться струм:
///  сонячні панелі → батарея (або житловий модуль, якщо батареї ще немає) → кожна кімната,
///  і ще → переробник льоду → бур.
/// По кабелю біжать жовті іскорки, коли струм іде; червоні — аварія (енергію дали, але її забракло);
/// немає іскорок — кімната вимкнена (гравець не дав їй енергії) або панелі в тіні.
/// Це тільки показ: скільки енергії, рахує PowerSystem (03). Кабелі прокладаються самі.
/// Нічого не треба ставити в сцену: створюється сам при старті гри.
/// </summary>
public class PowerCablesView : MonoBehaviour
{
    static readonly Color CableColor = new Color(0.16f, 0.16f, 0.18f);
    static readonly Color Spark = new Color(1f, 0.9f, 0.25f);
    static readonly Color SparkAlarm = new Color(1f, 0.2f, 0.15f);
    const float Radius = 0.06f;

    class Cable
    {
        public WorldPipe Pipe;
        public ModuleType Type;    // що живить: тип кімнати (для ModulePower)
        public bool FromSun;       // панель → батарея: струм іде, коли сонце дає енергію
        public bool ToIcePlant;    // до переробника / бура
    }

    readonly List<Cable> cables = new List<Cable>();
    string shownKey;
    GameState seenState;
    float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<PowerCablesView>() != null) return;
        var go = new GameObject("Power Cables");
        DontDestroyOnLoad(go);
        go.AddComponent<PowerCablesView>();
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || ModuleLook.Instance == null) { Clear(); return; }
        var s = game.State;

        // Раз на секунду: чи змінився набір модулів (або сцену перезавантажили)
        bool lost = cables.Count > 0 && (cables[0].Pipe == null || cables[0].Pipe.Root == null);
        if (s != seenState || lost || Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 1f;
            string key = Key(s);
            if (key != shownKey || s != seenState || lost)
            {
                seenState = s;
                shownKey = key;
                Rebuild(s);
            }
        }

        var power = game.GetSystem<PowerSystem>();
        var ice = game.GetSystem<IcePlantSystem>();
        bool sun = power != null && power.LastSupplyBlocks > 0.01f && !ModulePower.DebugOutage;
        foreach (var c in cables)
        {
            if (c.Pipe == null) continue;
            if (c.FromSun) { c.Pipe.Animate(sun, 3f); continue; }
            if (c.ToIcePlant) { c.Pipe.Animate(ice != null && (ice.ProcessorWorking || ice.DrillWorking), 3f); continue; }
            var state = ModulePower.StateOf(c.Type);
            bool alarm = state == RoomPower.Outage && Mathf.Repeat(Time.time, 1f) < 0.5f;
            c.Pipe.SetFlowColor(state == RoomPower.Outage ? SparkAlarm : Spark);
            c.Pipe.Animate(state == RoomPower.On || alarm, 3f);
        }
    }

    /// <summary>«Підпис» бази: які модулі збудовані + бур/переробник. Змінився — прокладаємо кабелі заново.</summary>
    static string Key(GameState s)
    {
        var sb = new StringBuilder();
        var plant = FindAnyObjectByType<IcePlantView>();
        sb.Append(plant != null && plant.HasPositions ? 'H' : '-');
        foreach (var m in s.Modules)
            if (m.IsBuilt) sb.Append(m.SlotId).Append(':').Append((int)m.Type).Append(';');
        sb.Append(s.IcePlant.ProcessorBuilt ? 'P' : '-').Append(s.IcePlant.DrillBuilt ? 'D' : '-');
        return sb.ToString();
    }

    void Rebuild(GameState s)
    {
        Clear();
        ModuleView hub = null;
        var solars = new List<ModuleView>();
        var rooms = new List<ModuleView>();
        foreach (var m in s.Modules)
        {
            if (!m.IsBuilt) continue;
            var v = BaseView.Find(m.SlotId);
            if (v == null) { shownKey = null; continue; } // модуль ще не намальований — спробуємо за секунду
            if (m.Type == ModuleType.Solar) solars.Add(v);
            else if (m.Type == ModuleType.Battery) { if (hub == null || hub.Type != ModuleType.Battery) hub = v; }
            else if (ModuleKinds.IsRoom(m.Type)) rooms.Add(v);
        }
        if (hub == null) // батареї ще немає — центр мережі житловий модуль (або будь-яка кімната)
        {
            foreach (var r in rooms) if (r.Type == ModuleType.Habitat) { hub = r; break; }
            if (hub == null && rooms.Count > 0) hub = rooms[0];
        }
        if (hub == null) return;

        Vector3 hubIn = Socket(hub, 3);   // сюди приходять кабелі від панелей (праворуч від дверей)
        Vector3 hubOut = Socket(hub, 0);  // звідси йдуть до кімнат (ліворуч від дверей)
        foreach (var sv in solars)
            Add(Route(Socket(sv, 0), hubIn), ModuleType.Solar, fromSun: true, toIce: false);
        foreach (var r in rooms)
            if (r != hub) Add(Route(hubOut, Socket(r, 3)), r.Type, fromSun: false, toIce: false); // кімната-центр живить себе сама

        // Переробник льоду і бур теж беруть струм з батареї
        var plant = FindAnyObjectByType<IcePlantView>();
        if (plant != null && s.IcePlant.ProcessorBuilt && plant.HasPositions)
        {
            Add(Route(hubOut, plant.ProcessorCablePoint), ModuleType.Solar, fromSun: false, toIce: true);
            if (s.IcePlant.DrillBuilt)
                Add(WorldPipe.Straight(plant.ProcessorCablePoint, plant.DrillCablePoint, 0.12f), ModuleType.Solar, fromSun: false, toIce: true);
        }
    }

    void Add(List<Vector3> pts, ModuleType type, bool fromSun, bool toIce)
    {
        if (pts == null || pts.Count < 2) return;
        var pipe = new WorldPipe("Power Cable", pts, Radius, CableColor, Spark, blobSpacing: 3f);
        cables.Add(new Cable { Pipe = pipe, Type = type, FromSun = fromSun, ToIcePlant = toIce });
    }

    /// <summary>Точка біля модуля, куди підходить кабель: перед модулем, збоку від дверей (i = 0..3).</summary>
    static Vector3 Socket(ModuleView v, int i) => v.FrontPoint(i) + v.transform.forward * 0.6f;

    /// <summary>Кабель проходами між модулями (як ходять астронавти), по землі.</summary>
    static List<Vector3> Route(Vector3 from, Vector3 to)
    {
        var path = BaseView.PathTo(from, to, 0.05f);
        var pts = new List<Vector3> { from };
        pts.AddRange(path);
        if ((pts[pts.Count - 1] - to).sqrMagnitude > 0.01f) pts.Add(to);
        return WorldPipe.Densify(pts, 0.12f);
    }

    void Clear()
    {
        foreach (var c in cables) c.Pipe?.Destroy();
        cables.Clear();
    }

    void OnDestroy() => Clear();
}
