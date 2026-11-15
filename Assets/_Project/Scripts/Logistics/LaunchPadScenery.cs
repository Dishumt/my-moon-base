// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Декорації пускового майданчика (роль 04): небо світанку із сонцем, Місяцем і хмарами, океан,
/// складальний корпус, паливні баки, водонапірна вежа, громовідводи, прожектори, пусковий стіл,
/// автобус з екіпажем, птахи і вода під ракетою на старті.
/// Якщо в інспекторі LaunchScreen вказано фон і автобус з Art/Earth (роль 02) — беремо їх,
/// інакше все малюється прямокутниками й колами UI Toolkit. LaunchPadView кличе Build()
/// при кожному запуску, а Ignition (0..1) показує, наскільки сильно вже працюють двигуни.
/// </summary>
public class LaunchPadScenery
{
    readonly VisualElement view, scene, rocket, crewLayer, frontLayer;
    VisualElement back, mount, van;
    readonly List<VisualElement> clouds = new List<VisualElement>();
    readonly List<VisualElement> shimmer = new List<VisualElement>();
    readonly List<VisualElement> blinkers = new List<VisualElement>();
    readonly List<VisualElement> lamps = new List<VisualElement>();
    readonly List<Label> birds = new List<Label>();
    readonly List<VisualElement> drops = new List<VisualElement>();
    IVisualElementScheduledItem ticker;
    float W, H, groundTop, cx, rocketW, lastTime;

    /// <summary>Висота пускового столу: ракета стоїть на ньому, а не на землі.</summary>
    public float MountHeight { get; private set; }
    /// <summary>Де двері автобуса: звідти виходить екіпаж.</summary>
    public float VanDoorX { get; private set; }
    /// <summary>0 — тиша, 1 — двигуни на повну (вода, птахи, мерехтіння).</summary>
    public float Ignition { get; set; }

    public LaunchPadScenery(VisualElement view, VisualElement scene, VisualElement rocket, VisualElement crewLayer, VisualElement frontLayer)
    {
        this.view = view;
        this.scene = scene;
        this.rocket = rocket;
        this.crewLayer = crewLayer;
        this.frontLayer = frontLayer;
    }

    // ---------- Побудова ----------

    // ---------- Картинки Юлі (роль 02): Art/Earth ----------

    /// <summary>Фон «Launch Site at Sunset». Порожньо — небо й будівлі малюються кодом, як раніше.</summary>
    public Texture2D Background { get; set; }
    /// <summary>Автобус «Blue-Stripe Bus». Порожньо — автобус із прямокутників.</summary>
    public Texture2D Bus { get; set; }
    public bool HasArt => Background != null;

    // Розміри й точки, виміряні на оригінальних картинках (частки ширини/висоти картинки).
    // Беремо числа, а не texture.width: Unity може розтягнути картинку до 2048×1024 під час імпорту.
    const float BgAspect = 1672f / 941f;
    const float BgPadX = 0.617f;      // центр пускового столу
    const float BgPadTop = 0.742f;    // верх пускового столу — тут стоїть ракета
    const float BgGround = 0.798f;    // низ столу — земля, по якій ходить екіпаж
    const float BusAspect = 1983f / 793f;
    const float BusLeft = 0.219f, BusRight = 0.779f, BusBottom = 0.851f; // де сам автобус на картинці (решта прозора)

    float bgX, bgY, bgW, bgH, vanW;

    /// <summary>Де на екрані лежить картинка фону (заповнює екран без спотворень, зайве обрізається).</summary>
    void FitBackground(float w, float h)
    {
        bgW = Mathf.Max(w, h * BgAspect);
        bgH = bgW / BgAspect;
        bgX = (w - bgW) * 0.5f;
        bgY = (h - bgH) * 0.5f;
    }

    float BgPx(float fx) => bgX + fx * bgW;

    /// <summary>
    /// Середина помаранчевої драбини-вежі, яку Юля намалювала на фоні (частка 0,5445 ширини картинки).
    /// LaunchPadView ставить туди ліфт і місток, а свою (кодову) вежу ховає — інакше було дві драбини. Після Build.
    /// </summary>
    public float PaintedTowerCenterX => BgPx(0.5445f);
    float BgPy(float fy) => bgY + fy * bgH;

