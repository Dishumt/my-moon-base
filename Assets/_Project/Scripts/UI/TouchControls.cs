// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Кнопки на екрані для телефона й планшета (гра відкривається в браузері, а там немає клавіатури).
///  - зліва — джойстик: тягни кружечок пальцем, і інженер іде туди;
///  - справа — велика кнопка F (дія: будувати, брати трубу; тримати — лагодити), кнопка стрибка,
///    і маленькі кнопки камери: &lt; &gt; — повернути, + і - — наблизити й віддалити.
/// Планшет, меню, пауза, швидкість, звук і допомога вже мають свої кнопки в HUD — їх тут не дублюємо.
/// Кнопки з'являються самі на телефоні або щойно гравець торкнувся екрана, і ховаються, коли відкритий
/// планшет, меню чи звіт. Усе передається через GameInput, тож скрипти світу не знають, звідки прийшла команда.
///
/// Для браузера легко: кілька елементів UI Toolkit, створених один раз; щокадру — тільки перевірка, чи їх показати.
/// Нічого не треба ставити в сцену: створюється сам при старті гри.
/// </summary>
[DefaultExecutionOrder(-200)] // раніше за інші скрипти: натискання з цього кадру вже видно в їхньому Update
public class TouchControls : MonoBehaviour
{
    const float StickRadius = 80f;   // радіус кола джойстика (у пікселях інтерфейсу 1280×720)
    const float KnobSize = 64f;

    static readonly Color Panel = new Color(0.016f, 0.063f, 0.1f, 0.5f);
    static readonly Color Line = new Color(0.5f, 0.89f, 1f, 0.65f);
    static readonly Color Knob = new Color(0.5f, 0.89f, 1f, 0.8f);
    static readonly Color Pressed = new Color(0.5f, 0.89f, 1f, 0.45f);

    UIDocument doc;
    VisualElement root, stickBase, knob, buttons;
    Label jumpLabel, turnPhone;
    int stickPointer = -1;
    string shownLanguage;
    bool visible, portraitShown;

