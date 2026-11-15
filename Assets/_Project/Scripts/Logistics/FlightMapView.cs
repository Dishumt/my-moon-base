// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Анімація польоту (роль 04): Дніпро → виток навколо Землі → політ до Місяця → орбіта Місяця → посадка на південному полюсі.
/// Земля обертається, Місяць рухається своєю орбітою, а ракета летить пунктиром: попереду — пунктир, позаду — суцільна лінія.
/// Малюється кодом (Painter2D), картинка потрібна тільки для ракети. LaunchScreen запускає Play() після вдалого старту.
/// Масштаб не справжній (на екрані підписано): по-справжньому між Землею і Місяцем вмістилося б 30 Земель.
/// </summary>
public class FlightMapView
{
    // місто старту: Дніпро
    const float DniproLat = 48.45f, DniproLon = 35.05f;
    static readonly float[] PhaseSeconds = { 1.6f, 2.2f, 4.2f, 2.2f, 1.8f };

    readonly VisualElement view;
    readonly Label title, caption, fact, scaleNote;
    readonly Button skipButton;
    readonly MapCanvas canvas;
    readonly VisualElement rocket;
    readonly Label earthLabel, moonLabel, cityLabel, poleLabel;
    bool skip;
    float rocketW = 12f, rocketH = 34f;

    // геометрія (пікселі)
    float W, H, R, r, D, earthSpin;
    Vector2 E, moonNow, moonEnd;
    readonly List<Vector2> path = new List<Vector2>();
    readonly List<int> phaseStart = new List<int>();
    float progressIndex;     // скільки точок шляху вже пройдено (дробове)
    bool burning;            // чи горить двигун (вогник позаду ракети)
    readonly List<Vector3> stars = new List<Vector3>();
    static readonly Vector2[] Continents =   // широта, довгота центрів «плям» континентів
    {
        new Vector2(50, 15), new Vector2(5, 20), new Vector2(45, 90), new Vector2(25, 75), new Vector2(45, -100),
        new Vector2(-15, -60), new Vector2(-25, 135), new Vector2(60, 60), new Vector2(15, -90)
    };

    static string T(string key) => LaunchText.Get(key);