    /// <summary>
    /// Для фону-картинки: де центр пускового столу, де земля і яка висота столу (px на екрані).
    /// LaunchPadView ставить ракету саме туди, щоб вона стояла на намальованому столі.
    /// </summary>
    public void ArtAnchors(float w, float h, out float centerX, out float ground, out float mountHeight)
    {
        FitBackground(w, h);
        centerX = BgPx(BgPadX);
        ground = BgPy(BgGround);
        mountHeight = (BgGround - BgPadTop) * bgH;
    }

    public void Build(float w, float h, float ground, float centerX, float rocketWidth, float rocketHeight, float towerX)
    {
        W = w; H = h; groundTop = ground; cx = centerX; rocketW = rocketWidth;
        Clear();
        var rnd = new System.Random(7); // однаковий вигляд щоразу

        back = Box(0, 0, W, H, null);
        back.pickingMode = PickingMode.Ignore;
        back.style.overflow = Overflow.Hidden;
        scene.Insert(0, back);

        if (HasArt) BuildArt(towerX);
        else BuildDrawn(rnd, rocketHeight, towerX);

        // вода під ракетою (система гасіння звуку)
        for (int i = 0; i < 16; i++)
        {
            var d = Box(0, 0, 4f, 10f, "#CFEAFF", frontLayer);
            d.style.opacity = 0f;
            drops.Add(d);
        }

        lastTime = Time.unscaledTime;
        Ignition = 0f;
        if (ticker == null) ticker = view.schedule.Execute(Tick).Every(30);
        else ticker.Resume();
    }

    /// <summary>Фон — картинка Юлі; поверх неї кодом лише те, що рухається: вогники, ліхтарі, птахи, автобус.</summary>
    void BuildArt(float towerX)
    {
        FitBackground(W, H);
        var img = Box(bgX, bgY, bgW, bgH, null, back);
        img.style.backgroundImage = new StyleBackground(Background);
        MountHeight = (BgGround - BgPadTop) * bgH;   // пусковий стіл уже намальований на картинці
        mount = null;

        // червоні вогники на верхівках громовідводів (на картинці вони вже є — ми змушуємо їх блимати)
        foreach (float fx in new[] { 0.459f, 0.756f })
            blinkers.Add(Circle(BgPx(fx), BgPy(0.078f), Mathf.Max(3f, bgH * 0.007f), "#FF4A3D", back));

        // м'яке світло ліхтарів
        foreach (float fx in new[] { 0.480f, 0.706f })
        {
            var glow = Circle(BgPx(fx), BgPy(0.620f), bgH * 0.025f, "#FFE9A0", back);
            glow.userData = 0.4f;   // світиться слабше, ніж намальовані кодом лампи
            lamps.Add(glow);
        }

        // птахи на кущах біля моря — злетять від гуркоту
        for (int i = 0; i < 5; i++)
            AddBird(BgPx(0.30f + 0.03f * i), BgPy(0.635f) - 6f * (i % 2));

        // автобус для екіпажу: стоїть на дорозі перед майданчиком
        float busVisibleW = Mathf.Clamp(W * 0.16f, 140f, 260f);
        vanW = busVisibleW / (BusRight - BusLeft);   // уся картинка разом із прозорими краями
        float vanH = vanW / BusAspect;
        VanDoorX = towerX - 40f - busVisibleW * 0.5f;
        if (Bus != null)
        {
            van = Box(-vanW, groundTop + H * 0.03f - BusBottom * vanH, vanW, vanH, null, back);
            van.style.backgroundImage = new StyleBackground(Bus);
        }
        else van = DrawnVan(busVisibleW, busVisibleW * 0.4f);
    }

