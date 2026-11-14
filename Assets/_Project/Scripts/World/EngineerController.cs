// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Інженер (персонаж гравця): ходить на WASD або стрілках, 2D-спрайт з анімацією ходьби.
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
    }

    [Header("Рух")]
    [Tooltip("Швидкість у метрах за секунду")]
    [SerializeField] float speed = 5f;
    [Tooltip("Межа карти: інженер не відійде від центру далі, ніж на стільки метрів по X і Z")]
    [SerializeField] float mapHalfSize = 200f;

    [Header("Спрайт")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [Tooltip("Кадрів анімації ходьби за секунду")]
    [SerializeField] float walkFps = 8f;
    [Tooltip("У скафандрі (надворі)")]
    public SpriteSet suit = new SpriteSet();
    [Tooltip("Без скафандра (у модулях)")]
    public SpriteSet indoor = new SpriteSet();
    [Tooltip("Якщо картинок без скафандра ще немає — фарбуємо спрайт цим кольором, щоб було видно різницю")]
    [SerializeField] Color noIndoorSpritesTint = new Color(0.7f, 0.85f, 1f);

    CharacterController controller;
    Transform cam;
    Vector2 facing = new Vector2(0f, -1f); // куди дивиться: спочатку обличчям до камери
    float animTime;
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
        if (spriteRenderer != null) spriteBasePos = spriteRenderer.transform.localPosition;
        RefreshSprite();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        Vector2 input = Locked ? Vector2.zero : ReadInput();
        IsMoving = input.sqrMagnitude > 0.01f && !IsGamePaused();

        if (IsMoving)
        {
            Move(input);
            facing = input;
            animTime += Time.deltaTime;
        }
        else animTime = 0f;

        RefreshSprite();
    }

    void LateUpdate()
    {
        if (spriteRenderer == null) return;
        // Трясіння у шлюзі: спрайт трохи смикається вліво-вправо
        float dx = Shake > 0f ? Mathf.Sin(Time.time * 60f) * Shake : 0f;
        spriteRenderer.transform.localPosition = spriteBasePos + new Vector3(dx, 0f, 0f);
    }

    void Move(Vector2 input)
    {
        // Перетворюємо напрямок «на екрані» у напрямок на землі з урахуванням повороту камери.
        Vector3 forward = Vector3.forward, right = Vector3.right;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null)
        {
            forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
        }

        Vector3 move = (forward * input.y + right * input.x) * speed * Time.deltaTime;
        move.y = 0f; // земля рівна, тож висоту не змінюємо
        controller.Move(move);
        ClampToMap();
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

        bool sideways = Mathf.Abs(facing.x) > Mathf.Abs(facing.y);
        Sprite[] walk;
        Sprite idle;
        bool flip = false;

        if (sideways)
        {
            bool right = facing.x > 0f;
            walk = right ? set.walkRight : set.walkLeft;
            if (right && IsEmpty(walk)) { walk = set.walkLeft; flip = true; } // правих кадрів нема — дзеркалимо ліві
            idle = IsEmpty(walk) ? set.idleFront : walk[0];
        }
        else if (facing.y > 0f) { walk = set.walkBack; idle = set.idleBack; }
        else { walk = set.walkFront; idle = set.idleFront; }

        Sprite frame = idle;
        if (IsMoving && !IsEmpty(walk))
            frame = walk[(int)(animTime * walkFps) % walk.Length];

        if (frame != null) spriteRenderer.sprite = frame;
        spriteRenderer.flipX = flip;
    }

    static bool IsEmpty(Sprite[] frames) => frames == null || frames.Length == 0 || frames[0] == null;
    static bool IsEmpty(SpriteSet s) => s.idleFront == null && IsEmpty(s.walkFront);
}
