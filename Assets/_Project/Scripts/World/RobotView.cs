// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Робот-будівельник у світі — 2D-спрайт Юлі (Art/Characters/robot), як персонажі.
/// Читає з GameState свій RobotState:
///  - TargetSlot >= 0 → їде до цього модуля (в обхід інших) і стає біля нього; кадри «їде» — у бік руху;
///  - Working = true → працює: будує (маніпулятор з ключем) або засипає модуль реголітом, обличчям до модуля;
///  - є завдання, але немає енергії → спить, над ним іконка енергії (гравець бачить, що роботам бракує енергії);
///  - без завдання → повертається в «ангар» перед базою і спить.
/// Якщо кадрів робота немає (меню Moon → Налаштувати екіпаж і роботів не запускали) — запасний ровер з фігур.
/// Створює його CrewView сам — руками в сцену не став.
/// </summary>
public class RobotView : MonoBehaviour
{
    RobotState data;
    int index;

    // Спрайт (кадри Юлі)
    SpriteRenderer body, icon;
    Billboard iconBillboard;
    float animTime;
    Vector3 moveDir = Vector3.back; // куди їхали востаннє (для вибору кадрів)

    // Запасний 3D-ровер з фігур
    Transform model, arm, head;
    Renderer lamp;
    Vector3 target;
    bool hasTarget;
    List<Vector3> path;
    int pathIndex;
    float workTime;

    static readonly Color Yellow = new Color(1f, 0.85f, 0.2f);
    static readonly Color Red = new Color(0.95f, 0.25f, 0.2f);
    static readonly Color Green = new Color(0.3f, 0.9f, 0.4f);

    /// <summary>Викликає CrewView одразу після створення.</summary>
    public void Init(RobotState r, int index)
    {
        data = r;
        this.index = index;
        var cl = CrewLook.Instance;
        if (cl != null && cl.HasRobotSprites) BuildSprite();
        else BuildModel();
        transform.position = HomeSpot();
    }

    void BuildSprite()
    {
        var go = new GameObject("Sprite");
        go.transform.SetParent(transform, false);
        body = go.AddComponent<SpriteRenderer>();
        go.AddComponent<Billboard>(); // стоїть рівно, як персонажі

        // Тінь-пляма під колесами (та сама, що під людьми й меблями)
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(transform, false);
        sh.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // лежить на землі
        sh.transform.localScale = new Vector3(1.1f, 0.55f, 1f);
        var shadow = sh.AddComponent<SpriteRenderer>();
        shadow.sprite = WorldSprites.Blob;
        shadow.color = new Color(0f, 0f, 0f, 0.38f);
        shadow.sortingOrder = -1;

        // Іконка над головою: «немає енергії»
        var ic = new GameObject("Status Icon");
        ic.transform.SetParent(transform, false);
        ic.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        icon = ic.AddComponent<SpriteRenderer>();
        icon.sortingOrder = 2;
        icon.enabled = false;
        iconBillboard = ic.AddComponent<Billboard>();
        iconBillboard.BillboardMode = Billboard.Mode.FaceCamera;
    }

    void BuildModel()
    {
        var look = ModuleLook.Instance;
        var mat = look != null ? look.wallMaterial : null;
        model = new GameObject("Model").transform;
        model.SetParent(transform, false);

        Color body = new Color(0.95f, 0.75f, 0.3f), dark = new Color(0.3f, 0.32f, 0.36f), metal = new Color(0.7f, 0.72f, 0.75f);
        ModuleParts.Box(model, "Body", mat, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.45f, 1.2f), false, body);
        foreach (float x in new[] { -0.5f, 0.5f })
            foreach (float z in new[] { -0.4f, 0.4f })
            {
                var w = ModuleParts.Cylinder(model, "Wheel", mat, new Vector3(x, 0.25f, z), new Vector3(0.5f, 0.08f, 0.5f), false, dark);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        head = ModuleParts.Box(model, "Head", mat, new Vector3(0f, 0.95f, 0.25f), new Vector3(0.5f, 0.35f, 0.4f), false, metal).transform;
        ModuleParts.Box(head, "Eye", mat, new Vector3(0f, 0f, 0.51f), new Vector3(0.7f, 0.3f, 0.05f), false, new Color(0.15f, 0.2f, 0.35f));
        lamp = ModuleParts.Sphere(model, "Lamp", mat, new Vector3(0f, 1.2f, -0.2f), new Vector3(0.18f, 0.18f, 0.18f), false, Green)
                          .GetComponent<Renderer>();

        // Маніпулятор: «плече» спереду, обертається, коли робот працює
        arm = new GameObject("Arm").transform;
        arm.SetParent(model, false);
        arm.localPosition = new Vector3(0.3f, 0.7f, 0.55f);
        ModuleParts.Box(arm, "Arm Part", mat, new Vector3(0f, 0f, 0.35f), new Vector3(0.12f, 0.12f, 0.7f), false, metal);
        ModuleParts.Box(arm, "Tool", mat, new Vector3(0f, -0.08f, 0.72f), new Vector3(0.2f, 0.2f, 0.12f), false, dark);
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null || data == null) return;
        bool paused = game.Clock != null && game.Clock.IsPaused;
        float dt = paused ? 0f : Time.deltaTime;

