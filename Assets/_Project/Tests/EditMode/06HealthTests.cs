// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>Тести здоров'я й ліків (роль 06, задача 3).</summary>
public class HealthTests
{
    RadiationBalance bal;
    HealthSystem health;
    GameState s;
    Astronaut medic, geo;
    float dice;

    [SetUp]
    public void Setup()
    {
        bal = new RadiationBalance { illnessFromChapter = 3, yellowPercent = 50f };
        bal.health = new HealthBalance { sickChancePerHour = 0.01f, riskMultiplier = 2f, hungrySatietyBelow = 25f, recoverHours = 6f, severeRecoverHours = 12f, sickToSevereHours = 24f };
        health = new HealthSystem(bal);
        dice = 1f; // за замовчуванням ніхто не хворіє сам
        health.Random01 = () => dice;

        s = new GameState { Chapter = 3 };
        s.Modules.Add(new ModuleState { Type = ModuleType.Medbay, SlotId = 7 });
        medic = new Astronaut { Name = "M", Role = CrewRole.Medic };
        geo = new Astronaut { Name = "G", Role = CrewRole.Geologist };
        s.Crew.Add(medic); s.Crew.Add(geo);
    }

    [TearDown]
    public void Clean() => GameEvents.ClearAll();

    void Hours(int n, DayReport r = null)
    {
        r = r ?? new DayReport(1);
        for (int i = 0; i < n; i++) health.Tick(s, 1f, r);
    }

    [Test]
    public void SickWithMedKit_GetsMedicine_AndRecovers()
    {
        s.Res.MedKits = 2;
        var r = new DayReport(1);
        health.MakeSick(s, geo, r);
        Hours(1, r);
        Assert.AreEqual(1, s.Res.MedKits);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.medkit_given" && l.Arg == "G"));
        Assert.AreEqual(HealthStatus.Sick, geo.Health);

        Hours(5, r);
        Assert.AreEqual(HealthStatus.Ok, geo.Health);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.recovered"));
        Assert.AreEqual(0, s.Radiation.Patients.Count);
    }

    [Test]
    public void NoMedKits_SickBecomesSevere_GoesToMedbay()
    {
        health.MakeSick(s, geo);
        Hours(23);
        Assert.AreEqual(HealthStatus.Sick, geo.Health);
        Hours(1);
        Assert.AreEqual(HealthStatus.Severe, geo.Health);
        Assert.AreEqual(CrewTask.Medbay, geo.Task);
        Assert.AreEqual("7", geo.TaskTarget);
    }

    [Test]
    public void NobodyDies_SevereWaitsForever()
    {
        geo.Health = HealthStatus.Severe;
        Hours(24 * 20);
        Assert.AreEqual(HealthStatus.Severe, geo.Health);
        Assert.AreEqual(2, s.Crew.Count);
        var r = new DayReport(1);
        health.OnDayEnd(s, r);
        Assert.IsTrue(r.Lines.Exists(l => l.Key == "report.waiting_medkits"));
    }

    [Test]
    public void SevereCured_WhenMedKitsArrive()
    {
        geo.Health = HealthStatus.Severe;
        Hours(10);
        s.Res.MedKits = 1; // прийшла поставка
        Hours(12);
        Assert.AreEqual(HealthStatus.Ok, geo.Health);
        Assert.AreEqual(CrewTask.None, geo.Task);
        Assert.AreEqual(0, s.Res.MedKits);
    }

    [Test]
    public void MedicCaring_WithPoweredMedbay_StopsGettingWorse()
    {
        medic.Task = CrewTask.Heal;
        s.Power.Medbay = 1;
        health.MakeSick(s, geo);
        Hours(48);
        Assert.AreEqual(HealthStatus.Sick, geo.Health, "медик доглядає — не гіршає");

        s.Power.Medbay = 0; // без енергії догляд не працює
        Hours(24);
        Assert.AreEqual(HealthStatus.Severe, geo.Health);
    }

    [Test]
    public void SevereMedic_CannotGiveMedicine()
    {
        s.Res.MedKits = 3;
        medic.Health = HealthStatus.Severe;
        health.MakeSick(s, geo);
        Hours(2);
        Assert.AreEqual(3, s.Res.MedKits);
    }

    [Test]
    public void Hungry_OrYellowDose_DoublesChance()
    {
        Assert.AreEqual(0.01f, health.SickChance(geo), 1e-6f);
        geo.Satiety = 10f;
        Assert.AreEqual(0.02f, health.SickChance(geo), 1e-6f);
        geo.Satiety = 100f;
        geo.Dose = 60f;
        Assert.AreEqual(0.02f, health.SickChance(geo), 1e-6f);

        dice = 0.015f; // між звичайним і подвоєним шансом
        Hours(1);
        Assert.AreEqual(HealthStatus.Sick, geo.Health);
        Assert.AreEqual(HealthStatus.Ok, medic.Health);
    }

    [Test]
    public void BeforeChapter3_NoIllness()
    {
        s.Chapter = 2;
        dice = 0f;
        Hours(24);
        Assert.AreEqual(HealthStatus.Ok, geo.Health);
        Assert.AreEqual(HealthStatus.Ok, medic.Health);
    }
}