    /// <summary>Старий варіант: усе малюється прямокутниками й колами (якщо картинку фону не вказали).</summary>
    void BuildDrawn(System.Random rnd, float rocketHeight, float towerX)
    {
        // небо світанку: смуги від темно-синього до теплого біля горизонту
        float horizon = H * 0.6f;
        string[] sky = { "#0E2347", "#16305C", "#244274", "#3D578C", "#6B6E9E", "#B48A9C", "#E2A88A" };
        // плавний градієнт: 60 тонких смуг, колір між сусідніми точками палітри змішується
        var pal = new Color[sky.Length];
        for (int i = 0; i < sky.Length; i++) ColorUtility.TryParseHtmlString(sky[i], out pal[i]);
        const int Strips = 60;
        for (int i = 0; i < Strips; i++)
        {
            float k = (float)i / (Strips - 1) * (pal.Length - 1);
            int a = Mathf.Min((int)k, pal.Length - 2);
            var strip = Box(0, horizon * i / Strips, W, horizon / Strips + 1f, null, back);
            strip.style.backgroundColor = Color.Lerp(pal[a], pal[a + 1], k - a);
        }

        // зорі (тьмяні — уже світає)
        for (int i = 0; i < 24; i++)
        {
            var star = Box((float)rnd.NextDouble() * W, (float)rnd.NextDouble() * H * 0.3f, 2f, 2f, "#FFFFFF", back);
            star.style.opacity = 0.25f + (float)rnd.NextDouble() * 0.5f;
        }

        // Місяць — куди летимо — і сонце над океаном
        float mr = H * 0.04f;
        var moon = Circle(W * 0.86f, H * 0.13f, mr, "#ECE8DA", back);
        Circle(mr * 0.55f, mr * 0.6f, mr * 0.22f, "#CFCAB8", moon);
        Circle(mr * 1.2f, mr * 1.15f, mr * 0.16f, "#CFCAB8", moon);
        Circle(mr * 0.8f, mr * 1.4f, mr * 0.1f, "#CFCAB8", moon);
        Circle(W * 0.2f, horizon - H * 0.01f, H * 0.06f, "#FFC56A", back);

        // хмари пливуть
        for (int i = 0; i < 4; i++)
        {
            var c = Box((float)rnd.NextDouble() * W, H * (0.1f + 0.3f * (float)rnd.NextDouble()), 0f, 0f, null, back);
            float cw = W * (0.08f + 0.06f * (float)rnd.NextDouble());
            Pill(0f, H * 0.02f, cw, H * 0.035f, "#FFFFFF", c);
            Pill(cw * 0.2f, 0f, cw * 0.5f, H * 0.04f, "#FFFFFF", c);
            c.style.opacity = 0.35f;
            c.userData = 6f + 10f * (float)rnd.NextDouble(); // швидкість, px/с
            clouds.Add(c);
        }

        // океан: справжні космодроми стоять біля моря
        float seaBottom = H * 0.69f;
        Box(0, horizon, W, seaBottom - horizon, "#1D4C78", back);
        for (int i = 0; i < 10; i++)
        {
            var line = Box((float)rnd.NextDouble() * W, horizon + 4f + (seaBottom - horizon - 8f) * (float)rnd.NextDouble(),
                           W * 0.04f, 2f, "#8FC3E8", back);
            shimmer.Add(line);
        }

        // далекий берег з деревами
        Box(0, seaBottom, W, groundTop - seaBottom, "#2E4A3B", back);
        for (int i = 0; i < 14; i++)
        {
            float tr = H * (0.015f + 0.012f * (float)rnd.NextDouble());
            Circle(W * i / 13f + (float)rnd.NextDouble() * 20f, seaBottom + tr * 0.4f, tr, "#263F32", back);
        }

        // складальний корпус (тут збирають ракету) — ліворуч удалині
        float vabW = W * 0.09f, vabH = H * 0.2f, vabX = W * 0.04f, vabB = seaBottom + H * 0.05f;
        var vab = Box(vabX, vabB - vabH, vabW, vabH, "#B7BCC5", back);
        Box(vabW * 0.38f, vabH * 0.2f, vabW * 0.24f, vabH * 0.8f, "#8D939D", vab);   // великі ворота
        Box(0, vabH * 0.08f, vabW, vabH * 0.05f, "#4C79A8", vab);                    // синя смуга

        // паливні баки-кулі праворуч
        float sr = H * 0.035f, sy = groundTop - H * 0.07f;
        for (int i = 0; i < 2; i++)
        {
            float sx = W * (0.88f + i * 0.07f);
            Box(sx - sr * 0.6f, sy, 3f, H * 0.06f, "#7E8692", back);
            Box(sx + sr * 0.6f, sy, 3f, H * 0.06f, "#7E8692", back);
            Circle(sx, sy, sr, "#E5E8ED", back).style.borderBottomColor = new Color(0.6f, 0.64f, 0.68f);
        }

        // водонапірна вежа: на старті ця вода ллється під ракету
        float wtX = W * 0.79f, wtTop = groundTop - H * 0.26f;
        Box(wtX - 14f, wtTop + H * 0.05f, 3f, groundTop - wtTop - H * 0.05f, "#9AA2AD", back);
        Box(wtX + 14f, wtTop + H * 0.05f, 3f, groundTop - wtTop - H * 0.05f, "#9AA2AD", back);
        Box(wtX - 14f, wtTop + H * 0.13f, 31f, 2f, "#9AA2AD", back);
        var tank = Pill(wtX - 26f, wtTop, 55f, H * 0.06f, "#DCE1E7", back);
        Box(4f, H * 0.024f, 47f, 4f, "#4C79A8", tank);

        // далекі птахи сидять на березі — злетять від гуркоту
        for (int i = 0; i < 5; i++)
            AddBird(W * (0.66f + 0.04f * i), seaBottom - H * 0.01f - 6f * (i % 2));

        // громовідводи навколо ракети з червоними вогниками
        float mastH = rocketHeight * 1.15f;
        foreach (float mx in new[] { cx - rocketW * 0.5f - rocketHeight * 0.32f, cx + rocketW * 0.5f + rocketHeight * 0.28f })
        {
            Box(mx, groundTop - mastH, 4f, mastH, "#C4CAD2", back);
            blinkers.Add(Circle(mx + 2f, groundTop - mastH - 2f, 4f, "#FF4A3D", back));
        }

        // прожектори
        foreach (float px in new[] { towerX - rocketHeight * 0.18f, cx + rocketW * 0.5f + rocketHeight * 0.12f })
        {
            float ph = H * 0.2f;
            Box(px, groundTop - ph, 3f, ph, "#59606B", back);
            var head = Box(px - 10f, groundTop - ph - 8f, 24f, 10f, "#2D3138", back);
            lamps.Add(Box(2f, 2f, 20f, 6f, "#FFE9A0", head));
        }

        // пусковий стіл: ракета стоїть на ньому, внизу — канал, куди йде вогонь
        MountHeight = H * 0.05f;
        float mw = Mathf.Max(rocketW * 1.6f, 70f);
        mount = Box(cx - mw * 0.5f, groundTop - MountHeight, mw, MountHeight, "#8E949E", null);
        Box(0, 0, mw, 4f, "#B9BEC6", mount);
        Box(mw * 0.5f - rocketW * 0.35f, MountHeight * 0.35f, rocketW * 0.7f, MountHeight * 0.65f, "#1B1E24", mount);
        Box(0, MountHeight - 6f, 8f, 6f, "#E08A2E", mount);
        Box(mw - 8f, MountHeight - 6f, 8f, 6f, "#E08A2E", mount);
        scene.Insert(scene.IndexOf(rocket), mount);

        // автобус для екіпажу
        van = DrawnVan(118f, 46f);
        VanDoorX = towerX - 40f - vanW * 0.35f;
    }

