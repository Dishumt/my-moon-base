// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// 3D-світ для «лід → вода і кисень» (IcePlantSystem):
///  - на кратері з льодом — БУР (вежа, двигун, шнек крутиться, коли копає; бункер з льодом наповнюється);
///  - біля бази, з боку кратера, — ПЕРЕРОБНИК (віконце нагрівача світиться, з труби йде пара);
///  - сіра труба від бура до переробника, синя (вода) і біло-блакитна (кисень) — до модулів, які вибрав гравець;
///    по трубах біжать кульки, коли щось тече.
/// Як грати (усе клавішею F, підказка внизу екрана):
///  1) підійди до напівпрозорого бура на кратері → F → будується (з тобою поруч — удвічі швидше);
///  2) так само переробник біля бази;
///  3) біля переробника F → береш кінець труби, ідеш у потрібний модуль → F → труба прокладається
///     (кожні 10 м труби = 1 будматеріал). Біля переробника F ще раз — покласти трубу назад.
/// Нічого не треба ставити в сцену: створюється сам при старті гри. Будівлі живуть у сцені й перебудовуються після її перезавантаження.
/// </summary>
public class IcePlantView : MonoBehaviour
{
    const float Reach = 5f;          // наскільки близько треба підійти до бура / переробника, метри
    const float DoorReach = 2.5f;    // або до дверей модуля, щоб підключити трубу
    const float DrillOffset = 3.5f;  // бур стоїть трохи ближче до бази, ніж центр кратера (там працює геолог)

    static readonly Color Ghost = new Color(0.4f, 0.85f, 1f, 0.22f);
    static readonly Color ScaffoldColor = new Color(1f, 0.6f, 0.2f, 0.5f);
    static readonly Color Metal = new Color(0.62f, 0.63f, 0.66f);
    static readonly Color DarkMetal = new Color(0.32f, 0.33f, 0.36f);
    static readonly Color Orange = new Color(1f, 0.55f, 0.15f);
    static readonly Color White = new Color(0.86f, 0.87f, 0.9f);
    static readonly Color IceColor = new Color(0.85f, 0.95f, 1f);
    static readonly Color WaterColor = new Color(0.15f, 0.45f, 1f);
    static readonly Color OxygenColor = new Color(0.7f, 0.95f, 1f);
    static readonly Color IcePipeColor = new Color(0.55f, 0.56f, 0.6f);
    static readonly Color LampGreen = new Color(0.4f, 1f, 0.4f);
    static readonly Color LampRed = new Color(1f, 0.2f, 0.15f);
    static readonly Color LampYellow = new Color(1f, 0.85f, 0.2f);
    static readonly Color HeaterOff = new Color(0.25f, 0.12f, 0.08f);
    static readonly Color HeaterOn = new Color(1f, 0.5f, 0.1f);

    enum Shown { None, Ghost, Building, Built }

    // ---------- що зараз намальовано ----------
    GameState seenState;
    Vector3 drillPos, processorPos;
    bool havePositions;
    string drillPoi;

    GameObject drillRoot, processorRoot;
    Shown drillShown, processorShown;
    Transform drillScaffold, processorScaffold, drillSpinner, hopperFill, drillLamp, processorLamp, heater, puff;
    float drillTotalHours, processorTotalHours;

    WorldPipe icePipe, waterPipe, oxygenPipe;
    int waterPipeSlot = -2, oxygenPipeSlot = -2;
    bool icePipeShown;

    // ---------- труба в руках ----------
    bool carrying;
    PipeKind carryKind;
    PipeKind lastTaken = PipeKind.Oxygen;
    LineRenderer carryLine;
    float carryMeters, nextMeasure;

