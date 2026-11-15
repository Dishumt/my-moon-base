// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Тунель між двома кімнатами в ОДНОМУ ряду сітки бази: SlotA (ліворуч) і SlotA + Length (праворуч).
/// Length = 1 — сусіди; більше — тунель іде крізь порожні квадратики між ними (у них тоді нічого не збудуєш).
/// Крізь тунель екіпаж ходить без скафандра й без виходу надвір — менше радіації і не треба чекати в шлюзі.
/// Будують роботи (ConstructionSystem.OrderTunnel, роль 03), малює Світ (TunnelView, роль 01).
/// </summary>
[Serializable]
public class TunnelState
{
    public int SlotA;               // лівий модуль; правий — SlotA + Length
    public int Length = 1;          // на скільки квадратиків праворуч (1 = сусід)
    public float BuildHoursLeft;    // скільки годин роботи роботів лишилось; 0 = готово

    public int SlotB => SlotA + Math.Max(1, Length);
    public bool IsBuilt => BuildHoursLeft <= 0f;
    public bool Connects(int slot) => slot == SlotA || slot == SlotB;
    /// <summary>Чи квадратик лежить ПІД тунелем (між кімнатами).</summary>
    public bool Covers(int slot) => slot > SlotA && slot < SlotB;
}
