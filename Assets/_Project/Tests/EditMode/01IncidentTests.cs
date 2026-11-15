// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Випадкові поломки: унітаз ламається тільки в збудованому житлі, вода витікає, ремонт дає науку, тексти є обома мовами.</summary>
public class IncidentTests
{
    static GameState NewState()
    {
        var s = new GameState { Day = 2, Hour = 12 };
        s.Supply.FirstMissionDone = true;
        s.Res.Water = 10f;
        return s;
    }

    [Test]
    public void Toilet_NeedsBuiltHabitat()
    {
        var s = NewState();
        var sys = new IncidentSystem();
        Assert.AreEqual("incident.error_no_module", sys.TryStart(s, null, "toilet"));
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 3, BuildHoursLeft = 0f });
        Assert.IsNull(sys.TryStart(s, null, "toilet"));
        Assert.AreEqual(3, sys.ActiveSlot);
    }

    [Test]
    public void BrokenToilet_LeaksWater_FixGivesScience()
    {
        var s = NewState();
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 3, BuildHoursLeft = 0f });
        var sys = new IncidentSystem();
        var r = new DayReport(2);
        Assert.IsNull(sys.TryStart(s, r, "toilet"));
        sys.Tick(s, 1f, r);
        Assert.Less(s.Res.Water, 10f, "вода має витікати, поки унітаз зламаний");
        float before = s.Science;
        sys.Fix(s, r);
        Assert.IsNull(sys.Active);
        Assert.Greater(s.Science, before);
        float water = s.Res.Water;
        sys.Tick(s, 1f, r);
        Assert.AreEqual(water, s.Res.Water, 0.0001f, "після ремонту вода не витікає");
    }

    [TestCase("en")]
    [TestCase("uk")]
    public void AllIncidentTexts_Exist(string language)
    {
        Loc.Get("");
        string before = Loc.Language;
        Loc.SetLanguage(language);
        try
        {
            var table = DataLoader.Load<IncidentTable>("Incidents/incidents");
            foreach (var item in table.items)
                foreach (var part in new[] { "title", "hint", "fact", "report", "still" })
                {
                    string key = "incident." + item.id + "." + part;
                    Assert.IsFalse(Loc.Get(key).StartsWith("#"), language + ": немає тексту " + key);
                }
            Assert.IsFalse(Loc.Get("incident.hold_f").StartsWith("#"));
            Assert.IsFalse(Loc.Get("incident.fixed").StartsWith("#"));
        }
        finally { Loc.SetLanguage(before); }
    }
}
