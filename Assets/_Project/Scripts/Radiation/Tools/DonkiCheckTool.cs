// AI-assisted: written with Claude. See docs/AI_USE.md
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Інструмент ролі 06 (тільки в редакторі Unity, у гру не потрапляє):
/// меню Moon → 06: Перевірити дані DONKI.
/// Для кожної події з donki_storms.json питає справжню базу NASA DONKI, знаходить ту саму подію,
/// записує її справжній id (donkiId), точний час і посилання, і ставить verified = true.
/// Події, яких не знайдено, лишаються verified = false — їх треба виправити або прибрати.
///
/// Адреси DONKI пробуємо по черзі (це той самий API): ccmc.gsfc.nasa.gov → kauai.ccmc.gsfc.nasa.gov →
/// дзеркало api.nasa.gov (DEMO_KEY, ліміт запитів на годину).
/// Якщо NASA не відповіла ЖОДНОГО разу (немає мережі, мережа блокує сервер, ліміт), файл НЕ перезаписується:
/// у Console з'являється «NASA DONKI недоступний, спробуй з іншої мережі».
/// </summary>
public static class DonkiCheckTool
{
    const string FilePath = "Assets/_Project/Resources/Data/Nasa/donki_storms.json";
    const double TimeoutSeconds = 20;
    const string MirrorBase = "https://api.nasa.gov/DONKI/";  // те саме API DONKI, через api.nasa.gov
    const string MirrorKey = "DEMO_KEY";                       // публічний демо-ключ, не секрет
    const int MaxNetworkFailsPerHost = 2;                      // після стількох збоїв поспіль хост пропускаємо до кінця запуску

    /// <summary>
    /// Адреси сервісу DONKI. ccmc.gsfc.nasa.gov працює без ключа навіть там, де kauai.ccmc.gsfc.nasa.gov
    /// закритий (так було в мережі хакатону), тому він перший. Дзеркало api.nasa.gov — останнє:
    /// воно лімітоване, і FLR через нього зараз віддає 404.
    /// </summary>
    static readonly string[] Hosts =
    {
        SpaceWeather.DonkiWs,       // ccmc.gsfc.nasa.gov — той самий хост, що й у живій космічній погоді в грі
        SpaceWeather.DonkiWsLegacy, // kauai.ccmc.gsfc.nasa.gov
        MirrorBase,
    };

    static int _host;                                   // який хост відповів востаннє — з нього починаємо
    static int[] _netFails = new int[Hosts.Length];     // збої мережі поспіль для кожного хоста
    static bool _rateLimited;                           // api.nasa.gov сказав «забагато запитів» (429)

    /// <summary>Усі можливі поля відповіді DONKI (FLR, SEP, GST) — JsonUtility пропускає зайві.</summary>
    [Serializable]
    class DonkiAny
    {
        public string flrID, sepID, gstID;
        public string beginTime, peakTime, eventTime, startTime;
        public string classType, link;

        public string Id => !string.IsNullOrEmpty(flrID) ? flrID : !string.IsNullOrEmpty(sepID) ? sepID : (gstID ?? "");
        public string Time => !string.IsNullOrEmpty(peakTime) ? peakTime
                            : !string.IsNullOrEmpty(eventTime) ? eventTime
                            : !string.IsNullOrEmpty(startTime) ? startTime : (beginTime ?? "");
    }

    [Serializable]
    class DonkiAnyList { public List<DonkiAny> items = new List<DonkiAny>(); }

    /// <summary>Чим закінчилась перевірка однієї події.</summary>
    enum Outcome { Verified, NotFound, BadDate, NetworkError, HttpError }

    /// <summary>Чи відповів сервер: Ok, мережевий збій (таймаут, немає зв'язку) чи помилка HTTP (404, 500, 429...).</summary>
    enum Reply { Ok, NetworkError, HttpError }

