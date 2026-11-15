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
    float W, H, groundTop, size, rocketW, cx, towerX, armY, doorX;
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
        Show(false);
    }

    /// <summary>Чи знайшлися всі частини в UXML.</summary>
    public bool Ready => view != null && scene != null && ground != null && platform != null && tower != null && arm != null
                         && rocket != null && trail != null && smokeLayer != null && crewLayer != null
                         && title != null && count != null && status != null && fact != null;

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
        Show(true);
        SetText(title, T("pad.title"));
        SetText(skipButton, T("pad.skip"));
        SetText(count, "");
        SetText(fact, "");
        SetText(status, T("pad.boarding"));

        yield return null; // чекаємо, поки UI Toolkit порахує розміри
        yield return null;
        Layout(rocketTex, hasTex, aspect, rocketData);

        // 1) екіпаж іде до ракети
        if (!skip) yield return Boarding(crew);

        // 2) місток відходить
        SetText(status, T("pad.arm"));
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
            // факти під час відліку: як летить ракета → скільки летіти → коли можна летіти; на старті — перевантаження
            if (n == 10) SetText(fact, T("pad.fact.1"));
            else if (n == 8) SetText(fact, T("pad.fact.6"));   // справжня ракета — як 600 слонів
            else if (n == 6) SetText(fact, T("pad.fact.4"));
            else if (n == 3) SetText(fact, T("pad.fact.5"));
            for (float t = 0f; t < 1f && !skip; t += Time.unscaledDeltaTime)
            {
                count.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t * 4f)));
                if (n <= 3) { Smoke(0.25f + (3 - n + t) / 3f * 0.5f); Shake(rocket, 1.5f + (3 - n) * 1.2f); }
                if (n <= 2) Flame(0.4f + (2 - n + t) * 0.3f);
                yield return null;
            }
        }

        // 4) СТАРТ!
        SetText(count, T("pad.liftoff"));
        SetText(fact, T("pad.fact.3"));
        SetText(status, "");
        count.style.scale = new Scale(Vector3.one);
        trail.style.display = DisplayStyle.Flex;
        float baseTop = groundTop - size;
        float rise = 0f, speed = 0f;
        for (float t = 0f; t < 4f && !skip; t += Time.unscaledDeltaTime)
        {
            float dt = Time.unscaledDeltaTime;
            speed += (t < 0.6f ? 60f : 420f) * dt;   // спершу ледве відривається, потім розганяється
            rise += speed * dt;
            if (!success && t > 2.2f) break;         // невдача: двигуни глохнуть на висоті
            rocket.style.top = baseTop - rise;
            rocket.style.translate = new Translate(UnityEngine.Random.Range(-2f, 2f), 0f);
            Flame(1f);
            Smoke(Mathf.Clamp01(0.8f + t * 0.2f));
            float bottom = baseTop - rise + size * 0.97f;
            trail.style.top = bottom;
            trail.style.height = Mathf.Max(0f, groundTop - bottom);
            trail.style.opacity = Mathf.Clamp01(1.2f - t * 0.3f);
            Shake(scene, Mathf.Lerp(7f, 0f, t / 3f));
            yield return null;
        }
        scene.style.translate = new Translate(0f, 0f);
        if (skip && success) rocket.style.top = -size * 2f; // пропустили — ракета вже далеко

        // 5) результат
        SetText(count, "");
        SetText(status, T(success ? "pad.success" : "pad.fail"));
        status.EnableInClassList("c-ok", success);
        status.EnableInClassList("c-bad", !success);
        if (!success) Flame(0f);
        float wait = skip ? 0.6f : 1.8f;
        for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;
        status.RemoveFromClassList("c-ok");
        status.RemoveFromClassList("c-bad");
        Show(false);
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
        CrewSize = Mathf.Clamp(H * 0.075f, 36f, 64f);
        size = Mathf.Min(CrewSize * heightInCrew, groundTop - H * 0.06f);   // висота ракети
        CrewSize = Mathf.Min(CrewSize, size / heightInCrew);                 // якщо ракету обрізали — менші й люди
        rocketW = size * aspect;
        cx = W * 0.62f;
        float left = cx - rocketW * 0.5f, top = groundTop - size;
        doorX = left + doorFx * rocketW;
        armY = top + doorFy * size;
        towerX = left - 16f - 36f;

        Place(ground, 0f, groundTop, W, H - groundTop);
        Place(platform, cx - rocketW * 0.75f, groundTop - 12f, rocketW * 1.5f, 12f);

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
        Place(trail, cx - tw * 0.5f, groundTop, tw, 0f);

        // клуби диму
        smokeLayer.Clear();
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
        for (int i = 0; i < crew.Count; i++) walkers.Add(Walker(crew[i].name, crew[i].frames, i * 1.1f));

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

    IEnumerator Walker(string name, PadCrewFrames frames, float delay)
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

        float x = -CrewSize, y = groundTop;
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
