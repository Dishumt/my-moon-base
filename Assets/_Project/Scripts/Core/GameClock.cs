// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;

/// <summary>
/// Ігровий годинник. Рахує час, пауза, швидкість ×1/×2.
/// Щогодини гри викликає Tick у всіх систем (у порядку реєстрації),
/// а наприкінці дня — OnDayEnd і подію GameEvents.DayEnded.
/// Звичайний C#-клас (не MonoBehaviour), тому його легко тестувати.
/// Реальний час йому передає GameBootstrap через Update(Time.deltaTime).
/// </summary>
public class GameClock
{
    readonly GameState state;
    readonly List<ISimSystem> systems = new List<ISimSystem>();

    float hourProgress;   // скільки вже минуло від поточної години: 0..1
    int modalCount;       // скільки вікон зараз просять паузу (планшет, звіт...)

    /// <summary>Скільки реальних секунд триває ігровий день (з balance.json).</summary>
    public float RealSecondsPerDay { get; set; }

    /// <summary>Швидкість: 1 = ×1, 2 = ×2.</summary>
    public float Speed { get; private set; } = 1f;

    /// <summary>Гравець натиснув паузу.</summary>
    public bool UserPaused { get; private set; }

    /// <summary>Чи стоїть час: пауза гравця або відкрите вікно (планшет, звіт).</summary>
    public bool IsPaused => UserPaused || modalCount > 0;

    /// <summary>Частка поточної години 0..1 — для плавного годинника в HUD.</summary>
    public float HourProgress => hourProgress;

    /// <summary>Звіт поточного дня. Системи пишуть у нього через параметр r.</summary>
    public DayReport Report { get; private set; }

    public GameClock(GameState state, float realSecondsPerDay)
    {
        this.state = state;
        RealSecondsPerDay = realSecondsPerDay > 0f ? realSecondsPerDay : 180f;
        Report = new DayReport(state.Day);
    }

    /// <summary>Додати систему. Порядок реєстрації = порядок Tick.</summary>
    public void Register(ISimSystem system) => systems.Add(system);

    /// <summary>Усі зареєстровані системи (тільки читати).</summary>
    public IReadOnlyList<ISimSystem> Systems => systems;

    // ---- Керування часом ----
    public void Pause() { UserPaused = true; GameEvents.RaiseStateChanged(); }
    public void Resume() { UserPaused = false; GameEvents.RaiseStateChanged(); }
    public void TogglePause() { if (UserPaused) Resume(); else Pause(); }
    public void SetSpeed(float speed) { Speed = speed; UserPaused = false; GameEvents.RaiseStateChanged(); }

    /// <summary>Відкрилось вікно, яке ставить гру на паузу (планшет, звіт). Обов'язково потім PopModal().</summary>
    public void PushModal() { modalCount++; }

    /// <summary>Вікно закрилось.</summary>
    public void PopModal() { if (modalCount > 0) modalCount--; }

    /// <summary>Викликати щокадру з реальним часом у секундах.</summary>
    public void Update(float realSeconds)
    {
        if (IsPaused || realSeconds <= 0f) return;

        hourProgress += realSeconds * Speed * 24f / RealSecondsPerDay;
        while (hourProgress >= 1f)
        {
            hourProgress -= 1f;
            RunOneHour();
            if (IsPaused) break; // хтось відкрив вікно (наприклад, вечірній звіт) — далі не йдемо
        }
    }

    /// <summary>Промотати N годин одразу (для DebugPanel і тестів). Працює навіть на паузі.</summary>
    public void AdvanceHours(int hours)
    {
        for (int i = 0; i < hours; i++) RunOneHour();
    }

    /// <summary>Одна ігрова година: Tick усіх систем, а о 24:00 — кінець дня.</summary>
    void RunOneHour()
    {
        foreach (var system in systems) system.Tick(state, 1f, Report);

        state.Hour++;
        GameEvents.RaiseHourPassed();

        if (state.Hour >= 24)
        {
            foreach (var system in systems) system.OnDayEnd(state, Report);

            var finished = Report;
            state.Hour = 0;
            state.Day++;
            Report = new DayReport(state.Day);
            GameEvents.RaiseDayEnded(finished);
        }

        GameEvents.RaiseStateChanged();
    }
}
