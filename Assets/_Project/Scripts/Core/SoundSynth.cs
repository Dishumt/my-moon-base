// AI-assisted: written with Claude. See docs/AI_USE.md
using System;

/// <summary>Усі звуки гри. Кожен генерується кодом, тому в проєкті немає чужих аудіофайлів і ліцензій.</summary>
public enum Sfx
{
    Click, Toggle, Confirm, Error, Alarm, StormWarning, StormStart,
    Report, Win, Lose, Launch, LaunchFail, Arrival, Built,
    Ambient, StormLoop,   // ці два крутяться по колу
}

/// <summary>
/// Синтезатор: перетворює формулу на масив чисел-звуку (float від −1 до 1).
/// Звичайний C#-клас без Unity, тому його легко перевірити тестом.
/// Для WebGL зручно: нічого не вантажимо з мережі, а файли .wav/.mp3 роздули б збірку.
/// </summary>
public static class SoundSynth
{
    /// <summary>Скільки відліків на секунду. 22 050 вистачає для простих сигналів і вдвічі легше за 44 100.</summary>
    public const int Rate = 22050;

    /// <summary>Довжина звуку, що крутиться по колу, у секундах (частоти підібрані під ціле число хвиль).</summary>
    public const float LoopSeconds = 4f;

    const float Tau = (float)(Math.PI * 2.0);

    public enum Wave { Sine, Triangle, Square }

