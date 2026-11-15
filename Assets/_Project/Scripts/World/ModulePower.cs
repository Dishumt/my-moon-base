// AI-assisted: written with Claude. See docs/AI_USE.md

/// <summary>Стан енергії в кімнаті.</summary>
public enum RoomPower
{
    On,       // енергія є — світло горить, усе працює
    Standby,  // гравець сам не дав кімнаті енергії (план = 0) — «сплячий режим»: напівтемно, без тривог
    Outage,   // енергію дали, але її забракло (батарея порожня, тінь) — темно, блимає червона аварійна лампа
}

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
    /// <summary>Кнопка DebugPanel «Аварія»: світ вважає, що енергії немає ніде (план не змінюється). Тільки для перевірки.</summary>
    public static bool DebugOutage;

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
            DebugOutage = false;
        }
        if (DebugOutage) return 0;

        bool ticked = s.Day != startDay || s.Hour != startHour; // минула хоч одна година
        PowerPlan p = ticked && power != null ? power.Granted : s.Power;
        return Of(p, type);
    }

    /// <summary>Скільки блоків гравець запланував кімнаті цього типу (план у планшеті).</summary>
    public static int Planned(ModuleType type)
    {
        var game = GameBootstrap.Instance;
        return game == null || game.State == null ? 0 : Of(game.State.Power, type);
    }

    /// <summary>Чи є в кімнаті енергія.</summary>
    public static bool HasPower(ModuleType type) => Blocks(type) > 0;

    /// <summary>Скільки блоків отримало життєзабезпечення (з них робиться кисень).</summary>
    public static int LifeSupportBlocks() => Blocks(ModuleType.Habitat);

    /// <summary>Увімкнена, «спить» (гравець не дав енергії) чи аварія (енергію дали, але забракло).</summary>
    public static RoomPower StateOf(ModuleType type)
    {
        if (Blocks(type) > 0) return RoomPower.On;
        return Planned(type) > 0 ? RoomPower.Outage : RoomPower.Standby;
    }

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
