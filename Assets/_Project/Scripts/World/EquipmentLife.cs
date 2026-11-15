// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// «Живе» обладнання надворі:
///  - сонячна панель: на сонці по ній біжить відблиск; у тіні від хребта відблиску немає (панель «не ловить» Сонце);
///  - батарея: на кожній із трьох шаф — лампочка заряду. Скільки світиться зелених — стільки заряду (третина на лампу).
///    Коли батарея віддає енергію (тінь або панелей замало), верхня лампочка блимає помаранчевим; порожня — блимає червона.
/// Додає ModuleView сам (для Solar і Battery). Читає GameState щокадру, нічого в нього не пише.
/// </summary>
public class EquipmentLife : MonoBehaviour
{
    static readonly Color Green = new Color(0.45f, 1f, 0.4f, 1f);
    static readonly Color Orange = new Color(1f, 0.6f, 0.15f, 1f);
    static readonly Color Red = new Color(1f, 0.2f, 0.18f, 1f);
    static readonly Color Off = new Color(0.16f, 0.17f, 0.2f, 1f);

    ModuleType type;
    SpriteRenderer[] glints, leds, ledGlows;

    /// <summary>Викликає ModuleView після побудови обладнання. size — розмір квадратика модуля, м.</summary>
    public void Init(ModuleType type, Vector2 size)
    {
        this.type = type;
        if (type == ModuleType.Solar)
        {
            glints = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * size.x * 0.22f; // як панелі в ModuleView.BuildEquipment
                glints[i] = Sprite3D("Glint " + i, WorldSprites.Blob, new Vector3(x, 1.45f, -0.25f), new Vector2(1.4f, 0.7f), 3);
            }
        }
        else if (type == ModuleType.Battery)
        {
            leds = new SpriteRenderer[3];
            ledGlows = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 1.3f; // як шафи в ModuleView.BuildEquipment
                ledGlows[i] = Sprite3D("LED Glow " + i, WorldSprites.Blob, new Vector3(x, 1.2f, -0.78f), new Vector2(0.7f, 0.7f), 2);
                leds[i] = Sprite3D("LED " + i, WorldSprites.Disc, new Vector3(x, 1.2f, -0.8f), new Vector2(0.22f, 0.22f), 3);
            }
        }
    }

    SpriteRenderer Sprite3D(string name, Sprite sprite, Vector3 pos, Vector2 size, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f); // до Billboard: він запам'ятовує масштаб в Awake
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;
        go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;
        return r;
    }

    void Update()
    {
        var game = GameBootstrap.Instance;
        if (game == null || game.State == null) return;

        if (glints != null)
        {
            float sun = 1f - ShadeFx.Amount; // у тіні відблиску немає
            for (int i = 0; i < glints.Length; i++)
            {
                float a = (0.22f + 0.2f * Mathf.Sin(Time.time * 1.7f + i * 2.1f)) * sun;
                glints[i].color = new Color(1f, 1f, 0.95f, a);
                glints[i].enabled = a > 0.01f;
            }
        }

        if (leds != null)
        {
            var s = game.State;
            var power = game.GetSystem<PowerSystem>();
            float cap = power != null ? power.BatteryCapacity(s) : 0f;
            float level = cap > 0f ? Mathf.Clamp01(s.Res.Battery / cap) : 0f;
            bool discharging = power != null && s.Res.Battery > 0.01f && power.LastSupplyBlocks + 0.01f < s.Power.Total();
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            int lit = Mathf.Clamp(Mathf.CeilToInt(level * 3f - 0.01f), 0, 3);

            for (int i = 0; i < leds.Length; i++)
            {
                Color c = i < lit ? Green : Off;
                if (discharging && i == lit - 1) c = blink ? Orange : Off; // віддає енергію — верхня лампа блимає
                if (lit == 0 && i == 0) c = blink ? Red : Off;             // порожня
                leds[i].color = c;
                bool glow = c != Off;
                ledGlows[i].enabled = glow;
                if (glow) ledGlows[i].color = new Color(c.r, c.g, c.b, 0.45f);
            }
        }
    }
}
