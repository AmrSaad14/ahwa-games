// The look and the smoothness, fitted to each phone by itself. Four levels:
//   3 high      60 fps · soft shadows (high resolution) · bloom, tone and warm grade · 4× edge smoothing ·
//               reflections of the café on glossy pieces · drawn up to 1080 lines · the whole café of people
//   2 balanced  60 fps · sharp-edged shadows · 2× edge smoothing · small reflections · up to 900 lines ·
//               no bloom (and no HDR buffer) · the whole café
//   1 saver     30 fps · no shadows, smoothing, reflections or piece outlines · up to 720 lines · two people
//   0 lowest    the saver, plus up to 600 lines, half-size textures, two-bone skinning, one light at a time,
//               no piece outlines, and nobody in the café's background
// The phone picks its starting level from what it has (memory, cores, which graphics chip and how new it is);
// then a watch keeps an eye on the frame rate: a few seconds well under it steps down, a long smooth stretch steps
// back up (never above what the phone started with, and not straight after a step down). The player can choose
// a level in Settings instead ("Auto" gives it back to the phone); the old "battery saver" switch maps to saver.
using UnityEngine;

public static class Perf
{
    public const int Lowest0 = 0, Saver1 = 1, Balanced2 = 2, High3 = 3;

    /// The saver (or the lowest level) is on.
    public static bool Saver { get => Level < Balanced2; set { Choose(value ? Mathf.Min(Detected, Saver1) : -1); } }
    /// The lowest level: the cheapest phones.
    public static bool Lowest => Level == Lowest0;

    static int Ram => SystemInfo.systemMemorySize;
    static int Cores => SystemInfo.processorCount;
    static int Vram => SystemInfo.graphicsMemorySize;
    static string GpuName => (SystemInfo.graphicsDeviceName ?? "").ToLowerInvariant();

    /// What this phone can do, from 0 to 3 (worked out once).
    public static int Detected
    {
        get
        {
            if (detected >= 0) return detected;
            if (!Application.isMobilePlatform) return detected = High3;
            int gpu = GpuTier();                     // -1 unknown, 0 weak … 3 strong
            int mem = Ram <= 0 ? 1 : Ram <= 2560 ? 0 : Ram <= 3584 ? 1 : Ram <= 6144 ? 2 : 3;
            int cpu = Cores <= 0 ? 2 : Cores <= 4 ? 0 : Cores <= 6 ? 1 : 3;
            if (SystemInfo.graphicsShaderLevel < 35 || SystemInfo.maxTextureSize < 4096) gpu = 0;
            int level = gpu < 0 ? Mathf.Min(mem, cpu, 2) : Mathf.Min(gpu, mem, cpu == 0 ? 1 : 3);
            // a big screen on a middling chip: one step lower
            if (level == High3 && Mathf.Max(Screen.width, Screen.height) > 2600 && gpu < 3) level = Balanced2;
            return detected = Mathf.Clamp(level, Lowest0, High3);
        }
    }
    static int detected = -1;

