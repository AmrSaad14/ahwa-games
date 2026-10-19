// Kings Battle in Unity: starts by itself (no scene setup needed), shows the splash, builds the café,
// connects to the game server, puts the home menu over the café floor, and runs the domino game.
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
    public DominoTable dominoTable;
    public GameUi game;
    public TableView view;
    Dictionary<string, object> user;
    double serverOffset;            // server clock minus ours (ms)
    int sel = -1; List<char> choose;
    string lastMode = "ffa2"; int lastStake;
    bool searching;

    // the home menu
    Canvas ui, toastCanvas;
    Text nameText, coinsText, statusText, toastText;
    CanvasGroup homeGroup, toastGroup;
    float toastUntil;
    readonly Dictionary<string, Image> gameButtons = new Dictionary<string, Image>();

    public static readonly Dictionary<string, string> GameNames = new Dictionary<string, string>
    {
        ["domino"] = "دومينو", ["tawla"] = "طاولة", ["chess"] = "شطرنج", ["ludo"] = "لودو",
    };

    /// Test mode (GitHub's desktop build, "-shots"): records the run as video frames plus a few stills, then quits.
    public static readonly bool Recording = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-shots") >= 0;
    public const int RecordFps = 12;
    static string ShotDir => Path.Combine(Directory.GetCurrentDirectory(), "shots");
    public static void Still(string name) { Directory.CreateDirectory(ShotDir); ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, name)); }
    int frame; bool recordOn = true;

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
        // recording: game time steps a fixed amount per frame however slow the test machine draws, so the video plays smoothly
        if (Recording) { Time.captureFramerate = RecordFps; Directory.CreateDirectory(Path.Combine(ShotDir, "frames")); }
    }

    void LateUpdate()
    {
        if (Recording && recordOn && frame < RecordFps * 80) ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "frames", $"f{frame++:D4}.png"));
    }

    IEnumerator Start()
    {
        var splash = gameObject.AddComponent<Splash>();
        splash.Show();
        yield return null;   // let the splash draw its first frame before the heavy loading

        cafe = new GameObject("Cafe").AddComponent<Cafe>();
        cafe.Build();
        dominoTable = new GameObject("Domino").AddComponent<DominoTable>();
        dominoTable.Init(cafe);
        dominoTable.Show(false);
        BuildHome();
        game = gameObject.AddComponent<GameUi>();
        game.Build(this);

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

    public static double LocalMs() => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
    public double ServerNow() => LocalMs() + serverOffset;

    void Update()
    {
        net?.Pump();
        if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.time < toastUntil ? 1 : 0, Time.deltaTime * 4);
        if (cafe == null || !cafe.ready || !Input.GetMouseButtonDown(0) || OverUi()) return;
        if (view != null)
        {
            // at the table: a glowing spot picks the end for the chosen tile
            var ray = cafe.cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, 200f))
            {
                var g = hit.collider.GetComponent<GhostEnd>();
                if (g != null) PlayEnd(g.end);
            }
            return;
        }
        // on the café floor, taps pick a table
        var t = cafe.TableAt(Input.mousePosition);
        if (t != null) Pick(t);
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
            case "queue":
                searching = m.TryGetValue("mode", out var md) && md is string;
                game.ShowQueue(searching ? m : null);
                break;
            case "table":
                var vm = m.Obj("view");
                if (vm == null) break;
                var v = TableView.Parse(vm);
                if (v.game != "domino") { Toast("اللعبة دي لسه جاية في نسخة Unity"); net.Send(new Dictionary<string, object> { ["t"] = "leave" }); break; }
                serverOffset = v.now - LocalMs();
                if (view == null) EnterGame();
                // the choice of end only lasts while it's still your turn and the tile is still in hand
                if (!v.MyTurn || !v.hand.Contains(sel)) { sel = -1; choose = null; }
                view = v;
                Redraw();
                break;
            case "left":
                if (view != null && view.phase == "mend") { view = null; break; }   // the result stays up until you choose
                ExitGame();
                break;
            case "payout":
                game.ShowPayout(m.Str("kind"), m.Num("delta"));
                break;
            case "err":
                Toast(ErrorText(m.Str("code")));
                break;
        }
    }

    static string ErrorText(string code)
    {
        switch (code)
        {
            case "not_enough_coins": return "رصيدك مش كفاية للرهان ده";
            case "not_your_turn": return "مش دورك";
            case "illegal_move": return "الحجر ده مش هينفع هنا";
            default: return "خطأ: " + code;
        }
    }

    void EnterGame()
    {
        searching = false;
        game.ShowQueue(null);
        game.HidePicker();
        homeGroup.alpha = 0; homeGroup.blocksRaycasts = false;
        cafe.SetGame(true);
        dominoTable.Show(true);
        game.ShowHud(true);
    }

    public void ExitGame()
    {
        view = null; sel = -1; choose = null;
        game.ShowHud(false);
        dominoTable.Show(false);
        cafe.SetGame(false);
        homeGroup.alpha = 1; homeGroup.blocksRaycasts = true;
    }

    void Redraw()
    {
        if (view == null) return;
        dominoTable.Set(view, sel, choose);
        game.Render(view, sel, choose);
    }

    void Send(Dictionary<string, object> m) => net.Send(m);

    /* ---------- actions ---------- */

    void Pick(string g)
    {
        cafe.Focus(g);
        foreach (var kv in gameButtons) kv.Value.color = kv.Key == g ? Ui.Gold : Ui.Panel;
        foreach (var kv in gameButtons) kv.Value.GetComponentInChildren<Text>().color = kv.Key == g ? Ui.Ink : Ui.GoldLight;
        game.ShowPicker(g, GameNames[g]);
    }

    public void Quick(string mode, int stake)
    {
        if (net == null || !net.Online) { Toast("مش متصل بالسيرفر لسه"); return; }
        if (user != null && user.Num("coins") < stake) { Toast("رصيدك مش كفاية للرهان ده"); return; }
        lastMode = mode; lastStake = stake;
        Send(new Dictionary<string, object> { ["t"] = "quick", ["mode"] = mode, ["stake"] = (double)stake });
        searching = true;
        game.ShowQueue(new Dictionary<string, object> { ["count"] = 1.0, ["need"] = (double)Domino.Players(mode), ["botsAt"] = 0.0, ["now"] = 0.0 });
    }

    public void CancelQueue()
    {
        Send(new Dictionary<string, object> { ["t"] = "cancel" });
        searching = false;
        game.ShowQueue(null);
    }

    public void Leave()
    {
        if (view != null && view.phase != "mend") Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
    }

    public void PlayAgain()
    {
        if (view != null && view.phase == "mend") Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
        Quick(lastMode, lastStake);
    }

    public void TapTile(int id)
    {
        var v = view;
        if (v == null || !v.MyTurn) return;
        if (sel == id) { sel = -1; choose = null; Redraw(); return; }
        if (v.open >= 0 && v.line.Empty && id != v.open) { Toast("افتح بالـ " + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1]); return; }
        var ends = Domino.EndsFor(v.line, id);
        if (ends.Count == 0) { Toast("الحجر ده مش هيركب"); return; }
        if (ends.Count == 1) { Play(id, ends[0]); return; }
        sel = id; choose = ends;
        Redraw();
    }

    public void PlayEnd(char e)
    {
        if (sel < 0 || choose == null || !choose.Contains(e)) return;
        Play(sel, e);
    }

    void Play(int id, char e)
    {
        sel = -1; choose = null;
        var a = new Dictionary<string, object> { ["t"] = "play", ["id"] = (double)id };
        if (e != 'C') a["e"] = e.ToString();
        Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });
        Redraw();
    }

    /* ---------- the home menu ---------- */

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

        // a hint at the bottom
        var hint = Ui.Label(root, "اضغط على أي ترابيزة في القهوة", 13, new Color(1, 1, 1, 0.75f));
        hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 24), new Vector2(0, 12));

        // toasts, over everything
        toastCanvas = Ui.MakeCanvas("Toast", 50);
        var toast = Ui.Box(toastCanvas.transform, "Toast", new Color(0.1f, 0.06f, 0.04f, 0.92f));
        toast.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 40), new Vector2(0, 60));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0; toastGroup.blocksRaycasts = false;
        toastText = Ui.Label(toast.transform, "", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        toastText.rectTransform.Fill(6);
    }

    static IEnumerator FadeIn(CanvasGroup g, float d)
    {
        for (float t = 0; t < d; t += Time.deltaTime) { g.alpha = t / d; yield return null; }
        g.alpha = 1;
    }

    /* ---------- test recording ---------- */

    /// The café, moving in on the tables, then (when the server answers) a real game of dominoes against
    /// the computer, playing the first tile that fits each turn; waits for the server aren't recorded.
    IEnumerator Shots()
    {
        yield return new WaitForSeconds(2.5f);
        Still("3-cafe.png");
        Pick("ludo");
        yield return new WaitForSeconds(2.5f);
        Pick("domino");
        yield return new WaitForSeconds(3f);
        Still("4-picker.png");
        // the free server can take a while to wake up
        recordOn = false;
        float t0 = Time.realtimeSinceStartup;
        while ((net == null || !net.Online) && Time.realtimeSinceStartup - t0 < 120) yield return null;
        Debug.Log("[app] online=" + (net != null && net.Online) + " after " + (Time.realtimeSinceStartup - t0) + " s");
        if (net != null && net.Online)
        {
            Quick("ffa2", Stakes0());
            t0 = Time.realtimeSinceStartup;
            while (view == null && Time.realtimeSinceStartup - t0 < 90) yield return null;
            recordOn = true;
            if (view != null)
            {
                Debug.Log("[app] at the table: " + view.mode + " seats " + view.seats.Count);
                float played = 0;
                int lastV = -1;
                while (played < 40f && view != null)
                {
                    if (view.MyTurn && view.v != lastV)
                    {
                        lastV = view.v;
                        yield return new WaitForSeconds(1.2f);
                        if (view == null || !view.MyTurn) continue;
                        foreach (var id in view.hand)
                        {
                            var ends = Domino.EndsFor(view.line, id);
                            if (view.open >= 0 && view.line.Empty && id != view.open) continue;
                            if (ends.Count == 0) continue;
                            if (ends.Count > 1) { TapTile(id); yield return new WaitForSeconds(1.2f); Still("6-choose.png"); PlayEnd(ends[0]); }
                            else Play(id, ends[0]);
                            break;
                        }
                    }
                    if (played > 8 && played < 8.1f) Still("5-domino.png");
                    played += Time.deltaTime;
                    yield return null;
                }
                Still("7-later.png");
                yield return new WaitForSeconds(0.5f);
            }
        }
        Debug.Log("[app] recording done, frames=" + frame);
        Application.Quit();
    }

    static int Stakes0() => GameUi.Stakes[0];
}
