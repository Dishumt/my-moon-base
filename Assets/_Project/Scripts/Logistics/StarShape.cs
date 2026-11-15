// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// П'ятикутна зірочка для звіту місії. Малюється кодом (Painter2D), тож не залежить від шрифту.
/// On = true — жовта, false — темна.
/// </summary>
public class StarShape : VisualElement
{
    bool on;
    static readonly Color OnColor = new Color(1f, 0.79f, 0.24f);
    static readonly Color OffColor = new Color(0.17f, 0.34f, 0.46f);

    public bool On
    {
        get => on;
        set { on = value; MarkDirtyRepaint(); }
    }

    public StarShape(bool on, bool small)
    {
        this.on = on;
        AddToClassList("star");
        if (small) AddToClassList("star--small");
        generateVisualContent += Draw;
    }

    void Draw(MeshGenerationContext ctx)
    {
        var r = contentRect;
        float cx = r.width * 0.5f, cy = r.height * 0.53f;
        float outer = Mathf.Min(r.width, r.height) * 0.5f, inner = outer * 0.45f;

        var p = ctx.painter2D;
        p.fillColor = on ? OnColor : OffColor;
        p.BeginPath();
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.Deg2Rad * (-90f + 36f * i);
            float rad = i % 2 == 0 ? outer : inner;
            var pt = new Vector2(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad);
            if (i == 0) p.MoveTo(pt); else p.LineTo(pt);
        }
        p.ClosePath();
        p.Fill();
    }
}
