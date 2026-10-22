// Battery saver (Settings): no shadows, no glow, no edge smoothing and 30 frames a second, so cheaper phones
// stay smooth and cool. Off: the full look at 60.
using UnityEngine;

public static class Perf
{
    public static bool Saver { get => PlayerPrefs.GetInt("saver", 0) == 1; set { PlayerPrefs.SetInt("saver", value ? 1 : 0); PlayerPrefs.Save(); Apply(); } }

    public static void Apply()
    {
        bool s = Saver;
        Application.targetFrameRate = s ? 30 : 60;
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
