// AI-assisted: written with Claude. See docs/AI_USE.md
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Земля, що обертається (20 кадрів Юлі, Resources/Art/Earth/earth_01..20, зменшені до 256 px для браузера).
/// Сама знаходить картинки Землі в інтерфейсі й міняє кадри:
///  - меню старту: елемент "menu-earth-art" (арт 04) або старе намальоване коло з класом "menu-earth";
///  - сцена посадки: "landing-earth" (з Місяця видно, як Земля обертається).
/// На карті польоту Землю НЕ крутимо: там на ній стоїть мітка міста старту.
/// Нічого не треба ставити в сцену і змінювати чужий код: створюється сам при старті гри (техлід, 01).
/// </summary>
public class EarthSpin : MonoBehaviour
{
    const float FramesPerSecond = 5f;   // 20 кадрів → один оберт за 4 секунди
    static readonly string[] Names = { "menu-earth-art", "landing-earth" };
    const string OldMenuEarthClass = "menu-earth";

    Texture2D[] frames;
    readonly List<VisualElement> targets = new List<VisualElement>();
    float nextScan;
    int shownFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<EarthSpin>() != null) return;
        var go = new GameObject("Earth Spin");
        DontDestroyOnLoad(go);
        go.AddComponent<EarthSpin>();
    }

    void Awake()
    {
        var list = new List<Texture2D>();
        for (int i = 1; i <= 20; i++)
        {
            var t = Resources.Load<Texture2D>("Art/Earth/earth_" + i.ToString("00"));
            if (t != null) list.Add(t);
        }
        frames = list.Count > 0 ? list.ToArray() : null;
    }

    void Update()
    {
        if (frames == null) return;

        // Двічі на секунду шукаємо Землю заново: меню, запуск і посадка створюють свої елементи, коли відкриваються
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Rescan();
            shownFrame = -1; // нові елементи теж отримають кадр
        }
        if (targets.Count == 0) return;

        // Кадри Юлі йдуть із заходу на схід, а справжня Земля крутиться навпаки — граємо їх з кінця
        int frame = frames.Length - 1 - (int)(Time.unscaledTime * FramesPerSecond) % frames.Length;
        if (frame == shownFrame) return;
        shownFrame = frame;
        var image = new StyleBackground(frames[frame]);
        foreach (var e in targets)
            if (e != null && e.panel != null) e.style.backgroundImage = image;
    }

    void Rescan()
    {
        targets.Clear();
        foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude))
        {
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) continue;
            foreach (var name in Names)
                foreach (var e in root.Query<VisualElement>(name).ToList()) targets.Add(e);
            foreach (var e in root.Query<VisualElement>(className: OldMenuEarthClass).ToList())
            {
                PrepareOldEarth(e);
                targets.Add(e);
            }
        }
    }

    /// <summary>Старе коло з CSS (синій фон, рамка, «материк»): ховаємо намальоване, лишаємо місце для кадрів.</summary>
    static void PrepareOldEarth(VisualElement e)
    {
        e.style.backgroundColor = Color.clear;
        e.style.borderTopWidth = e.style.borderBottomWidth = 0;
        e.style.borderLeftWidth = e.style.borderRightWidth = 0;
        e.style.width = e.style.height = 120;
        foreach (var child in e.Children()) child.style.display = DisplayStyle.None;
    }
}
