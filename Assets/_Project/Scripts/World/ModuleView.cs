// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Малює один модуль у квадратику сітки бази, читаючи GameState.
/// Створює його BaseView сам — руками в сцену не став.
///  - модуля немає → порожньо;
///  - будується (BuildHoursLeft > 0) → напівпрозоре риштування, яке росте з прогресом;
///  - готовий → коробка: підлога, низькі стіни з дверима, дах окремим об'єктом.
/// Коробку будує код із простих кубів, тож вручну її збирати не треба.
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

        if (want == Shown.Building) BuildScaffold(look);
        else BuildModule(look);
    }

    // ---------- Будова ----------

    void BuildScaffold(ModuleLook look)
    {
        scaffold = Box("Scaffold", look.roofMaterial, Vector3.zero, Vector3.one, collider: false).transform;
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

    // ---------- Готовий модуль ----------

    void BuildModule(ModuleLook look)
    {
        float w = size.x, d = size.y, h = look.wallHeight, t = look.wallThickness;
        var wall = look.wallMaterial;

        // Підлога (без колайдера: земля рівна, інженер ходить по висоті 0)
        Box("Floor", wall, new Vector3(0f, 0.05f, 0f), new Vector3(w, 0.1f, d), collider: false);

        // Задня, ліва і права стіни
        Box("Wall Back", wall, new Vector3(0f, h / 2, d / 2 - t / 2), new Vector3(w, h, t));
        Box("Wall Left", wall, new Vector3(-w / 2 + t / 2, h / 2, 0f), new Vector3(t, h, d));
        Box("Wall Right", wall, new Vector3(w / 2 - t / 2, h / 2, 0f), new Vector3(t, h, d));

        // Передня стіна з двома шматками і дверима посередині
        float door = Mathf.Min(look.doorWidth, w - 2 * t);
        float part = (w - door) / 2f;
        Box("Wall Front L", wall, new Vector3(-w / 2 + part / 2, h / 2, -d / 2 + t / 2), new Vector3(part, h, t));
        Box("Wall Front R", wall, new Vector3(w / 2 - part / 2, h / 2, -d / 2 + t / 2), new Vector3(part, h, t));

        // Дах — окремий об'єкт, його ховає ModuleInterior
        var roof = Box("Roof", look.roofMaterial, new Vector3(0f, h + 0.075f, 0f), new Vector3(w + 0.2f, 0.15f, d + 0.2f), collider: false);

        // Світло всередині (вмикається, коли інженер заходить)
        var lightGo = new GameObject("Interior Light");
        lightGo.transform.SetParent(visual.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, h - 0.3f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = look.interiorLightColor;
        light.range = Mathf.Max(w, d) * 1.2f;
        light.intensity = 0f;
        light.shadows = LightShadows.None;

        var interior = visual.AddComponent<ModuleInterior>();
        interior.Setup(this, roof.GetComponent<Renderer>(), light);
    }

    /// <summary>Створює куб-коробку всередині visual.</summary>
    GameObject Box(string name, Material mat, Vector3 localPos, Vector3 localScale, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(visual.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Destroy(go.GetComponent<Collider>());
        return go;
    }
}
