// AI-assisted: written with Claude. See docs/AI_USE.md
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Unity: Moon → Зробити землю 2D (як у DST).
/// Земля стає пласкою «намальованою» картинкою, як у Don't Starve Together:
///  - створює текстуру-заглушку реголіту з кратерами (Settings/MoonGroundTile.png), яка повторюється без швів;
///  - створює матеріал Settings/MoonGround2D.mat з шейдером Unlit (без 3D-світла й тіней — виглядає як малюнок);
///  - ставить його на Ground і додає Ground у OutdoorDarkening, щоб вулиця далі темнішала.
/// Коли арт (02) намалює справжню текстуру, просто перетягни її в Base Map матеріалу MoonGround2D.
/// Працює тільки в редакторі (тут можна System.IO; у грі — ні).
/// </summary>
public static class GroundStyleTool
{
    const string TexPath = "Assets/_Project/Settings/MoonGroundTile.png";
    const string MatPath = "Assets/_Project/Settings/MoonGround2D.mat";
    const int Size = 512;           // пікселів у текстурі
    const float MetersPerTile = 10f; // одна копія текстури = 10×10 м

    [MenuItem("Moon/Зробити землю 2D (як у DST)")]
    public static void Apply()
    {
        var ground = GameObject.Find("Ground");
        if (ground == null) { Debug.LogError("GroundStyleTool: немає об'єкта Ground."); return; }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (tex == null) tex = CreateTexture();

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) { Debug.LogError("GroundStyleTool: немає шейдера URP Unlit."); return; }
            mat = new Material(shader);
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        // Скільки разів повторити текстуру: Plane має 10 м × scale
        float meters = 10f * ground.transform.localScale.x;
        mat.SetTextureScale("_BaseMap", Vector2.one * (meters / MetersPerTile));
        EditorUtility.SetDirty(mat);

        var rend = ground.GetComponent<Renderer>();
        Undo.RecordObject(rend, "Ground 2D");
        rend.sharedMaterial = mat;

        // Unlit не реагує на світло, тож затемнення вулиці фарбує землю окремо
        var dark = Object.FindFirstObjectByType<OutdoorDarkening>();
        if (dark != null)
        {
            var so = new SerializedObject(dark);
            var list = so.FindProperty("alsoDarken");
            bool has = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == rend) has = true;
            if (!has)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = rend;
                so.ApplyModifiedProperties();
            }
        }
        else Debug.LogWarning("GroundStyleTool: на Sun немає OutdoorDarkening — земля не темнітиме в модулях.");

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(ground.scene);
        Debug.Log("GroundStyleTool: готово! Збережи сцену (Ctrl+S).");
    }

    // ---------- Текстура-заглушка ----------

    static Texture2D CreateTexture()
    {
        var rnd = new System.Random(7);
        var px = new Color[Size * Size];

        // Основа: м'які плями (шум, що повторюється без швів)
        float[] n1 = Noise(8, rnd), n2 = Noise(32, rnd), n3 = Noise(96, rnd);
        Color light = new Color(0.62f, 0.60f, 0.57f), dark = new Color(0.44f, 0.43f, 0.42f);
        for (int i = 0; i < px.Length; i++)
        {
            float v = n1[i] * 0.55f + n2[i] * 0.3f + n3[i] * 0.15f;
            px[i] = Color.Lerp(dark, light, Mathf.SmoothStep(0.25f, 0.75f, v));
        }

        // Кратери: темна чаша і світлий обідок, як намальовані пензлем
        for (int k = 0; k < 28; k++)
        {
            float cx = (float)rnd.NextDouble() * Size, cy = (float)rnd.NextDouble() * Size;
            float r = 5f + (float)System.Math.Pow(rnd.NextDouble(), 2.5) * 45f;
            Crater(px, cx, cy, r);
        }

        // Дрібні камінчики-цятки
        for (int k = 0; k < 900; k++)
        {
            int x = rnd.Next(Size), y = rnd.Next(Size);
            float shade = rnd.NextDouble() < 0.5 ? 0.8f : 1.15f;
            px[y * Size + x] *= shade;
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(TexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexPath);

        var imp = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.mipmapEnabled = true;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
    }

    static void Crater(Color[] px, float cx, float cy, float r)
    {
        int ri = Mathf.CeilToInt(r * 1.3f);
        for (int dy = -ri; dy <= ri; dy++)
        for (int dx = -ri; dx <= ri; dx++)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
            if (d > 1.3f) continue;
            int x = ((int)cx + dx + Size) % Size, y = ((int)cy + dy + Size) % Size; // загортаємо через край — без швів
            float k;
            if (d < 1f) k = Mathf.Lerp(0.78f, 0.95f, d * d);             // темна чаша, світлішає до краю
            else k = 1f + 0.18f * (1f - Mathf.Abs(d - 1.1f) / 0.2f);     // світлий обідок
            px[y * Size + x] *= Mathf.Max(k, 0.7f);
        }
    }

    /// <summary>Згладжений випадковий шум з g×g вузлів, що повторюється (лівий край = правому).</summary>
    static float[] Noise(int g, System.Random rnd)
    {
        var grid = new float[g * g];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)rnd.NextDouble();

        var result = new float[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float fx = (float)x / Size * g, fy = (float)y / Size * g;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            int x1 = (x0 + 1) % g, y1 = (y0 + 1) % g;
            float a = Mathf.Lerp(grid[y0 * g + x0], grid[y0 * g + x1], tx);
            float b = Mathf.Lerp(grid[y1 * g + x0], grid[y1 * g + x1], tx);
            result[y * Size + x] = Mathf.Lerp(a, b, ty);
        }
        return result;
    }
}
