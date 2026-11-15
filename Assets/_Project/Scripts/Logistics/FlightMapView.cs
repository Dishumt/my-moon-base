// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Анімація польоту (роль 04): Дніпро → виток навколо Землі → переліт до Місяця → орбіта Місяця → посадка на південному полюсі.
///
/// Усе рухається за СПРАВЖНІМ ГОДИННИКОМ МІСІЇ (на екрані — «Час місії»), лише прискореним по-різному в кожній фазі:
/// - Місяць увесь час іде своєю орбітою зі справжньою швидкістю (≈ 0.55° за годину): за 3 дні перельоту — ≈ 40°;
/// - переліт — це половинка еліпса навколо Землі (закони Кеплера): біля Землі корабель мчить, вдалині сповільнюється;
///   цілимося туди, де Місяць БУДЕ через 3 дні;
/// - на орбіті Місяця корабель кружляє навколо Місяця, що рухається, і сідає на південному полюсі (база рухається з Місяцем).
/// Двигун горить лише під час маневрів (старт, розгін до Місяця, гальмування, посадка), решту часу корабель летить сам.
/// Відстані не в масштабі (на екрані підписано): по-справжньому між Землею і Місяцем вмістилося б 30 Земель.
/// Малюється кодом (Painter2D), картинка потрібна тільки для ракети. LaunchScreen запускає Play() після вдалого старту.
/// </summary>
public class FlightMapView
{
    // місто старту: Дніпро
    const float DniproLat = 48.45f, DniproLon = 35.05f;

    // фази: скільки триває по-справжньому (години) і на екрані (секунди)
    static readonly float[] PhaseHours = { 0.2f, 2.5f, 72f, 4f, 1f };     // старт, 1.5 витка, переліт ≈ 3 дні, 2 витки біля Місяця, посадка
    static readonly float[] PhaseSeconds = { 2.0f, 2.4f, 5.0f, 2.6f, 2.2f };
    const float MoonDegPerHour = 360f / (27.3f * 24f);   // Місяць облітає Землю за 27.3 доби
    const float EarthDegPerHour = 15f;                   // Земля обертається за 24 години
    const float ArrivalAngle = 10f;                      // де на екрані буде Місяць у мить прильоту (градуси)

    readonly VisualElement view;
    readonly Label title, caption, fact, scaleNote;
    readonly Button skipButton;
    readonly MapCanvas canvas;
    readonly VisualElement rocket;
    readonly Label earthLabel, moonLabel, cityLabel, poleLabel, clockLabel;
    bool skip;
    float rocketW = 12f, rocketH = 34f;

    // геометрія (пікселі)
    float W, H, R, r, D, ro, r1, ra;
    Vector2 E;
    float cityAngle, cityRadius, parkingEnd;
    float missionHours, earthSpin;
    int phase;
    Vector2 ship, shipPrev;
    float lastAngle, landTilt;
    bool burning, landed;
    readonly List<Vector2> flown = new List<Vector2>();     // пройдений шлях
    readonly List<Vector3> stars = new List<Vector3>();
    static readonly Vector2[] Continents =   // широта, довгота центрів «плям» континентів
    {
        new Vector2(50, 15), new Vector2(5, 20), new Vector2(45, 90), new Vector2(25, 75), new Vector2(45, -100),
        new Vector2(-15, -60), new Vector2(-25, 135), new Vector2(60, 60), new Vector2(15, -90)
    };

