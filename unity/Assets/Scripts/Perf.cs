// Smooth on every phone. Three levels:
//   full     60 frames a second, shadows, glow, smooth edges, the whole café of people
//   saver    30 frames a second, no shadows / glow / edge smoothing, the 3D drawn at 80%, two people in the café
//   lowest   the saver plus 70% resolution, half-size textures, two-bone skinning, one light at a time, nobody in
//            the café's background (phones with 2–3 GB of memory or few cores)
// The level is picked from the phone (memory, cores, graphics memory) and then watched: if the game drops below a
// smooth frame rate for a few seconds, it steps down by itself (once per level, with a short note). "Battery saver"
// in Settings is the player's own choice and stops the automatic steps.
using UnityEngine;

public static class Perf
{
    /// The saver (or the lowest level) is on.
    public static bool Saver { get => Level < 2; set { PlayerPrefs.SetInt("perf.chosen", 1); SetLevel(value ? (VeryLowPhone ? 0 : 1) : 2); } }
    /// The lowest level: the cheapest phones.
    public static bool Lowest => Level == 0;

    public static bool VeryLowPhone => Application.isMobilePlatform && ((Ram > 0 && Ram <= 3072) || (Cores > 0 && Cores <= 4 && Ram <= 4096));
    public static bool LowEnd => Application.isMobilePlatform && ((Ram > 0 && Ram <= 4096) || (Cores > 0 && Cores <= 4) || (Gpu > 0 && Gpu <= 1024));
    static int Ram => SystemInfo.systemMemorySize;
    static int Cores => SystemInfo.processorCount;
    static int Gpu => SystemInfo.graphicsMemorySize;

    /// 0 lowest · 1 saver · 2 full. Older installs kept "saver" as on/off: that choice carries over.
    public static int Level
    {
        get
        {
            if (PlayerPrefs.HasKey("perf.level")) return Mathf.Clamp(PlayerPrefs.GetInt("perf.level"), 0, 2);
            if (PlayerPrefs.HasKey("saver")) return PlayerPrefs.GetInt("saver") == 1 ? (VeryLowPhone ? 0 : 1) : 2;
            return VeryLowPhone ? 0 : LowEnd ? 1 : 2;
        }
    }
    static bool Chosen => PlayerPrefs.GetInt("perf.chosen", 0) == 1;

    static void SetLevel(int l) { PlayerPrefs.SetInt("perf.level", Mathf.Clamp(l, 0, 2)); PlayerPrefs.Save(); Apply(); }

    static Vector2Int full;
    static Watch watch;

    public static void Apply()
    {
        int l = Level;
        bool s = l < 2, low = l == 0;
        Application.targetFrameRate = s ? 30 : 60;
        QualitySettings.vSyncCount = 0;
        // the 3D scene at a lower resolution on phones (the text stays readable at 70–80%)
        if (Application.isMobilePlatform)
        {
            if (full.x == 0) full = new Vector2Int(Screen.width, Screen.height);
            float k = low ? 0.7f : s ? 0.8f : 1f;
            Screen.SetResolution(Mathf.RoundToInt(full.x * k), Mathf.RoundToInt(full.y * k), true);
        }
        QualitySettings.shadows = s ? ShadowQuality.Disable : ShadowQuality.All;
        QualitySettings.shadowResolution = s ? ShadowResolution.Low : ShadowResolution.High;
        QualitySettings.antiAliasing = s ? 0 : 4;
        QualitySettings.pixelLightCount = low ? 1 : s ? 2 : 4;
        QualitySettings.globalTextureMipmapLimit = low ? 1 : 0;   // half-size textures: much less memory
        QualitySettings.skinWeights = low ? SkinWeights.TwoBones : SkinWeights.FourBones;
        QualitySettings.anisotropicFiltering = s ? AnisotropicFiltering.Disable : AnisotropicFiltering.Enable;
        QualitySettings.lodBias = low ? 0.6f : s ? 0.8f : 1f;
        QualitySettings.particleRaycastBudget = s ? 16 : 256;
        var app = App.I;
        if (app != null && app.cafe != null && app.cafe.cam != null)
        {
            var look = app.cafe.cam.GetComponent<CafeLook>();
            if (look != null) look.enabled = !s;
            app.cafe.cam.allowMSAA = !s;
            app.cafe.Crowd();
        }
        if (watch == null && Application.isMobilePlatform && app != null) watch = app.gameObject.AddComponent<Watch>();
        Debug.Log($"[perf] level {l} (ram {Ram} MB, cores {Cores}, gpu {Gpu} MB)");
    }

    /// How many of the café's regulars to show in the background.
    public static int CrowdSize => Level == 0 ? 0 : Level == 1 ? 2 : 99;

    /// Watches the frame rate; a few seconds well under the target steps the quality down (unless the player chose).
    class Watch : MonoBehaviour
    {
        float t, frames, slowFor;
        void Update()
        {
            if (Chosen || App.Recording) return;
            t += Time.unscaledDeltaTime; frames++;
            if (t < 2f) return;
            float fps = frames / t; t = 0; frames = 0;
            int l = Level;
            float need = l == 2 ? 42f : 22f;   // well under 60, or well under 30
            slowFor = fps < need ? slowFor + 2f : 0f;
            if (slowFor >= 6f && l > 0)
            {
                slowFor = 0;
                SetLevel(l - 1);
                App.I?.Toast(Loc.T("خففنا الرسوم عشان اللعب يبقى أنعم"));
            }
        }
    }
}
