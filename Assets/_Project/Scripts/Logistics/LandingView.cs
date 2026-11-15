// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Посадка на південному полюсі Місяця (роль 04), вид збоку — як пусковий майданчик, тільки на Місяці.
/// Що тут правдиве:
/// - посадковий модуль гальмує двигуном (парашути без повітря не працюють): спершу нахилений, потім вертикально;
/// - на полюсі Сонце завжди біля горизонту → тіні дуже довгі; тінь модуля мчить до нього, поки він знижується;
/// - Земля теж низько над горизонтом; сонячні панелі стоять вертикально (світло збоку);
/// - пил з-під двигуна летить ПРЯМИМИ лініями й одразу падає: без повітря хмар пилу не буває;
/// - тяжіння в 6 разів слабше — екіпаж виходить стрибками.
/// Телеметрія — приблизний профіль справжньої посадки (≈ 12 хв від 15 км), час прискорено.
/// Звичайний C#-клас: LaunchScreen створює його й запускає Play() після карти польоту. Усе малюється кодом.
/// </summary>
public class LandingView
{
    // справжній профіль посадки: час (с) → висота (м), швидкість (км/год)
    static readonly float[] ProfileT = { 0f, 300f, 540f, 660f, 720f };
    static readonly float[] ProfileAltM = { 15000f, 7000f, 2100f, 150f, 0f };
    static readonly float[] ProfileKmh = { 6000f, 2500f, 550f, 50f, 0f };
    const float RealSeconds = 720f;
    const float BrakeSeconds = 4.2f, DescentSeconds = 5.2f;

    readonly VisualElement view, world;
    readonly Label title, status, fact, telemetry;
    readonly Button skipButton;
    VisualElement lander, flame, shadow;
    readonly List<Dust> dust = new List<Dust>();
    readonly Stack<Dust> dustPool = new Stack<Dust>();
    readonly System.Random visualRandom = new System.Random(19);
    float dustRemainder, visualTime;
    bool skip;
    float W, H, groundY, padX, L, Lw, crewSize;

    /// <summary>Звуки двигуна (LaunchScreen ставить сюди). null — тиша.</summary>
    public LaunchSounds Sounds;

    class Dust { public VisualElement e; public Vector2 p, v; public float life; }

    static string T(string key) => LaunchText.Get(key);
    static string T(string key, params object[] args) => LaunchText.Get(key, args);

    public LandingView(VisualElement root)
    {
        view = new VisualElement { name = "landing-view", pickingMode = PickingMode.Position };
        view.AddToClassList("landing-view");
        world = new VisualElement { pickingMode = PickingMode.Ignore };
        world.AddToClassList("pad-layer");
        view.Add(world);
        title = AddLabel("t-label pad-title");
        status = AddLabel("t-big pad-status");
        telemetry = AddLabel("pad-telemetry");
        fact = AddLabel("note note--fact pad-fact");
        skipButton = new Button(() => skip = true);
        skipButton.AddToClassList("btn");
        skipButton.AddToClassList("btn--small");
        skipButton.AddToClassList("pad-skip");
        view.Add(skipButton);
        root.Add(view);
        view.style.display = DisplayStyle.None;
    }

    Label AddLabel(string classes)
    {
        var l = new Label { pickingMode = PickingMode.Ignore };
        foreach (var c in classes.Split(' ')) l.AddToClassList(c);
        view.Add(l);
        return l;
    }

    public bool Ready => view != null;

    // ---------- Послідовність ----------

