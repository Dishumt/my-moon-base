// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Вечірній звіт. Кожна система пише сюди, ЩО змінилося і ЧОМУ.
/// Тексту тут немає — тільки ключі для Loc.Get. Наприклад:
///   r.Add("report.oxygen_low", -8f, important: true);
/// </summary>
[Serializable]
public class DayReport
{
    public int Day;
    public List<ReportLine> Lines = new List<ReportLine>();

    public DayReport(int day) { Day = day; }

    /// <summary>Додати рядок у звіт.</summary>
    /// <param name="key">ключ тексту, напр. "report.oxygen_low"</param>
    /// <param name="delta">на скільки змінилось число (можна 0)</param>
    /// <param name="arg">додаткове слово для тексту, напр. ім'я астронавта; підставляється в {0}</param>
    /// <param name="important">true = тривога, показати червоним</param>
    public void Add(string key, float delta = 0f, string arg = "", bool important = false)
    {
        Lines.Add(new ReportLine { Key = key, Delta = delta, Arg = arg, Important = important });
    }
}

/// <summary>Один рядок звіту.</summary>
[Serializable]
public class ReportLine
{
    public string Key;
    public float Delta;
    public string Arg;
    public bool Important;
}
