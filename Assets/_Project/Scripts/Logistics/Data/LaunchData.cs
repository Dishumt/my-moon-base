// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

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

    public static LaunchData Load() => DataLoader.Load<LaunchData>("Launch/launch");
}
