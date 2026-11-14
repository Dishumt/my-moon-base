// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;

/// <summary>
/// Події гри. Подія — це "дзвіночок": хтось дзвонить (Raise...), а всі, хто підписався (+=), дізнаються.
/// Підписуйся в OnEnable:  GameEvents.DayEnded += ShowReport;
/// Відписуйся в OnDisable: GameEvents.DayEnded -= ShowReport;
/// </summary>
public static class GameEvents
{
    public static event Action HourPassed;               // минула ігрова година
    public static event Action<DayReport> DayEnded;      // закінчився день, ось звіт
    public static event Action StateChanged;             // щось у GameState змінилось — онови екран
    public static event Action<string> StormWarning;     // наближається буря (id події з даних NASA)
    public static event Action<string> StormStarted;     // буря почалась
    public static event Action<bool> LaunchFinished;     // запуск завершено: true = долетів
    public static event Action SupplyArrived;            // поставка прибула
    public static event Action<int> ChapterCompleted;    // розділ пройдено (номер)
    public static event Action<string> GameOver;         // поразка (ключ тексту з причиною)

    public static void RaiseHourPassed() => HourPassed?.Invoke();
    public static void RaiseDayEnded(DayReport r) => DayEnded?.Invoke(r);
    public static void RaiseStateChanged() => StateChanged?.Invoke();
    public static void RaiseStormWarning(string stormId) => StormWarning?.Invoke(stormId);
    public static void RaiseStormStarted(string stormId) => StormStarted?.Invoke(stormId);
    public static void RaiseLaunchFinished(bool success) => LaunchFinished?.Invoke(success);
    public static void RaiseSupplyArrived() => SupplyArrived?.Invoke();
    public static void RaiseChapterCompleted(int chapter) => ChapterCompleted?.Invoke(chapter);
    public static void RaiseGameOver(string reasonKey) => GameOver?.Invoke(reasonKey);

    /// <summary>Прибрати всіх підписників. Unity викликає це сам перед кожним запуском Play.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ClearAll()
    {
        HourPassed = null; DayEnded = null; StateChanged = null;
        StormWarning = null; StormStarted = null; LaunchFinished = null;
        SupplyArrived = null; ChapterCompleted = null; GameOver = null;
    }
}
