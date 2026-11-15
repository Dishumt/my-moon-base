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

    [Tooltip("Показувати пусковий майданчик (екіпаж, відлік, злет) після ПІДТВЕРДИТИ")]
    [SerializeField] bool showLaunchPad = true;

    [Tooltip("Скільки секунд летить ракета")]
    [SerializeField] float flightSeconds = 3f;

    public bool IsOpen { get; private set; }
    public bool IsFirstMission { get; private set; }

    VisualElement stage, cargoList, rocketList, ship, massBar, massCrew, massCargo, lightRed, lightYellow, lightGreen, linkLed;
    Label titleLabel, statusLabel, rocketTitle, rocketWhy, cargoTitle, shipTitle, legendCrew, legendCargo;
    Label seatsLabel, massLabel, fuelLabel, fuelWhy, riskLabel, hintLabel, resultLabel;
    Label infoTitle, infoText, infoGives;
    Label goalsTitle, daysTitle, advisorName, advisorText;
    VisualElement goalsList, daysList, advisor;
    SimBalanceData balance;   // правила витрат ролі 03 — для «на скільки днів вистачить»

    const float ShipBaseScale = 0.75f; // ракета на планшеті менша, ніж на майданчику
    Slider fuelSlider;
    Button launchButton, closeButton;
    VisualElement tablet;
    LaunchPadView pad;
    bool animating, modalPushed, ready;

    // що зараз показано в полі «Навіщо це?»: вантаж або ракета (null — загальна підказка)
    CargoItemData infoItem;
    RocketData infoRocket;

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
        closeButton = root.Q<Button>("close-button");
        tablet = root.Q("launch");
        goalsTitle = root.Q<Label>("goals-title");
        goalsList = root.Q("goals-list");
        daysTitle = root.Q<Label>("days-title");
        daysList = root.Q("days-list");
        advisor = root.Q("advisor");
        advisorName = root.Q<Label>("advisor-name");
        advisorText = root.Q<Label>("advisor-text");
        if (balance == null) balance = DataLoader.Load<SimBalanceData>("Balance/balance");
        pad = new LaunchPadView(root);
        if (!pad.Ready) pad = null; // у UXML немає майданчика — буде тільки політ по дузі

        ready = stage != null && cargoList != null && ship != null && fuelSlider != null && launchButton != null;
        if (!ready)
        {
            Debug.LogError("LaunchScreen: не знайдено елементи. Перевір, що в UIDocument вказано LaunchScreen.uxml.");
            return;
        }

        fuelSlider.RegisterValueChangedCallback(OnFuelChanged);
        launchButton.clicked += OnLaunchClicked;
        if (closeButton != null) closeButton.clicked += Close;
        Loc.LanguageChanged += Refresh;
        GameEvents.StateChanged += Refresh;

        SetVisible(false);
    }

    void OnDisable()
    {
        if (!ready) return;
        fuelSlider.UnregisterValueChangedCallback(OnFuelChanged);
        launchButton.clicked -= OnLaunchClicked;
        if (closeButton != null) closeButton.clicked -= Close;
        Loc.LanguageChanged -= Refresh;
        GameEvents.StateChanged -= Refresh;
        PopModal();
    }

    void Start()
    {
        if (openOnStart) Open();
    }

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
        IsOpen = true;
        if (!modalPushed) { GameBootstrap.Instance.Clock.PushModal(); modalPushed = true; }
        SetVisible(true);
        SetResult(null);
        infoItem = null;
        infoRocket = null;
        BuildRocketCards();
        BuildCargoRows();
        BuildDaysRows();
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

            row.Add(icon); row.Add(nameLabel); row.Add(massText); row.Add(help); row.Add(minus); row.Add(count); row.Add(plus);
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
        infoItem = item;
        infoRocket = item == null ? rocket : null;
        RefreshInfo();
    }

    void RefreshInfo()
    {
        SetText(infoTitle, T("launch.info_title"));
        if (infoItem != null)
        {
            string text = T(infoItem.nameKey) + ". " + T(infoItem.hintKey);
            if (infoItem.IsKit) text += " " + T("launch.kit_note");
            if (infoItem.TryGetModule(out var kit) && !Supply.KitUnlocked(kit)) text += " " + T("launch.locked_hint");
            SetText(infoText, text);
            SetText(infoGives, T("launch.gives", GivesText(infoItem)));
        }
        else if (infoRocket != null)
        {
            SetText(infoText, T(infoRocket.nameKey) + ". " + T(infoRocket.hintKey) + " " + T("rocket.why"));
            SetText(infoGives, T("rocket.stats", infoRocket.maxFuelKg.ToString("0"), infoRocket.flightHours));
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
        launchButton.text = T(IsFirstMission ? "launch.go" : "launch.send");
        launchButton.SetEnabled(editable);
        fuelSlider.SetEnabled(editable);

        // зв'язок (для поставки)
        bool link = IsFirstMission || (Comms != null && Comms.IsOpen(s));
        if (linkLed != null)
        {
            linkLed.EnableInClassList("led--ok", link);
            linkLed.EnableInClassList("led--bad", !link);
        }
        SetText(statusLabel, IsFirstMission
            ? T("launch.link_ok")
            : T("launch.status." + s.Supply.Status) + " · " + T(link ? "launch.link_ok" : "launch.link_off"));

        // ракети
        if (rocketList != null)
        {
            foreach (var card in rocketList.Children())
            {
                if (!(card.userData is RocketData rd)) continue;
                card.Q<Label>("name").text = T(rd.nameKey);
                card.Q<Label>("stats").text = T("rocket.stats", rd.maxFuelKg.ToString("0"), rd.flightHours);
                card.EnableInClassList("rocket-card--on", rocket == rd);
                card.SetEnabled(editable);
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
            row.Q<Label>("count").text = s.Supply.Count(item.id).ToString();
            row.Q<Button>("minus").SetEnabled(editable);
            row.Q<Button>("plus").SetEnabled(editable && !locked);
        }

        // паливо: повзунок до бака вибраної ракети
        float maxFuel = Supply.MaxFuel(s);
        fuelSlider.lowValue = 0f;
        fuelSlider.highValue = maxFuel;
        fuelSlider.SetValueWithoutNotify(r.FuelKg);
        SetText(fuelLabel, T("launch.fuel_value", r.FuelKg.ToString("0"), r.FuelNeededKg.ToString("0")));

        // смужка маси: шкала = найбільша маса, яку повний бак ще піднімає (жовта межа)
        float emptyKg = rocket != null ? rocket.emptyMassKg : data.emptyShipMassKg;
        float scaleKg = Mathf.Max(1f, maxFuel / (data.fuelPerKg * data.redMargin) - emptyKg);
        float crewPct = Mathf.Clamp01(r.PeopleKg / scaleKg) * 100f;
        float cargoPct = Mathf.Clamp(r.CargoKg / scaleKg * 100f, 0f, 100f - crewPct);
        if (massCrew != null) massCrew.style.width = Length.Percent(crewPct);
        if (massCargo != null) massCargo.style.width = Length.Percent(cargoPct);
        massBar?.EnableInClassList("mass-bar--over", r.PeopleKg + r.CargoKg > scaleKg);
        SetText(seatsLabel, T("launch.seats", Supply.People(s, IsFirstMission), data.personMassKg.ToString("0")));
        SetText(massLabel, T("launch.mass", r.TotalMassKg.ToString("0"), r.CargoKg.ToString("0")));

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

        RefreshMission(r);
        RefreshInfo();
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
    }

    void RefreshMission(LaunchResult r)
    {
        var chapters = GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<ChapterSystem>() : null;
        var plan = MissionPlanner.Build(State, Supply, IsFirstMission, r, balance, chapters != null ? chapters.Current : null);

        // цілі місії
        SetText(goalsTitle, T("mission.title"));
        if (goalsList != null)
        {
            goalsList.Clear();
            AddGoalRow(T("mission.next_ship", Mathf.CeilToInt(plan.DaysToNextShip - 0.01f), plan.ReserveDays), plan.SurviveOk);
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
            bool ok = Supply.LaunchFirstMission(State); // розрахунок і «кубик» — у SupplySystem
            if (pad != null && showLaunchPad) StartCoroutine(PadLaunch(ok));
            else StartCoroutine(Fly(ok));
        }
        else
        {
            SetResult(Supply.Send(State) ?? "launch.order_sent");
            Refresh();
        }
    }

    // ---------- Пусковий майданчик ----------

    /// <summary>Майданчик: екіпаж заходить у ракету, відлік, злет. Успіх — екран закривається; невдача — ракета повертається.</summary>
    IEnumerator PadLaunch(bool success)
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
