// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Тінь від хребта. Біля південного полюса Сонце ходить низько над горизонтом і частину доби ховається за горами
/// (balance.json → power.shadeHoursPerDay; енергію в ці години рахує PowerSystem, роль 03).
/// Світ показує це наочно, щоб дитина бачила, ЧОМУ панелі дають менше енергії:
///  - за годину до тіні світ плавно сутеніє (Сонце слабшає до 55%, екран трохи синіє — сутінки);
///  - на сонячних панелях зникає відблиск, на батареях видно, що вони розряджаються (EquipmentLife);
///  - на початку нової доби Сонце виходить з-за гір — світ знову яскравий.
/// Ставить себе сам (ModuleLook додає на World).
/// </summary>
public class ShadeFx : MonoBehaviour
{
    /// <summary>Скільки світла лишається в тіні.</summary>
    public const float ShadeLight = 0.55f;

    /// <summary>0 — Сонце світить, 1 — повна тінь від хребта (плавно між ними).</summary>
    public static float Amount { get; private set; }

    float shadeHours = -1f;
    float outside = 1f; // синюватий відтінок — тільки коли інженер надворі (у модулі своє світло)

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || game.Clock == null) return;
        if (shadeHours < 0f)
        {
            var b = DataLoader.Load<SimBalanceData>("Balance/balance");
            shadeHours = b != null && b.power != null ? b.power.shadeHoursPerDay : 0f;
        }

        // Час доби з дробами (14,5 = 14:30), щоб сутеніло плавно, а не стрибком щогодини
        float t = game.State.Hour + Mathf.Clamp01(game.Clock.HourProgress);
        float start = 24f - shadeHours; // тінь — останні години доби (як у PowerSystem.IsShade)
        float a;
        if (shadeHours <= 0f) a = 0f;
        else if (t >= start) a = 1f;
        else if (t >= start - 1f) a = t - (start - 1f); // за годину до тіні — сутеніє
        else if (t < 0.5f) a = 1f - t / 0.5f;           // нова доба — Сонце виходить з-за гір
        else a = 0f;
        a = a * a * (3f - 2f * a);
        Amount = a;
        OutdoorDarkening.Shade = Mathf.Lerp(1f, ShadeLight, a);

        outside = Mathf.MoveTowards(outside, ModuleInterior.EngineerInsideAny ? 0f : 1f, Time.unscaledDeltaTime * 2.5f);
        var overlay = ScreenOverlay.Get();
        if (overlay != null) overlay.ShadeTint = new Color(0.22f, 0.32f, 0.75f, 0.13f * a * outside);
    }
}
