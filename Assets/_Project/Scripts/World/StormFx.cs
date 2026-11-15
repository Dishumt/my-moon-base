// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сонячна буря у світі (дані й розклад бур — RadiationSystem, роль 06; вікна й тексти — планшет, роль 05).
/// ПОПЕРЕДЖЕННЯ (буря скоро):
///  - на всіх кімнатах блимають помаранчеві маячки;
///  - над укриттям (або житловим модулем, якщо укриття немає) підстрибує зелена стрілка «сюди!», біля входу — зелене коло;
///  - над астронавтами, що надворі, — іконка радіації; екран ледь-ледь пульсує бурштиновим.
/// БУРЯ:
///  - екран плавно червоніє, краї темно-червоні; летять сяйні частинки-протони (що сильніша буря — то густіше);
///  - на старті камера коротко трясеться;
///  - тих, хто лишився надворі, обводить червоне сяйво; маячки на кімнатах — червоні;
///  - в укритті — спокійне зелене світло (там безпечно).
/// Ставить себе сам (ModuleLook додає на World). Нічого в GameState не пише.
/// </summary>
public class StormFx : MonoBehaviour
{
    /// <summary>Фаза бурі, яку зараз показує світ.</summary>
    public static StormPhase Phase { get; private set; }

    /// <summary>Попередження або буря — надворі небезпечно (AstronautView показує іконку радіації).</summary>
    public static bool Danger => Phase != StormPhase.None;

    /// <summary>Кнопка DebugPanel: 0 — як у грі, 1 — попередження, 2 — буря сили 3.</summary>
    public static int DebugMode;

    static readonly Color Orange = new Color(1f, 0.55f, 0.1f, 1f);
    static readonly Color Red = new Color(1f, 0.15f, 0.12f, 1f);
    static readonly Color SafeGreen = new Color(0.35f, 1f, 0.45f, 1f);

    float stormK; // 0..1 — наскільки «увімкнена» буря (плавно)
    ParticleSystem protons;
    bool protonsTried;
    readonly Dictionary<ModuleView, SpriteRenderer[]> beacons = new Dictionary<ModuleView, SpriteRenderer[]>();
    readonly List<ModuleView> deadBeacons = new List<ModuleView>();
    SpriteRenderer arrow, ring;
    readonly Dictionary<Transform, SpriteRenderer> glows = new Dictionary<Transform, SpriteRenderer>();
    readonly HashSet<Transform> glowUsed = new HashSet<Transform>();
    ModuleInterior calmRoom;
    static Sprite arrowSprite;

    void Awake()
    {
        DebugMode = 0; // кожен запуск Play — як у грі (статичні поля могли лишитися з минулого запуску)
        Phase = StormPhase.None;
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;

        var rad = game.State.Radiation;
        StormPhase phase = rad != null ? rad.Phase : StormPhase.None;
        int strength = rad != null ? Mathf.Clamp(rad.StormStrength, 1, 3) : 1;
        if (DebugMode == 1) phase = StormPhase.Warning;
        else if (DebugMode == 2) { phase = StormPhase.Active; strength = 3; }

        if (phase == StormPhase.Active && Phase != StormPhase.Active) CameraFollow.Shake(1.4f, 0.35f); // буря почалась
        Phase = phase;

        float dt = Time.unscaledDeltaTime;
        stormK = Mathf.MoveTowards(stormK, phase == StormPhase.Active ? 1f : 0f, dt / 1.5f);

        UpdateOverlay(phase, strength);
        UpdateProtons(strength);
        var shelter = BaseView.FindRoom(ModuleType.Shelter); // куди ховатися: укриття, а якщо його ще немає — житловий
        if (shelter == null) shelter = BaseView.FindRoom(ModuleType.Habitat);
        UpdateBeacons(phase, shelter);
        UpdateShelter(phase, shelter);
        UpdateGlows(phase);
    }

    // ---------- Екран: відтінок, червоні краї ----------

