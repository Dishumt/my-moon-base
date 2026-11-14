// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Стан зв'язку: вікна зв'язку.
/// Лежить у GameState. Власник: роль 04.
/// </summary>
[Serializable]
public class CommsState
{
    public bool IsOpen;          // чи відкрите вікно зараз (оновлює CommsSystem щогодини)
    public int ExtraOpenHours;   // на скільки годин довше вікно (ретранслятор, розділ 4)
}
