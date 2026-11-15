// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>
/// Спільна основа часу для логістики (роль 04): усе рахується від ДОВЖИНИ РОЗДІЛУ.
/// Довжина розділу = ціль «SurviveDays» у chapter_NN.json (зона 03). Немає такої цілі — fallbackChapterDays з launch.json.
///
/// Навіщо: якщо техлід чи 03 змінять темп гри (довжину дня в секундах чи кількість днів у розділі),
/// поставки, політ, затримки й вікна зв'язку підлаштуються самі — у launch.json і rockets.json лежать
/// не години, а ЧАСТКИ («3 кораблі за розділ», «політ = 0.3 періоду»).
/// realSecondsPerDay (скільки триває день у секундах) на логістику не впливає: усе в ігрових годинах.
/// </summary>
public static class LogisticsTime
{
    /// <summary>Для тестів: підставити довжину розділу в днях. null — брати з ChapterSystem.</summary>
    public static Func<float> ChapterDaysOverride;

    /// <summary>
    /// Скільки ігрових днів триває поточний розділ — для логістики, тобто не довше за maxChapterDays.
    /// Межа потрібна, щоб у довгому розділі кораблі не рідшали без кінця: тоді запасів до першої поставки
    /// треба стільки, що разом із наборами модулів вони не влізуть у жодну ракету. Довгий розділ = просто більше кораблів.
    /// </summary>
    public static float ChapterDays(LaunchData data)
    {
        float days = Math.Max(0.25f, GoalDays(data));
        if (data != null && data.maxChapterDays > 0f) days = Math.Min(days, data.maxChapterDays);
        return days;
    }

    /// <summary>Довжина розділу з цілі SurviveDays (без межі maxChapterDays).</summary>
    static float GoalDays(LaunchData data)
    {
        if (ChapterDaysOverride != null) return ChapterDaysOverride();

        var game = GameBootstrap.Instance;
        var chapters = game != null ? game.GetSystem<ChapterSystem>() : null;
        var goals = chapters != null && chapters.Current != null ? chapters.Current.goals : null;
        if (goals != null)
            foreach (var g in goals)
                if (g != null && g.type == "SurviveDays" && g.target > 0f) return g.target;

        return data != null && data.fallbackChapterDays > 0f ? data.fallbackChapterDays : 3f;
    }

    /// <summary>Скільки ігрових годин триває поточний розділ.</summary>
    public static float ChapterHours(LaunchData data) => ChapterDays(data) * 24f;

    /// <summary>Округлити години до цілих, не менше min.</summary>
    public static int Hours(float hours, int min = 1) => Math.Max(min, (int)Math.Round(hours));
}
