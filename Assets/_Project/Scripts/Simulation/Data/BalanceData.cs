// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Числа балансу з Resources/Data/Balance/balance.json (власник: роль 03).
/// Читаємо так: var b = DataLoader.Load&lt;BalanceData&gt;("Balance/balance");
/// Назви полів = ключі в JSON. Поля "_note" не потрібні: JsonUtility їх просто пропускає.
/// Значення за замовчуванням тут — запасні, справжні числа завжди беремо з JSON.
/// </summary>
[Serializable]
public class BalanceData
{
    public float realSecondsPerDay = 180f;
    public PowerBalance power = new PowerBalance();
    public LifeSupportBalance lifeSupport = new LifeSupportBalance();
    public StartBalance start = new StartBalance();
    public FoodBalance food = new FoodBalance();
    public GreenhouseBalance greenhouse = new GreenhouseBalance();
    public RobotsBalance robots = new RobotsBalance();
    public ConstructionBalance construction = new ConstructionBalance();
    public DefeatBalance defeat = new DefeatBalance();
}

/// <summary>Енергія (блоки потужності).</summary>
[Serializable]
public class PowerBalance
{
    public float panelBlocks;
    public float panelBlocksInShade;
    public float shadeHoursPerDay;
    public float batteryCapacityBlockHours;
}

/// <summary>Життєзабезпечення: кисень із води та енергії.</summary>
[Serializable]
public class LifeSupportBalance
{
    public float oxygenTankMax;
    public float oxygenUsePerAstronautPerDay;
    public float oxygenMadePerBlockPerDay;
    public float waterUsePerBlockPerDay;
    public float oxygenAlarmPercent;
    public float waterAlarmPercent;
}

/// <summary>Стартові запаси.</summary>
[Serializable]
public class StartBalance
{
    public float water;
    public float food;
}

/// <summary>Їжа й ситість.</summary>
[Serializable]
public class FoodBalance
{
    public float portionsPerAstronautPerDay;
    public int mealsPerDay;
    public float satietyMax;
    public float daysToEmptyWithoutFood;
    public float satietyAfterMeal;
}

/// <summary>Теплиця.</summary>
[Serializable]
public class GreenhouseBalance
{
    public float growthPerBlockPerDay;
    public float harvestEveryDays;
    public float portionsPerGrowth;
    public float botanistMultiplier;
    public float waterUsePerBlockPerDay;
}

/// <summary>Роботи.</summary>
[Serializable]
public class RobotsBalance
{
    public int count;
    public float powerBlocksWhenWorking;
}

/// <summary>Будівництво.</summary>
[Serializable]
public class ConstructionBalance
{
    public float engineerNearbySpeedMultiplier;
}

/// <summary>Поразка.</summary>
[Serializable]
public class DefeatBalance
{
    public float oxygenAtOrBelow;
    public float daysWithoutFood;
}
