// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// "NASA data in the game" (start menu): which real data the game uses, where it comes from, and LIVE / ARCHIVE.
///  - space weather today: NASA DONKI asked right now (LIVE) or a saved past event (ARCHIVE);
///  - the solar storms of the game: real 2024 events from NASA DONKI (ARCHIVE) with dates and links;
///  - radiation dose: NASA published values (ARCHIVE) and how much the game speeds time up;
///  - facts from NASA space biology studies (OSDR).
/// Reads the files of role 06 (Resources/Data/Nasa). Not a MonoBehaviour: StartMenu creates it.
/// </summary>
public class NasaDataView
{
    readonly VisualElement stage;
    readonly ScrollView list;

    public NasaDataView(VisualElement stage, System.Action onClose)
    {
        this.stage = stage;
        list = stage.Q<ScrollView>("data-list");
        var close = stage.Q<Button>("data-close");
        if (close != null) close.clicked += () => { Hide(); onClose?.Invoke(); };
        Hide();
    }

    public void Hide() => UiKit.Show(stage, false);

    public void Show()
    {
        UiKit.SetText(stage.Q<Label>("data-title"), Loc.Get("data.title"));
        UiKit.SetText(stage.Q<Button>("data-close"), Loc.Get("data.close"));
        Build();
        UiKit.Show(stage, true);
    }

    void Build()
    {
        list.Clear();

        // 1. space weather today
        Section(Loc.Get("weather.title"), SpaceWeatherFetcher.Latest != null ? SpaceWeatherFetcher.Latest.Badge : "…");
        var w = SpaceWeatherFetcher.Latest;
        Line(w != null ? Loc.Get(w.TextKey, w.ClassType, w.TimeUtc) : Loc.Get("menu.weather_wait"), w != null ? w.SourceUrl : null);
        Line(Loc.Get("data.live_explain"), null, true);

        // 2. the solar storms of the game
        var storms = DonkiStormList.Load();
        Section(Loc.Get("data.storms_title"), "ARCHIVE");
        foreach (var e in storms.items)
        {
            string kind = Loc.Get("data.type." + e.type);
            string cls = string.IsNullOrEmpty(e.classType) ? "" : " " + e.classType;
            Line(Loc.Get("data.storm_line", e.Day, kind + cls, e.strength), e.sourceUrl);
        }

        // 3. radiation dose
        var dose = RadLabDose.Load();
        var rad = RadiationBalance.Load();
        Section(Loc.Get("data.dose_title"), "ARCHIVE");
        Line(Loc.Get("data.dose_text", UiKit.Num(dose.dailyDose_mSv), UiKit.Num(dose.careerLimit_mSv), UiKit.Num(rad.timeCompression)), null);
        Line(Loc.Get("data.msv"), null, true);
        foreach (var src in dose.sources) Line(src.name, src.url, true);

        // 4. facts from NASA studies
        var facts = NasaFactList.Load();
        Section(Loc.Get("data.facts_title"), "ARCHIVE");
        foreach (var f in facts.items)
            Line(f.Text + (string.IsNullOrEmpty(f.studyId) ? "" : "  (" + f.studyId + ")"), f.sourceUrl);
    }

    void Section(string title, string badge)
    {
        var row = UiKit.Box(list, "data-section");
        UiKit.Label(row, "data-section__title", title);
        UiKit.Label(row, "menu-badge" + (badge == "LIVE" ? " menu-badge--live" : ""), badge);
    }

    void Line(string text, string url, bool muted = false)
    {
        var box = UiKit.Box(list, "data-line" + (muted ? " data-line--muted" : ""));
        UiKit.Label(box, "data-line__text", text);
        if (string.IsNullOrEmpty(url)) return;
        // the link opens in a new browser tab; the address is shown too, so it can be typed in
        var link = UiKit.Button(box, "data-link", () => Application.OpenURL(url), url);
        link.tooltip = url;
    }
}
