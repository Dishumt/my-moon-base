// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один астронавт у світі. Читає з GameState його завдання (Task, TaskTarget, GoingHome) і веде туди:
///  - надворі — в обхід модулів (BaseView.PathTo), місячними стрибками (MoonHop);
///  - у кімнату — через шлюз: двері відчиняються, у тамбурі пил і зняти скафандр; вихід — одягнути скафандр;
///  - до точки на поверхні (лід, зразки) — до її PoiMarker.
/// Пише в GameState (так домовлено в CLAUDE.md): InsideSlot — щокадру, AtTaskSite — коли дійшов.
/// Над головою — іконка, тільки коли щось не так (голодний, хворий, радіація).
/// Створює його CrewView сам — руками в сцену не став.
/// </summary>
public class AstronautView : MonoBehaviour
{
    /// <summary>Усі астронавти у світі (шлюзи відчиняють їм двері).</summary>
    public static readonly List<AstronautView> All = new List<AstronautView>();

    enum Kind { Walk, Direct, SuitOff, SuitOn }   // Walk — в обхід модулів; Direct — по прямій (у шлюзі)
    struct Step
    {
        public Kind kind;
        public Vector3 pos;
        public float seconds;
        public Airlock airlock;
    }

    public Astronaut Data { get; private set; }
    int index;
    SpriteRenderer body, icon;
    Billboard iconBillboard;
    MoonHop hop;
    bool suit = true;
    Vector2 facing = new Vector2(0f, -1f);
    float shake;

    readonly Queue<Step> steps = new Queue<Step>();
    List<Vector3> path;
    int pathIndex;
    bool stepStarted;
    float waitLeft;
    string planKey;
    float replanTimer;
    bool goingToTask;

    /// <summary>Викликає CrewView одразу після створення.</summary>
    public void Init(Astronaut a, int index)
    {
        Data = a;
        this.index = index;

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(transform, false);
        body = spriteGo.AddComponent<SpriteRenderer>();
        spriteGo.AddComponent<Billboard>();

        var iconGo = new GameObject("Status Icon");
        iconGo.transform.SetParent(transform, false);
        icon = iconGo.AddComponent<SpriteRenderer>();
        icon.sortingOrder = 2;
        iconBillboard = iconGo.AddComponent<Billboard>();
        iconBillboard.BillboardMode = Billboard.Mode.FaceCamera;

        hop = gameObject.AddComponent<MoonHop>(); // після спрайта: MoonHop шукає білборд для сплющення

        // Звідки починаємо: у модулі (якщо збережена гра так каже) або на «площі» в центрі бази
        var room = a.InsideSlot >= 0 ? BaseView.Find(a.InsideSlot) : null;
        if (room != null && room.IsEnterableRoom) { transform.position = room.RoomSpot(index); suit = false; }
        else transform.position = IdleSpot();
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || Data == null) return;
        bool paused = game.Clock != null && game.Clock.IsPaused;
        float dt = paused ? 0f : Time.deltaTime;

        // Нове завдання — новий план. Без плану й не на місці — час від часу пробуємо знову
        // (наприклад, потрібну теплицю щойно добудували).
        string key = Data.Task + "|" + Data.TaskTarget + "|" + Data.GoingHome;
        replanTimer -= dt;
        if (key != planKey || (steps.Count == 0 && !Data.AtTaskSite && replanTimer <= 0f))
        {
            planKey = key;
            replanTimer = 2f;
            Replan();
        }

        bool moving = DoSteps(dt);

