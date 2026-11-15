// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Маленькі картинки, які світ створює кодом (файли не потрібні): білий квадрат, піксельний круг, м'яка пляма.
/// Розмір кожної — 1×1 метр, тож масштаб = розмір у метрах. Колір задаємо через SpriteRenderer.color.
/// Користуються живі меблі (FurnitureLife) і аварійна лампа в кімнатах (RoomBuilder).
/// </summary>
public static class WorldSprites
{
    static Sprite square, disc;

    /// <summary>Білий квадрат 1×1 м (для прямокутників поверх картинок).</summary>
    public static Sprite Square => square != null ? square : (square = Make(1, 1, 1f, (x, y) => Color.white));

    /// <summary>Білий піксельний круг діаметром 1 м (лампочки, бульбашки).</summary>
    public static Sprite Disc => disc != null ? disc : (disc = Make(16, 16, 16f, (x, y) =>
        new Vector2(x - 7.5f, y - 7.5f).sqrMagnitude <= 7.6f * 7.6f ? Color.white : Color.clear));

    /// <summary>М'яка пляма діаметром 1 м (сяйво, тінь, пара).</summary>
    public static Sprite Blob => MoonHop.BlobSprite();

    /// <summary>Картинка w×h пікселів з кольорів, які дає colorAt(x, y) (y = 0 — нижній рядок). Центр — посередині.</summary>
    public static Sprite Make(int w, int h, float pixelsPerUnit, System.Func<int, int, Color> colorAt)
    {
        var tex = NewTexture(w, h);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = colorAt(x, y);
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    /// <summary>Порожня текстура для піксель-арту (без розмиття).</summary>
    public static Texture2D NewTexture(int w, int h) =>
        new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
}
