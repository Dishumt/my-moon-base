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

    [Header("Стрибок (пробіл; тільки надворі, у скафандрі)")]
    [Tooltip("На скільки метрів угору. На Місяці гравітація в 6 разів слабша — стрибаєш високо й повільно")]
    [SerializeField] float jumpHeight = 2f;
    [Tooltip("Скільки секунд у повітрі")]
    [SerializeField] float jumpAirSeconds = 1.6f;
    [Tooltip("Ціна стрибка: у повітрі майже не кермуєш (м/с²; на землі в скафандрі — 7)")]
    [SerializeField] float airControl = 1.5f;
    [Tooltip("Ціна стрибка: після приземлення стільки секунд не можна стрибнути знову")]
    [SerializeField] float jumpCooldown = 0.5f;

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
    bool airborne;         // летить у великому стрибку
    float verticalSpeed;   // м/с угору (у стрибку)
    float jumpCooldownLeft;

    /// <summary>Чи інженер зараз у повітрі (великий стрибок на пробіл).</summary>
    public bool IsJumping => airborne;
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

        // Стрибок: пробіл, тільки надворі (у скафандрі), з землі і не одразу після приземлення
        if (jumpCooldownLeft > 0f) jumpCooldownLeft -= dt;
        if (!airborne && suitOn && !Locked && !paused && jumpCooldownLeft <= 0f && JumpPressed()) StartJump();

        // Бажана швидкість і плавний розгін/гальмування до неї (інерція). У повітрі майже не кермуєш.
        Vector3 target = ScreenToGround(input) * speed;
        float rate = airborne ? airControl
                   : wantMove ? (suitOn ? suitAccel : indoorAccel) : (suitOn ? suitStopAccel : indoorStopAccel);
        velocity = Vector3.MoveTowards(velocity, target, rate * dt);
        if (Locked) velocity = Vector3.zero;

        if (velocity.sqrMagnitude > 0.0001f)
        {
            Vector3 step = velocity * dt;
            if (dt > 0f) step = airborne ? LimitInAir(step) : LimitBySlope(step); // надто круто вгору (хребет) — не пускаємо
            controller.Move(step);
            ClampToMap();
        }
        if (airborne) Fly(dt);
        else FollowGround(dt);
        if (wantMove) facing = input;

        IsMoving = wantMove || velocity.sqrMagnitude > 0.25f;
        hop.JumpHeight = airborne ? Mathf.Max(0f, transform.position.y - FootOffset - MoonTerrain.HeightAt(transform.position)) : 0f;
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

    // ---------- Рельєф (MoonTerrain) ----------

    /// <summary>Від низу капсули CharacterController до точки, де стоїть об'єкт інженера.</summary>
    float FootOffset => controller.height * 0.5f - controller.center.y;

    /// <summary>Ноги — на землі: висота рельєфу під інженером (у базі земля рівна, висота 0).</summary>
    void FollowGround(float dt)
    {
        Vector3 p = transform.position;
        float want = MoonTerrain.HeightAt(p) + FootOffset;
        if (Mathf.Abs(p.y - want) > 0.0005f) controller.Move(new Vector3(0f, want - p.y, 0f));
        SlideIfTooSteep(dt);
    }

    /// <summary>Приземлився на круту стінку (хребет) — з'їжджаєш униз: на хребет не вистрибати.</summary>
    void SlideIfTooSteep(float dt)
    {
        if (dt <= 0f || MoonTerrain.Instance == null) return;
        Vector3 p = transform.position;
        const float e = 0.5f;
        float gx = (MoonTerrain.HeightAt(p.x + e, p.z) - MoonTerrain.HeightAt(p.x - e, p.z)) / (2f * e);
        float gz = (MoonTerrain.HeightAt(p.x, p.z + e) - MoonTerrain.HeightAt(p.x, p.z - e)) / (2f * e);
        float slope = Mathf.Sqrt(gx * gx + gz * gz);
        if (slope <= Mathf.Tan(MoonTerrain.Instance.maxWalkSlope * Mathf.Deg2Rad)) return;
        velocity += new Vector3(-gx, 0f, -gz) / slope * 9f * dt; // униз по схилу
    }

    // ---------- Стрибок ----------

    static bool JumpPressed()
    {
        var k = Keyboard.current;
        return k != null && k.spaceKey.wasPressedThisFrame;
    }

    /// <summary>Злетіти: швидкість угору така, щоб піднятися на jumpHeight і пробути в повітрі jumpAirSeconds.</summary>
    void StartJump()
    {
        airborne = true;
        verticalSpeed = 4f * jumpHeight / Mathf.Max(0.2f, jumpAirSeconds);
        hop.TakeOff(suitOn);
    }

    /// <summary>
    /// Політ: «гравітація» тягне вниз повільно (парабола). Ноги торкнулися землі — приземлення.
    /// (Справжня місячна g = 1,62 м/с²; у грі трохи сильніша, щоб стрибок тривав ≈1,6 с, а не 3.)
    /// </summary>
    void Fly(float dt)
    {
        if (dt <= 0f) return;
        float t = Mathf.Max(0.2f, jumpAirSeconds);
        float gravity = 8f * jumpHeight / (t * t);
        verticalSpeed -= gravity * dt;
        var hit = controller.Move(new Vector3(0f, verticalSpeed * dt, 0f));
        if ((hit & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f; // ударився головою

        Vector3 p = transform.position;
        float ground = MoonTerrain.HeightAt(p);
        bool onSomething = (hit & CollisionFlags.Below) != 0; // приземлився на обладнання (батарея, ящик)
        if (verticalSpeed <= 0f && (onSomething || p.y - FootOffset <= ground))
        {
            if (onSomething) ground = p.y - FootOffset; // лишаємось там, де стоїмо
            controller.Move(new Vector3(0f, ground + FootOffset - p.y, 0f));
            airborne = false;
            verticalSpeed = 0f;
            jumpCooldownLeft = jumpCooldown;
            hop.Land(suitOn);
        }
    }

    /// <summary>У повітрі: якщо попереду земля вища за ноги (схил хребта, вал) — врізався, далі не летиш.</summary>
    Vector3 LimitInAir(Vector3 step)
    {
        float feet = transform.position.y - FootOffset;
        if (MoonTerrain.HeightAt(transform.position + step) > feet + 0.25f)
        {
            velocity = Vector3.zero;
            return Vector3.zero;
        }
        return step;
    }

    /// <summary>
    /// Крок по рельєфу: якщо попереду надто круто вгору (стінка хребта), іти туди не можна.
    /// Тоді пробуємо ковзнути вздовж схилу (по горизонталі), як по стіні; не виходить — зупиняємось.
    /// </summary>
    Vector3 LimitBySlope(Vector3 step)
    {
        Vector3 p = transform.position;
        if (CanGo(p, step)) return step;

        // Уздовж схилу = перпендикулярно до напрямку, куди земля росте найшвидше
        const float e = 0.5f;
        float gx = MoonTerrain.HeightAt(p.x + e, p.z) - MoonTerrain.HeightAt(p.x - e, p.z);
        float gz = MoonTerrain.HeightAt(p.x, p.z + e) - MoonTerrain.HeightAt(p.x, p.z - e);
        var along = new Vector3(-gz, 0f, gx);
        if (along.sqrMagnitude > 1e-6f)
        {
            along.Normalize();
            Vector3 slide = along * Vector3.Dot(step, along);
            if (CanGo(p, slide))
            {
                velocity = along * Vector3.Dot(velocity, along);
                return slide;
            }
        }
        velocity = Vector3.zero;
        return Vector3.zero;
    }

    static bool CanGo(Vector3 p, Vector3 step)
    {
        float len = step.magnitude;
        if (len < 0.0001f) return true;
        Vector3 ahead = p + step / len * Mathf.Max(len, 0.4f); // міряємо схил на 0,4 м уперед — так надійніше
        return MoonTerrain.CanWalk(p, ahead);
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
        // Запобіжник: далі підніжжя хребта не відходимо, навіть якщо схил десь виявився пологим
        float r = Mathf.Sqrt(x * x + z * z), max = MoonTerrain.MaxRadius;
        if (r > max) { x *= max / r; z *= max / r; }
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
