// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Екран запуску: вибір вантажу, палива, світлофор, ризик і політ ракети.
/// Працює сам по собі (без GameState). Вішається на об'єкт з UIDocument.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LaunchScreen : MonoBehaviour
{
    // --- Тимчасові числа (потім підуть у launch.json) ---
    const float EmptyShipKg = 1000f;   // маса порожнього корабля
    const float FuelFactor = 1.5f;     // потрібне паливо = 1.5 * (вся маса)
    const float PersonKg = 150f;       // маса однієї людини
    const int CrewSeats = 4;           // місця екіпажу вже зайняті
    const float GreenFrom = 1.1f;      // запас >= 1.1 -> зелений
    const float RedBelow = 0.9f;       // запас < 0.9 -> червоний
    const float ChanceAtRed = 30f;     // шанс успіху (%) при запасі 0.9
    const float ChanceAtGreen = 95f;   // шанс успіху (%) при запасі 1.1
    const float FlightSeconds = 3f;    // скільки триває політ
    const float MaxPayloadKg = 3000f;  // для смужки маси

    class Item
    {
        public string name;
        public float massKg;
        public int max;
        public int count;
        public Label countLabel;
    }

    readonly Item[] items =
    {
        new Item { name = "Water",     massKg = 100, max = 10 },
        new Item { name = "Food",      massKg = 50,  max = 10 },
        new Item { name = "Materials", massKg = 150, max = 10 },
        new Item { name = "Medkits",   massKg = 20,  max = 10 },
    };

    VisualElement ship, massCrew, massCargo, lightRed, lightYellow, lightGreen, cargoList;
    Label massLabel, fuelLabel, riskLabel, resultLabel;
    Slider fuelSlider;
    Button launchButton;

    float chancePercent;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        cargoList = root.Q<VisualElement>("cargo-list");
        ship = root.Q<VisualElement>("ship");
        massCrew = root.Q<VisualElement>("mass-crew");
        massCargo = root.Q<VisualElement>("mass-cargo");
        lightRed = root.Q<VisualElement>("light-red");
        lightYellow = root.Q<VisualElement>("light-yellow");
        lightGreen = root.Q<VisualElement>("light-green");
        massLabel = root.Q<Label>("mass-label");
        fuelLabel = root.Q<Label>("fuel-label");
        riskLabel = root.Q<Label>("risk-label");
        resultLabel = root.Q<Label>("result-label");
        fuelSlider = root.Q<Slider>("fuel-slider");
        launchButton = root.Q<Button>("launch-button");

        if (cargoList == null || ship == null || fuelSlider == null || launchButton == null)
        {
            Debug.LogError("LaunchScreen: не знайдено елементи. Перевір, що в UIDocument вказано LaunchScreen.uxml.");
            return;
        }

        BuildCargoRows();
        fuelSlider.RegisterValueChangedCallback(evt => Recalc());
        launchButton.clicked += OnLaunchClicked;
        Recalc();
    }

    void OnDisable()
    {
        if (launchButton != null) launchButton.clicked -= OnLaunchClicked;
    }

    void BuildCargoRows()
    {
        cargoList.Clear();
        foreach (var item in items)
        {
            var row = new VisualElement();
            row.AddToClassList("cargo-row");

            var nameLabel = new Label(item.name);
            nameLabel.AddToClassList("cargo-name");

            var minus = new Button(() => Change(item, -1)) { text = "-" };
            minus.AddToClassList("small-btn");

            item.countLabel = new Label(item.count.ToString());
            item.countLabel.AddToClassList("cargo-count");

            var plus = new Button(() => Change(item, +1)) { text = "+" };
            plus.AddToClassList("small-btn");

            row.Add(nameLabel);
            row.Add(minus);
            row.Add(item.countLabel);
            row.Add(plus);
            cargoList.Add(row);
        }
    }

    void Change(Item item, int delta)
    {
        item.count = Mathf.Clamp(item.count + delta, 0, item.max);
        item.countLabel.text = item.count.ToString();
        Recalc();
    }

    // Рахує масу, паливо, колір світлофора і ризик
    void Recalc()
    {
        float cargoKg = 0f;
        foreach (var item in items) cargoKg += item.count * item.massKg;

        float peopleKg = CrewSeats * PersonKg;
        float totalKg = EmptyShipKg + cargoKg + peopleKg;
        float neededFuel = FuelFactor * totalKg;
        float fuel = Mathf.Round(fuelSlider.value / 10f) * 10f;
        float reserve = fuel / neededFuel;

        string color;
        if (reserve >= GreenFrom) { color = "green"; chancePercent = 100f; }
        else if (reserve < RedBelow) { color = "red"; chancePercent = 0f; }
        else
        {
            color = "yellow";
            float k = (reserve - RedBelow) / (GreenFrom - RedBelow);
            chancePercent = Mathf.Lerp(ChanceAtRed, ChanceAtGreen, k);
        }

        massLabel.text = "Mass: " + totalKg + " kg";
        fuelLabel.text = "Fuel: " + fuel + " kg (needed: " + Mathf.Round(neededFuel) + " kg)";
        riskLabel.text = "Risk: " + Mathf.Round(100f - chancePercent) + "%";
        resultLabel.text = "";

        float crewPct = Mathf.Clamp01(peopleKg / MaxPayloadKg) * 100f;
        float cargoPct = Mathf.Clamp(cargoKg / MaxPayloadKg * 100f, 0f, 100f - crewPct);
        massCrew.style.width = Length.Percent(crewPct);
        massCargo.style.width = Length.Percent(cargoPct);

        SetLight(lightRed, color == "red", new Color(0.9f, 0.24f, 0.24f), new Color(0.3f, 0.12f, 0.12f));
        SetLight(lightYellow, color == "yellow", new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.27f, 0.1f));
        SetLight(lightGreen, color == "green", new Color(0.2f, 0.85f, 0.4f), new Color(0.1f, 0.27f, 0.14f));
    }

    void SetLight(VisualElement light, bool on, Color onColor, Color offColor)
    {
        light.style.backgroundColor = on ? onColor : offColor;
    }

    void OnLaunchClicked()
    {
        bool success = Random.value * 100f < chancePercent;
        StartCoroutine(Fly(success));
    }

    // Політ: ракета летить по дузі за FlightSeconds. Невдача: до середини і назад.
    IEnumerator Fly(bool success)
    {
        launchButton.SetEnabled(false);
        cargoList.SetEnabled(false);
        fuelSlider.SetEnabled(false);
        resultLabel.text = "Flying...";

        float elapsed = 0f;
        while (elapsed < FlightSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(elapsed / FlightSeconds);
            float t = success ? p : 0.5f * (1f - Mathf.Abs(2f * p - 1f));
            PlaceShip(t);
            yield return null;
        }

        resultLabel.text = success
            ? "Launch successful!"
            : "Not enough fuel. The rocket turned back. Try again!";

        yield return new WaitForSecondsRealtime(1.2f);

        PlaceShip(0f);
        launchButton.SetEnabled(true);
        cargoList.SetEnabled(true);
        fuelSlider.SetEnabled(true);
    }

    // Крива Безьє: старт, точка згину, фініш (у пікселях відносно старту)
    void PlaceShip(float t)
    {
        Vector2 p0 = new Vector2(0f, 0f);
        Vector2 p1 = new Vector2(180f, -220f);
        Vector2 p2 = new Vector2(60f, -520f);
        Vector2 pos = (1f - t) * (1f - t) * p0 + 2f * (1f - t) * t * p1 + t * t * p2;
        ship.style.translate = new StyleTranslate(new Translate(pos.x, pos.y));
    }
}   