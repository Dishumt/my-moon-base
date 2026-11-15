// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Числа з Launch/launch.json: запуск, поставки, зв'язок.</summary>
[Serializable]
public class LaunchData
{
    public float emptyShipMassKg = 1000f;
    public float fuelPerKg = 1.5f;
    public float maxFuelKg = 6000f;
    public float greenMargin = 1.1f;
    public float redMargin = 0.9f;
    public float yellowMinChance = 0.30f;
    public float yellowMaxChance = 0.95f;
    public float personMassKg = 150f;
    public int firstMissionSeats = 4;
    public float startFuelKg = 3000f;

    public int supplyEveryDays = 4;
    public int launchHour = 8;
    public int flightHours = 12;
    public int failedDelayDays = 3;

    public int commsOpenHours = 30;
    public int commsClosedHours = 18;
    public int commsOffsetHours = 0;

    public float weatherWindyChance = 0.25f;  // шанс вітру в день запуску
    public float weatherStormChance = 0.15f;  // шанс грози
    public float windyPenalty = 0.15f;        // вітер: мінус до шансу в жовтій зоні
    public float stormMaxChance = 0.4f;       // гроза: шанс не вище цього
    public int stormDelayHours = 6;           // скільки годин чекати, поки гроза мине
    public float stormBoilOff = 0.1f;         // перша місія: яка частка палива випаровується, поки чекаємо

    public string defaultPlanId = "normal";
    public List<SupplyPlanData> supplyPlans = new List<SupplyPlanData>();

    public SupplyPlanData FindPlan(string id) => string.IsNullOrEmpty(id) ? null : supplyPlans.Find(p => p.id == id);

    public static LaunchData Load() => DataLoader.Load<LaunchData>("Launch/launch");
}

/// <summary>План поставок: як часто летить корабель і які ракети за цей час встигають підготувати.</summary>
[Serializable]
public class SupplyPlanData
{
    public string id;
    public string nameKey;
    public string hintKey;
    public int everyDays = 3;
    public List<string> rockets = new List<string>();   // порожньо — будь-яка ракета
}
