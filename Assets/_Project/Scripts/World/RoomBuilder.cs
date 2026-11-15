// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Будує кімнату-модуль з простих фігур. Справжні місячні бази — не коробки, тому:
///  - житловий і медмодуль — «бочка», що лежить на боці, з ребрами;
///  - теплиця — прозорий тунель-бочка, крізь який видно грядки;
///  - лабораторія — блок з маленьким скляним куполом;
///  - укриття — бункер, засипаний горбом реголіту.
/// Будова кімнати:
///  - ОБОЛОНКА (shell) — те, що видно надворі. Коли інженер заходить, вона зникає (як дах у Project Zomboid);
///  - ВНУТРІШНЄ (interior) — підлога, низький бортик і меблі. Видно, коли оболонка зникла;
///  - невидимі стіни-колайдери, щоб не ходити крізь оболонку;
///  - ШЛЮЗ — тамбур назовні перед кімнатою, з двома дверима.
/// Викликає ModuleView.
/// </summary>
public static class RoomBuilder
{
    /// <summary>Розміри кімнати й шлюзу (потрібні ModuleView.Contains). z — вздовж глибини модуля.</summary>
    public struct Layout
    {
        public float roomFront, roomBack, airlockFront, airlockHalfWidth;
    }

    public static Layout Build(Transform root, ModuleView view, ModuleLook look, ModuleType type, Vector2 size)
    {
        var solid = look.wallMaterial;
        float w = size.x, t = look.wallThickness;
        Color typeColor = look.ColorOf(type);

        // Кімната (глибина size.y - 1) зсунута назад, шлюз стирчить уперед; разом займають квадратик і лишають прохід.
        var L = new Layout();
        float roomDepth = size.y - 1f;
        L.roomBack = size.y / 2 + 0.5f;
        L.roomFront = L.roomBack - roomDepth;
        L.airlockFront = L.roomFront - look.airlockDepth;
        float roomMid = (L.roomFront + L.roomBack) / 2;

        float door = Mathf.Min(look.doorWidth, w - 4 * t);
        float aw = door + 1f;
        L.airlockHalfWidth = aw / 2;

        var shell = new List<Renderer>();
        var shellColors = new List<Color>();
        var interior = new List<Renderer>();

        // ----- Внутрішнє: підлога і низький бортик по краю -----
        interior.Add(R(ModuleParts.Box(root, "Floor", FloorMaterial(solid), new Vector3(0f, 0.05f, roomMid), new Vector3(w, 0.1f, roomDepth),
                                       collider: false, tint: Color.Lerp(typeColor, Color.white, 0.65f))));
        Color curb = new Color(0.55f, 0.56f, 0.6f);
        const float curbH = 0.25f;
        interior.Add(R(ModuleParts.Box(root, "Curb Back", solid, new Vector3(0f, curbH / 2, L.roomBack - t / 2), new Vector3(w, curbH, t), false, curb)));
        interior.Add(R(ModuleParts.Box(root, "Curb Left", solid, new Vector3(-w / 2 + t / 2, curbH / 2, roomMid), new Vector3(t, curbH, roomDepth), false, curb)));
        interior.Add(R(ModuleParts.Box(root, "Curb Right", solid, new Vector3(w / 2 - t / 2, curbH / 2, roomMid), new Vector3(t, curbH, roomDepth), false, curb)));

        // ----- Внутрішні стіни з текстурою Юлі (видно, коли ти в кімнаті; ближню до камери ховає InnerWalls) -----
        if (type != ModuleType.Greenhouse) // теплиця — скляний тунель, там стін немає
            BuildInnerWalls(root, view, solid, typeColor, w, t, L, roomMid, roomDepth, aw, interior);

        // ----- Невидимі стіни (щоб не пройти крізь оболонку) -----
        // Вищі за саму кімнату: інакше місячним стрибком (2 м) можна заскочити всередину повз шлюз
        const float wallH = 4.5f;
        Wall(root, "Block Back", new Vector3(0f, wallH / 2, L.roomBack - t / 2), new Vector3(w, wallH, t));
        Wall(root, "Block Left", new Vector3(-w / 2 + t / 2, wallH / 2, roomMid), new Vector3(t, wallH, roomDepth));
        Wall(root, "Block Right", new Vector3(w / 2 - t / 2, wallH / 2, roomMid), new Vector3(t, wallH, roomDepth));
        float part = (w - aw) / 2; // передня стіна кімнати обабіч шлюзу
        Wall(root, "Block Front L", new Vector3(-w / 2 + part / 2, wallH / 2, L.roomFront + t / 2), new Vector3(part, wallH, t));
        Wall(root, "Block Front R", new Vector3(w / 2 - part / 2, wallH / 2, L.roomFront + t / 2), new Vector3(part, wallH, t));

        // ----- Меблі -----
        var furniture = new List<Renderer>();
        if (!FurnishWithSprites(root, view, look, type, roomMid, roomDepth, w, furniture))
            Furnish(root, solid, type, roomMid, roomDepth, w, interior); // картинок ще немає — прості коробки

        // ----- Оболонка (видно надворі) -----
        BuildShell(root, look, type, typeColor, roomMid, roomDepth, w, shell, shellColors);

        // ----- Шлюз: тамбур, що трохи заходить в оболонку, щоб внутрішні двері були під його дахом -----
        float ah = look.wallHeight * 0.85f;
        float airBack = L.roomFront + 0.45f;
        float airLen = airBack - L.airlockFront, airMid = (L.airlockFront + airBack) / 2;
        Color airColor = new Color(0.66f, 0.68f, 0.72f);

        interior.Add(R(ModuleParts.Box(root, "Airlock Floor", solid, new Vector3(0f, 0.06f, airMid), new Vector3(aw, 0.1f, airLen),
                                       collider: false, tint: new Color(0.5f, 0.5f, 0.53f))));
        ModuleParts.Box(root, "Airlock Side L", solid, new Vector3(-aw / 2 + t / 2, ah / 2, airMid), new Vector3(t, ah, airLen), tint: airColor);
        ModuleParts.Box(root, "Airlock Side R", solid, new Vector3(aw / 2 - t / 2, ah / 2, airMid), new Vector3(t, ah, airLen), tint: airColor);
        float jamb = (aw - door) / 2;
        ModuleParts.Box(root, "Airlock Front L", solid, new Vector3(-aw / 2 + jamb / 2, ah / 2, L.airlockFront + t / 2), new Vector3(jamb, ah, t), tint: airColor);
        ModuleParts.Box(root, "Airlock Front R", solid, new Vector3(aw / 2 - jamb / 2, ah / 2, L.airlockFront + t / 2), new Vector3(jamb, ah, t), tint: airColor);
        ModuleParts.Box(root, "Airlock Inner L", solid, new Vector3(-aw / 2 + jamb / 2, ah / 2, L.roomFront + t / 2), new Vector3(jamb, ah, t), tint: airColor);
        ModuleParts.Box(root, "Airlock Inner R", solid, new Vector3(aw / 2 - jamb / 2, ah / 2, L.roomFront + t / 2), new Vector3(jamb, ah, t), tint: airColor);
        AddShell(shell, shellColors, ModuleParts.Box(root, "Airlock Roof", solid, new Vector3(0f, ah + 0.075f, airMid),
                                                     new Vector3(aw + 0.15f, 0.15f, airLen), collider: false, tint: airColor), airColor);

        // Невидимі високі стінки навколо тамбура — щоб не перестрибнути збоку
        Wall(root, "Block Airlock L", new Vector3(-aw / 2 + t / 2, wallH / 2, airMid), new Vector3(t, wallH, airLen));
        Wall(root, "Block Airlock R", new Vector3(aw / 2 - t / 2, wallH / 2, airMid), new Vector3(t, wallH, airLen));
        Wall(root, "Block Airlock Front L", new Vector3(-aw / 2 + jamb / 2, wallH / 2, L.airlockFront + t / 2), new Vector3(jamb, wallH, t));
        Wall(root, "Block Airlock Front R", new Vector3(aw / 2 - jamb / 2, wallH / 2, L.airlockFront + t / 2), new Vector3(jamb, wallH, t));
        // ...і над обома дверима (сам прохід на рівні підлоги лишається відкритим)
        Wall(root, "Block Above Outer Door", new Vector3(0f, (ah + wallH) / 2, L.airlockFront + t / 2), new Vector3(aw, wallH - ah, t));
        Wall(root, "Block Above Inner Door", new Vector3(0f, (ah + wallH) / 2, L.roomFront + t / 2), new Vector3(aw, wallH - ah, t));

        Color doorColor = new Color(0.35f, 0.38f, 0.44f);
        var outerDoor = DoubleDoor(root, "Door Outer", solid, new Vector3(0f, ah / 2, L.airlockFront + t / 2), door, ah - 0.1f, t * 0.6f, doorColor);
        var innerDoor = DoubleDoor(root, "Door Inner", solid, new Vector3(0f, ah / 2, L.roomFront + t / 2), door, ah - 0.1f, t * 0.6f, doorColor);

        var interiorView = root.gameObject.AddComponent<ModuleInterior>();
        interiorView.Setup(view, shell.ToArray(), shellColors.ToArray(), interior.ToArray(), furniture.ToArray());
        BuildAlarm(root, interiorView, roomMid, roomDepth, w, L.roomBack);
        // front = зовнішній край шлюзу, inner = середина внутрішніх дверей
        root.gameObject.AddComponent<Airlock>().Setup(outerDoor.transform, innerDoor.transform, door, aw,
                                                      L.airlockFront, L.roomFront + t / 2, ah);
        return L;
    }

