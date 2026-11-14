// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Тести кисню й води. Числа тут свої, не з balance.json:
/// бак 100, астронавт дихає 4/день, блок робить 4 кисню/день і п'є 2 води/день,
/// тривога: кисень менше 25%, вода менше 20% від стартових 60.
/// Тик на 24 години = одна ціла доба, так легко рахувати.
/// </summary>
public class LifeSupportSystemTests
{
    private static SimBalanceData Balance()
    {
        return new SimBalanceData
        {
            power = new PowerBalance
            {
                panelBlocks = 3f, panelBlocksInShade = 1f,
                shadeHoursPerDay = 6f, batteryCapacityBlockHours = 10f
            },
            lifeSupport = new LifeSupportBalance
            {
                oxygenTankMax = 100f,
                oxygenUsePerAstronautPerDay = 4f,
                oxygenMadePerBlockPerDay = 4f,
                waterUsePerBlockPerDay = 2f,
                oxygenAlarmPercent = 25f,
                waterAlarmPercent = 20f
            },
            start = new StartBalance { water = 60f, food = 40f }
        };
    }

    private static GameState State(int crew, float oxygen, float water)
    {
        var s = new GameState();
        s.Res.Oxygen = oxygen;
        s.Res.Water = water;
        for (int i = 0; i < crew; i++)
            s.Crew.Add(new Astronaut { Name = "A" + i });
        return s;
    }

    // Створює систему й одразу «дає» їй блоки енергії (замість справжньої PowerSystem).
    private static LifeSupportSystem Make(int grantedBlocks)
    {
        var power = new PowerSystem(Balance());
        power.Granted.LifeSupport = grantedBlocks;
        return new LifeSupportSystem(power, Balance());
    }

    [Test]
    public void FourBlocksFourCrew_OxygenStaysEqual_WaterGoesDown()
    {
        var s = State(4, 50f, 60f);
        var sys = Make(4);

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(50f, s.Res.Oxygen, 0.01f); // 16 зроблено, 16 вдихнуто
        Assert.AreEqual(52f, s.Res.Water, 0.01f);  // 4 блоки × 2 води
    }

    [Test]
    public void NoWater_NoOxygenIsMade()
    {
        var s = State(4, 50f, 0f);
        var sys = Make(4);

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(34f, s.Res.Oxygen, 0.01f); // тільки витрата екіпажу
        Assert.AreEqual(0f, s.Res.Water, 0.001f);
    }

    [Test]
    public void LittleWater_OnlyAsManyBlocksAsWaterAllows()
    {
        // Води на 2 блоки (2 × 2 = 4), а блоків 4 → робить кисень лише половина.
        var s = State(4, 50f, 4f);
        var sys = Make(4);

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(42f, s.Res.Oxygen, 0.01f); // +8 зроблено, −16 вдихнуто
        Assert.AreEqual(0f, s.Res.Water, 0.01f);
    }

    [Test]
    public void FullTank_StaysFull_AndWaterIsNotWasted()
    {
        var s = State(0, 99f, 60f);
        var sys = Make(4);

        sys.Tick(s, 24f, new DayReport(1));

        Assert.AreEqual(100f, s.Res.Oxygen, 0.001f);
        Assert.AreEqual(59.5f, s.Res.Water, 0.01f); // пішла вода тільки на 1 кисню
    }

    [Test]
    public void Oxygen_NeverGoesBelowZero_AndEmptyIsReported()
    {
        var s = State(4, 5f, 60f);
        var sys = Make(0);
        var r = new DayReport(1);

        sys.Tick(s, 24f, r);

        Assert.AreEqual(0f, s.Res.Oxygen, 0.001f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.oxygen_empty" && l.Important));
    }

    [Test]
    public void LowOxygen_NoPower_AlarmWithReason()
    {
        var s = State(4, 20f, 60f);
        var sys = Make(0);
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);

        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.oxygen_low.no_power" && l.Important));
    }

    [Test]
    public void LowOxygen_NoWater_AlarmWithReason()
    {
        var s = State(4, 20f, 0f);
        var sys = Make(4);
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);

        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.oxygen_low.no_water"));
    }

    [Test]
    public void LowOxygen_TooFewBlocks_AlarmWithReason()
    {
        var s = State(4, 20f, 60f);
        var sys = Make(1); // 1 блок робить менше, ніж дихають 4 людини
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);

        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.oxygen_low.crew_uses_more"));
    }

    [Test]
    public void LowWater_Alarm()
    {
        var s = State(4, 80f, 10f); // 20% від 60 = 12, а тут 10
        var sys = Make(0);
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);

        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.water_low" && l.Important));
    }

    [Test]
    public void SameAlarm_IsWrittenOnlyOncePerDay()
    {
        var s = State(4, 20f, 60f);
        var sys = Make(0);
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);
        sys.Tick(s, 1f, r);
        sys.Tick(s, 1f, r);

        Assert.AreEqual(1, r.Lines.FindAll(l => l.Key == "report.oxygen_low.no_power").Count);
    }

    [Test]
    public void DayEnd_ExplainsMadeAndUsed()
    {
        var s = State(4, 50f, 60f);
        var sys = Make(4);
        var r = new DayReport(1);

        sys.Tick(s, 24f, r);
        sys.OnDayEnd(s, r);

        Assert.AreEqual(16f, r.Lines.Find(l => l.Key == "report.oxygen_made").Delta, 0.01f);
        Assert.AreEqual(-16f, r.Lines.Find(l => l.Key == "report.oxygen_used").Delta, 0.01f);
        Assert.AreEqual(-8f, r.Lines.Find(l => l.Key == "report.water_used_lifesupport").Delta, 0.01f);
    }
}