    // натискання, що прийшли з UI між кадрами, — віддаємо в GameInput на початку наступного Update
    bool pendingUse, pendingJump;
    int pendingTurn, pendingZoom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<TouchControls>() != null) return;
        var go = new GameObject("Touch Controls");
        DontDestroyOnLoad(go);
        go.AddComponent<TouchControls>();
    }

    void Update()
    {
        // Телефон чи планшет: одразу. Ноутбук із сенсорним екраном чи iPad (каже браузеру, що він «Mac») — після першого дотику.
        if (!GameInput.TouchActive)
        {
            var touch = Touchscreen.current;
            if (Application.isMobilePlatform || (touch != null && touch.primaryTouch.press.wasPressedThisFrame))
                GameInput.TouchStarted();
            else return;
        }

        bool want = ShouldShow();
        if (want && root == null && !Build()) return;
        if (want != visible) SetVisible(want);
        if (!visible) return;

        if (pendingUse) { pendingUse = false; GameInput.TouchUse(); }
        if (pendingJump) { pendingJump = false; GameInput.TouchJump(); }
        if (pendingTurn != 0) { GameInput.TouchTurn(pendingTurn); pendingTurn = 0; }
        if (pendingZoom != 0) { GameInput.TouchZoom(pendingZoom); pendingZoom = 0; }

        if (shownLanguage != Loc.Language) RefreshTexts();
        bool portrait = Screen.height > Screen.width; // тримаєш телефон вертикально — просимо повернути
        if (portrait != portraitShown) { portraitShown = portrait; UiKit.Show(turnPhone, portrait); }
    }

    /// <summary>Кнопки потрібні, тільки коли інженер ходить: не в меню, не в планшеті, не у вікні звіту.</summary>
    static bool ShouldShow()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || EngineerController.Instance == null) return false;
        if (UiKit.Blocked) return false;
        return TabletController.Instance == null || !TabletController.Instance.IsOpen;
    }

    void SetVisible(bool on)
    {
        visible = on;
        UiKit.Show(root, on);
        if (on) return;
        // сховали посеред руху — відпускаємо все, щоб інженер не йшов сам
        ResetStick();
        GameInput.TouchReleaseAll();
        pendingUse = pendingJump = false;
        pendingTurn = pendingZoom = 0;
    }

    void RefreshTexts()
    {
        shownLanguage = Loc.Language;
        jumpLabel.text = Loc.Get("touch.jump");
        turnPhone.text = Loc.Get("touch.turn_phone");
    }

    // ---------- створюємо один раз ----------

    bool Build()
    {
        if (doc == null)
        {
            var look = ModuleLook.Instance;
            if (look == null || look.uiPanel == null) return false;
            doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = look.uiPanel;
            doc.sortingOrder = 45; // над HUD, під підказкою біля бура (48), планшетом і вікнами
        }
        var docRoot = doc.rootVisualElement;
        if (docRoot == null) return false;
        docRoot.pickingMode = PickingMode.Ignore;

        root = new VisualElement { name = "touch-controls", pickingMode = PickingMode.Ignore };
        root.style.position = Position.Absolute;
        root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
        docRoot.Add(root);

        BuildStick();
        BuildButtons();

        turnPhone = new Label { pickingMode = PickingMode.Ignore };
        turnPhone.style.position = Position.Absolute;
        turnPhone.style.left = 0;
        turnPhone.style.right = 0;
        turnPhone.style.top = 140;
        turnPhone.style.unityTextAlign = TextAnchor.MiddleCenter;
        turnPhone.style.fontSize = 26;
        turnPhone.style.color = Color.white;
        turnPhone.style.whiteSpace = WhiteSpace.Normal;
        turnPhone.style.backgroundColor = new Color(0.016f, 0.063f, 0.1f, 0.85f);
        turnPhone.style.paddingTop = turnPhone.style.paddingBottom = 12;
        root.Add(turnPhone);
        portraitShown = false;
        UiKit.Show(turnPhone, false);

        RefreshTexts();
        visible = true;
        return true;
    }

    void BuildStick()
    {
        // Зона дотику більша за коло: палець не мусить влучити точно
        var area = new VisualElement { name = "touch-stick" };
        area.style.position = Position.Absolute;
        area.style.left = 0;
        area.style.bottom = 0;
        area.style.width = 320;
        area.style.height = 300;
        root.Add(area);

        stickBase = Circle(area, StickRadius * 2f, Panel, Line, 2f);
        stickBase.pickingMode = PickingMode.Ignore;
        stickBase.style.position = Position.Absolute;
        stickBase.style.left = 50;
        stickBase.style.bottom = 40;

        knob = Circle(stickBase, KnobSize, Knob, Knob, 0f);
        knob.pickingMode = PickingMode.Ignore;
        knob.style.position = Position.Absolute;
        ResetStick();

        area.RegisterCallback<PointerDownEvent>(e =>
        {
            if (stickPointer >= 0 && area.HasPointerCapture(stickPointer)) return; // джойстик уже тримає інший палець
            stickPointer = e.pointerId;
            area.CapturePointer(e.pointerId);
            Drag(e.position);
        });
        area.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == stickPointer) Drag(e.position); });
        area.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == stickPointer) { area.ReleasePointer(e.pointerId); ResetStick(); } });
        area.RegisterCallback<PointerCaptureOutEvent>(e => ResetStick());
    }

    /// <summary>Палець на екрані (у координатах інтерфейсу) → куди йти. Вгору на екрані — вперед.</summary>
    void Drag(Vector2 finger)
    {
        Vector2 offset = finger - stickBase.worldBound.center;
        if (offset.sqrMagnitude > StickRadius * StickRadius) offset = offset.normalized * StickRadius;
        Vector2 move = offset / StickRadius;
        if (move.sqrMagnitude < 0.15f * 0.15f) move = Vector2.zero; // маленький рух пальця — ще не йдемо
        GameInput.TouchMove(new Vector2(move.x, -move.y));
        PlaceKnob(offset);
    }

    void ResetStick()
    {
        stickPointer = -1;
        GameInput.TouchMove(Vector2.zero);
        if (knob != null) PlaceKnob(Vector2.zero);
    }

    void PlaceKnob(Vector2 offset)
    {
        knob.style.left = StickRadius - KnobSize / 2f + offset.x;
        knob.style.top = StickRadius - KnobSize / 2f + offset.y;
    }

    void BuildButtons()
    {
        // Справа внизу, над кнопкою планшета HUD
        buttons = new VisualElement { pickingMode = PickingMode.Ignore };
        buttons.style.position = Position.Absolute;
        buttons.style.right = 18;
        buttons.style.bottom = 80;
        buttons.style.alignItems = Align.FlexEnd;
        root.Add(buttons);

        var camera = Row(buttons);
        camera.style.marginBottom = 14;
        Button(camera, "<", 50f, 24, () => pendingTurn = -1, null);
        Button(camera, ">", 50f, 24, () => pendingTurn = 1, null);
        Button(camera, "+", 50f, 26, () => pendingZoom = 1, null);
        Button(camera, "-", 50f, 30, () => pendingZoom = -1, null);

        var main = Row(buttons);
        main.style.alignItems = Align.FlexEnd;
        var jump = Button(main, "", 76f, 15, () => pendingJump = true, null);
        jumpLabel = jump.Q<Label>();
        jump.style.marginRight = 16;
        // F — як у підказках на екрані («F — збудувати бур»); тримати — лагодити поломку
        Button(main, "F", 100f, 40, () => { pendingUse = true; GameInput.TouchUseHeld(true); }, () => GameInput.TouchUseHeld(false));
    }

    static VisualElement Row(VisualElement parent)
    {
        var row = new VisualElement { pickingMode = PickingMode.Ignore };
        row.style.flexDirection = FlexDirection.Row;
        parent.Add(row);
        return row;
    }

    /// <summary>Кругла кнопка: down — палець торкнувся, up — відпустив (або зісковзнув).</summary>
    VisualElement Button(VisualElement parent, string text, float size, int fontSize, Action down, Action up)
    {
        var b = Circle(parent, size, Panel, Line, 2f);
        b.style.marginLeft = 6;
        b.style.alignItems = Align.Center;
        b.style.justifyContent = Justify.Center;
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = fontSize;
        label.style.color = Color.white;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        b.Add(label);

        int pointer = -1;
        void Release()
        {
            if (pointer < 0) return;
            if (b.HasPointerCapture(pointer)) b.ReleasePointer(pointer);
            pointer = -1;
            b.style.backgroundColor = Panel;
            up?.Invoke();
        }
        b.RegisterCallback<PointerDownEvent>(e =>
        {
            Release(); // старий дотик міг загубитися, поки кнопки були сховані
            pointer = e.pointerId;
            b.CapturePointer(e.pointerId);
            b.style.backgroundColor = Pressed;
            down?.Invoke();
        });
        b.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == pointer) Release(); });
        b.RegisterCallback<PointerCaptureOutEvent>(e => Release());
        return b;
    }

    static VisualElement Circle(VisualElement parent, float size, Color fill, Color border, float borderWidth)
    {
        var c = new VisualElement();
        c.style.width = c.style.height = size;
        c.style.borderTopLeftRadius = c.style.borderTopRightRadius = size / 2f;
        c.style.borderBottomLeftRadius = c.style.borderBottomRightRadius = size / 2f;
        c.style.backgroundColor = fill;
        c.style.borderTopColor = c.style.borderBottomColor = border;
        c.style.borderLeftColor = c.style.borderRightColor = border;
        c.style.borderTopWidth = c.style.borderBottomWidth = borderWidth;
        c.style.borderLeftWidth = c.style.borderRightWidth = borderWidth;
        parent.Add(c);
        return c;
    }
}
