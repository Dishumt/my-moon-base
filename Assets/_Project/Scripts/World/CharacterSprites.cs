// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Вибір кадру для персонажа (інженер, астронавти): у який бік дивиться, стоїть чи стрибає,
/// нога на землі чи в польоті. Спільне для EngineerController і AstronautView.
/// </summary>
public static class CharacterSprites
{
    /// <summary>
    /// Кадр з набору set. facing — напрямок «на екрані» (x вправо, y вгору по екрану).
    /// hop — місячні стрибки (може бути null = стоїть). flip — чи дзеркалити картинку.
    /// </summary>
    public static Sprite Pick(EngineerController.SpriteSet set, Vector2 facing, MoonHop hop, out bool flip)
    {
        flip = false;
        bool sideways = Mathf.Abs(facing.x) > Mathf.Abs(facing.y);
        Sprite[] walk;
        Sprite idle;

        if (sideways)
        {
            bool right = facing.x > 0f;
            walk = right ? set.walkRight : set.walkLeft;
            if (right && IsEmpty(walk)) { walk = set.walkLeft; flip = true; } // правих кадрів нема — дзеркалимо ліві
            idle = IsEmpty(walk) ? set.idleFront : walk[0];
        }
        else if (facing.y > 0f) { walk = set.walkBack; idle = set.idleBack; }
        else { walk = set.walkFront; idle = set.idleFront; }

        if (hop == null || !hop.IsHopping || IsEmpty(walk)) return idle;

        // Нога на землі — кадр поштовху цієї ноги; у польоті — кадр «ноги разом». Кожен стрибок — інша нога.
        int[] map = hop.OnGround ? set.pushFrames : set.airFrames;
        int i = map != null && map.Length > hop.Leg ? map[hop.Leg] : 0;
        return walk[Mathf.Clamp(i, 0, walk.Length - 1)];
    }

    public static bool IsEmpty(Sprite[] frames) => frames == null || frames.Length == 0 || frames[0] == null;
    public static bool IsEmpty(EngineerController.SpriteSet s) => s == null || (s.idleFront == null && IsEmpty(s.walkFront));

    /// <summary>Напрямок руху у світі → напрямок «на екрані» (для вибору кадру).</summary>
    public static Vector2 ToScreen(Vector3 worldDir)
    {
        var cam = Camera.main;
        if (cam == null) return new Vector2(worldDir.x, worldDir.z);
        Vector3 right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;
        Vector3 fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd));
    }
}
