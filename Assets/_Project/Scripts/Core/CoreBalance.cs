// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Числа з balance.json, які потрібні ядру (Core). Решту чисел у тому самому файлі
/// читає роль 03 своїм класом. JsonUtility просто пропускає поля, яких немає в класі,
/// тому один файл можна читати кількома класами.
/// </summary>
[Serializable]
public class CoreBalance
{
    public float realSecondsPerDay = 180f; // скільки реальних секунд триває ігровий день
}
