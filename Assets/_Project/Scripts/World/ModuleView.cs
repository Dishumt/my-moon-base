// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Малює один модуль у квадратику сітки бази, читаючи GameState.
/// Створює його BaseView сам — руками в сцену не став.
///  - модуля немає → порожньо;
///  - будується (BuildHoursLeft > 0) → напівпрозоре риштування, яке росте з прогресом;
///  - готова кімната → коробка: підлога, низькі стіни, шлюз з двома дверима, дах окремим об'єктом;
///  - готове обладнання (панель, батарея, антена...) → проста фігура без входу.
/// Усе будує код із простих кубів і циліндрів, тож вручну збирати не треба.
/// Коли 02 намалює текстури, їх просто ставимо в матеріали ModuleLook.
/// </summary>
public class ModuleView : MonoBehaviour
{
    enum Shown { Nothing, Building, Built }

    /// <summary>Номер квадратика (BaseGrid), який малює цей об'єкт.</summary>
    public int SlotId { get; private set; } = -1;
    Vector2 size;              // розмір модуля в метрах (з ModuleLook)

    Shown shown = Shown.Nothing;
    ModuleType shownType;
    GameObject visual;         // усе, що зараз намальовано
    Transform scaffold;        // риштування (тільки під час будови)
    float totalBuildHours;     // найбільше BuildHoursLeft, що ми бачили = 100% роботи

    /// <summary>Викликає BaseView одразу після створення.</summary>
    public void Init(int slotId)
    {
        SlotId = slotId;
        size = ModuleLook.Instance != null ? ModuleLook.Instance.moduleSize : new Vector2(6f, 6f);
        transform.position = BaseGrid.ToWorld(slotId);
    }

    /// <summary>Чи точка світу всередині модуля: у кімнаті або в шлюзі (не в проході).</summary>
    public bool Contains(Vector3 worldPos)
    {
        Vector3 p = transform.InverseTransformPoint(worldPos);
        if (float.IsNaN(roomFront)) // обладнання або будова: просто прямокутник
            return Mathf.Abs(p.x) < size.x * 0.5f && Mathf.Abs(p.z) < size.y * 0.5f;
        bool inRoom = Mathf.Abs(p.x) < size.x * 0.5f && p.z > roomFront && p.z < roomBack;
        bool inAirlock = Mathf.Abs(p.x) < airlockHalfWidth && p.z > airlockFront && p.z <= roomFront;
        return inRoom || inAirlock;
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || SlotId < 0) return;

        var m = game.State.FindModule(SlotId);
        Shown want = m == null ? Shown.Nothing : (m.IsBuilt ? Shown.Built : Shown.Building);

        if (want != shown || (m != null && m.Type != shownType)) Rebuild(want, m);

