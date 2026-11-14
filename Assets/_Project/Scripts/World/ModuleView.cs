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

    /// <summary>Чи точка світу всередині прямокутника модуля (не в проході).</summary>
    public bool Contains(Vector3 worldPos)
    {
        Vector3 p = transform.InverseTransformPoint(worldPos);
        return Mathf.Abs(p.x) < size.x * 0.5f && Mathf.Abs(p.z) < size.y * 0.5f;
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
    //  Вигляд згори (камера знизу):
    //   ┌────────────────┐  задня стіна
    //   │                │
    //   │    кімната     │
    //   │   ┌──╴ ╶──┐    │  внутрішні двері
    //   │   │ шлюз  │    │
    //   └───┴──╴ ╶──┴────┘  зовнішні двері
    //
    void BuildRoom(ModuleLook look, ModuleType type)
    {
        Transform root = visual.transform;
        float w = size.x, d = size.y, h = look.wallHeight, t = look.wallThickness;
        var wall = look.wallMaterial;
        Color typeColor = look.ColorOf(type);

        float door = Mathf.Min(look.doorWidth, w - 4 * t);
        float aw = door + 1f;                                // ширина шлюзу
        float front = -d / 2, inner = -d / 2 + look.airlockDepth; // z зовнішньої і внутрішньої стін шлюзу

        // Підлога кімнати — світлий відтінок кольору типу, підлога шлюзу — сіра
        ModuleParts.Box(root, "Floor", wall, new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d),
                        collider: false, tint: Color.Lerp(typeColor, Color.white, 0.6f));
        ModuleParts.Box(root, "Airlock Floor", wall, new Vector3(0f, 0.06f, (front + inner) / 2), new Vector3(aw, 0.1f, inner - front),
                        collider: false, tint: new Color(0.55f, 0.55f, 0.58f));

        // Задня, ліва і права стіни
        ModuleParts.Box(root, "Wall Back", wall, new Vector3(0f, h / 2, d / 2 - t / 2), new Vector3(w, h, t));
        ModuleParts.Box(root, "Wall Left", wall, new Vector3(-w / 2 + t / 2, h / 2, 0f), new Vector3(t, h, d));
        ModuleParts.Box(root, "Wall Right", wall, new Vector3(w / 2 - t / 2, h / 2, 0f), new Vector3(t, h, d));

        // Передня стіна з отвором для зовнішніх дверей
        WallWithGap(root, "Wall Front", wall, front + t / 2, -w / 2, w / 2, door, h, t);
        // Стінки шлюзу: дві бічні і внутрішня з отвором
        ModuleParts.Box(root, "Airlock Side L", wall, new Vector3(-aw / 2, h / 2, (front + inner) / 2), new Vector3(t, h, inner - front));
        ModuleParts.Box(root, "Airlock Side R", wall, new Vector3(aw / 2, h / 2, (front + inner) / 2), new Vector3(t, h, inner - front));
        WallWithGap(root, "Airlock Inner", wall, inner, -aw / 2, aw / 2, door, h, t);

        // Двері (зачинені; відчиняє Airlock)
        var outerDoor = ModuleParts.Box(root, "Door Outer", wall, new Vector3(0f, h / 2, front + t / 2), new Vector3(door, h - 0.1f, t * 0.6f),
                                        tint: new Color(0.35f, 0.38f, 0.42f));
        var innerDoor = ModuleParts.Box(root, "Door Inner", wall, new Vector3(0f, h / 2, inner), new Vector3(door, h - 0.1f, t * 0.6f),
                                        tint: new Color(0.35f, 0.38f, 0.42f));

        // Дах — окремий об'єкт кольору типу, його ховає ModuleInterior
        var roof = ModuleParts.Box(root, "Roof", look.roofMaterial, new Vector3(0f, h + 0.075f, 0f), new Vector3(w + 0.2f, 0.15f, d + 0.2f),
                                   collider: false, tint: typeColor);

        // Світло всередині (вмикається, коли інженер заходить)
        var lightGo = new GameObject("Interior Light");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(0f, h - 0.3f, d / 6);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = look.interiorLightColor;
        light.range = Mathf.Max(w, d) * 1.2f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;

        visual.AddComponent<ModuleInterior>().Setup(this, roof.GetComponent<Renderer>(), light, typeColor);

        var airlock = visual.AddComponent<Airlock>();
        airlock.Setup(outerDoor.transform, innerDoor.transform, door, aw, front, inner, h);
    }

    /// <summary>Стіна вздовж X від x0 до x1 на висоті z, з отвором ширини gap посередині.</summary>
    static void WallWithGap(Transform root, string name, Material mat, float z, float x0, float x1, float gap, float h, float t)
    {
        float mid = (x0 + x1) / 2;
        float partL = mid - gap / 2 - x0, partR = x1 - (mid + gap / 2);
        if (partL > 0.01f) ModuleParts.Box(root, name + " L", mat, new Vector3(x0 + partL / 2, h / 2, z), new Vector3(partL, h, t));
        if (partR > 0.01f) ModuleParts.Box(root, name + " R", mat, new Vector3(x1 - partR / 2, h / 2, z), new Vector3(partR, h, t));
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
