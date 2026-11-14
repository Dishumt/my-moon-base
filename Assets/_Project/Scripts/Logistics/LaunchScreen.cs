// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Екран запуску на UI Toolkit (розкладка UI/Launch/LaunchScreen.uxml).
/// Зліва вантажі (− / +), справа корабель і смужка маси (екіпаж окремо), внизу паливо,
/// світлофор, «Ризик: N%» і кнопка ЗАПУСК. Кожна зміна одразу перераховує світлофор.
/// Поки екран відкритий — гра на паузі (Clock.PushModal / PopModal).
/// Режими: перша місія (4 місця екіпажу, політ з анімацією) і поставка (замовлення йде у вікно зв'язку).
/// Числа — з Launch/launch.json через SupplySystem, тексти — через Loc.Get.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LaunchScreen : MonoBehaviour
{
    [Tooltip("Відкрити екран одразу після старту сцени")]
    [SerializeField] bool openOnStart = true;

    [Tooltip("Картинка ракети (Art/Launch/rocket.png від ролі 02). Порожньо — ракета з прямокутників.")]
    [SerializeField] Texture2D rocketTexture;

    [Tooltip("Скільки секунд летить ракета")]
    [SerializeField] float flightSeconds = 3f;

    public bool IsOpen { get; private set; }
    public bool IsFirstMission { get; private set; }

    VisualElement stage, cargoList, ship, massBar, massCrew, massCargo, lightRed, lightYellow, lightGreen, linkLed;
    Label titleLabel, statusLabel, cargoTitle, shipTitle, legendCrew, legendCargo;
    Label seatsLabel, massLabel, fuelLabel, riskLabel, hintLabel, resultLabel;
    Slider fuelSlider;
    Button launchButton, closeButton;
    bool animating, modalPushed, ready;

    GameState State => GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
    SupplySystem Supply => GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<SupplySystem>() : null;
    CommsSystem Comms => GameBootstrap.Instance != null ? GameBootstrap.Instance.GetSystem<CommsSystem>() : null;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        stage = root.Q("launch-stage");
        cargoList = root.Q("cargo-list");
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
        cargoTitle = root.Q<Label>("cargo-title");
        shipTitle = root.Q<Label>("ship-title");
        legendCrew = root.Q<Label>("legend-crew");
        legendCargo = root.Q<Label>("legend-cargo");
        seatsLabel = root.Q<Label>("seats-label");
        massLabel = root.Q<Label>("mass-label");
        fuelLabel = root.Q<Label>("fuel-label");
        riskLabel = root.Q<Label>("risk-label");
        hintLabel = root.Q<Label>("hint-label");
        resultLabel = root.Q<Label>("result-label");
        fuelSlider = root.Q<Slider>("fuel-slider");
        launchButton = root.Q<Button>("launch-button");
        closeButton = root.Q<Button>("close-button");

        ready = stage != null && cargoList != null && ship != null && fuelSlider != null && launchButton != null;
        if (!ready)
        {
            Debug.LogError("LaunchScreen: не знайдено елементи. Перевір, що в UIDocument вказано LaunchScreen.uxml.");
            return;
        }

        if (rocketTexture != null)
        {
            ship.style.backgroundImage = new StyleBackground(rocketTexture);
            ship.AddToClassList("ship--image");
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
        PlaceShip(0f, false);
        BuildCargoRows();
        Refresh();
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

    void SetResult(string key) { if (resultLabel != null) resultLabel.text = string.IsNullOrEmpty(key) ? "" : Loc.Get(key); }

    static void SetText(Label l, string text) { if (l != null) l.text = text; }

    // ---------- Вантажі ----------

    void BuildCargoRows()
    {
        cargoList.Clear();
        foreach (var item in Supply.Catalog.items)
        {
            if (item.crewSeats > 0) continue; // місця людей показуємо на кораблі окремо
            string id = item.id;

            var row = new VisualElement { userData = item };
            row.AddToClassList("cargo-row");

            var nameLabel = new Label { name = "name" };
            nameLabel.AddToClassList("cargo-name");
            var massText = new Label { name = "mass" };
            massText.AddToClassList("cargo-mass");
            var minus = new Button(() => ChangeCargo(id, -1)) { text = "−" };
            minus.AddToClassList("small-btn");
            var count = new Label { name = "count" };
            count.AddToClassList("cargo-count");
            var plus = new Button(() => ChangeCargo(id, +1)) { text = "+" };
            plus.AddToClassList("small-btn");

            row.Add(nameLabel); row.Add(massText); row.Add(minus); row.Add(count); row.Add(plus);
            cargoList.Add(row);
        }
    }

    void ChangeCargo(string id, int delta)
    {
        if (animating) return;
        SetResult(Supply.ChangeCargo(State, id, delta));
        Refresh();
    }

    void OnFuelChanged(ChangeEvent<float> e)
    {
        if (animating || State == null || Supply == null) return;
        SetResult(Supply.SetFuel(State, Mathf.Round(e.newValue / 50f) * 50f));
        Refresh();
    }

    // ---------- Перерахунок ----------

    void Refresh()
    {
        if (!ready || !IsOpen || State == null || Supply == null) return;
        var s = State;
        var data = Supply.Data;
        var r = Supply.Evaluate(s, IsFirstMission);
        bool editable = !animating && Supply.CanEdit(s, IsFirstMission);

        // заголовки
        SetText(titleLabel, Loc.Get(IsFirstMission ? "launch.title_first" : "launch.title_supply"));
        SetText(cargoTitle, Loc.Get("launch.cargo"));
        SetText(shipTitle, Loc.Get("launch.ship"));
        SetText(legendCrew, Loc.Get("launch.legend_crew"));
        SetText(legendCargo, Loc.Get("launch.legend_cargo"));
        launchButton.text = Loc.Get(IsFirstMission ? "launch.go" : "launch.send");
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
            ? Loc.Get("launch.link_ok")
            : Loc.Get("launch.status." + s.Supply.Status) + " · " + Loc.Get(link ? "launch.link_ok" : "launch.link_off"));

        // вантажі
        foreach (var row in cargoList.Children())
        {
            if (!(row.userData is CargoItemData item)) continue;
            row.Q<Label>("name").text = Loc.Get(item.nameKey);
            row.Q<Label>("mass").text = Loc.Get("launch.kg", item.massKg.ToString("0"));
            row.Q<Label>("count").text = s.Supply.Count(item.id).ToString();
            row.SetEnabled(editable);
        }

        // паливо
        fuelSlider.lowValue = 0f;
        fuelSlider.highValue = data.maxFuelKg;
        fuelSlider.SetValueWithoutNotify(r.FuelKg);
        SetText(fuelLabel, Loc.Get("launch.fuel_value", r.FuelKg.ToString("0"), r.FuelNeededKg.ToString("0")));

        // смужка маси: шкала = найбільша маса, яку повний бак ще піднімає (жовта межа)
        float scaleKg = data.maxFuelKg / (data.fuelPerKg * data.redMargin) - data.emptyShipMassKg;
        float crewPct = Mathf.Clamp01(r.PeopleKg / scaleKg) * 100f;
        float cargoPct = Mathf.Clamp(r.CargoKg / scaleKg * 100f, 0f, 100f - crewPct);
        if (massCrew != null) massCrew.style.width = Length.Percent(crewPct);
        if (massCargo != null) massCargo.style.width = Length.Percent(cargoPct);
        massBar?.EnableInClassList("mass-bar--over", r.PeopleKg + r.CargoKg > scaleKg);
        SetText(seatsLabel, Loc.Get("launch.seats", Supply.People(s, IsFirstMission), data.personMassKg.ToString("0")));
        SetText(massLabel, Loc.Get("launch.mass", r.TotalMassKg.ToString("0"), r.CargoKg.ToString("0")));

        // світлофор
        lightRed?.EnableInClassList("light--red-on", r.Light == LaunchLight.Red);
        lightYellow?.EnableInClassList("light--yellow-on", r.Light == LaunchLight.Yellow);
        lightGreen?.EnableInClassList("light--green-on", r.Light == LaunchLight.Green);
        SetText(riskLabel, Loc.Get("launch.risk", r.RiskPercent));
        if (riskLabel != null)
        {
            riskLabel.EnableInClassList("c-ok", r.Light == LaunchLight.Green);
            riskLabel.EnableInClassList("c-warn", r.Light == LaunchLight.Yellow);
            riskLabel.EnableInClassList("c-bad", r.Light == LaunchLight.Red);
        }
        SetText(hintLabel, Loc.Get("launch.hint." + r.Light));
    }

    // ---------- Кнопка ЗАПУСК ----------

    void OnLaunchClicked()
    {
        if (animating || State == null) return;
        if (IsFirstMission)
        {
            bool ok = Supply.LaunchFirstMission(State); // розрахунок і «кубик» — у SupplySystem
            StartCoroutine(Fly(ok));
        }
        else
        {
            SetResult(Supply.Send(State) ?? "launch.order_sent");
            Refresh();
        }
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
