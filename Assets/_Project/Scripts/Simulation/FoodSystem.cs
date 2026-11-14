// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Їжа й ситість астронавтів.
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class FoodSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): ситість падає, астронавт їсть із s.Res.Food
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
