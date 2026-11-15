// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Тунель між двома кімнатами в одному ряду (TunnelState.SlotA і SlotB, можна через кілька порожніх квадратиків) —
/// роль 04 з дозволу техліда.
/// Будується: напівпрозорий каркас росте від 10% до 100%. Готовий: труба між оболонками модулів, люки в бічних стінах,
/// прохід у невидимих стінах кімнат (щоб пройти й гравцю), бічні стінки тунелю (щоб не вийти крізь нього надвір).
/// Дах (труба й ребра) плавно зникає, коли інженер у тунелі або в одній із з'єднаних кімнат — як у модулів (ModuleInterior):
/// тоді видно підлогу, бортики й людей у тунелі.
/// Створює BaseView для кожного тунелю в GameState.Tunnels.
/// </summary>
public class TunnelView : MonoBehaviour
{
    public const float Width = 1.8f;     // ширина тунелю, м
    public const float Height = 2.0f;    // висота
    const float DoorWidth = 1.3f;        // ширина люка в стіні кімнати

    public int SlotA { get; private set; } = -1;
    enum Shown { Nothing, Building, Built }
    Shown shown = Shown.Nothing;
    GameObject visual;
    Transform scaffold;
    float totalHours;
    GameObject cutA, cutB;               // яким об'єктам модулів ми вже прорізали прохід

    // дах, що зникає
    readonly System.Collections.Generic.List<Renderer> roof = new System.Collections.Generic.List<Renderer>();
    readonly System.Collections.Generic.List<Color> roofColors = new System.Collections.Generic.List<Color>();
    float roofAlpha = 1f, appliedAlpha = -1f;
    MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static Transform engineer;

    /// <summary>Чи дах тунелю зараз прибраний (видно людей усередині).</summary>
    public bool IsOpen => roofAlpha < 0.6f;

    public void Init(int slotA)
    {
        SlotA = slotA;
        name = "Tunnel @" + slotA;
        transform.position = Vector3.zero;   // усі точки тунелю — у світових координатах
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;
        var t = game.State.FindTunnel(SlotA);
        var a = BaseView.Find(SlotA);
        var b = t != null ? BaseView.Find(t.SlotB) : null;
        bool rooms = t != null && a != null && b != null && a.IsEnterableRoom && b.IsEnterableRoom;
        Shown want = !rooms ? Shown.Nothing : (t.IsBuilt ? Shown.Built : Shown.Building);

        if (want != shown) Rebuild(want, a, b);
        if (want == Shown.Building) UpdateScaffold(t);
        if (want == Shown.Built)
        {
            // модуль перебудувався (нова «картинка») — прорізаємо прохід знову
            if (a.Visual != cutA) { CutDoor(a, +1); cutA = a.Visual; }
            if (b.Visual != cutB) { CutDoor(b, -1); cutB = b.Visual; }
            UpdateRoof(a, b);
        }
    }

