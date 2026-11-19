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
        sb.Append(s.IcePlant.ProcessorBuilt ? 'P' : '-');
        foreach (var d in s.IcePlant.Drills) if (d.Built) sb.Append('D').Append(d.PoiId);
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
            else if (ModuleKinds.IsRoom(m.Type))
            {
                if (!v.IsEnterableRoom) shownKey = null; // кімната ще будує стіни — переробимо кабелі за секунду
                rooms.Add(v);
            }
        }
        if (hub == null) // батареї ще немає — центр мережі житловий модуль (або будь-яка кімната)
        {
            foreach (var r in rooms) if (r.Type == ModuleType.Habitat) { hub = r; break; }
            if (hub == null && rooms.Count > 0) hub = rooms[0];
        }
        if (hub == null) return;

        // Кабелі заходять у БОКОВІ стіни модулів (не біля дверей): від панелей — до батареї, від батареї — до кімнат
        foreach (var sv in solars)
            Add(Route(sv, hub, 0f, -0.8f), ModuleType.Solar, fromSun: true, toIce: false);
        foreach (var r in rooms)
            if (r != hub) Add(Route(hub, r, 0.8f, 0f), r.Type, fromSun: false, toIce: false); // кімната-центр живить себе сама

        // Переробник льоду і бур теж беруть струм з батареї
        var plant = FindAnyObjectByType<IcePlantView>();
        if (plant != null && s.IcePlant.ProcessorBuilt && plant.HasPositions)
        {
            Add(RouteTo(hub, plant.ProcessorCablePoint, 0.8f), ModuleType.Solar, fromSun: false, toIce: true);
            foreach (var d in s.IcePlant.Drills)   // до кожного збудованого бура
                if (d.Built && plant.TryDrillCablePoint(d.PoiId, out Vector3 drillPoint))
                    Add(WorldPipe.Straight(plant.ProcessorCablePoint, drillPoint, 0.12f), ModuleType.Solar, fromSun: false, toIce: true);
        }
    }

    void Add(List<Vector3> pts, ModuleType type, bool fromSun, bool toIce)
    {
        if (pts == null || pts.Count < 2) return;
        var pipe = new WorldPipe("Power Cable", pts, Radius, CableColor, Spark, blobSpacing: 3f);
        cables.Add(new Cable { Pipe = pipe, Type = type, FromSun = fromSun, ToIcePlant = toIce });
    }

    /// <summary>Кабель від бокової стіни модуля a до бокової стіни модуля b, проходами між модулями.</summary>
    static List<Vector3> Route(ModuleView a, ModuleView b, float alongA, float alongB)
    {
        Vector3 start = WorldPipe.ModuleSide(a, b.transform.position, alongA, 0.4f);
        Vector3 end = WorldPipe.ModuleSide(b, a.transform.position, alongB, 0.4f);
        var pts = Path(start, end);
        pts.Insert(0, WorldPipe.ModuleSide(a, b.transform.position, alongA, 0.08f));
        pts.Add(WorldPipe.ModuleSide(b, a.transform.position, alongB, 0.08f));
        var ground = WorldPipe.Densify(pts, 0.12f);
        ground.Insert(0, ground[0] + Vector3.up * 0.8f); // виходить зі стіни зверху
        WorldPipe.AddRiser(ground, 0.8f);                // і заходить у стіну зверху
        return ground;
    }

    /// <summary>Кабель від бокової стіни модуля до точки надворі (переробник льоду).</summary>
    static List<Vector3> RouteTo(ModuleView a, Vector3 end, float alongA)
    {
        Vector3 start = WorldPipe.ModuleSide(a, end, alongA, 0.4f);
        var pts = Path(start, end);
        pts.Insert(0, WorldPipe.ModuleSide(a, end, alongA, 0.08f));
        var ground = WorldPipe.Densify(pts, 0.12f);
        ground.Insert(0, ground[0] + Vector3.up * 0.8f);
        return ground;
    }

    /// <summary>Шлях по землі в обхід модулів (як ходять астронавти).</summary>
    static List<Vector3> Path(Vector3 from, Vector3 to)
    {
        var path = BaseView.PathTo(from, to, 0.05f);
        var pts = new List<Vector3> { from };
        pts.AddRange(path);
        if ((pts[pts.Count - 1] - to).sqrMagnitude > 0.01f) pts.Add(to);
        return pts;
    }

    void Clear()
    {
        foreach (var c in cables) c.Pipe?.Destroy();
        cables.Clear();
    }

    void OnDestroy() => Clear();
}
