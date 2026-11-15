// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Напис «Увага, буря!» вгорі екрана (UI Toolkit, той самий шрифт, що в планшеті — з кирилицею):
///  - попередження: бурштинова рамка з трикутником «!» і текстом storm.warning («Сонячна буря через N год! Це справжня подія з NASA DONKI.»);
///  - буря: червона рамка, що пульсує, з текстом storm.all_inside («Всі в укриття!»).
/// Тексти — тільки через Loc (ключі вже є в texts_en/uk.json, роль 05). Нічого не натискається — не заважає грі.
/// Створює StormFx, коли знадобиться; потрібні PanelSettings (ModuleLook.uiPanel — ставить меню Moon → Напис «Увага, буря»).
/// </summary>
public class StormBanner : MonoBehaviour
{
    static readonly Color WarningBack = new Color(0.78f, 0.42f, 0.02f, 0.93f);
    static readonly Color WarningEdge = new Color(1f, 0.82f, 0.3f, 1f);
    static readonly Color StormBack = new Color(0.62f, 0.05f, 0.05f, 0.93f);
    static readonly Color StormEdge = new Color(1f, 0.45f, 0.4f, 1f);

    UIDocument doc;
    VisualElement box;
    Label label;
    StormPhase shownPhase = StormPhase.None;
    int shownHours = -1;
    string shownLanguage;
    static Texture2D icon;

    /// <summary>Окремий UI-документ поверх гри (sortingOrder 50).</summary>
    public static StormBanner Create(PanelSettings panel)
    {
        var go = new GameObject("Storm Banner");
        go.SetActive(false); // спершу налаштовуємо документ, потім вмикаємо
        var d = go.AddComponent<UIDocument>();
        d.panelSettings = panel;
        d.sortingOrder = 50;
        var b = go.AddComponent<StormBanner>();
        b.doc = d;
        go.SetActive(true);
        return b;
    }

    void Build()
    {
        var root = doc != null ? doc.rootVisualElement : null;
        if (root == null) return;
        root.pickingMode = PickingMode.Ignore;

        var wrap = new VisualElement { name = "storm-banner", pickingMode = PickingMode.Ignore };
        wrap.style.position = Position.Absolute;
        wrap.style.left = 0;
        wrap.style.right = 0;
        wrap.style.top = 64;
        wrap.style.alignItems = Align.Center;

        box = new VisualElement { pickingMode = PickingMode.Ignore };
        box.style.flexDirection = FlexDirection.Row;
        box.style.alignItems = Align.Center;
        box.style.paddingLeft = 20;
        box.style.paddingRight = 26;
        box.style.paddingTop = 12;
        box.style.paddingBottom = 12;
        box.style.borderTopLeftRadius = box.style.borderTopRightRadius = 14;
        box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = 14;
        box.style.borderTopWidth = box.style.borderBottomWidth = 3;
        box.style.borderLeftWidth = box.style.borderRightWidth = 3;

        var iconEl = new VisualElement { pickingMode = PickingMode.Ignore };
        iconEl.style.width = 46;
        iconEl.style.height = 46;
        iconEl.style.marginRight = 16;
        iconEl.style.flexShrink = 0;
        iconEl.style.backgroundImage = new StyleBackground(Icon);

        label = new Label { pickingMode = PickingMode.Ignore };
        label.style.fontSize = 26;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.color = Color.white;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.maxWidth = 820;

        box.Add(iconEl);
        box.Add(label);
        wrap.Add(box);
        root.Add(wrap);
    }

    /// <summary>Показати (або сховати) напис. hours — скільки годин до бурі (для попередження).</summary>
    public void Show(StormPhase phase, int hours)
    {
        if (box == null) Build();
        if (box == null) return;

        bool on = phase != StormPhase.None;
        box.parent.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        if (!on) { shownPhase = phase; return; }

        // Текст міняємо, тільки коли щось змінилось (фаза, години, мова)
        hours = Mathf.Max(1, hours);
        if (phase != shownPhase || hours != shownHours || shownLanguage != Loc.Language)
        {
            shownPhase = phase;
            shownHours = hours;
            shownLanguage = Loc.Language;
            label.text = phase == StormPhase.Active ? Loc.Get("storm.all_inside") : Loc.Get("storm.warning", hours);
            Color back = phase == StormPhase.Active ? StormBack : WarningBack;
            Color edge = phase == StormPhase.Active ? StormEdge : WarningEdge;
            box.style.backgroundColor = back;
            box.style.borderTopColor = box.style.borderBottomColor = edge;
            box.style.borderLeftColor = box.style.borderRightColor = edge;
        }

        // Дихає, щоб привертати увагу; під час бурі — швидше й трохи «б'ється»
        float t = Time.unscaledTime;
        bool storm = phase == StormPhase.Active;
        box.style.opacity = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(t * (storm ? 4f : 2f)));
        float s = storm ? 1f + 0.035f * Mathf.Sin(t * 7f) : 1f;
        box.style.scale = new Scale(new Vector3(s, s, 1f));
    }

    /// <summary>Жовтий трикутник зі знаком «!» (16×16 пікселів, створюємо кодом).</summary>
    static Texture2D Icon
    {
        get
        {
            if (icon != null) return icon;
            string[] rows =
            {
                ".......##.......",
                "......#yy#......",
                "......#yy#......",
                ".....#yyyy#.....",
                ".....#y##y#.....",
                "....#yy##yy#....",
                "....#yy##yy#....",
                "...#yyy##yyy#...",
                "...#yyy##yyy#...",
                "..#yyyy##yyyy#..",
                "..#yyyyyyyyyy#..",
                ".#yyyyy##yyyyy#.",
                ".#yyyyy##yyyyy#.",
                "#yyyyyyyyyyyyyy#",
                "################",
                "................",
            };
            icon = WorldSprites.NewTexture(16, 16);
            var px = new Color[16 * 16];
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    char c = rows[15 - y][x]; // у текстурі y = 0 — нижній рядок
                    px[y * 16 + x] = c == '#' ? new Color(0.15f, 0.08f, 0.02f, 1f) : c == 'y' ? new Color(1f, 0.85f, 0.15f, 1f) : Color.clear;
                }
            icon.SetPixels(px);
            icon.Apply();
            return icon;
        }
    }
}
