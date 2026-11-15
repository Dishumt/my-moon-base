// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// «Живі» меблі: предмет показує стан гри, і дитина бачить, що відбувається на базі, без цифр.
///  - oxygen_generator: лампа на кришці зелена (життєзабезпечення отримує енергію) або блимає червоним; трубка зліва — скільки кисню в баку;
///  - hydroponic_rack: рослини ростуть разом з урожаєм теплиці (GreenhouseSystem.Growth), лампи «дихають».
///    Без енергії лампи гаснуть, а без енергії чи води рослини жовтіють (не ростуть);
///  - water_recycler: у баках біжать бульбашки, коли є енергія й вода; без енергії блимає червона лампочка;
///  - medical_cabinet: маленькі аптечки внизу дверцят = скільки аптечок на базі (до 4); немає жодної — хрест блимає червоним;
///  - power_control_panel: зелені вогники = скільки блоків енергії отримала ця кімната (до 3); 0 — блимає червоний;
///  - workstation_desk, science_bench: екран мерехтить, коли є енергія, і гасне без неї;
///  - galley_kitchen: з чайника йде пара, лампи світять, коли є енергія.
/// Червоні тривоги — тільки при АВАРІЇ (енергію дали, але її забракло, див. ModulePower.StateOf).
/// Якщо гравець сам не дав кімнаті енергії, предмети просто вимкнені, без червоного.
/// Додає RoomBuilder сам. Читає GameState щокадру (це дешево) і нічого в нього не пише.
/// Позиції деталей — у пікселях картинки Юлі (256×256, відлік зліва згори), як у графічному редакторі.
/// Деталі — дочірні спрайти з більшим sortingOrder; предмет має SortingGroup, тож вони малюються поверх нього,
/// але не поверх людей, які стоять перед предметом.
/// </summary>
public class FurnitureLife : MonoBehaviour
{
    // ---------- Кольори ----------
    static readonly Color Off = new Color(0.16f, 0.17f, 0.2f, 0.96f);   // згасла лампа
    static readonly Color Green = new Color(0.45f, 1f, 0.4f, 1f);
    static readonly Color Red = new Color(1f, 0.22f, 0.2f, 1f);
    static readonly Color DarkRed = new Color(0.4f, 0.1f, 0.1f, 1f);    // червона лампа в «темній» фазі блимання
    static readonly Color Warm = new Color(1f, 0.85f, 0.55f, 1f);
    static readonly Color ScreenBlue = new Color(0.55f, 0.85f, 1f, 1f);
    static readonly Color Bubble = new Color(0.9f, 0.97f, 1f, 0.9f);

    /// <summary>Назва картинки (наприклад "hydroponic_rack").</summary>
    public string Kind { get; private set; }

    ModuleView view;
    ModuleType room;
    SpriteRenderer main;
    float ppu, heightPx;
    Vector2 pivotPx;
    bool open;   // чи видно, що всередині модуля (гравець у ньому)
    bool alarm;  // аварія: енергію дали, але її забракло — червоні лампи блимають
    float roomLight = 1f; // яскравість світла в кімнаті (ModuleInterior.Light): рослини й аптечки темнішають разом з нею
    float seed;  // щоб однакові предмети мерехтіли не в такт

    /// <summary>Ширина самого предмета, метри (без прозорих країв картинки).</summary>
    public float Width { get; private set; }
    /// <summary>На скільки середина предмета зсунута вбік від точки, де він стоїть, метри.</summary>
    public float CenterX { get; private set; }

    /// <summary>Викликає RoomBuilder одразу після створення предмета (після товщини й тіні).</summary>
    public void Init(string kind, ModuleView view, ModuleType room, SpriteRenderer main, float width, float centerX)
    {
        Kind = kind;
        this.view = view;
        this.room = room;
        this.main = main;
        var sp = main.sprite;
        ppu = sp.pixelsPerUnit;
        pivotPx = sp.pivot;
        heightPx = sp.rect.height;
        seed = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.61f, 1f);
        Width = width > 0f ? width : sp.bounds.size.x * 0.75f;
        CenterX = centerX;
        if (view != null) view.AddFurniture(this); // астронавти шукають «свої» меблі через ModuleView

