// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Керування грою в одному місці: клавіатура й мишка на комп'ютері АБО кнопки на екрані телефона чи планшета (TouchControls).
/// Скрипти світу питають тут, а не в Keyboard.current, тож гра однаково працює і так, і так.
///   GameInput.Move        — куди йти (WASD / стрілки / джойстик), від (-1,-1) до (1,1);
///   GameInput.UsePressed  — щойно натиснули «дію» (F / кнопка F);  UseHeld — тримають її;
///   GameInput.JumpPressed — стрибок (пробіл / кнопка);
///   GameInput.Turn        — повернути камеру: -1 (Q), +1 (E), 0;
///   GameInput.Zoom        — наблизити +1 / віддалити -1 (коліщатко мишки / кнопки + і -).
/// Кнопки на екрані повідомляють сюди через Touch*-методи (тільки TouchControls).
/// </summary>
public static class GameInput
{
    static Vector2 touchMove;
    static bool touchUseHeld;
    static int useFrame = -1, jumpFrame = -1, turnFrame = -1, zoomFrame = -1;
    static int turnDir, zoomDir;

    /// <summary>Гра керується дотиками (телефон, планшет): показуємо кнопки на екрані й підказки для них.</summary>
    public static bool TouchActive { get; private set; }

    /// <summary>Куди йти: від (-1,-1) до (1,1). По діагоналі не швидше.</summary>
    public static Vector2 Move
    {
        get
        {
            Vector2 v = touchMove;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
            }
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }

    /// <summary>Щойно натиснули «дію» (будувати, взяти трубу...). True лише в один кадр.</summary>
    public static bool UsePressed
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.fKey.wasPressedThisFrame) || useFrame == Time.frameCount;
        }
    }

    /// <summary>«Дію» тримають (лагодити поломку).</summary>
    public static bool UseHeld
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.fKey.isPressed) || touchUseHeld;
        }
    }

    /// <summary>Щойно натиснули стрибок.</summary>
    public static bool JumpPressed
    {
        get
        {
            var k = Keyboard.current;
            return (k != null && k.spaceKey.wasPressedThisFrame) || jumpFrame == Time.frameCount;
        }
    }

    /// <summary>Повернути камеру на крок: -1 — проти годинникової стрілки (Q), +1 — за (E), 0 — ні.</summary>
    public static int Turn
    {
        get
        {
            var k = Keyboard.current;
            if (k != null && k.qKey.wasPressedThisFrame) return -1;
            if (k != null && k.eKey.wasPressedThisFrame) return 1;
            return turnFrame == Time.frameCount ? turnDir : 0;
        }
    }

    /// <summary>Наблизити камеру (+1) чи віддалити (-1). Різні миші дають різні числа, тому беремо тільки напрямок.</summary>
    public static int Zoom
    {
        get
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) >= 0.01f) return scroll > 0f ? 1 : -1;
            }
            return zoomFrame == Time.frameCount ? zoomDir : 0;
        }
    }

    // ---------- для кнопок на екрані (TouchControls) ----------

    public static void TouchStarted() => TouchActive = true;
    public static void TouchMove(Vector2 v) => touchMove = v.sqrMagnitude > 1f ? v.normalized : v;
    public static void TouchUseHeld(bool held) => touchUseHeld = held;
    public static void TouchUse() => useFrame = Time.frameCount;
    public static void TouchJump() => jumpFrame = Time.frameCount;
    public static void TouchTurn(int dir) { turnDir = dir; turnFrame = Time.frameCount; }
    public static void TouchZoom(int dir) { zoomDir = dir; zoomFrame = Time.frameCount; }

    /// <summary>Відпустити все (кнопки сховалися: відкрили планшет чи меню).</summary>
    public static void TouchReleaseAll()
    {
        touchMove = Vector2.zero;
        touchUseHeld = false;
    }

    /// <summary>Скинути перед кожним Play (Unity може не перезапускати статичні поля).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        touchMove = Vector2.zero;
        touchUseHeld = false;
        useFrame = jumpFrame = turnFrame = zoomFrame = -1;
        TouchActive = false;
    }
}