    static string T(string key) => LaunchText.Get(key);
    static string T(string key, params object[] args) => LaunchText.Get(key, args);

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
        clockLabel = new Label { pickingMode = PickingMode.Ignore };
        clockLabel.AddToClassList("flight-clock");
        view.Add(clockLabel);
        rocket = new VisualElement { pickingMode = PickingMode.Ignore };
        rocket.AddToClassList("flight-rocket");
        canvas.Add(rocket);
        Show(false);
    }

    public bool Ready => view != null && caption != null && fact != null;

    /// <summary>true — закінчити на орбіті Місяця («Починаємо спуск!»): саму посадку покаже LandingView.</summary>
    public bool EndInLunarOrbit;

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

        int lastPhase = EndInLunarOrbit ? PhaseSeconds.Length - 1 : PhaseSeconds.Length;
        for (phase = 0; phase < lastPhase && !skip; phase++)
        {
            SetText(caption, T("flight.step" + (phase + 1)));
            SetText(fact, T("flight.fact" + (phase + 1)));
            float startHours = missionHours;
            float dur = PhaseSeconds[phase];
            for (float t = 0f; t < dur && !skip; t += Time.unscaledDeltaTime)
            {
                float u = Mathf.Clamp01(t / dur);
                missionHours = startHours + PhaseHours[phase] * u;
                Step(phase, u, Time.unscaledDeltaTime);
                yield return null;
            }
            missionHours = startHours + PhaseHours[phase];
        }

        if (EndInLunarOrbit)
        {
            // далі — сцена посадки
            phase = 3;
            if (skip) { missionHours = PhaseHours[0] + PhaseHours[1] + PhaseHours[2] + PhaseHours[3]; Step(3, 1f, 0f); }
            SetText(caption, T("flight.descent"));
            for (float t = 0f; t < (skip ? 0.3f : 1.2f); t += Time.unscaledDeltaTime) yield return null;
            Show(false);
            yield break;
        }

        // сіли (або пропустили — одразу на базі)
        phase = PhaseSeconds.Length - 1;
        missionHours = TotalHours();
        Step(phase, 1f, 0f);
        landed = true;
        burning = false;
        SetText(caption, T("flight.landed"));
        for (float t = 0f; t < (skip ? 0.5f : 1.6f); t += Time.unscaledDeltaTime)
        {
            missionHours += Time.unscaledDeltaTime * 0.5f;   // годинник іде далі, Місяць і база теж рухаються
            Step(phase, 1f, Time.unscaledDeltaTime);
            yield return null;
        }
        Show(false);
    }

    static float TotalHours()
    {
        float h = 0f;
        foreach (var x in PhaseHours) h += x;
        return h;
    }

    // ---------- Геометрія ----------

    void Build()
    {
        W = view.resolvedStyle.width;
        H = view.resolvedStyle.height;
        if (float.IsNaN(W) || W < 10f) W = 1280f;
        if (float.IsNaN(H) || H < 10f) H = 720f;

        E = new Vector2(W * 0.2f, H * 0.58f);
        R = H * 0.17f;
        r = R * 0.27f;                        // Місяць справді приблизно в 3.7 раза менший за Землю
        D = Mathf.Min(W * 0.55f, H * 0.9f);   // радіус орбіти Місяця (не в масштабі; так Місяць увесь час на екрані)
        ro = r * 2.4f;                        // орбіта корабля навколо Місяця
        r1 = R * 1.35f;                       // орбіта корабля навколо Землі
        ra = D - ro;                          // найдальша точка перельоту: з ближнього боку Місяця

        var rnd = new System.Random(3);
        stars.Clear();
        for (int i = 0; i < 90; i++)
            stars.Add(new Vector3((float)rnd.NextDouble() * W, (float)rnd.NextDouble() * H, 0.3f + 0.7f * (float)rnd.NextDouble()));

        missionHours = 0f;
        earthSpin = DniproLon + 25f;          // Дніпро трохи ліворуч від центру диска
        Vector2 city = Project(DniproLat, DniproLon, earthSpin);
        cityAngle = Mathf.Atan2(city.y - E.y, city.x - E.x) * Mathf.Rad2Deg;
        cityRadius = (city - E).magnitude;
        // виток навколо Землі: від кінця старту до точки розгону (протилежно місцю зустрічі з Місяцем)
        float burnAngle = ArrivalAngle + 180f;
        parkingEnd = burnAngle;
        while (parkingEnd < cityAngle + 40f + 450f) parkingEnd += 360f;   // ≈ 1.5 витка, як у місій «Аполлон»

        flown.Clear();
        landed = false;
        landTilt = 0f;
        lastAngle = 0f;
        phase = 0;
        ship = city;
        shipPrev = city;
        Step(0, 0f, 0f);
    }

    /// <summary>Де Місяць зараз: справжня кутова швидкість, у мить прильоту він рівно в ArrivalAngle.</summary>
    Vector2 MoonAt(float hours)
    {
        float arrival = PhaseHours[0] + PhaseHours[1] + PhaseHours[2];
        return E + D * Dir(ArrivalAngle + MoonDegPerHour * (hours - arrival));
    }

    /// <summary>Перелетний еліпс (фокус — Земля): true anomaly ν від 0 (біля Землі) до 180° (біля Місяця).</summary>
    Vector2 TransferPoint(float nuDeg)
    {
        float a = (r1 + ra) * 0.5f, e = (ra - r1) / (ra + r1);
        float nu = nuDeg * Mathf.Deg2Rad;
        float rad = a * (1f - e * e) / (1f + e * Mathf.Cos(nu));
        return E + rad * Dir(ArrivalAngle + 180f + nuDeg);
    }

    /// <summary>Рівняння Кеплера: за часом (частка 0..1 половини оберту) — true anomaly. Вдалині від Землі корабель повільніший.</summary>
    float TransferAnomaly(float u)
    {
        float e = (ra - r1) / (ra + r1);
        float M = Mathf.PI * u;                                   // середня аномалія росте рівномірно з часом
        float Ecc = M;
        for (int i = 0; i < 12; i++) Ecc -= (Ecc - e * Mathf.Sin(Ecc) - M) / (1f - e * Mathf.Cos(Ecc));
        float nu = 2f * Mathf.Atan2(Mathf.Sqrt(1f + e) * Mathf.Sin(Ecc * 0.5f), Mathf.Sqrt(1f - e) * Mathf.Cos(Ecc * 0.5f));
        return nu * Mathf.Rad2Deg;
    }

    /// <summary>Один кадр: де корабель, Місяць, Земля; чи горить двигун.</summary>
    void Step(int ph, float u, float dt)
    {
        // Земля обертається 15° за годину. За 3 дні перельоту вона обернулась би тричі — на екрані сповільнюємо, щоб не мерехтіла
        earthSpin = DniproLon + 25f + EarthDegPerHour * Mathf.Min(missionHours, 6f) + 25f * Mathf.Max(0f, missionHours - 6f) / 24f;
        Vector2 moon = MoonAt(missionHours);

        Vector2 pos;
        switch (ph)
        {
            case 0: // старт: спершу вгору, потім нахиляємось і виходимо на коло орбіти (гравітаційний поворот)
            {
                float k = Mathf.SmoothStep(0f, 1f, u);
                float rad = Mathf.Lerp(cityRadius, r1, Mathf.Sqrt(k));
                pos = E + rad * Dir(cityAngle + 40f * k * k);
                burning = true;
                break;
            }
            case 1: // виток навколо Землі: двигун вимкнено, корабель летить сам
                pos = E + r1 * Dir(Mathf.Lerp(cityAngle + 40f, parkingEnd, u));
                burning = false;
                break;
            case 2: // переліт: короткий розгін, далі політ по еліпсу без двигуна
                pos = TransferPoint(TransferAnomaly(u));
                burning = u < 0.06f;
                break;
            case 3: // орбіта Місяця: кружляємо навколо Місяця, що рухається; на початку — гальмування
                pos = moon + ro * Dir(Mathf.Lerp(ArrivalAngle + 180f, ArrivalAngle - 180f, u));   // з того місця, де закінчився переліт
                burning = u < 0.1f;
                break;
            default: // посадка: знижуємось до південного полюса (низ диска)
            {
                float k = Mathf.SmoothStep(0f, 1f, u);
                pos = moon + Mathf.Lerp(ro, r, k) * Dir(Mathf.Lerp(ArrivalAngle - 180f, -270f, u));   // -270° = низ диска
                burning = !landed;
                landTilt = k;
                break;
            }
        }

        shipPrev = ship;
        ship = pos;
        if (flown.Count == 0 || (flown[flown.Count - 1] - pos).sqrMagnitude > 4f) flown.Add(pos);

        // ракета дивиться туди, куди летить
        Vector2 dir = ship - shipPrev;
        float ang = dir.sqrMagnitude > 0.0001f ? Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f : lastAngle;
        if (ph == PhaseSeconds.Length - 1) ang = Mathf.LerpAngle(ang, 180f, landed ? 1f : landTilt);   // сідаємо «ногами» до Місяця
        lastAngle = ang;
        rocket.style.left = pos.x - rocketW * 0.5f;
        rocket.style.top = pos.y - rocketH * 0.5f;
        rocket.style.rotate = new Rotate(new Angle(ang));

        Place(earthLabel, E + new Vector2(-100f, R + 10f));
        Place(moonLabel, moon + new Vector2(-100f, -r - 30f));
        Place(poleLabel, moon + new Vector2(-100f, r + 8f));
        Vector2 city = Project(DniproLat, DniproLon, earthSpin);
        bool cityVisible = ph == 0 && Depth(DniproLat, DniproLon, earthSpin) > 0.1f;
        cityLabel.style.display = cityVisible ? DisplayStyle.Flex : DisplayStyle.None;
        Place(cityLabel, city + new Vector2(-100f, -26f));

        int days = (int)(missionHours / 24f), hours = (int)(missionHours % 24f);
        SetText(clockLabel, T("flight.clock", days, hours.ToString("00"), Mathf.FloorToInt(missionHours * 60f % 60f).ToString("00")));

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
        if (R <= 0f) return;

        foreach (var s in stars) Dot(p, new Vector2(s.x, s.y), 1.1f, new Color(1f, 1f, 1f, 0.35f * s.z));

        // орбіта Місяця — тонке коло: видно, що Місяць увесь час рухається по ній
        DashedCircle(p, E, D, new Color(0.7f, 0.75f, 0.85f, 0.25f), 6f);

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

        // Місяць зараз і база на південному полюсі (рухається разом із Місяцем)
        Vector2 moon = MoonAt(missionHours);
        Dot(p, moon, r, new Color(0.8f, 0.79f, 0.74f));
        Dot(p, moon + new Vector2(-r * 0.3f, -r * 0.25f), r * 0.22f, new Color(0.68f, 0.67f, 0.62f));
        Dot(p, moon + new Vector2(r * 0.35f, r * 0.15f), r * 0.15f, new Color(0.68f, 0.67f, 0.62f));
        Dot(p, moon + new Vector2(0f, r), 4f, new Color(0.37f, 0.83f, 0.96f));

        // план попереду — пунктир: до розгону — переліт до точки зустрічі; біля Місяця — орбіта навколо нього
        var plan = new Color(0.6f, 0.85f, 1f, 0.8f);
        if (phase <= 2)
        {
            float fromNu = phase == 2 ? TransferAnomaly(Mathf.Clamp01(CurrentU())) : 0f;
            float since = 0f;
            Vector2 prev = TransferPoint(fromNu);
            for (float nu = fromNu; nu <= 180f; nu += 1f)
            {
                Vector2 q = TransferPoint(nu);
                since += Vector2.Distance(q, prev);
                prev = q;
                if (since < 9f) continue;
                since = 0f;
                Dot(p, q, 1.6f, plan);
            }
            // куди прилетимо: Місяць там, де він БУДЕ
            Vector2 meet = MoonAt(PhaseHours[0] + PhaseHours[1] + PhaseHours[2]);
            DashedCircle(p, meet, r, new Color(0.8f, 0.79f, 0.74f, 0.35f), 4f);
        }
        else if (!landed) DashedCircle(p, moon, ro, plan, 5f);

        // пройдене — суцільна помаранчева лінія
        if (flown.Count > 1)
        {
            p.strokeColor = new Color(1f, 0.6f, 0.2f);
            p.lineWidth = 2.5f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(flown[0]);
            for (int i = 1; i < flown.Count; i++) p.LineTo(flown[i]);
            p.Stroke();
        }

        // вогник двигуна позаду ракети
        if (burning)
        {
            Vector2 back = ship - shipPrev;
            back = back.sqrMagnitude > 0.0001f ? -back.normalized : Vector2.down;
            Dot(p, ship + back * (rocketH * 0.55f), 4f + Random.Range(0f, 2f), new Color(1f, 0.75f, 0.3f, 0.9f));
        }
    }

    float CurrentU()
    {
        float start = 0f;
        for (int i = 0; i < 2; i++) start += PhaseHours[i];
        return (missionHours - start) / PhaseHours[2];
    }

    static void DashedCircle(Painter2D p, Vector2 c, float radius, Color color, float gapPx)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius / gapPx), 12, 400);
        for (int i = 0; i < n; i++) Dot(p, c + radius * Dir(360f * i / n), 1.2f, color);
    }

    static void Dot(Painter2D p, Vector2 c, float radius, Color color)
    {
        p.fillColor = color;
        p.BeginPath();
        p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Fill();
    }

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

    static void SetText(TextElement e, string text) { if (e != null) e.text = text; }
}
