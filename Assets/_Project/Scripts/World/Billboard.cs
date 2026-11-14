// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Білборд: 2D-картинка в 3D-світі, яка завжди повернута до камери (як у Don't Starve Together).
/// Став на об'єкт зі SpriteRenderer: персонажа, робота, камінь, предмет.
/// Порада: ноги спрайта мають бути внизу картинки (Pivot = Bottom), тоді він «стоїть» на землі.
/// </summary>
public class Billboard : MonoBehaviour
{
    [Tooltip("Увімкнено: спрайт стоїть рівно і повертається тільки вбік (не падає назад).\n" +
             "Вимкнено: спрайт нахиляється разом з камерою, як у DST.")]
    [SerializeField] bool keepUpright = false;

    Transform cam;

    void LateUpdate() // після руху камери, щоб не було тремтіння
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        if (keepUpright)
        {
            // Дивимось у той самий бік, що й камера, але тільки по горизонталі.
            Vector3 forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        else
        {
            // Площина спрайта паралельна екрану, тому картинка не спотворюється.
            transform.rotation = cam.rotation;
        }
    }
}