    /// <summary>Створити звук. Результат завжди в межах [−1; 1].</summary>
    public static float[] Make(Sfx sfx)
    {
        float[] b;
        switch (sfx)
        {
            case Sfx.Click:
                b = Buf(0.07f);
                Tone(b, 0f, 0.06f, 1500f, 900f, 0.55f, Wave.Sine, 2f);
                break;

            case Sfx.Toggle:
                b = Buf(0.16f);
                Tone(b, 0f, 0.06f, 600f, 600f, 0.45f, Wave.Sine, 2f);
                Tone(b, 0.07f, 0.08f, 900f, 900f, 0.45f, Wave.Sine, 2f);
                break;

            case Sfx.Confirm:
                b = Buf(0.30f);
                Tone(b, 0f, 0.12f, 660f, 660f, 0.45f, Wave.Triangle, 1.5f);
                Tone(b, 0.09f, 0.20f, 990f, 990f, 0.45f, Wave.Triangle, 1.5f);
                break;

            case Sfx.Error:
                b = Buf(0.30f);
                Tone(b, 0f, 0.14f, 190f, 150f, 0.35f, Wave.Square, 1f);
                Tone(b, 0.15f, 0.14f, 150f, 120f, 0.35f, Wave.Square, 1f);
                break;

            case Sfx.Alarm:
                b = Buf(0.90f);
                for (int i = 0; i < 3; i++)
                {
                    Tone(b, i * 0.30f, 0.14f, 880f, 880f, 0.32f, Wave.Square, 0.7f);
                    Tone(b, i * 0.30f + 0.15f, 0.14f, 660f, 660f, 0.32f, Wave.Square, 0.7f);
                }
                break;

            case Sfx.StormWarning:
                b = Buf(1.5f);
                Tone(b, 0f, 0.70f, 450f, 900f, 0.45f, Wave.Triangle, 0.6f);
                Tone(b, 0.75f, 0.70f, 450f, 900f, 0.45f, Wave.Triangle, 0.6f);
                break;

            case Sfx.StormStart:
                b = Buf(2.0f);
                Noise(b, 0f, 2.0f, 0.55f, 0.06f, 1.4f, 1);
                Tone(b, 0f, 1.2f, 70f, 45f, 0.55f, Wave.Sine, 1.2f);
                break;

            case Sfx.Report:
                b = Buf(0.70f);
                Tone(b, 0.00f, 0.30f, 523f, 523f, 0.38f, Wave.Triangle, 1.6f);
                Tone(b, 0.12f, 0.30f, 659f, 659f, 0.38f, Wave.Triangle, 1.6f);
                Tone(b, 0.24f, 0.45f, 784f, 784f, 0.38f, Wave.Triangle, 1.6f);
                break;

            case Sfx.Win:
                b = Buf(1.6f);
                Tone(b, 0.00f, 0.30f, 523f, 523f, 0.40f, Wave.Triangle, 1.4f);
                Tone(b, 0.18f, 0.30f, 659f, 659f, 0.40f, Wave.Triangle, 1.4f);
                Tone(b, 0.36f, 0.30f, 784f, 784f, 0.40f, Wave.Triangle, 1.4f);
                Tone(b, 0.54f, 1.00f, 1047f, 1047f, 0.45f, Wave.Triangle, 1.8f);
                Tone(b, 0.54f, 1.00f, 523f, 523f, 0.25f, Wave.Sine, 1.8f);
                break;

            case Sfx.Lose:
                b = Buf(1.5f);
                Tone(b, 0.00f, 0.40f, 440f, 430f, 0.38f, Wave.Triangle, 1.3f);
                Tone(b, 0.30f, 0.40f, 392f, 380f, 0.38f, Wave.Triangle, 1.3f);
                Tone(b, 0.60f, 0.40f, 349f, 335f, 0.38f, Wave.Triangle, 1.3f);
                Tone(b, 0.90f, 0.60f, 294f, 270f, 0.42f, Wave.Triangle, 1.6f);
                break;

            case Sfx.Launch:
                b = Buf(3.0f);
                Rumble(b, 3.0f, 0.55f);
                Tone(b, 0f, 3.0f, 55f, 140f, 0.35f, Wave.Sine, 0.8f);
                break;

            case Sfx.LaunchFail:
                b = Buf(1.6f);
                Rumble(b, 1.2f, 0.4f);
                Tone(b, 0.2f, 1.2f, 420f, 90f, 0.40f, Wave.Triangle, 1.2f);
                break;

            case Sfx.Arrival:
                b = Buf(0.9f);
                Tone(b, 0.00f, 0.50f, 1200f, 1200f, 0.30f, Wave.Sine, 2.2f);
                Tone(b, 0.15f, 0.70f, 1800f, 1800f, 0.25f, Wave.Sine, 2.2f);
                break;

            case Sfx.Built:
                b = Buf(0.65f);
                Tone(b, 0.00f, 0.22f, 880f, 880f, 0.40f, Wave.Sine, 2f);
                Tone(b, 0.13f, 0.50f, 1320f, 1320f, 0.40f, Wave.Sine, 2f);
                break;

            case Sfx.Ambient:
                b = Drone(LoopSeconds, 55f, 82.5f, 110f, 0.30f);
                break;

            case Sfx.StormLoop:
                // три близькі низькі частоти «б'ються» між собою: гудіння, що переливається
                b = Drone(LoopSeconds, 40f, 43f, 47f, 0.55f);
                break;

            default:
                b = Buf(0.05f);
                break;
        }
        Limit(b);
        return b;
    }

    // ---------- цеглинки ----------

