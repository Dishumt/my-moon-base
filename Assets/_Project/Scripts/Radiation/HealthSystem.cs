// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Здоров'я й ліки.
/// Власник: роль 06. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class HealthSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (06): шанс захворіти, ліки, одужання
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (06): що рахується раз на день (якщо потрібно)
    }
}
