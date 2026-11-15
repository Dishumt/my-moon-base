// AI-assisted: written with OpenAI Codex. See docs/AI_USE.md
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Shared, build-included sprites for the menu and flight. Missing art keeps the procedural fallback.</summary>
public static class FlightArt
{
    public static VisualElement Add(VisualElement parent, string resource, string name)
    {
        var texture = Resources.Load<Texture2D>(resource);
        if (texture == null) return null;
        var image = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
        image.style.position = Position.Absolute;
        image.style.backgroundImage = new StyleBackground(texture);
        image.AddToClassList("flight-art");
        parent.Add(image);
        return image;
    }

    public static void Rect(VisualElement image, float x, float y, float width, float height)
    {
        if (image == null) return;
        image.style.left = x;
        image.style.top = y;
        image.style.width = width;
        image.style.height = height;
    }
}
