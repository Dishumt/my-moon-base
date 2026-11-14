// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Швидкість одного завдання з Launch/tasks.json.</summary>
[Serializable]
public class TaskRateData
{
    public string task;        // назва CrewTask текстом, напр. "MineIce"
    public string specialist;  // CrewRole текстом або "" — немає спеціаліста
    public float perDay;       // скільки дає за день звичайний астронавт
    public bool outside;       // завдання надворі
}

/// <summary>Увесь файл tasks.json.</summary>
[Serializable]
public class TasksData
{
    public float specialistMultiplier = 2f;
    public float sickMultiplier = 0.5f;
    public bool labNeedsPower = true;
    public List<TaskRateData> items = new List<TaskRateData>();

    public TaskRateData Find(CrewTask task)
    {
        string name = task.ToString();
        return items.Find(i => i.task == name);
    }

    public static TasksData Load() => DataLoader.Load<TasksData>("Launch/tasks");
}
