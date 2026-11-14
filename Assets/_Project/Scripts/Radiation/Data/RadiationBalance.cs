// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Числа балансу радіації й здоров'я з Resources/Data/Nasa/radiation_balance.json (роль 06).
/// Доза — у % від ліміту NASA на всю кар'єру.
/// </summary>
[Serializable]
public class RadiationBalance
{
    public int doseFromChapter = 1;        // з якого розділу набирається доза
    public int illnessFromChapter = 3;     // з якого розділу можна захворіти
    public float timeCompression = 50f;    // у скільки разів гра стискає час для дози
    public float outsideMultiplier = 4f;
    public float insideMultiplier = 1f;
    public float regolithMultiplier = 0.5f;
    public float yellowPercent = 50f;
    public float redPercent = 100f;
    public StormBalance storm = new StormBalance();
    public HealthBalance health = new HealthBalance();

    public static RadiationBalance Load() => DataLoader.Load<RadiationBalance>("Nasa/radiation_balance");
}

[Serializable]
public class StormBalance
{
    public int warningHours = 6;
    public int minWarningHours = 2;
    public List<int> durationHours = new List<int> { 4, 6, 8 };
    public List<int> shelterPowerNeeded = new List<int> { 1, 2, 2 };
    public List<float> outsidePerHour = new List<float> { 3f, 5f, 8f };
    public List<float> modulePerHour = new List<float> { 0.8f, 1.5f, 2.5f };
    public List<float> shelterPerHour = new List<float> { 0.1f, 0.2f, 0.3f };

    /// <summary>Значення зі списку за силою 1..3 (зайве обрізається).</summary>
    public static T ByStrength<T>(List<T> list, int strength)
    {
        if (list == null || list.Count == 0) return default;
        int i = Math.Max(0, Math.Min(list.Count - 1, strength - 1));
        return list[i];
    }
}

[Serializable]
public class HealthBalance
{
    public float sickChancePerHour = 0.004f;
    public float riskMultiplier = 2f;
    public float hungrySatietyBelow = 25f;
    public float recoverHours = 6f;
    public float severeRecoverHours = 12f;
    public float sickToSevereHours = 24f;
}