    /// <summary>Автобус із прямокутників (якщо картинки автобуса немає).</summary>
    VisualElement DrawnVan(float w, float h)
    {
        vanW = w;
        var v = Box(-w - 20f, groundTop - h - 10f, w, h, "#E9ECF1", back); // за вежею, перед землею
        Round(v, 8f);
        float win = (w - 14f) / 4f;
        for (int i = 0; i < 4; i++) Box(10f + i * win, h * 0.17f, win * 0.77f, h * 0.3f, "#4C6D90", v);
        Box(0, h - 14f, w, 4f, "#4C79A8", v);
        Circle(w * 0.2f, h + 2f, 10f, "#1F2228", v);
        Circle(w * 0.8f, h + 2f, 10f, "#1F2228", v);
        return v;
    }

    void AddBird(float x, float y)
    {
        var bird = new Label("v");
        bird.pickingMode = PickingMode.Ignore;
        bird.style.position = Position.Absolute;
        bird.style.left = x;
        bird.style.top = y;
        bird.style.fontSize = 14;
        bird.style.color = new Color(0.12f, 0.12f, 0.16f);
        bird.userData = new Vector2(x, y);
        back.Add(bird);
        birds.Add(bird);
    }

    public void Stop() => ticker?.Pause();

    /// <summary>Прибрати все, що збудували минулого разу.</summary>
    public void Clear()
    {
        back?.RemoveFromHierarchy();   // разом із ним зникає і автобус
        mount?.RemoveFromHierarchy();
        foreach (var d in drops) d.RemoveFromHierarchy();
        clouds.Clear(); shimmer.Clear(); blinkers.Clear(); lamps.Clear(); birds.Clear(); drops.Clear();
    }