    [MenuItem("Moon/06: Перевірити дані DONKI")]
    static void CheckAll()
    {
        if (!File.Exists(FilePath))
        {
            Debug.LogError("DonkiCheckTool: немає файлу " + FilePath);
            return;
        }
        var list = JsonUtility.FromJson<DonkiStormList>(File.ReadAllText(FilePath));
        _host = 0;
        _netFails = new int[Hosts.Length];
        _rateLimited = false;
        int answered = 0;    // DONKI відповів (знайшли подію або чесно не знайшли)
        int netFail = 0;     // не вдалося зв'язатися з NASA
        int httpFail = 0;    // сервер відповів помилкою
        try
        {
            for (int i = 0; i < list.items.Count; i++)
            {
                var e = list.items[i];
                EditorUtility.DisplayProgressBar("DONKI", e.id, (float)i / list.items.Count);
                switch (Check(e))
                {
                    case Outcome.Verified:
                    case Outcome.NotFound:
                        answered++; break;
                    case Outcome.NetworkError:
                        netFail++; break;
                    case Outcome.HttpError:
                        httpFail++; break;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (answered == 0)
        {
            // NASA не відповіла жодного разу: перевірки не було, тож файл НЕ перезаписуємо
            string limit = _rateLimited ? " (api.nasa.gov: вичерпано ліміт DEMO_KEY, спробуй пізніше.)" : "";
            if (netFail > 0)
                Debug.LogWarning("DonkiCheckTool: NASA DONKI недоступний, спробуй з іншої мережі. Файл не змінено." + limit);
            else if (httpFail > 0)
                Debug.LogError("DonkiCheckTool: NASA DONKI відповів помилкою на всі запити (деталі вище). Файл не змінено." + limit);
            else
                Debug.LogWarning("DonkiCheckTool: нічого перевіряти — немає подій із правильною датою. Файл не змінено.");
            return;
        }

        int verified = 0;
        foreach (var e in list.items) if (e.verified) verified++;
        int notChecked = netFail + httpFail;

        list._status = "Checked with NASA DONKI on " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ": "
                     + verified + " of " + list.items.Count + " events verified (verified=true). The rest must be fixed or removed."
                     + (notChecked > 0 ? " " + notChecked + " could not be checked this time (network or server error) and keep their old flag." : "");
        File.WriteAllText(FilePath, JsonUtility.ToJson(list, true));
        AssetDatabase.Refresh();
        Debug.Log("DonkiCheckTool: підтверджено " + verified + " з " + list.items.Count
                + (notChecked > 0 ? " (не вдалося перевірити: " + notChecked + ")" : "")
                + ". Файл оновлено: " + FilePath);
    }

    static Outcome Check(DonkiStorm e)
    {
        DateTime day;
        if (!DateTime.TryParse(e.Day, out day))
        {
            Debug.LogWarning("DonkiCheckTool: погана дата у " + e.id);
            return Outcome.BadDate;
        }
        string query = e.type + "?startDate=" + day.AddDays(-1).ToString("yyyy-MM-dd")
                     + "&endDate=" + day.AddDays(1).ToString("yyyy-MM-dd");
        string text;
        switch (Download(query, out text))
        {
            case Reply.NetworkError: return Outcome.NetworkError;
            case Reply.HttpError: return Outcome.HttpError;
        }

        var found = JsonUtility.FromJson<DonkiAnyList>("{\"items\":" + (string.IsNullOrWhiteSpace(text) ? "[]" : text) + "}");
        DonkiAny best = null;
        foreach (var d in found.items)
        {
            if (!d.Time.StartsWith(e.Day)) continue;
            if (e.type == "FLR" && d.classType != e.classType) continue;
            if (best == null || Distance(d.Time, e.date) < Distance(best.Time, e.date)) best = d;
        }

        if (best == null)
        {
            var candidates = new List<string>();
            foreach (var d in found.items) candidates.Add(d.Time + " " + d.classType);
            Debug.LogWarning("DonkiCheckTool: НЕ знайдено " + e.id + ". Що є в DONKI поруч: " + string.Join("; ", candidates));
            e.verified = false;
            return Outcome.NotFound;
        }

        e.donkiId = best.Id;
        e.date = best.Time;
        if (!string.IsNullOrEmpty(best.link)) e.sourceUrl = best.link;
        e.verified = true;
        Debug.Log("DonkiCheckTool: ✓ " + e.id + " = " + best.Id);
        return Outcome.Verified;
    }

    /// <summary>Наскільки різні два часи (у хвилинах); якщо часу немає — 0.</summary>
    static double Distance(string a, string b)
    {
        DateTime ta, tb;
        if (!DateTime.TryParse(a, out ta) || !DateTime.TryParse(b, out tb)) return 0;
        return Math.Abs((ta - tb).TotalMinutes);
    }

    /// <summary>
    /// Скачує запит з першого хоста, що відповів (Reply.Ok — текст у <paramref name="text"/>).
    /// Хости, до яких не вдається достукатись, пропускаємо, щоб не чекати таймаут на кожну подію.
    /// Якщо до когось із хостів не вдалося зв'язатися — це NetworkError, навіть коли дзеркало дало HTTP-помилку.
    /// </summary>
    static Reply Download(string query, out string text)
    {
        text = null;
        bool anyNetworkError = false, anyHttpError = false;
        for (int k = 0; k < Hosts.Length; k++)
        {
            int idx = (_host + k) % Hosts.Length;
            if (_netFails[idx] >= MaxNetworkFailsPerHost)
            {
                anyNetworkError = true;
                continue;
            }
            string url = Hosts[idx] + query + (Hosts[idx] == MirrorBase ? "&api_key=" + MirrorKey : "");
            Reply r = TryGet(url, out text);
            if (r == Reply.Ok)
            {
                _host = idx;
                _netFails[idx] = 0;
                return Reply.Ok;
            }
            if (r == Reply.NetworkError) { _netFails[idx]++; anyNetworkError = true; }
            else anyHttpError = true;
        }
        text = null;
        return anyNetworkError ? Reply.NetworkError : anyHttpError ? Reply.HttpError : Reply.NetworkError;
    }

    static Reply TryGet(string url, out string text)
    {
        text = null;
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = (int)TimeoutSeconds;
            var op = req.SendWebRequest();
            double t0 = EditorApplication.timeSinceStartup;
            while (!op.isDone)
            {
                if (EditorApplication.timeSinceStartup - t0 > TimeoutSeconds + 5) { req.Abort(); break; }
                System.Threading.Thread.Sleep(50); // тільки в редакторі; у грі так не можна
            }
            if (req.result == UnityWebRequest.Result.Success)
            {
                text = req.downloadHandler.text;
                return Reply.Ok;
            }
            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                if (req.responseCode == 429) _rateLimited = true;
                Debug.LogError("DonkiCheckTool: сервер відповів помилкою HTTP " + req.responseCode + ": " + url);
                return Reply.HttpError;
            }
            Debug.LogWarning("DonkiCheckTool: немає зв'язку (" + req.error + "): " + url);
            return Reply.NetworkError;
        }
    }
}
#endif
