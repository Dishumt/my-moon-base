// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Тести енергії. Числа в тестах задаємо тут, щоб не залежати від balance.json:
/// панель = 3 блоки, у тіні 1, тінь 6 годин (з 18:00), батарея до 10.
/// </summary>
public class PowerSystemTests
{
    private const int SunHour = 12;   // день, тіні немає
    private const int ShadeHour = 20; // вечір, тінь

    private static SimBalanceData Balance()
    {
        return new SimBalanceData
        {
            power = new PowerBalance
            {
                panelBlocks = 3f,
                panelBlocksInShade = 1f,
                shadeHoursPerDay = 6f,
                batteryCapacityBlockHours = 10f
            }
        };
    }

    private static GameState State(int panels, int hour, float battery)
    {
        var s = new GameState { Hour = hour };
        s.Res.Battery = battery;
        for (int i = 0; i < panels; i++)
            s.Modules.Add(new ModuleState { Type = ModuleType.Solar, SlotId = i, BuildHoursLeft = 0f });
        return s;
    }

    [Test]
    public void Sun_SurplusChargesBattery()
    {
        // 2 панелі = 6 блоків, гравець роздав 4 → 2 зайві блоки за годину.
        var s = State(2, SunHour, 3f);
        s.Power.LifeSupport = 4;
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(5f, s.Res.Battery, 0.001f);
        Assert.AreEqual(4, sys.Granted.LifeSupport);
    }

    [Test]
    public void Shade_BatteryCoversTheGap()
    {
        // У тіні 2 панелі = 2 блоки, а треба 4 → 2 блоки беремо з батареї.
        var s = State(2, ShadeHour, 5f);
        s.Power.LifeSupport = 4;
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.IsTrue(sys.LastInShade);
        Assert.AreEqual(3f, s.Res.Battery, 0.001f);
        Assert.AreEqual(4, sys.Granted.LifeSupport);
    }

    [Test]
    public void FullBattery_DoesNotOvercharge()
    {
        // 4 панелі = 12 блоків, нічого не роздано; батарея не може бути більше 10.
        var s = State(4, SunHour, 9.5f);
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(10f, s.Res.Battery, 0.001f);
    }

    [Test]
    public void BatteryModule_RaisesCapacity()
    {
        var s = State(4, SunHour, 5f);
        s.Modules.Add(new ModuleState { Type = ModuleType.Battery, SlotId = 9, BuildHoursLeft = 0f });
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        // 5 + 12 = 17, а ліміт тепер 20 (база + один модуль «батарея»).
        Assert.AreEqual(17f, s.Res.Battery, 0.001f);
    }

    [Test]
    public void EmptyBattery_ShortageFollowsPriorityAndIsReported()
    {
        // У тіні 2 блоки, батареї немає, просять 3 → зв'язку не вистачає.
        var s = State(2, ShadeHour, 0f);
        s.Power.LifeSupport = 1;
        s.Power.Shelter = 1;
        s.Power.Comms = 1;
        var sys = new PowerSystem(Balance());
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);

        Assert.AreEqual(1, sys.Granted.LifeSupport);
        Assert.AreEqual(1, sys.Granted.Shelter);
        Assert.AreEqual(0, sys.Granted.Comms);
        Assert.AreEqual(0f, s.Res.Battery, 0.001f);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.power_short.comms" && l.Important));
    }

    [Test]
    public void PlayerGivesTooMuch_NeverMoreThanExists()
    {
        // 1 панель = 3 блоки, батареї немає, гравець роздав 12.
        var s = State(1, SunHour, 0f);
        s.Power.LifeSupport = 4;
        s.Power.Greenhouse = 4;
        s.Power.Lab = 4;
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(3, sys.Granted.Total());
        Assert.AreEqual(3, sys.Granted.LifeSupport); // життєзабезпечення першим
        Assert.AreEqual(0, sys.Granted.Lab);
    }

    [Test]
    public void PartialBattery_GivesWholeBlocksAndKeepsTheRest()
    {
        // 1 блок від панелі + 2.5 у батареї → 3 блоки; у батареї лишається 0.5.
        var s = State(1, ShadeHour, 2.5f);
        s.Power.LifeSupport = 4;
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(3, sys.Granted.LifeSupport);
        Assert.AreEqual(0.5f, s.Res.Battery, 0.001f);
    }

    [Test]
    public void UnbuiltPanel_GivesNothing()
    {
        var s = State(0, SunHour, 0f);
        s.Modules.Add(new ModuleState { Type = ModuleType.Solar, SlotId = 0, BuildHoursLeft = 5f });
        s.Power.LifeSupport = 1;
        var sys = new PowerSystem(Balance());

        sys.Tick(s, 1f, new DayReport(1));

        Assert.AreEqual(0f, sys.LastSupplyBlocks, 0.001f);
        Assert.AreEqual(0, sys.Granted.LifeSupport);
    }

    [Test]
    public void DayEnd_ReportsBatteryChange()
    {
        var s = State(2, SunHour, 3f);
        s.Power.LifeSupport = 4;
        var sys = new PowerSystem(Balance());
        var r = new DayReport(1);

        sys.Tick(s, 1f, r);
        sys.Tick(s, 1f, r);
        sys.OnDayEnd(s, r);

        var line = r.Lines.Find(l => l.Key == "report.battery_change");
        Assert.IsNotNull(line);
        Assert.AreEqual(4f, line.Delta, 0.001f); // 2 години по +2
    }
}
