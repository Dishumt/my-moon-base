// AI-assisted: written with Claude. See docs/AI_USE.md
// UiDocCheck (role 05): debug helper. Put it on a GameObject with UIDocument,
// press Play and read the [UiDocCheck] lines in Console. Not used in the game.
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class UiDocCheck : MonoBehaviour
{
    IEnumerator Start()
    {
        // wait 2 frames so the layout is calculated
        yield return null;
        yield return null;

        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        Debug.Log("[UiDocCheck] enabled=" + doc.enabled
                  + " panelSettings=" + (doc.panelSettings ? doc.panelSettings.name : "NONE")
                  + " source=" + (doc.visualTreeAsset ? doc.visualTreeAsset.name : "NONE")
                  + " root=" + (root == null ? "NULL" : "ok")
                  + " screen=" + Screen.width + "x" + Screen.height);
        if (root == null) yield break;

        Debug.Log("[UiDocCheck] panel=" + (root.panel != null ? root.panel.contextType.ToString() : "NULL")
                  + " children=" + root.childCount
                  + " rootLayout=" + root.layout
                  + " rootStyleSheets=" + root.styleSheets.count
                  + " display=" + root.resolvedStyle.display
                  + " opacity=" + root.resolvedStyle.opacity);

        LogElement(root, "preview-root");
        LogElement(root, "tablet");
    }

    void LogElement(VisualElement root, string name)
    {
        var e = root.Q(name);
        if (e == null) { Debug.Log("[UiDocCheck] '" + name + "' NOT FOUND"); return; }
        Debug.Log("[UiDocCheck] '" + name + "' layout=" + e.layout
                  + " world=" + e.worldBound
                  + " opacity=" + e.resolvedStyle.opacity
                  + " display=" + e.resolvedStyle.display
                  + " visibility=" + e.resolvedStyle.visibility
                  + " bg=" + e.resolvedStyle.backgroundColor
                  + " classes=" + string.Join(" ", e.GetClasses()));
    }
}