    static float[] Buf(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

    /// <summary>
    /// Додає в звук один тон: частота плавно йде від f0 до f1.
    /// attack — швидке наростання на початку (без клацання), decay — як швидко затихає: 1 = рівно до нуля, 2 = швидше.
    /// </summary>
    static void Tone(float[] b, float start, float length, float f0, float f1, float volume, Wave wave, float decay)
    {
        int from = Math.Max(0, (int)(start * Rate));
        int count = Math.Max(1, (int)(length * Rate));
        float phase = 0f;
        float attack = Math.Max(1, (int)(0.004f * Rate));
        for (int i = 0; i < count; i++)
        {
            int at = from + i;
            if (at >= b.Length) break;
            float t = i / (float)count;                       // 0..1 за час тону
            float freq = f0 + (f1 - f0) * t;
            phase += Tau * freq / Rate;
            if (phase > Tau) phase -= Tau;
            float env = Math.Min(1f, i / attack) * (float)Math.Pow(1f - t, decay);
            b[at] += Shape(phase, wave) * volume * env;
        }
    }

    static float Shape(float phase, Wave wave)
    {
        switch (wave)
        {
            case Wave.Square: return Math.Sin(phase) >= 0.0 ? 0.6f : -0.6f;
            case Wave.Triangle: return (2f / (float)Math.PI) * (float)Math.Asin(Math.Sin(phase));
            default: return (float)Math.Sin(phase);
        }
    }

    /// <summary>Шум, пропущений через простий фільтр: smooth близько 0.05 = глухий гул, 0.5 = шипіння.</summary>
    static void Noise(float[] b, float start, float length, float volume, float smooth, float decay, int seed)
    {
        int from = Math.Max(0, (int)(start * Rate));
        int count = Math.Max(1, (int)(length * Rate));
        uint x = (uint)(2463534242u + seed * 7919);
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            int at = from + i;
            if (at >= b.Length) break;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;                    // дешевий випадковий лічильник
            float white = (x & 0xFFFF) / 32768f - 1f;                   // −1..1
            low += (white - low) * smooth;                              // згладжування
            float t = i / (float)count;
            float env = Math.Min(1f, i / (0.02f * Rate)) * (float)Math.Pow(1f - t, decay);
            b[at] += low * volume * 2.2f * env;
        }
    }

    /// <summary>Гул двигуна: шум наростає, потім затихає.</summary>
    static void Rumble(float[] b, float length, float volume)
    {
        int count = Math.Min(b.Length, (int)(length * Rate));
        uint x = 88172645u;
        float low = 0f;
        for (int i = 0; i < count; i++)
        {
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            float white = (x & 0xFFFF) / 32768f - 1f;
            low += (white - low) * 0.07f;
            float t = i / (float)count;
            float env = t < 0.2f ? t / 0.2f : (float)Math.Pow(1f - (t - 0.2f) / 0.8f, 1.1f);
            b[i] += low * volume * 2.4f * env;
        }
    }

    /// <summary>
    /// Звук, що крутиться по колу без клацання: кожна частота робить ціле число хвиль за seconds,
    /// а повільне «дихання» гучності — ціле число періодів, тож кінець плавно переходить у початок.
    /// </summary>
    static float[] Drone(float seconds, float f1, float f2, float f3, float volume)
    {
        var b = Buf(seconds);
        int n = b.Length;
        float f1c = SnapToLoop(f1, seconds), f2c = SnapToLoop(f2, seconds), f3c = SnapToLoop(f3, seconds);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n; // 0..1 по колу
            float breath = 0.6f + 0.4f * (float)Math.Sin(Tau * 1f * t);   // 1 вдих за коло
            float breath2 = 0.7f + 0.3f * (float)Math.Sin(Tau * 3f * t + 1.3f);
            float s = (float)Math.Sin(Tau * f1c * seconds * t) * 0.5f * breath
                    + (float)Math.Sin(Tau * f2c * seconds * t) * 0.3f * breath2
                    + (float)Math.Sin(Tau * f3c * seconds * t) * 0.2f * breath;
            b[i] = s * volume;
        }
        return b;
    }

    /// <summary>Підігнати частоту так, щоб за seconds вмістилося ціле число хвиль.</summary>
    static float SnapToLoop(float hz, float seconds) => (float)Math.Round(hz * seconds) / seconds;

    /// <summary>Обрізати все, що вилізло за межі [−1; 1] (інакше буде тріск).</summary>
    static void Limit(float[] b)
    {
        for (int i = 0; i < b.Length; i++)
        {
            float v = b[i];
            if (float.IsNaN(v)) v = 0f;
            b[i] = v > 1f ? 1f : (v < -1f ? -1f : v);
        }
    }
}
