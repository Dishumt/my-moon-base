// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Затемнення вулиці, коли інженер у модулі: плавно зменшує головне світло (Sun)
/// і загальне фонове світло (ambient — світло «звідусіль», без джерела).
/// Ще й тінь від хребта (ShadeFx): у години тіні Сонце слабшає — множимо одне на інше.
/// Став на об'єкт Sun.
/// </summary>
[RequireComponent(typeof(Light))]
public class OutdoorDarkening : MonoBehaviour
{
    [Tooltip("Яка частка світла лишається надворі, коли інженер усередині: 0 = темрява, 1 = без змін")]
    [Range(0f, 1f)] [SerializeField] float insideBrightness = 0.25f;
    [Tooltip("Скільки секунд триває затемнення")]
    [SerializeField] float fadeSeconds = 0.4f;
    [Tooltip("Об'єкти з Unlit-матеріалом (наприклад, 2D-земля): світло на них не діє, тож фарбуємо їх темнішими самі")]
    [SerializeField] Renderer[] alsoDarken = new Renderer[0];

    MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>Тінь від хребта (ставить ShadeFx): 1 — Сонце світить, менше — Сонце за горами.</summary>
    public static float Shade { get; set; } = 1f;

    /// <summary>Яскравість вулиці зараз (модуль × тінь), 0..1. Спрайти надворі (каміння) темнішають за нею.</summary>
    public static float Brightness { get; private set; } = 1f;

    float appliedShade = -1f;

    Light sun;
    float baseSun, baseAmbientIntensity;
    Color baseAmbientLight;
    float k = 1f; // поточна яскравість: 1 = звичайна

    void Start()
    {
        sun = GetComponent<Light>();
        baseSun = sun.intensity;
        baseAmbientIntensity = RenderSettings.ambientIntensity;
        baseAmbientLight = RenderSettings.ambientLight;
    }

    void Update()
    {
        float target = ModuleInterior.EngineerInsideAny ? insideBrightness : 1f;
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        float newK = Mathf.MoveTowards(k, target, step * (1f - insideBrightness));
        if (Mathf.Approximately(newK, k) && Mathf.Approximately(appliedShade, Shade)) return;
        k = newK;
        appliedShade = Shade;

        float b = k * Shade;
        Brightness = b;
        sun.intensity = baseSun * b;
        RenderSettings.ambientIntensity = baseAmbientIntensity * b;
        RenderSettings.ambientLight = baseAmbientLight * b;
        TintUnlit(b);
    }

    void TintUnlit(float brightness)
    {
        if (block == null) block = new MaterialPropertyBlock();
        foreach (var r in alsoDarken)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, new Color(brightness, brightness, brightness, 1f));
            r.SetPropertyBlock(block);
        }
    }

    // Повертаємо світло як було, щоб не зіпсувати налаштування сцени після Play.
    void OnDisable()
    {
        if (sun == null) return;
        sun.intensity = baseSun;
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.ambientLight = baseAmbientLight;
        TintUnlit(1f);
        Brightness = 1f;
    }
}
