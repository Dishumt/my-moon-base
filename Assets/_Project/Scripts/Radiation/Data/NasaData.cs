// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Класи для JSON-файлів з Resources/Data/Nasa/ (роль 06).
/// Назви полів = ключі в JSON, тому деякі назви з малої літери.
/// </summary>

/// <summary>Одна справжня подія космічної погоди з NASA DONKI (donki_storms.json).</summary>
[Serializable]
public class DonkiStorm
{
    public string id;          // наш короткий id, напр. "flr-2024-05-11-x5.8"
    public string donkiId;     // справжній id у DONKI (заповнює інструмент перевірки)
    public string type;        // FLR — спалах, SEP — потік протонів, GST — геомагнітна буря
    public string date;        // дата (і час, якщо відомий) у UTC
    public string classType;   // клас спалаху: M5.0, X5.8…
    public int activeRegion;   // номер активної ділянки на Сонці
    public int strength;       // сила для гри 1..3
    public string sourceUrl;   // посилання на джерело
    public bool verified;      // true = перевірено в DONKI
    public string note;

    /// <summary>Тільки дата "2024-05-11" (без часу).</summary>
    public string Day => string.IsNullOrEmpty(date) || date.Length < 10 ? date : date.Substring(0, 10);
}

[Serializable]
public class DonkiStormList
{
    public string _ai;
    public string _note;
    public string _status;
    public List<DonkiStorm> items = new List<DonkiStorm>();

    public DonkiStorm Find(string id) => items.Find(x => x.id == id);

    public static DonkiStormList Load() => DataLoader.Load<DonkiStormList>("Nasa/donki_storms");
}

/// <summary>Одна буря в розкладі: у який день розділу і годину.</summary>
[Serializable]
public class ScheduledStorm
{
    public int day;          // день розділу, 1 = перший
    public int hour;         // година старту 0..23
    public string eventId;   // id з donki_storms.json
}

[Serializable]
public class ChapterStorms
{
    public int chapter;
    public List<ScheduledStorm> storms = new List<ScheduledStorm>();
}

/// <summary>storm_schedule.json: бурі для кожного розділу.</summary>
[Serializable]
public class StormSchedule
{
    public List<ChapterStorms> chapters = new List<ChapterStorms>();

    /// <summary>Бурі розділу (порожній список, якщо розділу немає).</summary>
    public List<ScheduledStorm> For(int chapter)
    {
        var c = chapters.Find(x => x.chapter == chapter);
        return c != null && c.storms != null ? c.storms : new List<ScheduledStorm>();
    }

    public static StormSchedule Load() => DataLoader.Load<StormSchedule>("Nasa/storm_schedule");
}

/// <summary>radlab_dose.json: справжні дози на МКС.</summary>
[Serializable]
public class RadLabDose
{
    public float dailyDose_mSv = 0.5f;      // типова доза на МКС за добу
    public float dailyDoseMin_mSv;
    public float dailyDoseMax_mSv;
    public float careerLimit_mSv = 600f;    // ліміт NASA на всю кар'єру
    public string periodQuiet;
    public string periodStorm;
    public List<NasaSource> sources = new List<NasaSource>();

    public static RadLabDose Load() => DataLoader.Load<RadLabDose>("Nasa/radlab_dose");
}

[Serializable]
public class NasaSource
{
    public string name;
    public string url;
}

/// <summary>Один факт для дітей (facts.json).</summary>
[Serializable]
public class NasaFact
{
    public string id;
    public string topic;
    public string en;
    public string uk;
    public string studyId;    // номер дослідження OSDR, напр. "OSD-48"
    public string sourceUrl;
    public bool verified;

    /// <summary>Текст поточною мовою гри.</summary>
    public string Text => Loc.Language == Loc.Ukrainian && !string.IsNullOrEmpty(uk) ? uk : en;
}

[Serializable]
public class NasaFactList
{
    public List<NasaFact> items = new List<NasaFact>();

    public static NasaFactList Load() => DataLoader.Load<NasaFactList>("Nasa/facts");
}

/// <summary>Спалах у відповіді DONKI FLR (для живої космічної погоди та перевірки).</summary>
[Serializable]
public class DonkiFlr
{
    public string flrID;
    public string beginTime;
    public string peakTime;
    public string classType;
    public string link;
}

[Serializable]
public class DonkiFlrList
{
    public List<DonkiFlr> items = new List<DonkiFlr>();
}