    /// <summary>Полотно, на якому малюємо Землю, Місяць і шлях.</summary>
    class MapCanvas : VisualElement
    {
        public System.Action<Painter2D> Draw;
        public MapCanvas()
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0; style.top = 0; style.right = 0; style.bottom = 0;
            generateVisualContent += ctx => Draw?.Invoke(ctx.painter2D);
        }
    }

    public FlightMapView(VisualElement root)
    {
        view = root.Q("flight-view");
        title = root.Q<Label>("flight-title");
        caption = root.Q<Label>("flight-caption");
        fact = root.Q<Label>("flight-fact");
        scaleNote = root.Q<Label>("flight-scale");
        skipButton = root.Q<Button>("flight-skip");
        if (view == null) return;
        if (skipButton != null) skipButton.clicked += () => skip = true;

        canvas = new MapCanvas { Draw = DrawMap };
        view.Insert(0, canvas);
        earthLabel = MapLabel("flight-map-label");
        moonLabel = MapLabel("flight-map-label");
        cityLabel = MapLabel("flight-city");
        poleLabel = MapLabel("flight-city");
        rocket = new VisualElement { pickingMode = PickingMode.Ignore };
        rocket.AddToClassList("flight-rocket");
        canvas.Add(rocket);
        Show(false);
    }

    public bool Ready => view != null && caption != null && fact != null;

    void Show(bool visible) => view?.EnableInClassList("flight-view--hidden", !visible);

    Label MapLabel(string cls)
    {
        var l = new Label { pickingMode = PickingMode.Ignore };
        l.AddToClassList(cls);
        canvas.Add(l);
        return l;
    }

    // ---------- Послідовність ----------

    public IEnumerator Play(Texture2D rocketTex, float aspect)
    {
        skip = false;
        Show(true);
        SetText(title, T("flight.title"));
        SetText(scaleNote, T("flight.not_to_scale"));
        SetText(skipButton, T("pad.skip"));
        SetText(earthLabel, T("flight.earth"));
        SetText(moonLabel, T("flight.moon"));
        SetText(cityLabel, T("flight.dnipro"));
        SetText(poleLabel, T("flight.pole"));

        if (rocketTex != null)
        {
            rocket.style.backgroundImage = new StyleBackground(rocketTex);
            rocket.RemoveFromClassList("flight-rocket--box");
        }
        else rocket.AddToClassList("flight-rocket--box");
        rocketH = 34f;
        rocketW = Mathf.Clamp(rocketH * aspect, 6f, 20f);
        rocket.style.width = rocketW;
        rocket.style.height = rocketH;

        yield return null; // чекаємо на розміри
        yield return null;
        Build();

        for (int phase = 0; phase < PhaseSeconds.Length && !skip; phase++)
        {
            SetText(caption, T("flight.step" + (phase + 1)));
            SetText(fact, T("flight.fact" + (phase + 1)));
            int from = phaseStart[phase], to = phaseStart[phase + 1] - 1;
            float dur = PhaseSeconds[phase];
            for (float t = 0f; t < dur && !skip; t += Time.unscaledDeltaTime)
            {
                float u = t / dur;
                if (phase == 0 || phase == 4) u = u * u * (3f - 2f * u); // плавно рушає і плавно сідає
                progressIndex = Mathf.Lerp(from, to, u);
                burning = phase == 0 || phase == 4 || (phase == 2 && u < 0.12f) || (phase == 3 && u < 0.15f);
                Animate(Time.unscaledDeltaTime, phase);
                yield return null;
            }
        }

        // сіли
        progressIndex = path.Count - 1;
        burning = false;
        moonNow = moonEnd;
        Animate(0f, PhaseSeconds.Length - 1);
        SetText(caption, T("flight.landed"));
        for (float t = 0f; t < (skip ? 0.5f : 1.6f); t += Time.unscaledDeltaTime) yield return null;
        Show(false);
    }

    // ---------- Шлях ----------

    void Build()
    {
        W = view.resolvedStyle.width;
        H = view.resolvedStyle.height;
        if (float.IsNaN(W) || W < 10f) W = 1280f;
        if (float.IsNaN(H) || H < 10f) H = 720f;

        E = new Vector2(W * 0.2f, H * 0.56f);
        R = H * 0.17f;
        r = R * 0.27f;                       // Місяць справді приблизно в 3,7 раза менший за Землю
        D = W * 0.62f;
        moonEnd = E + D * Dir(0f);
        moonNow = E + D * Dir(-9f);
        earthSpin = DniproLon + 25f;         // Дніпро трохи ліворуч від центру диска

        var rnd = new System.Random(3);
        stars.Clear();
        for (int i = 0; i < 90; i++)
            stars.Add(new Vector3((float)rnd.NextDouble() * W, (float)rnd.NextDouble() * H, 0.3f + 0.7f * (float)rnd.NextDouble()));

        path.Clear();
        phaseStart.Clear();
        Vector2 city = Project(DniproLat, DniproLon, earthSpin);
        float a0 = Mathf.Atan2(city.y - E.y, city.x - E.x);
        float r1 = R * 1.35f;

        // 0) старт із Дніпра: угору від поверхні і по спіралі на орбіту
        phaseStart.Add(0);
        for (int i = 0; i <= 40; i++)
        {
            float u = i / 40f;
            if (u < 0.25f) { path.Add(Vector2.Lerp(city, E + R * 1.02f * Dir(a0 * Mathf.Rad2Deg), u / 0.25f)); continue; }
            float k = (u - 0.25f) / 0.75f;
            float ang = a0 + k * Mathf.PI * 0.5f;
            path.Add(E + Mathf.Lerp(R * 1.02f, r1, Mathf.SmoothStep(0f, 1f, k)) * Dir(ang * Mathf.Rad2Deg));
        }

        // 1) виток навколо Землі — до верхньої точки, звідки летимо праворуч до Місяця
        phaseStart.Add(path.Count);
        float aStart = a0 + Mathf.PI * 0.5f;
        float aDep = -Mathf.PI * 0.5f;
        while (aDep < aStart + Mathf.PI * 1.2f) aDep += Mathf.PI * 2f;
        for (int i = 1; i <= 120; i++) path.Add(E + r1 * Dir(Mathf.Lerp(aStart, aDep, i / 120f) * Mathf.Rad2Deg));

        // 2) політ до Місяця — цілимося туди, де Місяць БУДЕ
        phaseStart.Add(path.Count);
        float ro = r * 2.4f;
        Vector2 S = path[path.Count - 1];
        Vector2 A = moonEnd + ro * Dir(200f);
        Vector2 C1 = S + new Vector2(D * 0.45f, -D * 0.12f);
        Vector2 C2 = A + new Vector2(-D * 0.3f, -D * 0.08f);
        for (int i = 1; i <= 160; i++) path.Add(Bezier(S, C1, C2, A, i / 160f));

        // 3) орбіта Місяця
        phaseStart.Add(path.Count);
        for (int i = 1; i <= 120; i++) path.Add(moonEnd + ro * Dir(Mathf.Lerp(200f, 420f, i / 120f)));

        // 4) посадка на південному полюсі (низ диска Місяця)
        phaseStart.Add(path.Count);
        for (int i = 1; i <= 50; i++)
        {
            float u = i / 50f;
            path.Add(moonEnd + Mathf.Lerp(ro, r, u) * Dir(Mathf.Lerp(420f, 450f, u)));
        }
        phaseStart.Add(path.Count);

        progressIndex = 0f;
        Animate(0f, 0);
    }

    void Animate(float dt, int phase)
    {
        earthSpin += 22f * dt;                                     // Земля обертається
        float moonK = phase >= 3 ? 1f : Mathf.Clamp01(progressIndex / Mathf.Max(1f, phaseStart[3]));
        moonNow = E + D * Dir(Mathf.Lerp(-9f, 0f, moonK));         // Місяць іде своєю орбітою

        // ракета і підписи
        int i = Mathf.Clamp(Mathf.FloorToInt(progressIndex), 0, path.Count - 1);
        int j = Mathf.Min(i + 1, path.Count - 1);
        Vector2 pos = Vector2.Lerp(path[i], path[j], progressIndex - i);
        Vector2 dir = path[j] - path[Mathf.Max(0, i - 1)];
        float ang = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f : 0f;
        rocket.style.left = pos.x - rocketW * 0.5f;
        rocket.style.top = pos.y - rocketH * 0.5f;
        rocket.style.rotate = new Rotate(new Angle(ang));

        Place(earthLabel, E + new Vector2(-100f, R + 10f));
        Place(moonLabel, moonNow + new Vector2(-100f, -r - 30f));
        Place(poleLabel, moonEnd + new Vector2(-100f, r + 8f));
        Vector2 city = Project(DniproLat, DniproLon, earthSpin);
        bool cityVisible = Depth(DniproLat, DniproLon, earthSpin) > 0.1f;
        cityLabel.style.display = cityVisible ? DisplayStyle.Flex : DisplayStyle.None;
        Place(cityLabel, city + new Vector2(-100f, -26f));

        canvas.MarkDirtyRepaint();
    }

    static void Place(Label l, Vector2 p)
    {
        l.style.left = p.x;
        l.style.top = p.y;
    }

    // ---------- Малювання ----------

    void DrawMap(Painter2D p)
    {
        if (path.Count == 0) return;

        foreach (var s in stars) Dot(p, new Vector2(s.x, s.y), 1.1f, new Color(1f, 1f, 1f, 0.35f * s.z));

        // Земля: океан, континенти, що обертаються, атмосфера
        Dot(p, E, R * 1.06f, new Color(0.45f, 0.7f, 1f, 0.15f));
        Dot(p, E, R, new Color(0.13f, 0.38f, 0.72f));
        foreach (var c in Continents)
        {
            float depth = Depth(c.x, c.y, earthSpin);
            if (depth < 0.2f) continue;
            Dot(p, Project(c.x, c.y, earthSpin), R * 0.2f * Mathf.Lerp(0.5f, 1f, depth), new Color(0.27f, 0.6f, 0.33f));
        }
        if (Depth(DniproLat, DniproLon, earthSpin) > 0.1f)
        {
            Vector2 city = Project(DniproLat, DniproLon, earthSpin);
            Dot(p, city, 5f, new Color(1f, 0.78f, 0.24f));
            Dot(p, city, 2.5f, new Color(0.1f, 0.1f, 0.12f));
        }

        // Місяць і база на південному полюсі
        Dot(p, moonNow, r, new Color(0.8f, 0.79f, 0.74f));
        Dot(p, moonNow + new Vector2(-r * 0.3f, -r * 0.25f), r * 0.22f, new Color(0.68f, 0.67f, 0.62f));
        Dot(p, moonNow + new Vector2(r * 0.35f, r * 0.15f), r * 0.15f, new Color(0.68f, 0.67f, 0.62f));
        Dot(p, moonEnd + new Vector2(0f, r), 4f, new Color(0.37f, 0.83f, 0.96f));

        // шлях: пройдене — суцільна помаранчева лінія, попереду — пунктир
        int done = Mathf.Clamp(Mathf.FloorToInt(progressIndex), 0, path.Count - 1);
        p.strokeColor = new Color(1f, 0.6f, 0.2f);
        p.lineWidth = 2.5f;
        p.lineCap = LineCap.Round;
        p.BeginPath();
        p.MoveTo(path[0]);
        for (int i = 1; i <= done; i++) p.LineTo(path[i]);
        p.Stroke();

        float since = 0f;
        for (int i = done + 1; i < path.Count; i++)
        {
            since += Vector2.Distance(path[i], path[i - 1]);
            if (since < 9f) continue;
            since = 0f;
            Dot(p, path[i], 1.6f, new Color(0.6f, 0.85f, 1f, 0.8f));
        }

        // вогник двигуна позаду ракети
        if (burning)
        {
            int k = Mathf.Max(0, done - 3);
            Dot(p, path[k], 4f + Random.Range(0f, 2f), new Color(1f, 0.75f, 0.3f, 0.9f));
        }
    }

    static void Dot(Painter2D p, Vector2 c, float radius, Color color)
    {
        p.fillColor = color;
        p.BeginPath();
        p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Fill();
    }

    // ---------- Геометрія ----------

    static Vector2 Dir(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>Точка на диску Землі для широти/довготи (вид збоку; spin — яка довгота зараз у центрі).</summary>
    Vector2 Project(float lat, float lon, float spin)
    {
        float la = lat * Mathf.Deg2Rad, lo = (lon - spin) * Mathf.Deg2Rad;
        return E + new Vector2(R * Mathf.Cos(la) * Mathf.Sin(lo), -R * Mathf.Sin(la));
    }

    /// <summary>Наскільки точка дивиться на нас: 1 — у центрі диска, 0 — на краю, менше 0 — з того боку.</summary>
    static float Depth(float lat, float lon, float spin) => Mathf.Cos(lat * Mathf.Deg2Rad) * Mathf.Cos((lon - spin) * Mathf.Deg2Rad);

    static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    static void SetText(TextElement e, string text) { if (e != null) e.text = text; }
}