    // ---------- Оболонки різних типів ----------

    static void BuildShell(Transform root, ModuleLook look, ModuleType type, Color c, float mid, float depth, float w,
                           List<Renderer> shell, List<Color> colors)
    {
        var solid = look.wallMaterial;
        switch (type)
        {
            case ModuleType.Greenhouse:
                // Прозорий тунель: скло зеленкувате, ребра білі
                var glass = new Color(0.55f, 0.9f, 0.6f, 0.4f);
                Barrel(root, solid, mid, depth, w, glass, Color.white, shell, colors);
                break;

            case ModuleType.Medbay:
                Barrel(root, solid, mid, depth, w, new Color(0.95f, 0.95f, 0.96f), c, shell, colors); // біла бочка, червоні ребра
                break;

            case ModuleType.Lab:
            {
                float h = 2.3f;
                AddShell(shell, colors, ModuleParts.Box(root, "Shell", solid, new Vector3(0f, h / 2, mid), new Vector3(w, h, depth), false, c), c);
                var dome = new Color(0.7f, 0.85f, 1f, 0.5f);
                AddShell(shell, colors, ModuleParts.Sphere(root, "Dome", solid, new Vector3(0f, h, mid), new Vector3(2.4f, 2.4f, 2.4f), false, dome), dome);
                break;
            }

            case ModuleType.Shelter:
            {
                // Бункер: низький блок, засипаний горбом реголіту (захист від радіації)
                float h = 1.5f;
                AddShell(shell, colors, ModuleParts.Box(root, "Shell", solid, new Vector3(0f, h / 2, mid), new Vector3(w, h, depth), false, c), c);
                var regolith = new Color(0.55f, 0.52f, 0.5f);
                AddShell(shell, colors, ModuleParts.Sphere(root, "Regolith Mound", solid, new Vector3(0f, h, mid), new Vector3(w, 2.2f, depth), false, regolith), regolith);
                break;
            }

            default: // Habitat: біла бочка з кольоровими ребрами
                Barrel(root, solid, mid, depth, w, new Color(0.93f, 0.94f, 0.96f), c, shell, colors);
                break;
        }
    }

