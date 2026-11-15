// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// «Як у Project Zomboid»: коли інженер заходить у модуль, оболонка (бочка, купол, дах шлюзу) плавно зникає,
/// а все всередині (підлога, меблі) світлішає теплим світлом.
/// Поки гравець надворі, меблі й люди всередині сховані — як у Project Zomboid чи The Sims. Вулицю затемнює OutdoorDarkening (на Sun).
/// Цей компонент додає RoomBuilder сам — руками ставити не треба.
/// Замість фізичного тригера перевіряємо, чи інженер у прямокутнику модуля: так простіше і не потрібні Rigidbody.
///
/// Світло залежить від енергії кімнати (ModulePower.StateOf):
///  - є енергія — тепле світло, усе яскраве;
///  - гравець сам не дав енергії — «сплячий режим»: напівтемно, без тривог;
///  - аварія (енергію дали, але її забракло) — темно, під стелею блимає червона аварійна лампа, на підлозі червоні відблиски.
/// Перехід плавний (≈0,5 с).
/// </summary>
public class ModuleInterior : MonoBehaviour
{
    /// <summary>Чи інженер зараз у якомусь модулі (читає OutdoorDarkening).</summary>
    public static bool EngineerInsideAny => insideCount > 0;
    static int insideCount;

    static Transform engineer;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Indoor = Shader.PropertyToID("_Indoor");

    static readonly int IndoorColor = Shader.PropertyToID("_IndoorColor");
    static readonly Color CalmIndoor = new Color(0.68f, 1f, 0.76f, 1f); // спокійне зелене світло в укритті під час бурі
    static readonly Color CalmSprites = new Color(0.85f, 1f, 0.88f, 1f);

    /// <summary>Буря: в укритті спокійне зелене світло (ставить StormFx).</summary>
    public bool CalmLight { get; set; }
    bool appliedCalm;
    Color normalIndoor = new Color(1f, 0.92f, 0.8f, 1f);

    const float StandbyLight = 0.6f;  // «сплячий режим»
    const float OutageLight = 0.3f;   // аварія
    static readonly Color AlarmRed = new Color(1f, 0.15f, 0.12f, 1f);
    static readonly Color AlarmDark = new Color(0.35f, 0.05f, 0.05f, 1f);

    ModuleView view;
    Renderer[] shell;
    Color[] shellColors;
    Renderer[] interior;
    Color[] interiorColors;      // справжні кольори підлоги й бортиків (темнішають, коли світло гасне)
    Renderer[] hiddenWhenClosed; // меблі-спрайти: видно тільки, коли оболонка відкрита
    SpriteRenderer[] sprites;    // ті з них, що спрайти, і їхні справжні кольори
    Color[] spriteColors;
    SpriteRenderer alarmLamp, alarmGlow, alarmFloor;

    /// <summary>Яскравість світла в кімнаті: 1 — є енергія, менше — темніше. FurnitureLife притемнює за нею рослини й аптечки.</summary>
    public float Light => lightLevel;
    float lightLevel = 1f;
    float appliedLight = -1f;
    RoomPower power = RoomPower.On;

    /// <summary>Чи модуль зараз «відкритий» (оболонка майже зникла) — тоді видно меблі й людей усередині.</summary>
    public bool IsOpen => shellAlpha < 0.6f;
    /// <summary>Наскільки видно оболонку: 1 — повністю, 0 — схована (за цим зникає й насип реголіту).</summary>
    public float ShellAlpha => shellAlpha;
    MaterialPropertyBlock block;
    float shellAlpha = 1f;   // 1 = оболонку видно, 0 = сховано
    float applied = -1f;     // яке значення вже намальоване (щоб не робити зайвої роботи щокадру)
    bool inside;

