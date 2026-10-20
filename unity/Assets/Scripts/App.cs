// Board Battles in Unity: starts by itself (no scene setup needed), shows the splash, builds the café,
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
    public Menus menus;
    public BoardGame boardGame;
    public Voice voice;
    public TableView view;
    Dictionary<string, object> user;
    double serverOffset;            // server clock minus ours (ms)
    int sel = -1; List<char> choose;
    string lastMode = "ffa2"; int lastStake;
    bool searching;
    int tilesOnTable;
    string leftTableId = "";
    OfflineGame offline;            // a practice game played on the phone, with no server

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
    // a still is a copy of the video frame it was asked on (Unity takes one screenshot per frame)
    static readonly List<(int frame, string name)> stills = new List<(int, string)>();
    public static void Still(string name) { if (I != null) stills.Add((I.frame, name)); }
    int frame; bool recordOn = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (I != null) return;
        var go = new GameObject("Board Battles");
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
        QualitySettings.shadowResolution = ShadowResolution.High;
        QualitySettings.antiAliasing = 4;            // smooth edges
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        // recording: game time steps a fixed amount per frame however slow the test machine draws, so the video plays smoothly
        if (Recording) { Time.captureFramerate = RecordFps; Directory.CreateDirectory(Path.Combine(ShotDir, "frames")); }
    }

    void LateUpdate()
    {
        if (Recording && recordOn && frame < RecordFps * 330) ScreenCapture.CaptureScreenshot(Path.Combine(ShotDir, "frames", $"f{frame++:D4}.png"));
    }

    IEnumerator Start()
    {
        var splash = gameObject.AddComponent<Splash>();
        splash.Show();
        Ambience.Begin();   // the café's crowd, from the first frame
        yield return null;   // let the splash draw its first frame before the heavy loading

        cafe = new GameObject("Cafe").AddComponent<Cafe>();
        cafe.Build();
        dominoTable = new GameObject("Domino").AddComponent<DominoTable>();
        dominoTable.Init(cafe);
        dominoTable.Show(false);
        boardGame = gameObject.AddComponent<BoardGame>();
        boardGame.Init(this);
        voice = gameObject.AddComponent<Voice>();
        voice.Init(this);
        BuildHome();
        menus = gameObject.AddComponent<Menus>();
        menus.Build(this, homeGroup.GetComponent<RectTransform>());
        Loc.Changed += () => { SetStatus(net != null && net.Online); Redraw(); SendPushToken(); };
        game = gameObject.AddComponent<GameUi>();
        game.Build(this);

        net = new Net();
        net.OnMessage += OnMessage;
        net.OnOnline += SetStatus;
        net.Start();
        Push.Begin();

        splash.Ready();
        while (!splash.Opened) yield return null;
        cafe.Entrance();
        StartCoroutine(FadeIn(homeGroup, 0.6f));

        if (Recording) StartCoroutine(Shots());
    }

    void SetStatus(bool on) { statusText.Set(on ? Loc.T("متصل") : Loc.T("جاري الاتصال…")); statusText.color = on ? new Color(0.55f, 0.85f, 0.5f) : Ui.GoldLight; }

    public static double LocalMs() => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
    public double ServerNow() => LocalMs() + serverOffset;

    void Update()
    {
        net?.Pump();
        PumpOffline();
        if (Input.GetKeyDown(KeyCode.Escape) && menus != null && menus.IsOpen) { menus.Close(); return; }   // Android's back button
        if (menus != null && menus.IsOpen) return;
        if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.time < toastUntil ? 1 : 0, Time.deltaTime * 4);
        if (cafe == null || !cafe.ready || !Input.GetMouseButtonDown(0) || OverUi()) return;
        if (view != null)
        {
            // chess, tawla, ludo: the board works out what you tapped
            if (view.Board) { boardGame.Tap(Input.mousePosition); return; }
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

    /* ---------- push reminders (Push.cs) ---------- */

    // called from Java (BBPush / BBMessagingService) on this object, "Board Battles"
    public void OnPushToken(string token) { Push.Token = token; Debug.Log("[push] token ready"); SendPushToken(); }
    public void OnPushError(string e) { Debug.Log("[push] " + e); }
    string sentPushToken;
    void SendPushToken()
    {
        if (string.IsNullOrEmpty(Push.Token) || net == null || !net.Online || user == null) return;
        string key = Push.Token + (Loc.En ? "en" : "ar");
        if (key == sentPushToken) return;
        sentPushToken = key;
        net.Send(new Dictionary<string, object> { ["t"] = "pushToken", ["token"] = Push.Token, ["lang"] = Loc.En ? "en" : "ar", ["platform"] = "android" });
    }

    void OnMessage(Dictionary<string, object> m)
    {
        menus?.OnMessage(m);
        switch (m.Str("t"))
        {
            case "welcome":
                voice.ice.Clear();
                foreach (var o in m.Arr("ice") ?? new List<object>()) if (o is Dictionary<string, object> d) voice.ice.Add(d);
                sentPushToken = null;   // (a new connection, maybe a different account)
                goto case "user";
            case "user":
                user = m.Obj("user");
                SendPushToken();
                if (user != null)
                {
                    nameText.Set(user.Str("name"));
                    coinsText.text = ((long)user.Num("coins")).ToString("N0");
                    menus?.OnUser(user);
                    Debug.Log($"[app] signed in as {user.Str("name")} with {user.Num("coins")} coins");
                }
                break;
            case "rtc":
                voice.OnSignal((int)m.Num("from", -1), m.Obj("data"));
                break;
            case "queue":
                searching = m.TryGetValue("mode", out var md) && md is string;
                game.ShowQueue(searching ? m : null);
                break;
            case "table":
                var vm = m.Obj("view");
                if (vm == null) break;
                var v = TableView.Parse(vm);
                if (v.id == leftTableId) break;   // a late message from a table you just left: it doesn't pull you back in
                if (view == null || view.id != v.id) Debug.Log($"[app] table {v.game} {v.mode} {v.phase} {v.id.Substring(0, Mathf.Min(6, v.id.Length))} (was {(view == null ? "none" : view.game + " " + view.id.Substring(0, Mathf.Min(6, view.id.Length)))})");
                serverOffset = v.now - LocalMs();
                if (view == null) EnterGame();
                // the choice of end only lasts while it's still your turn and the tile is still in hand
                if (!v.MyTurn || !v.hand.Contains(sel)) { sel = -1; choose = null; }
                // a tile going down clacks; your turn chimes (and buzzes the phone)
                int tilesNow = v.line.Empty ? 0 : 1 + v.line.L.Count + v.line.R.Count;
                if (view != null && tilesNow > tilesOnTable) Sfx.Clack();
                if (v.MyTurn && v.phase == "play" && (view == null || !view.MyTurn)) { Sfx.Chime(); Sfx.Buzz(); }
                tilesOnTable = tilesNow;
                view = v;
                Redraw();
                break;
            case "left":
                Debug.Log("[app] left the table" + (view != null ? " (" + view.game + " " + view.phase + ")" : ""));
                if (view != null && view.phase == "mend") { view = null; break; }   // the result stays up until you choose
                ExitGame();
                break;
            case "payout":
                game.ShowPayout(m.Str("kind"), m.Num("delta"));
                break;
            case "err":
                if (menus != null && menus.OnError(m.Str("code"))) break;
                Toast(ErrorText(m.Str("code")));
                break;
        }
    }

    static string ErrorText(string code)
    {
        switch (code)
        {
            case "not_enough_coins": return Loc.T("رصيدك مش كفاية للرهان ده");
            case "not_your_turn": return Loc.T("مش دورك");
            case "room_not_found": return Loc.T("مفيش غرفة بالكود ده");
            case "seats_empty": return Loc.T("لسه في أماكن فاضية");
            case "room_full": return Loc.T("الغرفة مليانة");
            case "player_short": return Loc.T("حد من اللاعبين رصيده مش كفاية");
            case "must_roll": return Loc.T("ارمي الزهر الأول");
            case "illegal_move": return Loc.T("الحجر ده مش هينفع هنا");
            default: return Loc.T("خطأ: ") + code;
        }
    }

    void EnterGame()
    {
        searching = false;
        game.ShowQueue(null);
        game.HidePicker();
        homeGroup.alpha = 0; homeGroup.blocksRaycasts = false;
        cafe.SetGame(true);
        Ambience.Home(false);
        dominoTable.Show(true);
        game.ShowHud(true);
    }

    public void ExitGame()
    {
        offline = null;
        view = null; sel = -1; choose = null;
        game.ShowHud(false);
        dominoTable.Show(false);
        boardGame.Show(false);
        voice.Leave(); voice.Sync(null);
        cafe.SetGame(false);
        Ambience.Home(true);
        homeGroup.alpha = 1; homeGroup.blocksRaycasts = true;
    }

    void Redraw()
    {
        if (view == null) return;
        voice.Sync(view);
        if (view.Board) { dominoTable.Set(view, -1, null); boardGame.Set(view); game.Render(view, -1, null); return; }
        dominoTable.Set(view, sel, choose);
        game.Render(view, sel, choose);
    }

    public void Send(Dictionary<string, object> m)
    {
        var t = m.Str("t");
        if (offline != null && SendOffline(t, m)) return;
        if (t != "act" && t != "ping") Debug.Log("[app] send " + t + (m.TryGetValue("mode", out var md) ? " " + md : ""));
        net?.Send(m);
    }

    /* ---------- actions ---------- */

    void Pick(string g)
    {
        cafe.Focus(g);
        foreach (var kv in gameButtons) kv.Value.color = kv.Key == g ? Ui.Gold : Ui.Panel;
        foreach (var kv in gameButtons) kv.Value.GetComponentInChildren<Text>().color = kv.Key == g ? Ui.Ink : Ui.GoldLight;
        game.ShowPicker(g, Loc.T(GameNames[g]));
    }

    public void Quick(string mode, int stake)
    {
        if (offline != null) { offline = null; if (view != null) ExitGame(); }
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (user != null && user.Num("coins") < stake) { Toast(Loc.T("رصيدك مش كفاية للرهان ده")); return; }
        lastMode = mode; lastStake = stake;
        Send(new Dictionary<string, object> { ["t"] = "quick", ["mode"] = mode, ["stake"] = (double)stake });
        searching = true;
        game.ShowQueue(new Dictionary<string, object> { ["count"] = 1.0, ["need"] = (double)GameUi.Players(mode), ["botsAt"] = 0.0, ["now"] = 0.0 });
    }

    /// A free game against the computer, starting at once (no stake, no rating). It runs on the phone itself,
    /// so it works without the server (Offline/*.cs).
    public void Practice(string mode)
    {
        lastMode = mode; lastStake = 0;
        if (view != null) Leave();
        if (searching) CancelQueue();
        string name = user != null ? user.Str("name") : "";
        int ch = user != null ? (int)user.Num("ch", 0) : 0;
        try { offline = OfflineGame.Create(mode, name, ch); }
        catch (System.Exception e) { Debug.LogException(e); Toast(Loc.T("خطأ: ") + e.Message); offline = null; return; }
        Debug.Log("[app] practice " + mode + " on the phone");
        PumpOffline();
    }

    /// Runs the practice game's timers and computer players, and shows its table when it changed.
    void PumpOffline()
    {
        if (offline == null) return;
        try
        {
            offline.Tick();
            if (!offline.dirty) return;
            offline.dirty = false;
            // through JSON, so the view has exactly the shape (and number types) of a server message
            var vm = Json.Parse(Json.Write(offline.View())) as Dictionary<string, object>;
            OnMessage(new Dictionary<string, object> { ["t"] = "table", ["view"] = vm });
        }
        catch (System.Exception e) { Debug.LogException(e); }
    }

    /// Messages for the practice game stay on the phone; the rest (shop, leaderboard, account) go to the server.
    bool SendOffline(string t, Dictionary<string, object> m)
    {
        switch (t)
        {
            case "act":
                string err = null;
                try { err = offline.Act(m.Obj("a")); }
                catch (System.Exception e) { Debug.LogException(e); err = "bad_action"; }
                if (err != null) { Debug.Log("[app] practice move refused: " + err); OnMessage(new Dictionary<string, object> { ["t"] = "err", ["code"] = err }); }
                PumpOffline();
                return true;
            case "leave":
                offline = null;
                return true;
            case "voice": case "rtc": case "emote": case "start": case "bot": case "kick": case "setup": case "cancel":
                return true;
        }
        return false;
    }

    /// A room for friends: you get a code to send them; 0 is a friendly game with no stake.
    public void CreateRoom(string mode, int stake)
    {
        if (offline != null) { offline = null; if (view != null) ExitGame(); }
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (stake > 0 && user != null && user.Num("coins") < stake) { Toast(Loc.T("رصيدك مش كفاية للرهان ده")); return; }
        Send(new Dictionary<string, object> { ["t"] = "create", ["mode"] = mode, ["target"] = 0.0, ["stake"] = (double)stake });
    }
    public void JoinRoom(string code)
    {
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        Send(new Dictionary<string, object> { ["t"] = "join", ["code"] = code });
    }
    public void AddBot(int seat) => Send(new Dictionary<string, object> { ["t"] = "bot", ["seat"] = (double)seat });
    public void StartRoom() => Send(new Dictionary<string, object> { ["t"] = "start" });

    public void CancelQueue()
    {
        Send(new Dictionary<string, object> { ["t"] = "cancel" });
        searching = false;
        game.ShowQueue(null);
    }

    public void Leave()
    {
        if (view != null) leftTableId = view.id;
        if (view != null && view.phase != "mend") Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
    }

    public void PlayAgain()
    {
        if (view != null && view.phase == "mend") Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
        if (lastStake == 0) Practice(lastMode); else Quick(lastMode, lastStake);
    }

    public void TapTile(int id)
    {
        var v = view;
        if (v == null || !v.MyTurn) return;
        if (sel == id) { sel = -1; choose = null; Redraw(); return; }
        if (v.open >= 0 && v.line.Empty && id != v.open) { Toast(Loc.T("افتح بالـ ") + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1]); return; }
        var ends = Domino.EndsFor(v.line, id);
        if (ends.Count == 0) { Toast(Loc.T("الحجر ده مش هيركب")); return; }
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

        // the logo, top left
        var logo = Ui.Picture(root, "Logo", Resources.Load<Texture2D>("UI/Logo"));
        logo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, 77), new Vector2(8, -4));

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
        // close-ups of the regulars
        int n = 0;
        foreach (var p in cafe.Regulars())
        {
            if (p.Head == null) continue;
            var face = p.Head.position + Vector3.up * 0.55f;
            var fwd = p.Forward;
            var f2 = (face - p.transform.position); f2.y = 0;
            cafe.Shot(face + fwd * 3.2f + Vector3.up * 0.35f + Vector3.Cross(Vector3.up, fwd) * 0.8f, face);
            yield return new WaitForSeconds(0.25f);
            Still($"p{n++}-{p.name}.png");
            yield return new WaitForSeconds(0.8f);
        }
        // and two of them from further back, the whole body
        foreach (var p in cafe.Regulars())
        {
            var mid = p.transform.position + Vector3.up * 4.2f;
            var pf = p.Forward; var pr = Vector3.Cross(Vector3.up, pf);
            cafe.Shot(mid + (pr * 0.6f + Vector3.up * 0.15f + pf).normalized * 9.5f, mid);
            yield return new WaitForSeconds(0.25f);
            Still($"b-{p.name}.png");
            yield return new WaitForSeconds(0.2f);
        }
        cafe.Shot(null);
        yield return new WaitForSeconds(1.5f);
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
            // the menus, in Arabic and then in English
            recordOn = true;
            Pick("domino"); game.HidePicker();
            yield return new WaitForSeconds(0.6f);
            Still("m0-home.png");
            menus.OpenSettings(); yield return new WaitForSeconds(0.8f); Still("m1-settings.png");
            menus.OpenShop(0); yield return new WaitForSeconds(0.8f); Still("m2-shop-coins.png");
            menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("m3-shop-tiles.png");
            menus.OpenLeaders(0); yield return new WaitForSeconds(2.5f); Still("m4-leaders.png");
            menus.OpenHowTo(0); yield return new WaitForSeconds(0.8f); Still("m5-howto.png");
            menus.Close();
            menus.WatchAd(); yield return new WaitForSeconds(1.5f); Still("m6-ad.png");
            yield return new WaitForSeconds(5f); Still("m7-ad-reward.png");
            menus.OpenCashout(); yield return new WaitForSeconds(1.2f); Still("m12-cashout.png"); menus.Close();
            Loc.Set(true); yield return new WaitForSeconds(0.8f); Still("m8-home-en.png");
            menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("m9-shop-en.png");
            menus.OpenHowTo(0); yield return new WaitForSeconds(0.8f); Still("m10-howto-en.png");
            menus.OpenSettings(); yield return new WaitForSeconds(0.8f); Still("m11-settings-en.png");
            menus.Close(); Loc.Set(false);
            recordOn = false;
            Practice("team");      // practice runs on the phone: no server needed
            t0 = Time.realtimeSinceStartup;
            while (view == null && Time.realtimeSinceStartup - t0 < 90) yield return null;
            recordOn = true;
            if (view != null)
            {
                Debug.Log("[app] at the table: " + view.mode + " seats " + view.seats.Count);
                float played = 0;
                int lastV = -1;
                while (played < 22f && view != null)
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
            // the board games, each a practice game against the computer, played by the test for a while
            foreach (var (mode, tag) in new[] { ("tawla", "tawla"), ("chess", "chess"), ("ludo4", "ludo"), ("mahbousa", "mahbousa") })
            {
                recordOn = false;
                if (view != null) Leave();
                yield return new WaitForSeconds(0.5f);
                Practice(mode);
                float tw = Time.realtimeSinceStartup;
                string want = tag == "mahbousa" ? "tawla" : tag;
                while ((view == null || view.game != want) && Time.realtimeSinceStartup - tw < 30) yield return null;
                if (view == null || view.game != want) { Debug.Log("[app] no " + tag + " game (have " + (view == null ? "none" : view.game) + ")"); continue; }
                recordOn = true;
                Debug.Log("[app] playing " + tag);
                yield return new WaitForSeconds(1.2f);
                Still($"g-{tag}-1.png");
                float playedB = 0; int shotN = 2;
                while (playedB < 16f && view != null)
                {
                    if (view.MyTurn) { yield return new WaitForSeconds(0.7f); boardGame.AutoMove(); yield return new WaitForSeconds(0.5f); }
                    if (playedB > 5f * (shotN - 1) && shotN <= 3) { Still($"g-{tag}-{shotN}.png"); shotN++; }
                    playedB += Time.deltaTime;
                    yield return null;
                }
            }
            // a room for friends: the lobby with its code
            recordOn = false;
            if (view != null) Leave();
            yield return new WaitForSeconds(0.5f);
            CreateRoom("ffa4", 0);
            float tl = Time.realtimeSinceStartup;
            while (view == null && Time.realtimeSinceStartup - tl < 20) yield return null;
            recordOn = true;
            yield return new WaitForSeconds(1.0f);
            Still("r-lobby.png");
            if (view != null) { AddBot(1); yield return new WaitForSeconds(0.6f); Still("r-lobby-bot.png"); }
            yield return new WaitForSeconds(0.4f);
        }
        Debug.Log("[app] recording done, frames=" + frame);
        yield return null; yield return null;
        foreach (var (f, name) in stills)
        {
            var src = Path.Combine(ShotDir, "frames", $"f{Mathf.Min(f, frame - 1):D4}.png");
            try { File.Copy(src, Path.Combine(ShotDir, name), true); } catch (System.Exception e) { Debug.Log("[app] still " + name + ": " + e.Message); }
        }
        Application.Quit();
    }

    static int Stakes0() => GameUi.Stakes[0];
}