        if (want == Shown.Building) UpdateScaffold(m);
        if (m != null) m.EngineerNearby = want == Shown.Building && EngineerIsNear();
    }

    static Transform engineer;

    /// <summary>
    /// Чи інженер стоїть біля будови (у її квадратику сітки, разом із проходом).
    /// Світ пише це в ModuleState.EngineerNearby — тоді роботи будують швидше (рахує 03).
    /// </summary>
    bool EngineerIsNear()
    {
        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return false;
            engineer = go.transform;
        }
        Vector3 d = engineer.position - transform.position;
        float half = BaseGrid.CellSize * 0.5f + 1f; // квадратик + 1 м навколо
        return Mathf.Abs(d.x) < half && Mathf.Abs(d.z) < half;
    }

    void Rebuild(Shown want, ModuleState m)
    {
        if (visual != null) Destroy(visual);
        visual = null;
        scaffold = null;
        roomFront = float.NaN;
        shown = want;
        if (m != null) shownType = m.Type;
        if (want == Shown.Nothing) { totalBuildHours = 0f; return; }

        var look = ModuleLook.Instance;
        if (look == null)
        {
            Debug.LogWarning("ModuleView: у сцені немає ModuleLook (об'єкт World).");
            return;
        }

        visual = new GameObject(want == Shown.Built ? "Module " + m.Type : "Construction " + m.Type);
        visual.transform.SetParent(transform, false);

        if (want == Shown.Building) BuildScaffold(look, m.Type);
        else if (ModuleKinds.IsRoom(m.Type)) BuildRoom(look, m.Type);
        else BuildEquipment(look, m.Type);
    }

    // ---------- Будова ----------

    void BuildScaffold(ModuleLook look, ModuleType type)
    {
        var c = look.ColorOf(type);
        c.a = 0.5f; // напівпрозоре риштування кольору майбутнього модуля
        scaffold = ModuleParts.Box(visual.transform, "Scaffold", look.roofMaterial, Vector3.zero, Vector3.one,
                                   collider: false, tint: c).transform;
    }

    void UpdateScaffold(ModuleState m)
    {
        if (scaffold == null) return;
        totalBuildHours = Mathf.Max(totalBuildHours, m.BuildHoursLeft);
        float progress = totalBuildHours > 0f ? 1f - m.BuildHoursLeft / totalBuildHours : 0f;

        float h = Mathf.Lerp(0.1f, ModuleLook.Instance.wallHeight, progress);
        scaffold.localScale = new Vector3(size.x, h, size.y);
        scaffold.localPosition = new Vector3(0f, h * 0.5f, 0f);
    }

    // ---------- Готова кімната ----------
    //
    //  Вигляд згори (камера знизу). Шлюз — окремий тамбур НАЗОВНІ, перед кімнатою:
    //   ┌────────────────┐  задня стіна
    //   │    кімната     │
    //   └──────╴ ╶───────┘  передня стіна з внутрішніми дверима
    //       │ шлюз  │
    //       └──╴ ╶──┘       зовнішні двері
    //
    // Кімната (глибина size.y - 1) зсунута назад, шлюз (airlockDepth) стирчить уперед;
    // разом це займає квадратик 8 м і лишає прохід між модулями.

    float roomFront = float.NaN, roomBack, airlockFront, airlockHalfWidth; // для Contains

    void BuildRoom(ModuleLook look, ModuleType type)
    {
        Transform root = visual.transform;
        float w = size.x, h = look.wallHeight, t = look.wallThickness;
        var wall = look.wallMaterial;
        Color typeColor = look.ColorOf(type);

        float roomDepth = size.y - 1f;
        roomBack = size.y / 2 + 0.5f;
        roomFront = roomBack - roomDepth;
        airlockFront = roomFront - look.airlockDepth;
        float roomMid = (roomFront + roomBack) / 2, airMid = (airlockFront + roomFront) / 2;

        float door = Mathf.Min(look.doorWidth, w - 4 * t);
        float aw = door + 1f;               // ширина шлюзу
        airlockHalfWidth = aw / 2;
        float ah = h * 0.85f;               // шлюз трохи нижчий за кімнату

        // ----- Кімната -----
        ModuleParts.Box(root, "Floor", wall, new Vector3(0f, 0.05f, roomMid), new Vector3(w, 0.1f, roomDepth),
                        collider: false, tint: Color.Lerp(typeColor, Color.white, 0.6f));
        ModuleParts.Box(root, "Wall Back", wall, new Vector3(0f, h / 2, roomBack - t / 2), new Vector3(w, h, t));
        ModuleParts.Box(root, "Wall Left", wall, new Vector3(-w / 2 + t / 2, h / 2, roomMid), new Vector3(t, h, roomDepth));
        ModuleParts.Box(root, "Wall Right", wall, new Vector3(w / 2 - t / 2, h / 2, roomMid), new Vector3(t, h, roomDepth));
        WallWithGap(root, "Wall Front", wall, roomFront + t / 2, -w / 2, w / 2, door, h, t);
        var roof = ModuleParts.Box(root, "Roof", look.roofMaterial, new Vector3(0f, h + 0.075f, roomMid), new Vector3(w + 0.2f, 0.15f, roomDepth + 0.2f),
                                   collider: false, tint: typeColor);

        // ----- Шлюз (тамбур назовні) -----
        Color airColor = new Color(0.62f, 0.64f, 0.68f);
        ModuleParts.Box(root, "Airlock Floor", wall, new Vector3(0f, 0.05f, airMid), new Vector3(aw, 0.1f, look.airlockDepth),
                        collider: false, tint: new Color(0.5f, 0.5f, 0.53f));
        ModuleParts.Box(root, "Airlock Side L", wall, new Vector3(-aw / 2 + t / 2, ah / 2, airMid), new Vector3(t, ah, look.airlockDepth), tint: airColor);
        ModuleParts.Box(root, "Airlock Side R", wall, new Vector3(aw / 2 - t / 2, ah / 2, airMid), new Vector3(t, ah, look.airlockDepth), tint: airColor);
        WallWithGap(root, "Airlock Front", wall, airlockFront + t / 2, -aw / 2, aw / 2, door, ah, t, airColor);
        var airRoof = ModuleParts.Box(root, "Airlock Roof", look.roofMaterial, new Vector3(0f, ah + 0.075f, airMid),
                                      new Vector3(aw + 0.2f, 0.15f, look.airlockDepth), collider: false, tint: airColor);

        // ----- Двері (зачинені; відчиняє Airlock) -----
        Color doorColor = new Color(0.35f, 0.38f, 0.42f);
        var outerDoor = DoubleDoor(root, "Door Outer", wall, new Vector3(0f, ah / 2, airlockFront + t / 2), door, ah - 0.1f, t * 0.6f, doorColor);
        var innerDoor = DoubleDoor(root, "Door Inner", wall, new Vector3(0f, h / 2, roomFront + t / 2), door, h - 0.1f, t * 0.6f, doorColor);

        // ----- Світло всередині (вмикається, коли інженер заходить) -----
        var lightGo = new GameObject("Interior Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, h - 0.3f, roomMid);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = look.interiorLightColor;
        light.range = Mathf.Max(w, roomDepth) * 1.2f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;

        visual.AddComponent<ModuleInterior>().Setup(this,
            new[] { roof.GetComponent<Renderer>(), airRoof.GetComponent<Renderer>() },
            new[] { typeColor, airColor }, light);

        // front = зовнішній край шлюзу, inner = середина внутрішніх дверей
        visual.AddComponent<Airlock>().Setup(outerDoor.transform, innerDoor.transform, door, aw,
                                             airlockFront, roomFront + t / 2, ah);
    }

    /// <summary>
    /// Двері з двох половинок (як у ліфті). Airlock стискає їх до країв отвору,
    /// тож вони не виїжджають за стіну.
    /// </summary>
    static GameObject DoubleDoor(Transform root, string name, Material mat, Vector3 center, float width, float height, float thick, Color color)
    {
        var door = new GameObject(name);
        door.transform.SetParent(root, false);
        door.transform.localPosition = center;
        float half = width / 2;
        ModuleParts.Box(door.transform, "L", mat, new Vector3(-half / 2, 0f, 0f), new Vector3(half, height, thick), tint: color);
        ModuleParts.Box(door.transform, "R", mat, new Vector3(half / 2, 0f, 0f), new Vector3(half, height, thick), tint: color);
        return door;
    }

    /// <summary>Стіна вздовж X від x0 до x1 на висоті z, з отвором ширини gap посередині.</summary>
    static void WallWithGap(Transform root, string name, Material mat, float z, float x0, float x1, float gap, float h, float t,
                            Color? tint = null)
    {
        float mid = (x0 + x1) / 2;
        float partL = mid - gap / 2 - x0, partR = x1 - (mid + gap / 2);
        if (partL > 0.01f) ModuleParts.Box(root, name + " L", mat, new Vector3(x0 + partL / 2, h / 2, z), new Vector3(partL, h, t), tint: tint);
        if (partR > 0.01f) ModuleParts.Box(root, name + " R", mat, new Vector3(x1 - partR / 2, h / 2, z), new Vector3(partR, h, t), tint: tint);
    }

    // ---------- Готове обладнання (без входу) ----------

    void BuildEquipment(ModuleLook look, ModuleType type)
    {
        Transform root = visual.transform;
        var mat = look.wallMaterial;
        Color c = look.ColorOf(type);
        Color metal = new Color(0.6f, 0.6f, 0.62f);

        // Бетонна площадка під усім обладнанням
        ModuleParts.Box(root, "Pad", mat, new Vector3(0f, 0.05f, 0f), new Vector3(size.x * 0.8f, 0.1f, size.y * 0.8f),
                        collider: false, tint: new Color(0.5f, 0.5f, 0.52f));

        switch (type)
        {
            case ModuleType.Solar:
                // Дві похилі панелі на ніжках
                for (int i = -1; i <= 1; i += 2)
                {
                    float x = i * size.x * 0.22f;
                    ModuleParts.Box(root, "Leg", mat, new Vector3(x, 0.6f, 0f), new Vector3(0.15f, 1.2f, 0.15f), tint: metal);
                    var panel = ModuleParts.Box(root, "Panel", mat, new Vector3(x, 1.25f, 0f), new Vector3(size.x * 0.4f, 0.08f, size.y * 0.6f),
                                                collider: false, tint: c);
                    panel.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f); // нахил до низького полярного сонця
                }
                break;

            case ModuleType.Battery:
                // Три шафи-акумулятори
                for (int i = -1; i <= 1; i++)
                    ModuleParts.Box(root, "Cell", mat, new Vector3(i * 1.3f, 0.75f, 0f), new Vector3(1f, 1.5f, 1.4f), tint: c);
                break;

            case ModuleType.Antenna:
            case ModuleType.Relay:
                float mast = type == ModuleType.Relay ? 5f : 3.5f;
                ModuleParts.Cylinder(root, "Mast", mat, new Vector3(0f, mast / 2, 0f), new Vector3(0.25f, mast / 2, 0.25f), tint: metal);
                var dish = ModuleParts.Cylinder(root, "Dish", mat, new Vector3(0f, mast, -0.2f), new Vector3(2f, 0.05f, 2f),
                                                collider: false, tint: c);
                dish.transform.localRotation = Quaternion.Euler(-60f, 0f, 0f); // тарілка дивиться в небо, на Землю
                break;

            case ModuleType.Reactor:
                ModuleParts.Cylinder(root, "Core", mat, new Vector3(0f, 1.25f, 0f), new Vector3(3f, 1.25f, 3f), tint: c);
                ModuleParts.Cylinder(root, "Top", mat, new Vector3(0f, 2.6f, 0f), new Vector3(2f, 0.1f, 2f), collider: false, tint: metal);
                break;

            default:
                ModuleParts.Box(root, "Block", mat, new Vector3(0f, 1f, 0f), new Vector3(3f, 2f, 3f), tint: c);
                break;
        }
    }
}
