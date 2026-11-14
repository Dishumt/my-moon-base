// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести розрахунку запуску (роль 04).</summary>
public class LaunchTests
{
    LaunchCalculator calc;

    [SetUp]
    public void Setup() => calc = new LaunchCalculator(new LaunchData());

    // приклад: 2000 кг вантажу, 0 людей → маса 3000, треба 4500 палива
    [Test]
    public void Formula_2000kgCargo()
    {
        var r = calc.Evaluate(2000f, 0, 4500f);
        Assert.AreEqual(3000f, r.TotalMassKg, 0.01f);
        Assert.AreEqual(4500f, r.FuelNeededKg, 0.01f);
        Assert.AreEqual(1f, r.Margin, 0.0001f);
    }

    [Test]
    public void PeopleWeigh150kg()
    {
        var r = calc.Evaluate(0f, 4, 0f);
        Assert.AreEqual(600f, r.PeopleKg, 0.01f);
        Assert.AreEqual(1.5f * 1600f, r.FuelNeededKg, 0.01f);
    }

    [Test]
    public void Green_WhenMarginAtLeast1_1()
    {
        var r = calc.Evaluate(2000f, 0, 4500f * 1.1f); // запас рівно 1.1
        Assert.AreEqual(LaunchLight.Green, r.Light);
        Assert.AreEqual(100, r.SuccessPercent);
    }

    [Test]
    public void Yellow_JustBelow1_1()
    {
        var r = calc.Evaluate(2000f, 0, 4500f * 1.09f);
        Assert.AreEqual(LaunchLight.Yellow, r.Light);
        Assert.Less(r.SuccessChance, 0.95f);
    }

    [Test]
    public void Yellow_AtExactly0_9_Chance30()
    {
        var r = calc.Evaluate(2000f, 0, 4500f * 0.9f);
        Assert.AreEqual(LaunchLight.Yellow, r.Light);
        Assert.AreEqual(0.30f, r.SuccessChance, 0.001f);
        Assert.AreEqual(70, r.RiskPercent);
    }

    [Test]
    public void Yellow_Middle_ChanceInBetween()
    {
        var r = calc.Evaluate(2000f, 0, 4500f); // запас 1.0 → посередині
        Assert.AreEqual(0.625f, r.SuccessChance, 0.001f);
    }

    [Test]
    public void Red_Below0_9()
    {
        var r = calc.Evaluate(2000f, 0, 4500f * 0.89f);
        Assert.AreEqual(LaunchLight.Red, r.Light);
        Assert.AreEqual(0, r.SuccessPercent);
    }

    [Test]
    public void FuelCappedByMaxTank()
    {
        var r = calc.Evaluate(0f, 0, 99999f);
        Assert.AreEqual(6000f, r.FuelKg, 0.01f);
    }

    [Test]
    public void Roll_GreenAlwaysWins_RedAlwaysFails()
    {
        Assert.IsTrue(LaunchCalculator.Roll(calc.Evaluate(0f, 0, 6000f), () => 0.999f));
        Assert.IsFalse(LaunchCalculator.Roll(calc.Evaluate(5000f, 0, 100f), () => 0f));
    }

    [Test]
    public void Roll_YellowUsesRandom()
    {
        var r = calc.Evaluate(2000f, 0, 4500f); // шанс 62.5%
        Assert.IsTrue(LaunchCalculator.Roll(r, () => 0.5f));
        Assert.IsFalse(LaunchCalculator.Roll(r, () => 0.7f));
    }

    [Test]
    public void CargoMass_FromCatalog()
    {
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f });
        var lines = new System.Collections.Generic.List<CargoLine> { new CargoLine { itemId = "water", count = 3 } };
        Assert.AreEqual(300f, LaunchCalculator.CargoMass(lines, cat), 0.01f);
    }
}
