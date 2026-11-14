// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>Фаза бурі: немає, попередження (буря скоро), буря йде. Додавати значення тільки в кінець.</summary>
public enum StormPhase { None, Warning, Active }

/// <summary>
/// Стан радіації й здоров'я: поточна або наступна буря, дози за день, хворі.
/// Лежить у GameState. Власник: роль 06.
/// Тільки поля, які JsonUtility вміє зберегти: числа, рядки, bool, enum, List, інші [Serializable] класи.
/// </summary>
[Serializable]
public class RadiationState
{
    // --- Розклад бур ---
    public int TrackedChapter;          // для якого розділу рахуємо розклад (0 = ще не почали)
    public int ChapterStartDay = 1;     // з якого дня гри почався розділ
    public int NextStormIndex;          // яку бурю з розкладу розділу чекаємо наступною

    // --- Поточна буря ---
    public StormPhase Phase = StormPhase.None;
    public string StormId = "";         // id події з donki_storms.json
    public int StormStrength;           // сила 1..3
    public int StormStartAt;            // година від початку гри, коли буря почнеться
    public int StormEndAt;              // година від початку гри, коли буря закінчиться
    public int StormsSurvived;          // скільки бур уже минуло

    // --- Для вечірнього звіту і здоров'я ---
    public List<DoseToday> Today = new List<DoseToday>();
    public List<PatientState> Patients = new List<PatientState>();

    /// <summary>Скільки годин до бурі, або -1, якщо бурі не чекаємо.</summary>
    public int HoursUntilStorm(GameState s)
    {
        if (Phase != StormPhase.Warning) return -1;
        return Math.Max(0, StormStartAt - RadiationSystem.AbsHour(s));
    }

    public bool StormActive => Phase == StormPhase.Active;
}

/// <summary>Скільки дози астронавт отримав сьогодні і де.</summary>
[Serializable]
public class DoseToday
{
    public string Name;
    public float Total;
    public float Outside;       // надворі, звичайний день
    public float Inside;        // у модулі
    public float Shielded;      // у модулі під реголітом
    public float StormOutside;  // буря, надворі
    public float StormModule;   // буря, в іншому модулі
    public float StormShelter;  // буря, в укритті з енергією
}

/// <summary>Хворий астронавт: скільки годин без ліків і скільки до одужання.</summary>
[Serializable]
public class PatientState
{
    public string Name;
    public float HoursUntreated;    // скільки годин хворіє без ліків
    public bool Treated;            // уже отримав ліки
    public float RecoverHoursLeft;  // скільки годин до одужання (якщо Treated)
}