    public IEnumerator Play(List<(string name, PadCrewFrames frames)> crew)
    {
        skip = false;
        view.style.display = DisplayStyle.Flex;
        view.BringToFront();
        SetText(title, T("landing.title"));
        SetText(skipButton, T("pad.skip"));
        SetText(status, T("landing.event.brake"));
        SetText(fact, T("landing.fact.1"));
        SetText(telemetry, "");

        yield return null;   // чекаємо на розміри
        yield return null;
        Build();

        // 1) гальмування: модуль нахилений, двигун дивиться вперед, по ходу руху
        Vector2 start = new Vector2(W * 1.08f, H * 0.04f);
        Vector2 gate = new Vector2(padX + W * 0.1f, groundY - L * 2.8f);   // «ворота»: звідси спускаємось вертикально
        Vector2 ctrl = new Vector2(W * 0.75f, H * 0.05f);
        for (float t = 0f; t < BrakeSeconds && !skip; t += Time.unscaledDeltaTime)
        {
            float u = t / BrakeSeconds;
            float e = 1f - Mathf.Pow(1f - u, 3f); // brake to a near-stop at the descent gate
            Vector2 pos = Quad(start, ctrl, gate, e);
            Place(pos, Mathf.Lerp(70f, 18f, e), 1f);
            Telemetry(u * BrakeSeconds / (BrakeSeconds + DescentSeconds));
            Tick(Time.unscaledDeltaTime, pos, 1f);
            yield return null;
        }
        Place(gate, 18f, 0.8f);

        // 2) вертикальний спуск: вирівнюємось, підправляємо місце, сідаємо
        SetText(status, T("landing.event.vertical"));
        SetText(fact, T("landing.fact.2"));
        Vector2 touch = new Vector2(padX, groundY);
        bool dustSaid = false;
        for (float t = 0f; t < DescentSeconds && !skip; t += Time.unscaledDeltaTime)
        {
            float u = t / DescentSeconds;
            float ex = FlightMotion.Smooth01(u);
            float ey = FlightMotion.Smooth01(u); // no velocity jump at gate; zero speed at contact
            Vector2 pos = new Vector2(Mathf.Lerp(gate.x, touch.x, ex), Mathf.Lerp(gate.y, touch.y, ey));
            float sway = Mathf.Sin(t * 3f) * 0.5f * Mathf.Sin(u * Mathf.PI);
            Place(pos, Mathf.Lerp(18f, 0f, FlightMotion.Smooth01(Mathf.Clamp01(u * 1.6f))) + sway, Mathf.Lerp(0.8f, 0.35f, u));
            Telemetry((BrakeSeconds + t) / (BrakeSeconds + DescentSeconds));
            if (!dustSaid && groundY - pos.y < L * 1.6f) { dustSaid = true; SetText(status, T("landing.event.dust")); SetText(fact, T("landing.fact.3")); }
            Tick(Time.unscaledDeltaTime, pos, Mathf.Lerp(0.8f, 0.35f, u));
            yield return null;
        }

        // 3) контакт: двигун вимкнено, опори пружинять, пил миттєво осідає (повітря немає)
        Place(touch, 0f, 0f);
        Telemetry(1f);
        Sounds?.Engines(0f);
        SetText(status, T("landing.event.contact"));
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            // Small suspension settle; a rigid metal cabin must not squash like a rubber toy.
            lander.style.translate = new Translate(0f, 1.5f * Mathf.Sin(Mathf.Clamp01(t / 0.5f) * Mathf.PI));
            Tick(Time.unscaledDeltaTime, touch, 0f);
            yield return null;
        }
        lander.style.translate = new Translate(0f, 0f);

        // 4) екіпаж виходить і стрибає до бази
        SetText(status, T("landing.event.crew"));
        SetText(fact, T("landing.fact.4"));
        yield return CrewOut(crew);