    /// The graphics chip's class from its name: Adreno by number, Mali by family, Apple, PowerVR and the rest.
    static int GpuTier()
    {
        string g = GpuName;
        if (g.Contains("apple")) return High3;
        if (g.Contains("adreno"))
        {
            int n = FirstNumber(g);
            if (n >= 730 || (n >= 640 && n < 700)) return High3;   // 640/650/660, 730+
            if (n >= 610) return n >= 618 ? Balanced2 : Saver1;   // 618–630 and the 7xx below 730: balanced
            if (n >= 506) return Saver1;
            return Lowest0;
        }
        if (g.Contains("mali"))
        {
            if (g.Contains("immortalis") || g.Contains("g720") || g.Contains("g715") || g.Contains("g710") || g.Contains("g925")) return High3;
            if (g.Contains("g78") || g.Contains("g77") || g.Contains("g76")) return High3;
            if (g.Contains("g68") || g.Contains("g610") || g.Contains("g615") || g.Contains("g57") || g.Contains("g72")) return Balanced2;
            if (g.Contains("g52") || g.Contains("g51") || g.Contains("g71")) return Saver1;
            if (g.Contains("t8") || g.Contains("t7") || g.Contains("g31") || g.Contains("400") || g.Contains("450")) return Lowest0;
            return Saver1;
        }
        if (g.Contains("xclipse")) return High3;
        if (g.Contains("powervr")) return g.Contains("bxm") ? Saver1 : Lowest0;
        if (g.Contains("nvidia") || g.Contains("geforce") || g.Contains("radeon") || g.Contains("intel")) return High3;
        return -1;
    }
    static int FirstNumber(string s)
    {
        int i = 0; while (i < s.Length && !char.IsDigit(s[i])) i++;
        int n = 0; while (i < s.Length && char.IsDigit(s[i])) { n = n * 10 + (s[i] - '0'); i++; }
        return n;
    }

    /// The level now: the player's choice, else where the watch has it (starting from what the phone can do).
    public static int Level
    {
        get
        {
            int chosen = Chosen;
            if (chosen >= 0) return chosen;
            if (auto < 0)
            {
                // the step the watch reached last time (so a phone that needed fewer frames doesn't start too high)
                int saved = PlayerPrefs.GetInt("perf.auto", -1);
                auto = saved >= 0 ? Mathf.Min(saved, Detected) : Detected;
            }
            return auto;
        }
    }
    static int auto = -1;
    /// The level the player picked in Settings, or -1 for automatic.
    public static int Chosen
    {
        get
        {
            if (PlayerPrefs.HasKey("perf.pick")) return Mathf.Clamp(PlayerPrefs.GetInt("perf.pick"), -1, High3);
            // older installs: "battery saver" on carries over as the saver
            if (PlayerPrefs.GetInt("perf.chosen", 0) == 1 && PlayerPrefs.GetInt("perf.level", 2) < 2) return Mathf.Min(Detected, Saver1);
            return -1;
        }
    }
    /// The player picks a level (-1: automatic).
    public static void Choose(int level)
    {
        PlayerPrefs.SetInt("perf.pick", Mathf.Clamp(level, -1, High3)); PlayerPrefs.Save();
        Apply();
    }
    static void StepTo(int level)
    {
        auto = Mathf.Clamp(level, Lowest0, Detected);
        PlayerPrefs.SetInt("perf.auto", auto); PlayerPrefs.Save();
        Apply();
    }

    static Vector2Int full;
    static Watch watch;

    /// The most lines the 3D is drawn with (the screen's short side): sharp enough, much cheaper on big screens.
    static int MaxLines(int l) => l == High3 ? 1080 : l == Balanced2 ? 900 : l == Saver1 ? 720 : 600;

