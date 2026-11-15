// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Колір світлофора запуску.</summary>
public enum LaunchLight { Green, Yellow, Red }

/// <summary>Погода на космодромі в день запуску. Додавати значення тільки в кінець.</summary>
public enum LaunchWeather { Clear, Windy, Storm }

/// <summary>Результат розрахунку: усе, що показує екран запуску.</summary>
public class LaunchResult
{
    public float CargoKg;        // вантаж
    public float PeopleKg;       // люди (по 150 кг)
    public float TotalMassKg;    // порожній корабель + вантаж + люди
    public float FuelKg;         // скільки палива залили
    public float FuelNeededKg;   // скільки палива треба
    public float Margin;         // запас = паливо / потрібне паливо
    public LaunchLight Light;
    public float SuccessChance;  // 0..1

    public int SuccessPercent => (int)Math.Round(SuccessChance * 100f);
    public int RiskPercent => 100 - SuccessPercent;
}

/// <summary>
/// Розрахунок запуску. Звичайний C#-клас, тому його легко тестувати.
/// Формула: потрібне паливо = 1.5 × (1000 + вантаж + люди). Запас = паливо / потрібне.
/// </summary>
public class LaunchCalculator
{
    readonly LaunchData data;

    public LaunchCalculator(LaunchData data) { this.data = data ?? new LaunchData(); }

    public LaunchData Data => data;

    /// <summary>Порахувати за масою вантажу, кількістю людей і паливом (ракета з launch.json).</summary>
    public LaunchResult Evaluate(float cargoKg, int people, float fuelKg)
    {
        return Evaluate(cargoKg, people, fuelKg, null);
    }

    /// <summary>Те саме для вибраної ракети: її маса і бак замість чисел з launch.json. rocket = null — як без ракети.</summary>
    public LaunchResult Evaluate(float cargoKg, int people, float fuelKg, RocketData rocket)
    {
        float emptyKg = rocket != null ? rocket.emptyMassKg : data.emptyShipMassKg;
        float tankKg = rocket != null ? rocket.maxFuelKg : data.maxFuelKg;

        var r = new LaunchResult();
        r.CargoKg = Math.Max(0f, cargoKg);
        r.PeopleKg = Math.Max(0, people) * data.personMassKg;
        r.TotalMassKg = emptyKg + r.CargoKg + r.PeopleKg;
        r.FuelKg = Math.Max(0f, Math.Min(fuelKg, tankKg));
        r.FuelNeededKg = data.fuelPerKg * r.TotalMassKg;
        r.Margin = r.FuelNeededKg > 0f ? r.FuelKg / r.FuelNeededKg : 0f;

        // невеликий допуск, щоб 1.1 рахувалось як 1.1, а не 1.0999999
        const float eps = 0.0001f;
        if (r.Margin >= data.greenMargin - eps)
        {
            r.Light = LaunchLight.Green;
            r.SuccessChance = 1f;
        }
        else if (r.Margin < data.redMargin - eps)
        {
            r.Light = LaunchLight.Red;
            r.SuccessChance = 0f;
        }
        else
        {
            r.Light = LaunchLight.Yellow;
            float t = (r.Margin - data.redMargin) / (data.greenMargin - data.redMargin);
            t = Math.Max(0f, Math.Min(1f, t));
            r.SuccessChance = data.yellowMinChance + (data.yellowMaxChance - data.yellowMinChance) * t;
        }
        return r;
    }

    /// <summary>Порахувати за списком вантажу з каталогу.</summary>
    public LaunchResult Evaluate(List<CargoLine> cargo, CargoCatalog catalog, int people, float fuelKg)
    {
        return Evaluate(CargoMass(cargo, catalog), people, fuelKg, null);
    }

    /// <summary>Порахувати за списком вантажу для вибраної ракети.</summary>
    public LaunchResult Evaluate(List<CargoLine> cargo, CargoCatalog catalog, int people, float fuelKg, RocketData rocket)
    {
        return Evaluate(CargoMass(cargo, catalog), people, fuelKg, rocket);
    }

    /// <summary>Маса всього вантажу в кг.</summary>
    public static float CargoMass(List<CargoLine> cargo, CargoCatalog catalog)
    {
        float kg = 0f;
        if (cargo == null || catalog == null) return 0f;
        foreach (var line in cargo)
        {
            var item = catalog.Find(line.itemId);
            if (item != null) kg += item.massKg * line.count;
        }
        return kg;
    }

    /// <summary>
    /// Погода змінює шанс успіху (світлофор палива лишається тим самим):
    /// вітер — у жовтій зоні шанс менший на windyPenalty (із запасом палива вітер не страшний);
    /// гроза — шанс не вище stormMaxChance навіть на зеленому (блискавка може влучити в ракету).
    /// </summary>
    public static void ApplyWeather(LaunchResult r, LaunchWeather weather, LaunchData data)
    {
        if (r == null || data == null) return;
        if (weather == LaunchWeather.Windy && r.Light == LaunchLight.Yellow)
            r.SuccessChance = Math.Max(0f, r.SuccessChance - data.windyPenalty);
        else if (weather == LaunchWeather.Storm && r.Light != LaunchLight.Red)
            r.SuccessChance = Math.Min(r.SuccessChance, data.stormMaxChance);
    }

    /// <summary>
    /// Вирішити, чи політ вдалий. random01 — «кубик» від 0 до 1.
    /// У тестах підставляємо своє число, у грі — UnityEngine.Random.value.
    /// </summary>
    public static bool Roll(LaunchResult r, Func<float> random01)
    {
        if (r.Light == LaunchLight.Red || r.SuccessChance <= 0f) return false;
        if (r.SuccessChance >= 1f) return true;   // зелений і без грози — точно вдалий
        return random01() < r.SuccessChance;
    }

    /// <summary>Те саме з System.Random.</summary>
    public static bool Roll(LaunchResult r, Random random)
    {
        return Roll(r, () => (float)random.NextDouble());
    }
}
