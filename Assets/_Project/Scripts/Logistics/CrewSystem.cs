// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Завдання екіпажу: хто що робить і що це дає (лід → вода, зразки → наука).
/// Власник: роль 04. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class CrewSystem : ISimSystem
{
    /// <summary>
    /// Дати завдання астронавту. targetId — id точки або номер слота.
    /// Повертає null, якщо все добре, або ключ тексту з причиною (для Loc.Get).
    /// Цей метод викликає Інтерфейс (05).
    /// </summary>
    public string Assign(Astronaut astronaut, CrewTask task, string targetId)
    {
        // TODO (04): перевірити InsideOnly / Severe; поставити Task і TaskTarget; AtTaskSite = false
        return "error.not_implemented";
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (04): якщо AtTaskSite — робота дає результат (спеціаліст ×2, Sick ×0.5)
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (04): що рахується раз на день (якщо потрібно)
    }
}