    /// <summary>Бочка, що лежить на боці вздовж X, з трьома ребрами.</summary>
    static void Barrel(Transform root, Material mat, float mid, float depth, float w, Color body, Color ribs,
                       List<Renderer> shell, List<Color> colors)
    {
        float d = depth, cy = 0.6f; // діаметр = глибина кімнати; центр трохи над землею (низ «утоплений»)
        var rot = Quaternion.Euler(0f, 0f, 90f);

        var b = ModuleParts.Cylinder(root, "Shell", mat, new Vector3(0f, cy, mid), new Vector3(d, w / 2, d), false, body);
        b.transform.localRotation = rot;
        AddShell(shell, colors, b, body);

        foreach (float x in new[] { -w / 2 + 0.5f, 0f, w / 2 - 0.5f })
        {
            var rib = ModuleParts.Cylinder(root, "Rib", mat, new Vector3(x, cy, mid), new Vector3(d + 0.14f, 0.07f, d + 0.14f), false, ribs);
            rib.transform.localRotation = rot;
            AddShell(shell, colors, rib, ribs);
        }
    }

    // ---------- Меблі-спрайти від 02 (Art/Props/Furniture) ----------
    //
    // Картинки намальовані «спереду й трохи згори», тому стоять як білборди FaceCamera — паралельно екрану,
    // і виглядають рівно так, як намалювала Юля. Стоять уздовж задньої стіни й по боках,
    // щоб не заступати прохід від шлюзу. x — від центру кімнати вліво/вправо, row: 0 = біля задньої стіни, 1 = збоку.

