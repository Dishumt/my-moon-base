// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Вкладка MAP планшета: схема поверхні зверху, північ угорі.
///  - сіре коло — рівнина всередині хребта, кола-лінійки показують відстань від бази (50, 100, 150 м);
///  - квадратики — модулі бази (жовтий = будується);
///  - ромби — точки на поверхні: кратер з льодом, зразки, стійка з фактом, посадковий майданчик.
///    Поки інженер не підійшов, точка — це «?»: місце відоме, а що там — ні;
///  - цятки — астронавти, біла з блакитною рамкою — сам інженер (гравець);
///  - праворуч список місць з відстанню від гравця: куди йти.
/// Малюється з «живих» об'єктів світу (PoiMarker, BaseView, AstronautView) і з GameState.
/// Не MonoBehaviour: його створює TabletController. Перемальовується, коли планшет відкритий (гра на паузі).
/// </summary>
public class MapTabView
{
    const float AreaPx = 470f;   // розмір квадрата карти в пікселях (як .mp-area в GameTabs.uss)
    const float DefaultRange = 200f;

    readonly VisualElement area;
    readonly Label title, hint, storm;
    readonly ScrollView legend;

    public MapTabView(VisualElement pane)
    {
        area = pane.Q("map-area");
        title = pane.Q<Label>("map-title");
        hint = pane.Q<Label>("map-hint");
        storm = pane.Q<Label>("map-storm");
        legend = pane.Q<ScrollView>("map-legend");
    }

    /// <summary>Скільки метрів від центру вміщується в половину карти.</summary>
    static float Range => MoonTerrain.Instance != null ? MoonTerrain.Instance.ridgeRadius + 22f : DefaultRange;

    /// <summary>Світова точка (x, z) → піксель на карті (північ, тобто +z, угорі).</summary>
    public static Vector2 ToMap(float worldX, float worldZ, float range)
    {
        float scale = AreaPx * 0.5f / range;
        return new Vector2(AreaPx * 0.5f + worldX * scale, AreaPx * 0.5f - worldZ * scale);
    }

    /// <summary>Яка точка поверхні розвідана: на карті вона з типом, інакше «?».</summary>
    public static bool IsKnown(PoiState poi) => poi != null && poi.Discovered;

