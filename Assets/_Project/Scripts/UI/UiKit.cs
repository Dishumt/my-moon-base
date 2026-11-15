// AI-assisted: written with Claude. See docs/AI_USE.md
using System;
using UnityEngine.UIElements;

/// <summary>
/// Small helpers for UI made in C#: a label or a box with USS classes in one line.
///   var title = UiKit.Label(parent, "gt-big");
///   var row = UiKit.Box(parent, "pw-row");
/// </summary>
public static class UiKit
{
    public static Label Label(VisualElement parent, string classes, string text = "")
    {
        var label = new Label(text);
        AddClasses(label, classes);
        parent?.Add(label);
        return label;
    }

    public static VisualElement Box(VisualElement parent, string classes)
    {
        var box = new VisualElement();
        AddClasses(box, classes);
        parent?.Add(box);
        return box;
    }

    public static Button Button(VisualElement parent, string classes, Action onClick, string text = "")
    {
        var button = new Button(onClick) { text = text };
        AddClasses(button, classes);
        parent?.Add(button);
        return button;
    }

    public static void AddClasses(VisualElement element, string classes)
    {
        if (string.IsNullOrEmpty(classes)) return;
        foreach (var c in classes.Split(' '))
            if (c.Length > 0) element.AddToClassList(c);
    }

    public static void SetText(TextElement element, string text)
    {
        if (element != null && element.text != text) element.text = text;
    }

    public static void Show(VisualElement element, bool visible)
    {
        if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>Game objects of the scene (null-safe): state and systems.</summary>
    public static GameState State => GameBootstrap.Instance != null ? GameBootstrap.Instance.State : null;
    public static T Sys<T>() where T : class, ISimSystem
        => GameBootstrap.Instance != null && GameBootstrap.Instance.Clock != null ? GameBootstrap.Instance.GetSystem<T>() : null;

    /// <summary>A window that must stay on top is open: start menu, report, launch screen. The tablet waits.</summary>
    public static bool Blocked =>
        StartMenu.IsShown || GameWindows.IsShown || GameMenu.IsShown || (LaunchScreen.Instance != null && LaunchScreen.Instance.IsOpen);

    /// <summary>Number for the player: 12, 3.5 (no long tails).</summary>
    public static string Num(float value) => Math.Abs(value - Math.Round(value)) < 0.05f ? Math.Round(value).ToString("0") : value.ToString("0.0");
}
