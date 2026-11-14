// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Поставки із Землі: розклад, замовлення, прибуття.
/// Власник: роль 04. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class SupplySystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (04): стан замовлення, прибуття ресурсів і людей
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (04): що рахується раз на день (якщо потрібно)
    }
}