        // Куди їхати: до будови (квадратик TargetSlot) або додому
        Vector3 want = HomeSpot();
        var mv = data.TargetSlot >= 0 ? BaseView.Find(data.TargetSlot) : null;
        if (mv != null) want = mv.RobotSpot(index);
        if (!hasTarget || (want - target).sqrMagnitude > 0.25f)
        {
            target = want;
            hasTarget = true;
            path = BaseView.PathTo(transform.position, target, 0.6f); // робот ширший — більший запас
            pathIndex = 0;
        }

        bool driving = false;
        if (path != null && pathIndex < path.Count)
        {
            if (Drive(path[pathIndex], dt)) pathIndex++;
            else driving = true;
        }
        bool atSite = !driving && mv != null;

        if (body != null)
        {
            AnimateSprite(driving, atSite, mv, dt);
            return;
        }

        // ----- Запасний 3D-ровер -----
        // Лампа: жовта — працює, червона — є завдання, але немає енергії, зелена — вільний
        Color c = data.Working ? Yellow : (data.Task != RobotTask.Idle ? Red : Green);
        if (data.Working && Mathf.Repeat(Time.time * 3f, 1f) > 0.5f) c = Color.white; // блимає
        ModuleParts.SetColor(lamp, c);

        // Робота: маніпулятор і «голова» рухаються, корпус трохи тремтить
        if (atSite && data.Working && dt > 0f)
        {
            workTime += dt;
            arm.localRotation = Quaternion.Euler(20f + Mathf.Sin(workTime * 6f) * 25f, 0f, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Sin(workTime * 2f) * 8f, Mathf.Sin(workTime * 1.3f) * 20f, 0f);
            model.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(workTime * 20f)) * 0.02f, 0f);
            FaceTowards(mv.transform.position, dt);
        }
        else
        {
            arm.localRotation = Quaternion.Slerp(arm.localRotation, Quaternion.identity, dt * 4f);
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.identity, dt * 4f);
            model.localPosition = Vector3.zero;
            if (atSite) FaceTowards(mv.transform.position, dt);
        }
    }

    /// <summary>
    /// Кадри Юлі: їде — у бік руху; на будові працює — будує або засипає реголітом (обличчям до модуля);
    /// без роботи або без енергії — спить. Без енергії над ним ще й іконка енергії.
    /// </summary>
    void AnimateSprite(bool driving, bool atSite, ModuleView mv, float dt)
    {
        var cl = CrewLook.Instance;
        if (cl == null) return;
        animTime += dt; // пауза — анімація теж стоїть

        Sprite[] frames;
        bool flip = false;
        if (driving)
        {
            Vector2 f = CharacterSprites.ToScreen(moveDir);
            if (Mathf.Abs(f.x) > Mathf.Abs(f.y))
            {
                frames = f.x > 0f ? cl.robotRight : cl.robotLeft;
                if (CharacterSprites.IsEmpty(frames)) { frames = f.x > 0f ? cl.robotLeft : cl.robotRight; flip = true; }
            }
            else frames = f.y > 0f ? cl.robotDriveAway : cl.robotDriveToward;
        }
        else if (atSite && data.Working)
        {
            frames = data.Task == RobotTask.Shield && !CharacterSprites.IsEmpty(cl.robotShield) ? cl.robotShield : cl.robotBuild;
            // Кадри роботи дивляться праворуч; модуль ліворуч від робота — дзеркалимо
            flip = CharacterSprites.ToScreen(mv.transform.position - transform.position).x < 0f;
        }
        else frames = cl.robotSleep;

        if (CharacterSprites.IsEmpty(frames)) frames = cl.robotDriveToward;
        float fps = cl.robotFps > 0f ? cl.robotFps : 10f;
        body.sprite = frames[(int)(animTime * fps) % frames.Length];
        body.flipX = flip;

        // Є завдання, але немає енергії — іконка енергії над головою «дихає», щоб її помітили
        bool noPower = data.Task != RobotTask.Idle && !data.Working && cl.iconNoEnergy != null;
        icon.enabled = noPower;
        if (noPower)
        {
            icon.sprite = cl.iconNoEnergy;
            float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 4f);
            iconBillboard.ExtraScale = new Vector2(pulse, pulse);
        }
    }

    /// <summary>Їхати до точки. true — приїхали.</summary>
    bool Drive(Vector3 to, float dt)
    {
        Vector3 d = to - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.08f) return true;
        if (dt <= 0f) return false;
        var cl = CrewLook.Instance;
        float speed = cl != null ? cl.robotSpeed : 2.5f;
        transform.position += d / dist * Mathf.Min(dist, speed * dt);
        moveDir = d / dist;
        if (body == null) FaceTowards(to, dt); // спрайт повертає Billboard, а ровер з фігур — сам
        return false;
    }

    void FaceTowards(Vector3 point, float dt)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        var want = Quaternion.LookRotation(d, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 360f * dt);
    }

    Vector3 HomeSpot()
    {
        var cl = CrewLook.Instance;
        Vector3 home = cl != null ? cl.robotHome : new Vector3(0f, 0f, -27f);
        return home + new Vector3((index - 0.5f) * 1.8f, 0f, 0f); // роботи стоять у ряд
    }
}
