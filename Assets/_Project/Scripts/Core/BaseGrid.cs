// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Сітка бази: квадратики, у кожен влазить один модуль. ModuleState.SlotId = номер квадратика.
/// Нумерація: 0 — лівий нижній (ближче до камери), далі вправо по рядку, потім наступний рядок:
///   рядок 1:  6  7  8  9 10 11
///   рядок 0:  0  1  2  3  4  5
/// Центр сітки стоїть у точці (0,0) світу.
/// Звичайний C#-клас: ним користуються Світ (01), Будівництво (03) і Планшет (05).
/// </summary>
public static class BaseGrid
{
    public const int Width = 6;        // квадратиків по X (зліва направо)
    public const int Depth = 6;        // квадратиків по Z (від камери вглиб)
    public const float CellSize = 8f;  // метрів: модуль 6 м + прохід 2 м

    /// <summary>Скільки всього квадратиків.</summary>
    public const int Count = Width * Depth;

    /// <summary>Чи такий номер квадратика існує.</summary>
    public static bool IsValid(int slotId) => slotId >= 0 && slotId < Count;

    public static int Column(int slotId) => slotId % Width;
    public static int Row(int slotId) => slotId / Width;

    /// <summary>Номер квадратика за стовпцем і рядком; -1, якщо за межами.</summary>
    public static int ToSlot(int column, int row)
    {
        if (column < 0 || row < 0 || column >= Width || row >= Depth) return -1;
        return row * Width + column;
    }

    /// <summary>Центр квадратика у світі (y = 0).</summary>
    public static Vector3 ToWorld(int slotId)
    {
        float x = (Column(slotId) - (Width - 1) * 0.5f) * CellSize;
        float z = (Row(slotId) - (Depth - 1) * 0.5f) * CellSize;
        return new Vector3(x, 0f, z);
    }

    /// <summary>У якому квадратику стоїть точка світу; -1 = поза базою.</summary>
    public static int FromWorld(Vector3 pos)
    {
        int col = Mathf.FloorToInt(pos.x / CellSize + Width * 0.5f);
        int row = Mathf.FloorToInt(pos.z / CellSize + Depth * 0.5f);
        return ToSlot(col, row);
    }

    /// <summary>
    /// Чи можна прокласти тунель від кімнати slotA до сусідньої праворуч (slotA + 1) в тому ж ряду.
    /// Тільки праворуч-ліворуч: спереду в модуля шлюз, тож спереду й ззаду тунель не приєднати.
    /// null = можна, інакше ключ тексту з причиною.
    /// </summary>
    public static string CanConnect(GameState s, int slotA)
    {
        if (!IsValid(slotA) || Column(slotA) >= Width - 1) return "error.tunnel_place";
        var a = s.FindModule(slotA);
        var b = s.FindModule(slotA + 1);
        if (a == null || b == null || !ModuleKinds.IsRoom(a.Type) || !ModuleKinds.IsRoom(b.Type)) return "error.tunnel_rooms";
        if (!a.IsBuilt || !b.IsBuilt) return "error.tunnel_not_built";
        if (s.FindTunnel(slotA) != null) return "error.tunnel_exists";
        return null;
    }

    /// <summary>
    /// Чи можна будувати в цьому квадратику. null = можна,
    /// інакше ключ тексту з причиною (для Loc.Get).
    /// </summary>
    public static string CanPlace(GameState s, int slotId)
    {
        if (!IsValid(slotId)) return "error.place_outside";
        if (s.FindModule(slotId) != null) return "error.place_taken";
        return null;
    }
}
