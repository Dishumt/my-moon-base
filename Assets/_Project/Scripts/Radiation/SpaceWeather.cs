// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;

/// <summary>Звідки дані про космічну погоду: живі з інтернету чи архівні з нашого файлу.</summary>
public enum SpaceWeatherSource { None, Live, Archive }

/// <summary>Що показати в блоці «Space weather today».</summary>
public class SpaceWeatherResult
{
    public SpaceWeatherSource Source;
    public string ClassType = "";  // клас спалаху, напр. "X1.2"; порожньо — подій немає
    public string TimeUtc = "";    // коли була подія (UTC)
    public string SourceUrl = "";  // посилання на джерело

    /// <summary>Позначка, яку ОБОВ'ЯЗКОВО показуємо поруч: "LIVE" або "ARCHIVE".</summary>
    public string Badge => Source == SpaceWeatherSource.Live ? "LIVE" : "ARCHIVE";

    /// <summary>Ключ тексту для рядка: спалах, «Сонце спокійне» або архівна подія.</summary>
    public string TextKey
    {
        get
        {
            if (Source == SpaceWeatherSource.Archive) return "weather.archive_event";
            return string.IsNullOrEmpty(ClassType) ? "weather.quiet" : "weather.live_flare";
        }
    }
}

/// <summary>
/// Логіка живої космічної погоди (задача 5 ролі 06): адреса запиту до DONKI і розбір відповіді.
/// Звичайний клас, тому його легко тестувати. Запит робить SpaceWeatherFetcher.
/// Правило: архівні дані НІКОЛИ не видаємо за живі.
/// </summary>
public static class SpaceWeather
{
    /// <summary>Сервіс DONKI без ключа (api.nasa.gov вимагає ключ, а його не можна класти в гру).</summary>
    public const string DonkiWs = "https://kauai.ccmc.gsfc.nasa.gov/DONKI/WS/get/";

    /// <summary>Адреса: спалахи за останні days днів.</summary>
    public static string BuildUrl(DateTime todayUtc, int days = 7)
    {
        var start = todayUtc.Date.AddDays(-days);
        return DonkiWs + "FLR?startDate=" + start.ToString("yyyy-MM-dd") + "&endDate=" + todayUtc.Date.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Розібрати відповідь DONKI FLR (масив JSON). Повертає LIVE-результат з останнім спалахом
    /// або LIVE «Сонце спокійне», якщо спалахів немає. null — відповідь не схожа на DONKI.
    /// </summary>
    public static SpaceWeatherResult ParseLive(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        string trimmed = json.Trim();
        if (trimmed.Length == 0) return new SpaceWeatherResult { Source = SpaceWeatherSource.Live };
        if (trimmed[0] != '[') return null;

        DonkiFlrList list;
        try { list = JsonUtility.FromJson<DonkiFlrList>("{\"items\":" + trimmed + "}"); }
        catch (ArgumentException) { return null; }
        if (list == null || list.items == null) return null;

        DonkiFlr latest = null;
        foreach (var f in list.items)
        {
            if (f == null) continue;
            if (latest == null || string.CompareOrdinal(TimeOf(f), TimeOf(latest)) > 0) latest = f;
        }

        var result = new SpaceWeatherResult { Source = SpaceWeatherSource.Live };
        if (latest != null)
        {
            result.ClassType = latest.classType ?? "";
            result.TimeUtc = TimeOf(latest);
            result.SourceUrl = latest.link ?? "";
        }
        return result;
    }

    static string TimeOf(DonkiFlr f) => !string.IsNullOrEmpty(f.peakTime) ? f.peakTime : (f.beginTime ?? "");

    /// <summary>Запасний варіант: найновіша подія з donki_storms.json, позначена ARCHIVE.</summary>
    public static SpaceWeatherResult Archive(DonkiStormList list)
    {
        var result = new SpaceWeatherResult { Source = SpaceWeatherSource.Archive };
        if (list == null) return result;
        DonkiStorm latest = null;
        foreach (var e in list.items)
        {
            if (e.type != "FLR") continue;
            if (latest == null || string.CompareOrdinal(e.date, latest.date) > 0) latest = e;
        }
        if (latest != null)
        {
            result.ClassType = latest.classType;
            result.TimeUtc = latest.date;
            result.SourceUrl = latest.sourceUrl;
        }
        return result;
    }
}
