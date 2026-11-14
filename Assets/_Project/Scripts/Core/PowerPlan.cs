// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// План енергії: скільки блоків енергії гравець дав кожній системі.
/// Діє, поки гравець його не змінить. Рахує енергію PowerSystem (роль 03).
/// </summary>
[Serializable]
public class PowerPlan
{
    public int LifeSupport; // життєзабезпечення (кисень)
    public int Greenhouse;  // теплиця
    public int Comms;       // антена (зв'язок із Землею)
    public int Shelter;     // укриття від радіації
    public int Lab;         // лабораторія
    public int Medbay;      // медмодуль
    public int Robots;      // роботи

    /// <summary>Скільки блоків роздано разом.</summary>
    public int Total()
    {
        return LifeSupport + Greenhouse + Comms + Shelter + Lab + Medbay + Robots;
    }
}
