// Kings Battle in Unity: starts by itself (no scene setup needed), shows the splash, builds the café,
// connects to the game server and puts the home menu over the café floor.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class App : MonoBehaviour
{
    public static App I;
    public Net net;
    public Cafe cafe;
    Dictionary<string, object> user;

    // the home menu
    Canvas ui;
    Text nameText, coinsText, statusText, toastText;
    CanvasGroup homeGroup, toastGroup;
    float toastUntil;
    readonly Dictionary<string, Image> gameButtons = new Dictionary<string, Image>();

    static readonly Dictionary<string, string> GameNames = new Dictionary<string, string>
    {
        ["domino"] = "دومينو", ["tawla"] = "طاولة", ["chess"] = "شطرنج", ["ludo"] = "لودو",
    };

    /// Test mode (GitHub's desktop build, "-shots"): records the run as video frames plus a few stills, then quits.
    public static readonly bool Recording = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-shots") >= 0;
    static string ShotDir => Path.Combine(Directory.GetCurrentDirectory(), "shots");
    public static void Still(string name) { Directory.CreateDirectory(ShotDir); ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, name)); }
    int frame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        var go = new GameObject("Kings Battle");
        DontDestroyOnLoad(go);
        go.AddComponent<App>();
    }

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        QualitySettings.shadowDistance = 60;
        QualitySettings.shadows = ShadowQuality.All;
        // recording: game time steps 1/25 s per frame however slow the test machine draws, so the video plays smoothly
        if (Recording) { Time.captureFramerate = 25; Directory.CreateDirectory(Path.Combine(ShotDir, "frames")); }
    }

    void LateUpdate()
    {
        if (Recording && frame < 25 * 24) ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "frames", $"f{frame++:D4}.png"));
    }

    IEnumerator Start()
    {
        var splash = gameObject.AddComponent<Splash>();
        splash.Show();
        yield return null;   // let the splash draw its first frame before the heavy loading

        cafe = new GameObject("Cafe").AddComponent<Cafe>();
        cafe.Build();
        BuildHome();

        net = new Net();
        net.OnMessage += OnMessage;
        net.OnOnline += on => { statusText.Set(on ? "متصل" : "جاري الاتصال…"); statusText.color = on ? new Color(0.55f, 0.85f, 0.5f) : Ui.GoldLight; };
        net.Start();

        splash.Ready();
        while (!splash.Opened) yield return null;
        cafe.Entrance();
        StartCoroutine(FadeIn(homeGroup, 0.6f));

        if (Recording) StartCoroutine(Shots());
    }

    void Update()
    {
        net?.Pump();
        // taps on the café floor pick a table
        if (cafe != null && cafe.ready && Input.GetMouseButtonDown(0) && !OverUi())
        {
            var g = cafe.TableAt(Input.mousePosition);
            if (g != null) Pick(g);
        }
        if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.time < toastUntil ? 1 : 0, Time.deltaTime * 4);
    }

    static bool OverUi() => UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

    void OnApplicationQuit() { net?.Stop(); }

    void OnMessage(Dictionary<string, object> m)
    {
        switch (m.Str("t"))
        {
            case "welcome":
            case "user":
                user = m.Obj("user");
                if (user != null)
                {
                    nameText.Set(user.Str("name"));
                    coinsText.text = ((long)user.Num("coins")).ToString("N0");
                    Debug.Log($"[app] signed in as {user.Str("name")} with {user.Num("coins")} coins");
                }
                break;
            case "err":
                Toast("خطأ: " + m.Str("code"));
                break;
        }
    }

    void Pick(string g)
    {
        cafe.Focus(g);
        foreach (var kv in gameButtons) kv.Value.color = kv.Key == g ? Ui.Gold : Ui.Panel;
        foreach (var kv in gameButtons) kv.Value.GetComponentInChildren<Text>().color = kv.Key == g ? Ui.Ink : Ui.GoldLight;
        Toast("اللعب على " + GameNames[g] + " قريباً في نسخة Unity");
    }

    public void Toast(string s)
    {
        toastText.Set(s);
        toastUntil = Time.time + 2.6f;
    }

    void BuildHome()
    {
        ui = Ui.MakeCanvas("Home", 10);
        var root = Ui.Rect("Home", ui.transform).Fill();
        homeGroup = root.gameObject.AddComponent<CanvasGroup>();
        homeGroup.alpha = 0;

        // the brand, top left
        var brand = Ui.Label(root, "KINGS BATTLE", 22, Ui.Gold, TextAnchor.MiddleLeft, Ui.Display);
        brand.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(260, 30), new Vector2(18, -10));
        brand.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);

        // you: name and coins, top right
        var me = Ui.Box(root, "Me", Ui.Panel);
        me.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(230, 40), new Vector2(-14, -10));
        nameText = Ui.Label(me.transform, "…", 15, Color.white, TextAnchor.MiddleRight, Ui.Bold);
        nameText.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(120, 30), new Vector2(-12, 0));
        var coin = Ui.Box(me.transform, "Coin", Ui.Gold);
        coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 18), new Vector2(12, 0));
        coinsText = Ui.Label(me.transform, "—", 15, Ui.GoldLight, TextAnchor.MiddleLeft, Ui.Bold);
        coinsText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 30), new Vector2(36, 0));

        // connection
        statusText = Ui.Label(root, "جاري الاتصال…", 12, Ui.GoldLight, TextAnchor.MiddleRight);
        statusText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(200, 20), new Vector2(-16, -54));

        // the four games, down the right side
        var list = Ui.Rect("Games", root).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(170, 232), new Vector2(-14, -18));
        var title = Ui.Label(list, "اختار لعبتك", 15, Ui.GoldLight, TextAnchor.MiddleRight, Ui.Bold);
        title.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(170, 24), new Vector2(0, 0));
        for (int i = 0; i < Cafe.Games.Length; i++)
        {
            var g = Cafe.Games[i];
            var b = Ui.Button(list, GameNames[g], Ui.Panel, Ui.GoldLight, 18, () => Pick(g));
            ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(170, 44), new Vector2(0, -30 - i * 50));
            gameButtons[g] = b.GetComponent<Image>();
            var outline = b.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.35f);
        }

        // a hint at the bottom, and the toast
        var hint = Ui.Label(root, "اضغط على أي ترابيزة في القهوة", 13, new Color(1, 1, 1, 0.75f));
        hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 24), new Vector2(-80, 12));
        var toast = Ui.Box(root, "Toast", new Color(0.1f, 0.06f, 0.04f, 0.92f));
        toast.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(380, 40), new Vector2(-80, 44));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        toastText = Ui.Label(toast.transform, "", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        toastText.rectTransform.Fill(6);
    }

    static IEnumerator FadeIn(CanvasGroup g, float d)
    {
        for (float t = 0; t < d; t += Time.deltaTime) { g.alpha = t / d; yield return null; }
        g.alpha = 1;
    }

    /// Test mode: the café, then moving in on two tables, then quit (the frames make the video).
    IEnumerator Shots()
    {
        yield return new WaitForSeconds(3.5f);
        Still("3-cafe.png");
        yield return new WaitForSeconds(2.5f);
        Pick("domino");
        yield return new WaitForSeconds(3.5f);
        Still("4-domino.png");
        Pick("ludo");
        yield return new WaitForSeconds(3.5f);
        Still("5-ludo.png");
        Pick("tawla");
        yield return new WaitForSeconds(3f);
        Debug.Log("[app] recording done, online=" + (net != null && net.Online) + ", frames=" + frame);
        Application.Quit();
    }
}
