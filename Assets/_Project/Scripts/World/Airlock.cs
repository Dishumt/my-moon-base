// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections;
using UnityEngine;

/// <summary>
/// Шлюз кімнати: тамбур між вулицею і кімнатою з двома дверима.
///  Вхід:  підходиш → відчиняються зовнішні двері → заходиш у тамбур → двері зачиняються →
///         здувається пил → знімаєш скафандр → відчиняються внутрішні двері.
///  Вихід: те саме навпаки, тільки скафандр одягаєш (пил здувати не треба).
/// Поки йде шлюз, інженер не рухається. Ігровий час іде — це ціна кожної вилазки.
/// Цей компонент додає ModuleView сам — руками ставити не треба.
/// </summary>
public class Airlock : MonoBehaviour
{
    enum Step { Idle, OuterOpen, InnerOpen, Busy, PassingIn, PassingOut }

    Transform outerDoor, innerDoor;
    float door, airlockWidth, front, inner;
    ParticleSystem dust;
    Step step = Step.Idle;

    float outerOpen, innerOpen;      // 0 = зачинено, 1 = відчинено (зараз)
    float outerTarget, innerTarget;  // куди рухаються двері

    /// <summary>Викликає ModuleView: двері, ширина дверей і шлюзу, z зовнішньої і внутрішньої стіни, висота.</summary>
    public void Setup(Transform outerDoor, Transform innerDoor, float door, float airlockWidth, float front, float inner, float height)
    {
        this.outerDoor = outerDoor;
        this.innerDoor = innerDoor;
        this.door = door;
        this.airlockWidth = airlockWidth;
        this.front = front;
        this.inner = inner;
        dust = CreateDust(new Vector3(0f, height - 0.2f, (front + inner) / 2));
    }

    void Update()
    {
        var engineer = EngineerController.Instance;
        if (engineer != null && step != Step.Busy) Think(engineer);
        MoveDoors();
    }

    // ---------- Що робити зараз ----------

    void Think(EngineerController engineer)
    {
        Vector3 p = transform.InverseTransformPoint(engineer.transform.position);
        float r = engineer.Radius;
        bool nearOuter = Mathf.Abs(p.x) < door && p.z < front && p.z > front - 2f;  // перед зовнішніми дверима
        bool nearInner = Mathf.Abs(p.x) < door && p.z > inner && p.z < inner + 2f;  // у кімнаті біля внутрішніх
        bool inChamber = Mathf.Abs(p.x) < airlockWidth / 2 && p.z - r > front + 0.1f && p.z + r < inner + 0.05f; // повністю в тамбурі

        switch (step)
        {
            case Step.Idle:
                if (nearOuter) { outerTarget = 1f; step = Step.OuterOpen; }
                else if (nearInner) { innerTarget = 1f; step = Step.InnerOpen; }
                break;

            case Step.OuterOpen:
                if (inChamber) StartCoroutine(Enter(engineer));
                else if (!nearOuter && p.z + r < front) { outerTarget = 0f; step = Step.Idle; } // передумав
                break;

            case Step.InnerOpen:
                if (inChamber) StartCoroutine(Exit(engineer));
                else if (!nearInner && p.z - r > inner) { innerTarget = 0f; step = Step.Idle; }
                break;

            case Step.PassingIn:  // чекаємо, поки зайде в кімнату, і зачиняємо за ним
                if (p.z - r > inner + 0.1f) { innerTarget = 0f; step = Step.Idle; }
                break;

            case Step.PassingOut: // чекаємо, поки вийде надвір
                if (p.z + r < front - 0.1f) { outerTarget = 0f; step = Step.Idle; }
                break;
        }
    }

    IEnumerator Enter(EngineerController engineer)
    {
        step = Step.Busy;
        var look = ModuleLook.Instance;
        engineer.Locked = true;

        outerTarget = 0f;                        // 1. зовнішні двері зачиняються
        yield return Wait(look.doorSeconds);

        if (dust != null) dust.Play();           // 2. здуваємо пил
        engineer.Shake = 0.05f;
        yield return Wait(look.dustSeconds);
        if (dust != null) dust.Stop();
        engineer.Shake = 0f;

        yield return Wait(look.suitSeconds / 2); // 3. знімаємо скафандр
        engineer.SuitOn = false;
        yield return Wait(look.suitSeconds / 2);

        innerTarget = 1f;                        // 4. внутрішні двері відчиняються
        yield return Wait(look.doorSeconds);

        engineer.Locked = false;
        step = Step.PassingIn;
    }

    IEnumerator Exit(EngineerController engineer)
    {
        step = Step.Busy;
        var look = ModuleLook.Instance;
        engineer.Locked = true;

        innerTarget = 0f;                        // 1. внутрішні двері зачиняються
        yield return Wait(look.doorSeconds);

        yield return Wait(look.suitSeconds / 2); // 2. одягаємо скафандр
        engineer.SuitOn = true;
        yield return Wait(look.suitSeconds / 2);

        outerTarget = 1f;                        // 3. зовнішні двері відчиняються
        yield return Wait(look.doorSeconds);

        engineer.Locked = false;
        step = Step.PassingOut;
    }

    /// <summary>Чекати N секунд, але не рахувати час, поки гра на паузі (наприклад, відкритий планшет).</summary>
    static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            var game = GameBootstrap.Instance;
            bool paused = game != null && game.Clock != null && game.Clock.IsPaused;
            if (!paused) t += Time.deltaTime;
            yield return null;
        }
    }

    // ---------- Двері й пил ----------

    void MoveDoors()
    {
        var look = ModuleLook.Instance;
        float speed = look != null && look.doorSeconds > 0f ? Time.deltaTime / look.doorSeconds : 1f;
        outerOpen = Mathf.MoveTowards(outerOpen, outerTarget, speed);
        innerOpen = Mathf.MoveTowards(innerOpen, innerTarget, speed);

        // Половинки дверей стискаються до країв отвору (як у ліфті)
        if (outerDoor != null) SetSlide(outerDoor, outerOpen);
        if (innerDoor != null) SetSlide(innerDoor, innerOpen);
    }

    void SetSlide(Transform d, float open)
    {
        float half = door / 2;
        float w = Mathf.Lerp(half, 0.06f, Mathf.SmoothStep(0f, 1f, open)); // ширина однієї половинки
        for (int i = 0; i < d.childCount; i++)
        {
            var part = d.GetChild(i);
            float side = part.localPosition.x < 0f ? -1f : 1f;
            var scale = part.localScale;
            scale.x = w;
            part.localScale = scale;
            part.localPosition = new Vector3(side * (half - w / 2), 0f, 0f); // притиснута до свого краю
        }
    }

    ParticleSystem CreateDust(Vector3 localPos)
    {
        var look = ModuleLook.Instance;
        if (look == null || look.dustMaterial == null) return null; // без матеріалу частинки були б рожеві

        var go = new GameObject("Dust Blower");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // дме вниз

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 0.8f;
        main.startSpeed = 3f;
        main.startSize = 0.12f;
        main.startColor = new Color(0.75f, 0.72f, 0.68f, 0.8f); // сірий місячний пил
        main.gravityModifier = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 80f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.6f;

        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = look.dustMaterial;
        return ps;
    }
}
