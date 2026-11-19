// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Що зараз робить корабель поставки біля бази.</summary>
public enum LanderPhase { None, Descending, Landed, Ascending }

/// <summary>
/// Розклад корабля поставки на Місяці (роль 04): коли він сідає на майданчик, скільки стоїть і коли злітає.
/// Тільки рахує — малює SupplyLanderView. Звичайний C#-клас, тож його легко перевірити тестами.
///
/// Час — «годинник світу»: година від початку гри + частка поточної години (0..1).
/// Так корабель рухається плавно, а на паузі (планшет, звіт) стоїть на місці.
///
/// Чому посадка о (ArrivalAt + 1): SupplySystem.Tick для години ArrivalAt спрацьовує,
/// коли ця година минула, — саме тоді вантаж і вивантажують.
/// </summary>
public static class LanderTimeline
{
    public const float DescentHours = 1f; // спуск з неба (≈ 10 с при ×1)
    public const float StayHours = 4f;    // стоїть на майданчику, поруч ящики (≈ 40 с при ×1)
    public const float AscentHours = 1f;  // злітає назад

    /// <summary>Годинник світу: година від початку гри + частка години.</summary>
    public static float Now(GameState s, float hourProgress) =>
        CommsSystem.AbsHour(s) + Math.Max(0f, Math.Min(1f, hourProgress));

    /// <summary>Коли корабель торкається майданчика, якщо SupplySystem вивантажує його в годину tickHour.</summary>
    public static int TouchdownOf(int tickHour) => tickHour + 1;

    /// <summary>
    /// Фаза корабля зараз і прогрес у ній t (0..1).
    /// Спуск того, що летить, важливіший за зліт попереднього (якщо вони раптом збіглися).
    /// </summary>
    public static LanderPhase Phase(SupplyState sup, float now, out float t)
    {
        bool flying = sup.Status == SupplyStatus.Launched || sup.Status == SupplyStatus.Returned;
        float incoming = flying ? TouchdownOf(sup.ArrivalAt) : float.NaN;
        return Phase(incoming, sup.LanderLanded, sup.LanderLandedAt, now, out t);
    }

    /// <summary>Показ для перевірки (кнопка в DebugPanel): корабель починає спуск о start, логіка гри не змінюється.</summary>
    public static LanderPhase Preview(float start, float now, out float t)
    {
        float touch = start + DescentHours;
        // після посадки він уже не «летить сюди», інакше стояв би на землі вічно (як справжній, що чекає вивантаження)
        return Phase(now < touch ? touch : float.NaN, true, touch, now, out t);
    }

    /// <summary>incomingTouch — коли сяде той, що летить (NaN — нічого не летить).</summary>
    static LanderPhase Phase(float incomingTouch, bool landed, float landedAt, float now, out float t)
    {
        t = 0f;
        // летить сюди: останню годину перед посадкою спускається (і стоїть на землі, поки логіка не вивантажила)
        if (!float.IsNaN(incomingTouch) && now >= incomingTouch - DescentHours)
        {
            t = Clamp01((now - (incomingTouch - DescentHours)) / DescentHours);
            return LanderPhase.Descending;
        }
        if (!landed) return LanderPhase.None;

        float since = now - landedAt;
        if (since < 0f) return LanderPhase.None;
        if (since < StayHours) { t = since / StayHours; return LanderPhase.Landed; }
        since -= StayHours;
        if (since < AscentHours) { t = since / AscentHours; return LanderPhase.Ascending; }
        return LanderPhase.None;
    }

    static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
}
