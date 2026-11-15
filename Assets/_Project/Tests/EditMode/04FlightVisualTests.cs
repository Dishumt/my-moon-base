// AI-assisted: written with OpenAI Codex. See docs/AI_USE.md
using NUnit.Framework;
using UnityEngine;

public class FlightVisualTests
{
    [Test]
    public void Transfer_ReachesBothOrbitBoundaries_AndNeverReverses()
    {
        Assert.That(FlightMotion.TransferAnomaly(0f, 135f, 315f), Is.EqualTo(0f).Within(0.001f));
        Assert.That(FlightMotion.TransferAnomaly(1f, 135f, 315f), Is.EqualTo(180f).Within(0.001f));
        float previous = -1f;
        for (int i = 0; i <= 100; i++)
        {
            float angle = FlightMotion.TransferAnomaly(i / 100f, 135f, 315f);
            Assert.That(angle, Is.GreaterThanOrEqualTo(previous));
            previous = angle;
        }
        // Equal time slices cover more angle near Earth than near the far end of the ellipse.
        Assert.That(FlightMotion.TransferAnomaly(0.1f, 135f, 315f),
            Is.GreaterThan(180f - FlightMotion.TransferAnomaly(0.9f, 135f, 315f)));
    }

    [Test]
    public void Attitude_TakesShortestTurn_AtDifferentFrameRates()
    {
        float at30 = 350f, at144 = 350f;
        for (int i = 0; i < 30; i++) at30 = FlightMotion.FollowAngle(at30, 10f, 1f / 30f);
        for (int i = 0; i < 144; i++) at144 = FlightMotion.FollowAngle(at144, 10f, 1f / 144f);
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(at30, at144)), Is.LessThan(0.01f));
        Assert.That(Mathf.Abs(Mathf.DeltaAngle(at30, 10f)), Is.LessThan(0.01f));
        Assert.That(FlightMotion.FollowAngle(350f, 10f, 1f / 60f), Is.GreaterThan(350f));
    }

    [Test]
    public void Descent_StartsAndEndsAtRest_WithoutOvershooting()
    {
        Assert.That(FlightMotion.Smooth01(-1f), Is.EqualTo(0f));
        Assert.That(FlightMotion.Smooth01(2f), Is.EqualTo(1f));
        Assert.That(FlightMotion.Smooth01(0.01f) / 0.01f, Is.LessThan(0.002f));
        Assert.That((1f - FlightMotion.Smooth01(0.99f)) / 0.01f, Is.LessThan(0.002f));
    }

    static int Particles(int fps)
    {
        float remainder = 0f;
        int total = 0;
        for (int i = 0; i < fps * 3; i++)
            total += FlightMotion.EmitCount(ref remainder, 90f, 1f / fps, 64);
        return total;
    }

    [Test]
    public void Dust_IsFrameRateIndependent_AndBoundedAfterTabResume()
    {
        Assert.That(Particles(30), Is.EqualTo(Particles(144)).Within(1));
        float remainder = 0f;
        Assert.That(FlightMotion.EmitCount(ref remainder, 900f, 30f, 64), Is.EqualTo(64));
        Assert.That(FlightMotion.EmitCount(ref remainder, 900f, 1f, 0), Is.Zero);
        Assert.That(FlightMotion.EmitCount(ref remainder, 0f, 1f / 60f, 64), Is.Zero);
    }

    [TestCase("Art/Flight/earth")]
    [TestCase("Art/Flight/moon")]
    [TestCase("Art/Flight/lander")]
    [TestCase("Art/Flight/moon_rocks")]
    [TestCase("ISRU/ice_drill")]
    [TestCase("ISRU/ice_processor")]
    public void Sprites_AreIncludedAtRuntime_AtNativeResolution(string path)
    {
        var texture = Resources.Load<Texture2D>(path);
        Assert.That(texture, Is.Not.Null, path);
        Assert.That(texture.width, Is.EqualTo(256));
        Assert.That(texture.height, Is.EqualTo(256));
        Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(texture.mipmapCount, Is.EqualTo(1));
        Assert.That(Resources.Load<Sprite>(path), Is.Not.Null, "Imported as a single sprite");
    }
}