        SetText(fact, T("landing.fact.5"));
        for (float t = 0f; t < (skip ? 0.3f : 1.4f); t += Time.unscaledDeltaTime)
        {
            Tick(Time.unscaledDeltaTime, touch, 0f);
            yield return null;
        }
        Sounds?.StopAll();
        view.style.display = DisplayStyle.None;
        world.Clear();
        dust.Clear();
        dustPool.Clear();
    }

    // ---------- Сцена ----------

    // Картинка бази (Resources/Art/Flight/landing_base.png, 1942×809): висота / ширина і де на ній поверхня ґрунту (частка висоти)
    const float BaseArtAspect = 809f / 1942f;
    const float BaseArtGround = 0.832f;

    void Build()
    {
        W = view.resolvedStyle.width;
        H = view.resolvedStyle.height;
        if (float.IsNaN(W) || W < 10f) W = 1280f;
        if (float.IsNaN(H) || H < 10f) H = 720f;
        world.Clear();
        dust.Clear();
        dustPool.Clear();
        dustRemainder = visualTime = 0f;

        groundY = H * 0.8f;
        padX = W * 0.45f;
        L = H * 0.22f;                       // висота посадкового модуля
        Lw = L * 1.15f;
        crewSize = L / 3.2f;                 // справжній модуль у 3–4 рази вищий за людину
        var rnd = new System.Random(9);

        // небо: чорне, із зорями (вдень на Місяці небо теж чорне — немає повітря)
        Box(world, 0f, 0f, W, H, "#020308");
        for (int i = 0; i < 80; i++)
        {
            var s = Box(world, (float)rnd.NextDouble() * W, (float)rnd.NextDouble() * groundY * 0.9f, 2f, 2f, "#FFFFFF");
            s.style.opacity = 0.2f + 0.6f * (float)rnd.NextDouble();
        }

        // Сонце біля самого горизонту (на полюсі воно ніколи не піднімається високо) і сяйво
        var glare = Circle(world, W * 0.06f, groundY - H * 0.01f, H * 0.2f, "#FFF3C4");
        glare.style.opacity = 0.12f;
        Circle(world, W * 0.06f, groundY - H * 0.01f, H * 0.045f, "#FFF8E0");

        // Земля теж низько над горизонтом
        float er = H * 0.04f;
        var earthArt = FlightArt.Add(world, "Art/Flight/earth", "landing-earth");
        if (earthArt != null) FlightArt.Rect(earthArt, W * 0.86f - er, groundY - H * 0.28f, er * 2f, er * 2f);
        else
        {
            var earth = Circle(world, W * 0.84f, H * 0.3f, er, "#2F6FD0");
            Circle(earth, er * 0.6f, er * 0.7f, er * 0.35f, "#3E9A55");
        }

        // далекі пагорби — краї кратерів
        for (int i = 0; i < 6; i++)
        {
            float hx = W * (0.1f + 0.18f * i) + (float)rnd.NextDouble() * 40f;
            float hr = H * (0.12f + 0.08f * (float)rnd.NextDouble());
            var hill = Circle(world, hx, groundY + hr * 0.75f, hr, i % 2 == 0 ? "#5E5E64" : "#55555B");
            hill.style.scale = new Scale(new Vector3(2.2f, 1f, 1f));
        }

        // кратер у вічній тіні — там лід
        var crater = Box(world, W * 0.66f, groundY - H * 0.012f, W * 0.2f, H * 0.03f, "#18181C");
        Round(crater, H * 0.015f);
        var ice = Box(crater, W * 0.07f, H * 0.008f, W * 0.05f, H * 0.012f, "#9FD8FF");
        ice.style.opacity = 0.35f;
        Round(ice, H * 0.006f);

        // ґрунт: реголіт (місячний пил)
        Box(world, 0f, groundY, W, H - groundY, "#8C8B88");
        Box(world, 0f, groundY + (H - groundY) * 0.5f, W, (H - groundY) * 0.5f, "#7A7976");

        // каміння з ДОВГИМИ тінями праворуч (Сонце ліворуч, низько)
        for (int i = 0; i < 9; i++)
        {
            float x = (float)rnd.NextDouble() * W, y = groundY + 6f + (float)rnd.NextDouble() * (H - groundY - 20f);
            float s = 4f + 10f * (float)rnd.NextDouble();
            var sh = Box(world, x, y - s * 0.2f, s * 7f, s * 0.5f, "#2A2A2E");
            sh.style.opacity = 0.75f;
            Circle(world, x, y, s * 0.5f, "#A9A7A2");
        }

        // наша база: картинка (житловий модуль з антеною ліворуч, сонячні панелі й ще один модуль праворуч);
        // поверхня реголіту на ній — на 83,2 % висоти, саме там ставимо лінію ґрунту. Немає картинки — малюємо панелі.
        var baseArt = FlightArt.Add(world, "Art/Flight/landing_base", "landing-base");
        if (baseArt != null)
        {
            float bh = W * BaseArtAspect;
            FlightArt.Rect(baseArt, 0f, groundY - bh * BaseArtGround, W, bh);
        }
        else
        {
            foreach (float px in new[] { W * 0.74f, W * 0.8f })
            {
                Box(world, px, groundY - H * 0.1f, 3f, H * 0.1f, "#7E8692");
                var panel = Box(world, px - H * 0.018f, groundY - H * 0.2f, H * 0.036f, H * 0.12f, "#2D5FA8");
                panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2f;
                panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = new Color(0.75f, 0.8f, 0.9f);
                Box(world, px + 3f, groundY - 2f, H * 0.25f, 4f, "#2A2A2E").style.opacity = 0.7f;   // тінь панелі
            }
        }
        var pad = Box(world, padX - L * 0.75f, groundY - 3f, L * 1.5f, 6f, "#B5B3AD");
        Round(pad, 3f);
        Circle(world, padX - L * 0.75f, groundY - 2f, 3f, "#FF6A4D");
        Circle(world, padX + L * 0.75f, groundY - 2f, 3f, "#FF6A4D");

        // Камені, бур і переробник льоду: з картинкою бази — у вільних місцях між модулями, щоб не налазили на них
        bool withBase = baseArt != null;
        if (!withBase)
        {
            var rocks = FlightArt.Add(world, "Art/Flight/moon_rocks", "landing-rocks");
            FlightArt.Rect(rocks, W * 0.04f, groundY - L * 0.4f, L * 0.8f, L * 0.8f);
        }
        var drill = FlightArt.Add(world, "ISRU/ice_drill", "landing-ice-drill");
        FlightArt.Rect(drill, W * (withBase ? 0.6f : 0.86f), groundY - L * 0.75f, L * 0.8f, L * 0.8f);
        var processor = FlightArt.Add(world, "ISRU/ice_processor", "landing-ice-processor");
        FlightArt.Rect(processor, W * (withBase ? 0.22f : 0.12f), groundY - L * 0.65f, L * 0.7f, L * 0.7f);

        // тінь посадкового модуля: витягнута праворуч, мчить до модуля, поки він знижується
        shadow = Box(world, 0f, groundY - 2f, Lw, 6f, "#1E1E22");
        shadow.style.opacity = 0f;
        Round(shadow, 3f);

        BuildLander();
        // Fixed pool: no element creation/removal in the animation loop, even at 144 Hz.
        for (int i = 0; i < 64; i++)
        {
            var particle = Box(world, 0f, 0f, 3f, 2f, "#B5B2AC");
            particle.style.display = DisplayStyle.None;
            dustPool.Push(new Dust { e = particle });
        }
    }

    /// <summary>Посадковий модуль: сріблястий відсік екіпажу, золотистий нижній ступінь, сопло, чотири опори (видно дві).</summary>
    void BuildLander()
    {
        lander = new VisualElement { pickingMode = PickingMode.Ignore };
        lander.style.position = Position.Absolute;
        lander.style.width = Lw;
        lander.style.height = L;
        lander.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f));

        // вогонь — під соплом (малюємо першим, щоб був позаду)
        flame = Box(lander, Lw * 0.4f, L * 0.72f, Lw * 0.2f, L * 0.4f, "#FF9A3D");
        flame.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(0f));
        flame.style.borderBottomLeftRadius = flame.style.borderBottomRightRadius = Lw * 0.1f;
        var core = Box(flame, Lw * 0.05f, 0f, Lw * 0.1f, L * 0.22f, "#FFE58A");
        core.style.borderBottomLeftRadius = core.style.borderBottomRightRadius = Lw * 0.05f;

        var art = FlightArt.Add(lander, "Art/Flight/lander", "landing-module-art");
        if (art != null)
        {
            // Art is a square, bottom-aligned export. Keep feet at Place()'s ground anchor.
            FlightArt.Rect(art, (Lw - L) * 0.5f, 0f, L, L);
            flame.style.left = Lw * 0.5f - L * 0.055f;
            flame.style.top = L * 0.79f;
            flame.style.width = L * 0.11f;
            core.style.left = L * 0.0275f;
            core.style.width = L * 0.055f;
            world.Add(lander);
            return;
        }

        // опори: від нижнього ступеня навскіс до «тарілок»
        Leg(new Vector2(Lw * 0.26f, L * 0.55f), new Vector2(Lw * 0.04f, L * 0.96f));
        Leg(new Vector2(Lw * 0.74f, L * 0.55f), new Vector2(Lw * 0.96f, L * 0.96f));
        Box(lander, Lw * 0.0f, L * 0.95f, Lw * 0.1f, L * 0.05f, "#5B5E66");
        Box(lander, Lw * 0.9f, L * 0.95f, Lw * 0.1f, L * 0.05f, "#5B5E66");

        // нижній ступінь (золота фольга) і сопло
        var stage = Box(lander, Lw * 0.18f, L * 0.36f, Lw * 0.64f, L * 0.28f, "#C9A227");
        Box(stage, 0f, L * 0.11f, Lw * 0.64f, L * 0.04f, "#A88419");
        Box(lander, Lw * 0.42f, L * 0.64f, Lw * 0.16f, L * 0.09f, "#4A4D55");

        // відсік екіпажу: сріблястий, з віконцем і люком
        var cabin = Box(lander, Lw * 0.28f, L * 0.02f, Lw * 0.44f, L * 0.35f, "#D9DDE3");
        cabin.style.borderTopLeftRadius = cabin.style.borderTopRightRadius = Lw * 0.12f;
        var window = Box(cabin, Lw * 0.06f, L * 0.08f, Lw * 0.1f, L * 0.08f, "#1C2433");
        Round(window, 3f);
        Box(cabin, Lw * 0.2f, L * 0.15f, Lw * 0.14f, L * 0.18f, "#AEB4BE");   // люк
        Box(lander, Lw * 0.48f, -L * 0.06f, 2f, L * 0.08f, "#AEB4BE");          // антена

        world.Add(lander);
    }

    void Leg(Vector2 from, Vector2 to)
    {
        Vector2 d = to - from;
        var leg = Box(lander, from.x - 2f, from.y, 4f, d.magnitude, "#8E929B");
        leg.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(0f));
        leg.style.rotate = new Rotate(new Angle(-Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg));
    }

    /// <summary>Поставити модуль: feet — точка між опорами внизу, tilt — нахил (градуси), throttle — сила вогню.</summary>
    void Place(Vector2 feet, float tilt, float throttle)
    {
        lander.style.left = feet.x - Lw * 0.5f;
        lander.style.top = feet.y - L;
        lander.style.rotate = new Rotate(new Angle(tilt));
        flame.style.opacity = throttle > 0.01f ? 1f : 0f;
        float flicker = 1f + 0.07f * Mathf.Sin(visualTime * 41f);
        flame.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.01f, throttle * flicker), 1f));
        Sounds?.Engines(throttle * 0.8f);

        // тінь: Сонце майже на горизонті, тож тінь падає далеко праворуч; що нижче модуль, то ближче тінь
        float height = Mathf.Max(0f, groundY - feet.y);
        float sx = feet.x + height * 4f;
        shadow.style.left = sx - Lw * 0.5f;
        shadow.style.width = Lw * (1f + height / L);
        shadow.style.opacity = sx < W + Lw ? Mathf.Lerp(0.85f, 0.3f, Mathf.Clamp01(height / (L * 4f))) : 0f;
    }

    /// <summary>Пил: летить прямо в боки й одразу падає (немає повітря). Нові порошинки — тільки коли двигун близько до ґрунту.</summary>
    void Tick(float dt, Vector2 feet, float throttle)
    {
        dt = Mathf.Clamp(dt, 0f, 0.1f);
        visualTime += dt;
        float height = groundY - feet.y;
        if (throttle > 0.05f && height < L * 1.6f)
        {
            float power = throttle * (1f - height / (L * 1.6f));
            int n = FlightMotion.EmitCount(ref dustRemainder, 90f * power, dt, dustPool.Count);
            for (int i = 0; i < n; i++)
            {
                float dir = visualRandom.NextDouble() < 0.5 ? -1f : 1f;
                var d = dustPool.Pop();
                d.p = new Vector2(feet.x + Noise(-Lw * 0.1f, Lw * 0.1f), groundY - 3f);
                d.v = new Vector2(dir * Noise(250f, 520f), -Noise(10f, 70f));
                d.life = Noise(0.4f, 0.8f);
                d.e.style.display = DisplayStyle.Flex;
                dust.Add(d);
            }
        }
        for (int i = dust.Count - 1; i >= 0; i--)
        {
            var d = dust[i];
            d.life -= dt;
            d.v.y += 30f * dt;                 // слабке тяжіння і ніякого опору повітря: летить по прямій
            d.p += d.v * dt;
            if (d.life <= 0f || d.p.y > groundY + 2f)
            {
                d.e.style.display = DisplayStyle.None;
                dust.RemoveAt(i);
                dustPool.Push(d);
                continue;
            }
            d.e.style.left = d.p.x;
            d.e.style.top = d.p.y;
            d.e.style.opacity = Mathf.Clamp01(d.life * 2f);
        }
    }

    float Noise(float min, float max) => Mathf.Lerp(min, max, (float)visualRandom.NextDouble());

    // ---------- Екіпаж ----------

    IEnumerator CrewOut(List<(string name, PadCrewFrames frames)> crew)
    {
        var walkers = new List<IEnumerator>();
        for (int i = 0; crew != null && i < crew.Count; i++) walkers.Add(Hopper(crew[i].name, crew[i].frames, i * 0.7f));
        bool any = true;
        for (float t = 0f; any && t < 8f && !skip; t += Time.unscaledDeltaTime)
        {
            any = false;
            foreach (var w in walkers) if (w.MoveNext()) any = true;
            Tick(Time.unscaledDeltaTime, new Vector2(padX, groundY), 0f);
            yield return null;
        }
    }

    /// <summary>Астронавт спускається з модуля й стрибками (тяжіння в 6 разів слабше) іде до панелей бази.</summary>
    IEnumerator Hopper(string name, PadCrewFrames frames, float delay)
    {
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        var body = new VisualElement { pickingMode = PickingMode.Ignore };
        body.AddToClassList("pad-astronaut");
        body.style.width = crewSize;
        body.style.height = crewSize;
        if (frames != null && frames.flip) body.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
        var label = new Label(name);
        label.AddToClassList("pad-name");
        world.Add(body);
        world.Add(label);

        float x = padX + Lw * 0.15f, endX = W * 0.7f, anim = 0f;
        while (x < endX)
        {
            float dt = Time.unscaledDeltaTime;
            x += 110f * dt;
            anim += dt;
            float hop = Mathf.Abs(Mathf.Sin(anim * Mathf.PI * 1.2f)) * crewSize * 0.45f;   // повільні високі стрибки
            body.style.left = x - crewSize * 0.5f;
            body.style.top = groundY - crewSize - hop;
            label.style.left = x - 60f;
            label.style.top = groundY - crewSize - hop - 18f;
            var frame = WalkFrame(frames, anim);
            if (frame.value.texture != null || frame.value.sprite != null) body.style.backgroundImage = frame;
            else body.AddToClassList("pad-astronaut--box");
            float fade = Mathf.Clamp01((endX - x) / 60f);
            body.style.opacity = fade;
            label.style.opacity = fade;
            yield return null;
        }
        body.RemoveFromHierarchy();
        label.RemoveFromHierarchy();
    }

    static StyleBackground WalkFrame(PadCrewFrames f, float time)
    {
        if (f == null || f.walk == null || f.walk.Length == 0) return default;
        return f.walk[(int)(time / 0.2f) % f.walk.Length];   // кадри повільніше, ніж на Землі
    }

    // ---------- Допоміжне ----------

    void Telemetry(float k)
    {
        float real = Mathf.Clamp01(k) * RealSeconds;
        float alt = Profile(ProfileAltM, real), kmh = Profile(ProfileKmh, real);
        SetText(telemetry, T("landing.telemetry", (alt >= 1000f ? Mathf.Round(alt / 100f) * 100f : Mathf.Round(alt)).ToString("0"),
                             Mathf.Round(kmh).ToString("0")));
    }

    static float Profile(float[] values, float real)
    {
        for (int i = 1; i < ProfileT.Length; i++)
            if (real <= ProfileT[i]) return Mathf.Lerp(values[i - 1], values[i], Mathf.InverseLerp(ProfileT[i - 1], ProfileT[i], real));
        return values[values.Length - 1];
    }

    static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static VisualElement Box(VisualElement parent, float left, float top, float width, float height, string color)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.position = Position.Absolute;
        e.style.left = left;
        e.style.top = top;
        e.style.width = width;
        e.style.height = height;
        if (color != null && ColorUtility.TryParseHtmlString(color, out var c)) e.style.backgroundColor = c;
        parent.Add(e);
        return e;
    }

    static VisualElement Circle(VisualElement parent, float cx, float cy, float r, string color)
    {
        var e = Box(parent, cx - r, cy - r, r * 2f, r * 2f, color);
        Round(e, r);
        return e;
    }

    static void Round(VisualElement e, float r)
    {
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }

    static void SetText(TextElement e, string text) { if (e != null) e.text = text; }
}