    // ---------- підказка внизу ----------
    UIDocument doc;
    VisualElement promptWrap;
    Label promptLabel;
    string flash;
    float flashUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<IcePlantView>() != null) return;
        var go = new GameObject("Ice Plant");
        DontDestroyOnLoad(go);
        go.AddComponent<IcePlantView>();
    }

    void OnEnable() => DebugPanel.ExtraGUI += DebugButtons;
    void OnDisable() => DebugPanel.ExtraGUI -= DebugButtons;

    /// <summary>Кнопка в DebugPanel: одразу збудувати бур і переробник (щоб не ходити й не чекати). Тільки для перевірки!</summary>
    void DebugButtons()
    {
        var game = GameBootstrap.Instance;
        var sys = Sys;
        if (game == null || game.State == null || sys == null) return;
        var s = game.State;
        var p = s.IcePlant;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Лід: бур + переробник одразу"))
        {
            s.Res.Materials += sys.B.drillMaterials + sys.B.processorMaterials;
            if (!p.DrillOrdered && drillPoi != null) sys.OrderDrill(s, drillPoi);
            if (!p.ProcessorOrdered) sys.OrderProcessor(s);
            p.DrillBuilt = p.DrillOrdered;
            p.ProcessorBuilt = p.ProcessorOrdered;
            p.DrillHoursLeft = p.ProcessorHoursLeft = 0f;
        }
        GUILayout.Label("лід у коробі: " + UiKit.Num(p.Ice) + ", вода→" + p.WaterSlot + ", кисень→" + p.OxygenSlot);
        GUILayout.EndHorizontal();
    }

    static IcePlantSystem Sys => GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null
        ? GameBootstrap.Instance.GetSystem<IcePlantSystem>() : null;

    void Update()
    {
        var game = GameBootstrap.Instance;
        var sys = Sys;
        if (game == null || game.State == null || sys == null || ModuleLook.Instance == null) { SetPrompt(null); return; }
        var s = game.State;

        // Нова гра або перезавантажена сцена — малюємо все заново
        if (s != seenState || (drillRoot == null && drillShown != Shown.None) || (processorRoot == null && processorShown != Shown.None))
            ResetAll(s);

        if (!havePositions && !FindPositions(s)) { SetPrompt(null); return; }

        var p = s.IcePlant;
        UpdateDrill(p, sys);
        UpdateProcessor(s, p, sys);
        UpdatePipes(s, p, sys);
        Interact(game, s, p, sys);
    }

    void ResetAll(GameState s)
    {
        if (drillRoot != null) Destroy(drillRoot);
        if (processorRoot != null) Destroy(processorRoot);
        icePipe?.Destroy(); waterPipe?.Destroy(); oxygenPipe?.Destroy();
        icePipe = waterPipe = oxygenPipe = null;
        waterPipeSlot = oxygenPipeSlot = -2;
        icePipeShown = false;
        drillRoot = processorRoot = null;
        drillShown = processorShown = Shown.None;
        havePositions = false;
        carrying = false;
        if (carryLine != null) Destroy(carryLine.gameObject);
        carryLine = null;
        seenState = s;
    }

    // ---------- де стоять бур і переробник ----------

    static Vector3 BaseCenter()
    {
        Vector3 c = (BaseGrid.ToWorld(0) + BaseGrid.ToWorld(BaseGrid.Count - 1)) * 0.5f;
        c.y = 0f;
        return c;
    }

    bool FindPositions(GameState s)
    {
        // Бур: на вже вибраному кратері або на першому кратері з льодом цього розділу
        PoiMarker marker = null;
        if (s.IcePlant.DrillOrdered) marker = PoiMarker.Find(s.IcePlant.DrillPoiId);
        if (marker == null)
            foreach (var poi in s.Pois)
            {
                if (poi.Type != PoiType.Ice) continue;
                marker = PoiMarker.Find(poi.Id);
                if (marker != null) break;
            }
        if (marker == null) return false;
        drillPoi = marker.poiId;

        Vector3 c = BaseCenter();
        Vector3 m = marker.transform.position;
        Vector3 toBase = c - m; toBase.y = 0f;
        if (toBase.sqrMagnitude < 1f) return false;
        drillPos = m + toBase.normalized * DrillOffset;
        drillPos.y = MoonTerrain.HeightAt(drillPos);

        // Переробник: за краєм бази, з боку кратера
        Vector3 dir = -toBase.normalized;
        float half = Mathf.Max(BaseGrid.Width, BaseGrid.Depth) * BaseGrid.CellSize * 0.5f;
        float t = (half + 5f) / Mathf.Max(0.3f, Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.z)));
        processorPos = c + dir * t;
        processorPos.y = MoonTerrain.HeightAt(processorPos);
        havePositions = true;
        return true;
    }

    // ---------- бур ----------

    void UpdateDrill(IcePlantState p, IcePlantSystem sys)
    {
        Shown want = p.DrillBuilt ? Shown.Built : p.DrillOrdered ? Shown.Building : Shown.Ghost;
        if (want != drillShown || drillRoot == null)
        {
            if (drillRoot != null) Destroy(drillRoot);
            drillRoot = new GameObject("Ice Drill");
            drillRoot.transform.position = drillPos;
            drillShown = want;
            drillScaffold = null;
            if (want == Shown.Ghost) Blueprint(drillRoot.transform, new Vector3(3f, 5f, 3f));
            else if (want == Shown.Building) { drillScaffold = Scaffold(drillRoot.transform); drillTotalHours = Mathf.Max(1f, p.DrillHoursLeft); }
            else { BuildDrill(drillRoot.transform); if (Time.timeSinceLevelLoad > 2f) GameAudio.Play(Sfx.Built); }
        }

        float time = Time.time;
        if (want == Shown.Ghost) Breathe(drillRoot.transform, time);
        if (want == Shown.Building) GrowScaffold(drillScaffold, new Vector2(3f, 3f), 5f, p.DrillHoursLeft, ref drillTotalHours);
        if (want != Shown.Built) return;

        bool working = sys.DrillWorking && !(GameBootstrap.Instance.Clock.IsPaused);
        if (drillSpinner != null)
        {
            if (working) drillSpinner.Rotate(0f, 360f * Time.deltaTime, 0f, Space.Self);
            Vector3 lp = drillSpinner.localPosition;
            lp.y = working ? -0.25f + 0.25f * Mathf.Sin(time * 5f) : 0f; // стукає вгору-вниз, коли копає
            drillSpinner.localPosition = lp;
        }
        if (hopperFill != null)
        {
            float k = Mathf.Clamp01(p.Ice / Mathf.Max(0.1f, sys.B.iceStorageMax));
            hopperFill.localScale = new Vector3(0.8f, Mathf.Max(0.02f, 0.85f * k), 0.8f);
            hopperFill.localPosition = new Vector3(1.9f, 0.15f + 0.85f * k * 0.5f, 0f);
        }
        if (drillLamp != null)
        {
            bool full = p.Ice >= sys.B.iceStorageMax - 0.01f;
            bool blink = Mathf.Repeat(time, 1f) < 0.5f;
            Color c = working ? LampGreen : full ? LampYellow : (blink ? LampRed : DarkMetal);
            ModuleParts.SetColor(drillLamp.GetComponent<Renderer>(), c);
        }
    }

    void BuildDrill(Transform root)
    {
        var mat = ModuleLook.Instance.wallMaterial;
        ModuleParts.Box(root, "Pad", mat, new Vector3(0f, 0.06f, 0f), new Vector3(3.2f, 0.12f, 3.2f), collider: false, tint: DarkMetal);
        for (int i = -1; i <= 1; i += 2)
            for (int j = -1; j <= 1; j += 2)
                ModuleParts.Box(root, "Leg", mat, new Vector3(i * 1.2f, 2.2f, j * 1.2f), new Vector3(0.18f, 4.4f, 0.18f), collider: false, tint: Metal);
        // Перекладини-хрестики, щоб вежа виглядала як справжня рама
        for (int i = -1; i <= 1; i += 2)
        {
            var brace = ModuleParts.Box(root, "Brace", mat, new Vector3(i * 1.2f, 2f, 0f), new Vector3(0.1f, 3.2f, 0.1f), collider: false, tint: Metal);
            brace.transform.localRotation = Quaternion.Euler(37f, 0f, 0f);
        }
        ModuleParts.Box(root, "Top", mat, new Vector3(0f, 4.45f, 0f), new Vector3(2.8f, 0.25f, 2.8f), collider: false, tint: Metal);
        ModuleParts.Box(root, "Motor", mat, new Vector3(0f, 5.05f, 0f), new Vector3(1.4f, 1f, 1.4f), collider: false, tint: Orange);

        // Шнек: окремий порожній об'єкт, який крутиться; на ньому вал і витки
        drillSpinner = new GameObject("Spinner").transform;
        drillSpinner.SetParent(root, false);
        ModuleParts.Cylinder(drillSpinner, "Shaft", mat, new Vector3(0f, 2.3f, 0f), new Vector3(0.3f, 2.3f, 0.3f), collider: false, tint: Metal);
        for (int k = 0; k < 7; k++)
        {
            var flight = ModuleParts.Box(drillSpinner, "Flight", mat, new Vector3(0f, 0.4f + k * 0.55f, 0f), new Vector3(0.95f, 0.07f, 0.22f),
                                         collider: false, tint: DarkMetal);
            flight.transform.localRotation = Quaternion.Euler(0f, k * 52f, 12f);
        }
        ModuleParts.Cylinder(root, "Hole", mat, new Vector3(0f, 0.13f, 0f), new Vector3(1.1f, 0.02f, 1.1f), collider: false, tint: new Color(0.1f, 0.1f, 0.12f));

        // Бункер з льодом (прозорий бокс, усередині росте біла «гора» льоду)
        ModuleParts.Box(root, "Hopper", ModuleLook.Instance.roofMaterial, new Vector3(1.9f, 0.6f, 0f), new Vector3(1f, 1f, 1f),
                        collider: false, tint: new Color(0.7f, 0.85f, 1f, 0.35f));
        hopperFill = ModuleParts.Box(root, "Ice", mat, new Vector3(1.9f, 0.16f, 0f), new Vector3(0.8f, 0.02f, 0.8f), collider: false, tint: IceColor).transform;

        drillLamp = ModuleParts.Sphere(root, "Lamp", mat, new Vector3(0f, 5.75f, 0f), Vector3.one * 0.35f, collider: false, tint: LampGreen).transform;
    }

    // ---------- переробник ----------

    void UpdateProcessor(GameState s, IcePlantState p, IcePlantSystem sys)
    {
        Shown want = p.ProcessorBuilt ? Shown.Built : p.ProcessorOrdered ? Shown.Building
                   : s.Supply.FirstMissionDone ? Shown.Ghost : Shown.None;
        if (want != processorShown || (processorRoot == null && want != Shown.None))
        {
            if (processorRoot != null) Destroy(processorRoot);
            processorRoot = null;
            processorShown = want;
            processorScaffold = null;
            if (want != Shown.None)
            {
                processorRoot = new GameObject("Ice Processor");
                processorRoot.transform.position = processorPos;
                if (want == Shown.Ghost) Blueprint(processorRoot.transform, new Vector3(4f, 3f, 3f));
                else if (want == Shown.Building) { processorScaffold = Scaffold(processorRoot.transform); processorTotalHours = Mathf.Max(1f, p.ProcessorHoursLeft); }
                else { BuildProcessor(processorRoot.transform); if (Time.timeSinceLevelLoad > 2f) GameAudio.Play(Sfx.Built); }
            }
        }
        if (processorRoot == null) return;

        float time = Time.time;
        if (want == Shown.Ghost) Breathe(processorRoot.transform, time);
        if (want == Shown.Building) GrowScaffold(processorScaffold, new Vector2(4f, 3f), 3f, p.ProcessorHoursLeft, ref processorTotalHours);
        if (want != Shown.Built) return;

        bool working = sys.ProcessorWorking;
        if (heater != null)
            ModuleParts.SetColor(heater.GetComponent<Renderer>(), working ? Color.Lerp(HeaterOff, HeaterOn, 0.6f + 0.4f * Mathf.Sin(time * 4f)) : HeaterOff);
        if (puff != null)
        {
            puff.gameObject.SetActive(working);
            float k = Mathf.Repeat(time * 0.6f, 1f); // пара піднімається і тане
            puff.localPosition = new Vector3(-1f, 3.1f + k * 1.6f, 0.6f);
            puff.localScale = Vector3.one * (0.35f + k * 0.8f);
            ModuleParts.SetColor(puff.GetComponent<Renderer>(), new Color(1f, 1f, 1f, 0.55f * (1f - k)));
        }
        if (processorLamp != null)
        {
            bool noPipes = p.WaterSlot < 0 && p.OxygenSlot < 0;
            bool blink = Mathf.Repeat(time, 1f) < 0.5f;
            Color c = working ? LampGreen : noPipes ? (blink ? LampYellow : DarkMetal) : DarkMetal;
            ModuleParts.SetColor(processorLamp.GetComponent<Renderer>(), c);
        }
    }

    void BuildProcessor(Transform root)
    {
        var mat = ModuleLook.Instance.wallMaterial;
        ModuleParts.Box(root, "Pad", mat, new Vector3(0f, 0.06f, 0f), new Vector3(4.4f, 0.12f, 3.4f), collider: false, tint: DarkMetal);
        ModuleParts.Box(root, "Body", mat, new Vector3(-0.3f, 1.1f, 0f), new Vector3(2.6f, 2f, 2f), tint: White);
        ModuleParts.Box(root, "Hopper", mat, new Vector3(-0.6f, 2.45f, -0.2f), new Vector3(1.2f, 0.7f, 1.2f), collider: false, tint: IceColor);
        heater = ModuleParts.Box(root, "Heater", mat, new Vector3(-0.3f, 1.2f, -1.03f), new Vector3(1.2f, 0.7f, 0.06f), collider: false, tint: HeaterOff).transform;
        ModuleParts.Cylinder(root, "Chimney", mat, new Vector3(-1f, 2.6f, 0.6f), new Vector3(0.3f, 0.5f, 0.3f), collider: false, tint: Metal);
        puff = ModuleParts.Sphere(root, "Steam", ModuleLook.Instance.roofMaterial, new Vector3(-1f, 3.1f, 0.6f), Vector3.one * 0.4f,
                                  collider: false, tint: new Color(1f, 1f, 1f, 0.5f)).transform;

        // Два баки: синій — вода, біло-блакитний — кисень (труби виходять з них)
        ModuleParts.Cylinder(root, "Water Tank", mat, new Vector3(1.6f, 0.95f, -0.75f), new Vector3(0.8f, 0.9f, 0.8f), collider: false, tint: WaterColor);
        ModuleParts.Cylinder(root, "Oxygen Tank", mat, new Vector3(1.6f, 0.95f, 0.75f), new Vector3(0.8f, 0.9f, 0.8f), collider: false, tint: OxygenColor);
        processorLamp = ModuleParts.Sphere(root, "Lamp", mat, new Vector3(-0.3f, 2.35f, 0.7f), Vector3.one * 0.3f, collider: false, tint: DarkMetal).transform;
    }

    /// <summary>Чи вже відомо, де стоять бур і переробник (PowerCablesView веде до них кабель).</summary>
    public bool HasPositions => havePositions;
    /// <summary>Куди підходить кабель енергії до переробника і до бура.</summary>
    public Vector3 ProcessorCablePoint => processorPos + new Vector3(-0.3f, 0f, 1.6f);
    public Vector3 DrillCablePoint => drillPos + new Vector3(0f, 0f, 1.7f);

    Vector3 Outlet(PipeKind kind) => processorPos + new Vector3(2.2f, 0f, kind == PipeKind.Water ? -0.75f : 0.75f);
    Vector3 IceInlet => processorPos + new Vector3(-1.9f, 0f, 0f);
    Vector3 DrillOutlet => drillPos + new Vector3(1.9f, 0f, 0f);

    // ---------- будова: привид, риштування ----------

    static void Blueprint(Transform root, Vector3 size)
    {
        ModuleParts.Box(root, "Blueprint", ModuleLook.Instance.roofMaterial, new Vector3(0f, size.y * 0.5f, 0f), size, collider: false, tint: Ghost);
    }

    static void Breathe(Transform root, float time)
    {
        float s = 1f + 0.03f * Mathf.Sin(time * 2.5f);
        root.localScale = new Vector3(s, 1f, s);
    }

    static Transform Scaffold(Transform root)
    {
        return ModuleParts.Box(root, "Scaffold", ModuleLook.Instance.roofMaterial, Vector3.zero, Vector3.one, collider: false, tint: ScaffoldColor).transform;
    }

    static void GrowScaffold(Transform scaffold, Vector2 size, float height, float hoursLeft, ref float total)
    {
        if (scaffold == null) return;
        total = Mathf.Max(total, hoursLeft);
        float progress = total > 0f ? 1f - hoursLeft / total : 0f;
        float h = Mathf.Lerp(0.1f, height, progress);
        scaffold.localScale = new Vector3(size.x, h, size.y);
        scaffold.localPosition = new Vector3(0f, h * 0.5f, 0f);
    }

    // ---------- труби ----------

    void UpdatePipes(GameState s, IcePlantState p, IcePlantSystem sys)
    {
        bool showIce = p.DrillBuilt && p.ProcessorBuilt;
        if (showIce != icePipeShown || (showIce && (icePipe == null || icePipe.Root == null)))
        {
            icePipe?.Destroy();
            icePipe = showIce ? new WorldPipe("Ice Pipe", WorldPipe.Straight(DrillOutlet, IceInlet), 0.16f, IcePipeColor, IceColor) : null;
            icePipeShown = showIce;
        }
        icePipe?.Animate(sys.ProcessorWorking, 1.5f);

        if (p.WaterSlot != waterPipeSlot || (waterPipe != null && waterPipe.Root == null))
        {
            waterPipe?.Destroy();
            waterPipe = p.WaterSlot >= 0 ? MakeModulePipe(PipeKind.Water, p.WaterSlot) : null;
            waterPipeSlot = p.WaterSlot < 0 || waterPipe != null ? p.WaterSlot : -2; // модуль ще не намальований — спробуємо наступного кадру
        }
        if (p.OxygenSlot != oxygenPipeSlot || (oxygenPipe != null && oxygenPipe.Root == null))
        {
            oxygenPipe?.Destroy();
            oxygenPipe = p.OxygenSlot >= 0 ? MakeModulePipe(PipeKind.Oxygen, p.OxygenSlot) : null;
            oxygenPipeSlot = p.OxygenSlot < 0 || oxygenPipe != null ? p.OxygenSlot : -2;
        }
        waterPipe?.Animate(sys.ProcessorWorking, 2f);
        oxygenPipe?.Animate(sys.ProcessorWorking, 2f);
    }

    WorldPipe MakeModulePipe(PipeKind kind, int slot)
    {
        var view = BaseView.Find(slot);
        if (view == null || !view.IsEnterableRoom) return null;
        var pts = PipeRoute(kind, view);
        Color c = kind == PipeKind.Water ? WaterColor : OxygenColor;
        return new WorldPipe(kind == PipeKind.Water ? "Water Pipe" : "Oxygen Pipe", pts, 0.14f, c, Color.white);
    }

    /// <summary>Шлях труби від переробника до дверей модуля — проходами між модулями (як ходять астронавти).</summary>
    List<Vector3> PipeRoute(PipeKind kind, ModuleView view)
    {
        Vector3 from = Outlet(kind);
        Vector3 side = view.transform.right * (kind == PipeKind.Water ? -1.1f : 1.1f); // труби поруч із дверима, а не в них
        Vector3 to = view.OuterDoorPoint + side;
        var path = BaseView.PathTo(from, to);
        var pts = new List<Vector3> { from };
        pts.AddRange(path);
        if (pts.Count < 2 || (pts[pts.Count - 1] - to).sqrMagnitude > 0.01f) pts.Add(to);
        pts.Add(view.InnerDoorPoint + side * 0.9f); // заходить у стіну модуля
        return WorldPipe.Densify(pts);
    }

    // ---------- гравець: F біля бура, переробника, модуля ----------

    void Interact(GameBootstrap game, GameState s, IcePlantState p, IcePlantSystem sys)
    {
        var eng = EngineerController.Instance;
        bool canAct = !game.Clock.IsPaused && !UiKit.Blocked && (TabletController.Instance == null || !TabletController.Instance.IsOpen);
        if (eng == null) { p.EngineerNearDrill = p.EngineerNearProcessor = false; SetPrompt(null); return; }

        Vector3 pos = eng.transform.position;
        bool nearDrill = FlatDistance(pos, drillPos) < Reach;
        bool nearProcessor = processorShown != Shown.None && FlatDistance(pos, processorPos) < Reach;
        p.EngineerNearDrill = nearDrill && p.DrillOrdered && !p.DrillBuilt;
        p.EngineerNearProcessor = nearProcessor && p.ProcessorOrdered && !p.ProcessorBuilt;

        var k = Keyboard.current;
        bool pressed = canAct && k != null && k.fKey.wasPressedThisFrame;

        if (carrying) { Carry(s, p, sys, pos, nearProcessor, pressed); return; }
        if (carryLine != null) carryLine.gameObject.SetActive(false);

        string text = null;
        if (nearDrill)
        {
            if (!p.DrillOrdered)
            {
                text = Loc.Get("iceplant.prompt_build_drill", sys.B.drillMaterials);
                if (pressed) Result(sys.OrderDrill(s, drillPoi));
            }
            else if (!p.DrillBuilt) text = Loc.Get("iceplant.prompt_building_drill", Percent(p.DrillHoursLeft, drillTotalHours));
            else text = Loc.Get("iceplant.status_drill", UiKit.Num(p.Ice), UiKit.Num(sys.B.iceStorageMax)) + "\n" + Loc.Get("iceplant.fact_drill");
        }
        else if (nearProcessor)
        {
            if (!p.ProcessorOrdered)
            {
                text = Loc.Get("iceplant.prompt_build_processor", sys.B.processorMaterials);
                if (pressed) Result(sys.OrderProcessor(s));
            }
            else if (!p.ProcessorBuilt) text = Loc.Get("iceplant.prompt_building_processor", Percent(p.ProcessorHoursLeft, processorTotalHours));
            else
            {
                PipeKind next = NextPipe(p);
                text = Loc.Get("iceplant.status_processor", Target(s, p.WaterSlot), Target(s, p.OxygenSlot)) + "\n"
                     + Loc.Get(next == PipeKind.Water ? "iceplant.prompt_take_water" : "iceplant.prompt_take_oxygen");
                if (pressed)
                {
                    carrying = true;
                    carryKind = next;
                    lastTaken = next;
                    carryMeters = 0f;
                    nextMeasure = 0f;
                    GameAudio.Play(Sfx.Click);
                }
            }
        }
        SetPrompt(text);
    }

    /// <summary>Яку трубу взяти: першу непідключену, а якщо обидві підключені — по черзі (щоб перекласти).</summary>
    PipeKind NextPipe(IcePlantState p)
    {
        if (p.WaterSlot < 0) return PipeKind.Water;
        if (p.OxygenSlot < 0) return PipeKind.Oxygen;
        return lastTaken == PipeKind.Water ? PipeKind.Oxygen : PipeKind.Water;
    }

    void Carry(GameState s, IcePlantState p, IcePlantSystem sys, Vector3 pos, bool nearProcessor, bool pressed)
    {
        // Лінія від переробника до інженера
        if (carryLine == null) carryLine = MakeCarryLine();
        carryLine.gameObject.SetActive(true);
        ModuleParts.SetColor(carryLine, carryKind == PipeKind.Water ? WaterColor : OxygenColor);
        var line = WorldPipe.Straight(Outlet(carryKind), pos);
        carryLine.positionCount = line.Count;
        carryLine.SetPositions(line.ToArray());

        // Модуль, біля дверей якого (або в якому) стоїть інженер
        int slot = BaseView.SlotAt(pos);
        ModuleView view = slot >= 0 ? BaseView.Find(slot) : null;
        if (view == null || !view.IsEnterableRoom)
        {
            view = null;
            foreach (var v in BaseView.All)
                if (v != null && v.IsEnterableRoom && FlatDistance(pos, v.OuterDoorPoint) < DoorReach) { view = v; break; }
        }

        // Довжину труби по проходах рахуємо двічі на секунду (це пошук шляху, не щокадру)
        if (view != null && Time.unscaledTime >= nextMeasure)
        {
            nextMeasure = Time.unscaledTime + 0.5f;
            carryMeters = WorldPipe.PathLength(PipeRoute(carryKind, view));
        }
        if (view == null) carryMeters = WorldPipe.PathLength(line);

        string kindKey = carryKind == PipeKind.Water ? "iceplant.prompt_carry_water" : "iceplant.prompt_carry_oxygen";
        string text = Loc.Get(kindKey, Mathf.RoundToInt(carryMeters), sys.PipeCost(carryMeters));
        var m = view != null ? s.FindModule(view.SlotId) : null;
        if (nearProcessor && view == null)
        {
            text += "\n" + Loc.Get("iceplant.prompt_drop");
            if (pressed) { carrying = false; GameAudio.Play(Sfx.Click); }
        }
        else if (m != null)
        {
            bool fits = IcePlantSystem.CanFeed(carryKind, m.Type);
            text = fits ? Loc.Get("iceplant.prompt_connect", Loc.Get("mod." + m.Type), sys.PipeCost(carryMeters))
                        : Loc.Get(carryKind == PipeKind.Water ? "iceplant.err_water_target" : "iceplant.err_oxygen_target");
            if (pressed)
            {
                string err = sys.ConnectPipe(s, carryKind, view.SlotId, carryMeters);
                Result(err);
                if (err == null) carrying = false;
            }
        }
        SetPrompt(text);
    }

    LineRenderer MakeCarryLine()
    {
        var go = new GameObject("Carried Pipe");
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = ModuleLook.Instance.wallMaterial;
        lr.widthMultiplier = 0.28f;
        lr.numCornerVertices = 2;
        lr.generateLightingData = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    string Target(GameState s, int slot)
    {
        var m = slot >= 0 ? s.FindModule(slot) : null;
        return m != null ? Loc.Get("mod." + m.Type) : Loc.Get("iceplant.not_connected");
    }

    static int Percent(float hoursLeft, float total) => Mathf.RoundToInt(100f * Mathf.Clamp01(1f - hoursLeft / Mathf.Max(0.01f, total)));

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void Result(string err)
    {
        if (err == null) { GameAudio.Play(Sfx.Confirm); return; }
        GameAudio.Play(Sfx.Error);
        flash = Loc.Get(err);
        flashUntil = Time.unscaledTime + 2.5f;
    }

    // ---------- підказка внизу екрана ----------

    void SetPrompt(string text)
    {
        if (Time.unscaledTime < flashUntil && !string.IsNullOrEmpty(flash)) text = flash;
        bool covered = UiKit.Blocked || (TabletController.Instance != null && TabletController.Instance.IsOpen);
        if (string.IsNullOrEmpty(text) || covered)
        {
            if (promptWrap != null) promptWrap.style.display = DisplayStyle.None;
            return;
        }
        if (promptWrap == null && !BuildPrompt()) return;
        promptWrap.style.display = DisplayStyle.Flex;
        if (promptLabel.text != text) promptLabel.text = text;
    }

    bool BuildPrompt()
    {
        if (doc == null)
        {
            var look = ModuleLook.Instance;
            if (look == null || look.uiPanel == null) return false;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = look.uiPanel;
            doc.sortingOrder = 48;
        }
        var root = doc.rootVisualElement;
        if (root == null) return false;
        root.pickingMode = PickingMode.Ignore;

        promptWrap = new VisualElement { name = "iceplant-prompt", pickingMode = PickingMode.Ignore };
        promptWrap.style.position = Position.Absolute;
        promptWrap.style.left = 0;
        promptWrap.style.right = 0;
        promptWrap.style.bottom = 110;
        promptWrap.style.alignItems = Align.Center;

        var box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.backgroundColor = new Color(0.06f, 0.1f, 0.18f, 0.9f);
        box.style.borderTopColor = box.style.borderBottomColor = new Color(0.5f, 0.85f, 1f, 1f);
        box.style.borderLeftColor = box.style.borderRightColor = new Color(0.5f, 0.85f, 1f, 1f);
        box.style.borderTopWidth = box.style.borderBottomWidth = 2;
        box.style.borderLeftWidth = box.style.borderRightWidth = 2;
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 12;
        box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 12;
        box.style.paddingLeft = box.style.paddingRight = 18;
        box.style.paddingTop = box.style.paddingBottom = 8;
        box.style.maxWidth = 760;

        promptLabel = new Label { pickingMode = PickingMode.Ignore };
        promptLabel.style.fontSize = 19;
        promptLabel.style.color = Color.white;
        promptLabel.style.whiteSpace = WhiteSpace.Normal;
        promptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        box.Add(promptLabel);
        promptWrap.Add(box);
        root.Add(promptWrap);
        return true;
    }

    void OnDestroy()
    {
        if (drillRoot != null) Destroy(drillRoot);
        if (processorRoot != null) Destroy(processorRoot);
        icePipe?.Destroy(); waterPipe?.Destroy(); oxygenPipe?.Destroy();
        if (carryLine != null) Destroy(carryLine.gameObject);
    }
}
