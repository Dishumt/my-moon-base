// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Місячна хода. На Місяці гравітація g = 1,62 м/с² — у 6 разів менша, ніж на Землі (9,81).
/// Тому астронавти не крокують, а підстрибують «зайчиком», як на записах місій «Аполлон».
/// Цей компонент:
///  - рахує висоту стрибка (парабола, як справжній політ) — спрайт піднімається, а тінь лишається на землі;
///  - малює м'яку тінь-пляму під ногами (як у Don't Starve Together) — по ній видно, що персонаж у повітрі;
///  - при приземленні трохи сплющує спрайт і здіймає пил, який ПОВІЛЬНО осідає (місячна гравітація);
///  - каже, який кадр ходьби показувати (Steps).
/// Ставить себе сам (EngineerController додає його). Згодом так само ходитимуть астронавти.
/// </summary>
public class MoonHop : MonoBehaviour
{
    /// <summary>Місячна гравітація відносно земної: 1,62 / 9,81 ≈ 0,165.</summary>
    public const float MoonGravityScale = 1.62f / 9.81f;

    [Header("У скафандрі (надворі)")]
    [SerializeField] float suitHopHeight = 0.32f;    // метрів
    [SerializeField] float suitHopSeconds = 0.62f;   // скільки триває один стрибок
    [Header("Без скафандра (у модулі)")]
    [SerializeField] float indoorHopHeight = 0.12f;
    [SerializeField] float indoorHopSeconds = 0.4f;
    [Header("Тінь")]
    [SerializeField] float shadowSize = 0.9f;        // діаметр плями, метрів

    /// <summary>Висота над землею зараз (метрів).</summary>
    public float Height { get; private set; }
    /// <summary>Скільки стрибків зроблено (дробове) — для вибору кадру анімації.</summary>
    public float Steps { get; private set; }
    /// <summary>Чи зараз іде стрибок (у повітрі або щойно почав).</summary>
    public bool IsHopping { get; private set; }

    float t;            // 0..1 — де ми в поточному стрибку
    float squash;       // 0..1 — сплющення після приземлення
    float maxHeight = 0.3f;
    Transform shadow;
    SpriteRenderer shadowRenderer;
    ParticleSystem dust;
    bool dustTried;     // пробували створити пил (ModuleLook міг ще не прокинутись в Awake)
    Billboard billboard;

    static Sprite blobSprite;

    void Awake()
    {
        billboard = GetComponentInChildren<Billboard>();
        CreateShadow();
    }

    /// <summary>Викликати щокадру: чи персонаж хоче йти, у скафандрі він чи ні.</summary>
    public void Step(bool wantMove, bool suit, float dt)
    {
        maxHeight = suit ? suitHopHeight : indoorHopHeight;
        float seconds = suit ? suitHopSeconds : indoorHopSeconds;

        if (!IsHopping && wantMove) { IsHopping = true; t = 0f; }
        if (IsHopping)
        {
            t += dt / seconds;
            Steps += dt / seconds;
            if (t >= 1f) // приземлились
            {
                Land(suit);
                if (wantMove) t -= 1f;               // одразу наступний стрибок
                else { IsHopping = false; t = 0f; }  // зупинились
            }
        }

        // Парабола: угору швидко, на вершині «зависає», униз — як справжній політ при малій гравітації
        Height = IsHopping ? 4f * maxHeight * t * (1f - t) : 0f;

        squash = Mathf.MoveTowards(squash, 0f, dt * 5f);
        if (billboard != null) billboard.ExtraScale = new Vector2(1f + squash * 0.12f, 1f - squash * 0.12f);

        UpdateShadow();
    }

    void Land(bool suit)
    {
        squash = 1f;
        if (!dustTried) { dustTried = true; CreateDust(); }
        if (suit && dust != null) dust.Emit(8); // у модулі пилу немає
    }

    // ---------- Тінь ----------

    void CreateShadow()
    {
        var go = new GameObject("Shadow");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // лежить на землі
        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = BlobSprite();
        shadowRenderer.sortingOrder = -1; // під персонажем
        shadow = go.transform;
        UpdateShadow();
    }

    void UpdateShadow()
    {
        if (shadow == null) return;
        var p = transform.position;
        shadow.position = new Vector3(p.x, 0.12f, p.z); // трохи вище підлоги модулів (0.1 м)
        float k = maxHeight > 0f ? Height / maxHeight : 0f;      // 0 на землі, 1 на вершині
        float s = shadowSize * (1f - 0.3f * k);                  // у повітрі тінь менша
        shadow.localScale = new Vector3(s, s, 1f);
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.38f * (1f - 0.45f * k)); // і світліша
    }

    /// <summary>М'яке темне коло, яке створюємо кодом (картинка не потрібна).</summary>
    static Sprite BlobSprite()
    {
        if (blobSprite != null) return blobSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
            float a = Mathf.Clamp01(1f - d);
            px[y * n + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a)); // м'який край
        }
        tex.SetPixels(px);
        tex.Apply();
        blobSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n); // 1 одиниця = 1 метр
        return blobSprite;
    }

    // ---------- Пил ----------

    void CreateDust()
    {
        var look = ModuleLook.Instance;
        if (look == null || look.dustMaterial == null) return; // без матеріалу частинки були б рожеві

        var go = new GameObject("Hop Dust");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // конус дивиться вгору
        dust = go.AddComponent<ParticleSystem>();
        dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = dust.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // пил лишається там, де впав
        main.startLifetime = 1.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new Color(0.72f, 0.7f, 0.68f, 0.85f);
        main.gravityModifier = MoonGravityScale; // повільно осідає — як на Місяці

        var emission = dust.emission;
        emission.rateOverTime = 0f;

        var shape = dust.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 70f;
        shape.radius = 0.25f;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = look.dustMaterial;
    }

    void LateUpdate()
    {
        // Пил стартує біля ніг, навіть коли персонаж у повітрі
        if (dust != null)
        {
            var p = transform.position;
            dust.transform.position = new Vector3(p.x, 0.05f, p.z);
        }
    }
}
