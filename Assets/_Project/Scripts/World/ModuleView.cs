// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Малює один модуль у квадратику сітки бази, читаючи GameState.
/// Створює його BaseView сам — руками в сцену не став.
///  - модуля немає → порожньо;
///  - будується (BuildHoursLeft > 0) → напівпрозоре риштування, яке росте з прогресом;
///  - готова кімната → оболонка свого типу (бочка, купол, бункер), меблі всередині і шлюз (див. RoomBuilder);
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
        furniture.Clear();
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

    // ---------- Готова кімната (будує RoomBuilder) ----------

    float roomFront = float.NaN, roomBack, airlockFront, airlockHalfWidth; // для Contains

    // ---------- Точки для астронавтів і роботів (у світових координатах) ----------

    /// <summary>Чи це готова кімната зі шлюзом (у неї можна зайти).</summary>
    public bool IsEnterableRoom => shown == Shown.Built && !float.IsNaN(roomFront);
    /// <summary>Чи модуль ще будується (там працюють роботи).</summary>
    public bool IsUnderConstruction => shown == Shown.Building;
    /// <summary>Оболонка й меблі кімнати (null, якщо це не кімната).</summary>
    public ModuleInterior Interior { get; set; }
    /// <summary>Чи видно, що всередині (гравець у модулі). Обладнання й будова — завжди «відкриті».</summary>
    public bool InteriorOpen => Interior == null || Interior.IsOpen;

    /// <summary>Тип модуля, який зараз намальований.</summary>
    public ModuleType Type => shownType;

    Vector3 Local(float x, float z) => transform.TransformPoint(new Vector3(x, 0f, z));

    /// <summary>Надворі перед зовнішніми дверима шлюзу.</summary>
    public Vector3 OuterDoorPoint => Local(0f, airlockFront - 0.5f); // посередині проходу між рядами модулів
    /// <summary>Посередині шлюзу (тут знімають і одягають скафандр).</summary>
    public Vector3 ChamberPoint => Local(0f, (airlockFront + roomFront) / 2f);
    /// <summary>У кімнаті одразу за внутрішніми дверима.</summary>
    public Vector3 InnerDoorPoint => Local(0f, roomFront + 1f);
    /// <summary>Місце в кімнаті для i-го астронавта (щоб не стояли один в одному).</summary>
    public Vector3 RoomSpot(int i)
    {
        float mid = (roomFront + roomBack) / 2f;
        float x = (i % 2 == 0 ? -1f : 1f) * size.x * 0.22f;
        float z = mid + ((i / 2) % 2 == 0 ? 0.6f : -0.5f);
        return Local(x, z);
    }
    // ---------- Меблі (реєструє FurnitureLife, коли RoomBuilder їх ставить) ----------

    readonly List<FurnitureLife> furniture = new List<FurnitureLife>();

    /// <summary>Записати предмет кімнати (викликає FurnitureLife.Init).</summary>
    public void AddFurniture(FurnitureLife f)
    {
        if (f != null) furniture.Add(f);
    }

    /// <summary>
    /// i-й предмет з такою назвою картинки (по колу: у теплиці 4 полиці — астронавти розходяться по різних).
    /// null — такого предмета в кімнаті немає.
    /// </summary>
    public FurnitureLife FindFurniture(string kind, int i)
    {
        int count = 0;
        foreach (var f in furniture) if (f != null && f.Kind == kind) count++;
        if (count == 0) return null;
        int k = ((i % count) + count) % count;
        foreach (var f in furniture)
        {
            if (f == null || f.Kind != kind) continue;
            if (k == 0) return f;
            k--;
        }
        return null;
    }

    /// <summary>Підсуває точку всередину кімнати, не ближче margin метрів до стін.</summary>
    public Vector3 ClampInRoom(Vector3 world, float margin = 0.45f)
    {
        if (float.IsNaN(roomFront)) return world;
        Vector3 p = transform.InverseTransformPoint(world);
        p.x = Mathf.Clamp(p.x, -size.x / 2f + margin, size.x / 2f - margin);
        p.z = Mathf.Clamp(p.z, roomFront + margin, roomBack - margin);
        return transform.TransformPoint(p);
    }

    /// <summary>Перед модулем надворі (обладнання, ремонт, будова).</summary>
    public Vector3 FrontPoint(int i) => Local((i - 1.5f) * 1.2f, -size.y / 2f - 0.8f);
    /// <summary>Місце для робота біля будови (по кутах квадратика).</summary>
    public Vector3 RobotSpot(int i)
    {
        float hx = size.x / 2f + 0.6f, hz = size.y / 2f + 0.5f;
        switch (i % 4)
        {
            case 0: return Local(-hx, -hz * 0.4f);
            case 1: return Local(hx, -hz * 0.4f);
            case 2: return Local(-hx, hz * 0.5f);
            default: return Local(hx, hz * 0.5f);
        }
    }

    /// <summary>Додає прямокутники (x/z у метрах), крізь які не можна ходити: кімната, шлюз або весь модуль.</summary>
    public void AddBlockers(List<Rect> list, float margin)
    {
        if (shown == Shown.Nothing) return;
        Vector3 c = transform.position;
        if (IsEnterableRoom)
        {
            list.Add(Rect.MinMaxRect(c.x - size.x / 2 - margin, c.z + roomFront - margin, c.x + size.x / 2 + margin, c.z + roomBack + margin));
            list.Add(Rect.MinMaxRect(c.x - airlockHalfWidth - margin, c.z + airlockFront - margin, c.x + airlockHalfWidth + margin, c.z + roomFront));
        }
        else
        {
            float k = shown == Shown.Built ? 0.8f : 1f; // обладнання стоїть на площадці 80%
            list.Add(Rect.MinMaxRect(c.x - size.x * k / 2 - margin, c.z - size.y * k / 2 - margin, c.x + size.x * k / 2 + margin, c.z + size.y * k / 2 + margin));
        }
    }

    void BuildRoom(ModuleLook look, ModuleType type)
    {
        var r = RoomBuilder.Build(visual.transform, this, look, type, size);
        roomFront = r.roomFront;
        roomBack = r.roomBack;
        airlockFront = r.airlockFront;
        airlockHalfWidth = r.airlockHalfWidth;
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
