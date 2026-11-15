// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Насип реголіту (місячного пилу й каміння) на модулі-кімнаті — роль 04 з дозволу техліда.
/// Коли роботи засипають модуль (ConstructionSystem.ShieldWithRegolith), він змінюється поступово:
///   1) спершу по периметру з'являються мішки з реголітом (перші ~35% роботи);
///   2) потім над модулем росте пагорб із камінням (до 100%).
/// Форма залежить від модуля: бочки (житло, медмодуль, теплиця) і укриття — округлий пагорб,
/// лабораторія — бункер зі схилами, а її скляний купол видно згори. Шлюз спереду лишається відкритим — вхід у бункер.
/// Коли інженер заходить у модуль, пагорб зникає разом з оболонкою (ModuleInterior), щоб було видно кімнату.
/// Справжні місячні бази планують засипати реголітом саме так: шар у ~2 м сильно зменшує радіацію всередині.
/// ModuleView додає цей компонент сам.
/// </summary>
public class RegolithCover : MonoBehaviour
{
    static readonly Color Regolith = new Color(0.56f, 0.54f, 0.51f);
    static readonly Color BagLight = new Color(0.64f, 0.62f, 0.57f);
    static readonly Color BagDark = new Color(0.5f, 0.48f, 0.45f);
    static readonly Color Rock = new Color(0.38f, 0.37f, 0.36f);
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    const float BagsPart = 0.35f;    // скільки роботи йде на мішки по периметру, решта — пагорб

    ModuleView view;
    GameObject cover;                 // усе, що намальовано (дочірнє до картинки модуля — зникає разом із нею)
    GameObject builtFor;              // для якої «картинки» модуля збудовано
    ModuleType builtType;
    Transform mound;                  // пагорб: росте у висоту
    readonly List<Renderer> moundParts = new List<Renderer>();
    readonly List<Color> moundColors = new List<Color>();
    readonly List<GameObject> bags = new List<GameObject>();
    float shown = -1f;                // показаний поступ 0..1 (плавно наздоганяє справжній)
    float appliedProgress = -1f, appliedAlpha = -1f;
    MaterialPropertyBlock block;

    /// <summary>На скільки насип ширший за модуль (м): ModuleView додає це до перешкод, щоб люди не ходили крізь пагорб.</summary>
    public Vector2 Margin => shown > BagsPart ? new Vector2(0.7f, 0.35f) : Vector2.zero;