    public void Refresh()
    {
        if (area == null) return;
        var s = UiKit.State;
        UiKit.SetText(title, Loc.Get("mapv.title"));
        UiKit.SetText(hint, Loc.Get("mapv.hint"));
        area.Clear();
        legend?.Clear();
        if (s == null) return;

        float range = Range;
        DrawGround(range);
        DrawBase();

        // гравець: де він, звідси рахуємо відстані
        var engineerObject = GameObject.FindGameObjectWithTag("Player");
        Vector3 you = engineerObject != null ? engineerObject.transform.position : Vector3.zero;

        var places = new List<Place>();
        foreach (var marker in PoiMarker.All)
        {
            var poi = s.FindPoi(marker.poiId);
            if (poi == null) continue; // точка відкриється в наступному розділі
            Vector3 p = marker.transform.position;
            float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(you.x, you.z));
            places.Add(new Place { Poi = poi, X = p.x, Z = p.z, Distance = dist });
        }
        places.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var place in places) DrawPlace(place, range);

        DrawCrew(range);
        if (engineerObject != null) DrawDot(you.x, you.z, 14f, "mp-dot mp-dot--you", range, Loc.Get("mapv.you"));

        BuildLegend(places);

        bool storming = s.Radiation != null && s.Radiation.Phase != StormPhase.None;
        area.EnableInClassList("mp-area--storm", storming);
        UiKit.Show(storm, storming);
        UiKit.SetText(storm, Loc.Get("mapv.storm"));
    }

    struct Place
    {
        public PoiState Poi;
        public float X, Z, Distance;
    }

    // ---------- карта ----------

    void DrawGround(float range)
    {
        // рівнина всередині хребта
        float ridge = MoonTerrain.Instance != null ? MoonTerrain.Instance.ridgeRadius : range - 22f;
        Circle("mp-ground", 0f, 0f, ridge, range);
        // кола-лінійки з підписом відстані
        for (int meters = 50; meters < ridge; meters += 50)
        {
            Circle("mp-ring", 0f, 0f, meters, range);
            var p = ToMap(0f, meters, range);
            var label = UiKit.Label(area, "mp-ring-label", meters + " m");
            label.style.left = p.x + 3f;
            label.style.top = p.y - 1f;
            label.pickingMode = PickingMode.Ignore;
        }
    }

    /// <summary>Коло з центром (x, z) і радіусом у метрах.</summary>
    void Circle(string classes, float x, float z, float radiusMeters, float range)
    {
        float px = radiusMeters / range * AreaPx * 0.5f;
        var center = ToMap(x, z, range);
        var e = UiKit.Box(area, classes);
        e.pickingMode = PickingMode.Ignore;
        e.style.left = center.x - px;
        e.style.top = center.y - px;
        e.style.width = px * 2f;
        e.style.height = px * 2f;
        Round(e, px * 2f);
    }

    void DrawBase()
    {
        float range = Range;
        bool any = false;
        foreach (var view in BaseView.All)
        {
            if (view == null) continue;
            any = true;
            var p = view.transform.position;
            DrawDot(p.x, p.z, 9f, "mp-dot mp-dot--module" + (view.IsUnderConstruction ? " mp-dot--building" : ""), range,
                    Loc.Get("mod." + view.Type));
        }
        if (!any) DrawDot(0f, 0f, 12f, "mp-dot mp-dot--module", range, Loc.Get("mapv.base"));
        var label = UiKit.Label(area, "mp-label mp-label--base", Loc.Get("mapv.base"));
        label.pickingMode = PickingMode.Ignore;
        var at = ToMap(0f, 0f, range);
        label.style.left = at.x - 20f;
        label.style.top = at.y + 12f;
    }

    void DrawPlace(Place place, float range)
    {
        bool known = IsKnown(place.Poi);
        string classes = known ? "mp-dot mp-dot--poi mp-dot--" + place.Poi.Type : "mp-dot mp-dot--poi mp-dot--unknown";
        float size = known ? 13f : 17f; // «?» має вміститися в кружок
        var dot = DrawDot(place.X, place.Z, size, classes, range, known ? Loc.Get("mapv.poi." + place.Poi.Type) : Loc.Get("mapv.unknown"));
        if (!known) UiKit.Label(dot, "mp-q", "?").pickingMode = PickingMode.Ignore;
        var p = ToMap(place.X, place.Z, range);
        var label = UiKit.Label(area, "mp-label", known ? Loc.Get("mapv.poi." + place.Poi.Type) : "?");
        label.pickingMode = PickingMode.Ignore;
        label.style.left = p.x + 10f;
        label.style.top = p.y - 6f;
        if (!known) label.style.display = DisplayStyle.None; // «?» уже в самій точці
    }

    void DrawCrew(float range)
    {
        foreach (var view in AstronautView.All)
        {
            if (view == null || view.Data == null) continue;
            if (view.Data.Role == CrewRole.Engineer) continue; // гравця малюємо окремо, великим
            var p = view.transform.position;
            DrawDot(p.x, p.z, 8f, "mp-dot mp-dot--crew", range, view.Data.Name + " · " + Loc.Get("role." + view.Data.Role.ToString().ToLowerInvariant()));
        }
    }

    VisualElement DrawDot(float x, float z, float size, string classes, float range, string tooltip)
    {
        var p = ToMap(x, z, range);
        var e = UiKit.Box(area, classes);
        e.style.left = p.x - size * 0.5f;
        e.style.top = p.y - size * 0.5f;
        e.style.width = size;
        e.style.height = size;
        if (classes.Contains("mp-dot--you") || classes.Contains("mp-dot--crew") || classes.Contains("mp-dot--poi")) Round(e, size);
        e.tooltip = tooltip;
        return e;
    }

    static void Round(VisualElement e, float size)
    {
        float r = size * 0.5f;
        e.style.borderTopLeftRadius = r;
        e.style.borderTopRightRadius = r;
        e.style.borderBottomLeftRadius = r;
        e.style.borderBottomRightRadius = r;
    }

    // ---------- список місць ----------

    void BuildLegend(List<Place> places)
    {
        if (legend == null) return;
        Row("mp-swatch mp-swatch--you", Loc.Get("mapv.you"));
        Row("mp-swatch mp-swatch--module", Loc.Get("mapv.base"));
        Row("mp-swatch mp-swatch--crew", Loc.Get("mapv.crew"));
        UiKit.Box(legend, "gt-gap");
        foreach (var place in places)
        {
            bool known = IsKnown(place.Poi);
            string name = known ? Loc.Get("mapv.poi." + place.Poi.Type) : Loc.Get("mapv.unknown");
            string swatch = known ? "mp-swatch mp-swatch--" + place.Poi.Type : "mp-swatch mp-swatch--unknown";
            Row(swatch, name + " · " + Loc.Get("mapv.dist", Mathf.RoundToInt(place.Distance)));
        }
    }

    void Row(string swatchClasses, string text)
    {
        var row = UiKit.Box(legend, "mp-row");
        UiKit.Box(row, swatchClasses).pickingMode = PickingMode.Ignore;
        UiKit.Label(row, "mp-row-text", text).pickingMode = PickingMode.Ignore;
    }
}
