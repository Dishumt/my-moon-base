// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Тунель між двома сусідніми кімнатами в ОДНОМУ ряду сітки бази: SlotA (ліворуч) і SlotA + 1 (праворуч).
/// Крізь тунель екіпаж ходить без скафандра й без виходу надвір — менше радіації і не треба чекати в шлюзі.
/// Будують роботи (ConstructionSystem.OrderTunnel, роль 03), малює Світ (TunnelView, роль 01).
/// </summary>
[Serializable]
public class TunnelState
{
    public int SlotA;               // лівий модуль; правий — SlotA + 1
    public float BuildHoursLeft;    // скільки годин роботи роботів лишилось; 0 = готово

    public int SlotB => SlotA + 1;
    public bool IsBuilt => BuildHoursLeft <= 0f;
    public bool Connects(int slot) => slot == SlotA || slot == SlotB;
}
