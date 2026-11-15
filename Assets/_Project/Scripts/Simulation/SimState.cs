// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Те, що системи 03 мусять пам'ятати між збереженням і завантаженням гри. Лежить у GameState.Sim.
/// Раніше ці числа жили всередині систем, тож після «Продовжити» вони скидалися:
/// «прожити N днів» починалось спочатку, урожай теплиці пропадав, а таймер голоду обнулявся.
/// Тільки поля, які JsonUtility вміє зберегти. Власник: роль 03.
/// </summary>
[Serializable]
public class SimState
{
    // --- Розділ (ChapterSystem) ---
    public int ChapterStartChapter;             // для якого розділу записано ChapterStartDay (0 = ще не стартував)
    public int ChapterStartDay;                 // з якого дня гри почався розділ
    public List<int> GoalsDone = new List<int>(); // номери цілей, які вже зараховано

    // --- Їжа (FoodSystem) ---
    public float HoursWithoutFood;              // скільки годин ніхто на базі не їв

    // --- Теплиця (GreenhouseSystem) ---
    public float GreenhouseGrowth;              // ріст від останнього врожаю
    public int GreenhouseDaysSinceHarvest;      // днів від останнього врожаю
}
