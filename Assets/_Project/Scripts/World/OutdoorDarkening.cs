// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Затемнення вулиці, коли інженер у модулі: плавно зменшує головне світло (Sun)
/// і загальне фонове світло (ambient — світло «звідусіль», без джерела).
/// Став на об'єкт Sun.
/// </summary>
[RequireComponent(typeof(Light))]
public class OutdoorDarkening : MonoBehaviour
{
    [Tooltip("Яка частка світла лишається надворі, коли інженер усередині: 0 = темрява, 1 = без змін")]
    [Range(0f, 1f)] [SerializeField] float insideBrightness = 0.25f;
    [Tooltip("Скільки секунд триває затемнення")]
    [SerializeField] float fadeSeconds = 0.4f;

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
        if (Mathf.Approximately(newK, k)) return;
        k = newK;

        sun.intensity = baseSun * k;
        RenderSettings.ambientIntensity = baseAmbientIntensity * k;
        RenderSettings.ambientLight = baseAmbientLight * k;
    }

    // Повертаємо світло як було, щоб не зіпсувати налаштування сцени після Play.
    void OnDisable()
    {
        if (sun == null) return;
        sun.intensity = baseSun;
        RenderSettings.ambientIntensity = baseAmbientIntensity;
        RenderSettings.ambientLight = baseAmbientLight;
    }
}
