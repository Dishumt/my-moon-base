// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Робот-будівельник у світі: маленький ровер на гусеницях з простих фігур (у мультяшному стилі модулів).
/// Їде по рельєфу й нахиляється разом зі схилом, з-під гусениць летить пил і лишається колія (Footprints);
/// коли будує — з інструмента сиплються іскри зварювання, коли засипає модуль реголітом — летить пил.
/// Читає з GameState свій RobotState:
///  - TargetSlot >= 0 → їде до цього модуля (в обхід інших) і стає біля нього;
///  - Working = true → «працює»: маніпулятор рухається, лампа блимає жовтим;
///  - є завдання, але немає енергії → лампа червона (гравець бачить, що роботам бракує енергії);
///  - без завдання → повертається в «ангар» перед базою, лампа зелена;
///  - без завдання, а біля посадкового майданчика лежать ящики з корабля поставки → їде по ящик
///    і везе його в ангар (SupplyLanderView). Це тільки картинка: GameState не змінюється.
/// Створює його CrewView сам — руками в сцену не став.
/// </summary>
public class RobotView : MonoBehaviour
{
    RobotState data;
    int index;
    Transform model, arm, head, tool;
    Renderer lamp;
    readonly List<Transform> wheels = new List<Transform>();
    float wheelSpin;
    Quaternion heading = Quaternion.identity; // куди повернутий (без нахилу)
    Vector3 moveDir = Vector3.forward;
    float trackDist, dustTimer, sparkTimer;
    ParticleSystem fx;      // і пил, і іскри (кожну частинку задаємо окремо)
    bool fxTried;
    Vector3 target;
    bool hasTarget;
    List<Vector3> path;
    int pathIndex;
    float workTime;

    // розвантаження корабля поставки
    Renderer cargoBox;        // ящик спереду, видно тільки коли везе
    int cargoCrate = -1;      // по який ящик їде (-1 — ні по який)
    int cargoGen;             // ящики якої посадки (SupplyLanderView.CargoGeneration)
    Vector3 cratePos;
    bool carrying;
    float pickTime;

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
        // Гусениці: дві темні стрічки з заокругленими кінцями і котками збоку (котки крутяться, коли їде)
        foreach (float x in new[] { -0.55f, 0.55f })
        {
            ModuleParts.Box(model, "Track", mat, new Vector3(x, 0.22f, 0f), new Vector3(0.3f, 0.36f, 1.2f), false, dark);
            foreach (float z in new[] { -0.6f, 0.6f })
            {
                var end = ModuleParts.Cylinder(model, "Track End", mat, new Vector3(x, 0.22f, z), new Vector3(0.36f, 0.15f, 0.36f), false, dark);
                end.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            foreach (float z in new[] { -0.4f, 0f, 0.4f })
            {
                var w = ModuleParts.Cylinder(model, "Road Wheel", mat, new Vector3(x + Mathf.Sign(x) * 0.16f, 0.22f, z),
                                             new Vector3(0.26f, 0.02f, 0.26f), false, metal);
                w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheels.Add(w.transform);
            }
        }

        // Тінь-пляма під ровером (як під людьми)
        var sh = new GameObject("Shadow");
        sh.transform.SetParent(transform, false);
        sh.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        sh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        sh.transform.localScale = new Vector3(1.6f, 2f, 1f);
        var shadow = sh.AddComponent<SpriteRenderer>();
        shadow.sprite = WorldSprites.Blob;
        shadow.color = new Color(0f, 0f, 0f, 0.4f);
        shadow.sortingOrder = -1;
        head = ModuleParts.Box(model, "Head", mat, new Vector3(0f, 0.95f, 0.25f), new Vector3(0.5f, 0.35f, 0.4f), false, metal).transform;
        ModuleParts.Box(head, "Eye", mat, new Vector3(0f, 0f, 0.51f), new Vector3(0.7f, 0.3f, 0.05f), false, new Color(0.15f, 0.2f, 0.35f));
        lamp = ModuleParts.Sphere(model, "Lamp", mat, new Vector3(0f, 1.2f, -0.2f), new Vector3(0.18f, 0.18f, 0.18f), false, Green)
                          .GetComponent<Renderer>();

        // Маніпулятор: «плече» спереду, обертається, коли робот працює
        arm = new GameObject("Arm").transform;
        arm.SetParent(model, false);
        arm.localPosition = new Vector3(0.3f, 0.7f, 0.55f);
        ModuleParts.Box(arm, "Arm Part", mat, new Vector3(0f, 0f, 0.35f), new Vector3(0.12f, 0.12f, 0.7f), false, metal);
        tool = ModuleParts.Box(arm, "Tool", mat, new Vector3(0f, -0.08f, 0.72f), new Vector3(0.2f, 0.2f, 0.12f), false, dark).transform;

        // ящик з корабля поставки — робот тримає його спереду, як навантажувач
        cargoBox = ModuleParts.Box(model, "Cargo", mat, new Vector3(0f, 0.62f, 0.95f), new Vector3(0.55f, 0.55f, 0.55f), false, Color.white)
                              .GetComponent<Renderer>();
        cargoBox.gameObject.SetActive(false);
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
        bool toCrate = CargoTarget(mv == null && data.Task == RobotTask.Idle, ref want);
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
        // Нахиляється разом зі схилом, носом — куди їде
        transform.rotation = Quaternion.FromToRotation(Vector3.up, MoonTerrain.NormalAt(pos.x, pos.z)) * heading;

        // Котки крутяться, з-під гусениць летить пил
        if (driving && dt > 0f)
        {
            wheelSpin += dt * 400f;
            foreach (var w in wheels) w.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, wheelSpin, 0f);
            dustTimer -= dt;
            if (dustTimer <= 0f)
            {
                dustTimer = 0.07f;
                Vector3 rear = transform.TransformPoint(new Vector3(Random.Range(-0.6f, 0.6f), 0.1f, -0.7f));
                Emit(rear, Vector3.up * Random.Range(0.3f, 0.8f) - moveDir * 0.4f, Random.Range(0.12f, 0.28f), 1.3f,
                     new Color(0.7f, 0.68f, 0.66f, 0.7f));
            }
        }