        Data.InsideSlot = BaseView.SlotAt(transform.position); // світ пише щокадру
        hop.Step(moving, suit, dt);
        RefreshSprite();
        RefreshIcon();
    }

    void LateUpdate()
    {
        float h = hop != null ? hop.Height : 0f;
        float dx = shake > 0f ? Mathf.Sin(Time.time * 60f) * shake : 0f;
        if (body != null) body.transform.localPosition = new Vector3(dx, h, 0f);
        if (icon != null)
        {
            icon.transform.localPosition = new Vector3(0f, 2.3f + h, 0f);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f); // іконка «дихає», щоб привертати увагу
            iconBillboard.ExtraScale = new Vector2(pulse, pulse);
        }
    }

    // ---------- План: куди йти ----------

    void Replan()
    {
        steps.Clear();
        stepStarted = false;
        shake = 0f;
        Data.AtTaskSite = false;

        ModuleView room = null;
        Vector3? outdoor = null;
        goingToTask = Data.Task != CrewTask.None || Data.GoingHome;

        if (Data.GoingHome)
        {
            var pad = FindPoi(PoiType.LandingPad); // летить додому з найближчою поставкою
            if (pad != null) outdoor = pad.position + Ring(2f);
        }
        else
        {
            // TaskTarget: номер квадратика модуля або id точки на поверхні
            if (!string.IsNullOrEmpty(Data.TaskTarget))
            {
                if (int.TryParse(Data.TaskTarget, out int slot))
                {
                    var mv = BaseView.Find(slot);
                    if (mv != null) { if (mv.IsEnterableRoom) room = mv; else outdoor = mv.FrontPoint(index); }
                }
                else
                {
                    var marker = PoiMarker.Find(Data.TaskTarget);
                    if (marker != null) outdoor = marker.transform.position + Ring(1.5f);
                }
            }
            // Ціль не вказана — беремо перше відповідне місце
            if (room == null && outdoor == null)
            {
                switch (Data.Task)
                {
                    case CrewTask.None:
                    case CrewTask.Rest: room = BaseView.FindRoom(ModuleType.Habitat); break;
                    case CrewTask.Greenhouse: room = BaseView.FindRoom(ModuleType.Greenhouse); break;
                    case CrewTask.Lab: room = BaseView.FindRoom(ModuleType.Lab); break;
                    case CrewTask.Heal:
                    case CrewTask.Medbay: room = BaseView.FindRoom(ModuleType.Medbay); break;
                    case CrewTask.Shelter: room = BaseView.FindRoom(ModuleType.Shelter); break;
                    case CrewTask.MineIce: { var p = FindPoi(PoiType.Ice); if (p != null) outdoor = p.position + Ring(1.5f); break; }
                    case CrewTask.CollectSamples: { var p = FindPoi(PoiType.Sample); if (p != null) outdoor = p.position + Ring(1.5f); break; }
                }
            }
        }
        if (room == null && outdoor == null) { outdoor = IdleSpot(); goingToTask = false; } // нема куди — чекаємо на «площі»

        // Де ми зараз
        var here = Data.InsideSlot >= 0 ? BaseView.Find(Data.InsideSlot) : null;
        if (here != null && !here.IsEnterableRoom) here = null;
        var look = ModuleLook.Instance;
        float suitTime = look != null ? look.suitSeconds : 1f;
        float dustTime = look != null ? look.dustSeconds : 1.5f;

        if (here != null && here != room) // виходимо: тамбур → одягнути скафандр → надвір
        {
            var al = here.GetComponentInChildren<Airlock>();
            Add(Kind.Direct, here.InnerDoorPoint);
            Add(Kind.Direct, here.ChamberPoint);
            Add(Kind.SuitOn, here.ChamberPoint, suitTime, al);
            Add(Kind.Direct, here.OuterDoorPoint);
        }

        if (room != null && here != room) // заходимо: тамбур → пил і зняти скафандр → кімната
        {
            var al = room.GetComponentInChildren<Airlock>();
            Add(Kind.Walk, room.OuterDoorPoint);
            Add(Kind.Direct, room.ChamberPoint);
            Add(Kind.SuitOff, room.ChamberPoint, dustTime + suitTime, al);
            Add(Kind.Direct, room.InnerDoorPoint);
            Add(Kind.Direct, room.RoomSpot(index));
        }
        else if (room != null) Add(Kind.Direct, room.RoomSpot(index)); // уже в цій кімнаті
        else Add(Kind.Walk, outdoor.Value);
    }

    void Add(Kind kind, Vector3 pos, float seconds = 0f, Airlock airlock = null)
    {
        steps.Enqueue(new Step { kind = kind, pos = pos, seconds = seconds, airlock = airlock });
    }

    // ---------- Виконання плану ----------

    /// <summary>Робить поточний крок плану. Повертає true, якщо астронавт зараз іде.</summary>
    bool DoSteps(float dt)
    {
        if (steps.Count == 0) return false;
        var st = steps.Peek();
        var cl = CrewLook.Instance;
        float speed = cl == null ? 3f : (suit && st.kind == Kind.Walk ? cl.crewSpeed : cl.crewIndoorSpeed);
        bool moving = false;

        switch (st.kind)
        {
            case Kind.Walk:
                if (!stepStarted) { path = BaseView.PathTo(transform.position, st.pos); pathIndex = 0; stepStarted = true; }
                if (pathIndex < path.Count)
                {
                    if (MoveTo(path[pathIndex], speed, dt, out moving)) pathIndex++;
                }
                else NextStep();
                break;

            case Kind.Direct:
                if (MoveTo(st.pos, speed, dt, out moving)) NextStep();
                break;

            case Kind.SuitOff:
            case Kind.SuitOn:
                if (!stepStarted)
                {
                    stepStarted = true;
                    waitLeft = st.seconds;
                    if (st.kind == Kind.SuitOff && st.airlock != null) st.airlock.BlowDust(st.seconds * 0.6f);
                }
                waitLeft -= dt;
                shake = st.kind == Kind.SuitOff && waitLeft > st.seconds * 0.4f ? 0.04f : 0f; // трясеться під пилом
                if (waitLeft <= 0f)
                {
                    suit = st.kind == Kind.SuitOn;
                    shake = 0f;
                    NextStep();
                }
                facing = new Vector2(0f, -1f);
                break;
        }

        if (steps.Count == 0) Data.AtTaskSite = goingToTask; // дійшов
        return moving;
    }

    void NextStep()
    {
        steps.Dequeue();
        stepStarted = false;
    }

    /// <summary>Крок до точки. true — дійшли. moving — чи рухались у цьому кадрі.</summary>
    bool MoveTo(Vector3 target, float speed, float dt, out bool moving)
    {
        Vector3 d = target - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        moving = false;
        if (dist < 0.05f) return true;
        if (dt <= 0f) return false;

        float stepLen = Mathf.Min(dist, speed * dt);
        transform.position += d / dist * stepLen;
        facing = CharacterSprites.ToScreen(d / dist);
        moving = true;
        return false;
    }

    // ---------- Картинка й іконка ----------

    void RefreshSprite()
    {
        var cl = CrewLook.Instance;
        var role = cl != null ? cl.Find(Data.Role) : null;
        if (role == null || body == null) return;

        var set = suit ? role.suit : role.indoor;
        bool missing = !suit && CharacterSprites.IsEmpty(role.indoor);
        if (missing) set = role.suit;
        body.color = missing ? new Color(0.7f, 0.85f, 1f) : Color.white;

        var frame = CharacterSprites.Pick(set, facing, hop, out bool flip);
        if (frame != null) body.sprite = frame;
        body.flipX = flip;
    }

    void RefreshIcon()
    {
        var cl = CrewLook.Instance;
        if (icon == null || cl == null) return;
        Sprite s = null;
        if (Data.Health == HealthStatus.Severe) s = cl.iconSevere;
        else if (Data.Health == HealthStatus.Sick) s = cl.iconSick;
        else if (Data.GoingHome) s = cl.iconDoseRed;
        else if (Data.InsideOnly) s = cl.iconDoseYellow;
        else if (Data.Satiety < cl.hungryBelow) s = cl.iconHungry;
        icon.sprite = s;
        icon.enabled = s != null;
    }

    // ---------- Дрібниці ----------

    /// <summary>Місце біля цілі для цього астронавта (по колу, щоб не стояли один в одному).</summary>
    Vector3 Ring(float radius)
    {
        float a = (index * 90f + 45f) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
    }

    Vector3 IdleSpot()
    {
        var cl = CrewLook.Instance;
        return (cl != null ? cl.crewIdleSpot : Vector3.zero) + Ring(1.6f);
    }

    /// <summary>Точка на поверхні потрібного типу (з GameState + її PoiMarker у сцені).</summary>
    Transform FindPoi(PoiType type)
    {
        var s = GameBootstrap.Instance.State;
        foreach (var p in s.Pois)
        {
            if (p.Type != type) continue;
            var m = PoiMarker.Find(p.Id);
            if (m != null) return m.transform;
        }
        return null;
    }
}
