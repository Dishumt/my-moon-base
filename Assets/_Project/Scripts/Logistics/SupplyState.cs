// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;

/// <summary>
/// Стан замовлення. Шлях: Draft (чернетка) → Sent (відправлено у вікно зв'язку)
/// → Launched (летить) або Returned (повернулось, прилетить на 3 дні пізніше) → прибуло → нова Draft.
/// Додавати значення тільки в кінець.
/// </summary>
public enum SupplyStatus { Draft, Sent, Launched, Returned }

/// <summary>
/// Стан поставок: що замовлено, коли прилетить.
/// Лежить у GameState. Власник: роль 04.
/// </summary>
[Serializable]
public class SupplyState
{
    public SupplyStatus Status = SupplyStatus.Draft;
    public List<CargoLine> Cargo = new List<CargoLine>();       // вантаж поточного замовлення
    public float FuelKg = -1f;                                  // -1 = ще не задано, візьмемо startFuelKg
    public List<string> Passengers = new List<string>();        // імена людей з резерву, що летять на заміну

    public bool FirstMissionDone;   // перша місія (з екіпажем) долетіла
    public bool Scheduled;          // розклад уже створено
    public int NextLaunchAt;        // година (від початку гри), коли старт за розкладом
    public int ArrivalAt;           // година прибуття поточного польоту
    public bool MissedReported;     // уже писали у звіт, що замовлення не відправлене вчасно

    public int Deliveries;          // скільки поставок прибуло
    public int LastArrivalDay;      // день останнього прибуття
    public bool LastLaunchFailed;   // останній запуск був невдалий

    // Набори, які приїхали і чекають на будівництво (03 може їх використати)
    public int SolarPanelKits;
    public int BatteryKits;

    public List<string> WentHome = new List<string>(); // хто полетів додому

    /// <summary>Скільки штук цього вантажу в замовленні.</summary>
    public int Count(string itemId)
    {
        var line = Cargo.Find(l => l.itemId == itemId);
        return line != null ? line.count : 0;
    }
}
