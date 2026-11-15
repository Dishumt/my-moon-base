// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Кадри одного астронавта для майданчика: ходьба вправо і «спиною» (їде ліфтом).</summary>
public class PadCrewFrames
{
    public StyleBackground[] walk;   // ходьба вправо (4 кадри)
    public StyleBackground back;     // стоїть спиною
    public bool flip;                // кадри намальовані вліво — дзеркалимо
}

/// <summary>
/// Пусковий майданчик (вид збоку) на UI Toolkit: екіпаж іде до ракети і заходить у неї,
/// місток вежі відходить, відлік 10…1 з фактами для дітей, дим, вогонь і злет.
/// Звичайний C#-клас: LaunchScreen створює його і запускає Play() як корутину.
/// Усе рахується в пікселях від розміру екрана, тож працює на будь-якій роздільній здатності.
/// </summary>
public class LaunchPadView
{
    readonly VisualElement view, scene, ground, platform, tower, arm, rocket, flame, trail, smokeLayer, crewLayer;
    readonly Label title, count, status, fact;
    readonly Button skipButton;
    readonly List<VisualElement> puffs = new List<VisualElement>();

    bool skip;
    float W, H, groundTop, size, rocketW, cx, towerX, armY, doorX, rocketTop, mountTop;
    readonly LaunchPadScenery scenery;   // небо, океан, будівлі, автобус, вода під ракетою

    /// <summary>Звуки відліку й двигунів (LaunchScreen ставить сюди). null — тиша.</summary>
    public LaunchSounds Sounds;
    float CrewSize = 56f;             // розмір астронавта на екрані, px (рахується від висоти екрана)

    const float WalkSpeed = 200f;     // px за секунду
    const float FrameTime = 0.12f;    // зміна кадру ходьби

    static string T(string key) => LaunchText.Get(key);

    public LaunchPadView(VisualElement root)
    {
        view = root.Q("pad-view");
        scene = root.Q("pad-scene");
        ground = root.Q("pad-ground");
        platform = root.Q("pad-platform");
        tower = root.Q("pad-tower");
        arm = root.Q("pad-arm");
        rocket = root.Q("pad-rocket");
        flame = root.Q("pad-flame");
        trail = root.Q("pad-trail");
        smokeLayer = root.Q("pad-smoke");
        crewLayer = root.Q("pad-crew");
        title = root.Q<Label>("pad-title");
        count = root.Q<Label>("pad-count");
        status = root.Q<Label>("pad-status");
        fact = root.Q<Label>("pad-fact");
        skipButton = root.Q<Button>("pad-skip");
        if (skipButton != null) skipButton.clicked += () => skip = true;
        if (view != null && scene != null && rocket != null) scenery = new LaunchPadScenery(view, scene, rocket, crewLayer, smokeLayer);
        Show(false);
    }

    /// <summary>Чи знайшлися всі частини в UXML.</summary>
    public bool Ready => view != null && scene != null && ground != null && platform != null && tower != null && arm != null
                         && rocket != null && trail != null && smokeLayer != null && crewLayer != null
                         && title != null && count != null && status != null && fact != null;

    /// <summary>Картинки Юлі з Art/Earth: фон майданчика й автобус. null — малюємо кодом.</summary>
    public void SetArt(Texture2D background, Texture2D bus)
    {
        if (scenery == null) return;
        scenery.Background = background;
        scenery.Bus = bus;
    }

    public void Show(bool visible) => view?.EnableInClassList("pad-view--hidden", !visible);

    // ---------- Головна послідовність ----------

