// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Кристали льоду на дні кратера мерехтять: яскравість і холодний відтінок хвилею, кожен кристал у своєму такті,
/// а повітря над кратером іноді «спалахує» блискіткою. Додається на точку з льодом (PoiMarker) сама.
/// Шукає дочірні спрайти з «ice» або «crystal» в назві об'єкта чи спрайта (як у DecorSetupTool.PoiProps).
/// </summary>
public class IceSparkle : MonoBehaviour
{
    class Crystal
    {
        public SpriteRenderer Renderer;
        public float Phase, Speed;
    }

    readonly List<Crystal> crystals = new List<Crystal>();
    Sprite spark;
    SpriteRenderer[] sparks;
    float[] sparkLeft;
    float nextSpark;

    static readonly Color Cold = new Color(0.62f, 0.86f, 1f, 1f);

    void Start()
    {
        foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
        {
            string n = (r.gameObject.name + " " + (r.sprite != null ? r.sprite.name : "")).ToLowerInvariant();
            if (!n.Contains("ice") && !n.Contains("crystal")) continue;
            crystals.Add(new Crystal { Renderer = r, Phase = Random.value * 6.28f, Speed = Random.Range(1.4f, 2.6f) });
        }
        // маленька біла «блиск-зірочка» (8×8, створюється кодом)
        spark = MakeSpark();
        sparks = new SpriteRenderer[3];
        sparkLeft = new float[3];
        for (int i = 0; i < sparks.Length; i++)
        {
            var go = new GameObject("Ice Spark " + i);
            go.transform.SetParent(transform, false);
            go.AddComponent<Billboard>().BillboardMode = Billboard.Mode.FaceCamera;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spark;
            sr.enabled = false;
            sparks[i] = sr;
        }
    }

    void Update()
    {
        float t = Time.time;
        foreach (var c in crystals)
        {
            if (c.Renderer == null) continue;
            float wave = 0.5f + 0.5f * Mathf.Sin(t * c.Speed + c.Phase); // 0..1
            c.Renderer.color = Color.Lerp(Cold, Color.white, wave);
        }

        if (sparks == null || crystals.Count == 0) return;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparkLeft[i] <= 0f) continue;
            sparkLeft[i] -= Time.deltaTime;
            float k = Mathf.Clamp01(sparkLeft[i] / 0.5f);               // 1 → 0 за пів секунди
            float s = Mathf.Sin(k * Mathf.PI);                           // з'являється й гасне
            sparks[i].transform.localScale = Vector3.one * (0.25f + 0.55f * s);
            sparks[i].color = new Color(1f, 1f, 1f, s);
            if (sparkLeft[i] <= 0f) sparks[i].enabled = false;
        }
        if (t < nextSpark) return;
        nextSpark = t + Random.Range(0.35f, 1.1f);
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparkLeft[i] > 0f) continue;
            var c = crystals[Random.Range(0, crystals.Count)];
            if (c.Renderer == null) return;
            sparks[i].transform.position = c.Renderer.transform.position + Vector3.up * Random.Range(0.2f, 0.9f)
                                           + new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            sparks[i].sortingOrder = c.Renderer.sortingOrder + 1;
            sparks[i].enabled = true;
            sparkLeft[i] = 0.5f;
            return;
        }
    }

    static Sprite MakeSpark()
    {
        const int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                // чотирипроменева зірка: яскраво вздовж осей, швидко гасне по діагоналі
                float a = Mathf.Clamp01(1f - (dx + dy) * 1.0f) + Mathf.Clamp01(1f - Mathf.Min(dx, dy) * 6f) * Mathf.Clamp01(1f - Mathf.Max(dx, dy));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }
}
