// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Запаси бази. Клас НЕ називаємо Resources — це ім'я вже зайняте Unity.
/// float — число з дробовою частиною (наприклад 12.5), бо ресурси змінюються потроху щогодини.
/// </summary>
[Serializable]
public class ResourceState
{
    public float Battery;   // заряд батареї, у блоко-годинах
    public float Water;     // вода
    public float Oxygen;    // кисень
    public float Food;      // їжа, у порціях
    public float Materials; // будматеріали
    public int MedKits;     // аптечки (ліки), цілі штуки
}
