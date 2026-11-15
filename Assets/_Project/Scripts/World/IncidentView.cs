// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Світ для випадкових поломок (IncidentSystem):
///  - над модулем з поломкою підстрибує червоний знак «!»;
///  - угорі екрана напис: що сталося, що робити (дійти й тримати F) і смужка ремонту;
///  - інженер у модулі (або біля обладнання надворі) тримає F → смужка росте → «Полагоджено! +наука» і факт NASA.
/// Нічого не треба ставити в сцену: створюється сам при старті гри (без GameBootstrap просто мовчить).
/// Кнопки для перевірки — у DebugPanel: «Поломка: випадкова / унітаз / пил / метеорит».
/// </summary>
public class IncidentView : MonoBehaviour
{
    static readonly Color BrokenBack = new Color(0.55f, 0.12f, 0.05f, 0.94f);
    static readonly Color BrokenEdge = new Color(1f, 0.55f, 0.3f, 1f);
    static readonly Color FixedBack = new Color(0.08f, 0.42f, 0.2f, 0.95f);
    static readonly Color FixedEdge = new Color(0.55f, 1f, 0.6f, 1f);
    static readonly Color BarBack = new Color(0f, 0f, 0f, 0.45f);
    static readonly Color BarFill = new Color(1f, 0.85f, 0.2f, 1f);

    const float FixedShowSeconds = 12f; // скільки секунд видно «Полагоджено!» з фактом NASA
    const float OutdoorReach = 2.5f;    // наскільки далеко від краю обладнання надворі можна лагодити, метри

    UIDocument doc;
    VisualElement wrap, box, barBack, barFill;
    Label title, hint, fact;
    GameObject marker;
    float progress;        // скільки секунд F уже тримали
    float fixedUntil;      // до якого часу показуємо «Полагоджено!»
    int seenFixCount;
    string seenId;
    float nextTick;

