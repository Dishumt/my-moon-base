// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Їжа й ситість (роль 03).
///  - Ситість кожного астронавта падає з часом: з 100 до 0 за 2 доби без їжі.
///  - Коли ситість впала настільки, що минуло «одне прийомування їжі» (8 годин при 3 прийомах на день),
///    астронавт їсть третину порції з запасів бази, і ситість знову 100. Виходить тричі на день.
///  - Якщо їжі немає, ситість падає далі й не йде нижче 0. Це підвищує шанс захворіти (хвороби — роль 06).
///  - HoursWithoutFood — скільки годин ніхто на базі не їв. ChapterSystem (Задача 5) програє гру,
///    коли воно досягне 2 діб (48 годин).
/// Усередині Tick ділиться на кроки по годині, тому великий Tick (наприклад, 24 години) теж дає три прийоми їжі.
/// </summary>
public class FoodSystem : ISimSystem
{
    private const float Eps = 0.001f;

    private readonly FoodBalance _b;

    /// <summary>Скільки годин підряд ніхто на базі не їв (скидається, щойно хтось поїв).</summary>
    public float HoursWithoutFood { get; private set; }

    private float _eatenToday;
    private readonly HashSet<string> _reportedToday = new HashSet<string>();

    /// <summary>Гра: читає числа з Resources/Data/Balance/balance.json.</summary>
    public FoodSystem() : this(DataLoader.Load<SimBalanceData>("Balance/balance")) { }

    /// <summary>Тести: числа передаємо руками.</summary>
    public FoodSystem(SimBalanceData balance)
    {
        _b = balance.food;
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        float left = hours;
        while (left > Eps)
        {
            float step = Math.Min(1f, left);
            Step(s, step, r);
            left -= step;
        }
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        if (_eatenToday > 0.01f) r.Add("report.food_eaten", -_eatenToday);
        _eatenToday = 0f;
        _reportedToday.Clear();
    }

    // ───────────────────────── Усередині ─────────────────────────

    private void Step(GameState s, float hours, DayReport r)
    {
        if (s.Crew.Count == 0) return;

        // На скільки падає ситість за цей крок.
        float decayPerHour = _b.satietyMax / (_b.daysToEmptyWithoutFood * 24f);
        float decay = decayPerHour * hours;

        // Скільки порції їсть астронавт за один прийом (3 прийоми = 1 порція на день).
        float meal = _b.portionsPerAstronautPerDay / _b.mealsPerDay;
        float afterMeal = Math.Min(_b.satietyAfterMeal, _b.satietyMax);

        // Коли ситість впала до цього рівня, настав час їсти (раз на 24 / mealsPerDay годин).
        float hoursBetweenMeals = 24f / _b.mealsPerDay;
        float eatBelow = afterMeal - hoursBetweenMeals * decayPerHour;

        // Якщо їжі мало, першим їсть той, хто найголодніший.
        var order = new List<Astronaut>(s.Crew);
        order.Sort((x, y) => x.Satiety.CompareTo(y.Satiety));

        bool anyoneAte = false;

        foreach (Astronaut a in order)
        {
            a.Satiety = Math.Max(0f, a.Satiety - decay);

            if (a.Satiety > eatBelow + Eps) continue; // ще не голодний

            if (s.Res.Food >= meal - Eps)
            {
                s.Res.Food = Math.Max(0f, s.Res.Food - meal);
                a.Satiety = afterMeal;
                _eatenToday += meal;
                anyoneAte = true;
            }
            else
            {
                // Хотів їсти, а їжі немає.
                AddOnce(r, "report.food_empty", "");
                if (a.Satiety < _b.satietyMax * 0.5f)
                    AddOnce(r, "report.hungry", a.Name);
            }
        }

        if (anyoneAte) HoursWithoutFood = 0f;
        else HoursWithoutFood += hours;
    }

    // Кожну тривогу пишемо раз на день (для «голодний» — раз на день на кожного астронавта).
    private void AddOnce(DayReport r, string key, string arg)
    {
        if (!_reportedToday.Add(key + "|" + arg)) return;
        r.Add(key, 0f, arg, important: true);
    }
}
