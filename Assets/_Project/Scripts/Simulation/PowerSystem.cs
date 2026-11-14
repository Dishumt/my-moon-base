// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Енергія: блоки від сонячних панелей, розподіл за PowerPlan, батарея.
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class PowerSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): блоки від панелей (менше в тіні), розподіл s.Power, надлишок у батарею, нестача з батареї
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