    void UpdateOverlay(StormPhase phase, int strength)
    {
        var overlay = ScreenOverlay.Get();
        if (overlay == null) return;
        float inside = ModuleInterior.EngineerInsideAny ? 0.35f : 1f; // у модулі стіни трохи захищають — менше червоного

        Color tint = Color.clear;
        if (phase == StormPhase.Warning)
            tint = new Color(1f, 0.7f, 0.2f, (0.04f + 0.03f * Mathf.Sin(Time.time * 2f)) * inside);
        if (stormK > 0f)
            tint = new Color(1f, 0.22f, 0.18f, (0.1f + 0.03f * strength) * stormK * inside);
        overlay.StormTint = tint;
        overlay.StormVignette = new Color(0.55f, 0f, 0.02f, (0.35f + 0.1f * strength) * stormK * inside);
    }

    // ---------- Частинки-протони ----------

    void UpdateProtons(int strength)
    {
        if (!protonsTried) { protonsTried = true; CreateProtons(); }
        if (protons == null) return;
        var em = protons.emission;
        em.rateOverTime = 45f * strength * stormK * (ModuleInterior.EngineerInsideAny ? 0.3f : 1f);
    }

    void CreateProtons()
    {
        var look = ModuleLook.Instance;
        var cam = Camera.main;
        if (look == null || look.dustMaterial == null || cam == null) return; // без матеріалу частинки були б рожеві

        var go = new GameObject("Storm Protons");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(5f, 5f, 9f);                                  // вгорі праворуч перед камерою
        go.transform.localRotation = Quaternion.LookRotation(new Vector3(-0.55f, -0.8f, 0.05f)); // летять униз-ліворуч по екрану
        protons = go.AddComponent<ParticleSystem>();
        protons.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = protons.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // разом з камерою — як смуги на екрані
        main.startLifetime = 0.9f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(16f, 24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 0.9f), new Color(1f, 0.55f, 0.35f, 0.85f));
        main.maxParticles = 500;

        var em = protons.emission;
        em.rateOverTime = 0f;

        var shape = protons.shape;
        shape.shapeType = ParticleSystemShapeType.Box; // з площини 18×12 м, уздовж її напрямку
        shape.scale = new Vector3(18f, 12f, 0.5f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = look.dustMaterial;
        r.renderMode = ParticleSystemRenderMode.Stretch; // витягнуті в смужки
        r.velocityScale = 0.05f;
        r.lengthScale = 1.5f;
        r.sortingOrder = 1002; // поверх червоного екрана
        protons.Play();
    }

    // ---------- Маячки на кімнатах ----------

    void UpdateBeacons(StormPhase phase, ModuleView shelter)
    {
        bool on = phase != StormPhase.None;
        Color c = phase == StormPhase.Active ? Red : Orange;
        bool flash = Mathf.Repeat(Time.time, 0.9f) < 0.5f;
        float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 7f);

        foreach (var mv in BaseView.All)
        {
            if (mv == null || !mv.IsEnterableRoom) continue;
            if (!beacons.TryGetValue(mv, out var b))
            {
                if (!on) continue; // створюємо лише тоді, коли знадобились
                b = new[]
                {
                    Child(mv.transform, "Storm Beacon Glow", WorldSprites.Blob, new Vector3(0f, 3.7f, 1f), 2.2f, 4),
                    Child(mv.transform, "Storm Beacon", WorldSprites.Disc, new Vector3(0f, 3.7f, 1f), 0.42f, 5),
                };
                beacons[mv] = b;
            }
            bool show = on && mv != shelter && !mv.InteriorOpen; // гравець усередині — дах прибраний, маячок не потрібен
            b[0].enabled = show && flash;
            b[1].enabled = show;
            if (!show) continue;
            b[0].color = new Color(c.r, c.g, c.b, pulse);
            b[1].color = flash ? c : c * 0.45f;
        }

