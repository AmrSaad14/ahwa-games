// Sounds and vibration, with the player's settings. The sounds are made here (no audio files): a wooden
// clack for a tile going down, a soft two-note chime when it's your turn, a coin jingle for rewards, and a
// light tap for buttons.
using UnityEngine;

public static class Sfx
{
    public static bool SoundOn { get => PlayerPrefs.GetInt("sound", 1) == 1; set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); } }
    public static bool VibrateOn { get => PlayerPrefs.GetInt("vibrate", 1) == 1; set { PlayerPrefs.SetInt("vibrate", value ? 1 : 0); PlayerPrefs.Save(); } }

    static AudioSource src;
    static AudioClip clack, chime, coins, tap;
    const int Rate = 44100;

    static AudioSource Src
    {
        get
        {
            if (src != null) return src;
            var go = new GameObject("Sounds"); Object.DontDestroyOnLoad(go);
            src = go.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0;
            return src;
        }
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float> f)
    {
        int n = (int)(Rate * seconds);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1);
        var c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }

    static float Noise(float t) { var x = Mathf.Sin(t * 12.9898f * 1000f) * 43758.5453f; return (x - Mathf.Floor(x)) * 2 - 1; }

    public static void Clack() => Play(clack ??= Make("clack", 0.12f, t =>
        (Noise(t) * 0.5f * Mathf.Exp(-t * 90f)) + Mathf.Sin(t * 2 * Mathf.PI * 820f) * 0.35f * Mathf.Exp(-t * 55f) + Mathf.Sin(t * 2 * Mathf.PI * 1960f) * 0.15f * Mathf.Exp(-t * 80f)), 0.9f);

    public static void Chime() => Play(chime ??= Make("chime", 0.7f, t =>
    {
        float a = Mathf.Sin(t * 2 * Mathf.PI * 784f) * Mathf.Exp(-t * 6f);
        float b = t > 0.13f ? Mathf.Sin((t - 0.13f) * 2 * Mathf.PI * 1175f) * Mathf.Exp(-(t - 0.13f) * 5f) : 0;
        return (a + b) * 0.28f;
    }), 0.8f);

    public static void Coins() => Play(coins ??= Make("coins", 0.6f, t =>
    {
        float s = 0;
        for (int k = 0; k < 5; k++) { float t0 = k * 0.08f; if (t > t0) s += Mathf.Sin((t - t0) * 2 * Mathf.PI * (2200f + k * 330f)) * Mathf.Exp(-(t - t0) * 18f); }
        return s * 0.18f;
    }), 0.9f);

    public static void Tap() => Play(tap ??= Make("tap", 0.05f, t => Mathf.Sin(t * 2 * Mathf.PI * 1400f) * 0.2f * Mathf.Exp(-t * 120f)), 0.6f);

    static void Play(AudioClip c, float vol) { if (SoundOn && !App.Recording) Src.PlayOneShot(c, vol); }

    public static void Buzz()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (VibrateOn) Handheld.Vibrate();
#endif
    }
}
