// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Внутрішні стіни кімнати з текстурою Юлі (як у Don't Starve / Project Zomboid):
///  - видно тільки коли ти в кімнаті (оболонка зникла), інакше вони стирчали б крізь круглу «бочку» модуля;
///  - видно лише дальні стіни (задню й бокові); стіну між камерою і кімнатою ховаємо,
///    щоб вона не закривала людей і меблі. Працює й після повороту камери (Q/E).
/// Додає RoomBuilder сам.
/// </summary>
public class InnerWalls : MonoBehaviour
{
    struct Wall
    {
        public Renderer Renderer;
        public Vector3 Inward; // куди дивиться стіна всередину кімнати (у координатах модуля)
    }

    readonly List<Wall> walls = new List<Wall>();
    ModuleView view;
    Transform cam;

    public void Init(ModuleView view) => this.view = view;

    public void Add(Renderer r, Vector3 inward)
    {
        if (r != null) walls.Add(new Wall { Renderer = r, Inward = inward });
    }

    void LateUpdate()
    {
        bool open = view != null && view.Interior != null && view.Interior.IsOpen;
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        Vector3 forward = cam != null ? cam.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        foreach (var w in walls)
        {
            if (w.Renderer == null) continue;
            // Стіна далеко (її внутрішній бік дивиться на камеру) — показуємо; ближня до камери — ховаємо
            float facing = Vector3.Dot(transform.TransformDirection(w.Inward), forward);
            bool show = open && facing < 0.3f;
            if (w.Renderer.enabled != show) w.Renderer.enabled = show;
        }
    }
}