        // Модуль прибрали — прибираємо і запис про його маячок
        deadBeacons.Clear();
        foreach (var kv in beacons) if (kv.Key == null) deadBeacons.Add(kv.Key);
        foreach (var k in deadBeacons) beacons.Remove(k);
    }

    // ---------- Укриття: «сюди!» і спокійне світло ----------

    void UpdateShelter(StormPhase phase, ModuleView shelter)
    {
        bool on = phase != StormPhase.None && shelter != null && !shelter.InteriorOpen;
        if (on && arrow == null)
        {
            arrow = Child(transform, "Storm Shelter Arrow", ArrowSprite, Vector3.zero, 1f, 6);
            ring = Flat(transform, "Storm Shelter Ring", 5f);
        }
        if (arrow != null)
        {
            arrow.enabled = on;
            ring.enabled = on;
            if (on)
            {
                Vector3 door = shelter.OuterDoorPoint;
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 3f)) * 0.5f; // підстрибує — «сюди!»
                arrow.transform.position = door + Vector3.up * (2.6f + bob);
                arrow.color = SafeGreen;
                ring.transform.position = door + Vector3.up * (MoonTerrain.HeightAt(door) + 0.05f);
                ring.color = new Color(SafeGreen.r, SafeGreen.g, SafeGreen.b, 0.25f + 0.2f * Mathf.Sin(Time.time * 4f));
            }
        }

        // Під час бурі в укритті — спокійне зелене світло
        ModuleInterior want = phase == StormPhase.Active && shelter != null ? shelter.Interior : null;
        if (calmRoom != want)
        {
            if (calmRoom != null) calmRoom.CalmLight = false;
            calmRoom = want;
            if (calmRoom != null) calmRoom.CalmLight = true;
        }
    }

    // ---------- Червоне сяйво навколо тих, хто надворі ----------

    void UpdateGlows(StormPhase phase)
    {
        glowUsed.Clear();
        if (stormK > 0.01f)
        {
            foreach (var a in AstronautView.All)
                if (a != null && a.Data != null && a.Data.InsideSlot < 0) Glow(a.transform);
            var eng = EngineerController.Instance;
            if (eng != null && eng.SuitOn && !ModuleInterior.EngineerInsideAny) Glow(eng.transform);
        }
        foreach (var kv in glows) if (kv.Value != null && !glowUsed.Contains(kv.Key)) kv.Value.enabled = false;
    }

    void Glow(Transform who)
    {
        glowUsed.Add(who);
        if (!glows.TryGetValue(who, out var g) || g == null)
        {
            g = Child(transform, "Storm Danger Glow", WorldSprites.Blob, Vector3.zero, 1f, -1); // під тілом
            g.transform.localScale = new Vector3(2.4f, 2.8f, 1f);
            glows[who] = g;
        }
        var cam = Camera.main;
        Vector3 back = cam != null ? cam.transform.forward * 0.3f : Vector3.zero; // трохи за людиною
        g.transform.position = who.position + Vector3.up * 0.9f + back;
        g.color = new Color(Red.r, Red.g, Red.b, (0.25f + 0.2f * Mathf.Sin(Time.time * 5f)) * stormK);
        g.enabled = true;
    }

    // ---------- Дрібниці ----------

    static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Vector3 localPos, float size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size, size, 1f); // до Billboard: він запам'ятовує масштаб в Awake
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        r.enabled = false;
        go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;
        return r;
    }

    /// <summary>Пляма, що лежить на землі (коло біля входу в укриття).</summary>
    static SpriteRenderer Flat(Transform parent, string name, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size, size, 1f);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = WorldSprites.Blob;
        r.sortingOrder = -3;
        r.enabled = false;
        return r;
    }

    // Стрілка вниз «сюди!» (рядки згори вниз)
    static readonly string[] ArrowRows =
    {
        "..###..",
        "..###..",
        "..###..",
        "#######",
        ".#####.",
        "..###..",
        "...#...",
    };

    static Sprite ArrowSprite => arrowSprite != null ? arrowSprite : (arrowSprite = WorldSprites.Make(7, 7, 7f, (x, y) =>
        ArrowRows[6 - y][x] == '#' ? Color.white : Color.clear));
}
