// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Тести сонячних бур (роль 06, задача 2).</summary>
public class StormTests
{
    RadiationBalance bal;
    DonkiStormList events;
    StormSchedule schedule;
    RadiationSystem rad;
    GameState s;
    Astronaut inShelter, inHabitat, outside;
    List<string> warnings, starts;

    [SetUp]
    public void Setup()
    {
        bal = new RadiationBalance();
        bal.storm.warningHours = 6;
        bal.storm.minWarningHours = 2;
        events = new DonkiStormList();
        events.items.Add(new DonkiStorm { id = "weak", type = "FLR", date = "2024-10-09", classType = "X1.8", strength = 1 });
        events.items.Add(new DonkiStorm { id = "strong", type = "FLR", date = "2024-05-14", classType = "X8.7", strength = 3 });
        schedule = new StormSchedule();
        schedule.chapters.Add(new ChapterStorms { chapter = 1 });
        var ch3 = new ChapterStorms { chapter = 3 };
        ch3.storms.Add(new ScheduledStorm { day = 2, hour = 10, eventId = "strong" });
        schedule.chapters.Add(ch3);
        rad = new RadiationSystem(bal, new RadLabDose { dailyDose_mSv = 0.5f, careerLimit_mSv = 600f }, events, schedule);

        s = new GameState { Chapter = 3, Day = 1, Hour = 0 };
        s.Modules.Add(new ModuleState { Type = ModuleType.Shelter, SlotId = 1 });
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 2 });
        s.Power.Shelter = 2;
        inShelter = new Astronaut { Name = "S", Role = CrewRole.Medic, InsideSlot = 1 };
        inHabitat = new Astronaut { Name = "H", Role = CrewRole.Botanist, InsideSlot = 2 };
        outside = new Astronaut { Name = "O", Role = CrewRole.Geologist, InsideSlot = -1 };
        s.Crew.Add(inShelter); s.Crew.Add(inHabitat); s.Crew.Add(outside);

        warnings = new List<string>();
        starts = new List<string>();
        GameEvents.StormWarning += id => warnings.Add(id);
        GameEvents.StormStarted += id => starts.Add(id);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void RunUntil(int day, int hour, DayReport r)
    {
        while (s.Day < day || (s.Day == day && s.Hour < hour))
        {
            rad.Tick(s, 1f, r);
            s.Hour++;
            if (s.Hour >= 24) { s.Hour = 0; s.Day++; }
        }
    }

    [Test]
    public void Warning_ComesHoursBefore_ThenStormStarts()
    {
        var r = new DayReport(1);
        RunUntil(2, 4, r); // 2-й день 04:00 — ще рано (буря о 10:00, попередження за 6 год)
        Assert.AreEqual(0, warnings.Count);

        RunUntil(2, 5, r); // година 04→05 пройшла з тиком о 04:00
        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual("strong", warnings[0]);
        Assert.AreEqual(StormPhase.Warning, s.Radiation.Phase);
        Assert.AreEqual(6, s.Radiation.StormStartAt - (24 + 4));
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.storm_warning" && l.Arg == "strong"));

        RunUntil(2, 11, r);
        Assert.AreEqual(1, starts.Count);
        Assert.AreEqual(StormPhase.Active, s.Radiation.Phase);
        Assert.AreEqual(3, s.Radiation.StormStrength);
    }

    [Test]
    public void Storm_ShelterLessThanModuleLessThanOutside()
    {
        Assert.IsNull(rad.ForceStorm(s, "strong", 0));
        var before = new[] { inShelter.Dose, inHabitat.Dose, outside.Dose };
        rad.Tick(s, 1f, new DayReport(1));
        float dShelter = inShelter.Dose - before[0];
        float dHabitat = inHabitat.Dose - before[1];
        float dOutside = outside.Dose - before[2];

        Assert.AreEqual(StormPhase.Active, s.Radiation.Phase);
        Assert.AreEqual(bal.storm.shelterPerHour[2], dShelter, 1e-4f);
        Assert.AreEqual(bal.storm.modulePerHour[2], dHabitat, 1e-4f);
        Assert.AreEqual(bal.storm.outsidePerHour[2], dOutside, 1e-4f);
        Assert.Less(dShelter, dHabitat);
        Assert.Less(dHabitat, dOutside);
    }

    [Test]
    public void Shelter_WithoutEnoughPower_IsLikeOtherModule()
    {
        s.Power.Shelter = 1; // сила 3 потребує 2 блоки
        rad.ForceStorm(s, "strong", 0);
        rad.Tick(s, 1f, new DayReport(1));
        Assert.AreEqual(inHabitat.Dose, inShelter.Dose, 1e-4f);
        Assert.IsFalse(rad.IsShelterReady(s));
    }

    [Test]
    public void StrongerStorm_BiggerDoseOutside()
    {
        rad.ForceStorm(s, "weak", 0);
        rad.Tick(s, 1f, new DayReport(1));
        float weak = outside.Dose;
        Assert.AreEqual(bal.storm.outsidePerHour[0], weak, 1e-4f);
        Assert.Less(weak, bal.storm.outsidePerHour[2]);
    }

    [Test]
    public void Storm_EndsAfterDuration()
    {
        var r = new DayReport(1);
        rad.ForceStorm(s, "weak", 0);
        int duration = bal.storm.durationHours[0];
        for (int i = 0; i < duration + 1; i++) { rad.Tick(s, 1f, r); s.Hour++; }
        Assert.AreEqual(StormPhase.None, s.Radiation.Phase);
        Assert.AreEqual(1, s.Radiation.StormsSurvived);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.storm_ended"));
    }

    [Test]
    public void Chapter1_NoStorms()
    {
        s.Chapter = 1;
        RunUntil(6, 0, new DayReport(1));
        Assert.AreEqual(0, warnings.Count);
        Assert.AreEqual(StormPhase.None, s.Radiation.Phase);
    }

    [Test]
    public void ForceStorm_WhileStormGoing_Refused()
    {
        Assert.IsNull(rad.ForceStorm(s, "weak", 3));
        Assert.AreEqual("error.storm_busy", rad.ForceStorm(s, "strong", 3));
        Assert.AreEqual("error.not_found", new RadiationSystem(bal, null, events, schedule).ForceStorm(new GameState(), "nope", 1));
    }
}
