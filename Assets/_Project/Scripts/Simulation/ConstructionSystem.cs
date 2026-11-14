// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Будівництво модулів роботами і засипання реголітом.
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class ConstructionSystem : ISimSystem
{
    /// <summary>
    /// Замовити модуль у слоті. Повертає null, якщо все добре,
    /// або ключ тексту з причиною, чому не можна (для Loc.Get).
    /// Цей метод викликає Інтерфейс (05).
    /// </summary>
    public string Order(ModuleType type, int slotId)
    {
        // TODO (03): перевірити розділ, слот, матеріали; зняти матеріали; додати ModuleState; дати роботам TargetSlot
        return "error.not_implemented";
    }

    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): роботи з енергією зменшують BuildHoursLeft (×2, якщо EngineerNearby)
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
