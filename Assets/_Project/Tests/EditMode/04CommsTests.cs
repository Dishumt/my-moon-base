// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести вікон зв'язку (роль 04).</summary>
public class CommsTests
{
    CommsSystem comms;
    GameState s;

    [SetUp]
    public void Setup()
    {
        comms = new CommsSystem(new LaunchData { commsOpenHours = 30, commsClosedHours = 18 });
        s = new GameState { Day = 1, Hour = 0 };
        s.Power.Comms = 1;
    }

    [Test]
    public void OpenFirst30Hours_ThenClosed18()
    {
        Assert.IsTrue(comms.IsOpen(s));
        s.Day = 2; s.Hour = 5;   // година 29
        Assert.IsTrue(comms.IsOpen(s));
        s.Hour = 6;              // година 30
        Assert.IsFalse(comms.IsOpen(s));
        s.Day = 3; s.Hour = 0;   // година 48 — нове вікно
        Assert.IsTrue(comms.IsOpen(s));
    }

    [Test]
    public void NoPower_Closed()
    {
        s.Power.Comms = 0;
        Assert.IsFalse(comms.IsOpen(s));
    }

    [Test]
    public void UpcomingWindows_ForCalendar()
    {
        s.Day = 2; s.Hour = 10; // година 34, закрито
        var list = comms.UpcomingWindows(s, 2);
        Assert.AreEqual(2, list.Count);
        Assert.AreEqual(48, list[0].StartHour);
        Assert.AreEqual(78, list[0].EndHour);
        Assert.AreEqual(96, list[1].StartHour);
        Assert.AreEqual(14, comms.HoursUntilOpen(s));
    }

    [Test]
    public void UpcomingWindows_IncludesCurrent()
    {
        s.Hour = 10;
        var list = comms.UpcomingWindows(s, 1);
        Assert.AreEqual(0, list[0].StartHour);
        Assert.AreEqual(30, list[0].EndHour);
    }

    [Test]
    public void Tick_WritesIsOpen()
    {
        comms.Tick(s, 1f, new DayReport(1));
        Assert.IsTrue(s.Comms.IsOpen);
    }
}
