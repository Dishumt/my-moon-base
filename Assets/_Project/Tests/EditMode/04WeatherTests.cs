// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести погоди в день запуску (роль 04): вітер, гроза, очікування, затримка поставки.</summary>
public class WeatherTests
{
    SupplySystem sup;
    GameState s;
    DayReport r;
    LaunchData data;
    float dice;

    [SetUp]
    public void Setup()
    {
        data = new LaunchData { supplyEveryDays = 4, launchHour = 8, flightHours = 12, failedDelayDays = 3,
                                commsOpenHours = 30, commsClosedHours = 18,
                                weatherStormChance = 0.15f, weatherWindyChance = 0.25f,
                                windyPenalty = 0.15f, stormMaxChance = 0.4f, stormDelayHours = 6, stormBoilOff = 0.1f };
        var cat = new CargoCatalog();
        cat.items.Add(new CargoItemData { id = "water", massKg = 100f, water = 20f });
        dice = 0.5f;
        sup = new SupplySystem(data, cat) { Random01 = () => dice };
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
        r = new DayReport(1);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void RunHours(int n)
    {
        for (int i = 0; i < n; i++)
        {
            sup.Tick(s, 1f, r);
            s.Hour++;
            if (s.Hour >= 24) { s.Hour = 0; s.Day++; }
        }
    }

    static LaunchResult Result(LaunchLight light, float chance) => new LaunchResult { Light = light, SuccessChance = chance };

    [Test]
    public void Windy_LowersChanceOnlyOnYellow()
    {
        var yellow = Result(LaunchLight.Yellow, 0.6f);
        LaunchCalculator.ApplyWeather(yellow, LaunchWeather.Windy, data);
        Assert.AreEqual(0.45f, yellow.SuccessChance, 0.001f);

        var green = Result(LaunchLight.Green, 1f);
        LaunchCalculator.ApplyWeather(green, LaunchWeather.Windy, data);
        Assert.AreEqual(1f, green.SuccessChance, 0.001f);   // із запасом палива вітер не страшний
    }

    [Test]
    public void Storm_CapsChanceEvenOnGreen()
    {
        var green = Result(LaunchLight.Green, 1f);
        LaunchCalculator.ApplyWeather(green, LaunchWeather.Storm, data);
        Assert.AreEqual(0.4f, green.SuccessChance, 0.001f);
        Assert.IsFalse(LaunchCalculator.Roll(green, () => 0.5f));   // кубик 0.5 > 0.4 — блискавка
        Assert.IsTrue(LaunchCalculator.Roll(green, () => 0.2f));

        var red = Result(LaunchLight.Red, 0f);
        LaunchCalculator.ApplyWeather(red, LaunchWeather.Storm, data);
        Assert.AreEqual(0f, red.SuccessChance, 0.001f);
    }

    [Test]
    public void Clear_GreenAlwaysFlies()
    {
        var green = Result(LaunchLight.Green, 1f);
        LaunchCalculator.ApplyWeather(green, LaunchWeather.Clear, data);
        Assert.IsTrue(LaunchCalculator.Roll(green, () => 0.999f));
    }

    [Test]
    public void RollWeather_UsesChances()
    {
        dice = 0.1f; Assert.AreEqual(LaunchWeather.Storm, sup.RollWeather(s));
        dice = 0.2f; Assert.AreEqual(LaunchWeather.Windy, sup.RollWeather(s));
        dice = 0.5f; Assert.AreEqual(LaunchWeather.Clear, sup.RollWeather(s));
        dice = 0.1f; Assert.AreEqual(LaunchWeather.Windy, sup.RollWeather(s, false)); // гроза вже минула
        Assert.IsTrue(s.Supply.WeatherKnown);
    }

    [Test]
    public void WaitOutStorm_BoilsFuelAndEndsStorm()
    {
        sup.SetFuel(s, 5000f);
        s.Supply.Weather = LaunchWeather.Storm;
        Assert.IsNull(sup.WaitOutStorm(s));
        Assert.AreEqual(4500f, s.Supply.FuelKg, 0.01f);
        Assert.AreNotEqual(LaunchWeather.Storm, s.Supply.Weather);
    }

    [Test]
    public void WaitOutStorm_NoStorm_ReturnsError()
    {
        s.Supply.Weather = LaunchWeather.Clear;
        Assert.AreEqual("error.no_storm", sup.WaitOutStorm(s));
    }

    [Test]
    public void FirstMission_StormOnGreen_CanFail()
    {
        sup.ChangeCargo(s, "water", 1);
        sup.SetFuel(s, 6000f);
        s.Supply.Weather = LaunchWeather.Storm;
        dice = 0.9f;
        Assert.IsFalse(sup.LaunchFirstMission(s));
        Assert.IsFalse(s.Supply.FirstMissionDone);
    }

    [Test]
    public void Supply_StormDelaysLaunchOnce()
    {
        sup.SetFuel(s, 6000f);
        Assert.IsTrue(sup.LaunchFirstMission(s));
        Assert.IsNull(sup.Send(s));
        int launchAt = s.Supply.NextLaunchAt;

        dice = 0.1f;   // гроза на Землі
        RunHours(launchAt + 1);
        Assert.AreEqual(SupplyStatus.Sent, s.Supply.Status);
        Assert.AreEqual(launchAt + data.stormDelayHours, s.Supply.NextLaunchAt);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.supply_storm"));

        // за 6 годин знову гроза, але вдруге не відкладаємо: летимо (вітер, зелений — успіх)
        RunHours(data.stormDelayHours);
        Assert.AreEqual(SupplyStatus.Launched, s.Supply.Status);
    }
}
