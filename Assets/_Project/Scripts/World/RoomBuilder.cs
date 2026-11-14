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
        interior.Add(R(ModuleParts.Box(root, "Floor", solid, new Vector3(0f, 0.05f, roomMid), new Vector3(w, 0.1f, roomDepth),
                                       collider: false, tint: Color.Lerp(typeColor, Color.white, 0.65f))));
        Color curb = new Color(0.55f, 0.56f, 0.6f);
        const float curbH = 0.25f;
        interior.Add(R(ModuleParts.Box(root, "Curb Back", solid, new Vector3(0f, curbH / 2, L.roomBack - t / 2), new Vector3(w, curbH, t), false, curb)));
        interior.Add(R(ModuleParts.Box(root, "Curb Left", solid, new Vector3(-w / 2 + t / 2, curbH / 2, roomMid), new Vector3(t, curbH, roomDepth), false, curb)));
        interior.Add(R(ModuleParts.Box(root, "Curb Right", solid, new Vector3(w / 2 - t / 2, curbH / 2, roomMid), new Vector3(t, curbH, roomDepth), false, curb)));

        // ----- Невидимі стіни (щоб не пройти крізь оболонку) -----
        const float wallH = 2f;
        Wall(root, "Block Back", new Vector3(0f, wallH / 2, L.roomBack - t / 2), new Vector3(w, wallH, t));
        Wall(root, "Block Left", new Vector3(-w / 2 + t / 2, wallH / 2, roomMid), new Vector3(t, wallH, roomDepth));
        Wall(root, "Block Right", new Vector3(w / 2 - t / 2, wallH / 2, roomMid), new Vector3(t, wallH, roomDepth));
        float part = (w - aw) / 2; // передня стіна кімнати обабіч шлюзу
        Wall(root, "Block Front L", new Vector3(-w / 2 + part / 2, wallH / 2, L.roomFront + t / 2), new Vector3(part, wallH, t));
        Wall(root, "Block Front R", new Vector3(w / 2 - part / 2, wallH / 2, L.roomFront + t / 2), new Vector3(part, wallH, t));

        // ----- Меблі -----
        Furnish(root, solid, type, roomMid, roomDepth, w, interior);

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

        Color doorColor = new Color(0.35f, 0.38f, 0.44f);
        var outerDoor = DoubleDoor(root, "Door Outer", solid, new Vector3(0f, ah / 2, L.airlockFront + t / 2), door, ah - 0.1f, t * 0.6f, doorColor);
        var innerDoor = DoubleDoor(root, "Door Inner", solid, new Vector3(0f, ah / 2, L.roomFront + t / 2), door, ah - 0.1f, t * 0.6f, doorColor);

        root.gameObject.AddComponent<ModuleInterior>().Setup(view, shell.ToArray(), shellColors.ToArray(), interior.ToArray());
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

    // ---------- Меблі (поки прості фігури; потім заміняться спрайтами від 02) ----------

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