    public void Setup(ModuleView view, Renderer[] shell, Color[] shellColors, Renderer[] interior, Renderer[] hiddenWhenClosed)
    {
        this.view = view;
        view.Interior = this;
        this.hiddenWhenClosed = hiddenWhenClosed;
        this.shell = shell;
        this.shellColors = shellColors;
        this.interior = interior;
        block = new MaterialPropertyBlock();

        // Запам'ятовуємо справжні кольори, щоб притемнювати від них (а не від уже притемнених)
        interiorColors = new Color[interior.Length];
        for (int i = 0; i < interior.Length; i++)
        {
            var r = interior[i];
            interiorColors[i] = Color.white;
            if (r == null) continue;
            r.GetPropertyBlock(block);
            if (!block.isEmpty) interiorColors[i] = block.GetColor(BaseColor);
        }
        var list = new System.Collections.Generic.List<SpriteRenderer>();
        foreach (var r in hiddenWhenClosed) if (r is SpriteRenderer sr) list.Add(sr);
        sprites = list.ToArray();
        spriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;

        foreach (var r in interior) // яке тепле світло задане в матеріалі — до нього повертаємось після бурі
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty(IndoorColor))
            {
                normalIndoor = r.sharedMaterial.GetColor(IndoorColor);
                break;
            }
    }

    /// <summary>Аварійна лампа (створює RoomBuilder): лампа під стелею, червоне сяйво навколо неї і червоні відблиски на підлозі.</summary>
    public void SetupAlarm(SpriteRenderer lamp, SpriteRenderer glow, SpriteRenderer floor)
    {
        alarmLamp = lamp;
        alarmGlow = glow;
        alarmFloor = floor;
    }

    void Update()
    {
        if (view == null) return;
        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            engineer = go.transform;
        }

        bool nowInside = view.Contains(engineer.position);
        if (nowInside != inside)
        {
            inside = nowInside;
            insideCount += inside ? 1 : -1;
        }

        var look = ModuleLook.Instance;
        float fade = look != null ? look.roofFadeSeconds : 0.4f;
        float step = fade > 0f ? Time.unscaledDeltaTime / fade : 1f;
        shellAlpha = Mathf.MoveTowards(shellAlpha, inside ? 0f : 1f, step);

        // Світло: є енергія → 1; гравець не дав енергії → напівтемно; аварія → темно
        power = ModulePower.StateOf(view.Type);
        float want = power == RoomPower.On ? 1f : (power == RoomPower.Standby ? StandbyLight : OutageLight);
        lightLevel = Mathf.MoveTowards(lightLevel, want, Time.unscaledDeltaTime * 2f);

        if (!Mathf.Approximately(applied, shellAlpha) || !Mathf.Approximately(appliedLight, lightLevel) || appliedCalm != CalmLight) Apply(look);
        UpdateAlarm();
    }

    /// <summary>Аварійна лампа «дихає» червоним (як маячок), тільки коли аварія і гравець у модулі.</summary>
    void UpdateAlarm()
    {
        bool show = power == RoomPower.Outage && IsOpen;
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f); // ≈ раз на секунду
        if (alarmLamp != null) { alarmLamp.enabled = show; alarmLamp.color = Color.Lerp(AlarmDark, AlarmRed, k); }
        if (alarmGlow != null) { alarmGlow.enabled = show; alarmGlow.color = WithAlpha(AlarmRed, 0.55f * k); }
        if (alarmFloor != null) { alarmFloor.enabled = show; alarmFloor.color = WithAlpha(AlarmRed, 0.3f * k); }
    }

    static Color WithAlpha(Color c, float a) { c.a = a; return c; }

    static Color Dim(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

    void Apply(ModuleLook look)
    {
        applied = shellAlpha;
        appliedLight = lightLevel;
        appliedCalm = CalmLight;

        for (int i = 0; i < shell.Length; i++)
        {
            var r = shell[i];
            if (r == null) continue;
            r.enabled = shellAlpha > 0.01f; // зовсім прозору оболонку просто вимикаємо

            var c = shellColors[i];
            c.a *= shellAlpha;
            // Непрозоре малюємо звичайним матеріалом (правильно ховає персонажів позаду),
            // напівпрозоре — скляним (roofMaterial).
            if (look != null)
            {
                var mat = c.a > 0.99f ? look.wallMaterial : look.roofMaterial;
                if (mat != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
            }
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }

        // Меблі-спрайти: поки ти надворі — сховані (інакше плоска картинка вилазить крізь оболонку)
        foreach (var r in hiddenWhenClosed) if (r != null) r.enabled = IsOpen;
        // ...і темнішають, коли світло гасне
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null) sprites[i].color = Dim(CalmLight ? spriteColors[i] * CalmSprites : spriteColors[i], lightLevel);

        // Світло всередині: шейдер Moon/Toon підмішує теплий колір (_Indoor 0..1).
        // Без енергії теплого світла менше, а підлога темніша.
        for (int i = 0; i < interior.Length; i++)
        {
            var r = interior[i];
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(Indoor, (1f - shellAlpha) * lightLevel);
            block.SetColor(BaseColor, Dim(interiorColors[i], lightLevel));
            block.SetColor(IndoorColor, CalmLight ? CalmIndoor : normalIndoor);
            r.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        if (inside) insideCount--;
    }
}
