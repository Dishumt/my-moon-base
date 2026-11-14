// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Білборд: 2D-картинка в 3D-світі, яка завжди повернута до камери (як у Don't Starve Together).
/// Став на об'єкт зі SpriteRenderer: персонажа, робота, камінь, предмет.
/// Ноги спрайта мають бути внизу картинки (Pivot унизу), тоді він «стоїть» на землі.
///
/// Режими:
///  - Upright (звичайний): спрайт стоїть рівно вертикально, як картонна фігурка, і повертається тільки вбік.
///    Щоб камера згори не «сплющувала» його, спрайт трохи витягується вгору.
///    Так він не встромляється верхівкою в стіни й двері позаду.
///  - FaceCamera: спрайт лежить паралельно екрану (нахилений назад разом з камерою).
/// </summary>
public class Billboard : MonoBehaviour
{
    public enum Mode { Upright, FaceCamera }

    [SerializeField] Mode mode = Mode.Upright;

    /// <summary>Додатковий масштаб (сплющення при приземленні — ставить MoonHop). 1,1 = без змін.</summary>
    public Vector2 ExtraScale { get; set; } = Vector2.one;

    Transform cam;
    Vector3 baseScale;

    void Awake() => baseScale = transform.localScale;

    void LateUpdate() // після руху камери, щоб не було тремтіння
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        if (mode == Mode.FaceCamera)
        {
            transform.rotation = cam.rotation;
            transform.localScale = new Vector3(baseScale.x * ExtraScale.x, baseScale.y * ExtraScale.y, baseScale.z);
            return;
        }

        // Дивимось у той самий бік, що й камера, але тільки по горизонталі.
        Vector3 forward = cam.forward;
        forward.y = 0f;
        float flat = forward.magnitude;              // = cos(нахилу камери)
        if (flat < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(forward / flat, Vector3.up);

        // Камера під кутом бачить вертикальну фігурку коротшою в cos(кута) разів — компенсуємо.
        float stretch = Mathf.Min(1f / flat, 2f);
        transform.localScale = new Vector3(baseScale.x * ExtraScale.x, baseScale.y * stretch * ExtraScale.y, baseScale.z);
    }
}
