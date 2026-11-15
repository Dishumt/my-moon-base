// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Сліди на реголіті: відбитки черевиків інженера й астронавтів (кожен місячний крок надворі)
/// і колії гусениць роботів. Факт для дітей: на Місяці немає вітру й дощу, тож сліди астронавтів
/// «Аполлона» лежать там уже понад 50 років — і пролежать мільйони.
/// У грі тримаємо до 500 відбитків (щоб браузер не гальмував): найстаріші плавно тануть.
/// Ставить себе сам (ModuleLook додає на World). Сліди кладуть MoonHop (кроки) і RobotView (колії).
/// </summary>
public class Footprints : MonoBehaviour
{
    const int Max = 500;
    const int FadeCount = 120; // стільки найстаріших поступово світлішають

    static Footprints instance;
    static readonly Color PrintColor = new Color(0.14f, 0.14f, 0.18f, 0.45f);

    SpriteRenderer[] pool;
    int next, count;
    static Sprite boot, track;

    void Awake() => instance = this;

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>Відбиток черевика. pos — де людина, dir — куди йшла, leg 0/1 — ліва чи права нога (трохи вбік).</summary>
    public static void Step(Vector3 pos, Vector3 dir, int leg)
    {
        if (instance == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
        dir.Normalize();
        Vector3 side = new Vector3(dir.z, 0f, -dir.x) * (leg == 0 ? -0.13f : 0.13f);
        instance.Add(pos + side, dir, Boot, 0.2f, 0.34f);
    }

    /// <summary>Колія робота: два відбитки гусениць обабіч.</summary>
    public static void Track(Vector3 pos, Vector3 dir)
    {
        if (instance == null) return;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        dir.Normalize();
        Vector3 side = new Vector3(dir.z, 0f, -dir.x) * 0.55f;
        instance.Add(pos + side, dir, Tread, 0.26f, 0.5f);
        instance.Add(pos - side, dir, Tread, 0.26f, 0.5f);
    }

    void Add(Vector3 pos, Vector3 dir, Sprite sprite, float width, float length)
    {
        if (pool == null) pool = new SpriteRenderer[Max];
        var r = pool[next];
        if (r == null)
        {
            var go = new GameObject("Footprint");
            go.transform.SetParent(transform, false);
            r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = -4; // на землі, під тінями
            pool[next] = r;
        }

        // Лежить на землі (і на схилі), носком у бік руху
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        r.transform.SetPositionAndRotation(
            new Vector3(pos.x, MoonTerrain.HeightAt(pos) + 0.025f, pos.z),
            Quaternion.FromToRotation(Vector3.up, MoonTerrain.NormalAt(pos.x, pos.z)) * Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f));
        r.sprite = sprite;
        var size = sprite.bounds.size;
        r.transform.localScale = new Vector3(width / size.x, length / size.y, 1f);
        r.color = PrintColor;
        r.enabled = true;

        next = (next + 1) % Max;
        if (count < Max) count++;
        if (count == Max) FadeOldest();
    }

    /// <summary>Найстаріші відбитки (наступні на заміну) поступово світлішають — нові «витісняють» старі непомітно.</summary>
    void FadeOldest()
    {
        for (int i = 0; i < FadeCount; i++)
        {
            var r = pool[(next + i) % Max];
            if (r == null) continue;
            Color c = PrintColor;
            c.a *= (i + 1f) / FadeCount;
            r.color = c;
        }
    }

    // ---------- Картинки відбитків (створюємо кодом) ----------

    // Черевик: носок угорі (у бік руху), підбор унизу. Рядки згори вниз.
    static readonly string[] BootRows = { ".###.", "#####", "#####", "#####", ".###.", ".....", ".###.", "#####", ".###." };
    // Гусениця: поперечні смуги протектора
    static readonly string[] TreadRows = { "####", "....", "####", "....", "####", "....", "####", "...." };

    static Sprite Boot => boot != null ? boot : (boot = FromRows(BootRows));
    static Sprite Tread => track != null ? track : (track = FromRows(TreadRows));

    static Sprite FromRows(string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        return WorldSprites.Make(w, h, h, (x, y) => rows[h - 1 - y][x] == '#' ? Color.white : Color.clear);
    }
}
