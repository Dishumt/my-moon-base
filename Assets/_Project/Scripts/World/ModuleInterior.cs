// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// «Як у Project Zomboid»: коли інженер заходить у модуль, дах плавно зникає
/// і вмикається світло всередині. Вулицю затемнює OutdoorDarkening (на Sun).
/// Цей компонент додає ModuleView сам — руками ставити не треба.
/// Замість фізичного тригера перевіряємо, чи інженер у прямокутнику слота:
/// так простіше і не потрібні Rigidbody.
/// </summary>
public class ModuleInterior : MonoBehaviour
{
    /// <summary>Чи інженер зараз у якомусь модулі (читає OutdoorDarkening).</summary>
    public static bool EngineerInsideAny => insideCount > 0;
    static int insideCount;

    static Transform engineer;

    ModuleSlot slot;
    Renderer roof;
    Light interiorLight;
    MaterialPropertyBlock block;
    Color roofColor = Color.white;
    float roofAlpha = 1f;   // 1 = дах видно, 0 = сховано
    bool inside;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public void Setup(ModuleSlot slot, Renderer roof, Light interiorLight)
    {
        this.slot = slot;
        this.roof = roof;
        this.interiorLight = interiorLight;
        block = new MaterialPropertyBlock();
        if (roof.sharedMaterial != null && roof.sharedMaterial.HasProperty(BaseColor))
            roofColor = roof.sharedMaterial.GetColor(BaseColor);
    }

    void Update()
    {
        if (slot == null) return;
        if (engineer == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            engineer = go.transform;
        }

        bool nowInside = slot.Contains(engineer.position);
        if (nowInside != inside)
        {
            inside = nowInside;
            insideCount += inside ? 1 : -1;
        }

        var look = ModuleLook.Instance;
        float fade = look != null ? look.roofFadeSeconds : 0.4f;
        float step = fade > 0f ? Time.unscaledDeltaTime / fade : 1f;
        roofAlpha = Mathf.MoveTowards(roofAlpha, inside ? 0f : 1f, step);

        ApplyRoof();
        if (interiorLight != null && look != null)
            interiorLight.intensity = (1f - roofAlpha) * look.interiorLightIntensity;
    }

    void ApplyRoof()
    {
        if (roof == null) return;
        roof.enabled = roofAlpha > 0.01f; // зовсім прозорий дах просто вимикаємо
        var c = roofColor;
        c.a = roofColor.a * roofAlpha;
        block.SetColor(BaseColor, c);
        roof.SetPropertyBlock(block);
    }

    void OnDestroy()
    {
        if (inside) insideCount--;
    }
}
