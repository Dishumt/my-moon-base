// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// «Як у Project Zomboid»: коли інженер заходить у модуль, оболонка (бочка, купол, дах шлюзу) плавно зникає,
/// а все всередині (підлога, меблі) світлішає теплим світлом.
/// Поки гравець надворі, меблі й люди всередині сховані — як у Project Zomboid чи The Sims. Вулицю затемнює OutdoorDarkening (на Sun).
/// Цей компонент додає RoomBuilder сам — руками ставити не треба.
/// Замість фізичного тригера перевіряємо, чи інженер у прямокутнику модуля: так простіше і не потрібні Rigidbody.
/// </summary>
public class ModuleInterior : MonoBehaviour
{
    /// <summary>Чи інженер зараз у якомусь модулі (читає OutdoorDarkening).</summary>
    public static bool EngineerInsideAny => insideCount > 0;
    static int insideCount;

    static Transform engineer;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static readonly int Indoor = Shader.PropertyToID("_Indoor");

    ModuleView view;
    Renderer[] shell;
    Color[] shellColors;
    Renderer[] interior;
    Renderer[] hiddenWhenClosed; // меблі-спрайти: видно тільки, коли оболонка відкрита

    /// <summary>Чи модуль зараз «відкритий» (оболонка майже зникла) — тоді видно меблі й людей усередині.</summary>
    public bool IsOpen => shellAlpha < 0.6f;
    MaterialPropertyBlock block;
    float shellAlpha = 1f;   // 1 = оболонку видно, 0 = сховано
    float applied = -1f;     // яке значення вже намальоване (щоб не робити зайвої роботи щокадру)
    bool inside;

    public void Setup(ModuleView view, Renderer[] shell, Color[] shellColors, Renderer[] interior, Renderer[] hiddenWhenClosed)
    {
        this.view = view;
        view.Interior = this;
        this.hiddenWhenClosed = hiddenWhenClosed;
        this.shell = shell;
        this.shellColors = shellColors;
        this.interior = interior;
        block = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (view == null) return;
        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            engineer = go.transform;
        }

        bool nowInside = view.Contains(engineer.position);
        if (nowInside != inside)
        {
            inside = nowInside;
            insideCount += inside ? 1 : -1;
        }

        var look = ModuleLook.Instance;
        float fade = look != null ? look.roofFadeSeconds : 0.4f;
        float step = fade > 0f ? Time.unscaledDeltaTime / fade : 1f;
        shellAlpha = Mathf.MoveTowards(shellAlpha, inside ? 0f : 1f, step);

        if (!Mathf.Approximately(applied, shellAlpha)) Apply(look);
    }

    void Apply(ModuleLook look)
    {
        applied = shellAlpha;

        for (int i = 0; i < shell.Length; i++)
        {
            var r = shell[i];
            if (r == null) continue;
            r.enabled = shellAlpha > 0.01f; // зовсім прозору оболонку просто вимикаємо

            var c = shellColors[i];
            c.a *= shellAlpha;
            // Непрозоре малюємо звичайним матеріалом (правильно ховає персонажів позаду),
            // напівпрозоре — скляним (roofMaterial).
            if (look != null)
            {
                var mat = c.a > 0.99f ? look.wallMaterial : look.roofMaterial;
                if (mat != null && r.sharedMaterial != mat) r.sharedMaterial = mat;
            }
            r.GetPropertyBlock(block);
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }

        // Меблі-спрайти: поки ти надворі — сховані (інакше плоска картинка вилазить крізь оболонку)
        foreach (var r in hiddenWhenClosed) if (r != null) r.enabled = IsOpen;

        // Світло всередині: шейдер Moon/Toon підмішує теплий колір (_Indoor 0..1)
        foreach (var r in interior)
        {
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(Indoor, 1f - shellAlpha);
            r.SetPropertyBlock(block);
        }
    }

    void OnDestroy()
    {
        if (inside) insideCount--;
    }
}