    void Awake() => view = GetComponent<ModuleView>();

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || view == null) return;

        // скільки засипано: 0 — нічого, 1 — готово (поки роботи працюють — між ними)
        float target = 0f;
        var m = game.State.FindModule(view.SlotId);
        if (m != null && m.IsBuilt && view.IsEnterableRoom)
        {
            var construction = game.GetSystem<ConstructionSystem>();
            target = construction != null ? construction.ShieldProgress(game.State, view.SlotId) : (m.RegolithShield ? 1f : 0f);
        }

        // модуль перебудувався (нова картинка або інший тип) — насип будуємо заново
        if (cover != null && (view.Visual != builtFor || view.Type != builtType)) { Destroy(cover); cover = null; }
        if (cover == null && builtFor != null && view.Visual != builtFor) builtFor = null;

        if (shown < 0f) shown = target;   // перший кадр (наприклад, завантажили гру) — одразу як є
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.3f);

        if (shown <= 0.001f)
        {
            if (cover != null) { Destroy(cover); cover = null; }
            return;
        }
        if (cover == null) Build();
        if (cover == null) return;

        if (!Mathf.Approximately(appliedProgress, shown)) ApplyProgress();
        float alpha = view.Interior != null ? view.Interior.ShellAlpha : 1f;
        if (!Mathf.Approximately(appliedAlpha, alpha)) ApplyAlpha(alpha);
    }

    // ---------- Побудова ----------

    void Build()
    {
        var look = ModuleLook.Instance;
        if (look == null || view.Visual == null) return;
        moundParts.Clear();
        moundColors.Clear();
        bags.Clear();
        appliedProgress = appliedAlpha = -1f;

        cover = new GameObject("Regolith Cover");
        cover.transform.SetParent(view.Visual.transform, false);
        builtFor = view.Visual;
        builtType = view.Type;

        float w = view.Size.x, d = view.RoomDepth, mid = view.RoomMidZ;
        mound = new GameObject("Mound").transform;
        mound.SetParent(cover.transform, false);
        mound.localPosition = new Vector3(0f, 0f, mid);
        var rnd = new System.Random(view.SlotId * 31 + 7);   // однакове каміння щоразу

        float halfX, halfZ;   // половина розміру насипу біля землі — для мішків
        if (builtType == ModuleType.Lab) Bunker(look, w, d, rnd, out halfX, out halfZ);
        else
        {
            // укриття і так низьке й частково засипане — насип нижчий і трохи ширший
            bool shelter = builtType == ModuleType.Shelter;
            Loaf(look, w / 2f, shelter ? 3.3f : 3.6f, shelter ? d + 0.8f : d + 0.6f, rnd, out halfX, out halfZ);
        }
        Bags(look, mid, halfX, halfZ);
    }

    /// <summary>Округлий пагорб уздовж модуля: «бочка» з реголіту із заокругленими кінцями, на ній каміння.</summary>
    void Loaf(ModuleLook look, float halfLen, float top, float depthDia, System.Random rnd, out float halfX, out float halfZ)
    {
        var body = ModuleParts.Cylinder(mound, "Mound Body", look.wallMaterial, Vector3.zero,
                                        new Vector3(2f * top, halfLen, depthDia), false, Regolith);
        body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);   // вісь уздовж x (як бочка модуля)
        AddMound(body, Regolith);
        foreach (int side in new[] { -1, 1 })
            AddMound(ModuleParts.Sphere(mound, "Mound End", look.wallMaterial, new Vector3(side * halfLen, 0f, 0f),
                                        new Vector3(1.4f, 2f * top, depthDia), false, Regolith), Regolith);

        // каміння на схилах (лежить на поверхні пагорба)
        for (int i = 0; i < 7; i++)
        {
            float x = Mathf.Lerp(-halfLen + 0.3f, halfLen - 0.3f, (float)rnd.NextDouble());
            float z = Mathf.Lerp(-0.8f, 0.8f, (float)rnd.NextDouble()) * depthDia / 2f;
            float y = top * Mathf.Sqrt(Mathf.Max(0f, 1f - (z / (depthDia / 2f)) * (z / (depthDia / 2f))));
            float s = 0.3f + 0.25f * (float)rnd.NextDouble();
            AddMound(ModuleParts.Sphere(mound, "Rock", look.wallMaterial, new Vector3(x, y - 0.06f, z),
                                        new Vector3(s, s * 0.6f, s * 0.85f), false, Rock), Rock);
        }
        halfX = halfLen + 0.6f;
        halfZ = depthDia / 2f;
    }

    /// <summary>Бункер для лабораторії: насип-коробка з округлим верхом і схилами по боках. Скляний купол видно згори.</summary>
    void Bunker(ModuleLook look, float w, float d, System.Random rnd, out float halfX, out float halfZ)
    {
        float cw = w + 0.3f, cd = d + 0.3f, ch = 2.45f;
        AddMound(ModuleParts.Box(mound, "Bunker", look.wallMaterial, new Vector3(0f, ch / 2f, 0f), new Vector3(cw, ch, cd), false, Regolith), Regolith);
        AddMound(ModuleParts.Sphere(mound, "Bunker Top", look.wallMaterial, new Vector3(0f, ch, 0f), new Vector3(cw, 0.6f, cd), false, Regolith), Regolith);

        // схили: ззаду й з боків суцільні, спереду — двома шматками, щоб не закрити шлюз
        float berm = 0.8f;   // наскільки схил виходить за стіну
        var alongX = Quaternion.Euler(0f, 0f, 90f);
        var alongZ = Quaternion.Euler(90f, 0f, 0f);
        Berm(look, new Vector3(0f, 0f, cd / 2f), alongX, new Vector3(2.4f, cw / 2f, 2f * berm));
        foreach (int side in new[] { -1, 1 })
            Berm(look, new Vector3(side * cw / 2f, 0f, 0f), alongZ, new Vector3(2f * berm, cd / 2f, 2.4f));
        float gap = view.AirlockHalfWidth + 0.35f;
        float part = (cw / 2f - gap) / 2f;   // половина довжини переднього шматка
        foreach (int side in new[] { -1, 1 })
            Berm(look, new Vector3(side * (gap + part), 0f, -cd / 2f), alongX, new Vector3(2.4f, part, 2f * berm));

        for (int i = 0; i < 5; i++)
        {
            float x = Mathf.Lerp(-cw / 2f + 0.4f, cw / 2f - 0.4f, (float)rnd.NextDouble());
            float z = Mathf.Lerp(-cd / 2f + 0.4f, cd / 2f - 0.4f, (float)rnd.NextDouble());
            if (Mathf.Abs(x) < 1.4f && Mathf.Abs(z) < 1.4f) continue;   // не на куполі
            float s = 0.3f + 0.2f * (float)rnd.NextDouble();
            AddMound(ModuleParts.Sphere(mound, "Rock", look.wallMaterial, new Vector3(x, ch + 0.2f, z), new Vector3(s, s * 0.6f, s * 0.85f), false, Rock), Rock);
        }
        halfX = cw / 2f + berm * 0.7f;
        halfZ = cd / 2f + berm * 0.5f;
    }

    void Berm(ModuleLook look, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var b = ModuleParts.Cylinder(mound, "Berm", look.wallMaterial, pos, scale, false, Regolith);
        b.transform.localRotation = rot;
        AddMound(b, Regolith);
    }

    /// <summary>Мішки з реголітом уздовж краю насипу: ззаду, з боків і спереду обабіч шлюзу. З'являються першими.</summary>
    void Bags(ModuleLook look, float mid, float halfX, float halfZ)
    {
        const float Step = 0.8f;
        float front = mid - halfZ + 0.15f, back = mid + halfZ - 0.15f;
        float gap = view.AirlockHalfWidth + 0.4f;
        var spots = new List<(Vector3 pos, float yaw)>();
        // по колу: передній лівий шматок → лівий бік → зад → правий бік → передній правий шматок
        for (float x = -gap; x > -halfX; x -= Step) spots.Add((new Vector3(x, 0f, front), 0f));
        for (float z = front; z < back; z += Step) spots.Add((new Vector3(-halfX, 0f, z), 90f));
        for (float x = -halfX; x < halfX; x += Step) spots.Add((new Vector3(x, 0f, back), 0f));
        for (float z = back; z > front; z -= Step) spots.Add((new Vector3(halfX, 0f, z), 90f));
        for (float x = halfX; x > gap; x -= Step) spots.Add((new Vector3(x, 0f, front), 0f));

        for (int i = 0; i < spots.Count; i++)
        {
            var c = i % 2 == 0 ? BagLight : BagDark;
            var bag = ModuleParts.Sphere(cover.transform, "Regolith Bag", look.wallMaterial, spots[i].pos + Vector3.up * 0.17f,
                                         new Vector3(0.75f, 0.34f, 0.48f), false, c);
            bag.transform.localRotation = Quaternion.Euler(0f, spots[i].yaw, 0f);
            bags.Add(bag);
        }
    }

    void AddMound(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        moundParts.Add(r);
        moundColors.Add(c);
    }

    // ---------- Поступ і прозорість ----------

    /// <summary>Мішки з'являються по одному, потім пагорб росте від землі вгору.</summary>
    void ApplyProgress()
    {
        appliedProgress = shown;
        int visibleBags = Mathf.RoundToInt(bags.Count * Mathf.Clamp01(shown / BagsPart));
        for (int i = 0; i < bags.Count; i++) if (bags[i] != null) bags[i].SetActive(i < visibleBags);

        float grow = Mathf.Clamp01((shown - BagsPart) / (1f - BagsPart));
        grow = 1f - (1f - grow) * (1f - grow);   // спершу швидко, під кінець повільніше
        mound.gameObject.SetActive(grow > 0.01f);
        mound.localScale = new Vector3(1f, Mathf.Max(0.02f, grow), 1f);
    }

    /// <summary>Пагорб зникає разом з оболонкою модуля, коли інженер усередині (як ModuleInterior).</summary>
    void ApplyAlpha(float alpha)
    {
        appliedAlpha = alpha;
        var look = ModuleLook.Instance;
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < moundParts.Count; i++)
        {
            var r = moundParts[i];
            if (r == null) continue;
            r.enabled = alpha > 0.01f;
            var c = moundColors[i];
            c.a *= alpha;
            if (look != null)
            {
                var mat = c.a > 0.99f ? look.wallMaterial : look.roofMaterial;   // напівпрозоре — скляним матеріалом
                if (mat != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
            }
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }
    }
}