    public static void Apply()
    {
        int l = Level;
        bool high = l == High3, bal = l == Balanced2, s = l <= Saver1, low = l == Lowest0;
        Application.targetFrameRate = s ? 30 : 60;
        QualitySettings.vSyncCount = 0;
        if (Application.isMobilePlatform)
        {
            if (full.x == 0) full = new Vector2Int(Screen.width, Screen.height);
            int shortSide = Mathf.Min(full.x, full.y);
            float k = Mathf.Min(1f, MaxLines(l) / (float)Mathf.Max(1, shortSide));
            Screen.SetResolution(Mathf.RoundToInt(full.x * k), Mathf.RoundToInt(full.y * k), true);
        }
        QualitySettings.shadows = high ? ShadowQuality.All : bal ? ShadowQuality.HardOnly : ShadowQuality.Disable;
        QualitySettings.shadowResolution = high ? ShadowResolution.High : ShadowResolution.Medium;
        QualitySettings.shadowCascades = 1;                         // one map, kept tight to what the camera sees
        QualitySettings.shadowProjection = ShadowProjection.StableFit;
        QualitySettings.antiAliasing = high ? 4 : bal ? 2 : 0;
        QualitySettings.pixelLightCount = high ? 4 : bal ? 2 : 1;
        QualitySettings.globalTextureMipmapLimit = low ? 1 : 0;    // half-size textures: much less memory
        QualitySettings.skinWeights = high ? SkinWeights.FourBones : SkinWeights.TwoBones;
        QualitySettings.anisotropicFiltering = high ? AnisotropicFiltering.ForceEnable : bal ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;
        QualitySettings.lodBias = low ? 0.6f : s ? 0.8f : 1f;
        QualitySettings.particleRaycastBudget = s ? 16 : 256;
        QualitySettings.realtimeReflectionProbes = !s;
        QualitySettings.softParticles = high;
        // textures and meshes reach the graphics chip a little each frame (no hitch when a board or a menu opens)
        QualitySettings.asyncUploadTimeSlice = 4;
        QualitySettings.asyncUploadBufferSize = 16;
        QualitySettings.asyncUploadPersistentBuffer = true;
        var app = App.I;
        if (app != null && app.cafe != null && app.cafe.cam != null)
        {
            var cam = app.cafe.cam;
            var look = cam.GetComponent<CafeLook>();
            if (look != null) look.enabled = high;
            cam.allowHDR = high && look != null && look.isActiveAndEnabled;   // an HDR buffer only when bloom uses it
            cam.allowMSAA = !s;
            cam.useOcclusionCulling = false;
            app.cafe.Crowd();
            app.cafe.Reflections(high ? 128 : bal ? 64 : 0);
        }
        if (watch == null && Application.isMobilePlatform && app != null) watch = app.gameObject.AddComponent<Watch>();
        Debug.Log($"[perf] level {l} (phone {Detected}; ram {Ram} MB, cores {Cores}, gpu '{SystemInfo.graphicsDeviceName}' {Vram} MB, {SystemInfo.graphicsDeviceType})");
    }

    /// How many of the café's regulars to show in the background.
    public static int CrowdSize => Level == Lowest0 ? 0 : Level == Saver1 ? 2 : 99;
    /// How finely painted boards are drawn (1 full, less on cheap phones: less memory and a quicker start).
    public static float PaintScale => Level == Lowest0 ? 0.6f : Level == Saver1 ? 0.8f : 1f;

    /// Watches the frame rate. Steps down after a few seconds well under the target; steps back up after a long
    /// smooth stretch (not above what the phone started with, and never within two minutes of a step down).
    class Watch : MonoBehaviour
    {
        float t, frames, slowFor, smoothFor, lastDown = -999f;
        int ups;
        void Update()
        {
            if (Chosen >= 0 || App.Recording) { slowFor = smoothFor = 0; return; }
            t += Time.unscaledDeltaTime; frames++;
            if (t < 2f) return;
            float fps = frames / t; t = 0; frames = 0;
            int l = Level;
            float target = l >= Balanced2 ? 60f : 30f;
            if (fps < target * 0.8f) { slowFor += 2f; smoothFor = 0; }   // an uneven 40-something stutters: a steady 30 plays smoother
            else if (fps > target * 0.95f) { smoothFor += 2f; slowFor = 0; }
            else { slowFor = 0; smoothFor = 0; }
            if (slowFor >= 6f && l > Lowest0)
            {
                slowFor = 0; lastDown = Time.unscaledTime;
                StepTo(l - 1);
                if (l - 1 == Saver1) App.I?.Toast(Loc.T("خففنا الرسوم عشان اللعب يبقى أنعم"));
            }
            else if (smoothFor >= 45f && l < Detected && ups < 2 && Time.unscaledTime - lastDown > 120f)
            {
                // a saver phone running at a steady 30 can't tell whether 60 would hold: try it, the watch steps back if not
                smoothFor = 0; ups++;
                StepTo(l + 1);
            }
        }
    }
}