    /// <summary>
    /// Уся сцена запуску. crew — хто летить (з кадрами), rocketTex — картинка вибраної ракети,
    /// scale — розмір ракети, success — чи політ вдалий (вже вирішено в SupplySystem).
    /// </summary>
    public IEnumerator Play(List<(string name, PadCrewFrames frames)> crew, StyleBackground rocketTex, bool hasTex, float aspect,
                            RocketData rocketData, bool success)
    {
        skip = false;
        ClearExtras();
        Show(true);
        SetText(title, T("pad.title"));
        SetText(skipButton, T("pad.skip"));
        SetText(count, "");
        count.RemoveFromClassList("pad-count--word");
        SetText(fact, "");
        SetText(status, T("pad.boarding"));

        yield return null; // чекаємо, поки UI Toolkit порахує розміри
        yield return null;
        Layout(rocketTex, hasTex, aspect, rocketData);

        // 1) автобус привозить екіпаж, астронавти йдуть до ракети
        SetText(fact, T("pad.fact.7"));   // космодроми стоять біля океану
        if (!skip && scenery != null) yield return scenery.DriveVan(() => skip);
        if (!skip) yield return Boarding(crew);

        // 2) місток відходить
        SetText(status, T("pad.arm"));
        SetText(fact, T("pad.fact.2"));   // найбільше важить паливо
        for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime)
        {
            arm.style.width = Mathf.Lerp(doorX - (towerX + 36f), 0f, t);
            yield return null;
        }
        arm.style.width = 0f;

