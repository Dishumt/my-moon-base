// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Спільні налаштування вигляду всіх модулів: матеріали, розміри, світло всередині.
/// Один такий компонент на всю сцену (на об'єкті "World").
/// ModuleView бере звідси все, щоб не налаштовувати кожен слот окремо.
/// </summary>
public class ModuleLook : MonoBehaviour
{
    public static ModuleLook Instance { get; private set; }

    [Header("Матеріали (створи в Art/ або Settings/)")]
    [Tooltip("Підлога і стіни: URP Lit, Surface Type = Opaque")]
    public Material wallMaterial;
    [Tooltip("Дах і риштування будови: URP Lit, Surface Type = Transparent (щоб дах міг плавно зникати)")]
    public Material roofMaterial;

    [Header("Розміри, метри")]
    public float wallHeight = 2.2f;
    public float wallThickness = 0.2f;
    [Tooltip("Ширина дверного отвору в передній стіні (що ближче до камери)")]
    public float doorWidth = 1.6f;

    [Header("Дах і світло")]
    [Tooltip("Скільки секунд дах зникає або з'являється")]
    public float roofFadeSeconds = 0.4f;
    public Color interiorLightColor = new Color(1f, 0.85f, 0.65f); // тепле світло
    public float interiorLightIntensity = 3f;

    void Awake()
    {
        Instance = this;
        if (wallMaterial == null || roofMaterial == null)
            Debug.LogWarning("ModuleLook: не задано матеріали. Модулі будуть рожевими у веб-збірці.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
