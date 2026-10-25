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

    /// A tile put down at the table: a sharp slap of bone on wood with a little body, a touch different each time.
    static AudioClip slap;
    public static void Slap() => Play(slap ??= Make("slap", 0.18f, t =>
        Noise(t) * 0.75f * Mathf.Exp(-t * 140f) + Mathf.Sin(t * 2 * Mathf.PI * 340f) * 0.45f * Mathf.Exp(-t * 38f)
        + Mathf.Sin(t * 2 * Mathf.PI * 1180f) * 0.22f * Mathf.Exp(-t * 70f) + Mathf.Sin(t * 2 * Mathf.PI * 2650f) * 0.08f * Mathf.Exp(-t * 110f)), 0.95f, Random.Range(0.9f, 1.1f));

    /// The winning tile slammed down: a heavy knock on the table, and the other tiles rattling.
    static AudioClip slamClip;
    public static void Slam() => Play(slamClip ??= Make("slam", 0.7f, t =>
        Noise(t) * 0.9f * Mathf.Exp(-t * 60f) + Mathf.Sin(t * 2 * Mathf.PI * (120f - 40f * t)) * 0.9f * Mathf.Exp(-t * 9f)
        + Mathf.Sin(t * 2 * Mathf.PI * 260f) * 0.4f * Mathf.Exp(-t * 18f)
        + (t > 0.05f ? Noise(t * 3.1f) * 0.25f * Mathf.Exp(-(t - 0.05f) * 9f) * (0.5f + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * 31f)) : 0)), 1f);

    /// Dice shaken in the hand before the throw: quick little knocks of bone on bone.
    static AudioClip rattle;
    public static void Rattle() => Play(rattle ??= Make("rattle", 0.45f, t =>
    {
        float k = 0;
        for (int i = 0; i < 9; i++) { float at = i * 0.047f + (i % 3) * 0.006f; if (t > at) k += Mathf.Sin((t - at) * 2 * Mathf.PI * (2300f + i * 170f)) * Mathf.Exp(-(t - at) * 260f) * 0.5f + Noise(t + i) * Mathf.Exp(-(t - at) * 400f) * 0.25f; }
        return k * (0.6f + 0.4f * Mathf.Sin(t * 14f));
    }), 0.8f);

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

    /// Two dice rattling and landing on wood.
    static AudioClip dice;
    public static void Dice() => Play(dice ??= Make("dice", 0.55f, t =>
    {
        float s = 0;
        float[] hits = { 0f, 0.07f, 0.12f, 0.2f, 0.26f, 0.33f, 0.37f };
        for (int k = 0; k < hits.Length; k++)
        {
            float d = t - hits[k];
            if (d < 0) continue;
            float amp = 0.55f - k * 0.06f;
            s += (Noise(t * (1 + k)) * 0.6f + Mathf.Sin(d * 2 * Mathf.PI * (1300f + k * 170f)) * 0.5f) * amp * Mathf.Exp(-d * 70f);
        }
        return s * 0.6f;
    }), 0.9f);

    public static void Tap() => Play(tap ??= Make("tap", 0.05f, t => Mathf.Sin(t * 2 * Mathf.PI * 1400f) * 0.2f * Mathf.Exp(-t * 120f)), 0.6f);

    /// The last seconds of your turn: a short, bright double beep.
    static AudioClip alert;
    public static void Alert() => Play(alert ??= Make("alert", 0.32f, t =>
    {
        float on = (t < 0.09f || (t > 0.16f && t < 0.25f)) ? 1 : 0;
        return Mathf.Sin(t * 2 * Mathf.PI * 1760f) * 0.32f * on * Mathf.Clamp01(1 - (t % 0.16f) * 4);
    }), 1f);

    /// A quieter tick for each of the last seconds.
    static AudioClip tick;
    public static void Tick() => Play(tick ??= Make("tick", 0.06f, t => Mathf.Sin(t * 2 * Mathf.PI * 1250f) * 0.3f * Mathf.Exp(-t * 70f)), 0.7f);

    /// All game sound off while a full-screen ad plays.
    public static void Pause(bool on) { AudioListener.pause = on; }

    static void Play(AudioClip c, float vol) { if (SoundOn && !App.Recording) Src.PlayOneShot(c, vol); }
    static AudioSource pitched;
    static void Play(AudioClip c, float vol, float pitch)
    {
        if (!SoundOn || App.Recording) return;
        if (pitched == null) { pitched = Src.gameObject.AddComponent<AudioSource>(); pitched.playOnAwake = false; pitched.spatialBlend = 0; }
        pitched.pitch = pitch; pitched.PlayOneShot(c, vol);
    }

    public static void Buzz()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (VibrateOn) Handheld.Vibrate();
#endif
    }
}
