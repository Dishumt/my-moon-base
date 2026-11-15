// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using NUnit.Framework;

/// <summary>Тести справжніх даних NASA і живої космічної погоди (роль 06, задачі 0, 2, 5).</summary>
public class NasaDataTests
{
    [Test]
    public void DonkiStorms_LoadAndHaveSources()
    {
        var list = DonkiStormList.Load();
        Assert.That(list.items.Count, Is.InRange(10, 15));
        foreach (var e in list.items)
        {
            Assert.IsNotEmpty(e.id);
            Assert.That(e.type, Is.EqualTo("FLR").Or.EqualTo("SEP").Or.EqualTo("GST"), e.id); // Is.AnyOf немає в NUnit з Unity 6.6
            Assert.That(e.strength, Is.InRange(1, 3), e.id);
            StringAssert.StartsWith("https://", e.sourceUrl, e.id);
            StringAssert.StartsWith("2024-", e.date, e.id);
        }
        Assert.IsTrue(list.items.Exists(e => e.date.StartsWith("2024-05")), "обов'язково травень 2024");
    }

    [Test]
    public void Schedule_EveryEventExists_AndChaptersMatchDesign()
    {
        var list = DonkiStormList.Load();
        var schedule = StormSchedule.Load();

        Assert.AreEqual(0, schedule.For(1).Count, "розділ 1 — без бур");

        var ch2 = schedule.For(2);
        Assert.AreEqual(1, ch2.Count, "розділ 2 — одна слабка");
        Assert.AreEqual(1, list.Find(ch2[0].eventId).strength);

        var ch3 = schedule.For(3);
        Assert.AreEqual(3, ch3.Count, "розділ 3 — серія з 3 бур");
        foreach (var st in ch3)
        {
            var e = list.Find(st.eventId);
            Assert.IsNotNull(e, st.eventId);
            StringAssert.StartsWith("2024-05", e.date, "бурі травня 2024");
            Assert.That(st.hour, Is.InRange(0, 23));
            Assert.That(st.day, Is.GreaterThanOrEqualTo(1));
        }
    }

    [Test]
    public void RadLab_And_Balance_Load()
    {
        var radlab = RadLabDose.Load();
        Assert.Greater(radlab.dailyDose_mSv, 0f);
        Assert.AreEqual(600f, radlab.careerLimit_mSv);
        Assert.IsNotEmpty(radlab.sources);

        var bal = RadiationBalance.Load();
        Assert.Greater(bal.timeCompression, 1f);
        Assert.AreEqual(3, bal.storm.outsidePerHour.Count);
        Assert.AreEqual(3, bal.storm.modulePerHour.Count);
        Assert.AreEqual(3, bal.storm.shelterPerHour.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Less(bal.storm.shelterPerHour[i], bal.storm.modulePerHour[i]);
            Assert.Less(bal.storm.modulePerHour[i], bal.storm.outsidePerHour[i]);
        }
    }

    [Test]
    public void Facts_HaveBothLanguagesAndSource()
    {
        var facts = NasaFactList.Load();
        Assert.That(facts.items.Count, Is.InRange(5, 8));
        foreach (var f in facts.items)
        {
            Assert.IsNotEmpty(f.en, f.id);
            Assert.IsNotEmpty(f.uk, f.id);
            StringAssert.StartsWith("https://osdr.nasa.gov/", f.sourceUrl, f.id);
        }
    }

    [Test]
    public void LiveWeather_ParsesDonkiAndTakesLatest()
    {
        string json = "[{\"flrID\":\"a\",\"beginTime\":\"2026-11-10T01:00Z\",\"peakTime\":\"2026-11-10T01:10Z\",\"classType\":\"M2.1\",\"activeRegionNum\":null,\"link\":\"https://x/a\"}," +
                      "{\"flrID\":\"b\",\"beginTime\":\"2026-11-12T05:00Z\",\"peakTime\":\"2026-11-12T05:20Z\",\"classType\":\"X1.0\",\"link\":\"https://x/b\"}]";
        var r = SpaceWeather.ParseLive(json);
        Assert.AreEqual(SpaceWeatherSource.Live, r.Source);
        Assert.AreEqual("LIVE", r.Badge);
        Assert.AreEqual("X1.0", r.ClassType);
        Assert.AreEqual("weather.live_flare", r.TextKey);
    }

    [Test]
    public void LiveWeather_EmptyList_IsQuietButStillLive()
    {
        var r = SpaceWeather.ParseLive("[]");
        Assert.AreEqual(SpaceWeatherSource.Live, r.Source);
        Assert.AreEqual("weather.quiet", r.TextKey);
    }

    [Test]
    public void LiveWeather_Garbage_IsNull_SoArchiveIsUsed()
    {
        Assert.IsNull(SpaceWeather.ParseLive("<html>blocked</html>"));
        Assert.IsNull(SpaceWeather.ParseLive(null));

        var archive = SpaceWeather.Archive(DonkiStormList.Load());
        Assert.AreEqual(SpaceWeatherSource.Archive, archive.Source);
        Assert.AreEqual("ARCHIVE", archive.Badge, "архів ніколи не видаємо за живі дані");
        Assert.IsNotEmpty(archive.ClassType);
    }

    [Test]
    public void LiveWeather_UrlIsLast7Days()
    {
        string url = SpaceWeather.BuildUrl(new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc));
        Assert.AreEqual("https://ccmc.gsfc.nasa.gov/DONKI/WS/get/FLR?startDate=2026-11-08&endDate=2026-11-15", url);
    }

    [Test]
    public void LiveWeather_UsesHostThatIsOpenInHackathonNetwork_NotTheLegacyOne()
    {
        // kauai.* не відкривався в мережі хакатону; ccmc.* відкритий і віддає CORS «*» (потрібно для веб-збірки)
        StringAssert.StartsWith("https://ccmc.gsfc.nasa.gov/", SpaceWeather.DonkiWs);
        StringAssert.StartsWith("https://kauai.", SpaceWeather.DonkiWsLegacy);
        StringAssert.DoesNotStartWith(SpaceWeather.DonkiWsLegacy, SpaceWeather.BuildUrl(DateTime.UtcNow));
    }
}
