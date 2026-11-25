// AI-assisted: written with Claude. See docs/AI_USE.md
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>Налаштування, без яких гра в браузері на телефоні й планшеті ламається.</summary>
public class WebBuildTests
{
    [Test]
    public void Ui_FitsAnyScreen()
    {
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/_Project/Settings/GamePanelSettings.asset");
        Assert.IsNotNull(panel);
        Assert.AreEqual(PanelScreenMatchMode.Expand, panel.screenMatchMode,
            "інакше на екрані 4:3 чи 16:10 вікно планшета (1232×688) обрізається разом із кнопкою X");
    }

    [Test]
    public void WebProfile_UsesOurPage()
    {
        string profile = File.ReadAllText("Assets/Settings/Build Profiles/Web - Desktop - Release.asset");
        StringAssert.Contains("webGLTemplate: PROJECT:MoonBase", profile, "сторінка гри з підтримкою телефона");
        StringAssert.Contains("productName: My Moon Base", profile);
    }
}