        // Робота: іскри зварювання (будує) або пил (засипає модуль реголітом)
        if (atSite && data.Working && dt > 0f && tool != null)
        {
            sparkTimer -= dt;
            if (sparkTimer <= 0f)
            {
                bool build = data.Task == RobotTask.Build || data.Task == RobotTask.Tunnel;   // тунель теж зварюють
                sparkTimer = build ? 0.1f : 0.12f;
                Vector3 tip = tool.position;
                for (int i = 0; i < (build ? 5 : 2); i++)
                {
                    Vector3 v = (transform.forward + Random.insideUnitSphere * 0.9f + Vector3.up * 0.6f).normalized;
                    if (build)
                        Emit(tip, v * Random.Range(1.5f, 3.5f), Random.Range(0.03f, 0.07f), Random.Range(0.25f, 0.5f),
                             Color.Lerp(new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.55f, 0.15f, 1f), Random.value));
                    else
                        Emit(tip, v * Random.Range(0.4f, 1f), Random.Range(0.2f, 0.4f), 1.4f, new Color(0.68f, 0.66f, 0.64f, 0.75f));
                }
            }
        }

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

        if (!driving && dt > 0f) CargoArrived(toCrate, dt);
    }

    // ---------- розвантаження корабля поставки (тільки картинка) ----------

    /// <summary>Вільний робот їде по ящик: міняє want на місце біля ящика. true — їде по ящик.</summary>
    bool CargoTarget(bool free, ref Vector3 want)
    {
        if (cargoCrate >= 0 && cargoGen != SupplyLanderView.CargoGeneration) cargoCrate = -1; // нові ящики чи нова гра
        if (!free)
        {
            // дали справжню роботу: ящик, по який їхав, лишається іншим; а той, що віз, уже «на складі»
            if (cargoCrate >= 0) SupplyLanderView.ReleaseCrate(cargoCrate, index);
            cargoCrate = -1;
            if (carrying) SetCargo(false, Color.white);
            return false;
        }
        if (carrying) return false; // везе ящик в ангар (want — HomeSpot)
        if (cargoCrate < 0)
        {
            cargoCrate = SupplyLanderView.ClaimCrate(index);
            cargoGen = SupplyLanderView.CargoGeneration;
            pickTime = 0f;
            if (cargoCrate < 0) return false; // везти нічого
        }
        if (!SupplyLanderView.CrateSpot(cargoCrate, index, want, out Vector3 spot, out cratePos))
        {
            cargoCrate = -1;
            return false;
        }
        want = spot;
        return true;
    }

    /// <summary>Доїхав: біля ящика — підіймає його маніпулятором; в ангарі — ящик на складі.</summary>
    void CargoArrived(bool toCrate, float dt)
    {
        if (carrying)
        {
            SetCargo(false, Color.white);
            return;
        }
        if (!toCrate) return;

        FaceTowards(cratePos, dt);
        pickTime += dt;
        arm.localRotation = Quaternion.Euler(Mathf.PingPong(pickTime * 110f, 45f), 0f, 0f); // опускає й підіймає
        if (pickTime < 0.8f) return;

        if (SupplyLanderView.PickUpCrate(cargoCrate, index, out Color color)) SetCargo(true, color);
        cargoCrate = -1;
        pickTime = 0f;
    }

    void SetCargo(bool on, Color color)
    {
        carrying = on;
        if (cargoBox == null) return;
        cargoBox.gameObject.SetActive(on);
        if (on) ModuleParts.SetColor(cargoBox, color);
    }

    void OnDestroy()
    {
        if (cargoCrate >= 0) SupplyLanderView.ReleaseCrate(cargoCrate, index); // робота прибрали — ящик іншим
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
        float stepLen = Mathf.Min(dist, speed * dt);
        transform.position += d / dist * stepLen;
        moveDir = d / dist;
        FaceTowards(to, dt);

        // Колія гусениць на реголіті — кожні півметра
        trackDist += stepLen;
        if (trackDist >= 0.5f)
        {
            trackDist = 0f;
            Footprints.Track(transform.position, moveDir);
        }
        return false;
    }

    /// <summary>Повертаємо «курс» (без нахилу); нахил за схилом додає Update.</summary>
    void FaceTowards(Vector3 point, float dt)
    {
        Vector3 d = point - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.001f) return;
        var want = Quaternion.LookRotation(d, Vector3.up);
        heading = Quaternion.RotateTowards(heading, want, 360f * dt);
    }

    /// <summary>Одна частинка (пил або іскра) у світі: звідки, швидкість, розмір, скільки живе, колір.</summary>
    void Emit(Vector3 pos, Vector3 velocity, float size, float life, Color color)
    {
        if (!fxTried) { fxTried = true; CreateFx(); }
        if (fx == null) return;
        var ep = new ParticleSystem.EmitParams
        {
            position = pos,
            velocity = velocity,
            startSize = size,
            startLifetime = life,
            startColor = color,
            applyShapeToPosition = false,
        };
        fx.Emit(ep, 1);
    }

    void CreateFx()
    {
        var look = ModuleLook.Instance;
        if (look == null || look.dustMaterial == null) return; // без матеріалу частинки були б рожеві
        var go = new GameObject("Robot Fx");
        go.transform.SetParent(transform, false);
        fx = go.AddComponent<ParticleSystem>();
        fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = fx.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // пил лишається там, де здійнявся
        main.gravityModifier = 0.35f;                               // іскри й пил падають повільно (Місяць)
        main.maxParticles = 300;
        var em = fx.emission;
        em.rateOverTime = 0f;
        var shape = fx.shape;
        shape.enabled = false;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = look.dustMaterial;
    }

    Vector3 HomeSpot()
    {
        var cl = CrewLook.Instance;
        Vector3 home = cl != null ? cl.robotHome : new Vector3(0f, 0f, -27f);
        return home + new Vector3((index - 0.5f) * 1.8f, 0f, 0f); // роботи стоять у ряд
    }
}
