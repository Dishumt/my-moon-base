// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Кольоровий «фільтр» на весь екран: напівпрозорий шар перед камерою (тонує все — землю, модулі, спрайти).
///  - тінь від хребта (ShadeFx) — легкі синюваті сутінки;
///  - сонячна буря (StormFx) — червоний відтінок і темні червоні краї екрана (віньєтка).
/// Ставить себе на головну камеру сам (ScreenOverlay.Get()). Інтерфейс (планшет, HUD) — поверх нього.
/// </summary>
public class ScreenOverlay : MonoBehaviour
{
    static ScreenOverlay instance;

    /// <summary>Відтінок від тіні хребта (колір + прозорість).</summary>
    public Color ShadeTint { get; set; } = Color.clear;
    /// <summary>Відтінок від бурі.</summary>
    public Color StormTint { get; set; } = Color.clear;
    /// <summary>Темні краї екрана (віньєтка) від бурі.</summary>
    public Color StormVignette { get; set; } = Color.clear;

    Camera cam;
    SpriteRenderer tint, vignette;
    static Sprite vignetteSprite;

    /// <summary>Фільтр на головній камері (створює, якщо ще немає). null — камери немає.</summary>
    public static ScreenOverlay Get()
    {
        if (instance != null) return instance;
        var c = Camera.main;
        if (c == null) return null;
        instance = c.GetComponent<ScreenOverlay>();
        if (instance == null) instance = c.gameObject.AddComponent<ScreenOverlay>();
        return instance;
    }

    void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        tint = Layer("Screen Tint", WorldSprites.Square, 1000);
        vignette = Layer("Screen Vignette", VignetteSprite, 1001);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    SpriteRenderer Layer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order; // поверх усього світу
        r.enabled = false;
        return r;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        // Шар трохи далі за ближню площину камери і завбільшки з увесь кадр
        float d = cam.nearClipPlane + 0.02f;
        float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
        float w = h * cam.aspect;
        foreach (var r in new[] { tint, vignette })
        {
            r.transform.localPosition = new Vector3(0f, 0f, d);
            r.transform.localRotation = Quaternion.identity;
            var size = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(w / size.x, h / size.y, 1f);
        }

        // Два відтінки змішуємо: прозорість = 1 − (1 − a1)(1 − a2), колір — середнє зважене
        float a1 = ShadeTint.a, a2 = StormTint.a;
        float a = 1f - (1f - a1) * (1f - a2);
        tint.enabled = a > 0.003f;
        if (tint.enabled)
        {
            Color c = (ShadeTint * a1 + StormTint * a2) / Mathf.Max(0.0001f, a1 + a2);
            c.a = a;
            tint.color = c;
        }
        vignette.enabled = StormVignette.a > 0.003f;
        if (vignette.enabled) vignette.color = StormVignette;
    }

    /// <summary>Прозорий центр, непрозорі краї (м'яко; з розмиттям, бо розтягуємо на весь екран).</summary>
    static Sprite VignetteSprite
    {
        get
        {
            if (vignetteSprite != null) return vignetteSprite;
            vignetteSprite = WorldSprites.Make(64, 64, 64f, (x, y) =>
            {
                float dx = (x - 31.5f) / 31.5f, dy = (y - 31.5f) / 31.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f; // 0 у центрі, 1 у кутах
                float k = Mathf.Clamp01((r - 0.35f) / 0.65f);
                return new Color(1f, 1f, 1f, k * k);
            });
            vignetteSprite.texture.filterMode = FilterMode.Bilinear;
            return vignetteSprite;
        }
    }
}
