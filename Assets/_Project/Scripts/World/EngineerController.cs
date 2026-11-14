// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Інженер (персонаж гравця): ходить на WASD або стрілках, 2D-спрайт з анімацією ходьби.
/// Рух місячний: стрибки «зайчиком» (MoonHop), розгін і гальмування з інерцією — у скафандрі
/// зчеплення з пилом слабке, тож інженер трохи проковзує, коли зупиняється.
/// «Вперед» (W) — це вгору по екрану, тобто туди, куди дивиться камера.
/// Поки гра на паузі (відкритий планшет) або йде шлюз (Locked), інженер стоїть.
/// Два набори картинок: у скафандрі (надворі) і без нього (у модулях). Перемикає шлюз через SuitOn.
///
/// Як налаштувати: меню Moon → Налаштувати інженера (спрайти й анімація) — зробить усе саме.
/// CharacterController — вбудований компонент Unity, який рухає персонажа і не дає пройти крізь стіни.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EngineerController : MonoBehaviour
{
    /// <summary>Інженер у сцені (він один).</summary>
    public static EngineerController Instance { get; private set; }

    /// <summary>Набір картинок для одного вбрання.</summary>
    [Serializable]
    public class SpriteSet
    {
        public Sprite idleFront;     // стоїть обличчям до камери
        public Sprite idleBack;      // стоїть спиною
        public Sprite[] walkFront;   // іде вниз по екрану
        public Sprite[] walkBack;    // іде вгору по екрану
        public Sprite[] walkLeft;    // іде вліво
        [Tooltip("Можна лишити порожнім — тоді беремо walkLeft і дзеркалимо")]
        public Sprite[] walkRight;   // іде вправо
        [Tooltip("Номер кадру ходьби (0..3), коли нога на землі й відштовхується: [одна нога, інша нога]. Широкий крок.")]
        public int[] pushFrames = { 1, 2 };
        [Tooltip("Номер кадру ходьби (0..3) у польоті: [після однієї ноги, після іншої]. Ноги разом.")]
        public int[] airFrames = { 0, 3 };
    }

    [Header("Рух")]
    [Tooltip("Швидкість у метрах за секунду")]
    [SerializeField] float speed = 5f;
    [Tooltip("Межа карти: інженер не відійде від центру далі, ніж на стільки метрів по X і Z")]
    [SerializeField] float mapHalfSize = 200f;
    [Tooltip("Розгін і гальмування в скафандрі, м/с² (менше = більше «ковзання»)")]
    [SerializeField] float suitAccel = 7f, suitStopAccel = 8f;
    [Tooltip("Розгін і гальмування без скафандра (у модулі)")]
    [SerializeField] float indoorAccel = 16f, indoorStopAccel = 14f;

    [Header("Спрайт")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [Tooltip("У скафандрі (надворі)")]
    public SpriteSet suit = new SpriteSet();
    [Tooltip("Без скафандра (у модулях)")]
    public SpriteSet indoor = new SpriteSet();
    [Tooltip("Якщо картинок без скафандра ще немає — фарбуємо спрайт цим кольором, щоб було видно різницю")]
    [SerializeField] Color noIndoorSpritesTint = new Color(0.7f, 0.85f, 1f);

    CharacterController controller;
    Transform cam;
    Vector2 facing = new Vector2(0f, -1f); // куди дивиться: спочатку обличчям до камери
    Vector3 velocity;                      // швидкість зараз (м/с) — з інерцією
    MoonHop hop;
    bool suitOn = true;
    Vector3 spriteBasePos;

    /// <summary>Чи інженер зараз іде (знадобиться для звуку кроків).</summary>
    public bool IsMoving { get; private set; }

    /// <summary>true = інженер не слухається клавіш (наприклад, поки йде шлюз).</summary>
    public bool Locked { get; set; }

    /// <summary>Сила трясіння спрайта (шлюз здуває пил). 0 = не трясеться.</summary>
    public float Shake { get; set; }

    /// <summary>Радіус тіла інженера в метрах (з CharacterController).</summary>
    public float Radius => controller != null ? controller.radius : 0.5f;

    /// <summary>У скафандрі чи ні. Перемикає шлюз.</summary>
    public bool SuitOn
    {
        get => suitOn;
        set { suitOn = value; RefreshSprite(); }
    }

    void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        hop = GetComponent<MoonHop>();
        if (hop == null) hop = gameObject.AddComponent<MoonHop>(); // після пошуку спрайта: MoonHop додає свій спрайт-тінь
        if (IsEmpty(suit))
            Debug.LogWarning("EngineerController: немає картинок інженера. Запусти меню Moon → Налаштувати інженера (спрайти й анімація) і збережи сцену.");
        else
        {
            var cube = GetComponent<MeshRenderer>();
            if (cube != null) cube.enabled = false; // є спрайт — кубик-заглушка не потрібен
        }
        if (spriteRenderer != null) spriteBasePos = spriteRenderer.transform.localPosition;
        RefreshSprite();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        bool paused = IsGamePaused();
        float dt = paused ? 0f : Time.deltaTime;
        Vector2 input = Locked || paused ? Vector2.zero : ReadInput();
        bool wantMove = input.sqrMagnitude > 0.01f;

        // Бажана швидкість і плавний розгін/гальмування до неї (інерція)
        Vector3 target = ScreenToGround(input) * speed;
        float rate = wantMove ? (suitOn ? suitAccel : indoorAccel) : (suitOn ? suitStopAccel : indoorStopAccel);
        velocity = Vector3.MoveTowards(velocity, target, rate * dt);
        if (Locked) velocity = Vector3.zero;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            controller.Move(velocity * dt);
            ClampToMap();
        }
        if (wantMove) facing = input;

        IsMoving = wantMove || velocity.sqrMagnitude > 0.25f;
        hop.Step(wantMove && !Locked, suitOn, dt); // стрибає, поки тиснуть клавіші; відпустив — доземлюється і ковзає
        RefreshSprite();
    }

    void LateUpdate()
    {
        if (spriteRenderer == null) return;
        // Стрибок піднімає спрайт; трясіння у шлюзі смикає його вліво-вправо
        float dx = Shake > 0f ? Mathf.Sin(Time.time * 60f) * Shake : 0f;
        float dy = hop != null ? hop.Height : 0f;
        spriteRenderer.transform.localPosition = spriteBasePos + new Vector3(dx, dy, 0f);
    }

    /// <summary>Напрямок «на екрані» (WASD) → напрямок на землі з урахуванням повороту камери.</summary>
    Vector3 ScreenToGround(Vector2 input)
    {
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        }
        Vector3 dir = forward * input.y + right * input.x;
        dir.y = 0f; // земля рівна, тож висоту не змінюємо
        return dir;
    }

    /// <summary>WASD або стрілки → вектор від (-1,-1) до (1,1).</summary>
    static Vector2 ReadInput()
    {
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;

        Vector2 v = Vector2.zero;
        if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
        if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
        return v.sqrMagnitude > 1f ? v.normalized : v; // по діагоналі не швидше
    }

    static bool IsGamePaused()
    {
        var game = GameBootstrap.Instance;
        return game != null && game.Clock != null && game.Clock.IsPaused;
    }

    void ClampToMap()
    {
        Vector3 p = transform.position;
        float x = Mathf.Clamp(p.x, -mapHalfSize, mapHalfSize);
        float z = Mathf.Clamp(p.z, -mapHalfSize, mapHalfSize);
        if (x != p.x || z != p.z)
        {
            controller.enabled = false; // CharacterController не дає просто так міняти позицію
            transform.position = new Vector3(x, p.y, z);
            controller.enabled = true;
        }
    }

    // ---------- Картинка ----------

    /// <summary>Вибирає кадр: напрямок (спереду, ззаду, вбік), стоїть чи йде, у скафандрі чи ні.</summary>
    void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        SpriteSet set = suitOn ? suit : indoor;
        bool missingIndoor = !suitOn && IsEmpty(indoor);
        if (missingIndoor) set = suit;                         // картинок без скафандра нема — беремо скафандр
        spriteRenderer.color = missingIndoor ? noIndoorSpritesTint : Color.white;

        Sprite frame = CharacterSprites.Pick(set, facing, hop, out bool flip);
        if (frame != null) spriteRenderer.sprite = frame;
        spriteRenderer.flipX = flip;
    }

    static bool IsEmpty(SpriteSet s) => CharacterSprites.IsEmpty(s);
}
