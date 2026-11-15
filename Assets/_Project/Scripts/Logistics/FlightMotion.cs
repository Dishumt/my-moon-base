// AI-assisted: written with OpenAI Codex. See docs/AI_USE.md
using UnityEngine;

/// <summary>Presentation-only motion; never advances the simulation or consumes its random numbers.</summary>
public static class FlightMotion
{
    public static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    public static float FollowAngle(float current, float target, float deltaTime)
    {
        return Mathf.LerpAngle(current, target, 1f - Mathf.Exp(-12f * Mathf.Max(0f, deltaTime)));
    }

    public static float TransferAnomaly(float u, float near, float far)
    {
        float e = (far - near) / (far + near);
        float mean = Mathf.PI * Mathf.Clamp01(u), eccentric = mean;
        for (int i = 0; i < 12; i++)
            eccentric -= (eccentric - e * Mathf.Sin(eccentric) - mean) / (1f - e * Mathf.Cos(eccentric));
        return 2f * Mathf.Atan2(Mathf.Sqrt(1f + e) * Mathf.Sin(eccentric * 0.5f),
                               Mathf.Sqrt(1f - e) * Mathf.Cos(eccentric * 0.5f)) * Mathf.Rad2Deg;
    }

    // Keep fractional emissions between frames; cap work after a long frame / browser-tab resume.
    public static int EmitCount(ref float remainder, float rate, float dt, int capacity)
    {
        remainder += Mathf.Max(0f, rate) * Mathf.Clamp(dt, 0f, 0.1f);
        int count = Mathf.FloorToInt(remainder);
        remainder -= count;
        return Mathf.Min(count, Mathf.Max(0, capacity));
    }
}
