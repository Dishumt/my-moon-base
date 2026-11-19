// AI-assisted: written with Claude. See docs/AI_USE.md
using NUnit.Framework;
using UnityEngine;

/// <summary>Кнопки на екрані телефона передають команди через GameInput так само, як клавіатура.</summary>
public class TouchInputTests
{
    [TearDown]
    public void Clean() => GameInput.TouchReleaseAll();

    [Test]
    public void Joystick_MovesEngineer_NotFasterThanOne()
    {
        GameInput.TouchMove(new Vector2(3f, 4f));
        Assert.AreEqual(1f, GameInput.Move.magnitude, 0.001f, "по діагоналі не швидше");
        GameInput.TouchMove(new Vector2(0f, 0.5f));
        Assert.AreEqual(0.5f, GameInput.Move.y, 0.001f, "пів дороги пальцем — пів швидкості");
    }

    [Test]
    public void ReleaseAll_StopsEngineer_AndRepair()
    {
        GameInput.TouchMove(Vector2.up);
        GameInput.TouchUseHeld(true);
        Assert.IsTrue(GameInput.UseHeld);
        GameInput.TouchReleaseAll();
        Assert.AreEqual(Vector2.zero, GameInput.Move);
        Assert.IsFalse(GameInput.UseHeld, "сховали кнопки — інженер не лагодить сам");
    }

    [Test]
    public void Buttons_PressOnlyInThisFrame()
    {
        GameInput.TouchUse();
        GameInput.TouchJump();
        GameInput.TouchTurn(-1);
        GameInput.TouchZoom(1);
        Assert.IsTrue(GameInput.UsePressed);
        Assert.IsTrue(GameInput.JumpPressed);
        Assert.AreEqual(-1, GameInput.Turn);
        Assert.AreEqual(1, GameInput.Zoom);
    }

    [TestCase("en")]
    [TestCase("uk")]
    public void TouchTexts_Exist(string language)
    {
        string before = Loc.Language;
        Loc.SetLanguage(language);
        foreach (var key in new[] { "hud.walk_touch", "touch.jump", "touch.turn_phone" })
            Assert.IsFalse(Loc.Get(key).StartsWith("#"), key + " немає в texts_" + language);
        Loc.SetLanguage(before);
    }
}
