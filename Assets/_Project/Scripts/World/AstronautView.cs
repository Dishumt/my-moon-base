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
/// У кімнаті йде до «своїх» меблів і стає збоку обличчям до них: ботанік — до полиці з рослинами,
/// геолог у лабораторії — до шафи зі зразками, медик — до медшафи; відпочинок — лягає на ліжко (над ним «Zzz»),
/// тяжко хворий лежить на ліжку в медмодулі; без завдання — біля обіднього столу.
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
    SpriteRenderer body, icon, shadowRenderer;
    List<SpriteRenderer> bodyParts;
    Billboard bodyBillboard, iconBillboard;
    SpriteRenderer[] zzz;
    bool bodyVisible = true;

    // Меблі: до якого предмета йдемо і чи лягаємо
    FurnitureLife targetFurniture; // null — просто вільне місце в кімнаті
    bool targetLie;
    FurnitureLife bed;             // ліжко, на якому лежимо (або яке зайняли, поки йдемо)
    Vector3? claimedSpot;          // місце в кімнаті, яке ми зайняли (інші туди не стають)
    bool lying;
    Vector3 lyingHead;             // де голова лежачого (для «Zzz» та іконки)
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
        bodyBillboard = spriteGo.AddComponent<Billboard>();

        var iconGo = new GameObject("Status Icon");
        iconGo.transform.SetParent(transform, false);
        icon = iconGo.AddComponent<SpriteRenderer>();
        icon.sortingOrder = 2;
        iconBillboard = iconGo.AddComponent<Billboard>();
        iconBillboard.BillboardMode = Billboard.Mode.FaceCamera;

        hop = gameObject.AddComponent<MoonHop>(); // після спрайта: MoonHop шукає білборд для сплющення
        bodyParts = new List<SpriteRenderer>(GetComponentsInChildren<SpriteRenderer>(true));
        bodyParts.Remove(icon); // тіло й тінь; іконка — окремо
        shadowRenderer = bodyParts.Find(r => r != body);
        CreateZzz();

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
        var pos = transform.position;
        pos.y = MoonTerrain.HeightAt(pos); // по рельєфу: у кратер і з кратера (у базі земля рівна)
        transform.position = pos;

        Data.InsideSlot = BaseView.SlotAt(transform.position); // світ пише щокадру

        // У закритому модулі (гравець надворі) людину не видно — тільки іконку стану над дахом
        var room = Data.InsideSlot >= 0 ? BaseView.Find(Data.InsideSlot) : null;
        bool visible = room == null || room.InteriorOpen;
        bodyVisible = visible;
        foreach (var r in bodyParts) if (r != null) r.enabled = visible;
        hop.Step(moving, suit, dt);
        if (lying && shadowRenderer != null) shadowRenderer.enabled = false; // лежить на ліжку — тіні на підлозі немає
        RefreshSprite();
        RefreshIcon();
    }

    void LateUpdate()
    {
        if (lying && bed == null) StandUp(); // ліжко зникло (модуль перебудували)

        if (lying) PlaceLying();
        else
        {
            float h = hop != null ? hop.Height : 0f;
            float dx = shake > 0f ? Mathf.Sin(Time.time * 60f) * shake : 0f;
            if (body != null) body.transform.localPosition = new Vector3(dx, h, 0f);
            if (icon != null) icon.transform.localPosition = new Vector3(0f, 2.3f + h, 0f);
        }
        if (icon != null)
        {
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 4f); // іконка «дихає», щоб привертати увагу
            iconBillboard.ExtraScale = new Vector2(pulse, pulse);
        }
        UpdateZzz();
    }

    // ---------- План: куди йти ----------

    void Replan()
    {
        steps.Clear();
        stepStarted = false;
        shake = 0f;
        Data.AtTaskSite = false;
        var oldBed = lying ? bed : null; // уже лежимо — якщо план той самий, не встаємо
        StandUp();
        targetFurniture = null;
        targetLie = false;
        bed = null;
        claimedSpot = null;

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

        // Тунелі: кімната-ціль у тому ж ряду і всі тунелі між нами готові — ідемо всередині,
        // без шлюзу, скафандра й радіації (роль 04)
        var indoor = here != null && room != null && here != room ? IndoorRoute(here.SlotId, room.SlotId) : null;
        Vector3 roomPos0 = Vector3.zero;
        if (indoor != null)
        {
            int dir = room.SlotId > here.SlotId ? 1 : -1;
            for (int i = 0; i + 1 < indoor.Count; i++)
            {
                Add(Kind.Direct, indoor[i].SideDoorPoint(dir));
                Add(Kind.Direct, indoor[i + 1].SideDoorPoint(-dir));
            }
            Add(Kind.Direct, RoomTarget(room));
        }
        else if (here != null && here != room) // виходимо: тамбур → одягнути скафандр → надвір
        {
            var al = here.GetComponentInChildren<Airlock>();
            Add(Kind.Direct, here.InnerDoorPoint);
            Add(Kind.Direct, here.ChamberPoint);
            Add(Kind.SuitOn, here.ChamberPoint, suitTime, al);
            Add(Kind.Direct, here.OuterDoorPoint);
        }

        Vector3 roomPos = room != null && indoor == null ? RoomTarget(room) : roomPos0; // біля «своїх» меблів

        if (indoor != null) { } // уже дійдемо тунелями
        else if (room != null && here != room) // заходимо: тамбур → пил і зняти скафандр → кімната
        {
            var al = room.GetComponentInChildren<Airlock>();
            Add(Kind.Walk, room.OuterDoorPoint);
            Add(Kind.Direct, room.ChamberPoint);
            Add(Kind.SuitOff, room.ChamberPoint, dustTime + suitTime, al);
            Add(Kind.Direct, room.InnerDoorPoint);
            Add(Kind.Direct, roomPos);
        }
        else if (room != null) Add(Kind.Direct, roomPos); // уже в цій кімнаті
        else Add(Kind.Walk, outdoor.Value);

        if (oldBed != null && targetLie && bed == oldBed && here == room) // лежимо на тому ж ліжку — лишаємось
        {
            steps.Clear();
            lying = true;
            Data.AtTaskSite = goingToTask;
        }
    }

    /// <summary>
    /// Кімнати від from до to, якщо всі вони в одному ряду, усі — готові кімнати і між кожною парою є готовий тунель.
    /// Інакше null (тоді йдемо надвір через шлюзи, як раніше).
    /// </summary>
    static List<ModuleView> IndoorRoute(int from, int to)
    {
        var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        if (state == null || BaseGrid.Row(from) != BaseGrid.Row(to)) return null;
        int dir = to > from ? 1 : -1;
        var route = new List<ModuleView>();
        for (int slot = from; ; slot += dir)
        {
            var mv = BaseView.Find(slot);
            if (mv == null || !mv.IsEnterableRoom) return null;
            route.Add(mv);
            if (slot == to) return route;
            if (!state.HasBuiltTunnel(slot, slot + dir)) return null;
        }
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

        if (steps.Count == 0) // дійшов
        {
            Data.AtTaskSite = goingToTask;
            OnArrived();
        }
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

    // ---------- Меблі: біля чого стати, де лягти ----------

    /// <summary>Яку картинку меблів шукати в кімнаті цього типу для поточного завдання. lie — лягти на неї.</summary>
    string FurnitureFor(ModuleType roomType, out bool lie)
    {
        lie = false;
        if (Data.Health == HealthStatus.Severe && roomType == ModuleType.Medbay) { lie = true; return "bunk_bed"; } // тяжко хворий лежить
        switch (Data.Task)
        {
            case CrewTask.Rest:
                if (roomType == ModuleType.Habitat) { lie = true; return "bunk_bed"; }
                break;
            case CrewTask.Medbay: // пацієнт у медмодулі
                if (roomType == ModuleType.Medbay) { lie = true; return "bunk_bed"; }
                break;
            case CrewTask.Heal: return "medical_cabinet";
            case CrewTask.Greenhouse: return "hydroponic_rack";
            case CrewTask.Lab:
                if (Data.Role == CrewRole.Geologist) return "specimen_cabinet";
                return Data.Role == CrewRole.Medic ? "workstation_desk" : "science_bench";
            case CrewTask.Shelter: return "cargo_crates";
            case CrewTask.None:
                if (roomType == ModuleType.Habitat) return "dining_table";
                break;
        }
        return null;
    }

    /// <summary>Куди стати в кімнаті: біля «свого» предмета, а якщо його немає — на вільне місце.</summary>
    Vector3 RoomTarget(ModuleView room)
    {
        string kind = FurnitureFor(room.Type, out bool lie);
        var f = kind != null ? room.FindFurniture(kind, index) : null;
        if (f != null && lie && BedTaken(f)) // ліжко одне, а лягти хочуть двоє — другий іде до столу або медшафи
        {
            lie = false;
            f = room.FindFurniture(room.Type == ModuleType.Medbay ? "medical_cabinet" : "dining_table", index);
        }
        Vector3 spot;
        if (f != null && lie)
        {
            bed = f; // займаємо ліжко одразу, щоб інший не пішов туди ж
            spot = room.ClampInRoom(f.FrontPoint);
        }
        else if (f == null || !FreeSpotNear(room, f, out spot))
        {
            f = null; // біля предмета все зайнято — стаємо на вільне місце в кімнаті
            lie = false;
            spot = room.RoomSpot(index);
        }

        targetFurniture = f;
        targetLie = lie;
        claimedSpot = spot;
        return spot;
    }

    /// <summary>
    /// Перше вільне місце біля предмета: спершу збоку (щоб не закривати предмет), потім спереду.
    /// Місце не годиться, якщо воно за стіною (довелося б сильно підсунути) або там уже хтось стоїть чи йде туди.
    /// </summary>
    bool FreeSpotNear(ModuleView room, FurnitureLife f, out Vector3 spot)
    {
        int[] order = index % 2 == 0 ? new[] { 0, 1, 2, 3 } : new[] { 1, 0, 3, 2 }; // сусіди починають з різних боків
        foreach (int k in order)
        {
            Vector3 p = f.Spot(k);
            Vector3 c = room.ClampInRoom(p);
            if (Mathf.Abs(c.x - p.x) > 0.2f) continue; // за бічною стіною (біля задньої стіни просто підсуваємо вперед)
            if (SpotTaken(c)) continue;
            spot = c;
            return true;
        }
        spot = default;
        return false;
    }

    /// <summary>Чи хтось інший уже зайняв місце поруч (ближче 0,7 м — люди на картинках широкі).</summary>
    bool SpotTaken(Vector3 p)
    {
        foreach (var a in All)
        {
            if (a == this || !a.claimedSpot.HasValue) continue;
            Vector3 d = a.claimedSpot.Value - p;
            d.y = 0f;
            if (d.sqrMagnitude < 0.7f * 0.7f) return true;
        }
        return false;
    }

    bool BedTaken(FurnitureLife f)
    {
        foreach (var a in All) if (a != this && a.bed == f) return true;
        return false;
    }

    /// <summary>Дійшов: лягає на ліжко або повертається обличчям до свого предмета.</summary>
    void OnArrived()
    {
        if (targetFurniture == null) return;
        if (targetLie && bed != null)
        {
            lying = true;
            facing = new Vector2(0f, -1f); // обличчям до нас
            return;
        }
        Vector3 d = targetFurniture.FloorCenter - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) facing = CharacterSprites.ToScreen(d.normalized);
    }

    void StandUp()
    {
        lying = false;
        if (bodyBillboard == null) return;
        bodyBillboard.BillboardMode = Billboard.Mode.Upright;
        bodyBillboard.Roll = 0f;
        bodyBillboard.Size = 1f;
    }

    // Ліжко bunk_bed (пікселі картинки Юлі): ноги внизу ліворуч, подушка вгорі праворуч — людина лежить по діагоналі.
    // Ліжко збільшене (RoomBuilder.BedScale), тож людина лежить у звичайному розмірі.
    static readonly Vector2 BedFeet = new Vector2(84f, 157f);
    static readonly Vector2 BedHead = new Vector2(190f, 100f);
    const float LyingSize = 1f;
    const float TowardCamera = 0.6f;  // трохи ближче до камери, щоб людину було видно поверх ліжка

    /// <summary>Тіло лежить на матраці: картинка паралельно екрану, повернута головою до подушки.</summary>
    void PlaceLying()
    {
        if (body == null) return;
        Vector3 feet = bed.PixelToWorld(BedFeet.x, BedFeet.y);
        Vector3 head = bed.PixelToWorld(BedHead.x, BedHead.y);
        var cam = Camera.main;
        if (cam != null)
        {
            // Зсуваємо вздовж променя від камери: на екрані точка не рухається, а малюється поверх ліжка
            Vector3 c = cam.transform.position;
            feet += (c - feet).normalized * TowardCamera;
            head += (c - head).normalized * TowardCamera;
        }
        // Кут від ніг до голови на картинці: картинку тіла (голова вгорі) повертаємо на (кут − 90°)
        float angle = Mathf.Atan2(BedFeet.y - BedHead.y, BedHead.x - BedFeet.x) * Mathf.Rad2Deg;
        bodyBillboard.BillboardMode = Billboard.Mode.FaceCamera;
        bodyBillboard.Roll = angle - 90f;
        bodyBillboard.Size = LyingSize;
        body.transform.position = feet; // точка опори картинки — ноги
        lyingHead = head;
        if (icon != null) icon.transform.position = head + Vector3.up * 0.6f;
    }

    // ---------- «Zzz» над тим, хто спить ----------

    static Sprite zSprite;
    static readonly string[] ZRows = { "WWWWW", "...W.", "..W..", ".W...", "WWWWW" }; // рядки згори вниз

    static Sprite ZSprite => zSprite != null ? zSprite : (zSprite = WorldSprites.Make(5, 5, 5f, (x, y) =>
        ZRows[ZRows.Length - 1 - y][x] == 'W' ? Color.white : Color.clear));

    void CreateZzz()
    {
        zzz = new SpriteRenderer[3];
        for (int i = 0; i < zzz.Length; i++)
        {
            var go = new GameObject("Zzz " + i);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ZSprite;
            r.sortingOrder = 3;
            r.enabled = false;
            zzz[i] = r;
        }
    }

    /// <summary>Три літери Z по черзі спливають від голови й тануть (хворому — його іконка замість «Zzz»).</summary>
    void UpdateZzz()
    {
        var cam = Camera.main;
        bool show = lying && bodyVisible && cam != null && Data.Health != HealthStatus.Severe;
        for (int i = 0; i < zzz.Length; i++)
        {
            var r = zzz[i];
            r.enabled = show;
            if (!show) continue;
            float p = Mathf.Repeat(Time.time / 2.4f + i / 3f, 1f); // 0 — щойно з'явилась, 1 — розтанула
            Vector3 pos = lyingHead + cam.transform.right * (0.1f + p * 0.35f) + cam.transform.up * (0.25f + p * 0.7f);
            r.transform.SetPositionAndRotation(pos, cam.transform.rotation);
            float size = Mathf.Lerp(0.12f, 0.24f, p);
            r.transform.localScale = new Vector3(size, size, 1f);
            r.color = new Color(0.85f, 0.92f, 1f, Mathf.Sin(p * Mathf.PI));
        }
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
        else if (StormFx.Danger && Data.InsideSlot < 0) s = cl.iconDoseRed; // буря: надворі небезпечно — в укриття!
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
