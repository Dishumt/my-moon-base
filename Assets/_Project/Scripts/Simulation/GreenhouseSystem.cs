// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Теплиця: ріст від енергії й води, урожай кожні кілька днів.
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class GreenhouseSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): ріст від блоків, теплиця п'є воду; урожай — в OnDayEnd
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
