// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Робить запит до NASA DONKI за останні 7 днів (через UnityWebRequest — так можна у WebGL).
/// Вдалося — Latest.Source = Live. Помилка (CORS у браузері, немає інтернету) — Archive з donki_storms.json.
/// Роль 05: постав цей компонент на об'єкт головного меню і слухай SpaceWeatherFetcher.Updated:
///   label.text = Loc.Get(r.TextKey, r.ClassType, r.TimeUtc) + "  " + r.Badge;
/// Позначку Badge (LIVE / ARCHIVE) показуй завжди.
/// </summary>
public class SpaceWeatherFetcher : MonoBehaviour
{
    [SerializeField] int days = 7;
    [SerializeField] int timeoutSeconds = 10;

    /// <summary>Останній результат (null — ще не завантажено).</summary>
    public static SpaceWeatherResult Latest { get; private set; }

    /// <summary>Подія: результат готовий.</summary>
    public static event Action<SpaceWeatherResult> Updated;

    void Start()
    {
        StartCoroutine(Fetch());
    }

    IEnumerator Fetch()
    {
        string url = SpaceWeather.BuildUrl(DateTime.UtcNow, days);
        using (var req = UnityWebRequest.Get(url))
        {
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();

            SpaceWeatherResult result = null;
            if (req.result == UnityWebRequest.Result.Success)
                result = SpaceWeather.ParseLive(req.downloadHandler.text);
            else
                Debug.Log("SpaceWeatherFetcher: живі дані недоступні (" + req.error + "), показую ARCHIVE.");

            Latest = result ?? SpaceWeather.Archive(DonkiStormList.Load());
            Updated?.Invoke(Latest);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Latest = null; Updated = null; }
}
