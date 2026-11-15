// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>
/// Скільки енергії отримала кімната — для «живих» меблів і світла в модулях.
/// Читаємо PowerSystem.Granted (роль 03): скільки блоків кожна система РЕАЛЬНО отримала в останню ігрову годину
/// (план гравця s.Power може бути більшим, якщо енергії не вистачило).
/// Кімната → система: житловий → життєзабезпечення, теплиця → теплиця, медмодуль → медмодуль,
/// лабораторія → лабораторія, укриття → укриття.
/// Поки не минула перша година гри, Granted ще порожній — тоді показуємо план гравця, щоб на старті все не блимало червоним.
/// </summary>
public static class ModulePower
{
    static GameBootstrap seenGame;
    static PowerSystem power;
    static int startDay, startHour;

    /// <summary>Скільки блоків енергії зараз отримує кімната цього типу. Обладнання без своєї системи — завжди 1.</summary>
    public static int Blocks(ModuleType type)
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return 0;
        var s = game.State;
        if (seenGame != game) // нова гра (або новий запуск Play)
        {
            seenGame = game;
            power = game.GetSystem<PowerSystem>();
            startDay = s.Day;
            startHour = s.Hour;
        }

        bool ticked = s.Day != startDay || s.Hour != startHour; // минула хоч одна година
        PowerPlan p = ticked && power != null ? power.Granted : s.Power;
        return Of(p, type);
    }

    /// <summary>Чи є в кімнаті енергія.</summary>
    public static bool HasPower(ModuleType type) => Blocks(type) > 0;

    /// <summary>Яке поле плану енергії живить кімнату цього типу.</summary>
    public static int Of(PowerPlan p, ModuleType type)
    {
        if (p == null) return 0;
        switch (type)
        {
            case ModuleType.Habitat: return p.LifeSupport;
            case ModuleType.Greenhouse: return p.Greenhouse;
            case ModuleType.Medbay: return p.Medbay;
            case ModuleType.Lab: return p.Lab;
            case ModuleType.Shelter: return p.Shelter;
            case ModuleType.Antenna:
            case ModuleType.Relay: return p.Comms;
            default: return 1;
        }
    }
}
