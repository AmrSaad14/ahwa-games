// Battery saver (Settings): no shadows, no glow, no edge smoothing, 30 frames a second and a lower resolution, so
// cheaper phones stay smooth and cool (on by itself on phones with 3 GB of memory or less). Off: the full look at 60.
using UnityEngine;

public static class Perf
{
    /// On by default on phones with little memory (3 GB or less), until the player chooses.
    public static bool Saver { get => PlayerPrefs.GetInt("saver", LowEnd ? 1 : 0) == 1; set { PlayerPrefs.SetInt("saver", value ? 1 : 0); PlayerPrefs.Save(); Apply(); } }
    public static bool LowEnd => Application.isMobilePlatform && SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize <= 3072;
    static Vector2Int full;

    public static void Apply()
    {
        bool s = Saver;
        Application.targetFrameRate = s ? 30 : 60;
        // the saver also draws the 3D scene at a lower resolution on phones (the text stays sharp enough at 80%)
        if (Application.isMobilePlatform)
        {
            if (full.x == 0) full = new Vector2Int(Screen.width, Screen.height);
            float k = s ? 0.8f : 1f;
            Screen.SetResolution(Mathf.RoundToInt(full.x * k), Mathf.RoundToInt(full.y * k), true);
        }
        QualitySettings.shadows = s ? ShadowQuality.Disable : ShadowQuality.All;
        QualitySettings.shadowResolution = s ? ShadowResolution.Low : ShadowResolution.High;
        QualitySettings.antiAliasing = s ? 0 : 4;
        var app = App.I;
        if (app != null && app.cafe != null && app.cafe.cam != null)
        {
            var look = app.cafe.cam.GetComponent<CafeLook>();
            if (look != null) look.enabled = !s;
            app.cafe.cam.allowMSAA = !s;
        }
    }
}
