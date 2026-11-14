// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Зв'язок із Землею: вікна зв'язку.
/// Власник: роль 04. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class CommsSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (04): чи відкрите вікно зараз; без енергії на антену — закрите
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (04): що рахується раз на день (якщо потрібно)
    }
}
