// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Спільні налаштування вигляду всіх модулів: матеріали, розміри, кольори типів, світло, шлюз.
/// Один такий компонент на всю сцену (на об'єкті "World").
/// ModuleView бере звідси все, щоб не налаштовувати кожен модуль окремо.
/// </summary>
public class ModuleLook : MonoBehaviour
{
    public static ModuleLook Instance { get; private set; }

    [Header("Матеріали (створи в Art/ або Settings/)")]
    [Tooltip("Підлога і стіни: URP Lit, Surface Type = Opaque")]
    public Material wallMaterial;
    [Tooltip("Дах і риштування будови: URP Lit, Surface Type = Transparent (щоб дах міг плавно зникати)")]
    public Material roofMaterial;
    [Tooltip("Пил у шлюзі: шейдер Universal Render Pipeline/Particles/Unlit. Порожньо = без частинок.")]
    public Material dustMaterial;

    [Header("Розміри, метри")]
    [Tooltip("Ширина (X) і глибина (Y) модуля. Має бути менше за клітинку сітки (BaseGrid.CellSize = 8), щоб лишався прохід.")]
    public Vector2 moduleSize = new Vector2(6f, 6f);
    public float wallHeight = 2.2f;
    public float wallThickness = 0.2f;
    [Tooltip("Ширина дверей шлюзу")]
    public float doorWidth = 1.6f;
    [Tooltip("Глибина шлюзу (тамбура) від зовнішніх дверей до внутрішніх")]
    public float airlockDepth = 2f;

    [Header("Шлюз, секунди (реального часу; ігровий час іде — це ціна вилазки)")]
    public float doorSeconds = 0.4f;
    [Tooltip("Скільки триває здування пилу")]
    public float dustSeconds = 1.5f;
    [Tooltip("Скільки триває зняти або одягти скафандр")]
    public float suitSeconds = 1f;

    [Header("Дах і світло")]
    [Tooltip("Скільки секунд дах зникає або з'являється")]
    public float roofFadeSeconds = 0.4f;
    public Color interiorLightColor = new Color(1f, 0.85f, 0.65f); // тепле світло
    public float interiorLightIntensity = 3f;

    [Serializable]
    public class TypeColor
    {
        public ModuleType type;
        public Color color = Color.white;
    }

    [Header("Колір кожного типу (дах кімнати або корпус обладнання)")]
    public List<TypeColor> typeColors = new List<TypeColor>
    {
        new TypeColor { type = ModuleType.Habitat,    color = new Color(0.90f, 0.90f, 0.92f) }, // білий
        new TypeColor { type = ModuleType.Greenhouse, color = new Color(0.45f, 0.80f, 0.45f) }, // зелений
        new TypeColor { type = ModuleType.Shelter,    color = new Color(0.95f, 0.75f, 0.25f) }, // жовтий
        new TypeColor { type = ModuleType.Medbay,     color = new Color(0.95f, 0.45f, 0.45f) }, // червоний
        new TypeColor { type = ModuleType.Lab,        color = new Color(0.55f, 0.65f, 0.95f) }, // синій
        new TypeColor { type = ModuleType.Solar,      color = new Color(0.15f, 0.25f, 0.55f) }, // темно-синій
        new TypeColor { type = ModuleType.Battery,    color = new Color(0.30f, 0.75f, 0.70f) }, // бірюзовий
        new TypeColor { type = ModuleType.Antenna,    color = new Color(0.85f, 0.85f, 0.85f) },
        new TypeColor { type = ModuleType.Relay,      color = new Color(0.75f, 0.60f, 0.90f) },
        new TypeColor { type = ModuleType.Reactor,    color = new Color(0.70f, 0.70f, 0.30f) },
    };

    /// <summary>Колір типу модуля (білий, якщо не задано).</summary>
    public Color ColorOf(ModuleType type)
    {
        foreach (var tc in typeColors) if (tc.type == type) return tc.color;
        return Color.white;
    }

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
