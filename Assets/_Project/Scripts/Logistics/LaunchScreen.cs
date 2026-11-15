// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Екран запуску на UI Toolkit (розкладка UI/Launch/LaunchScreen.uxml).
/// 1) зліва вибір ракети (rockets.json), 2) посередині вантажі по групах (запаси, набори модулів) з − / + і «?»,
/// 3) справа ракета, смужка маси і поле «Навіщо це?» з підказкою для дітей.
/// Внизу паливо, світлофор, «Ризик: N%» і кнопка ЗАПУСК. Кожна зміна одразу перераховує світлофор.
/// Поки екран відкритий — гра на паузі (Clock.PushModal / PopModal).
/// Режими: перша місія (4 місця екіпажу, політ з анімацією) і поставка (замовлення йде у вікно зв'язку).
/// Числа — з Launch/*.json через SupplySystem, тексти — через LaunchText (Loc + запасні тексти ролі 04).
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LaunchScreen : MonoBehaviour
{
    /// <summary>Картинка для ракети чи вантажу за id (заповнюється в інспекторі).</summary>
    [Serializable]
    public class Look
    {
        public string id;
        public Texture2D texture;
    }

    [Tooltip("Відкрити екран одразу після старту сцени (для першої місії — так)")]
    [SerializeField] bool openOnStart = true;

    [Tooltip("Запасна картинка ракети, якщо для вибраної ракети немає своєї. Порожньо — ракета з прямокутників.")]
    [SerializeField] Texture2D rocketTexture;

    [Tooltip("Картинки ракет: id як у Launch/rockets.json (small, medium, heavy)")]
    [SerializeField] List<Look> rocketLooks = new List<Look>();

    [Tooltip("Іконки вантажів: id як у Launch/cargo_items.json (water, food, oxygen...)")]
    [SerializeField] List<Look> cargoIcons = new List<Look>();

    /// <summary>Скафандр астронавта для майданчика (якщо в сцені немає CrewLook від 01).</summary>
    [Serializable]
    public class CrewSuit
    {
        public CrewRole role;
        public Texture2D[] walkRight;   // 4 кадри ходьби вправо (Art/Characters/*_outdoor/walk_right_0N)
        public Texture2D back;          // спиною (back.png)
    }

    [Tooltip("Скафандри для пускового майданчика. Якщо в сцені є CrewLook (головна сцена), картинки беруться з нього — такі самі, як на Місяці.")]
    [SerializeField] List<CrewSuit> crewSuits = new List<CrewSuit>();

    [Tooltip("Фон пускового майданчика (Art/Earth/Pixel Art Rocket Launch Site at Sunset). Порожньо — небо й будівлі малюються кодом.")]
    [SerializeField] Texture2D padBackground;

    [Tooltip("Автобус для екіпажу (Art/Earth/Pixel Art Blue-Stripe Bus Sprite). Порожньо — автобус із прямокутників.")]
    [SerializeField] Texture2D padBus;

    [Tooltip("Звук на кожну секунду відліку. Порожньо — простий «біп», згенерований кодом.")]
    [SerializeField] AudioClip countdownBeep;

    [Tooltip("Звук «СТАРТ!». Порожньо — сигнал, згенерований кодом.")]
    [SerializeField] AudioClip liftoffSound;

    [Tooltip("Гул двигунів (грає по колу). Порожньо — низький шум, згенерований кодом.")]
    [SerializeField] AudioClip engineRumble;

    [Tooltip("Гучність звуків майданчика (0 — без звуку)")]
    [Range(0f, 1f)] [SerializeField] float padVolume = 0.8f;

    [Tooltip("Показувати пусковий майданчик (екіпаж, відлік, злет) після ПІДТВЕРДИТИ")]
    [SerializeField] bool showLaunchPad = true;

    [Tooltip("Заповнити порожню вкладку «ПОСТАВКИ» в планшеті (TabletController від 05): таймер, кроки, вміст замовлення і кнопка «Пакувати корабель».")]
    [SerializeField] bool attachToTablet = true;

    [Tooltip("Скільки секунд летить ракета")]
    [SerializeField] float flightSeconds = 3f;

    /// <summary>Екран запуску в сцені. Планшет і HUD відкривають замовлення так: LaunchScreen.Instance.OpenSupplyOrder().</summary>
    public static LaunchScreen Instance { get; private set; }

    public bool IsOpen { get; private set; }
    public bool IsFirstMission { get; private set; }

    VisualElement stage, cargoList, rocketList, ship, massBar, massCrew, massCargo, lightRed, lightYellow, lightGreen, linkLed;
    Label titleLabel, statusLabel, rocketTitle, rocketWhy, cargoTitle, shipTitle, legendCrew, legendCargo;
    Label seatsLabel, massLabel, fuelLabel, fuelWhy, riskLabel, hintLabel, resultLabel;
    Label infoTitle, infoText, infoGives;
    Label goalsTitle, daysTitle, advisorName, advisorText;
    VisualElement goalsList, daysList, advisor, hold;
    Label massLike, holdTitle;
    VisualElement report, reportStars, reportList;
    Label reportTitle, reportTip;
    Button reportNext;
    bool reportClosed;
    int failedTries;          // скільки разів у цій місії ракета поверталась (для зірочки)
    SupplyTabView supplyTab;           // вміст вкладки «ПОСТАВКИ» в планшеті
    TabletController checkedTablet;    // планшет, який уже перевірили (щоб не пробувати щокадру)
    SimBalanceData balance;   // правила витрат ролі 03 — для «на скільки днів вистачить»
    TasksData tasks;          // швидкості завдань (лід) — щоб порадник рахував добування води

    const float ShipBaseScale = 0.92f; // ракета на планшеті менша, ніж на майданчику
    Slider fuelSlider;
    Button launchButton, closeButton, weatherChip;
    VisualElement tablet;
    LaunchPadView pad;
    LaunchSounds sounds;      // біп відліку, «СТАРТ!», гул двигунів
    FlightMapView flight;     // політ Дніпро → південний полюс Місяця
    LandingView landing;      // посадка на південному полюсі (вид збоку)
    bool animating, modalPushed, ready;

    // що зараз показано в полі «Навіщо це?»: вантаж або ракета (null — загальна підказка)
    CargoItemData infoItem;
    RocketData infoRocket;
    bool infoMass;            // показати «чому корабель такий легкий»
    SupplyPlanData infoPlan;  // показати пояснення плану поставок
    Label planTitle;
    VisualElement planList;

    GameState State => GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
    SupplySystem Supply => GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<SupplySystem>() : null;
    CommsSystem Comms => GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<CommsSystem>() : null;

    static string T(string key) => LaunchText.Get(key);
    static string T(string key, params object[] args) => LaunchText.Get(key, args);

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        stage = root.Q("launch-stage");
        cargoList = root.Q("cargo-list");
        rocketList = root.Q("rocket-list");
        ship = root.Q("ship");
        massBar = root.Q(className: "mass-bar");
        massCrew = root.Q("mass-crew");
        massCargo = root.Q("mass-cargo");
        lightRed = root.Q("light-red");
        lightYellow = root.Q("light-yellow");
        lightGreen = root.Q("light-green");
        linkLed = root.Q("link-led");
        titleLabel = root.Q<Label>("title-label");
        statusLabel = root.Q<Label>("status-label");
        rocketTitle = root.Q<Label>("rocket-title");
        rocketWhy = root.Q<Label>("rocket-why");
        cargoTitle = root.Q<Label>("cargo-title");
        shipTitle = root.Q<Label>("ship-title");
        legendCrew = root.Q<Label>("legend-crew");
        legendCargo = root.Q<Label>("legend-cargo");
        seatsLabel = root.Q<Label>("seats-label");
        massLabel = root.Q<Label>("mass-label");
        fuelLabel = root.Q<Label>("fuel-label");
        fuelWhy = root.Q<Label>("fuel-why");
        riskLabel = root.Q<Label>("risk-label");
        hintLabel = root.Q<Label>("hint-label");
        resultLabel = root.Q<Label>("result-label");
        infoTitle = root.Q<Label>("info-title");
        infoText = root.Q<Label>("info-text");
        infoGives = root.Q<Label>("info-gives");
        fuelSlider = root.Q<Slider>("fuel-slider");
        launchButton = root.Q<Button>("launch-button");
        weatherChip = root.Q<Button>("weather-chip");
        closeButton = root.Q<Button>("close-button");
        tablet = root.Q("launch");
        goalsTitle = root.Q<Label>("goals-title");
        goalsList = root.Q("goals-list");
        daysTitle = root.Q<Label>("days-title");
        daysList = root.Q("days-list");
        advisor = root.Q("advisor");
        advisorName = root.Q<Label>("advisor-name");
        advisorText = root.Q<Label>("advisor-text");
        hold = root.Q("hold");
        holdTitle = root.Q<Label>("hold-title");
        massLike = root.Q<Label>("mass-like");
        planTitle = root.Q<Label>("plan-title");
        planList = root.Q("plan-list");
        // навели на масу корабля — пояснюємо, чому він важить лише тонни, а не тисячі тонн
        root.Q("ship-panel")?.RegisterCallback<PointerEnterEvent>(_ => { infoItem = null; infoRocket = null; infoPlan = null; infoMass = true; RefreshInfo(); });
        report = root.Q("report");
        reportStars = root.Q("report-stars");
        reportList = root.Q("report-list");
        reportTitle = root.Q<Label>("report-title");
        reportTip = root.Q<Label>("report-tip");
        reportNext = root.Q<Button>("report-next");
        if (reportNext != null) reportNext.clicked += () => reportClosed = true;
        if (balance == null) balance = DataLoader.Load<SimBalanceData>("Balance/balance");
        if (tasks == null) tasks = TasksData.Load();
        pad = new LaunchPadView(root);
        if (!pad.Ready) pad = null; // у UXML немає майданчика — буде тільки політ по дузі
        else
        {
            pad.SetArt(padBackground, padBus);
            // звуки створюємо один раз (OnEnable може викликатися кілька разів)
            if (padVolume > 0f && sounds == null)
                sounds = new LaunchSounds(gameObject, countdownBeep, liftoffSound, engineRumble, padVolume);
            pad.Sounds = sounds;
        }
        flight = new FlightMapView(root);
        if (!flight.Ready) flight = null;
        root.Q("landing-view")?.RemoveFromHierarchy();   // OnEnable може викликатись кілька разів
        landing = new LandingView(root) { Sounds = sounds };
        if (flight != null) flight.EndInLunarOrbit = true;   // карта — до орбіти Місяця, посадку покаже LandingView

        ready = stage != null && cargoList != null && ship != null && fuelSlider != null && launchButton != null;
        if (!ready)
        {
            Debug.LogError("LaunchScreen: не знайдено елементи. Перевір, що в UIDocument вказано LaunchScreen.uxml.");
            return;
        }

        fuelSlider.RegisterValueChangedCallback(OnFuelChanged);
        launchButton.clicked += OnLaunchClicked;
        if (closeButton != null) closeButton.clicked += Close;
        if (weatherChip != null)
        {
            weatherChip.clicked += OnWeatherClicked;
            // навели — пояснюємо, що ця погода означає для польоту
            weatherChip.RegisterCallback<PointerEnterEvent>(_ => { if (State != null) SetResult("launch.weather_why." + State.Supply.Weather); });
        }
        Loc.LanguageChanged += Refresh;
        GameEvents.StateChanged += Refresh;
        Loc.LanguageChanged += RefreshTab;
        GameEvents.StateChanged += RefreshTab;
        GameEvents.HourPassed += RefreshTab;

        // екран запуску малюється поверх планшета (обидва на одній панелі UI Toolkit)
        var doc = GetComponent<UIDocument>();
        if (doc.sortingOrder < 10) doc.sortingOrder = 10;

        Instance = this;
        SetVisible(false);
    }

    void OnDisable()
    {
        if (!ready) return;
        fuelSlider.UnregisterValueChangedCallback(OnFuelChanged);
        launchButton.clicked -= OnLaunchClicked;
        if (closeButton != null) closeButton.clicked -= Close;
        if (weatherChip != null) weatherChip.clicked -= OnWeatherClicked;
        Loc.LanguageChanged -= Refresh;
        GameEvents.StateChanged -= Refresh;
        Loc.LanguageChanged -= RefreshTab;
        GameEvents.StateChanged -= RefreshTab;
        GameEvents.HourPassed -= RefreshTab;
        PopModal();
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    // Картинки Юлі (роль 02): редактор сам підставляє їх у порожні поля, щоб не перетягувати вручну.
    // Після цього збережи сцену (Ctrl+S) — тоді картинки потраплять і у WebGL-збірку.
    const string PadBackgroundPath = "Assets/_Project/Art/Earth/Pixel Art Rocket Launch Site at Sunset.png";
    const string PadBusPath = "Assets/_Project/Art/Earth/Pixel Art Blue-Stripe Bus Sprite.png";

    void OnValidate() => AutoFillArt();
    void Reset() => AutoFillArt();

    void AutoFillArt()
    {
        bool changed = false;
        if (padBackground == null)
        {
            padBackground = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PadBackgroundPath);
            changed |= padBackground != null;
        }
        if (padBus == null)
        {
            padBus = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PadBusPath);
            changed |= padBus != null;
        }
        if (changed) UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    void Start()
    {
        if (openOnStart) Open();
    }

    void Update()
    {
        // планшет (05) може з'явитися пізніше за нас — тоді й заповнюємо його вкладку «ПОСТАВКИ»
        var tabletUi = TabletController.Instance;
        if (attachToTablet && ready && tabletUi != null && tabletUi != checkedTablet) AttachToTablet(tabletUi);
    }

    // ---------- Вкладка «ПОСТАВКИ» в планшеті ----------

    /// <summary>Відкрити замовлення поставки (з планшета чи HUD). До першої місії відкриється перша місія.</summary>
    public void OpenSupplyOrder() => Open();

    /// <summary>
    /// Вкладка «ПОСТАВКИ» у Вані поки заглушка (pane-supply-stub). Якщо заглушка ще там — ховаємо її і ставимо свій вміст.
    /// Якщо роль 05 уже зробила свою вкладку (заглушки немає), нічого не чіпаємо.
    /// </summary>
    void AttachToTablet(TabletController tabletUi)
    {
        checkedTablet = tabletUi;
        var tabletDoc = tabletUi.GetComponent<UIDocument>();
        var pane = tabletDoc != null ? tabletDoc.rootVisualElement?.Q("pane-supply") : null;
        var stub = pane?.Q<Label>("pane-supply-stub");
        if (pane == null || stub == null) return;

        stub.style.display = DisplayStyle.None;
        supplyTab = new SupplyTabView(pane, this, CollectStyleSheets());
        RefreshTab();
    }

    /// <summary>Стилі екрана запуску (Theme.uss + LaunchScreen.uss), щоб вкладка в планшеті виглядала так само.</summary>
    List<StyleSheet> CollectStyleSheets()
    {
        var sheets = new List<StyleSheet>();
        var root = GetComponent<UIDocument>().rootVisualElement;
        void Take(VisualElement e)
        {
            for (int i = 0; i < e.styleSheets.count; i++)
                if (!sheets.Contains(e.styleSheets[i])) sheets.Add(e.styleSheets[i]);
        }
        Take(root);
        root.Query<VisualElement>().ForEach(Take);
        return sheets;
    }

    void RefreshTab()
    {
        if (supplyTab == null || State == null || Supply == null) return;
        supplyTab.Refresh(State, Supply, Comms, PlanFor(false, Supply.Evaluate(State, false)));
    }

    /// <summary>План місії (дні запасів, цілі, порада) — для екрана і для вкладки планшета.</summary>
    public MissionPlan PlanFor(bool firstMission, LaunchResult r)
    {
        var chapters = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ChapterSystem>() : null;
        return MissionPlanner.Build(State, Supply, firstMission, r, balance, chapters != null ? chapters.Current : null, tasks);
    }

    /// <summary>Картинка ракети з інспектора (null — немає).</summary>
    public Texture2D RocketLook(RocketData rocket)
    {
        var tex = rocket != null ? FindLook(rocketLooks, rocket.id) : null;
        return tex != null ? tex : rocketTexture;
    }

    /// <summary>Іконка вантажу з інспектора (null — немає).</summary>
    public Texture2D CargoIcon(string id) => FindLook(cargoIcons, id);

    /// <summary>«день 3, 08:00» для години від початку гри.</summary>
    public static string DayTime(int absHour) => T("supply.day_time", absHour / 24 + 1, (absHour % 24).ToString("00") + ":00");

    // ---------- Відкрити / закрити ----------

    /// <summary>Відкрити: перша місія, якщо вона ще не долетіла, інакше — замовлення поставки.</summary>
    public void Open()
    {
        if (!ready) return;
        if (State == null || Supply == null)
        {
            Debug.LogError("LaunchScreen: у сцені немає GameBootstrap (об'єкт Game).");
            return;
        }
        IsFirstMission = !State.Supply.FirstMissionDone;
        if (IsFirstMission) Supply.ForecastFirstMission(State);   // погода на космодромі
        IsOpen = true;
        if (!modalPushed) { GameBootstrap.Instance.Clock.PushModal(); modalPushed = true; }
        SetVisible(true);
        SetResult(null);
        infoItem = null;
        infoRocket = null;
        infoMass = false;
        infoPlan = null;
        failedTries = 0;
        report?.AddToClassList("report--hidden");
        BuildRocketCards();
        BuildCargoRows();
        BuildDaysRows();
        BuildPlanButtons();
        Refresh();
        PlaceShip(0f, false);
    }

    public void Close()
    {
        if (animating) return;
        // першу місію не можна просто закрити: спершу треба долетіти
        if (IsFirstMission && State != null && !State.Supply.FirstMissionDone)
        {
            SetResult("launch.must_launch");
            return;
        }
        IsOpen = false;
        SetVisible(false);
        PopModal();
        GameEvents.RaiseStateChanged(); // планшет під нами оновить вкладку «ПОСТАВКИ»
    }

    void PopModal()
    {
        if (!modalPushed) return;
        modalPushed = false;
        if (GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null) GameBootstrap.Instance.Clock.PopModal();
    }

    void SetVisible(bool visible) => stage.EnableInClassList("launch-stage--hidden", !visible);

    void SetResult(string key) { if (resultLabel != null) resultLabel.text = string.IsNullOrEmpty(key) ? "" : T(key); }

    static void SetText(Label l, string text) { if (l != null) l.text = text; }

    static Texture2D FindLook(List<Look> looks, string id)
    {
        var look = looks.Find(l => l != null && l.id == id);
        return look != null ? look.texture : null;
    }

    // ---------- Ракети ----------

    void BuildRocketCards()
    {
        if (rocketList == null) return;
        rocketList.Clear();
        foreach (var rocket in Supply.Rockets.items)
        {
            var data = rocket;
            var card = new VisualElement { userData = data };
            card.AddToClassList("rocket-card");

            var pic = new VisualElement();
            pic.AddToClassList("rocket-pic");
            var tex = FindLook(rocketLooks, data.id) ?? rocketTexture;
            if (tex != null) pic.style.backgroundImage = new StyleBackground(tex);

            var texts = new VisualElement();
            texts.AddToClassList("rocket-texts");
            var nameLabel = new Label { name = "name" };
            nameLabel.AddToClassList("rocket-name");
            var stats = new Label { name = "stats" };
            stats.AddToClassList("rocket-stats");
            texts.Add(nameLabel); texts.Add(stats);

            card.Add(pic); card.Add(texts);
            card.RegisterCallback<ClickEvent>(_ => SelectRocket(data));
            card.RegisterCallback<PointerEnterEvent>(_ => ShowInfo(null, data));
            rocketList.Add(card);
        }
    }

    void SelectRocket(RocketData rocket)
    {
        if (animating || !Supply.CanEdit(State, IsFirstMission)) return;
        SetResult(Supply.SetRocket(State, rocket.id));
        ShowInfo(null, rocket);
        Refresh();
        PlaceShip(0f, false);
    }

    /// <summary>Вигляд ракети на кораблі: картинка вибраної ракети і її розмір.</summary>
    void ApplyShipLook(RocketData rocket)
    {
        var tex = rocket != null ? FindLook(rocketLooks, rocket.id) : null;
        if (tex == null) tex = rocketTexture;
        bool hasImage = tex != null;
        ship.EnableInClassList("ship--image", hasImage);
        ship.style.backgroundImage = hasImage ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
        float k = rocket != null && rocket.scale > 0f ? rocket.scale : 1f;
        k *= ShipBaseScale;
        ship.style.scale = new Scale(new Vector3(k, k, 1f));
    }

    // ---------- Вантажі ----------

    void BuildCargoRows()
    {
        cargoList.Clear();
        string group = null;
        foreach (var item in Supply.Catalog.items)
        {
            if (item.crewSeats > 0) continue; // місця людей показуємо на кораблі окремо

            // заголовок групи: «Запаси», «Набори модулів»
            string g = string.IsNullOrEmpty(item.group) ? "supplies" : item.group;
            if (g != group)
            {
                group = g;
                var header = new Label { userData = "launch.group." + g };
                header.AddToClassList("cargo-group");
                cargoList.Add(header);
            }

            var data = item;
            string id = item.id;
            var row = new VisualElement { userData = item };
            row.AddToClassList("cargo-row");

            var icon = new VisualElement();
            icon.AddToClassList("cargo-icon");
            var tex = FindLook(cargoIcons, id);
            if (tex != null) icon.style.backgroundImage = new StyleBackground(tex);
            else icon.AddToClassList("cargo-icon--none");

            var nameLabel = new Label { name = "name" };
            nameLabel.AddToClassList("cargo-name");
            var massText = new Label { name = "mass" };
            massText.AddToClassList("cargo-mass");
            var help = new Button(() => ShowInfo(data, null)) { text = "?" };
            help.AddToClassList("help-btn");
            var minus = new Button(() => ChangeCargo(id, -1)) { text = "−", name = "minus" };
            minus.AddToClassList("small-btn");
            var count = new Label { name = "count" };
            count.AddToClassList("cargo-count");
            var plus = new Button(() => ChangeCargo(id, +1)) { text = "+", name = "plus" };
            plus.AddToClassList("small-btn");

            // плитка: іконка і «?» зверху, назва, маса, внизу − кількість +
            var top = new VisualElement();
            top.AddToClassList("tile-top");
            var gap = new VisualElement();
            gap.AddToClassList("tile-gap");
            top.Add(icon); top.Add(gap); top.Add(help);
            var bottom = new VisualElement();
            bottom.AddToClassList("tile-bottom");
            bottom.Add(minus); bottom.Add(count); bottom.Add(plus);
            row.Add(top); row.Add(nameLabel); row.Add(massText); row.Add(bottom);
            row.RegisterCallback<PointerEnterEvent>(_ => ShowInfo(data, null));
            cargoList.Add(row);
        }
    }

    void ChangeCargo(string id, int delta)
    {
        if (animating) return;
        var item = Supply.Catalog.Find(id);
        SetResult(Supply.ChangeCargo(State, id, delta));
        if (item != null) ShowInfo(item, null);
        Refresh();
    }

    void OnFuelChanged(ChangeEvent<float> e)
    {
        if (animating || State == null || Supply == null) return;
        SetResult(Supply.SetFuel(State, Mathf.Round(e.newValue / 50f) * 50f));
        Refresh();
    }

    // ---------- Поле «Навіщо це?» ----------

    void ShowInfo(CargoItemData item, RocketData rocket)
    {
        infoMass = false;
        infoPlan = null;
        infoItem = item;
        infoRocket = item == null ? rocket : null;
        RefreshInfo();
    }

    void RefreshInfo()
    {
        SetText(infoTitle, T("launch.info_title"));
        if (infoItem != null)
        {
            SetText(infoTitle, T(infoItem.nameKey));   // назва замість «Навіщо це?» — тексту менше, влазить
            string text = T(infoItem.hintKey);
            if (infoItem.IsKit) text += " " + T("launch.kit_note");
            if (infoItem.TryGetModule(out var kit) && !Supply.KitUnlocked(kit)) text += " " + T("launch.locked_hint");
            SetText(infoText, text);
            SetText(infoGives, T("launch.gives", GivesText(infoItem)));
        }
        else if (infoRocket != null)
        {
            SetText(infoTitle, T(infoRocket.nameKey));
            SetText(infoText, T(infoRocket.hintKey) + " " + T("rocket.why"));
            string gives = T("rocket.stats", infoRocket.maxCargoKg.ToString("0"), LaunchText.Duration(Supply.FlightHoursOf(State, infoRocket)));
            int tripHours = IsFirstMission ? Supply.CrewTripHours(State, infoRocket) : 0;
            if (tripHours > 0)
            {
                // повільніша ракета — екіпаж довше в дорозі і більше з'їсть: це ціна великого трюму
                Supply.CrewTripUse(State, infoRocket, out float tripFood, out float tripOxygen);
                gives += "\n" + T("rocket.trip", LaunchText.Duration(tripHours), tripFood.ToString("0.#"), tripOxygen.ToString("0.#"));
            }
            SetText(infoGives, gives);
        }
        else if (infoPlan != null)
        {
            SetText(infoTitle, LaunchText.PlanName(Supply, infoPlan));
            SetText(infoText, T(infoPlan.hintKey) + (IsFirstMission ? "" : " " + T("plan.locked_hint")));
            SetText(infoGives, T("plan.rockets", RocketNames(infoPlan)));
        }
        else if (infoMass)
        {
            SetText(infoTitle, T("launch.mass_title"));
            SetText(infoText, T("launch.mass_why"));
            SetText(infoGives, "");
        }
        else
        {
            SetText(infoText, T("launch.info_default") + " " + T("mission.reserve_why"));
            SetText(infoGives, "");
        }

        foreach (var row in cargoList.Children())
            row.EnableInClassList("cargo-row--on", infoItem != null && row.userData == infoItem);
    }

    /// <summary>Що вантаж дає базі, словами: «+20 вода» або «новий модуль: теплиця».</summary>
    static string GivesText(CargoItemData item)
    {
        if (item.TryGetModule(out var type)) return T("launch.res.module", T("launch.module." + type));
        if (item.crewSeats > 0) return T("launch.res.seat");

        var parts = new List<string>();
        if (item.water > 0f) parts.Add("+" + item.water.ToString("0") + " " + T("launch.res.water"));
        if (item.food > 0f) parts.Add("+" + item.food.ToString("0") + " " + T("launch.res.food"));
        if (item.oxygen > 0f) parts.Add("+" + item.oxygen.ToString("0") + " " + T("launch.res.oxygen"));
        if (item.materials > 0f) parts.Add("+" + item.materials.ToString("0") + " " + T("launch.res.materials"));
        if (item.medKits > 0) parts.Add("+" + item.medKits + " " + T("launch.res.medkits"));
        return string.Join(", ", parts);
    }

    // ---------- Перерахунок ----------

    void Refresh()
    {
        if (!ready || !IsOpen || State == null || Supply == null) return;
        var s = State;
        var data = Supply.Data;
        var rocket = Supply.GetRocket(s);
        var r = Supply.Evaluate(s, IsFirstMission);
        bool editable = !animating && Supply.CanEdit(s, IsFirstMission);

        // заголовки
        SetText(titleLabel, T(IsFirstMission ? "launch.title_first" : "launch.title_supply"));
        SetText(rocketTitle, T("rocket.title"));
        SetText(rocketWhy, T("rocket.why"));
        SetText(cargoTitle, T("launch.cargo"));
        SetText(shipTitle, rocket != null ? T(rocket.nameKey) : T("launch.ship"));
        SetText(legendCrew, T("launch.legend_crew"));
        SetText(legendCargo, T("launch.legend_cargo"));
        SetText(fuelWhy, T("launch.fuel_why"));
        fuelSlider.SetEnabled(editable);

        // зв'язок (для поставки)
        bool link = IsFirstMission || (Comms != null && Comms.IsOpen(s));
        if (linkLed != null)
        {
            linkLed.EnableInClassList("led--ok", link);
            linkLed.EnableInClassList("led--bad", !link);
        }

        if (IsFirstMission)
        {
            launchButton.text = T("launch.go");
            launchButton.SetEnabled(editable);
            SetText(statusLabel, T("launch.link_ok"));
        }
        else
        {
            // поставка: ВІДПРАВИТИ (у вікно зв'язку) → СКАСУВАТИ ВІДПРАВКУ (поки не стартувала) → КОРАБЕЛЬ ЛЕТИТЬ
            var status = s.Supply.Status;
            if (status == SupplyStatus.Draft)
            {
                launchButton.text = link ? T("launch.send") : NoLinkText(s);
                launchButton.SetEnabled(!animating && link);
            }
            else if (status == SupplyStatus.Sent)
            {
                launchButton.text = T("supply.cancel");
                launchButton.SetEnabled(!animating);
            }
            else
            {
                launchButton.text = T("supply.flying_btn");
                launchButton.SetEnabled(false);
            }
            // зв'язок видно з вогника поруч, тому в рядку тільки статус і розклад (коротше — не налазить на заголовок)
            SetText(statusLabel, T("launch.status." + status) + " · "
                + T("launch.schedule", DayTime(Supply.ExpectedLaunchAt(s)), DayTime(Supply.ExpectedArrivalAt(s))));
        }

        // ракети
        if (rocketList != null)
        {
            foreach (var card in rocketList.Children())
            {
                if (!(card.userData is RocketData rd)) continue;
                card.Q<Label>("name").text = T(rd.nameKey);
                bool allowed = Supply.RocketAllowed(s, rd.id);   // поставка: тільки ракети з плану
                card.Q<Label>("stats").text = allowed
                    ? T("rocket.stats", rd.maxCargoKg.ToString("0"), LaunchText.Duration(Supply.FlightHoursOf(s, rd)))
                    : T("rocket.not_in_plan");
                card.EnableInClassList("rocket-card--on", rocket == rd);
                card.EnableInClassList("rocket-card--locked", !allowed);
                card.SetEnabled(editable && allowed);
            }
        }
        if (!animating) ApplyShipLook(rocket);

        // вантажі
        foreach (var row in cargoList.Children())
        {
            if (row.userData is string groupKey && row is Label header) { header.text = T(groupKey); continue; }
            if (!(row.userData is CargoItemData item)) continue;
            bool locked = item.TryGetModule(out var kitType) && !Supply.KitUnlocked(kitType);
            row.EnableInClassList("cargo-row--locked", locked);
            row.Q<Label>("name").text = T(item.nameKey);
            row.Q<Label>("mass").text = locked ? T("launch.locked") : T("launch.kg", item.massKg.ToString("0"));
            int packed = s.Supply.Count(item.id);
            row.Q<Label>("count").text = packed.ToString();
            row.EnableInClassList("cargo-row--has", packed > 0);
            row.Q<Button>("minus").SetEnabled(editable);
            row.Q<Button>("plus").SetEnabled(editable && !locked && Supply.Fits(s, item.id));   // не влазить у трюм — «+» не тиснеться
        }

        // паливо: повзунок до бака вибраної ракети
        float maxFuel = Supply.MaxFuel(s);
        fuelSlider.lowValue = 0f;
        fuelSlider.highValue = maxFuel;
        fuelSlider.SetValueWithoutNotify(r.FuelKg);
        SetText(fuelLabel, T("launch.fuel_value", r.FuelKg.ToString("0"), r.FuelNeededKg.ToString("0")));

        // смужка маси: шкала = найбільша маса, яку повний бак ще піднімає (жовта межа)
        float emptyKg = rocket != null ? rocket.emptyMassKg : data.emptyShipMassKg;
        float holdKg = Supply.MaxCargo(s);
        float scaleKg = holdKg < float.MaxValue
            ? Mathf.Max(1f, r.PeopleKg + holdKg)                                       // смужка = місця людей + трюм
            : Mathf.Max(1f, maxFuel / (data.fuelPerKg * data.redMargin) - emptyKg);
        float crewPct = Mathf.Clamp01(r.PeopleKg / scaleKg) * 100f;
        float cargoPct = Mathf.Clamp(r.CargoKg / scaleKg * 100f, 0f, 100f - crewPct);
        if (massCrew != null) massCrew.style.width = Length.Percent(crewPct);
        if (massCargo != null) massCargo.style.width = Length.Percent(cargoPct);
        massBar?.EnableInClassList("mass-bar--over", r.PeopleKg + r.CargoKg > scaleKg);
        SetText(seatsLabel, T("launch.seats", Supply.People(s, IsFirstMission), data.personMassKg.ToString("0")));
        SetText(massLabel, T("launch.mass", r.TotalMassKg.ToString("0"), r.CargoKg.ToString("0")));
        SetText(massLike, LikeText(r.TotalMassKg + r.FuelKg));
        RefreshHold(s);

        // світлофор
        lightRed?.EnableInClassList("light--red-on", r.Light == LaunchLight.Red);
        lightYellow?.EnableInClassList("light--yellow-on", r.Light == LaunchLight.Yellow);
        lightGreen?.EnableInClassList("light--green-on", r.Light == LaunchLight.Green);
        SetText(riskLabel, T("launch.risk", r.RiskPercent));
        if (riskLabel != null)
        {
            riskLabel.EnableInClassList("c-ok", r.Light == LaunchLight.Green);
            riskLabel.EnableInClassList("c-warn", r.Light == LaunchLight.Yellow);
            riskLabel.EnableInClassList("c-bad", r.Light == LaunchLight.Red);
        }
        SetText(hintLabel, T("launch.hint." + r.Light));
        RefreshWeather(editable);

        RefreshPlan(editable);
        RefreshMission(r);
        RefreshInfo();
    }

    // ---------- План поставок ----------

    void BuildPlanButtons()
    {
        if (planList == null) return;
        planList.Clear();
        foreach (var plan in Supply.Data.supplyPlans)
        {
            var p = plan;
            var b = new Button(() => SelectPlan(p)) { userData = p };
            b.AddToClassList("plan-btn");
            b.RegisterCallback<PointerEnterEvent>(_ => ShowPlanInfo(p));
            planList.Add(b);
        }
        planList.parent?.EnableInClassList("launch-hidden", Supply.Data.supplyPlans.Count == 0);
    }

    void SelectPlan(SupplyPlanData plan)
    {
        if (animating) return;
        SetResult(Supply.SetPlan(State, plan.id));
        ShowPlanInfo(plan);
        Refresh();
    }

    void ShowPlanInfo(SupplyPlanData plan)
    {
        infoItem = null;
        infoRocket = null;
        infoMass = false;
        infoPlan = plan;
        RefreshInfo();
    }

    void RefreshPlan(bool editable)
    {
        if (planList == null) return;
        SetText(planTitle, T("plan.title"));
        var current = Supply.GetPlan(State);
        foreach (var child in planList.Children())
        {
            if (!(child is Button b) || !(b.userData is SupplyPlanData p)) continue;
            b.text = LaunchText.PlanName(Supply, p).ToUpperInvariant();   // «РАЗ НА 16 ГОД» — з довжини розділу
            b.EnableInClassList("plan-btn--on", p == current);
            b.SetEnabled(editable && IsFirstMission);   // план обирають перед першим стартом
        }
    }

    /// <summary>«Бджола, Пелікан» — назви ракет плану.</summary>
    string RocketNames(SupplyPlanData plan)
    {
        var names = new List<string>();
        foreach (var r in Supply.Rockets.items)
            if (plan.rockets == null || plan.rockets.Count == 0 || plan.rockets.Contains(r.id)) names.Add(T(r.nameKey));
        return string.Join(", ", names);
    }

    // ---------- Трюм і «важить як…» ----------

    const float ElephantKg = 5000f;   // дорослий африканський слон важить приблизно 5 тонн
    const float CarKg = 1500f;        // легкова машина — приблизно 1,5 тонни
    const int HoldSlots = 24;         // скільки ящиків видно в трюмі

    /// <summary>Маса словами для дітей: «як 3 слони» або «як 2 машини».</summary>
    static string LikeText(float kg)
    {
        if (kg >= ElephantKg * 2f) return T("launch.like_elephants", (kg / ElephantKg).ToString("0.#"));
        return T("launch.like_cars", (kg / CarKg).ToString("0.#"));
    }

    /// <summary>Трюм: кожне місце вантажу — ящик зі своєю іконкою, знизу вгору.</summary>
    void RefreshHold(GameState s)
    {
        if (hold == null) return;
        float cap = Supply.MaxCargo(s);
        SetText(holdTitle, cap < float.MaxValue
            ? T("launch.hold_kg", Supply.CargoKg(s).ToString("0"), cap.ToString("0"))
            : T("launch.hold"));
        hold.Clear();
        int shown = 0, total = 0;
        foreach (var line in s.Supply.Cargo) total += line.count;
        foreach (var line in s.Supply.Cargo)
        {
            var item = Supply.Catalog.Find(line.itemId);
            if (item == null || item.crewSeats > 0) continue;
            var tex = FindLook(cargoIcons, item.id);
            for (int i = 0; i < line.count; i++)
            {
                if (shown >= HoldSlots - 1 && total > HoldSlots) break;
                var crate = new VisualElement();
                crate.AddToClassList("crate");
                if (item.IsKit) crate.AddToClassList("crate--kit");
                if (tex != null) crate.style.backgroundImage = new StyleBackground(tex);
                hold.Add(crate);
                shown++;
            }
        }
        if (total > shown)
        {
            var more = new Label("+" + (total - shown));
            more.AddToClassList("crate");
            more.AddToClassList("crate--more");
            hold.Add(more);
        }
    }

    // ---------- Звіт місії із зірочками ----------

    /// <summary>Зірочки рахуємо ДО старту: що поклали, чи вистачить, чи не завелика ракета.</summary>
    class MissionStars
    {
        public bool FirstTry, Supplies, NoWaste;
        public string TipKey;
        public object[] TipArgs = new object[0];
        public int Count => (FirstTry ? 1 : 0) + (Supplies ? 1 : 0) + (NoWaste ? 1 : 0);
    }

    MissionStars RateMission()
    {
        var s = State;
        var r = Supply.Evaluate(s, true);
        var chapters = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ChapterSystem>() : null;
        var plan = MissionPlanner.Build(s, Supply, true, r, balance, chapters != null ? chapters.Current : null, tasks);
        var smaller = MissionPlanner.SmallerRocketThatFits(s, Supply, true);
        bool oxygenWasted = plan.Oxygen.RawTotal > plan.OxygenTankMax + 0.01f;

        var stars = new MissionStars
        {
            FirstTry = failedTries == 0,
            Supplies = plan.SurviveOk,
            NoWaste = !oxygenWasted && smaller == null
        };
        if (!stars.Supplies) stars.TipKey = "report.tip_supplies";
        else if (oxygenWasted) { stars.TipKey = "report.tip_oxygen"; stars.TipArgs = new object[] { plan.OxygenTankMax.ToString("0") }; }
        else if (smaller != null) { stars.TipKey = "report.tip_smaller"; stars.TipArgs = new object[] { T(smaller.nameKey) }; }
        else if (!stars.FirstTry) stars.TipKey = "report.tip_first";
        else stars.TipKey = "report.all";
        return stars;
    }

    IEnumerator ShowReport(MissionStars stars)
    {
        if (report == null) yield break;
        SetText(reportTitle, T("launch.report_title"));
        reportStars.Clear();
        for (int i = 0; i < 3; i++) reportStars.Add(new StarShape(i < stars.Count, false));
        reportList.Clear();
        AddReportRow(stars.FirstTry, "report.star_first");
        AddReportRow(stars.Supplies, "report.star_supplies");
        AddReportRow(stars.NoWaste, "report.star_nowaste");
        if (State != null && (State.Supply.TripFood > 0f || State.Supply.TripOxygen > 0f))
        {
            var trip = new Label(T("report.trip", State.Supply.TripFood.ToString("0.#"), State.Supply.TripOxygen.ToString("0.#")));
            trip.AddToClassList("report-text");
            trip.AddToClassList("report-trip");
            reportList.Add(trip);
        }
        SetText(reportTip, T(stars.TipKey, stars.TipArgs));
        if (reportNext != null) reportNext.text = T("report.next");

        reportClosed = false;
        report.RemoveFromClassList("report--hidden");
        while (!reportClosed) yield return null;
        report.AddToClassList("report--hidden");
    }

    void AddReportRow(bool on, string key)
    {
        var row = new VisualElement();
        row.AddToClassList("report-row");
        row.Add(new StarShape(on, true));
        var text = new Label(T(key));
        text.AddToClassList("report-text");
        text.EnableInClassList("report-text--off", !on);
        row.Add(text);
        reportList.Add(row);
    }

    // ---------- Цілі місії, дні запасів, «Центр керування» ----------

    static readonly string[] DayKinds = { "food", "water", "oxygen" };

    void BuildDaysRows()
    {
        if (daysList == null) return;
        daysList.Clear();
        foreach (var kind in DayKinds)
        {
            var row = new VisualElement { name = "days-" + kind, userData = kind };
            row.AddToClassList("days-row");
            var nameLabel = new Label { name = "name" };
            nameLabel.AddToClassList("days-name");
            var bar = new VisualElement();
            bar.AddToClassList("days-bar");
            var have = new VisualElement { name = "have" };
            have.AddToClassList("days-have");
            var bring = new VisualElement { name = "bring" };
            bring.AddToClassList("days-bring");
            var mark = new VisualElement();
            mark.AddToClassList("days-mark");
            bar.Add(have); bar.Add(bring); bar.Add(mark);
            var value = new Label { name = "value" };
            value.AddToClassList("days-value");
            row.Add(nameLabel); row.Add(bar); row.Add(value);
            daysList.Add(row);
        }
        // рядок «у дорозі екіпаж з'їсть…» (тільки перша місія)
        var trip = new Label { name = "days-trip", userData = "trip" };
        trip.AddToClassList("days-trip");
        daysList.Add(trip);
    }

    void RefreshMission(LaunchResult r)
    {
        var plan = PlanFor(IsFirstMission, r);

        // цілі місії
        // «Цілі першого етапу місії»: етап = розділ гри; якщо для розділу тексту немає — просто «Цілі місії»
        string goalsKey = "mission.title." + State.Chapter;
        string goals = T(goalsKey);
        SetText(goalsTitle, goals.StartsWith("#") ? T("mission.title") : goals);
        if (goalsList != null)
        {
            goalsList.Clear();
            AddGoalRow(T("mission.next_ship", LaunchText.Duration(Mathf.CeilToInt(plan.DaysToNextShip * 24f - 0.01f)),
                         LaunchText.Duration(Mathf.RoundToInt(plan.ReserveDays * 24f))), plan.SurviveOk);
            foreach (var g in plan.Goals)
                AddGoalRow(T("mission.build", T("launch.module." + g.Module), Mathf.Min(g.Have + g.Packed, g.Target), g.Target), g.Done);
        }

        // на скільки днів вистачить
        SetText(daysTitle, T("days.title"));
        if (daysList != null)
        {
            foreach (var row in daysList.Children())
            {
                string kind = row.userData as string;
                if (kind == "trip")
                {
                    bool show = plan.TripHours > 0;
                    row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                    if (show) ((Label)row).text = T("days.trip", LaunchText.Duration(plan.TripHours),
                                                      plan.Food.Travel.ToString("0.#"), plan.Oxygen.Travel.ToString("0.#"));
                    continue;
                }
                var d = kind == "food" ? plan.Food : kind == "water" ? plan.Water : plan.Oxygen;
                row.Q<Label>("name").text = T("days." + kind);

                float full = Mathf.Max(1f, plan.MissionDays * 2f); // уся смужка = двічі по стільки, скільки треба; біла риска = треба
                bool endless = float.IsInfinity(d.Days);
                float havePct = endless ? 100f : Mathf.Clamp01(d.Have / d.PerDay / full) * 100f;
                float bringPct = endless ? 0f : Mathf.Clamp(d.BringDays / full * 100f, 0f, 100f - havePct);
                row.Q("have").style.width = Length.Percent(havePct);
                row.Q("bring").style.width = Length.Percent(bringPct);

                string days = endless ? T("days.enough")
                    : d.Bring > 0f ? T("days.value", Mathf.FloorToInt(d.Days)) + " (+" + d.BringDays.ToString("0.#") + ")"
                    : T("days.value", Mathf.FloorToInt(d.Days));
                row.Q<Label>("value").text = days;

                bool ok = d.Days >= plan.MissionDays;
                bool warn = !ok && d.Days >= plan.MissionDays * 0.6f;
                row.EnableInClassList("days-row--ok", ok);
                row.EnableInClassList("days-row--warn", warn);
                row.EnableInClassList("days-row--bad", !ok && !warn);
            }
        }

        // порада
        SetText(advisorName, T("advisor.name"));
        SetText(advisorText, T(plan.TipKey, plan.TipArgs));
        advisor?.EnableInClassList("advisor--good", !plan.TipIsWarning);
        advisor?.EnableInClassList("advisor--warn", plan.TipIsWarning);
    }

    void AddGoalRow(string text, bool done)
    {
        var row = new VisualElement();
        row.AddToClassList("goal-row");
        var box = new VisualElement();
        box.AddToClassList("goal-box");
        box.EnableInClassList("goal-box--done", done);
        var label = new Label(text);
        label.AddToClassList("goal-text");
        label.EnableInClassList("goal-text--done", done);
        row.Add(box); row.Add(label);
        goalsList.Add(row);
    }

    // ---------- Кнопка ЗАПУСК ----------

    void OnLaunchClicked()
    {
        if (animating || State == null) return;
        if (IsFirstMission)
        {
            var stars = RateMission();                  // до старту: вантаж ще в ракеті, запаси ще не додані
            bool ok = Supply.LaunchFirstMission(State); // розрахунок і «кубик» — у SupplySystem
            if (!ok) failedTries++;
            if (pad != null && showLaunchPad) StartCoroutine(PadLaunch(ok, stars));
            else StartCoroutine(Fly(ok));
        }
        else
        {
            if (State.Supply.Status == SupplyStatus.Sent) SetResult(Supply.CancelSend(State) ?? "supply.cancelled");
            else SetResult(Supply.Send(State) ?? "launch.order_sent");
            GameEvents.RaiseStateChanged();
            Refresh();
        }
    }

    // ---------- Погода ----------

    /// <summary>Чип погоди біля ризику. Тільки для першої місії: погоду поставок вирішує старт за розкладом.</summary>
    void RefreshWeather(bool editable)
    {
        if (weatherChip == null) return;
        var w = State.Supply.Weather;
        weatherChip.EnableInClassList("weather-chip--hidden", !IsFirstMission);
        foreach (LaunchWeather v in Enum.GetValues(typeof(LaunchWeather)))
            weatherChip.EnableInClassList("weather-chip--" + v, v == w);
        bool storm = w == LaunchWeather.Storm;
        weatherChip.text = storm
            ? T("launch.weather_wait", Mathf.RoundToInt(Supply.Data.stormBoilOff * 100f))
            : T("launch.weather." + w);
        weatherChip.SetEnabled(editable);
    }

    /// <summary>Гроза: перечекати (частина палива випаровується). В іншу погоду — просто пояснення.</summary>
    void OnWeatherClicked()
    {
        if (animating || State == null || !IsFirstMission) return;
        if (State.Supply.Weather != LaunchWeather.Storm)
        {
            SetResult("launch.weather_why." + State.Supply.Weather);
            return;
        }
        SetResult(Supply.WaitOutStorm(State) ?? "launch.weather_waited");
        Refresh();
    }

    /// <summary>Чому не можна відправити: антена без енергії або вікно зв'язку закрите (і коли відкриється).</summary>
    string NoLinkText(GameState s)
    {
        if (s.Power.Comms <= 0) return T("supply.no_power_btn");
        return T("supply.no_link_btn", Comms != null ? Comms.HoursUntilOpen(s) : 0);
    }

    // ---------- Пусковий майданчик ----------

    /// <summary>Майданчик: екіпаж заходить у ракету, відлік, злет. Успіх — екран закривається; невдача — ракета повертається.</summary>
    IEnumerator PadLaunch(bool success, MissionStars stars)
    {
        animating = true;
        Refresh();
        tablet?.AddToClassList("launch-hidden");

        var rocket = Supply.GetRocket(State);
        var tex = rocket != null ? FindLook(rocketLooks, rocket.id) : null;
        if (tex == null) tex = rocketTexture;
        float aspect = tex != null && tex.height > 0 ? (float)tex.width / tex.height : 0.3f;

        var crew = new List<(string name, PadCrewFrames frames)>();
        foreach (var a in State.Crew)
        {
            if (crew.Count >= Supply.People(State, true)) break;
            crew.Add((a.Name, CrewFrames(a.Role)));
        }

        yield return pad.Play(crew, tex != null ? new StyleBackground(tex) : default, tex != null, aspect, rocket, success);

        if (success && flight != null) yield return flight.Play(tex, aspect); // Дніпро → орбіта Місяця
        if (success && landing != null) yield return landing.Play(crew);     // посадка на південному полюсі
        if (success) yield return ShowReport(stars);
        tablet?.RemoveFromClassList("launch-hidden");
        if (success)
        {
            animating = false;
            GameEvents.RaiseLaunchFinished(true);
            Close();   // гра на Місяці продовжується
        }
        else
        {
            yield return Fly(false); // ракета розвертається на Землю, гравець перепаковує
        }
    }

    /// <summary>Кадри скафандра: з CrewLook (як на Місяці), інакше з crewSuits в інспекторі. null — картинок немає.</summary>
    PadCrewFrames CrewFrames(CrewRole role)
    {
        var look = CrewLook.Instance != null ? CrewLook.Instance.Find(role) : null;
        if (look != null && look.suit != null)
        {
            var set = look.suit;
            bool flip = CharacterSprites.IsEmpty(set.walkRight);
            var walk = flip ? set.walkLeft : set.walkRight;
            if (!CharacterSprites.IsEmpty(walk))
            {
                var frames = new PadCrewFrames { flip = flip, walk = new StyleBackground[walk.Length] };
                for (int i = 0; i < walk.Length; i++) frames.walk[i] = new StyleBackground(walk[i]);
                frames.back = new StyleBackground(set.idleBack != null ? set.idleBack : walk[0]);
                return frames;
            }
        }

        var suit = crewSuits.Find(c => c != null && c.role == role);
        if (suit == null || suit.walkRight == null || suit.walkRight.Length == 0 || suit.walkRight[0] == null) return null;
        var result = new PadCrewFrames { walk = new StyleBackground[suit.walkRight.Length] };
        for (int i = 0; i < suit.walkRight.Length; i++) result.walk[i] = new StyleBackground(suit.walkRight[i]);
        result.back = new StyleBackground(suit.back != null ? suit.back : suit.walkRight[0]);
        return result;
    }

    // ---------- Анімація польоту ----------

    /// <summary>Ракета летить по дузі (крива Безьє). Невдача: долітає до середини, розвертається і повертається.</summary>
    IEnumerator Fly(bool success)
    {
        animating = true;
        Refresh();
        SetResult("launch.flying");

        float endT = success ? 1f : 0.5f;
        float duration = flightSeconds * endT;
        for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
        {
            PlaceShip(time / flightSeconds, false);
            yield return null;
        }
        PlaceShip(endT, false);

        if (!success)
        {
            SetResult("launch.turning_back");
            const float turnSeconds = 0.5f;
            for (float a = 0f; a < turnSeconds; a += Time.unscaledDeltaTime)
            {
                ship.style.rotate = new Rotate(new Angle(TangentDeg(endT) + 180f * (a / turnSeconds)));
                yield return null;
            }
            for (float time = duration; time > 0f; time -= Time.unscaledDeltaTime)
            {
                PlaceShip(time / flightSeconds, true);
                yield return null;
            }
            PlaceShip(0f, true);
        }

        yield return new WaitForSecondsRealtime(1f);
        animating = false;
        GameEvents.RaiseLaunchFinished(success);

        if (success)
        {
            Close();          // долетіли — екран закривається, гра продовжується
        }
        else
        {
            PlaceShip(0f, false);
            SetResult("launch.failed_repack");
            Refresh();
        }
    }

    // Точки кривої Безьє у пікселях відносно старту: старт, точка згину, фініш
    static readonly Vector2 P0 = new Vector2(0f, 0f);
    static readonly Vector2 P1 = new Vector2(180f, -220f);
    static readonly Vector2 P2 = new Vector2(60f, -520f);

    static Vector2 Bezier(float t)
    {
        float u = 1f - t;
        return u * u * P0 + 2f * u * t * P1 + t * t * P2;
    }

    /// <summary>Кут дотичної до кривої, щоб ніс ракети дивився туди, куди вона летить.</summary>
    static float TangentDeg(float t)
    {
        Vector2 d = 2f * (1f - t) * (P1 - P0) + 2f * t * (P2 - P1);
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 90f; // ракета намальована носом угору
    }

    void PlaceShip(float t, bool backwards)
    {
        t = Mathf.Clamp01(t);
        Vector2 pos = Bezier(t);
        ship.style.translate = new StyleTranslate(new Translate(pos.x, pos.y));
        ship.style.rotate = t <= 0f && !backwards
            ? new Rotate(new Angle(0f))
            : new Rotate(new Angle(TangentDeg(t) + (backwards ? 180f : 0f)));
    }
}
