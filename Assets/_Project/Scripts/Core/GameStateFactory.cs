// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;

/// <summary>Створює новий GameState зі стартових даних розділу.</summary>
public static class GameStateFactory
{
    public static GameState FromChapter(ChapterStartData data)
    {
        var s = new GameState
        {
            Day = 1,
            Hour = data.startHour,
            Chapter = data.chapter,
            Res = data.resources ?? new ResourceState(),
            Power = data.power ?? new PowerPlan(),
        };

        foreach (var c in data.crew) s.Crew.Add(MakeAstronaut(c));
        foreach (var c in data.reserve) s.Reserve.Add(MakeAstronaut(c));
        for (int i = 0; i < data.robots; i++) s.Robots.Add(new RobotState());
        foreach (var p in data.pois)
        {
            s.Pois.Add(new PoiState { Id = p.id, Type = ParseEnum(p.type, PoiType.Sample), Amount = p.amount });
        }
        return s;
    }

    static Astronaut MakeAstronaut(CrewStart c)
    {
        return new Astronaut { Name = c.name, Role = ParseEnum(c.role, CrewRole.Engineer) };
    }

    /// <summary>Текст "Medic" → CrewRole.Medic. Якщо помилка в JSON — попередження і значення за замовчуванням.</summary>
    static T ParseEnum<T>(string text, T fallback) where T : struct
    {
        if (Enum.TryParse(text, out T value)) return value;
        Debug.LogWarning("GameStateFactory: невідоме значення '" + text + "' для " + typeof(T).Name);
        return fallback;
    }
}
