// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Життєзабезпечення: кисень з води та енергії.
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class LifeSupportSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): кисень = блоки × норма, поки є вода; екіпаж дихає; тривога у звіті
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
