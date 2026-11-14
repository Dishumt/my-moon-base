// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Спільний "договір" для всіх систем симуляції (енергія, кисень, радіація...).
/// interface — це список методів, які клас ЗОБОВ'ЯЗАНИЙ мати.
/// GameClock викликає Tick щогодини гри і OnDayEnd наприкінці дня.
/// </summary>
public interface ISimSystem
{
    /// <summary>Що змінилося за цей шматок часу (hours — скільки ігрових годин минуло, зазвичай 1).</summary>
    void Tick(GameState s, float hours, DayReport r);

    /// <summary>Що рахується раз на день (урожай, підсумки).</summary>
    void OnDayEnd(GameState s, DayReport r);
}
