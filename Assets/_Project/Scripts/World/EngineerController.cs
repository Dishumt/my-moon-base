// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Інженер (персонаж гравця): ходить на WASD або стрілках.
/// «Вперед» (W) — це вгору по екрану, тобто туди, куди дивиться камера.
/// Поки гра на паузі (наприклад, відкритий планшет), інженер стоїть.
///
/// Як налаштувати:
///  - на об'єкт інженера: тег Player, компоненти CharacterController і цей скрипт;
///  - спрайт — у дочірньому об'єкті зі SpriteRenderer і Billboard; перетягни його в Sprite Renderer.
/// CharacterController — вбудований компонент Unity, який рухає персонажа і не дає пройти крізь стіни.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class EngineerController : MonoBehaviour
{
    [Header("Рух")]
    [Tooltip("Швидкість у метрах за секунду")]
    [SerializeField] float speed = 5f;
    [Tooltip("Межа карти: інженер не відійде від центру далі, ніж на стільки метрів по X і Z")]
    [SerializeField] float mapHalfSize = 200f;

    [Header("Спрайт (можна лишити порожнім, поки немає арту)")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [Tooltip("Обличчям до камери (іде вниз по екрану)")]
    [SerializeField] Sprite front;
    [Tooltip("Спиною до камери (іде вгору по екрану)")]
    [SerializeField] Sprite back;
    [Tooltip("Боком, дивиться ВПРАВО. Для руху вліво картинку дзеркалимо.")]
    [SerializeField] Sprite side;

    CharacterController controller;
    Transform cam;

    /// <summary>Чи інженер зараз іде (знадобиться для анімації й звуку кроків).</summary>
    public bool IsMoving { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Vector2 input = ReadInput();
        IsMoving = input.sqrMagnitude > 0.01f && !IsGamePaused();
        if (!IsMoving) return;

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
        UpdateSprite(input);
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

    /// <summary>Вибирає картинку: спереду, ззаду або збоку (вліво — дзеркально).</summary>
    void UpdateSprite(Vector2 input)
    {
        if (spriteRenderer == null) return;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            if (side != null) spriteRenderer.sprite = side;
            spriteRenderer.flipX = input.x < 0f;
        }
        else
        {
            var s = input.y > 0f ? back : front;
            if (s != null) spriteRenderer.sprite = s;
            spriteRenderer.flipX = false;
        }
    }
}