    /// <summary>Дах зникає, коли інженер у тунелі або в одній із з'єднаних кімнат.</summary>
    void UpdateRoof(ModuleView a, ModuleView b)
    {
        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) engineer = go.transform;
        }
        bool inside = false;
        if (engineer != null)
        {
            Vector3 p = engineer.position;
            inside = a.Contains(p) || b.Contains(p) || (Footprint(SlotA, out var r) && r.Contains(new Vector2(p.x, p.z)));
        }
        var look = ModuleLook.Instance;
        float fade = look != null ? look.roofFadeSeconds : 0.4f;
        roofAlpha = Mathf.MoveTowards(roofAlpha, inside ? 0f : 1f, fade > 0f ? Time.unscaledDeltaTime / fade : 1f);
        if (Mathf.Approximately(roofAlpha, appliedAlpha)) return;
        appliedAlpha = roofAlpha;
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < roof.Count; i++)
        {
            var rend = roof[i];
            if (rend == null) continue;
            rend.enabled = roofAlpha > 0.01f;   // зовсім прозорий дах просто вимикаємо
            var c = roofColors[i];
            c.a *= roofAlpha;
            if (look != null)
            {
                var mat = c.a > 0.99f ? look.wallMaterial : look.roofMaterial;   // напівпрозоре — скляним матеріалом
                if (mat != null && rend.sharedMaterial != mat) rend.sharedMaterial = mat;
            }
            rend.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            rend.SetPropertyBlock(block);
        }
    }

    GameObject Roof(GameObject go, Color c)
    {
        roof.Add(go.GetComponent<Renderer>());
        roofColors.Add(c);
        return go;
    }

    // ---------- Геометрія (спільна з BaseView) ----------

    /// <summary>Прямокутник тунелю на землі (x/z, м): від стіни лівої кімнати до стіни правої. false — модулів немає.</summary>
    public static bool Footprint(int slotA, out Rect rect)
    {
        rect = default;
        var state = GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
        var t = state != null ? state.FindTunnel(slotA) : null;
        if (t == null) return false;
        var a = BaseView.Find(slotA);
        var b = BaseView.Find(t.SlotB);
        if (a == null || b == null || !a.IsEnterableRoom || !b.IsEnterableRoom) return false;
        float x0 = a.transform.position.x + a.Size.x / 2f, x1 = b.transform.position.x - b.Size.x / 2f;
        float z = a.transform.position.z + a.RoomMidZ;
        rect = Rect.MinMaxRect(x0, z - Width / 2f, x1, z + Width / 2f);
        return true;
    }

    void Rebuild(Shown want, ModuleView a, ModuleView b)
    {
        if (visual != null) Destroy(visual);
        visual = null;
        scaffold = null;
        cutA = cutB = null;
        roof.Clear();
        roofColors.Clear();
        roofAlpha = 1f;
        appliedAlpha = -1f;
        shown = want;
        if (want == Shown.Nothing) { totalHours = 0f; return; }

        var look = ModuleLook.Instance;
        if (look == null || !Footprint(SlotA, out var r)) return;
        visual = new GameObject(want == Shown.Built ? "Tunnel" : "Tunnel Construction");
        visual.transform.SetParent(transform, false);
        Vector3 c = new Vector3(r.center.x, 0f, r.center.y);
        float len = r.width + 0.6f;   // трохи заходить в оболонки модулів — без щілин

        if (want == Shown.Building)
        {
            var col = new Color(0.85f, 0.87f, 0.9f, 0.5f);
            scaffold = ModuleParts.Box(visual.transform, "Scaffold", look.roofMaterial, c, Vector3.one, collider: false, tint: col).transform;
            return;
        }

        // труба: світла оболонка з темнішими ребрами, підлога всередині
        var shell = new Color(0.9f, 0.91f, 0.93f);
        var rib = new Color(0.55f, 0.58f, 0.64f);
        Roof(ModuleParts.Box(visual.transform, "Tube", look.wallMaterial, c + Vector3.up * Height / 2f, new Vector3(len, Height, Width), false, shell), shell);
        int ribs = Mathf.Max(2, Mathf.RoundToInt(r.width / 2.5f));   // ребро приблизно кожні 2.5 м
        for (int i = 0; i < ribs; i++)
        {
            float x = r.xMin + r.width * (i + 0.5f) / ribs;
            Roof(ModuleParts.Box(visual.transform, "Rib", look.wallMaterial, new Vector3(x, Height / 2f, c.z),
                                 new Vector3(0.18f, Height + 0.12f, Width + 0.12f), false, rib), rib);
        }
        // підлога й бортики — лишаються, коли дах зникає: видно коридор
        ModuleParts.Box(visual.transform, "Floor", look.wallMaterial, c + Vector3.up * 0.06f, new Vector3(len, 0.1f, Width - 0.2f), false,
                        new Color(0.7f, 0.72f, 0.75f));
        foreach (int side in new[] { -1, 1 })
            ModuleParts.Box(visual.transform, "Curb", look.wallMaterial, c + new Vector3(0f, 0.15f, side * (Width / 2f - 0.08f)),
                            new Vector3(len, 0.3f, 0.12f), false, new Color(0.55f, 0.56f, 0.6f));

        // бічні стінки (невидимі, вищі за стрибок): з тунелю не вийти надвір
        foreach (int side in new[] { -1, 1 })
        {
            var wall = new GameObject("Block Side");
            wall.transform.SetParent(visual.transform, false);
            wall.transform.position = c + new Vector3(0f, 2.25f, side * Width / 2f);
            wall.AddComponent<BoxCollider>().size = new Vector3(len, 4.5f, 0.15f);
        }

        // люки в стінах обох кімнат (темна рамка — видно зсередини, як двері)
        Hatch(a, +1, look);
        Hatch(b, -1, look);
    }

    void Hatch(ModuleView m, int side, ModuleLook look)
    {
        Vector3 p = m.transform.TransformPoint(new Vector3(side * (m.Size.x / 2f - 0.14f), 0.95f, m.RoomMidZ));
        ModuleParts.Box(visual.transform, "Hatch", look.wallMaterial, p, new Vector3(0.1f, 1.9f, DoorWidth), false, new Color(0.18f, 0.2f, 0.24f));
        Vector3 lamp = m.transform.TransformPoint(new Vector3(side * (m.Size.x / 2f - 0.16f), 2.0f, m.RoomMidZ));
        ModuleParts.Box(visual.transform, "Hatch Lamp", look.wallMaterial, lamp, new Vector3(0.08f, 0.1f, 0.3f), false, new Color(0.4f, 1f, 0.5f));
    }

    void UpdateScaffold(TunnelState t)
    {
        if (scaffold == null || !Footprint(SlotA, out var r)) return;
        totalHours = Mathf.Max(totalHours, t.BuildHoursLeft);
        float progress = totalHours > 0f ? 1f - t.BuildHoursLeft / totalHours : 0f;
        float h = Mathf.Lerp(0.1f, Height, progress);
        scaffold.position = new Vector3(r.center.x, h / 2f, r.center.y);
        scaffold.localScale = new Vector3(r.width + 0.6f, h, Width);
    }

    /// <summary>
    /// Прорізати прохід у невидимій стіні кімнати («Block Right» / «Block Left» від RoomBuilder):
    /// одна суцільна стіна стає двома — перед люком і за ним.
    /// </summary>
    void CutDoor(ModuleView m, int side)
    {
        if (m.Visual == null) return;
        string wallName = side > 0 ? "Block Right" : "Block Left";
        Transform wall = Find(m.Visual.transform, wallName);
        if (wall == null) return;
        var box = wall.GetComponent<BoxCollider>();
        if (box == null) return;

        float z0 = wall.localPosition.z + box.center.z - box.size.z / 2f;
        float z1 = wall.localPosition.z + box.center.z + box.size.z / 2f;
        float d0 = m.RoomMidZ - DoorWidth / 2f, d1 = m.RoomMidZ + DoorWidth / 2f;
        Vector3 size = box.size;

        // перед люком
        box.center = new Vector3(box.center.x, box.center.y, (z0 + d0) / 2f - wall.localPosition.z);
        box.size = new Vector3(size.x, size.y, Mathf.Max(0.01f, d0 - z0));
        // за люком
        var back = wall.gameObject.AddComponent<BoxCollider>();
        back.center = new Vector3(box.center.x, box.center.y, (d1 + z1) / 2f - wall.localPosition.z);
        back.size = new Vector3(size.x, size.y, Mathf.Max(0.01f, z1 - d1));
        wall.name = wallName + " (tunnel door)";
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = Find(c, name);
            if (f != null) return f;
        }
        return null;
    }
}
