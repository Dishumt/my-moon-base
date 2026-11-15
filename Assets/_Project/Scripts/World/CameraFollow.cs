// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Камера як у Don't Starve Together: дивиться згори під кутом і плавно йде за інженером.
/// Колесо миші наближає й віддаляє камеру.
/// Став на Main Camera, а в поле Target перетягни інженера.
/// Якщо Target порожній, камера шукає об'єкт з тегом "Player".
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Tooltip("За ким іде камера (інженер)")]
    [SerializeField] Transform target;

    [Header("Кут")]
    [Tooltip("Нахил униз у градусах: 90 = дивиться прямо вниз, 40–45 = як у DST")]
    [Range(20f, 80f)] [SerializeField] float pitch = 45f;
    [Tooltip("Поворот навколо вертикалі в градусах")]
    [SerializeField] float yaw = 0f;

    [Header("Відстань і масштаб")]
    [SerializeField] float distance = 16f;
    [SerializeField] float minDistance = 8f;
    [SerializeField] float maxDistance = 30f;
    [Tooltip("На скільки змінюється відстань за один «клік» колеса миші")]
    [SerializeField] float zoomStep = 2f;

    [Header("Плавність")]
    [Tooltip("Більше = камера швидше наздоганяє інженера")]
    [SerializeField] float followSpeed = 6f;
    [Tooltip("Більше = масштаб змінюється швидше")]
    [SerializeField] float zoomSpeed = 8f;

    [Tooltip("Точка, на яку дивимось, трохи вище ніг інженера")]
    [SerializeField] float lookHeight = 0.5f;

    float targetDistance;   // відстань, до якої плавно наближаємось
    Vector3 focus;          // точка на землі, за якою стежить камера

    void Start()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else Debug.LogWarning("CameraFollow: не задано Target і немає об'єкта з тегом Player.");
        }

        targetDistance = distance;
        if (target != null) focus = FocusPoint();
        ApplyPosition();
    }

    /// <summary>
    /// Куди дивитись: на інженера, але по висоті — на землю під ним (+0,5 м, як раніше),
    /// щоб камера не підскакувала разом із місячним стрибком.
    /// </summary>
    Vector3 FocusPoint()
    {
        Vector3 p = target.position;
        p.y = MoonTerrain.Instance != null ? MoonTerrain.HeightAt(p) + 0.5f : p.y;
        return p;
    }

    void LateUpdate() // LateUpdate іде після руху інженера, тож камера не тремтить
    {
        ReadZoom();

        // Плавно: щокадру проходимо частину шляху до цілі.
        float dt = Time.unscaledDeltaTime;
        if (target != null) focus = Vector3.Lerp(focus, FocusPoint(), 1f - Mathf.Exp(-followSpeed * dt));
        distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSpeed * dt));

        ApplyPosition();
    }

    void ReadZoom()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        // Різні браузери й миші дають різні числа, тому беремо тільки напрямок: +1 або -1.
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) return;
        targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
    }

    /// <summary>Ставить камеру на відстань distance від точки focus під кутом pitch/yaw.</summary>
    void ApplyPosition()
    {
        var rotation = Quaternion.Euler(pitch, yaw, 0f);
        var lookAt = focus + Vector3.up * lookHeight;
        // Біля хребта камера могла б опинитися всередині гори — тоді підсуваємо її ближче
        float d = distance;
        Vector3 pos = lookAt - rotation * Vector3.forward * d;
        for (int i = 0; i < 12 && d > 4f && pos.y < MoonTerrain.HeightAt(pos) + 2f; i++)
        {
            d *= 0.85f;
            pos = lookAt - rotation * Vector3.forward * d;
        }
        transform.SetPositionAndRotation(pos, rotation);
    }

    /// <summary>Миттєво перенести камеру до цілі (наприклад, після завантаження гри).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        focus = FocusPoint();
        distance = targetDistance;
        ApplyPosition();
    }

    /// <summary>Поточний поворот камери навколо вертикалі (знадобиться для руху WASD).</summary>
    public float Yaw => yaw;
}
