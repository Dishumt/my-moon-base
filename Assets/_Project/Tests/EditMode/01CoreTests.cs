// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Тести ядра (роль 01): годинник і створення стану з розділу.</summary>
public class CoreTests
{
    /// <summary>Тестова система: просто записує, що її викликали.</summary>
    class FakeSystem : ISimSystem
    {
        readonly string name; readonly List<string> log;
        public FakeSystem(string name, List<string> log) { this.name = name; this.log = log; }
        public void Tick(GameState s, float hours, DayReport r) => log.Add(name + ".Tick");
        public void OnDayEnd(GameState s, DayReport r) => log.Add(name + ".DayEnd");
    }

    [TearDown]
    public void CleanEvents() => GameEvents.ClearAll();

    [Test]
    public void OneHour_TicksSystemsInOrder()
    {
        var log = new List<string>();
        var clock = new GameClock(new GameState(), 180f);
        clock.Register(new FakeSystem("A", log));
        clock.Register(new FakeSystem("B", log));

        clock.AdvanceHours(1);

        Assert.AreEqual(new[] { "A.Tick", "B.Tick" }, log.ToArray());
    }

    [Test]
    public void TwentyFourHours_EndsDayOnce()
    {
        var state = new GameState { Day = 1, Hour = 0 };
        var clock = new GameClock(state, 180f);
        int dayEnded = 0;
        GameEvents.DayEnded += r => dayEnded++;

        clock.AdvanceHours(24);

        Assert.AreEqual(2, state.Day);
        Assert.AreEqual(0, state.Hour);
        Assert.AreEqual(1, dayEnded);
    }

    [Test]
    public void RealTime_DayLengthFromBalance()
    {
        var state = new GameState { Hour = 0 };
        var clock = new GameClock(state, 24f); // день = 24 секунди, тобто 1 секунда = 1 година

        clock.Update(1.01f);
        Assert.AreEqual(1, state.Hour);

        clock.SetSpeed(2f);
        clock.Update(1.01f);                   // на ×2 за секунду минає 2 години
        Assert.AreEqual(3, state.Hour);
    }

    [Test]
    public void Paused_TimeDoesNotMove()
    {
        var state = new GameState { Hour = 0 };
        var clock = new GameClock(state, 24f);

        clock.Pause();
        clock.Update(5f);
        Assert.AreEqual(0, state.Hour);

        clock.Resume();
        clock.PushModal();                     // відкрили планшет
        clock.Update(5f);
        Assert.AreEqual(0, state.Hour);
    }

    [Test]
    public void Factory_CreatesStateFromChapter()
    {
        var data = new ChapterStartData { startHour = 6, robots = 2 };
        data.resources.Water = 60;
        data.crew.Add(new CrewStart { name = "Mira", role = "Medic" });
        data.pois.Add(new PoiStart { id = "ice_crater_1", type = "Ice", amount = 100 });

        var s = GameStateFactory.FromChapter(data);

        Assert.AreEqual(6, s.Hour);
        Assert.AreEqual(60, s.Res.Water);
        Assert.AreEqual(CrewRole.Medic, s.Crew[0].Role);
        Assert.AreEqual(-1, s.Crew[0].InsideSlot);
        Assert.AreEqual(2, s.Robots.Count);
        Assert.AreEqual(PoiType.Ice, s.FindPoi("ice_crater_1").Type);
    }

    [Test]
    public void Chapter01Json_LoadsWithFourCrew()
    {
        var data = DataLoader.Load<ChapterStartData>("Chapters/chapter_01");
        Assert.AreEqual(4, data.crew.Count);
    }
}