    struct Item
    {
        public string name; public float x; public int row; public float scale; public float dz;
        // dz — зсув уперед (до камери, «нижче» на екрані), метри: велике ліжко інакше візуально заходить за стіну
        public Item(string name, float x, int row, float scale = 1f, float dz = 0f)
        { this.name = name; this.x = x; this.row = row; this.scale = scale; this.dz = dz; }
    }

    // Ліжко-капсула більше за інші меблі: у ньому лежить людина звичайного розміру (AstronautView).
    const float BedScale = 1.6f;

    const float InnerWallH = 2f;      // висота внутрішніх стін, метри
    const float InnerWallT = 0.08f;   // товщина

    static void BuildInnerWalls(Transform root, ModuleView view, Material solid, Color typeColor, float w, float t, Layout L,
                                float roomMid, float roomDepth, float aw, List<Renderer> interior)
    {
        var mat = ModuleParts.Textured(solid, "Art/Base/moon_base_wall", Vector2.one);
        if (mat == solid) return; // текстури немає — лишаємо кімнату як була
        // Стіни — як намальований фон: не пишуть глибину і малюються після землі, але ДО спрайтів.
        // Інакше верхівки меблів і людей (картинки нахилені до камери) ховалися б за задньою стіною.
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.renderQueue = 2450; // після звичайних 3D-об'єктів (2000), перед прозорими спрайтами (3000)
        var walls = root.gameObject.AddComponent<InnerWalls>();
        walls.Init(view);
        Color tint = Color.Lerp(typeColor, Color.white, 0.8f);
        float inX = w / 2 - t - InnerWallT / 2 - 0.02f;           // трохи всередині бортика
        float backZ = L.roomBack - t - InnerWallT / 2 - 0.02f;
        float frontZ = L.roomFront + t + InnerWallT / 2 + 0.02f;
        float sideLen = backZ - frontZ;
        float sideMid = (backZ + frontZ) / 2;
        float y = InnerWallH / 2;

        void Add(string name, Vector3 pos, Vector3 scale, float length, Vector3 inward)
        {
            var r = R(ModuleParts.Box(root, name, mat, pos, scale, collider: false, tint: tint));
            ModuleParts.SetTiling(r, new Vector2(Mathf.Max(1f, Mathf.Round(length / InnerWallH)), 1f)); // квадратні панелі
            r.enabled = false; // покаже InnerWalls, коли інженер зайде
            interior.Add(r);   // темнішає разом зі світлом у кімнаті
            walls.Add(r, inward);
        }

        Add("Inner Wall Back", new Vector3(0f, y, backZ), new Vector3(2 * inX, InnerWallH, InnerWallT), 2 * inX, Vector3.back);
        Add("Inner Wall Left", new Vector3(-inX, y, sideMid), new Vector3(InnerWallT, InnerWallH, sideLen), sideLen, Vector3.right);
        Add("Inner Wall Right", new Vector3(inX, y, sideMid), new Vector3(InnerWallT, InnerWallH, sideLen), sideLen, Vector3.left);
        // передня стіна — обабіч дверей шлюзу
        float seg = inX - aw / 2;
        if (seg > 0.2f)
        {
            Add("Inner Wall Front L", new Vector3(-(aw / 2 + seg / 2), y, frontZ), new Vector3(seg, InnerWallH, InnerWallT), seg, Vector3.forward);
            Add("Inner Wall Front R", new Vector3(aw / 2 + seg / 2, y, frontZ), new Vector3(seg, InnerWallH, InnerWallT), seg, Vector3.forward);
        }
    }

    /// <summary>
    /// Підлога кімнати з плитками Юлі (Resources/Art/Base/moon_base_floor), плитки ≈ 2 м.
    /// Колір кімнати (тінт) накладається поверх. Немає текстури — звичайна підлога.
    /// </summary>
    static Material FloorMaterial(Material solid) => ModuleParts.Textured(solid, "Art/Base/moon_base_floor", new Vector2(3f, 2.5f));

