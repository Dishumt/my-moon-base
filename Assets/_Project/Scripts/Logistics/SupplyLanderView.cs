// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Корабель поставки справді прилітає на Місяць (роль 04).
///  - Останню ігрову годину перед прибуттям посадковий модуль спускається з неба на посадковий майданчик (pad_01):
///    гальмує двигуном (на Місяці немає повітря — парашути не працюють), тінь під ним росте, пил летить прямими лініями.
///  - Після посадки кілька годин стоїть. Потім злітає назад
///    (тим самим кораблем летять додому ті, у кого закінчився ліміт дози).
///  - Поруч з'являються 3 ящики з вантажем. Вільні роботи (без завдання) по одному відвозять їх на базу (RobotView).
///    Ящики лежать і після зльоту, доки їх не заберуть: зайняті будівництвом роботи приїдуть пізніше.
///    Хто який ящик везе, рахує LanderCargo.
///  - Після першої місії корабель з екіпажем теж спершу стоїть на майданчику.
/// Коли що відбувається, рахує LanderTimeline. Логіку гри цей скрипт не змінює — тільки малює.
///
/// Для браузера (WebGL) легкий: один спрайт (lander.png уже в грі), кілька м'яких плям, 3 ящики й одна система частинок,
/// усе створюється один раз і далі тільки вмикається/вимикається. Без світла й без нових текстур.
/// Нічого не треба ставити в сцену: створюється сам при старті гри.
/// Кнопка для перевірки в DebugPanel: «Корабель: показати посадку».
/// </summary>
public class SupplyLanderView : MonoBehaviour
{
    const string SpritePath = "Art/Flight/lander";
    const float Size = 2.2f;          // lander.png — 2,56 м, ×2,2 ≈ 5,6 м (астронавт ≈ 1,8 м)
    const float StartHeight = 70f;    // з якої висоти видно спуск, метри
    const float Drift = 16f;          // підлітає трохи збоку і гальмує над майданчиком, метри
    const float BellY = 0.3f;         // низ сопла на картинці (у метрах картинки, до масштабу Size)
    const float DustHeight = 14f;     // нижче цієї висоти двигун здуває пил з землі

    static readonly Color FlameColor = new Color(1f, 0.72f, 0.3f, 0.95f);
    static readonly Color FlameCore = new Color(1f, 0.97f, 0.85f, 1f);
    static readonly Color GlowColor = new Color(1f, 0.6f, 0.25f, 1f);
    static readonly Color DustColor = new Color(0.75f, 0.72f, 0.68f, 0.85f); // сірий місячний пил
    static readonly Color[] CrateColors =
    {
        new Color(0.85f, 0.62f, 0.3f), new Color(0.9f, 0.9f, 0.88f), new Color(0.35f, 0.55f, 0.85f)
    };

    GameState seenState;
    PoiMarker pad;
    Vector3 padPos;
    float previewStart = -1f;   // > 0 — показ із DebugPanel (логіка гри не змінюється)

    GameObject root;
    Transform body, flame, core, shadow, glow;
    SpriteRenderer shadowRenderer, glowRenderer;
    ParticleSystem dust;
    ParticleSystem.EmissionModule dustEmission;
    GameObject[] crates;
    bool built;

