// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

// Класи для Resources/Data/Balance/balance.json.
// Читати: var b = DataLoader.Load<SimBalanceData>("Balance/balance");
// Поля, що закінчуються на _note, — це пояснення для людей. У коді їх не використовуємо.

[Serializable]
public class SimBalanceData
{
    public string _ai;
    public string _note;
    public float realSecondsPerDay;
    public string realSecondsPerDay_note;
    public PowerBalance power;
    public LifeSupportBalance lifeSupport;
    public StartBalance start;
    public FoodBalance food;
    public GreenhouseBalance greenhouse;
    public RobotsBalance robots;
    public ConstructionBalance construction;
    public DefeatBalance defeat;
}

[Serializable]
public class PowerBalance
{
    public string _note;
    public float panelBlocks;
    public string panelBlocks_note;
    public float panelBlocksInShade;
    public string panelBlocksInShade_note;
    public float shadeHoursPerDay;
    public string shadeHoursPerDay_note;
    public float batteryCapacityBlockHours;
    public string batteryCapacityBlockHours_note;
}

[Serializable]
public class LifeSupportBalance
{
    public string _note;
    public float oxygenTankMax;
    public string oxygenTankMax_note;
    public float oxygenUsePerAstronautPerDay;
    public string oxygenUsePerAstronautPerDay_note;
    public float oxygenMadePerBlockPerDay;
    public string oxygenMadePerBlockPerDay_note;
    public float waterUsePerBlockPerDay;
    public string waterUsePerBlockPerDay_note;
    public float oxygenAlarmPercent;
    public string oxygenAlarmPercent_note;
    public float waterAlarmPercent;
    public string waterAlarmPercent_note;
}

[Serializable]
public class StartBalance
{
    public string _note;
    public float water;
    public string water_note;
    public float food;
    public string food_note;
}

[Serializable]
public class FoodBalance
{
    public string _note;
    public float portionsPerAstronautPerDay;
    public string portionsPerAstronautPerDay_note;
    public int mealsPerDay;
    public string mealsPerDay_note;
    public float satietyMax;
    public string satietyMax_note;
    public float daysToEmptyWithoutFood;
    public string daysToEmptyWithoutFood_note;
    public float satietyAfterMeal;
    public string satietyAfterMeal_note;
}

[Serializable]
public class GreenhouseBalance
{
    public string _note;
    public float growthPerBlockPerDay;
    public string growthPerBlockPerDay_note;
    public float harvestEveryDays;
    public string harvestEveryDays_note;
    public float portionsPerGrowth;
    public string portionsPerGrowth_note;
    public float botanistMultiplier;
    public string botanistMultiplier_note;
    public float waterUsePerBlockPerDay;
    public string waterUsePerBlockPerDay_note;
}

[Serializable]
public class RobotsBalance
{
    public string _note;
    public int count;
    public string count_note;
    public float powerBlocksWhenWorking;
    public string powerBlocksWhenWorking_note;
}

[Serializable]
public class ConstructionBalance
{
    public string _note;
    public float engineerNearbySpeedMultiplier;
    public string engineerNearbySpeedMultiplier_note;
}

[Serializable]
public class DefeatBalance
{
    public string _note;
    public float oxygenAtOrBelow;
    public string oxygenAtOrBelow_note;
    public float daysWithoutFood;
    public string daysWithoutFood_note;
}