        switch (kind)
        {
            case "oxygen_generator": SetupOxygen(); break;
            case "hydroponic_rack": SetupRack(); break;
            case "water_recycler": SetupRecycler(); break;
            case "medical_cabinet": SetupMedical(); break;
            case "power_control_panel": SetupPanel(); break;
            case "workstation_desk": SetupScreen(79, 64, 116, 92, true); break;
            case "science_bench": SetupScreen(165, 85, 202, 114, false); break;
            case "galley_kitchen": SetupKitchen(); break;
            default: enabled = false; break; // решта меблів просто стоїть
        }
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;
        var s = game.State;
        open = view == null || view.InteriorOpen;
        int blocks = ModulePower.Blocks(room);
        bool powered = blocks > 0;
        alarm = ModulePower.StateOf(room) == RoomPower.Outage;
        roomLight = view != null && view.Interior != null ? view.Interior.Light : 1f;

        switch (Kind)
        {
            case "oxygen_generator": UpdateOxygen(s, ModulePower.LifeSupportBlocks() > 0); break;
            case "hydroponic_rack": UpdateRack(s, powered); break;
            case "water_recycler": UpdateRecycler(s, powered); break;
            case "medical_cabinet": UpdateMedical(s); break;
            case "power_control_panel": UpdatePanel(blocks); break;
            case "workstation_desk":
            case "science_bench": UpdateScreen(powered); break;
            case "galley_kitchen": UpdateKitchen(powered); break;
        }
    }

    // =====================================================================
    // Для астронавтів: де стати біля предмета і де на картинці лягти
    // =====================================================================

    /// <summary>Точка картинки (пікселі, відлік зліва згори) → точка у світі (на площині картинки).</summary>
    public Vector3 PixelToWorld(float x, float y) => transform.TransformPoint(Px(x, y, 0));

    /// <summary>Середина предмета на підлозі (туди дивиться людина, що з ним працює).</summary>
    public Vector3 FloorCenter => new Vector3(transform.position.x + CenterX, 0f, transform.position.z);

    /// <summary>Скільки є місць навколо предмета (див. Spot).</summary>
    public const int SpotCount = 4;

    /// <summary>
    /// k-те місце на підлозі, де може стати людина, що працює з предметом:
    /// 0 — збоку ліворуч, 1 — збоку праворуч (так людина не закриває предмет), 2 і 3 — спереду ліворуч і праворуч.
    /// Яке з них вільне і чи воно всередині кімнати, вирішує AstronautView.
    /// </summary>
    public Vector3 Spot(int k)
    {
        Vector3 c = FloorCenter;
        float side = Width * 0.5f + 0.1f;
        switch (k)
        {
            case 0: return new Vector3(c.x - side, 0f, c.z - 0.35f);
            case 1: return new Vector3(c.x + side, 0f, c.z - 0.35f);
            case 2: return new Vector3(c.x - 0.45f, 0f, c.z - 1f);
            default: return new Vector3(c.x + 0.45f, 0f, c.z - 1f);
        }
    }

    /// <summary>Посередині перед предметом (тут стоїть «тіло» лежачого — саму картинку малюємо на ліжку).</summary>
    public Vector3 FrontPoint => FloorCenter + new Vector3(0f, 0f, -0.6f);

    // =====================================================================
    // Генератор кисню: трубка-рівень зліва і лампа-маячок на кришці
    // =====================================================================

    const float TubeX0 = 41, TubeX1 = 54, TubeTop = 135, TubeBottom = 173;
    SpriteRenderer o2Empty, o2Lamp, o2Glow;

    void SetupOxygen()
    {
        o2Empty = Box("O2 Empty", TubeX0, TubeTop, TubeX1, TubeTop + 1, new Color(0.12f, 0.14f, 0.18f, 0.95f), 1);
        o2Glow = Glow("O2 Lamp Glow", 124, 72, 44, 44, Green, 2);
        o2Lamp = Dot("O2 Lamp", 124, 72, 12, Green, 3);
    }

    void UpdateOxygen(GameState s, bool powered)
    {
        // Трубка: темне згори = порожня частина бака
        var ls = Balance != null ? Balance.lifeSupport : null;
        float max = ls != null && ls.oxygenTankMax > 0f ? ls.oxygenTankMax : 100f;
        float empty = (TubeBottom - TubeTop) * (1f - Mathf.Clamp01(s.Res.Oxygen / max));
        Place(o2Empty, TubeX0, TubeTop, TubeX1, TubeTop + empty);
        Show(o2Empty, empty > 0.5f);

        // Лампа: зелена — кисень робиться; червона блимає — життєзабезпечення без енергії (це завжди тривога: кисень не робиться)
        bool lit = powered || Blink();
        o2Lamp.color = powered ? Green : (lit ? Red : DarkRed);
        o2Glow.color = A(powered ? Green : Red, powered ? 0.3f + 0.1f * Mathf.Sin(Time.time * 2f) : 0.6f);
        Show(o2Lamp, true);
        Show(o2Glow, lit);
    }

    // =====================================================================
    // Гідропонна полиця: рослини ростуть, лампи «дихають»
    // =====================================================================

    struct Cut
    {
        public int x0, y0, x1, y1, tray; // прямокутник ряду рослин; з рядка tray і нижче — лоток із ґрунтом
        public Cut(int x0, int y0, int x1, int y1, int tray) { this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1; this.tray = tray; }
    }

    static readonly Cut[] RackRows = { new Cut(64, 72, 200, 110, 101), new Cut(64, 139, 200, 171, 160) };
    static readonly float[,] RackLamps = { { 78, 57, 122, 67 }, { 141, 57, 186, 67 }, { 79, 121, 122, 131 }, { 142, 121, 186, 131 } };

    // Картинку полиці ріжемо один раз на всю гру: порожня полиця + окремо ряди рослин.
    static Texture2D rackSource;
    static Sprite rackBase;
    static Sprite[] rackPlants;

    SpriteRenderer[] plants, lampsOff, lampsGlow;
    float shownGrowth = -1f;
    Color plantTint = Color.white;

    void SetupRack()
    {
        PrepareRack(main.sprite);
        if (rackBase != null)
        {
            main.sprite = rackBase;
            var thick = transform.Find("Thickness"); // товщина «картону» (RoomBuilder) — теж без рослин
            var tr = thick != null ? thick.GetComponent<SpriteRenderer>() : null;
            if (tr != null) tr.sprite = rackBase;

            plants = new SpriteRenderer[RackRows.Length];
            for (int i = 0; i < RackRows.Length; i++)
            {
                var c = RackRows[i];
                plants[i] = Part("Plants " + (i + 1), rackPlants[i], 1);
                plants[i].transform.localPosition = Px((c.x0 + c.x1 + 1) * 0.5f, c.y1 + 1, 1); // ростуть від ґрунту вгору
            }
        }

        int n = RackLamps.GetLength(0);
        lampsOff = new SpriteRenderer[n];
        lampsGlow = new SpriteRenderer[n];
        for (int i = 0; i < n; i++)
        {
            float x0 = RackLamps[i, 0], y0 = RackLamps[i, 1], x1 = RackLamps[i, 2], y1 = RackLamps[i, 3];
            lampsOff[i] = Box("Lamp Off " + i, x0, y0, x1, y1, Off, 2);
            lampsGlow[i] = Glow("Lamp Glow " + i, (x0 + x1) * 0.5f, y1 + 6f, (x1 - x0) * 1.5f, 24f, Warm, 2);
        }
    }

    void UpdateRack(GameState s, bool powered)
    {
        bool growing = powered && s.Res.Water > 0.01f;
        if (plants != null)
        {
            float g = GrowthFraction(s);
            // Урожай зібрали (ріст = 0) — рослини плавно «зрізаються», а не зникають миттєво
            shownGrowth = shownGrowth < 0f ? g : Mathf.MoveTowards(shownGrowth, g, Time.deltaTime * 0.8f);
            var scale = new Vector3(Mathf.Lerp(0.55f, 1f, shownGrowth), Mathf.Lerp(0.15f, 1f, shownGrowth), 1f);
            Color tint = growing ? Color.white : new Color(0.8f, 0.76f, 0.5f); // не ростуть — жовтіють
            plantTint = Color.Lerp(plantTint, tint, Time.deltaTime * 2f);
            foreach (var p in plants)
            {
                p.transform.localScale = scale;
                p.color = Dim(plantTint);
                Show(p, true);
            }
        }

        for (int i = 0; i < lampsOff.Length; i++)
        {
            Show(lampsOff[i], !powered);
            lampsGlow[i].color = A(Warm, 0.22f + 0.1f * Mathf.Sin(Time.time * 1.5f + i));
            Show(lampsGlow[i], powered);
        }
    }

    /// <summary>0 — щойно посадили (зібрали врожай), 1 — повний урожай.</summary>
    static float GrowthFraction(GameState s)
    {
        var gh = GameBootstrap.Instance.GetSystem<GreenhouseSystem>();
        if (gh == null) return 1f;
        var b = Balance != null ? Balance.greenhouse : null;
        float perDay = b != null && b.growthPerBlockPerDay > 0f ? b.growthPerBlockPerDay : 1f;
        float days = b != null && b.harvestEveryDays > 0f ? b.harvestEveryDays : 3f;
        float full = perDay * days * Mathf.Max(1, s.Power.Greenhouse); // стільки росте за весь цикл з планом гравця
        return Mathf.Clamp01(gh.Growth / full);
    }

    static void PrepareRack(Sprite src)
    {
        if (src == null || src.texture == rackSource) return;
        rackSource = src.texture;
        rackBase = null;
        rackPlants = null;
        if (!rackSource.isReadable)
        {
            Debug.LogWarning("FurnitureLife: картинка hydroponic_rack без Read/Write — рослини не ростимуть. Запусти Moon → Налаштувати меблі (спрайти Юлі).");
            return;
        }
        try { CutRack(src); }
        catch (System.Exception e)
        {
            Debug.LogWarning("FurnitureLife: не вдалося вирізати рослини з полиці: " + e.Message);
            rackBase = null;
            rackPlants = null;
        }
    }

    /// <summary>
    /// Ріже картинку полиці на «порожню полицю» і ряди рослин:
    ///  - над лотком усе непрозоре в прямокутнику ряду — рослина (листя і контур);
    ///  - у лотку тільки зелене — стебла; на їхньому місці в порожній полиці лишаємо ґрунт (колір сусіднього пікселя).
    /// </summary>
    static void CutRack(Sprite src)
    {
        var tex = src.texture;
        int W = tex.width, H = tex.height;
        Color32[] px = tex.GetPixels32();
        var basePx = (Color32[])px.Clone();
        var rows = new Sprite[RackRows.Length];

        for (int i = 0; i < RackRows.Length; i++)
        {
            var c = RackRows[i];
            int cw = c.x1 - c.x0 + 1, ch = c.y1 - c.y0 + 1;
            var plant = new Color32[cw * ch]; // спочатку все прозоре
            for (int y = c.y0; y <= c.y1; y++)
            {
                int row = (H - 1 - y) * W; // у текстурі рядки йдуть знизу вгору
                Color32 soil = new Color32(28, 30, 28, 255);
                for (int x = c.x0; x <= c.x1; x++)
                {
                    Color32 p = px[row + x];
                    bool leaf = y < c.tray ? p.a > 0 : p.a > 0 && IsGreen(p);
                    if (!leaf)
                    {
                        if (y >= c.tray && p.a > 0) soil = p;
                        continue;
                    }
                    plant[(c.y1 - y) * cw + (x - c.x0)] = p;
                    basePx[row + x] = y < c.tray ? new Color32(0, 0, 0, 0) : soil;
                }
            }
            var ptex = WorldSprites.NewTexture(cw, ch);
            ptex.SetPixels32(plant);
            ptex.Apply();
            rows[i] = Sprite.Create(ptex, new Rect(0, 0, cw, ch), new Vector2(0.5f, 0f), src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        var btex = WorldSprites.NewTexture(W, H);
        btex.SetPixels32(basePx);
        btex.Apply();
        var pivot = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
        rackBase = Sprite.Create(btex, new Rect(0, 0, W, H), pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        rackPlants = rows;
    }

    static bool IsGreen(Color32 p) => p.g > p.r + 10 && p.g > p.b + 10;

    // =====================================================================
    // Переробка води: бульбашки в баках, лампочки внизу
    // =====================================================================

    static readonly float[,] Tanks = { { 42, 132, 68, 189 }, { 171, 121, 204, 171 } }; // x0, верх, x1, низ (скло бака)
    const int BubblesPerTank = 4;
    SpriteRenderer[] bubbles;
    SpriteRenderer recGreenOff, recGreen, recRed, recRedGlow;

    void SetupRecycler()
    {
        bubbles = new SpriteRenderer[Tanks.GetLength(0) * BubblesPerTank];
        for (int i = 0; i < bubbles.Length; i++) bubbles[i] = Dot("Bubble " + i, 0, 0, 4, Bubble, 2);
        recGreenOff = Box("Green LED Off", 131, 202, 136, 206, Off, 1);
        recGreen = Glow("Green LED Glow", 133.5f, 204, 12, 12, A(Green, 0.6f), 2);
        recRed = Dot("Red LED", 146, 203.5f, 5, Red, 2);
        recRedGlow = Glow("Red LED Glow", 146, 203.5f, 16, 16, A(Red, 0.6f), 3);
    }

    void UpdateRecycler(GameState s, bool powered)
    {
        bool flowing = powered && s.Res.Water > 0.01f;
        for (int i = 0; i < bubbles.Length; i++)
        {
            int tank = i / BubblesPerTank, k = i % BubblesPerTank;
            float x0 = Tanks[tank, 0], top = Tanks[tank, 1], x1 = Tanks[tank, 2], bottom = Tanks[tank, 3];
            float p = Mathf.Repeat(Time.time / 1.8f + (float)k / BubblesPerTank + tank * 0.37f + seed, 1f); // 0 — внизу, 1 — вгорі
            float lane = Mathf.Lerp(x0 + 3f, x1 - 3f, (k + 0.5f) / BubblesPerTank);
            float x = lane + Mathf.Sin(p * 11f + k * 2f) * 1.5f;  // трохи гойдаються
            float y = Mathf.Lerp(bottom - 2f, top + 2f, p);
            bubbles[i].transform.localPosition = Px(x, y, 2);
            float fade = Mathf.Clamp01((p - 0.8f) / 0.2f);          // біля поверхні лопаються
            bubbles[i].color = A(Bubble, Bubble.a * (1f - fade));
            Show(bubbles[i], flowing);
        }
        Show(recGreenOff, !powered);
        Show(recGreen, powered);
        bool red = alarm && Blink();
        Show(recRed, red);
        Show(recRedGlow, red);
    }

    // =====================================================================
    // Медична шафа: аптечки внизу дверцят, хрест червоніє, коли їх немає
    // =====================================================================

    SpriteRenderer[] kits;
    SpriteRenderer crossV, crossH;

    void SetupMedical()
    {
        kits = new SpriteRenderer[4];
        for (int i = 0; i < kits.Length; i++)
        {
            kits[i] = Part("MedKit " + i, KitSprite, 1);
            kits[i].transform.localPosition = Px(104.5f + (i - 1.5f) * 20f, 178.5f, 1);
            kits[i].transform.localScale = Vector3.one * (2f / ppu); // 1 піксель аптечки = 2 пікселі картинки (щоб було видно)
        }
        crossV = Box("Cross Red V", 96, 121, 113, 171, Red, 1);
        crossH = Box("Cross Red H", 81, 137, 129, 155, Red, 1);
    }

    void UpdateMedical(GameState s)
    {
        int n = Mathf.Clamp(s.Res.MedKits, 0, kits.Length);
        for (int i = 0; i < kits.Length; i++)
        {
            kits[i].color = Dim(Color.white);
            Show(kits[i], i < n);
        }
        bool empty = s.Res.MedKits <= 0 && Blink(0.9f); // ліків немає — це тривога незалежно від енергії
        Show(crossV, empty);
        Show(crossH, empty);
    }

    // =====================================================================
    // Щиток: три вогники = блоки енергії кімнати, екран з блискавкою
    // =====================================================================

    static readonly float[] PanelLedX = { 131, 153, 175 };
    SpriteRenderer[] ledOff, ledGlow;
    SpriteRenderer ledRed, panelScreenOff, panelScreenGlow;

    void SetupPanel()
    {
        ledOff = new SpriteRenderer[PanelLedX.Length];
        ledGlow = new SpriteRenderer[PanelLedX.Length];
        for (int i = 0; i < PanelLedX.Length; i++)
        {
            float x = PanelLedX[i];
            ledOff[i] = Box("LED Off " + i, x, 75, x + 10, 84, Off, 1);
            ledGlow[i] = Glow("LED Glow " + i, x + 5f, 79.5f, 22, 20, Green, 2);
        }
        ledRed = Box("LED Red", PanelLedX[0], 75, PanelLedX[0] + 10, 84, Red, 1);
        panelScreenOff = Box("Screen Off", 61, 74, 105, 126, new Color(0.08f, 0.08f, 0.1f, 0.6f), 1);
        panelScreenGlow = Glow("Screen Glow", 83, 100, 60, 70, new Color(1f, 0.85f, 0.3f, 0.15f), 2);
    }

    void UpdatePanel(int blocks)
    {
        int n = Mathf.Clamp(blocks, 0, PanelLedX.Length);
        bool blink = Blink();
        for (int i = 0; i < PanelLedX.Length; i++)
        {
            bool on = i < n;
            bool red = alarm && i == 0 && blink;
            Show(ledOff[i], !on && !red);
            ledGlow[i].color = A(Green, 0.25f + 0.35f * Flicker(6f + i)); // вогники ледь мерехтять
            Show(ledGlow[i], on);
        }
        Show(ledRed, alarm && blink);
        Show(panelScreenOff, blocks <= 0);
        panelScreenGlow.color = new Color(1f, 0.85f, 0.3f, 0.12f + 0.08f * Mathf.Sin(Time.time * 2f));
        Show(panelScreenGlow, blocks > 0);
    }

    // =====================================================================
    // Екрани (стіл з ноутбуком, науковий стіл)
    // =====================================================================

    SpriteRenderer scrOff, scrFlicker, scrGlow, deskLampOff, deskLampGlow;

    void SetupScreen(float x0, float y0, float x1, float y1, bool deskLamp)
    {
        scrOff = Box("Screen Off", x0, y0, x1, y1, new Color(0.04f, 0.05f, 0.08f, 0.95f), 1);
        scrFlicker = Box("Screen Flicker", x0, y0, x1, y1, new Color(1f, 1f, 1f, 0.1f), 1);
        scrGlow = Glow("Screen Glow", (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (x1 - x0) * 1.9f, (y1 - y0) * 1.9f, A(ScreenBlue, 0.22f), 2);
        if (deskLamp)
        {
            deskLampOff = Box("Lamp Off", 129, 47, 153, 53, Off, 1);
            deskLampGlow = Glow("Lamp Glow", 141, 62, 46, 30, A(Warm, 0.3f), 2);
        }
    }

    void UpdateScreen(bool powered)
    {
        Show(scrOff, !powered);
        scrFlicker.color = new Color(1f, 1f, 1f, 0.03f + 0.14f * Flicker(9f));
        Show(scrFlicker, powered);
        Show(scrGlow, powered);
        Show(deskLampOff, !powered);
        Show(deskLampGlow, powered);
    }

    // =====================================================================
    // Кухня: пара з чайника, лампи
    // =====================================================================

    static readonly float[,] KitchenLamps = { { 66, 88, 122, 98 }, { 155, 72, 186, 82 } };
    SpriteRenderer[] steam, kitchenLampOff;
    SpriteRenderer kitchenLampGlow;

    void SetupKitchen()
    {
        steam = new SpriteRenderer[3];
        for (int i = 0; i < steam.Length; i++) steam[i] = Glow("Steam " + i, 189, 93, 8, 8, Color.white, 3);
        kitchenLampOff = new SpriteRenderer[KitchenLamps.GetLength(0)];
        for (int i = 0; i < kitchenLampOff.Length; i++)
            kitchenLampOff[i] = Box("Lamp Off " + i, KitchenLamps[i, 0], KitchenLamps[i, 1], KitchenLamps[i, 2], KitchenLamps[i, 3], Off, 1);
        kitchenLampGlow = Glow("Lamp Glow", 94, 106, 80, 34, A(Warm, 0.25f), 2);
    }

    void UpdateKitchen(bool powered)
    {
        for (int i = 0; i < steam.Length; i++)
        {
            float p = Mathf.Repeat(Time.time / 2.4f + i / 3f + seed, 1f); // 0 — щойно з чайника, 1 — розтанула
            float size = Mathf.Lerp(6f, 24f, p);
            steam[i].transform.localPosition = Px(189f + p * 6f + Mathf.Sin(p * 6f + i) * 3f, 92f - p * 44f, 3);
            steam[i].transform.localScale = new Vector3(size / ppu, size / ppu, 1f);
            steam[i].color = new Color(1f, 1f, 1f, 0.5f * Mathf.Sin(p * Mathf.PI));
            Show(steam[i], powered);
        }
        foreach (var r in kitchenLampOff) Show(r, !powered);
        Show(kitchenLampGlow, powered);
    }

    // =====================================================================
    // Дрібниці: створення деталей
    // =====================================================================

    /// <summary>Точка картинки (пікселі, відлік зліва згори) → локальні координати предмета (метри).</summary>
    Vector3 Px(float x, float y, int order) =>
        new Vector3((x - pivotPx.x) / ppu, (heightPx - y - pivotPx.y) / ppu, -0.003f * order);

    SpriteRenderer Part(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        r.enabled = false; // вмикає Update, коли модуль відкритий
        return r;
    }

    /// <summary>Прямокутник поверх картинки: x0, y0 — лівий верхній кут, x1, y1 — правий нижній (пікселі).</summary>
    SpriteRenderer Box(string name, float x0, float y0, float x1, float y1, Color c, int order)
    {
        var r = Part(name, WorldSprites.Square, order);
        r.color = c;
        Place(r, x0, y0, x1, y1);
        return r;
    }

    void Place(SpriteRenderer r, float x0, float y0, float x1, float y1)
    {
        r.transform.localPosition = Px((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, r.sortingOrder);
        r.transform.localScale = new Vector3((x1 - x0) / ppu, (y1 - y0) / ppu, 1f);
    }

    /// <summary>М'яке сяйво (розмита пляма) з центром cx, cy і розміром w×h пікселів.</summary>
    SpriteRenderer Glow(string name, float cx, float cy, float w, float h, Color c, int order)
    {
        var r = Part(name, WorldSprites.Blob, order);
        r.color = c;
        r.transform.localPosition = Px(cx, cy, order);
        r.transform.localScale = new Vector3(w / ppu, h / ppu, 1f);
        return r;
    }

    /// <summary>Кружечок (лампочка, бульбашка) діаметром d пікселів.</summary>
    SpriteRenderer Dot(string name, float cx, float cy, float d, Color c, int order)
    {
        var r = Part(name, WorldSprites.Disc, order);
        r.color = c;
        r.transform.localPosition = Px(cx, cy, order);
        r.transform.localScale = new Vector3(d / ppu, d / ppu, 1f);
        return r;
    }

    /// <summary>Деталь видно, тільки коли модуль відкритий (гравець усередині) і логіка каже «так».</summary>
    void Show(SpriteRenderer r, bool on)
    {
        if (r != null) r.enabled = open && on;
    }

    static Color A(Color c, float a) { c.a = a; return c; }

    /// <summary>Колір, притемнений разом зі світлом у кімнаті (лампи й сяйво НЕ темнішають — вони самі світять).</summary>
    Color Dim(Color c) => new Color(c.r * roomLight, c.g * roomLight, c.b * roomLight, c.a);
    static bool Blink(float period = 0.7f) => Mathf.Repeat(Time.time, period) < period * 0.5f;
    float Flicker(float speed) => Mathf.PerlinNoise(Time.time * speed, seed * 10f);

    // ---------- Числа балансу (кисень у баку, ріст теплиці) ----------

    static SimBalanceData balance;
    static bool balanceTried;
    static SimBalanceData Balance
    {
        get
        {
            if (!balanceTried) { balanceTried = true; balance = DataLoader.Load<SimBalanceData>("Balance/balance"); }
            return balance;
        }
    }

    // ---------- Аптечка (піксельна картинка, яку створюємо кодом) ----------

    static Sprite kit;

    // Аптечка 9×7 пікселів: червона коробка з білим хрестом. Рядки згори вниз.
    static readonly string[] KitRows =
    {
        ".#######.",
        "#RRRWRRR#",
        "#RRRWRRR#",
        "#RWWWWWR#",
        "#RRRWRRR#",
        "#RRRWRRR#",
        ".#######.",
    };

    /// <summary>Аптечка: 1 піксель = 1 одиниця (масштаб 1/PPU ставимо самі).</summary>
    static Sprite KitSprite => kit != null ? kit : (kit = WorldSprites.Make(9, 7, 1f, (x, y) =>
    {
        char ch = KitRows[KitRows.Length - 1 - y][x]; // у текстурі y=0 — нижній рядок
        if (ch == '#') return new Color(0.3f, 0.08f, 0.1f);
        if (ch == 'R') return new Color(0.9f, 0.25f, 0.25f);
        if (ch == 'W') return Color.white;
        return Color.clear;
    }));
}
