// The forced update. When a newer version of the game is out, players have to update before they play on:
//  - Google Play says so (in-app updates, BBUpdate.java): checked when the game opens, when it comes back to the
//    front and every half hour. This is the usual way: nothing to do when a release goes out.
//  - or the server says so: the dashboard's "Minimum app version" (Game settings), for when an old version must
//    stop at once (the server answers "mustUpdate" to any app below it, and refuses to start its games).
// Either way a dialog covers the game and can't be closed: "Update now" opens Play's own update screen (download,
// install, restart), or the Play Store page when that can't run. A game in progress is never cut off: the dialog
// waits until the player is back in the café.
using UnityEngine;
using UnityEngine.UI;

public class Updater : MonoBehaviour
{
    /// This app's version code (the build number; the version name is "1.0.<build>").
    public static int Build
    {
        get
        {
            if (build >= 0) return build;
            var v = Application.version ?? "";
            int dot = v.LastIndexOf('.');
            return build = int.TryParse(dot >= 0 ? v.Substring(dot + 1) : v, out var n) ? n : 0;
        }
    }
    static int build = -1;

    public static Updater I;
    /// An update is needed (Play has one, or the server asked).
    public static bool Required { get; private set; }
    Canvas dialog;
    Text note;
    float nextCheck = 3f;
    bool flowOpen;

    void Awake() { I = this; }

    void Update()
    {
        if (Application.platform == RuntimePlatform.Android && Time.unscaledTime > nextCheck && !Required)
        {
            nextCheck = Time.unscaledTime + 1800f;
            Call("check");
        }
        if (Required && dialog == null && App.I != null && App.I.view == null && !App.I.Practicing) Show();
    }

    void OnApplicationPause(bool paused)
    {
        // back from Play's update screen or from the background: look again (an update left half-way resumes)
        if (!paused && Application.platform == RuntimePlatform.Android) nextCheck = Time.unscaledTime + 0.5f;
        if (!paused && Required && !flowOpen) Call("check");
    }

    /// From Java: "available:<versionCode>", "none" or "error".
    public void OnUpdateCheck(string value)
    {
        Debug.Log("[update] play: " + value);
        if (value != null && value.StartsWith("available")) Need();
    }

    /// From Java: Play's update screen closed ("ok" while it installs, "cancelled", "failed").
    public void OnUpdateFlow(string value)
    {
        flowOpen = false;
        Debug.Log("[update] flow: " + value);
        if (value == "failed") Call("store");
        else if (value == "cancelled" && note != null) note.Set(Loc.T("لازم تحدّث عشان تكمل لعب"));
    }

    /// The server (or Play) says this version is too old.
    public static void Need()
    {
        if (Required) return;
        Required = true;
        Debug.Log($"[update] required (this build {Build})");
    }

    void Show()
    {
        dialog = Ui.MakeCanvas("Update", 200);
        // a dark cover over everything, catching every tap
        var cover = Ui.Box(dialog.transform, "Cover", new Color(0, 0, 0, 0.82f), false);
        cover.rectTransform.Fill();
        cover.raycastTarget = true;
        var card = Ui.Box(dialog.transform, "Card", Ui.Panel);
        card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420, 230), Vector2.zero);
        var edge = card.gameObject.AddComponent<Outline>(); edge.effectColor = Ui.Gold; edge.effectDistance = new Vector2(1.5f, -1.5f);
        var title = Ui.Label(card.transform, "في تحديث جديد للعبة!", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Display);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 40), new Vector2(0, -22));
        var body = Ui.Label(card.transform, "حدّث اللعبة عشان تكمل لعب", 16, Ui.GoldLight);
        body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 26), new Vector2(0, -70));
        var more = Ui.Label(card.transform, "فيها حاجات جديدة وتصليحات", 13, new Color(1f, 0.91f, 0.69f, 0.75f));
        more.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 22), new Vector2(0, -98));
        note = Ui.Label(card.transform, "", 12, new Color(1f, 0.75f, 0.6f));
        note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(380, 20), new Vector2(0, 76));
        var go = Ui.Button(card.transform, "حدّث دلوقتي", Ui.Gold, Ui.Ink, 18, Go);
        ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(220, 48), new Vector2(0, 22));
        Sfx.Chime();
    }

    void Go()
    {
        if (Application.platform != RuntimePlatform.Android) { Application.OpenURL("https://play.google.com/store/apps/details?id=" + Application.identifier); return; }
        flowOpen = true;
        Call("start");
    }

    static void Call(string method)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var p = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var a = p.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var c = new AndroidJavaClass("com.ahwa.extras.BBUpdate"))
                c.CallStatic(method, a);
        }
        catch (System.Exception e) { Debug.Log("[update] " + method + ": " + e.Message); if (method == "start") Application.OpenURL("market://details?id=" + Application.identifier); }
#endif
    }
}
