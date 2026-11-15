// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Усі тексти, які показують системи 03 (симуляція) і 06 (радіація, здоров'я), мають бути в обох мовах.
/// Без цього гравець бачить у звіті «#report.dose_day» замість речення.
/// </summary>
public class TextKeysTests
{
    static readonly string[] Keys03 =
    {
        // будівництво
        "build.err.locked", "build.err.no_robots", "build.err.no_module", "build.err.not_built",
        "build.err.already_shielded", "build.err.shield_in_progress",
        "error.place_outside", "error.place_taken", "error.unknown_module", "error.no_materials",
        "report.module_built", "report.module_shielded", "report.robots_no_power",
        // енергія
        "report.battery_change",
        "report.power_short.life_support", "report.power_short.shelter", "report.power_short.medbay",
        "report.power_short.comms", "report.power_short.greenhouse", "report.power_short.robots", "report.power_short.lab",
        // кисень і вода
        "report.oxygen_made", "report.oxygen_used", "report.water_used_lifesupport", "report.oxygen_empty", "report.water_low",
        "report.oxygen_low.no_power", "report.oxygen_low.no_water", "report.oxygen_low.crew_uses_more", "report.oxygen_low.refilling",
        // їжа і теплиця
        "report.food_eaten", "report.food_empty", "report.hungry",
        "report.greenhouse_no_module", "report.greenhouse_no_water", "report.harvest",
        // розділи
        "report.chapter_completed", "gameover.no_oxygen", "gameover.no_food",
    };

    static readonly string[] Keys06 =
    {
        "report.dose_day", "report.dose_yellow", "report.dose_red",
        "report.dose_why_outside", "report.dose_why_inside", "report.dose_why_shielded",
        "report.dose_why_storm_outside", "report.dose_why_storm_module", "report.dose_why_storm_shelter",
        "report.storm_warning", "report.storm_started", "report.storm_ended",
        "report.sick", "report.severe", "report.medkit_given", "report.recovered",
        "report.waiting_medkits", "report.no_medic",
        "error.storm_busy", "error.already_sick", "error.not_found",
        "storm.event", "storm.shelter_need",
        "dose.zone.green", "dose.zone.yellow", "dose.zone.red", "time.compressed",
        "weather.title", "weather.live_flare", "weather.quiet", "weather.archive_event",
    };

    static HashSet<string> Load(string language)
    {
        var table = DataLoader.Load<TextTable>("Text/texts_" + language);
        Assert.IsNotNull(table, language);
        var set = new HashSet<string>();
        foreach (var e in table.items)
        {
            Assert.IsNotEmpty(e.text, language + ": порожній текст у " + e.key);
            Assert.IsTrue(set.Add(e.key), language + ": ключ двічі: " + e.key);
        }
        return set;
    }

    static void AssertAllPresent(string language, IEnumerable<string> keys)
    {
        var have = Load(language);
        var missing = new List<string>();
        foreach (var k in keys) if (!have.Contains(k)) missing.Add(k);
        Assert.IsEmpty(missing, language + ": немає текстів для ключів: " + string.Join(", ", missing));
    }

    [TestCase("en")]
    [TestCase("uk")]
    public void Systems03_And_06_HaveTexts(string language)
    {
        var all = new List<string>(Keys03);
        all.AddRange(Keys06);
        AssertAllPresent(language, all);
    }

    [TestCase("en")]
    [TestCase("uk")]
    public void ChapterFiles_HaveTitleDescriptionAndGoalTexts(string language)
    {
        var keys = new List<string>();
        for (int n = 1; n <= 3; n++)
        {
            var c = DataLoader.Load<ChapterData>("Chapters/chapter_" + n.ToString("00"));
            Assert.IsNotNull(c, "chapter_" + n);
            keys.Add(c.titleKey);
            keys.Add(c.descriptionKey);
            foreach (var g in c.goals) keys.Add(g.titleKey);
        }
        AssertAllPresent(language, keys);
    }

    [Test]
    public void English_And_Ukrainian_HaveTheSameKeys()
    {
        var en = Load("en");
        var uk = Load("uk");
        var onlyEn = new List<string>(en); onlyEn.RemoveAll(uk.Contains);
        var onlyUk = new List<string>(uk); onlyUk.RemoveAll(en.Contains);
        Assert.IsEmpty(onlyEn, "є в EN, немає в UK: " + string.Join(", ", onlyEn));
        Assert.IsEmpty(onlyUk, "є в UK, немає в EN: " + string.Join(", ", onlyUk));
    }
}
