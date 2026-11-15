// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;

/// <summary>
/// Loc reads two tables: the main one (Text/texts_*.json) and role 04's one (Launch/texts_launch_*.json).
/// Without the second table the evening report shows "#report.supply_arrived" instead of a sentence.
/// </summary>
public class LocTests
{
    static readonly string[] Role04Keys =
    {
        "report.supply_arrived", "report.supply_launched", "report.kit_building", "report.kit_unpacked",
        "report.comms_no_power", "error.comms_closed", "crew.err_no_place", "error.unknown_rocket",
    };

    string languageBefore;

    [SetUp]
    public void RememberLanguage()
    {
        Loc.Get("");                 // loads the tables and the saved language
        languageBefore = Loc.Language;
    }

    [TearDown]
    public void RestoreLanguage() => Loc.SetLanguage(languageBefore);

    [TestCase("en")]
    [TestCase("uk")]
    public void Role04Keys_HaveTexts(string language)
    {
        Loc.SetLanguage(language);
        foreach (var key in Role04Keys)
            Assert.IsFalse(Loc.Get(key).StartsWith("#"), language + ": no text for " + key);
    }

    [Test]
    public void MissingKey_ShowsHash()
    {
        Assert.AreEqual("#no.such.key", Loc.Get("no.such.key"));
    }
}