    // ---------- старт: сам з'являється в кожній сцені з грою ----------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // Живе між сценами (як GameAudio): після «Почати розділ знову» сцена перезавантажується, а ми лишаємось.
        if (FindAnyObjectByType<IncidentView>() != null) return;
        var go = new GameObject("Incidents");
        DontDestroyOnLoad(go);
        go.AddComponent<IncidentView>();
    }

    void OnEnable() => DebugPanel.ExtraGUI += DebugButtons;
    void OnDisable() => DebugPanel.ExtraGUI -= DebugButtons;

    static IncidentSystem Incidents => GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null
        ? GameBootstrap.Instance.GetSystem<IncidentSystem>() : null;

    void Update()
    {
        var game = GameBootstrap.Instance;
        var sys = Incidents;
        if (game == null || game.State == null || sys == null) { ShowBanner(false); SetMarker(null); return; }

        // Полагодили щойно — зелений напис з фактом NASA
        if (sys.FixCount != seenFixCount)
        {
            seenFixCount = sys.FixCount;
            fixedUntil = Time.unscaledTime + FixedShowSeconds;
        }

        var active = sys.Active;
        if (active != null && active.id != seenId) // нова поломка
        {
            seenId = active.id;
            progress = 0f;
            fixedUntil = 0f;
            GameAudio.Play(Sfx.Alarm);
        }
        if (active == null) seenId = null;

        ModuleView view = active != null ? BaseView.Find(sys.ActiveSlot) : null;
        SetMarker(view);
        UpdateToilet(active, view);

        bool inRange = view != null && EngineerInRange(view);
        if (active != null && inRange && CanWork(game) && FixHeld())
        {
            progress += Time.deltaTime;
            if (Time.unscaledTime >= nextTick) { nextTick = Time.unscaledTime + 0.35f; GameAudio.Play(Sfx.Step, 0.6f); }
            if (progress >= sys.FixSeconds)
            {
                sys.Fix(game.State, game.Clock.Report);
                progress = 0f;
                GameAudio.Play(Sfx.Built);
            }
        }

        UpdateBanner(sys, active, inRange);
    }

    /// <summary>Гра йде: немає паузи, меню, звіту чи планшета.</summary>
    static bool CanWork(GameBootstrap game)
    {
        if (game.Clock.IsPaused || UiKit.Blocked) return false;
        return TabletController.Instance == null || !TabletController.Instance.IsOpen;
    }

    static bool FixHeld()
    {
        var k = Keyboard.current;
        return k != null && k.fKey.isPressed;
    }

    /// <summary>Кімната: інженер усередині (у кімнаті чи шлюзі). Обладнання надворі: інженер поруч.</summary>
    static bool EngineerInRange(ModuleView view)
    {
        var eng = EngineerController.Instance;
        if (eng == null) return false;
        Vector3 p = eng.transform.position;
        if (view.Contains(p)) return true;
        if (view.IsEnterableRoom) return false;
        Vector2 size = ModuleLook.Instance != null ? ModuleLook.Instance.moduleSize : new Vector2(6f, 6f);
        Vector3 local = view.transform.InverseTransformPoint(p);
        return Mathf.Abs(local.x) < size.x * 0.5f + OutdoorReach && Mathf.Abs(local.z) < size.y * 0.5f + OutdoorReach;
    }

    // ---------- знак «!» над модулем ----------

    void SetMarker(ModuleView view)
    {
        if (view == null)
        {
            if (marker != null) marker.SetActive(false);
            return;
        }
        if (marker == null) marker = MakeMarker();
        marker.SetActive(true);

        float t = Time.unscaledTime;
        float bob = Mathf.Abs(Mathf.Sin(t * 3f)) * 0.6f;
        marker.transform.position = view.transform.position + Vector3.up * (4.6f + bob);
        var cam = Camera.main;
        if (cam != null) marker.transform.rotation = cam.transform.rotation; // завжди обличчям до камери
        float pulse = 1f + 0.12f * Mathf.Sin(t * 6f) + (progress > 0f ? 0.25f : 0f);
        marker.transform.localScale = Vector3.one * pulse;
    }

    static GameObject MakeMarker()
    {
        var go = new GameObject("Incident Marker");
        var sr = go.AddComponent<SpriteRenderer>();
        var tex = MarkerTexture();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 16f / 1.6f);
        sr.sortingOrder = 500;
        return go;
    }

    /// <summary>Червоне коло з білим «!» (16×16, малюємо кодом — арт може замінити).</summary>
    static Texture2D MarkerTexture()
    {
        string[] rows =
        {
            ".....######.....",
            "...##rrrrrr##...",
            "..#rrrrwwrrrr#..",
            ".#rrrrrwwrrrrr#.",
            ".#rrrrrwwrrrrr#.",
            "#rrrrrrwwrrrrrr#",
            "#rrrrrrwwrrrrrr#",
            "#rrrrrrwwrrrrrr#",
            "#rrrrrrwwrrrrrr#",
            "#rrrrrrrrrrrrrr#",
            ".#rrrrrwwrrrrr#.",
            ".#rrrrrwwrrrrr#.",
            "..#rrrrrrrrrr#..",
            "...##rrrrrr##...",
            ".....######.....",
            "................",
        };
        var tex = WorldSprites.NewTexture(16, 16);
        var px = new Color[16 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                char c = rows[15 - y][x];
                px[y * 16 + x] = c == '#' ? new Color(0.3f, 0.02f, 0.02f, 1f)
                    : c == 'r' ? new Color(0.95f, 0.15f, 0.1f, 1f)
                    : c == 'w' ? Color.white : Color.clear;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------- напис угорі екрана ----------

    void UpdateBanner(IncidentSystem sys, IncidentData active, bool inRange)
    {
        bool showFixed = active == null && sys.LastFixed != null && Time.unscaledTime < fixedUntil;
        bool covered = UiKit.Blocked || (TabletController.Instance != null && TabletController.Instance.IsOpen);
        if ((active == null && !showFixed) || covered) { ShowBanner(false); return; }
        if (!ShowBanner(true)) return;

        if (active != null)
        {
            Paint(BrokenBack, BrokenEdge);
            title.text = Loc.Get("incident." + active.id + ".title");
            hint.text = inRange ? Loc.Get("incident.hold_f") : Loc.Get("incident." + active.id + ".hint");
            fact.style.display = DisplayStyle.None;
            float k = Mathf.Clamp01(progress / sys.FixSeconds);
            barBack.style.display = progress > 0f || inRange ? DisplayStyle.Flex : DisplayStyle.None;
            barFill.style.width = Length.Percent(k * 100f);
            float t = Time.unscaledTime;
            box.style.opacity = 0.85f + 0.15f * Mathf.Abs(Mathf.Sin(t * 2.5f));
        }
        else
        {
            var done = sys.LastFixed;
            Paint(FixedBack, FixedEdge);
            title.text = Loc.Get("incident.fixed", UiKit.Num(done.science));
            hint.text = Loc.Get("incident." + done.id + ".fact");
            fact.text = Loc.Get("incident.fact_source");
            fact.style.display = DisplayStyle.Flex;
            barBack.style.display = DisplayStyle.None;
            box.style.opacity = 1f;
        }
    }

    void Paint(Color back, Color edge)
    {
        box.style.backgroundColor = back;
        box.style.borderTopColor = box.style.borderBottomColor = edge;
        box.style.borderLeftColor = box.style.borderRightColor = edge;
    }

    /// <summary>Показати/сховати напис. Повертає false, якщо напису немає (немає PanelSettings).</summary>
    bool ShowBanner(bool on)
    {
        if (on && box == null) Build();
        if (wrap == null) return false;
        wrap.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        return true;
    }

    void Build()
    {
        if (doc == null)
        {
            var look = ModuleLook.Instance;
            if (look == null || look.uiPanel == null) return; // немає панелі — напису не буде (як у StormBanner)
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = look.uiPanel;
            doc.sortingOrder = 49;
        }
        var root = doc.rootVisualElement;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        wrap = new VisualElement { name = "incident-banner", pickingMode = PickingMode.Ignore };
        wrap.style.position = Position.Absolute;
        wrap.style.left = 0;
        wrap.style.right = 0;
        wrap.style.top = 150; // під написом бурі (StormBanner стоїть на 64)
        wrap.style.alignItems = Align.Center;

        box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.alignItems = Align.Center;
        box.style.paddingLeft = box.style.paddingRight = 22;
        box.style.paddingTop = box.style.paddingBottom = 10;
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 14;
        box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 14;
        box.style.borderTopWidth = box.style.borderBottomWidth = 3;
        box.style.borderLeftWidth = box.style.borderRightWidth = 3;
        box.style.maxWidth = 760;

        title = NewLabel(24, true);
        hint = NewLabel(19, false);
        fact = NewLabel(14, false);
        fact.style.opacity = 0.8f;

        barBack = new VisualElement { pickingMode = PickingMode.Ignore };
        barBack.style.width = 360;
        barBack.style.height = 14;
        barBack.style.marginTop = 8;
        barBack.style.backgroundColor = BarBack;
        barBack.style.borderTopLeftRadius = barBack.style.borderTopRightRadius = 7;
        barBack.style.borderBottomLeftRadius = barBack.style.borderBottomRightRadius = 7;
        barFill = new VisualElement { pickingMode = PickingMode.Ignore };
        barFill.style.height = 14;
        barFill.style.width = 0;
        barFill.style.backgroundColor = BarFill;
        barFill.style.borderTopLeftRadius = barFill.style.borderBottomLeftRadius = 7;
        barFill.style.borderTopRightRadius = barFill.style.borderBottomRightRadius = 7;
        barBack.Add(barFill);

        box.Add(title);
        box.Add(hint);
        box.Add(barBack);
        box.Add(fact);
        wrap.Add(box);
        root.Add(wrap);
    }

    static Label NewLabel(int size, bool bold)
    {
        var l = new Label { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size;
        if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
        l.style.color = Color.white;
        l.style.whiteSpace = WhiteSpace.Normal;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.style.marginTop = 2;
        return l;
    }

    // ---------- кнопки перевірки в DebugPanel ----------

    void DebugButtons()
    {
        var game = GameBootstrap.Instance;
        var sys = Incidents;
        if (game == null || game.State == null || sys == null) return;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Поломка: випадкова")) Log(sys.TryStart(game.State, game.Clock.Report, null));
        if (GUILayout.Button("Унітаз")) Log(sys.TryStart(game.State, game.Clock.Report, "toilet"));
        if (GUILayout.Button("Пил")) Log(sys.TryStart(game.State, game.Clock.Report, "panel_dust"));
        if (GUILayout.Button("Метеорит")) Log(sys.TryStart(game.State, game.Clock.Report, "meteor"));
        if (GUILayout.Button("Полагодити")) sys.Fix(game.State, game.Clock.Report);
        GUILayout.EndHorizontal();
    }

    static void Log(string err)
    {
        if (err != null) Debug.Log("Поломка не почалась: " + Loc.Get(err));
    }

    // ---------- зламаний унітаз: анімація Юлі замість звичайного унітаза в житловому модулі ----------

    static Sprite[] toiletFrames;
    readonly List<SpriteRenderer> toiletRenderers = new List<SpriteRenderer>();
    Sprite toiletOriginal;

    void UpdateToilet(IncidentData active, ModuleView view)
    {
        bool want = active != null && active.id == "toilet" && view != null;
        if (!want) { RestoreToilet(); return; }
        if (toiletRenderers.Count == 0 || toiletRenderers[0] == null)
        {
            toiletRenderers.Clear();
            var f = view.FindFurniture("shower_toilet", 0);
            var frames = ToiletFrames();
            if (f == null || frames == null) return;
            var main = f.GetComponent<SpriteRenderer>();
            if (main == null) return;
            toiletOriginal = main.sprite;
            // і сам предмет, і його «товщина» (копія спрайта позаду) — щоб анімація була цілою
            foreach (var r in f.GetComponentsInChildren<SpriteRenderer>(true))
                if (r.sprite == toiletOriginal) toiletRenderers.Add(r);
        }
        int frame = (int)(Time.time * 10f) % toiletFrames.Length; // 10 кадрів на секунду: вода б'є фонтаном
        foreach (var r in toiletRenderers)
            if (r != null) r.sprite = toiletFrames[frame];
    }

    void RestoreToilet()
    {
        foreach (var r in toiletRenderers)
            if (r != null && toiletOriginal != null) r.sprite = toiletOriginal;
        toiletRenderers.Clear();
    }

    /// <summary>Кадри Юлі (Resources/Art/Incidents/toilet_frame_01..09). Немає — лишається звичайний унітаз.</summary>
    static Sprite[] ToiletFrames()
    {
        if (toiletFrames == null)
        {
            var list = new List<Sprite>();
            for (int i = 1; i <= 9; i++)
            {
                var sp = Resources.Load<Sprite>("Art/Incidents/toilet_frame_" + i.ToString("00"));
                if (sp != null) list.Add(sp);
            }
            toiletFrames = list.ToArray();
        }
        return toiletFrames.Length > 0 ? toiletFrames : null;
    }

    void OnDestroy()
    {
        RestoreToilet();
        if (marker != null) Destroy(marker);
    }
}
