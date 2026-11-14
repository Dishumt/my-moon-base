// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;

/// <summary>Одне вікно зв'язку для календаря в планшеті. Години — від початку гри (день 1, 00:00 = 0).</summary>
public struct CommsWindow
{
    public int StartHour;
    public int EndHour;     // не включно
    public int StartDay => StartHour / 24 + 1;
    public int StartHourOfDay => StartHour % 24;
    public int EndDay => EndHour / 24 + 1;
    public int EndHourOfDay => EndHour % 24;
}

/// <summary>
/// Зв'язок із Землею: вікна зв'язку за шаблоном (наприклад, 30 годин є, 18 немає).
/// Без енергії на антену (Power.Comms = 0) вікно закрите.
/// Власник: роль 04.
/// </summary>
public class CommsSystem : ISimSystem
{
    readonly LaunchData data;

    public CommsSystem() : this(LaunchData.Load()) { }
    public CommsSystem(LaunchData data) { this.data = data ?? new LaunchData(); }

    /// <summary>Година від початку гри.</summary>
    public static int AbsHour(GameState s) => (s.Day - 1) * 24 + s.Hour;

    int OpenHours(GameState s) => data.commsOpenHours + (s.Comms != null ? s.Comms.ExtraOpenHours : 0);
    int Period(GameState s) => OpenHours(s) + data.commsClosedHours;

    /// <summary>Чи година потрапляє у вікно за шаблоном (без урахування енергії).</summary>
    public bool InWindow(GameState s, int absHour)
    {
        int period = Period(s);
        if (period <= 0) return true;
        int h = (absHour + data.commsOffsetHours) % period;
        if (h < 0) h += period;
        return h < OpenHours(s);
    }

    /// <summary>Чи є зв'язок зараз: вікно відкрите І антена має енергію.</summary>
    public bool IsOpen(GameState s)
    {
        return s.Power.Comms > 0 && InWindow(s, AbsHour(s));
    }

    /// <summary>Найближчі вікна (поточне теж, якщо воно вже йде) — для календаря.</summary>
    public List<CommsWindow> UpcomingWindows(GameState s, int count)
    {
        var list = new List<CommsWindow>();
        int h = AbsHour(s);
        int limit = h + Period(s) * (count + 1) + 1;
        while (list.Count < count && h < limit)
        {
            if (InWindow(s, h))
            {
                int start = h;
                // знайти початок, якщо вікно почалося раніше
                while (start > 0 && InWindow(s, start - 1) && h - start < Period(s)) start--;
                int end = h;
                while (InWindow(s, end) && end - start < Period(s)) end++;
                list.Add(new CommsWindow { StartHour = start, EndHour = end });
                h = end;
            }
            else h++;
        }
        return list;
    }

    /// <summary>Скільки годин до наступного відкриття (0, якщо відкрито).</summary>
    public int HoursUntilOpen(GameState s)
    {
        int now = AbsHour(s);
        for (int i = 0; i <= Period(s); i++)
            if (InWindow(s, now + i)) return i;
        return 0;
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        bool open = IsOpen(s);
        // попередити один раз, якщо вікно мало б бути, але антені не дали енергії
        if (!open && s.Comms.IsOpen && s.Power.Comms <= 0)
            r.Add("report.comms_no_power", 0f, "", true);
        s.Comms.IsOpen = open;
    }

    public void OnDayEnd(GameState s, DayReport r) { }
}
