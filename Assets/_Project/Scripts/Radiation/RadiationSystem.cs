// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Радіація: доза щогодини, сонячні бурі.
/// Власник: роль 06. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class RadiationSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (06): доза кожному астронавту (надворі більше), пороги InsideOnly/GoingHome
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (06): що рахується раз на день (якщо потрібно)
    }
}
