// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Розділи: цілі, перемога (ChapterCompleted) і поразка (GameOver).
/// Власник: роль 03. Поки що заготовка: нічого не робить, але компілюється.
/// </summary>
public class ChapterSystem : ISimSystem
{
    public void Tick(GameState s, float hours, DayReport r)
    {
        // TODO (03): перевірити цілі розділу; кисень 0 або 2 дні без їжі → GameEvents.RaiseGameOver
    }

    public void OnDayEnd(GameState s, DayReport r)
    {
        // TODO (03): що рахується раз на день (якщо потрібно)
    }
}
