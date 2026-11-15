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
/// Якщо kauai.ccmc.gsfc.nasa.gov не відповідає, пробує дзеркало api.nasa.gov (DEMO_KEY, ліміт запитів на годину).
/// Якщо NASA не відповіла ЖОДНОГО разу (немає мережі, ліміт), файл НЕ перезаписується.
/// </summary>
public static class DonkiCheckTool
{
    const string FilePath = "Assets/_Project/Resources/Data/Nasa/donki_storms.json";
    const double TimeoutSeconds = 30;
    const string MirrorBase = "https://api.nasa.gov/DONKI/";  // те саме API DONKI, через api.nasa.gov
    const string MirrorKey = "DEMO_KEY";                       // публічний демо-ключ, не секрет

    static int answered;      // скільки запитів отримали відповідь від NASA (за один запуск меню)
    static bool rateLimited;  // api.nasa.gov сказав «забагато запитів» (429)

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

    [MenuItem("Moon/06: Перевірити дані DONKI")]
    static void CheckAll()
    {
        if (!File.Exists(FilePath))
        {
            Debug.LogError("DonkiCheckTool: немає файлу " + FilePath);
            return;
        }
        var list = JsonUtility.FromJson<DonkiStormList>(File.ReadAllText(FilePath));
        int ok = 0;
        answered = 0;
        rateLimited = false;
        try
        {
            for (int i = 0; i < list.items.Count; i++)
            {
                var e = list.items[i];
                EditorUtility.DisplayProgressBar("DONKI", e.id, (float)i / list.items.Count);
                if (Check(e)) ok++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Жоден запит не дійшов до NASA: перевірки не було, тож файл не чіпаємо.
        if (answered == 0)
        {
            Debug.LogError("DonkiCheckTool: NASA DONKI недоступний, спробуй з іншої мережі. Файл не змінено."
                + (rateLimited ? " (api.nasa.gov: вичерпано ліміт DEMO_KEY, спробуй пізніше.)" : ""));
            return;
        }

        list._status = "Checked with NASA DONKI on " + DateTime.UtcNow.ToString("yyyy-MM-dd") + ": "
                     + ok + " of " + list.items.Count + " events verified (verified=true). The rest must be fixed or removed.";
        File.WriteAllText(FilePath, JsonUtility.ToJson(list, true));
        AssetDatabase.Refresh();
        Debug.Log("DonkiCheckTool: перевірено " + ok + " з " + list.items.Count + ". Файл оновлено: " + FilePath);
    }

    static bool Check(DonkiStorm e)
    {
        DateTime day;
        if (!DateTime.TryParse(e.Day, out day))
        {
            Debug.LogWarning("DonkiCheckTool: погана дата у " + e.id);
            return false;
        }
        string query = e.type + "?startDate=" + day.AddDays(-1).ToString("yyyy-MM-dd")
                     + "&endDate=" + day.AddDays(1).ToString("yyyy-MM-dd");
        string text = Download(SpaceWeather.DonkiWs + query);
        if (text == null) text = Download(MirrorBase + query + "&api_key=" + MirrorKey); // дзеркало
        if (text == null) return false; // NASA не відповіла: подію не чіпаємо
        answered++;

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
            return false;
        }

        e.donkiId = best.Id;
        e.date = best.Time;
        if (!string.IsNullOrEmpty(best.link)) e.sourceUrl = best.link;
        e.verified = true;
        Debug.Log("DonkiCheckTool: ✓ " + e.id + " = " + best.Id);
        return true;
    }

    /// <summary>Наскільки різні два часи (у хвилинах); якщо часу немає — 0.</summary>
    static double Distance(string a, string b)
    {
        DateTime ta, tb;
        if (!DateTime.TryParse(a, out ta) || !DateTime.TryParse(b, out tb)) return 0;
        return Math.Abs((ta - tb).TotalMinutes);
    }

    static string Download(string url)
    {
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
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (req.responseCode == 429) rateLimited = true;
                Debug.LogError("DonkiCheckTool: запит не вдався (" + req.error + "): " + url);
                return null;
            }
            return req.downloadHandler.text;
        }
    }
}
#endif
