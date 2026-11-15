// AI-assisted: written with Claude. See docs/AI_USE.md
using UnityEngine;

/// <summary>
/// Звуки пускового майданчика (роль 04): «біп» на кожну секунду відліку, сигнал «СТАРТ!» і гул двигунів.
/// Якщо в інспекторі LaunchScreen не вказали свої звуки — генеруємо прості звуки кодом (синус і шум),
/// тож чужі файли й ліцензії не потрібні. Працює у WebGL: кліпи створюються один раз через AudioClip.Create + SetData.
/// Звичайний C#-клас: LaunchScreen створює його і передає в LaunchPadView.
/// </summary>
public class LaunchSounds
{
    const int Rate = 22050;   // частота дискретизації: для біпів і гулу вистачає

    readonly AudioSource shots;    // короткі звуки
    readonly AudioSource rumble;   // гул двигунів (по колу)
    readonly AudioClip beep, liftoff;
    readonly float volume;

    public LaunchSounds(GameObject host, AudioClip beepClip, AudioClip liftoffClip, AudioClip rumbleClip, float volume)
    {
        this.volume = Mathf.Clamp01(volume);
        shots = host.AddComponent<AudioSource>();
        shots.playOnAwake = false;
        rumble = host.AddComponent<AudioSource>();
        rumble.playOnAwake = false;
        rumble.loop = true;

        beep = beepClip != null ? beepClip : Tone("beep", 880f, 0.12f);
        liftoff = liftoffClip != null ? liftoffClip : Tone("liftoff", 1320f, 0.6f);
        rumble.clip = rumbleClip != null ? rumbleClip : Noise("rumble", 2f);

        // без AudioListener у сцені звуку не буде (зазвичай він на камері; у пісочниці його може не бути)
        if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
    }

    /// <summary>Секунда відліку: останні три — трохи вище, щоб було напружено.</summary>
    public void Count(int n)
    {
        if (beep == null) return;
        shots.pitch = n <= 3 ? 1.25f : 1f;
        shots.PlayOneShot(beep, volume * 0.6f);
    }

    /// <summary>СТАРТ!</summary>
    public void Liftoff()
    {
        if (liftoff == null) return;
        shots.pitch = 1f;
        shots.PlayOneShot(liftoff, volume * 0.7f);
    }

    /// <summary>Гучність гулу двигунів: 0 — тиша, 1 — на повну.</summary>
    public void Engines(float power)
    {
        if (rumble.clip == null) return;
        power = Mathf.Clamp01(power);
        if (power <= 0.001f) { if (rumble.isPlaying) rumble.Stop(); return; }
        rumble.volume = volume * power;
        rumble.pitch = 0.8f + 0.4f * power;   // що сильніше, то вищий гул
        if (!rumble.isPlaying) rumble.Play();
    }

    public void StopAll()
    {
        shots.Stop();
        rumble.Stop();
    }

    // ---------- Звуки кодом ----------

    /// <summary>Синус із плавним початком і кінцем (без клацання).</summary>
    static AudioClip Tone(string name, float hz, float seconds)
    {
        int n = Mathf.CeilToInt(Rate * seconds);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Min(1f, t / 0.01f) * Mathf.Clamp01((seconds - t) / (seconds * 0.4f));
            data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.5f;
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Низький шум (як гул двигуна): білий шум, пропущений через простий фільтр низьких частот.</summary>
    static AudioClip Noise(string name, float seconds)
    {
        int n = Mathf.CeilToInt(Rate * seconds);
        var data = new float[n];
        var rnd = new System.Random(11);
        float low = 0f, lower = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)rnd.NextDouble() * 2f - 1f;
            low += (white - low) * 0.08f;     // два фільтри підряд — глибший гул
            lower += (low - lower) * 0.08f;
            data[i] = lower * 3f;
        }
        // кінець плавно переходить у початок, щоб по колу не клацало
        int fade = Rate / 10;
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[n - fade + i] = Mathf.Lerp(data[n - fade + i], data[i], k);
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