        // 3) відлік 10…1 з фактами
        SetText(status, T("pad.countdown"));
        for (int n = 10; n >= 1 && !skip; n--)
        {
            SetText(count, n.ToString());
            Sounds?.Count(n);
            // факти під час відліку: як летить ракета → скільки летіти → коли можна летіти; на старті — перевантаження
            if (n == 10) SetText(fact, T("pad.fact.1"));
            else if (n == 8) SetText(fact, T("pad.fact.6"));   // справжня ракета — як 600 слонів
            else if (n == 6) SetText(fact, T("pad.fact.4"));
            else if (n == 4) SetText(fact, T("pad.fact.5"));
            else if (n == 2) SetText(fact, T("pad.fact.8"));   // вода під ракетою
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime)
            {
                count.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t * 4f)));
                if (n <= 3) { Smoke(0.25f + (3 - n + t) / 3f * 0.5f); Shake(rocket, 1.5f + (3 - n) * 1.2f); }
                if (n <= 2) Flame(0.4f + (2 - n + t) * 0.3f);
                if (scenery != null) scenery.Ignition = n <= 2 ? 0.3f + (2 - n + t) * 0.35f : 0f;
                Sounds?.Engines(n <= 2 ? 0.2f + (2 - n + t) * 0.25f : 0f);   // двигуни прокидаються за 2 с до старту
                yield return null;
            }
        }

        // 4) СТАРТ! Справжній профіль виходу на орбіту, час прискорено (≈ 8 хв польоту за кілька секунд)
        SetText(count, T("pad.liftoff"));
        count.AddToClassList("pad-count--word");   // слово довше за цифру — менший шрифт, щоб не налазило на ракету
        SetText(fact, T("pad.fact.3"));
        SetText(status, "");
        count.style.scale = new Scale(Vector3.one);
        trail.style.display = DisplayStyle.Flex;
        if (scenery != null) scenery.Ignition = 1f;
        if (!skip) Sounds?.Liftoff();
        yield return Ascent(success);

        // 5) результат
        SetText(count, "");
        SetText(telemetry, "");
        SetText(status, T(success ? "pad.success" : "pad.fail"));
        status.EnableInClassList("c-ok", success);
        status.EnableInClassList("c-bad", !success);
        if (!success) Flame(0f);
        Sounds?.Engines(0f);
        float wait = skip ? 0.6f : 1.8f;
        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;
        status.RemoveFromClassList("c-ok");
        status.RemoveFromClassList("c-bad");
        if (scenery != null) { scenery.Ignition = 0f; scenery.Stop(); }
        Sounds?.StopAll();
        ClearExtras();
        Show(false);
    }

    // ---------- Політ на орбіту ----------

    // Справжній профіль пілотованого старту (приблизно, як у сучасних ракет): час від старту (с) → висота (км), швидкість (км/год)
    static readonly float[] ProfileT = { 0f, 60f, 75f, 150f, 200f, 510f };
    static readonly float[] ProfileAltKm = { 0f, 10f, 13f, 65f, 100f, 200f };
    static readonly float[] ProfileKmh = { 0f, 1500f, 1800f, 7500f, 8000f, 27500f };
    const float RealAscentSeconds = 510f;   // ≈ 8.5 хв до орбіти
    const float AscentSeconds = 10f;        // стільки триває на екрані (факти встигають прочитати)
    const float MaxQAt = 75f, StagingAt = 150f, AbortAt = 120f;

    readonly List<VisualElement> extras = new List<VisualElement>();   // космос, ступінь, капсула — прибираємо після сцени
    Label telemetry;

    /// <summary>
    /// Злет: ракета нахиляється на схід (гравітаційний поворот), камера летить за нею, небо темнішає до чорного космосу,
    /// телеметрія показує висоту й швидкість, Max Q, відокремлення ступеня, орбіта.
    /// Невдача: двигуни глохнуть, і система порятунку відносить капсулу з екіпажем, вона сідає в океан на парашуті.
    /// </summary>
    IEnumerator Ascent(bool success)
    {
        float baseTop = rocketTop, baseLeft = cx - rocketW * 0.5f;
        if (telemetry == null)
        {
            telemetry = new Label { pickingMode = PickingMode.Ignore };
            telemetry.AddToClassList("pad-telemetry");
            view.Add(telemetry);
        }
        AddSpace();

        float rise = 0f, drift = 0f, speed = 0f, tilt = 0f;
        bool maxQ = false, staged = false, turnFact = false, orbit = false;
        float stagingPause = 0f;
        for (float t = 0f; t < AscentSeconds && !skip; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            float real = t / AscentSeconds * RealAscentSeconds;      // скільки минуло б по-справжньому
            if (!success && real >= AbortAt) { yield return Abort(baseTop - rise, baseLeft + drift, tilt); yield break; }
            if (t > 1.2f) SetText(count, "");

            // події
            if (!maxQ && real >= MaxQAt) { maxQ = true; SetText(status, T("pad.event.maxq")); }
            if (!staged && real >= StagingAt)
            {
                staged = true;
                stagingPause = 0.35f;   // мить тиші між ступенями
                SetText(status, T("pad.event.staging"));
                SetText(fact, T("pad.fact.staging"));
                DropStage(baseTop - rise, baseLeft + drift, tilt);
            }
            if (!turnFact && t >= 6f) { turnFact = true; SetText(status, T("pad.event.turn")); SetText(fact, T("pad.fact.turn")); }
            if (!orbit && real >= RealAscentSeconds * 0.9f) { orbit = true; SetText(status, T("pad.event.orbit")); SetText(fact, T("pad.fact.orbit")); }

            // двигун: на Max Q трохи притишують, між ступенями — пауза
            float throttle = maxQ && real < MaxQAt + 30f ? 0.7f : 1f;
            if (stagingPause > 0f) { stagingPause -= dt; throttle = 0f; }
            Flame(throttle);
            Sounds?.Engines(throttle * Mathf.Lerp(1f, 0.35f, t / AscentSeconds));

            // рух: розгін і поворот на схід (праворуч) — так Земля, що обертається, допомагає набрати швидкість
            speed += (t < 0.6f ? 60f : 260f + 70f * t) * throttle * dt;
            tilt = Mathf.Lerp(0f, 60f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.85f, t / AscentSeconds)));
            float rad = tilt * Mathf.Deg2Rad;
            rise += speed * Mathf.Cos(rad) * dt;
            drift += speed * Mathf.Sin(rad) * dt;
            rocket.style.top = baseTop - rise;
            rocket.style.left = baseLeft + drift;
            rocket.style.rotate = new Rotate(new Angle(tilt));
            rocket.style.translate = new Translate(UnityEngine.Random.Range(-1.5f, 1.5f), 0f);

            // камера летить за ракетою: земля йде вниз, згори відкривається чорний космос
            float camY = Mathf.Max(0f, H * 0.38f - (baseTop - rise));
            float camX = Mathf.Max(0f, baseLeft + drift - W * 0.62f);
            float shake = Mathf.Lerp(7f, 0f, t / 3f);
            scene.style.translate = new Translate(-camX + UnityEngine.Random.Range(-shake, shake), camY + UnityEngine.Random.Range(-shake, shake));

            Smoke(Mathf.Clamp01(0.8f + t * 0.2f));
            float bottom = baseTop - rise + size * 0.97f;
            trail.style.top = bottom;
            trail.style.height = Mathf.Max(0f, mountTop - bottom);
            trail.style.opacity = Mathf.Clamp01(1.2f - t * 0.5f);   // стовп вихлопу розвіюється

            ShowTelemetry(real);
            yield return null;
        }
        if (success) ShowTelemetry(RealAscentSeconds);
    }

    void ShowTelemetry(float real)
    {
        int sec = Mathf.RoundToInt(real);
        SetText(telemetry, T("pad.telemetry", (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00"),
                             Profile(ProfileAltKm, real).ToString("0"), (Mathf.Round(Profile(ProfileKmh, real) / 100f) * 100f).ToString("0")));
    }

    static float Profile(float[] values, float real)
    {
        for (int i = 1; i < ProfileT.Length; i++)
            if (real <= ProfileT[i]) return Mathf.Lerp(values[i - 1], values[i], Mathf.InverseLerp(ProfileT[i - 1], ProfileT[i], real));
        return values[values.Length - 1];
    }

    /// <summary>Темне небо над сценою: коли камера піднімається, згори відкривається космос.</summary>
    void AddSpace()
    {
        var space = Box(-W, -H * 40f, W * 4f, H * 40f, new Color(0.03f, 0.04f, 0.1f));
        var band = Box(-W, -H * 1.5f, W * 4f, H * 1.5f, new Color(0.07f, 0.12f, 0.25f));   // перехід: синє → чорне
        band.style.opacity = 0.9f;
        var rnd = new System.Random(5);
        for (int i = 0; i < 60; i++)
        {
            var star = Box((float)rnd.NextDouble() * W * 4f, H * 40f - (float)rnd.NextDouble() * H * 38f, 2f, 2f, Color.white);
            star.style.opacity = 0.4f + 0.6f * (float)rnd.NextDouble();
            space.Add(star);
        }
        scene.Insert(0, space);
        scene.Insert(1, band);
        extras.Add(space);
        extras.Add(band);
    }

    /// <summary>Відпрацьований ступінь відпадає і падає вниз (в океан), крутячись.</summary>
    void DropStage(float top, float left, float tilt)
    {
        float w = rocketW * 0.7f, h = size * 0.3f;
        var stage = Box(left + (rocketW - w) * 0.5f, top + size * 0.68f, w, h, new Color(0.8f, 0.82f, 0.86f));
        stage.style.borderBottomLeftRadius = 4f;
        stage.style.borderBottomRightRadius = 4f;
        stage.style.rotate = new Rotate(new Angle(tilt));
        stage.style.opacity = 1f;
        scene.Add(stage);
        extras.Add(stage);
        stage.schedule.Execute(() =>
        {
            stage.style.top = stage.style.top.value.value + 6f;
            stage.style.left = stage.style.left.value.value - 1.5f;
            stage.style.rotate = new Rotate(new Angle(stage.style.rotate.value.angle.value + 4f));
            stage.style.opacity = Mathf.Max(0f, stage.style.opacity.value - 0.012f);
        }).Every(30).Until(() => stage.parent == null || stage.style.opacity.value <= 0.01f);
    }

    /// <summary>Аварія: двигуни глохнуть, система порятунку відносить капсулу, капсула сідає на парашуті в океан.</summary>
    IEnumerator Abort(float rocketTopNow, float rocketLeftNow, float tilt)
    {
        Flame(0f);
        Sounds?.Engines(0f);
        SetText(status, T("pad.event.abort"));
        SetText(fact, T("pad.fact.abort"));

        float cw = Mathf.Max(14f, rocketW * 0.6f), ch = cw * 0.8f;
        var capsule = Box(rocketLeftNow + (rocketW - cw) * 0.5f, rocketTopNow, cw, ch, new Color(0.93f, 0.94f, 0.96f));
        capsule.style.borderTopLeftRadius = cw * 0.5f;
        capsule.style.borderTopRightRadius = cw * 0.5f;
        var jet = Box(cw * 0.3f, ch, cw * 0.4f, ch * 0.8f, new Color(1f, 0.6f, 0.2f));   // рятувальні двигуни
        capsule.Add(jet);
        var chute = Box(-cw * 0.9f, -cw * 2f, cw * 2.8f, cw * 1.4f, new Color(1f, 0.45f, 0.15f));
        chute.style.borderTopLeftRadius = cw * 1.4f;
        chute.style.borderTopRightRadius = cw * 1.4f;
        chute.style.display = DisplayStyle.None;
        capsule.Add(chute);
        scene.Add(capsule);
        extras.Add(capsule);

        float cTop = rocketTopNow, cLeft = capsule.style.left.value.value, rTop = rocketTopNow, rLeft = rocketLeftNow, rTilt = tilt;
        for (float t = 0f; t < 3.2f && !skip; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            if (t < 0.8f) { cTop -= 320f * dt; cLeft -= 60f * dt; }          // ривок угору й убік від ракети
            else
            {
                jet.style.display = DisplayStyle.None;
                chute.style.display = DisplayStyle.Flex;                     // парашут
                cTop += 45f * dt;
                cLeft += Mathf.Sin(t * 2f) * 10f * dt;
            }
            capsule.style.top = cTop;
            capsule.style.left = cLeft;

            // ракета без двигунів падає і відстає
            rTop += 140f * t * dt;
            rTilt += 25f * dt;
            rocket.style.top = rTop;
            rocket.style.left = rLeft;
            rocket.style.rotate = new Rotate(new Angle(rTilt));
            rocket.style.opacity = Mathf.Max(0f, 1f - t / 2f);
            trail.style.opacity = Mathf.Max(0f, trail.style.opacity.value - dt);
            yield return null;
        }
    }

    void ClearExtras()
    {
        foreach (var e in extras) e.RemoveFromHierarchy();
        extras.Clear();
        scene.style.translate = new Translate(0f, 0f);
        rocket.style.rotate = new Rotate(new Angle(0f));
        rocket.style.opacity = 1f;
    }

    static VisualElement Box(float left, float top, float width, float height, Color color)
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.position = Position.Absolute;
        e.style.left = left;
        e.style.top = top;
        e.style.width = width;
        e.style.height = height;
        e.style.backgroundColor = color;
        return e;
    }

    // ---------- Розкладка ----------

    /// <summary>
    /// Розкласти майданчик. size — висота ракети: heightInCrew астронавтів (але не вище екрана).
    /// aspect — ширина/висота картинки ракети; doorX/doorY — де люк капсули на картинці.
    /// </summary>
    void Layout(StyleBackground rocketTex, bool hasTex, float aspect, RocketData data)
    {
        W = view.resolvedStyle.width;
        H = view.resolvedStyle.height;
        if (float.IsNaN(W) || W < 10f) W = 1280f;
        if (float.IsNaN(H) || H < 10f) H = 720f;

        float heightInCrew = data != null && data.heightInCrew > 0f ? data.heightInCrew : 8f;
        float doorFx = data != null ? data.doorX : 0.1f;
        float doorFy = data != null ? data.doorY : 0.28f;
        if (aspect <= 0.01f) aspect = 0.3f;

        groundTop = H * 0.86f;
        cx = W * 0.62f;
        bool art = scenery != null && scenery.HasArt;
        if (art) scenery.ArtAnchors(W, H, out cx, out groundTop, out _); // ракета — на пусковому столі з картинки
        CrewSize = Mathf.Clamp(H * 0.075f, 36f, 64f);
        size = Mathf.Min(CrewSize * heightInCrew, groundTop - H * 0.06f);   // висота ракети
        CrewSize = Mathf.Min(CrewSize, size / heightInCrew);                 // якщо ракету обрізали — менші й люди
        rocketW = size * aspect;
        float left = cx - rocketW * 0.5f;
        towerX = left - 16f - 36f;

        // декорації (небо, океан, будівлі, пусковий стіл, автобус); ракета стоїть на пусковому столі
        smokeLayer.Clear();
        float mountH = 0f;
        if (scenery != null)
        {
            scenery.Build(W, H, groundTop, cx, rocketW, size, towerX);
            mountH = scenery.MountHeight;
        }
        mountTop = groundTop - mountH;
        float top = mountTop - size;
        rocketTop = top;
        doorX = left + doorFx * rocketW;
        armY = top + doorFy * size;

        Place(ground, 0f, groundTop, W, H - groundTop);
        Place(platform, cx - rocketW * 0.75f, groundTop - 12f, rocketW * 1.5f, 12f);
        // на картинці вже є і земля з дорогою, і пусковий стіл
        ground.style.display = art ? DisplayStyle.None : DisplayStyle.Flex;
        platform.style.display = art ? DisplayStyle.None : DisplayStyle.Flex;

        // ракета
        Place(rocket, left, top, rocketW, size);
        rocket.style.translate = new Translate(0f, 0f);
        rocket.EnableInClassList("pad-rocket--box", !hasTex);
        rocket.style.backgroundImage = hasTex ? rocketTex : new StyleBackground(StyleKeyword.None);
        if (flame != null)
        {
            float fw = Mathf.Max(28f, rocketW * 0.6f);
            Place(flame, (rocketW - fw) * 0.5f, size * 0.97f, fw, size * 0.3f);
            Flame(0f);
        }

        // вежа з поперечками: від землі трохи вище за люк
        float towerTop = armY - 50f;
        Place(tower, towerX, towerTop, 36f, groundTop - towerTop);
        tower.Clear();
        for (float y = 18f; y < groundTop - towerTop; y += 26f)
        {
            var bar = new VisualElement();
            bar.AddToClassList("pad-tower-bar");
            Place(bar, 0f, y, 28f, 4f);
            tower.Add(bar);
        }
        Place(arm, towerX + 36f, armY - 8f, Mathf.Max(0f, doorX - (towerX + 36f)), 10f);

        // слід вогню (видно після старту)
        trail.style.display = DisplayStyle.None;
        float tw = Mathf.Max(18f, rocketW * 0.4f);
        Place(trail, cx - tw * 0.5f, mountTop, tw, 0f);

        // клуби диму (поверх декорацій; бризки води scenery вже додала в цей шар)
        foreach (var old in puffs) old.RemoveFromHierarchy();
        puffs.Clear();
        for (int i = 0; i < 12; i++)
        {
            var puff = new VisualElement();
            puff.AddToClassList("pad-puff");
            puff.userData = (i % 2 == 0 ? -1f : 1f) * (0.15f + 0.1f * (i / 2)); // куди розлітається
            smokeLayer.Add(puff);
            puffs.Add(puff);
        }
        Smoke(0f);
    }

    static void Place(VisualElement e, float left, float top, float width, float height)
    {
        if (e == null) return;
        e.style.position = Position.Absolute;
        e.style.left = left;
        e.style.top = top;
        e.style.width = width;
        e.style.height = height;
    }

    // ---------- Екіпаж ----------

    IEnumerator Boarding(List<(string name, PadCrewFrames frames)> crew)
    {
        crewLayer.Clear();
        var walkers = new List<IEnumerator>();
        float startX = scenery != null ? scenery.VanDoorX : -CrewSize;
        for (int i = 0; i < crew.Count; i++) walkers.Add(Walker(crew[i].name, crew[i].frames, i * 0.9f, startX));

        // усі астронавти рухаються одночасно, кожен зі своєю затримкою
        bool any = true;
        while (any && !skip)
        {
            any = false;
            foreach (var w in walkers) if (w.MoveNext()) any = true;
            yield return null;
        }
        crewLayer.Clear();
    }

    IEnumerator Walker(string name, PadCrewFrames frames, float delay, float startX)
    {
        var body = new VisualElement();
        body.AddToClassList("pad-astronaut");
        body.style.width = CrewSize;
        body.style.height = CrewSize;
        if (frames != null && frames.flip) body.style.scale = new Scale(new Vector3(-1f, 1f, 1f));
        var label = new Label(name);
        label.AddToClassList("pad-name");

        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime) yield return null;
        crewLayer.Add(body);
        crewLayer.Add(label);

        float x = startX, y = groundTop;
        float liftX = towerX + 18f;
        float anim = 0f;

        // до вежі
        while (x < liftX)
        {
            x = Mathf.Min(liftX, x + WalkSpeed * Time.unscaledDeltaTime);
            anim += Time.unscaledDeltaTime;
            SetFeet(body, label, x, y, WalkFrame(frames, anim));
            yield return null;
        }
        // ліфтом угору (стоїть спиною)
        while (y > armY)
        {
            y = Mathf.Max(armY, y - WalkSpeed * 0.8f * Time.unscaledDeltaTime);
            SetFeet(body, label, x, y, frames != null ? frames.back : default);
            yield return null;
        }
        // по містку до люка
        while (x < doorX)
        {
            x = Mathf.Min(doorX, x + WalkSpeed * 0.7f * Time.unscaledDeltaTime);
            anim += Time.unscaledDeltaTime;
            SetFeet(body, label, x, y, WalkFrame(frames, anim));
            yield return null;
        }
        // заходить у ракету
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            body.style.opacity = 1f - t / 0.4f;
            label.style.opacity = 1f - t / 0.4f;
            yield return null;
        }
        body.RemoveFromHierarchy();
        label.RemoveFromHierarchy();
    }

    static bool HasImage(StyleBackground b) => b.value.texture != null || b.value.sprite != null;

    static StyleBackground WalkFrame(PadCrewFrames f, float time)
    {
        if (f == null || f.walk == null || f.walk.Length == 0) return default;
        return f.walk[(int)(time / FrameTime) % f.walk.Length];
    }

    void SetFeet(VisualElement body, Label label, float x, float y, StyleBackground frame)
    {
        body.style.left = x - CrewSize * 0.5f;
        body.style.top = y - CrewSize;
        if (HasImage(frame)) body.style.backgroundImage = frame;
        else body.AddToClassList("pad-astronaut--box"); // картинок немає — кольоровий прямокутник
        label.style.left = x - 60f;
        label.style.top = y - CrewSize - 18f;
    }

    // ---------- Ефекти ----------

    void Flame(float power)
    {
        if (flame == null) return;
        flame.style.opacity = power;
        float flicker = power > 0f ? UnityEngine.Random.Range(0.85f, 1.15f) : 1f;
        flame.style.scale = new Scale(new Vector3(1f, Mathf.Max(0.01f, power * flicker), 1f));
    }

    void Smoke(float amount)
    {
        float t = Time.unscaledTime;
        for (int i = 0; i < puffs.Count; i++)
        {
            var p = puffs[i];
            float dir = (float)p.userData;
            float wobble = Mathf.Sin(t * 2f + i) * 0.05f;
            float r = size * (0.05f + 0.1f * amount) * (1f + wobble);
            float px = cx + dir * size * (0.25f + 0.7f * amount) - r;
            float py = groundTop - r * 1.1f - (i % 3) * size * 0.02f * amount;
            p.style.left = px;
            p.style.top = py;
            p.style.width = r * 2f;
            p.style.height = r * 2f;
            p.style.borderTopLeftRadius = r;
            p.style.borderTopRightRadius = r;
            p.style.borderBottomLeftRadius = r;
            p.style.borderBottomRightRadius = r;
            p.style.opacity = Mathf.Clamp01(amount) * 0.85f;
        }
    }

    static void Shake(VisualElement e, float px)
    {
        if (e == null) return;
        e.style.translate = new Translate(UnityEngine.Random.Range(-px, px), UnityEngine.Random.Range(-px, px));
    }

    static void SetText(TextElement e, string text) { if (e != null) e.text = text; }
}