    static Item[] LayoutFor(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.Habitat: // генератор кисню тут: житловий модуль перший, і дитина одразу бачить, звідки кисень
                return new[] { new Item("bunk_bed", -1.7f, 0, BedScale, -0.5f), new Item("galley_kitchen", 0.45f, 0), new Item("oxygen_generator", 2.1f, 0),
                               new Item("shower_toilet", -2.1f, 1), new Item("dining_table", 2.1f, 1) };
            case ModuleType.Greenhouse:
                return new[] { new Item("hydroponic_rack", -2.0f, 0), new Item("hydroponic_rack", 0f, 0), new Item("hydroponic_rack", 2.0f, 0),
                               new Item("hydroponic_rack", -2.1f, 1), new Item("water_recycler", 2.1f, 1) };
            case ModuleType.Medbay:
                return new[] { new Item("medical_cabinet", -2.1f, 0), new Item("bunk_bed", 0f, 0, BedScale, -0.5f), new Item("wash_station", 2.1f, 0),
                               new Item("wardrobe_storage", -2.1f, 1) };
            case ModuleType.Lab:
                return new[] { new Item("science_bench", -2.0f, 0), new Item("specimen_cabinet", 0f, 0), new Item("workstation_desk", 2.0f, 0),
                               new Item("repair_workbench", -2.1f, 1), new Item("power_control_panel", 2.1f, 1) };
            case ModuleType.Shelter:
                return new[] { new Item("cargo_crates", -2.0f, 0), new Item("oxygen_generator", 0f, 0), new Item("power_control_panel", 2.0f, 0),
                               new Item("cargo_crates", -2.1f, 1), new Item("water_recycler", 2.1f, 1) };
        }
        return new Item[0];
    }

    /// <summary>Розставляє меблі-спрайти і кладе їх у list. false — якщо картинок немає (тоді будуть коробки).</summary>
    static bool FurnishWithSprites(Transform root, ModuleView view, ModuleLook look, ModuleType type, float mid, float depth, float w, List<Renderer> list)
    {
        if (look == null || !look.HasFurniture) return false;
        // Впритул до задньої стіни: поки гравець надворі, меблі сховані (ModuleInterior),
        // тож верх картинки, що нахилений назад разом з камерою, ніхто не побачить крізь оболонку.
        float backZ = mid + depth / 2 - 0.35f;
        float sideZ = mid - 0.6f;
        bool any = false;
        foreach (var it in LayoutFor(type))
        {
            var info = look.FurnitureInfo(it.name);
            if (info == null || info.sprite == null) continue;
            var go = new GameObject("Furniture " + it.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(Mathf.Clamp(it.x, -w / 2 + 0.9f, w / 2 - 0.9f), 0.1f, (it.row == 0 ? backZ : sideZ) + it.dz);
            go.transform.localScale = Vector3.one * it.scale; // до Billboard: він запам'ятовує масштаб в Awake
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = info.sprite;
            // Новий унітаз Юлі (кабінка; її ж кадри грають, коли він ламається — IncidentView)
            if (it.name == "shower_toilet")
            {
                var booth = Resources.Load<Sprite>("Art/Incidents/toilet_frame_01");
                if (booth != null) sr.sprite = booth;
            }
            go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;
            // Предмет разом з лампами й товщиною сортується як одне ціле: лампи поверх предмета, але не поверх людей перед ним
            go.AddComponent<UnityEngine.Rendering.SortingGroup>();
            list.Add(sr);
            // Ширина самого предмета (без прозорих країв) і зсув його середини — з урахуванням масштабу
            float width = (info.width > 0f ? info.width : sr.sprite.bounds.size.x * 0.75f) * it.scale;
            float centerX = info.centerX * it.scale;
            AddDepth(root, go.transform, sr, width, centerX, it.scale, list);
            // лампи, екрани, рослини (показують стан гри); ще й записує предмет у ModuleView, щоб астронавти знали, куди йти
            go.AddComponent<FurnitureLife>().Init(it.name, view, type, sr, width, centerX);
            any = true;
        }
        return any;
    }

    static readonly Color ThicknessColor = new Color(0.12f, 0.12f, 0.16f, 1f);

    /// <summary>
    /// Щоб меблі не виглядали наліпками:
    ///  - ТОВЩИНА — та сама картинка, темна, трохи правіше, нижче й далі від камери. Видно тонкий темний край,
    ///    як у фігурки з товстого картону (світло падає зліва згори). Низ краю ховається під підлогою — так і має бути;
    ///  - ТІНЬ — м'яка пляма на підлозі під предметом (та сама, що під персонажами, MoonHop.BlobSprite).
    /// Обидві теж у списку list, тож ховаються разом з меблями, поки гравець надворі.
    /// </summary>
    static void AddDepth(Transform root, Transform item, SpriteRenderer sr, float width, float centerX, float scale, List<Renderer> list)
    {
        // Копія — дитина білборда, тож повертається разом з ним: x — вправо по екрану, y — вгору, z — від камери.
        var back = new GameObject("Thickness");
        back.transform.SetParent(item, false);
        back.transform.localPosition = new Vector3(0.035f, -0.02f, 0.05f);
        var bsr = back.AddComponent<SpriteRenderer>();
        bsr.sprite = sr.sprite;
        bsr.color = ThicknessColor;
        bsr.sortingOrder = sr.sortingOrder - 1; // завжди позаду самого предмета
        list.Add(bsr);

        // Тінь лежить на підлозі (підлога на висоті 0,1 м). Ширина — як сам предмет, без прозорих країв картинки.
        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(root, false);
        shadow.transform.localPosition = item.localPosition + new Vector3(centerX, 0.015f, 0.05f);
        shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shadow.transform.localScale = new Vector3(width * 0.95f, 0.75f * scale, 1f); // пляма 1 м → овал під предметом
        var ssr = shadow.AddComponent<SpriteRenderer>();
        ssr.sprite = MoonHop.BlobSprite();
        ssr.color = new Color(0f, 0f, 0f, 0.4f);
        ssr.sortingOrder = sr.sortingOrder - 2; // під товщиною і під тінями людей
        list.Add(ssr);
    }

    // ---------- Аварійна лампа (коли енергії забракло) ----------

    /// <summary>
    /// Червона лампа під «стелею» над задньою стіною (саму стелю всередині не видно), сяйво навколо неї
    /// і червоні відблиски на підлозі. Спочатку вимкнені — вмикає ModuleInterior під час аварії.
    /// </summary>
    static void BuildAlarm(Transform root, ModuleInterior interiorView, float mid, float depth, float w, float back)
    {
        var lamp = AlarmSprite(root, "Alarm Lamp", WorldSprites.Disc, new Vector3(0f, 2.7f, back - 0.4f), 0.3f, 6);
        var glow = AlarmSprite(root, "Alarm Glow", WorldSprites.Blob, new Vector3(0f, 2.7f, back - 0.42f), 1.8f, 5);

        var floorGo = new GameObject("Alarm Floor Light");
        floorGo.transform.SetParent(root, false);
        floorGo.transform.localPosition = new Vector3(0f, 0.13f, mid);
        floorGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // лежить на підлозі
        floorGo.transform.localScale = new Vector3(w * 0.95f, depth * 0.95f, 1f);
        var floor = floorGo.AddComponent<SpriteRenderer>();
        floor.sprite = WorldSprites.Blob;
        floor.sortingOrder = -3; // під тінями меблів і людей
        floor.enabled = false;

        interiorView.SetupAlarm(lamp, glow, floor);
    }

    static SpriteRenderer AlarmSprite(Transform root, string name, Sprite sprite, Vector3 pos, float size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size, size, 1f); // до Billboard: він запам'ятовує масштаб в Awake
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        r.enabled = false;
        go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;
        return r;
    }

    // ---------- Меблі-коробки (запасний варіант, якщо картинок немає) ----------

    static void Furnish(Transform root, Material m, ModuleType type, float mid, float depth, float w, List<Renderer> list)
    {
        float back = mid + depth / 2 - 0.6f; // біля задньої стіни
        switch (type)
        {
            case ModuleType.Habitat: // два ліжка і стіл
                F(list, root, m, "Bed L", new Vector3(-w / 2 + 0.9f, 0.25f, mid + 0.4f), new Vector3(1f, 0.4f, 2f), new Color(0.35f, 0.5f, 0.85f));
                F(list, root, m, "Bed R", new Vector3(w / 2 - 0.9f, 0.25f, mid + 0.4f), new Vector3(1f, 0.4f, 2f), new Color(0.85f, 0.55f, 0.3f));
                F(list, root, m, "Table", new Vector3(0f, 0.4f, back - 0.3f), new Vector3(1.4f, 0.7f, 0.8f), new Color(0.75f, 0.7f, 0.62f));
                break;

            case ModuleType.Greenhouse: // грядки з проходом посередині
                for (int row = -1; row <= 1; row++)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        var pos = new Vector3(side * (w / 4 + 0.3f), 0.2f, mid + row * 1.2f + 0.3f);
                        F(list, root, m, "Bed", pos, new Vector3(w / 2 - 1.4f, 0.35f, 0.6f), new Color(0.45f, 0.32f, 0.22f));
                        F(list, root, m, "Plants", pos + Vector3.up * 0.3f, new Vector3(w / 2 - 1.5f, 0.3f, 0.45f), new Color(0.3f, 0.75f, 0.3f));
                    }
                break;

            case ModuleType.Medbay: // медичне ліжко і шафа з ліками
                F(list, root, m, "Med Bed", new Vector3(-w / 2 + 1f, 0.35f, mid + 0.3f), new Vector3(1f, 0.55f, 2.1f), Color.white);
                F(list, root, m, "Cabinet", new Vector3(w / 2 - 0.7f, 0.6f, back), new Vector3(0.8f, 1.2f, 0.5f), new Color(0.9f, 0.3f, 0.3f));
                break;

            case ModuleType.Lab: // довгий стіл зі зразками і стелаж
                F(list, root, m, "Bench", new Vector3(0f, 0.4f, back - 0.2f), new Vector3(3f, 0.75f, 0.8f), new Color(0.8f, 0.8f, 0.85f));
                F(list, root, m, "Sample A", new Vector3(-0.6f, 0.9f, back - 0.2f), new Vector3(0.3f, 0.25f, 0.3f), new Color(0.55f, 0.5f, 0.6f));
                F(list, root, m, "Sample B", new Vector3(0.5f, 0.9f, back - 0.2f), new Vector3(0.25f, 0.2f, 0.25f), new Color(0.45f, 0.42f, 0.4f));
                F(list, root, m, "Rack", new Vector3(-w / 2 + 0.6f, 0.75f, mid), new Vector3(0.6f, 1.5f, 1.4f), new Color(0.5f, 0.6f, 0.8f));
                break;

            case ModuleType.Shelter: // лави вздовж стін
                F(list, root, m, "Bench L", new Vector3(-w / 2 + 0.6f, 0.22f, mid), new Vector3(0.6f, 0.4f, depth - 1.2f), new Color(0.7f, 0.6f, 0.4f));
                F(list, root, m, "Bench R", new Vector3(w / 2 - 0.6f, 0.22f, mid), new Vector3(0.6f, 0.4f, depth - 1.2f), new Color(0.7f, 0.6f, 0.4f));
                break;
        }
    }

    // ---------- Дрібниці ----------

    static void F(List<Renderer> list, Transform root, Material m, string name, Vector3 pos, Vector3 scale, Color c)
    {
        list.Add(R(ModuleParts.Box(root, name, m, pos, scale, collider: false, tint: c)));
    }

    static Renderer R(GameObject go) => go.GetComponent<Renderer>();

    static void AddShell(List<Renderer> shell, List<Color> colors, GameObject go, Color c)
    {
        shell.Add(R(go));
        colors.Add(c);
    }

    /// <summary>Невидима стіна: тільки колайдер.</summary>
    static void Wall(Transform root, string name, Vector3 pos, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.AddComponent<BoxCollider>().size = size;
    }

    /// <summary>Двері з двох половинок (як у ліфті). Airlock стискає їх до країв отвору.</summary>
    static GameObject DoubleDoor(Transform root, string name, Material mat, Vector3 center, float width, float height, float thick, Color color)
    {
        var door = new GameObject(name);
        door.transform.SetParent(root, false);
        door.transform.localPosition = center;
        float half = width / 2;
        ModuleParts.Box(door.transform, "L", mat, new Vector3(-half / 2, 0f, 0f), new Vector3(half, height, thick), tint: color);
        ModuleParts.Box(door.transform, "R", mat, new Vector3(half / 2, 0f, 0f), new Vector3(half, height, thick), tint: color);
        return door;
    }
}