    // ---------- Автобус ----------

    /// <summary>Автобус під'їжджає до вежі й зупиняється. skip — одразу на місце.</summary>
    public IEnumerator DriveVan(System.Func<bool> skip)
    {
        if (van == null) yield break;
        float from = -vanW - 20f, to = VanDoorX - vanW * 0.5f;
        for (float t = 0f; t < 1.6f && !skip(); t += Time.unscaledDeltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / 1.6f, 3f); // гальмує під кінець
            van.style.left = Mathf.Lerp(from, to, k);
            van.style.translate = new Translate(0f, Mathf.Sin(t * 30f) * 0.8f);
            yield return null;
        }
        van.style.left = to;
        van.style.translate = new Translate(0f, 0f);
    }

    // ---------- Рух (кожні 30 мс) ----------

    void Tick()
    {
        if (back == null) return;
        float now = Time.unscaledTime, dt = Mathf.Clamp(now - lastTime, 0f, 0.1f);
        lastTime = now;

        foreach (var c in clouds)
        {
            float x = c.style.left.value.value + (float)c.userData * dt;
            if (x > W + 40f) x = -W * 0.2f;
            c.style.left = x;
        }
        for (int i = 0; i < shimmer.Count; i++) shimmer[i].style.opacity = 0.3f + 0.5f * Mathf.Abs(Mathf.Sin(now * 1.3f + i));
        foreach (var b in blinkers) b.style.opacity = Mathf.Repeat(now, 1.2f) < 0.6f ? 1f : 0.2f;
        foreach (var l in lamps)
            l.style.opacity = (l.userData is float k ? k : 1f) * (0.75f + 0.25f * Mathf.Sin(now * 3f) + (Ignition > 0f ? 0.25f : 0f));

        // птахи злітають, щойно запрацювали двигуни
        for (int i = 0; i < birds.Count; i++)
        {
            var home = (Vector2)birds[i].userData;
            if (Ignition <= 0.05f) { birds[i].style.left = home.x; birds[i].style.top = home.y; birds[i].text = "v"; continue; }
            birds[i].style.left = birds[i].style.left.value.value - (60f + i * 12f) * dt;
            birds[i].style.top = birds[i].style.top.value.value - (40f + i * 8f) * dt;
            birds[i].text = Mathf.Repeat(now * 6f + i, 2f) < 1f ? "v" : "-";
        }

        // вода під ракетою: бризки в обидва боки від столу
        float baseY = groundTop - MountHeight * 0.5f;
        for (int i = 0; i < drops.Count; i++)
        {
            var d = drops[i];
            if (Ignition < 0.2f) { d.style.opacity = 0f; continue; }
            float life = Mathf.Repeat(now * 1.6f + i * 0.37f, 1f);
            float dir = i % 2 == 0 ? -1f : 1f;
            d.style.left = cx + dir * (rocketW * 0.4f + life * rocketW * (1.2f + 0.1f * (i % 4)));
            d.style.top = baseY - Mathf.Sin(life * Mathf.PI) * MountHeight * (1.2f + 0.2f * (i % 3));
            d.style.opacity = (1f - life) * Ignition;
        }
    }

    // ---------- Допоміжне ----------

    static VisualElement Box(float left, float top, float width, float height, string color, VisualElement parent = null)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.position = Position.Absolute;
        e.style.left = left;
        e.style.top = top;
        e.style.width = width;
        e.style.height = height;
        if (color != null && ColorUtility.TryParseHtmlString(color, out var c)) e.style.backgroundColor = c;
        parent?.Add(e);
        return e;
    }

    static VisualElement Circle(float centerX, float centerY, float r, string color, VisualElement parent)
    {
        var e = Box(centerX - r, centerY - r, r * 2f, r * 2f, color, parent);
        Round(e, r);
        return e;
    }

    static VisualElement Pill(float left, float top, float width, float height, string color, VisualElement parent)
    {
        var e = Box(left, top, width, height, color, parent);
        Round(e, height * 0.5f);
        return e;
    }

    static void Round(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }
}
