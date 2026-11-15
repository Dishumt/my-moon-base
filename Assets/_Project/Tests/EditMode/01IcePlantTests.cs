// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Бур → переробник → труби: ціна в будматеріалах, лід стає водою й киснем лише з підключеною трубою.</summary>
public class IcePlantTests
{
    static GameState NewState()
    {
        var s = new GameState { Day = 2, Hour = 12 };
        s.Supply.FirstMissionDone = true;
        s.Res.Materials = 100f;
        s.Res.Battery = 10f;
        s.Res.Water = 5f;
        s.Res.Oxygen = 20f;
        s.Pois.Add(new PoiState { Id = "ice_01", Type = PoiType.Ice });
        s.Modules.Add(new ModuleState { Type = ModuleType.Habitat, SlotId = 3, BuildHoursLeft = 0f });
        s.Modules.Add(new ModuleState { Type = ModuleType.Solar, SlotId = 0, BuildHoursLeft = 0f });
        return s;
    }

    static void Hours(IcePlantSystem sys, GameState s, int hours)
    {
        var r = new DayReport(s.Day);
        for (int i = 0; i < hours; i++) sys.Tick(s, 1f, r);
    }

    [Test]
    public void Drill_CostsMaterials_AndNeedsIce()
    {
        var s = NewState();
        var sys = new IcePlantSystem();
        Assert.AreEqual("iceplant.err_no_ice", sys.OrderDrill(s, "nope"));
        float before = s.Res.Materials;
        Assert.IsNull(sys.OrderDrill(s, "ice_01"));
        Assert.AreEqual(before - sys.B.drillMaterials, s.Res.Materials, 0.001f);
        Assert.AreEqual("iceplant.err_already", sys.OrderDrill(s, "ice_01"));
    }

    [Test]
    public void NoPipes_NoWater_ThenWaterPipe_GivesWater()
    {
        var s = NewState();
        var sys = new IcePlantSystem();
        Assert.IsNull(sys.OrderDrill(s, "ice_01"));
        Assert.IsNull(sys.OrderProcessor(s));
        Hours(sys, s, 30);
        Assert.IsTrue(s.IcePlant.DrillBuilt && s.IcePlant.ProcessorBuilt, "за 30 годин усе мало збудуватися");
        s.Res.Battery = 10f;
        float water = s.Res.Water;
        Hours(sys, s, 4);
        Assert.AreEqual(water, s.Res.Water, 0.001f, "без труби вода не йде на базу");

        Assert.AreEqual("iceplant.err_water_target", sys.ConnectPipe(s, PipeKind.Water, 0, 10f), "у сонячну панель воду не ведемо");
        Assert.IsNull(sys.ConnectPipe(s, PipeKind.Water, 3, 25f));
        s.Res.Battery = 10f;
        Hours(sys, s, 4);
        Assert.Greater(s.Res.Water, water, "з трубою води лід стає водою");
    }

    [Test]
    public void OxygenPipe_SplitsWaterIntoOxygen()
    {
        var s = NewState();
        var sys = new IcePlantSystem();
        sys.OrderDrill(s, "ice_01");
        sys.OrderProcessor(s);
        Hours(sys, s, 30);
        Assert.IsNull(sys.ConnectPipe(s, PipeKind.Oxygen, 3, 25f));
        s.Res.Battery = 10f;
        float oxygen = s.Res.Oxygen, water = s.Res.Water;
        Hours(sys, s, 4);
        Assert.Greater(s.Res.Oxygen, oxygen);
        Assert.AreEqual(water, s.Res.Water, 0.001f, "лише труба кисню — уся вода йде на кисень");
    }

    [Test]
    public void EmptyBattery_StopsTheDrill()
    {
        var s = NewState();
        var sys = new IcePlantSystem();
        sys.OrderDrill(s, "ice_01");
        Hours(sys, s, 20);
        s.Res.Battery = 0f;
        float ice = s.IcePlant.Ice;
        Hours(sys, s, 3);
        Assert.AreEqual(ice, s.IcePlant.Ice, 0.001f);
    }

    [Test]
    public void PipeCost_GrowsWithLength()
    {
        var sys = new IcePlantSystem();
        Assert.Less(sys.PipeCost(8f), sys.PipeCost(45f));
    }

    [TestCase("en")]
    [TestCase("uk")]
    public void Texts_Exist(string language)
    {
        Loc.Get("");
        string before = Loc.Language;
        Loc.SetLanguage(language);
        try
        {
            foreach (var key in new[] { "iceplant.prompt_build_drill", "iceplant.prompt_take_water", "iceplant.prompt_connect",
                                        "iceplant.err_water_target", "iceplant.report_water", "iceplant.fact_drill" })
                Assert.IsFalse(Loc.Get(key).StartsWith("#"), language + ": немає тексту " + key);
        }
        finally { Loc.SetLanguage(before); }
    }
}