    // ящики з вантажем: лежать окремо від корабля (Cargo), щоб не зникнути, коли він злетить
    static SupplyLanderView instance;
    readonly LanderCargo cargo = new LanderCargo(3);
    Transform cargoRoot;
    float lastLanding = float.NaN;   // коли сів корабель, чиї ящики зараз лежать

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Живе між сценами (як IcePlantView): після «Почати розділ знову» сцена перезавантажується, а ми лишаємось.
        if (FindAnyObjectByType<SupplyLanderView>() != null) return;
        var go = new GameObject("Supply Lander");
        DontDestroyOnLoad(go);
        go.AddComponent<SupplyLanderView>();
    }

    void OnEnable()
    {
        instance = this;
        DebugPanel.ExtraGUI += DebugButtons;
    }

    void OnDisable()
    {
        if (instance == this) instance = null;
        DebugPanel.ExtraGUI -= DebugButtons;
    }

    /// <summary>Кнопка в DebugPanel: показати спуск, посадку й зліт просто зараз. Вантаж не додається — тільки картинка.</summary>
    void DebugButtons()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || game.Clock == null) return;
        if (GUILayout.Button("Корабель: показати посадку"))
            previewStart = LanderTimeline.Now(game.State, game.Clock.HourProgress);
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || game.Clock == null) { Hide(); return; }
        var s = game.State;
        if (s != seenState) { seenState = s; pad = null; previewStart = -1f; ClearCargo(); } // нова гра — забуваємо старе
        if (pad == null && !FindPad(s)) { Hide(); return; }                    // pad == null і після перезавантаження сцени

        float now = LanderTimeline.Now(s, game.Clock.HourProgress);
        float t, landedAt;
        LanderPhase phase;
        if (previewStart >= 0f)
        {
            phase = LanderTimeline.Preview(previewStart, now, out t);
            landedAt = previewStart + LanderTimeline.DescentHours;
            if (phase == LanderPhase.None) previewStart = -1f;
        }
        else
        {
            phase = LanderTimeline.Phase(s.Supply, now, out t);
            landedAt = s.Supply.LanderLandedAt;
        }

        if (phase == LanderPhase.None) { Hide(); return; }
        if (!built && !Build()) return;
        root.SetActive(true);
        // щойно сів — поруч з'являються ящики (один раз на кожну посадку)
        if (phase == LanderPhase.Landed && landedAt != lastLanding) { lastLanding = landedAt; UnloadCargo(); }
        Show(phase, t, game.Clock.IsPaused);
    }

    // ---------- де майданчик ----------

    bool FindPad(GameState s)
    {
        pad = null;
        foreach (var p in s.Pois)
            if (p.Type == PoiType.LandingPad) { pad = PoiMarker.Find(p.Id); if (pad != null) break; }
        if (pad == null)
            foreach (var m in PoiMarker.All)
                if (m.type == PoiType.LandingPad) { pad = m; break; }
        if (pad == null) return false;
        padPos = pad.transform.position;
        padPos.y = MoonTerrain.HeightAt(padPos);
        if (built) PlaceOnPad();
        return true;
    }

    // ---------- створюємо один раз ----------

    bool Build()
    {
        var sprite = Resources.Load<Sprite>(SpritePath);
        if (sprite == null) { enabled = false; Debug.LogWarning("SupplyLanderView: немає картинки " + SpritePath); return false; }

        root = new GameObject("Lander");
        root.transform.SetParent(transform, false);

        // корабель — білборд, як персонажі; низ картинки (опори) — на землі
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(root.transform, false);
        bodyGo.transform.localScale = Vector3.one * Size; // до Billboard: він запам'ятовує масштаб в Awake
        bodyGo.AddComponent<SpriteRenderer>().sprite = sprite;
        bodyGo.AddComponent<Billboard>();
        body = bodyGo.transform;

        // полум'я під соплом: дві м'які плями, витягнуті вниз (дочірні — повертаються до камери разом з кораблем)
        flame = Blob("Flame", body, FlameColor, 1).transform;
        core = Blob("Flame Core", body, FlameCore, 2).transform;

        // тінь і відблиск двигуна лежать на землі
        shadowRenderer = Blob("Shadow", root.transform, Color.black, -1);
        shadow = shadowRenderer.transform;
        glowRenderer = Blob("Engine Glow", root.transform, GlowColor, -1);
        glow = glowRenderer.transform;

        cargoRoot = new GameObject("Cargo").transform;
        cargoRoot.SetParent(transform, false);

        var look = ModuleLook.Instance;
        if (look != null && look.dustMaterial != null) dust = CreateDust(look.dustMaterial);
        if (look != null && look.roofMaterial != null) crates = CreateCrates(look.roofMaterial);

        built = true;
        PlaceOnPad();
        return true;
    }

    static SpriteRenderer Blob(string name, Transform parent, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = WorldSprites.Blob;
        r.color = color;
        r.sortingOrder = order;
        return r;
    }

    ParticleSystem CreateDust(Material mat)
    {
        var go = new GameObject("Engine Dust");
        go.transform.SetParent(root.transform, false);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // коло лежить на землі, пил летить в усі боки по землі

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = 160;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 12f);   // без повітря пил летить швидко й прямо, не клубочиться
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
        main.startColor = DustColor;
        main.gravityModifier = MoonHop.MoonGravityScale;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.4f;

        var vel = ps.velocityOverLifetime; // трохи вгору, щоб місячна гравітація не «втопила» пил у землю одразу
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        dustEmission = ps.emission;
        dustEmission.rateOverTime = 0f;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
        ps.Play();
        return ps;
    }

    GameObject[] CreateCrates(Material mat)
    {
        // три ящики поруч із кораблем (вільні роботи потім відвозять їх на базу)
        var list = new GameObject[3];
        var offsets = new[] { new Vector3(3.6f, 0f, -1.2f), new Vector3(4.5f, 0f, 0.4f), new Vector3(3.3f, 0f, 1.3f) };
        var sizes = new[] { 1.1f, 0.8f, 0.95f };
        for (int i = 0; i < list.Length; i++)
        {
            float k = sizes[i];
            list[i] = ModuleParts.Box(cargoRoot, "Crate " + i, mat, offsets[i] + Vector3.up * k * 0.5f,
                                      new Vector3(k, k, k), collider: false, tint: CrateColors[i]);
            list[i].transform.localRotation = Quaternion.Euler(0f, 17f * i - 10f, 0f);
            list[i].SetActive(false); // з'являться, коли корабель сяде
        }
        return list;
    }

    void PlaceOnPad()
    {
        root.transform.position = padPos;
        cargoRoot.position = padPos;
        shadow.rotation = glow.rotation = MoonTerrain.FlatOnGround(padPos);
        shadow.position = padPos + Vector3.up * 0.12f;  // трохи над землею, як тіні персонажів
        glow.position = padPos + Vector3.up * 0.13f;
        if (dust != null) dust.transform.position = padPos + Vector3.up * 0.3f;
    }

    // ---------- кожен кадр ----------

    void Show(LanderPhase phase, float t, bool paused)
    {
        float height = 0f, side = 0f, flameLen = 0f;
        switch (phase)
        {
            case LanderPhase.Descending:
                // гальмує: швидкість падає до нуля якраз біля землі (висота ∝ (1 - t)²)
                float left = 1f - t;
                height = StartHeight * left * left;
                side = Drift * left * left * left;
                flameLen = Mathf.Lerp(0.7f, 1.1f, t);    // біля землі двигун працює сильніше — тримає корабель
                break;
            case LanderPhase.Ascending:
                height = StartHeight * t * t;            // розганяється вгору
                flameLen = 1.3f;
                break;
        }

        body.localPosition = new Vector3(side, height, 0f);

        // полум'я: мерехтить, і не встромляється в землю, поки корабель низько
        bool burning = flameLen > 0f;
        SetShown(flame, burning);
        SetShown(core, burning);
        if (burning)
        {
            float flicker = paused ? 1f : 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 18f, 0.3f);
            float len = Mathf.Min(flameLen * flicker, BellY + height / Size);
            flame.localPosition = new Vector3(0f, BellY - len * 0.5f, -0.01f);
            flame.localScale = new Vector3(0.42f, len, 1f);
            core.localPosition = new Vector3(0f, BellY - len * 0.3f, -0.02f);
            core.localScale = new Vector3(0.2f, len * 0.55f, 1f);
        }

        // тінь росте й темнішає, коли корабель ближче до землі; відблиск двигуна — тільки низько
        float near = 1f - Mathf.Clamp01(height / 35f);
        float shadowSize = Mathf.Lerp(2f, 5f, near);
        shadow.localScale = new Vector3(shadowSize, shadowSize, 1f);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.45f * near);
        float glowK = burning ? Mathf.Clamp01(1f - height / 12f) : 0f;
        SetShown(glow, glowK > 0.01f);
        if (glowK > 0.01f)
        {
            glow.localScale = Vector3.one * (3f + 2f * glowK);
            var c = GlowColor; c.a = 0.55f * glowK;
            glowRenderer.color = c;
        }

        // пил: тільки коли двигун низько над землею
        if (dust != null)
        {
            // на паузі пил завмирає; після Hide() (вимкнений об'єкт) система зупинена — запускаємо знову
            if (paused) { if (dust.isPlaying) dust.Pause(); }
            else if (!dust.isPlaying) dust.Play();
            dustEmission.rateOverTime = burning && height < DustHeight ? 140f * (1f - height / DustHeight) : 0f;
        }
    }

    /// <summary>Увімкнути/вимкнути тільки якщо змінилося (щоб не смикати Unity щокадру).</summary>
    static void SetShown(Transform t, bool on)
    {
        if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
    }

    void Hide()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
    }

    // ---------- ящики з вантажем ----------

    void UnloadCargo()
    {
        cargo.Unload();
        ShowCrates();
    }

    void ClearCargo()
    {
        cargo.Clear();
        lastLanding = float.NaN;
        ShowCrates();
    }

    void ShowCrates()
    {
        if (crates == null) return;
        for (int i = 0; i < crates.Length; i++) SetShown(crates[i].transform, cargo.OnGround(i));
    }

    // ---------- для роботів (RobotView): вільний робот везе ящик на базу ----------

    /// <summary>Змінюється, коли з'являються нові ящики або починається нова гра: тоді робот забуває свій старий ящик.</summary>
    public static int CargoGeneration => instance != null ? instance.cargo.Generation : 0;

    /// <summary>Вільний робот бере собі ящик. -1 — везти нічого.</summary>
    public static int ClaimCrate(int robot)
    {
        var v = instance;
        return v != null && v.crates != null ? v.cargo.Claim(robot) : -1;
    }

    /// <summary>Роботу дали справжню роботу — ящик знову вільний для інших.</summary>
    public static void ReleaseCrate(int crate, int robot)
    {
        if (instance != null) instance.cargo.Release(crate, robot);
    }

    /// <summary>
    /// Де стати, щоб підняти ящик: за 1,5 м від нього з боку towards (звідки робот їде).
    /// false — ящик уже не цього робота.
    /// </summary>
    public static bool CrateSpot(int crate, int robot, Vector3 towards, out Vector3 spot, out Vector3 cratePos)
    {
        spot = cratePos = Vector3.zero;
        var v = instance;
        if (v == null || v.crates == null || !v.cargo.IsClaimedBy(crate, robot)) return false;
        cratePos = v.crates[crate].transform.position;
        Vector3 d = towards - cratePos;
        d.y = 0f;
        d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.back;
        spot = cratePos + d * 1.5f;
        return true;
    }

    /// <summary>Робот підняв ящик: біля майданчика він зникає, а робот везе ящик такого кольору.</summary>
    public static bool PickUpCrate(int crate, int robot, out Color color)
    {
        color = Color.white;
        var v = instance;
        if (v == null || v.crates == null || !v.cargo.PickUp(crate, robot)) return false;
        color = CrateColors[crate % CrateColors.Length];
        SetShown(v.crates[crate].transform, false);
        return true;
    }
}
