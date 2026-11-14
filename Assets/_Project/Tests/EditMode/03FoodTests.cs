// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Тести їжі й ситості. Числа тут свої, не з balance.json:
/// порція на день 1, прийомів їжі 3 (тобто 1/3 порції за раз),
/// ситість 100 → 0 за 2 доби без їжі (≈ 2.08 за годину).
/// </summary>
public class FoodSystemTests
{
    private static SimBalanceData Balance()
    {
        return new SimBalanceData
        {
            food = new FoodBalance
            {
                portionsPerAstronautPerDay = 1f,
                mealsPerDay = 3,
                satietyMax = 100f,
                daysToEmptyWithoutFood = 2f,
                satietyAfterMeal = 100f
            }
        };
    }

    private static GameState State(int crew, float food)
    {
        var s = new GameState();
        s.Res.Food = food;
        for (int i = 0; i < crew; i++)
            s.Crew.Add(new Astronaut { Name = "A" + i });
        return s;
    }

    [Test]
    public void EatsThreeTimesADay_FourCrewUseOnePortionEachPerDay()
    {
        var s = State(4, 40f);
        var sys = new FoodSystem(Balance());

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(36f, s.Res.Food, 0.01f); // 4 людини × 1 порція
        foreach (var a in s.Crew)
            Assert.AreEqual(100f, a.Satiety, 0.01f);
    }

    [Test]
    public void NoFood_SatietyKeepsFalling_AndHoursAreCounted()
    {
        var s = State(4, 0f);
        var sys = new FoodSystem(Balance());
        var r = new DayReport(1);

        sys.Tick(s, 24f, r);

        Assert.AreEqual(50f, s.Crew[0].Satiety, 0.1f);
        Assert.AreEqual(24f, sys.HoursWithoutFood, 0.01f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.food_empty" && l.Important));
    }

    [Test]
    public void TwoDaysWithoutFood_SatietyIsZero_NeverNegative()
    {
        var s = State(2, 0f);
        var sys = new FoodSystem(Balance());

        sys.Tick(s, 60f, new DayReport(1)); // більше ніж дві доби

        Assert.AreEqual(0f, s.Crew[0].Satiety, 0.01f);
        Assert.GreaterOrEqual(s.Crew[0].Satiety, 0f);
        Assert.AreEqual(60f, sys.HoursWithoutFood, 0.01f);
    }

    [Test]
    public void HungryAstronaut_EatsAndSatietyIsFull()
    {
        var s = State(1, 5f);
        s.Crew[0].Satiety = 40f;
        var sys = new FoodSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(100f, s.Crew[0].Satiety, 0.01f);
        Assert.AreEqual(5f - 1f / 3f, s.Res.Food, 0.01f);
    }

    [Test]
    public void HoursWithoutFood_ResetsWhenFoodReturns()
    {
        var s = State(1, 0f);
        var sys = new FoodSystem(Balance());
        var r = new DayReport(1);

        sys.Tick(s, 10f, r);
        Assert.AreEqual(10f, sys.HoursWithoutFood, 0.01f);

        s.Res.Food = 5f; // приїхала поставка
        sys.Tick(s, 1f, r);

        Assert.AreEqual(0f, sys.HoursWithoutFood, 0.01f);
        Assert.AreEqual(100f, s.Crew[0].Satiety, 0.01f);
    }

    [Test]
    public void ScarceFood_HungriestEatsFirst()
    {
        var s = State(2, 0.34f); // вистачить лише на один прийом (1/3 порції)
        s.Crew[0].Satiety = 60f;
        s.Crew[1].Satiety = 30f;
        var sys = new FoodSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(60f - 100f / 48f, s.Crew[0].Satiety, 0.05f); // не поїв, тільки схуд
        Assert.AreEqual(100f, s.Crew[1].Satiety, 0.01f);             // голодніший поїв
    }

    [Test]
    public void DayEnd_ReportsEatenFood()
    {
        var s = State(4, 40f);
        var sys = new FoodSystem(Balance());
        var r = new DayReport(1);

        sys.Tick(s, 24f, r);
        sys.OnDayEnd(s, r);

        var line = r.Lines.Find(l => l.Key == "report.food_eaten");
        Assert.IsNotNull(line);
        Assert.AreEqual(-4f, line.Delta, 0.01f);
    }
}
