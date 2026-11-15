// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Маленькі «цеглинки», з яких код збирає модулі: коробка, циліндр.
/// Колір задаємо через MaterialPropertyBlock — так не треба окремого матеріалу на кожен колір.
/// </summary>
public static class ModuleParts
{
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock block;

    /// <summary>Куб усередині parent. collider = false — крізь нього можна ходити.</summary>
    public static GameObject Box(Transform parent, string name, Material mat, Vector3 localPos, Vector3 localScale,
                                 bool collider = true, Color? tint = null)
    {
        return Make(PrimitiveType.Cube, parent, name, mat, localPos, localScale, collider, tint);
    }

    /// <summary>Циліндр (висота = 2 × localScale.y — так влаштований циліндр Unity).</summary>
    public static GameObject Cylinder(Transform parent, string name, Material mat, Vector3 localPos, Vector3 localScale,
                                      bool collider = true, Color? tint = null)
    {
        return Make(PrimitiveType.Cylinder, parent, name, mat, localPos, localScale, collider, tint);
    }

    /// <summary>Сфера (діаметр = localScale). Половина під землею виглядає як купол.</summary>
    public static GameObject Sphere(Transform parent, string name, Material mat, Vector3 localPos, Vector3 localScale,
                                    bool collider = true, Color? tint = null)
    {
        return Make(PrimitiveType.Sphere, parent, name, mat, localPos, localScale, collider, tint);
    }

    static GameObject Make(PrimitiveType type, Transform parent, string name, Material mat, Vector3 pos, Vector3 scale,
                           bool collider, Color? tint)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        var rend = go.GetComponent<Renderer>();
        if (mat != null) rend.sharedMaterial = mat;
        rend.receiveShadows = false; // без рваних тіней усередині модулів
        if (tint.HasValue) SetColor(rend, tint.Value);

        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        return go;
    }

    static readonly System.Collections.Generic.Dictionary<string, Material> textured =
        new System.Collections.Generic.Dictionary<string, Material>();

    /// <summary>
    /// Копія матеріалу з текстурою з Resources (наприклад "Art/Base/solar_panel"), плитки tiling разів на грань.
    /// Одна копія на текстуру (кеш). Немає текстури — повертає звичайний матеріал.
    /// </summary>
    public static Material Textured(Material baseMat, string resource, Vector2 tiling)
    {
        if (baseMat == null) return null;
        if (textured.TryGetValue(resource, out var cached) && cached != null) return cached;
        var tex = Resources.Load<Texture2D>(resource);
        if (tex == null) return baseMat;
        var mat = new Material(baseMat) { name = baseMat.name + " + " + tex.name };
        if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        else
        {
            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;
        }
        textured[resource] = mat;
        return mat;
    }

    /// <summary>Перефарбувати один об'єкт, не змінюючи спільний матеріал.</summary>
    public static void SetColor(Renderer rend, Color color)
    {
        if (block == null) block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColor, color);
        rend.SetPropertyBlock(block);
    }
}
