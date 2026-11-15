// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Робот-будівельник у світі: маленький ровер з простих фігур (у мультяшному стилі модулів).
/// Читає з GameState свій RobotState:
///  - TargetSlot >= 0 → їде до цього модуля (в обхід інших) і стає біля нього;
///  - Working = true → «працює»: маніпулятор рухається, лампа блимає жовтим;
///  - є завдання, але немає енергії → лампа червона (гравець бачить, що роботам бракує енергії);
///  - без завдання → повертається в «ангар» перед базою, лампа зелена.
/// Створює його CrewView сам — руками в сцену не став.
/// </summary>
public class RobotView : MonoBehaviour
{
    RobotState data;
    int index;
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
        BuildModel();
        transform.position = HomeSpot();
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

        var pos = transform.position;
        pos.y = MoonTerrain.HeightAt(pos); // їде по рельєфу (біля бази земля рівна)
        transform.position = pos;

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
        FaceTowards(to, dt);
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
