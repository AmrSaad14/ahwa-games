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
    public EstimationTable estimationTable;
    /// The card game (estimation): drawn flat over the table, its computer players on the server.
    public static bool CardGame(string g) => g == "estimation";
    public Voice voice;
    public TableView view;
    Dictionary<string, object> user;
    double serverOffset;            // server clock minus ours (ms)
    int sel = -1; List<char> choose;
    string lastMode = "ffa2"; int lastStake;
    bool searching;
    int tilesOnTable;
    string leftTableId = "";
    OfflineGame offline;
    string reportedPractice = "";
    bool boardDragging, boardPinched; Vector2 boardDragFrom;            // a practice game played on the phone, with no server

    // the home menu
    Canvas ui, toastCanvas;
    Text nameText, coinsText, statusText, toastText, levelText;
    GameObject levelBox;
    RectTransform playNow;
    CanvasGroup homeGroup, toastGroup;
    float toastUntil;
    readonly Dictionary<string, Image> gameButtons = new Dictionary<string, Image>();

    /// The games on the home list: the café's five tables.
    public static readonly string[] HomeGames = { "domino", "tawla", "chess", "ludo", "estimation" };
    public static readonly Dictionary<string, string> GameNames = new Dictionary<string, string>
    {
        ["domino"] = "دومينو", ["tawla"] = "طاولة", ["chess"] = "شطرنج", ["ludo"] = "لودو", ["estimation"] = "استيميشن",
    };

    /// A game's name (older games, no longer offered, by their own name).
    public static string GameName(string g) => g != null && GameNames.TryGetValue(g, out var n) ? n : g == "basra" ? "بصرة" : g == "tarneeb" ? "طرنيب" : g == "konkan" ? "كونكان" : g ?? "";

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
        CrashReport.Begin();   // (did the game close by itself last time?)
        gameObject.AddComponent<Updater>();   // the forced update (Play's in-app update, or the server's minimum)
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        // either way round in landscape: the game turns with the phone (camera on the left or the right)
        Screen.autorotateToLandscapeLeft = true; Screen.autorotateToLandscapeRight = true;
        Screen.autorotateToPortrait = false; Screen.autorotateToPortraitUpsideDown = false;
        Screen.orientation = ScreenOrientation.AutoRotation;
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
        yield return null;   // let the splash draw its first frame before the heavy loading

        float tBuild = Time.realtimeSinceStartup;
        // load the big pieces (the room, the table, the six regulars) in the background while the splash plays;
        // once loaded, the building below finds them at once instead of freezing the screen
        var loads = new List<ResourceRequest> { Resources.LoadAsync<GameObject>("Art/cafe"), Resources.LoadAsync<GameObject>("Art/MainTable") };
        if (!Perf.Lowest) foreach (var n in DominoTable.CharNames) loads.Add(Resources.LoadAsync<GameObject>("People/" + n));   // the cheapest phones skip them
        var kept = new List<Object>();
        foreach (var r in loads) { while (!r.isDone) { splash.Progress(0.75f * Loaded(loads)); yield return null; } kept.Add(r.asset); }
        Debug.Log($"[app] assets loaded in {Time.realtimeSinceStartup - tBuild:0.0} s");
        cafe = new GameObject("Cafe").AddComponent<Cafe>();
        splash.Progress(0.8f);
        yield return cafe.BuildSteps();
        splash.Progress(0.95f);
        Debug.Log($"[app] café built in {Time.realtimeSinceStartup - tBuild:0.0} s");
        Perf.Apply();
        Music.Begin();
        yield return null;
        dominoTable = new GameObject("Domino").AddComponent<DominoTable>();
        dominoTable.Init(cafe);
        dominoTable.Show(false);
        boardGame = gameObject.AddComponent<BoardGame>();
        boardGame.Init(this);
        estimationTable = gameObject.AddComponent<EstimationTable>();
        estimationTable.Init(this);
        voice = gameObject.AddComponent<Voice>();
        voice.Init(this);
        BuildHome();
        menus = gameObject.AddComponent<Menus>();
        menus.Build(this, homeGroup.GetComponent<RectTransform>());
        Loc.Changed += () => { SetStatus(net != null && net.Online); Redraw(); SendPushToken(); myTitleId = null; ShowMyTitle(); };
        game = gameObject.AddComponent<GameUi>();
        game.Build(this);

        FreshInstall = string.IsNullOrEmpty(PlayerPrefs.GetString("ahwa.token", ""));   // no saved sign-in on this phone
        net = new Net();
        net.OnMessage += OnMessage;
        net.OnOnline += SetStatus;
        net.OnReplaced += () => Toast(Loc.T("حسابك اتفتح على موبايل تاني"), 6f);
        if (!AdFilm.Any)   // filming the advert: no server, no ads, nothing popping up over the café
        {
            net.Start();
            Push.Begin();
            Ads.Begin();
            Growth.CheckReferrer();
        }

        splash.Ready();
        while (!splash.Opened) yield return null;
        cafe.Entrance();
        if (AdFilm.On) { gameObject.AddComponent<AdFilm>().Begin(this); yield break; }
        if (AdFilm.Salon) { gameObject.AddComponent<AdSalon>().Begin(this); yield break; }
        StartCoroutine(FadeIn(homeGroup, 0.6f));

        if (Recording) StartCoroutine(Shots());
    }

    /// A practice game against the computer is running on the phone.
    public bool Practicing => offline != null;
    /// A practice game against the computer (on the phone, or the card game's on the server): no drinks, no stakes.
    public bool PracticeGame => offline != null || serverPractice;
    bool serverPractice;
    /// The first-game guide: an easy practice game of dominoes with hints, for a new player (its reward once).
    bool guided; int guideHints;

    /// The first practice game of each game teaches it, a step on each of your first turns.
    static readonly Dictionary<string, string[]> Guides = new Dictionary<string, string[]>
    {
        ["domino"] = new[] { "دورك! دوس على حجر بينوّر عشان تلعبه", "الحجر لازم رقمه يكون زي رقم طرف من الطرفين", "مفيش حجر يركب؟ اسحب من الكومة", "اللي يخلّص حجارته الأول يكسب نقط الباقيين" },
        ["tawla"] = new[] { "دورك! دوس على الزهر عشان ترمي", "دوس على قشاط بينوّر، وبعدين على المكان اللي عايز تروحه", "قشاط لوحده ممكن يتضرب: خلّي قشاطين مع بعض عشان تحميهم", "لما تجمّع قشاطك كله في آخر ربع، طلّعه برة. اللي يطلّع الأول يكسب" },
        ["chess"] = new[] { "دورك! دوس على قطعة من قطعك عشان تشوف هتروح فين", "دوس على مربع من اللي بينوّروا عشان تحرّك القطعة", "خلّي بالك على الملك: لو اتحاصر من غير مهرب تبقى كش مات", "كل لاعب ليه ساعة: لو وقتك خلص تخسر" },
        ["estimation"] = new[] { "اطلب كام لمّة هتاخدها وإيه الطرنيب (♣ ♦ ♥ ♠ صن)، أو قول باص", "اللي طلب أعلى بيبدأ، والباقيين يقولوا كل واحد هياخد كام: المجموع ماينفعش يبقى ١٣", "لازم تنزل من نفس النوع اللي نزل الأول لو معاك. الورق اللي ينفع ينزل واقف", "خد بالظبط اللي قلته عشان تكسب نقط. بعد ٥ أدوار الأعلى نقط يكسب" },
        ["ludo"] = new[] { "دورك! دوس على الزهر عشان ترمي", "لازم تجيب ٦ عشان تطلّع قطعة من البيت", "دوس على القطعة اللي عايز تحرّكها", "لو وقفت على قطعة منافس بترجّعها البيت. وصّل قطعك الأربعة الأول عشان تكسب" },
    };
    int guideStep; string guideGame;
    void GameGuide(TableView v)
    {
        // practice on the phone, or a card game (those practise on the server) played just for fun
        if (v == null || (offline == null && !(CardGame(v.game) && v.stake == 0)) || !Guides.TryGetValue(v.game, out var steps)) return;
        if (UnityEngine.PlayerPrefs.GetInt("guide." + v.game, 0) == 1) return;
        if (guideGame != v.id) { guideGame = v.id; guideStep = 0; }
        if (guideStep >= steps.Length) return;
        Toast(Loc.T(steps[guideStep++]), 4.5f);
        if (guideStep >= steps.Length && CardGame(v.game)) { PlayerPrefs.SetInt("guide." + v.game, 1); PlayerPrefs.Save(); }
    }
    public void StartGuide()
    {
        guided = true; guideHints = 0;
        OfflineGame.Level = 0;
        PlayerPrefs.SetInt("guideOffered", 1);
        Practice("ffa2");
    }
    /// Ends a practice game (a tournament match or a friend's room is waiting).
    public void StopPractice() { offline = null; if (view != null) ExitGame(); }
    bool pumping;   // the table message being handled comes from the practice game, not the server

    /// One tap from the friends list: a room for the two of you (in the game you play most) and an invitation.
    public void InviteFriend(string id)
    {
        if (!CanPlayOnline()) return;
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (offline != null) StopPractice();
        string g = LastGame;
        string mode = PlayerPrefs.GetString("mode." + g, GameUi.GameModes[g][0].mode);
        Send(new Dictionary<string, object> { ["t"] = "invite", ["id"] = id, ["mode"] = mode });
        Toast(Loc.T("الدعوة اتبعتت، استنى صاحبك في الغرفة"));
    }

    // called from Java (BBExtras): who invited this install, and the review card closing
    public void OnReferrer(string value) => Growth.OnReferrer(value);
    public void OnReview(string value) { Debug.Log("[review] " + value); }
    public void OnPurchase(string value) => Billing.OnPurchase(value);
    public void OnBillingError(string value) => Billing.OnError(value);

    void SetStatus(bool on) { statusText.Set(on ? Loc.T("متصل") : Loc.T("جاري الاتصال…")); statusText.color = on ? new Color(0.55f, 0.85f, 0.5f) : Ui.GoldLight; }

    public static double LocalMs() => (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalMilliseconds;
    public double ServerNow() => LocalMs() + serverOffset;

    void Update()
    {
        net?.Pump();
        PumpOffline();
        if (playNow != null && view == null) playNow.localScale = Vector3.one * (1 + 0.035f * Mathf.Sin(Time.time * 3.2f));
        if (Input.GetKeyDown(KeyCode.Escape) && menus != null && menus.IsOpen) { menus.Close(); return; }   // Android's back button
        if (menus != null && menus.IsOpen) return;
        if (toastGroup != null) toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, Time.time < toastUntil ? 1 : 0, Time.deltaTime * 4);
        // at a game, a touch counts when the finger lifts: two fingers on the screen are a zoom, never a move
        if (Input.touchCount >= 2) boardPinched = true;
        if (boardDragging && Input.GetMouseButtonUp(0))
        {
            boardDragging = false;
            if (view != null && !boardPinched && cafe != null && cafe.ready)
            {
                Vector2 up = Input.mousePosition;
                if (view.Board)
                {
                    // chess, tawla, ludo: the board works out what you tapped; dragged somewhere else, the piece goes there
                    boardGame.Tap(boardDragFrom);
                    if ((up - boardDragFrom).magnitude > Screen.height * 0.04f) boardGame.Drop(up);
                }
                else if ((up - boardDragFrom).magnitude < Screen.height * 0.04f)
                {
                    // at the table: a glowing spot picks the end for the chosen tile
                    var ray = cafe.cam.ScreenPointToRay(boardDragFrom);
                    if (Physics.Raycast(ray, out var hit, 200f))
                    {
                        var g = hit.collider.GetComponent<GhostEnd>();
                        if (g != null) PlayEnd(g.end);
                    }
                }
            }
        }
        // on the café floor: a tap on a table picks its game; a tap anywhere else (not a drag or a pinch) zooms back
        // out to the whole café
        if (view == null && cafe != null && cafe.ready && homeTapping)
        {
            if (Input.touchCount >= 2) homeTapPinched = true;
            if (Input.GetMouseButtonUp(0))
            {
                homeTapping = false;
                Vector2 up = Input.mousePosition;
                if (!homeTapPinched && (up - homeTapFrom).magnitude < Screen.height * 0.04f)
                {
                    var t = cafe.TableAt(homeTapFrom);
                    if (t != null) Pick(t);
                    else if (picked != null) Unpick();
                }
            }
        }
        if (cafe == null || !cafe.ready || !Input.GetMouseButtonDown(0) || OverUi()) return;
        if (view != null)
        {
            boardDragFrom = Input.mousePosition; boardDragging = true; boardPinched = Input.touchCount >= 2;
            return;
        }
        homeTapFrom = Input.mousePosition; homeTapping = true; homeTapPinched = Input.touchCount >= 2;
    }
    Vector2 homeTapFrom; bool homeTapping, homeTapPinched;

    /// Back to the whole café: the game's choices close and the camera pulls out.
    public void Unpick()
    {
        picked = null;
        game.HidePicker();
        cafe.Focus(null);
        foreach (var kv in gameButtons) kv.Value.color = Ui.Panel;
        foreach (var kv in gameButtons) kv.Value.GetComponentInChildren<Text>().color = Ui.GoldLight;
        Sfx.Tap();
    }

    /// Is the finger (or mouse) on a button or panel? On a phone each touch has its own pointer id, so the plain
    /// check (the mouse's) is never true there and taps on menus went through to the café behind them.
    static float Loaded(List<ResourceRequest> l) { float p = 0; foreach (var r in l) p += r.progress; return p / Mathf.Max(1, l.Count); }

    static bool OverUi()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        if (es.IsPointerOverGameObject()) return true;
        for (int i = 0; i < Input.touchCount; i++) if (es.IsPointerOverGameObject(Input.GetTouch(i).fingerId)) return true;
        return false;
    }

    void OnApplicationQuit() { CrashReport.SetMark(false); net?.Stop(); }

    /// Back in the app (after a call, the home screen…): reconnect right away; the server puts you back in your game.
    void OnApplicationPause(bool paused) { CrashReport.SetMark(!paused); if (!paused) net?.Resume(); }

    /* ---------- push reminders (Push.cs) ---------- */

    // called from Java (BBPush / BBMessagingService) on this object, "Board Battles"
    public void OnPushToken(string token) { Push.Token = token; Debug.Log("[push] token ready"); SendPushToken(); }
    public void OnPushError(string e) { Debug.Log("[push] " + e); }
    public void OnAdEvent(string e) => Ads.OnEvent(e);
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
            case "config":
                ApplyConfig(m);
                break;
            case "welcome":
                guestsOnline = m.Bool("guestsOnline");
                ApplyConfig(m.Obj("config"));
                voice.ice.Clear();
                foreach (var o in m.Arr("ice") ?? new List<object>()) if (o is Dictionary<string, object> d) voice.ice.Add(d);
                sentPushToken = null;   // (a new connection, maybe a different account)
                CrashReport.Flush(this);
                goto case "user";
            case "user":
                user = m.Obj("user");
                SendPushToken();
                if (user != null)
                {
                    nameText.Set(user.Str("name"));
                    coinsText.text = ((long)user.Num("coins")).ToString("N0");
                    if (user.Num("level") > 0) { levelBox.SetActive(true); levelText.Set(Loc.T("مستوى") + " " + (int)user.Num("level")); }
                    ShowMyTitle();
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
                Watching = m.Bool("watch");      // someone else's game, as an onlooker
                // a table from the server (a tournament match, a friend's room) ends a practice game on the phone
                if (!pumping && offline != null) { offline = null; view = null; }
                if (view == null || view.id != v.id) Debug.Log($"[app] table {v.game} {v.mode} {v.phase} {v.id.Substring(0, Mathf.Min(6, v.id.Length))} (was {(view == null ? "none" : view.game + " " + view.id.Substring(0, Mathf.Min(6, view.id.Length)))})");
                serverOffset = v.now - LocalMs();
                if (view == null) EnterGame();
                // the choice of end only lasts while it's still your turn and the tile is still in hand
                if (!v.MyTurn || !v.hand.Contains(sel)) { sel = -1; choose = null; }
                // a tile going down clacks; your turn chimes (and buzzes the phone)
                int tilesNow = v.line.Empty ? 0 : 1 + v.line.L.Count + v.line.R.Count;
                if (view != null && tilesNow > tilesOnTable) Sfx.Slap();
                if (v.MyTurn && v.phase == "play" && (view == null || !view.MyTurn)) { Sfx.Chime(); Sfx.Buzz(); }   // no tips during a game
                tilesOnTable = tilesNow;
                cafe.SetGameKind(v.game);   // each board framed its own way
                view = v;
                Redraw();
                break;
            case "left":
                Debug.Log("[app] left the table" + (view != null ? " (" + view.game + " " + view.phase + ")" : ""));
                if (Watching) { Watching = false; ExitGame(); break; }
                if (view != null && view.phase == "mend") { view = null; break; }   // the result stays up until you choose
                ExitGame();
                break;
            case "emote":
                ShowEmote((int)m.Num("seat", -1), m.Str("k"));
                break;
            case "served":
            {
                // someone at the table ordered a drink: the waiter brings it to their place
                int seat = (int)m.Num("seat", -1); string k = m.Str("k");
                if (view == null || seat < 0 || seat >= view.seats.Count || string.IsNullOrEmpty(k)) break;
                dominoTable.Serve(seat, k);
                var who = view.seats[seat];
                if (seat == view.you) Toast(Loc.T("الجرسون جايبلك") + " " + Skins.Name(k));
                else if (who != null) Toast(who.name + " " + Loc.T("طلب") + " " + Skins.Name(k));
                break;
            }
            case "doubled":
                // the ad after a win was watched: the winnings again
                Sfx.Coins(); Sfx.Chime();
                Toast(Loc.T("اتضاعف مكسبك!") + "  +" + Loc.Short((long)m.Num("coins")) + " " + Loc.T("كوين"), 3f);
                game.HideDouble();
                break;
            case "payout":
                game.ShowPayout(m.Str("kind"), m.Num("delta"), m.Num("double"));
                break;
            case "bought":
                Billing.OnBought(m);
                break;
            case "buyFailed":
                Billing.OnFailed(m);
                break;
            case "titles":
                // the titles catalog (designs), from the server; the dashboard can change it any time
                Titles.Set(m.Arr("list"));
                myTitleId = null; ShowMyTitle();
                break;
            case "titleWon":
            {
                var t = Titles.Get(m.Str("id"));
                if (t != null && view == null && menus != null) menus.ShowCard(Loc.T("مبروك!"), Loc.T("بقيت") + " «" + t.Name + "» " + Loc.T("الشهر ده كله"), t.id, true);
                else if (t != null) Toast(Loc.T("مبروك! بقيت") + " «" + t.Name + "» " + Loc.T("الشهر ده كله"), 8f);
                Sfx.Coins();
                break;
            }
            case "mustUpdate":
                // this version is below the minimum the dashboard set: the update dialog
                Updater.Need();
                break;
            case "notice":
                // a message from the team (the dashboard): a notice, a gift, maintenance
                Toast(m.Str("text"), 7f);
                Sfx.Chime();
                break;
            case "err":
                if (m.Str("code") == "banned")
                {
                    var why = m.Str("reason");
                    Toast(Loc.T("حسابك اتقفل") + (string.IsNullOrEmpty(why) ? "" : ": " + why), 12f);
                    net?.Stop();   // no reconnecting
                    break;
                }
                if (m.Str("code") == "sign_in_required")
                {
                    if (searching) CancelQueue();
                    menus?.AskSignIn(lastMode);
                    break;
                }
                if (menus != null && menus.OnError(m.Str("code"))) break;
                errorsSeen++;
                Debug.Log("[app] error " + m.Str("code"));
                Toast(ErrorText(m.Str("code")));
                break;
        }
    }

    int errorsSeen;
    static string ErrorText(string code)
    {
        switch (code)
        {
            case "maintenance": return Loc.T("السيرفر في صيانة دلوقتي، ارجع كمان شوية");
            case "game_off": return Loc.T("اللعبة دي مقفولة شوية، جرّب لعبة تانية");
            case "update": Updater.Need(); return Loc.T("لازم تحدّث عشان تكمل لعب");
            case "not_enough_coins": return Loc.T("رصيدك مش كفاية للرهان ده");
            case "drink_no_coins": return Loc.T("رصيدك مش كفاية للمشروب ده");
            case "not_your_turn": return Loc.T("مش دورك");
            case "room_not_found": return Loc.T("مفيش غرفة بالكود ده");
            case "seats_empty": return Loc.T("لسه في أماكن فاضية");
            case "room_full": return Loc.T("الغرفة مليانة");
            case "player_short": return Loc.T("حد من اللاعبين رصيده مش كفاية");
            case "must_roll": return Loc.T("ارمي الزهر الأول");
            case "must_open": return Loc.T("لازم تفتح بأكبر دوش معاك");
            case "choose_end": return Loc.T("اختار هتحطها في أنهي ناحية");
            case "can_play": return Loc.T("معاك حجر ينفع، العبه");
            case "must_draw": return Loc.T("اسحب من الكوم الأول");
            case "no_draw": return Loc.T("مفيش سحب في الغرفة دي");
            case "already_rolled": return Loc.T("انت رميت الزهر خلاص");
            case "must_move": return Loc.T("لسه عندك لعب، حرّك");
            case "not_playing": case "no_tile": case "bad_action": return Loc.T("الحركة دي مش هينفع دلوقتي");
            case "must_follow": return Loc.T("لازم تنزل من نفس النوع");
            case "call_higher": case "bad_call": return Loc.T("اطلب أعلى أو قول باص");
            case "not_13": return Loc.T("إنت الأخير: المجموع ماينفعش يبقى ١٣");
            case "bad_est": case "not_in_hand": case "game_over": return Loc.T("الحركة دي مش هينفع دلوقتي");
            case "illegal_move": return Loc.T("الحجر ده مش هينفع هنا");
            default: return Loc.T("حصلت مشكلة: ") + code;
        }
    }

    void EnterGame()
    {
        CrashReport.At("game " + (view != null ? view.mode : "?"));
        searching = false;
        game.ShowQueue(null);
        game.HidePicker();
        homeGroup.alpha = 0; homeGroup.blocksRaycasts = false;
        cafe.SetGame(true);
        Music.Home(false);
        dominoTable.Show(true);
        game.ShowHud(true);
    }

    /// Your move in a practice board game, made by the computer (filming the advert's game footage).
    public void AutoMove()
    {
        if (offline is OfflineBoard b) { var a = b.AutoMine(); if (a != null) Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a }); }
    }

    /// The home menu out of sight (filming the advert).
    public void HideHome() { homeGroup.alpha = 0; homeGroup.blocksRaycasts = false; }

    public void ExitGame()
    {
        CrashReport.At("home");
        offline = null; serverPractice = false;
        view = null; sel = -1; choose = null;
        game.ShowHud(false);
        dominoTable.Show(false);
        boardGame.Show(false);
        estimationTable.Hide();
        toastUntil = 0;   // a game's hint doesn't stay up over the café
        voice.Leave(); voice.Sync(null);
        cafe.SetGame(false);
        Music.Home(true);
        homeGroup.alpha = 1; homeGroup.blocksRaycasts = true;
    }

    void Redraw()
    {
        if (view == null) return;
        voice.Sync(view);
        if (CardGame(view.game))
        {
            dominoTable.Set(view, -1, null); boardGame.Show(false);
            estimationTable.Render(view);
            game.Render(view, -1, null);
            return;
        }
        estimationTable.Hide();
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

    /* ---------- what the control room sets for the app ---------- */
    /// Games switched off in the control room (no online or server games of them for now).
    public static readonly HashSet<string> GamesOff = new HashSet<string>();
    /// The control room's part of the settings: ads between games, games off, the table stakes and the wheel's prizes.
    void ApplyConfig(Dictionary<string, object> c)
    {
        if (c == null) return;
        Ads.Every = (int)c.Num("adEvery", 2);
        GamesOff.Clear();
        foreach (var g in c.Arr("gamesOff") ?? new List<object>()) GamesOff.Add(g as string ?? "");
        var st = c.Arr("stakes");
        if (st != null && st.Count > 0) { var a = new int[st.Count]; for (int i = 0; i < a.Length; i++) a[i] = System.Convert.ToInt32(st[i]); GameUi.Stakes = a; }
        var wh = c.Arr("wheel");
        if (wh != null && wh.Count == Menus.WheelCoins.Length) for (int i = 0; i < wh.Count; i++) Menus.WheelCoins[i] = System.Convert.ToInt64(wh[i]);
    }

    /// The game played last (dominoes at first, or when that game is no longer offered or is switched off).
    public static string LastGame
    {
        get { var g = PlayerPrefs.GetString("lastGame", "domino"); return GameUi.GameModes.ContainsKey(g) && !GamesOff.Contains(g) ? g : "domino"; }
    }

    string picked;
    void Pick(string g)
    {
        if (!GameUi.GameModes.ContainsKey(g)) return;
        if (GamesOff.Contains(g)) { Sfx.Tap(); Toast(Loc.T("اللعبة دي مقفولة شوية، جرّب لعبة تانية")); return; }
        PlayerPrefs.SetString("lastGame", g);
        if (g == picked && game.PickerOpen) return;   // already showing this game's choices
        picked = g;
        cafe.Focus(g);
        foreach (var kv in gameButtons) kv.Value.color = kv.Key == g ? Ui.Gold : Ui.Panel;
        foreach (var kv in gameButtons) kv.Value.GetComponentInChildren<Text>().color = kv.Key == g ? Ui.Ink : Ui.GoldLight;
        game.ShowPicker(g, Loc.T(GameNames[g]));
    }

    /// A real account (Google, Apple, phone or Play Games): online games need one; guests play the computer.
    public bool SignedIn => user != null && (!string.IsNullOrEmpty(user.Str("google")) || !string.IsNullOrEmpty(user.Str("apple"))
        || !string.IsNullOrEmpty(user.Str("phone")) || user.Bool("playGames") || !string.IsNullOrEmpty(user.Str("email")));
    /// False (and asks the player to sign in) when online play needs an account they don't have yet.
    bool guestsOnline;   // the server lets guests play online too (test servers)
    /// This phone had no saved account when the game opened (a new install, or the app's data was cleared).
    public bool FreshInstall { get; private set; }
    public bool CanPlayOnline(string mode = null)
    {
        if (SignedIn || user == null || guestsOnline) return true;
        menus?.AskSignIn(mode ?? lastMode);
        return false;
    }

    public void Quick(string mode, int stake)
    {
        if (!CanPlayOnline(mode)) return;
        if (offline != null) { offline = null; if (view != null) ExitGame(); }
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (user != null && user.Num("coins") < stake) { Toast(Loc.T("رصيدك مش كفاية للرهان ده")); return; }
        lastMode = mode; lastStake = stake; serverPractice = false;
        Send(new Dictionary<string, object> { ["t"] = "quick", ["mode"] = mode, ["stake"] = (double)stake });
        searching = true;
        game.ShowQueue(new Dictionary<string, object> { ["count"] = 1.0, ["need"] = (double)GameUi.Players(mode), ["botsAt"] = 0.0, ["now"] = 0.0 });
    }

    /// A free game against the computer, starting at once (no stake, no rating). It runs on the phone itself,
    /// so it works without the server (Offline/*.cs).
    public void Practice(string mode)
    {
        // the first practice game of each game starts with a short guide
        string g = Modes.GameOf(mode);
        lastMode = mode; lastStake = 0;
        if (view != null) Leave();
        if (searching) CancelQueue();
        if (CardGame(g))
        {
            // the card games' computer players live on the server: practice there (needs the connection)
            if (net == null || !net.Online) { Toast(Loc.T("ألعاب الورق محتاجة نت عشان تتلعب")); return; }
            offline = null; serverPractice = true;
            Send(new Dictionary<string, object> { ["t"] = "practice", ["mode"] = mode, ["level"] = OfflineGame.Level == 0 ? "easy" : OfflineGame.Level == 2 ? "hard" : "normal" });
            return;
        }
        string name = user != null ? user.Str("name") : "";
        int ch = user != null ? (int)user.Num("ch", 0) : 0;
        try { offline = OfflineGame.Create(mode, name, ch); }
        catch (System.Exception e) { Debug.LogException(e); Toast(Loc.T("حصلت مشكلة: ") + e.Message); offline = null; return; }
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
            pumping = true;
            try { OnMessage(new Dictionary<string, object> { ["t"] = "table", ["view"] = vm }); } finally { pumping = false; }
            // a finished practice game earns a little experience (when connected)
            if (view != null && view.phase == "mend" && view.match != null && view.id != reportedPractice)
            {
                reportedPractice = view.id;
                bool won = view.you >= 0 && view.match.team == view.TeamOf(view.you);
                if (net != null && net.Online) net.Send(new Dictionary<string, object> { ["t"] = "practiceDone", ["game"] = view.game, ["won"] = won });
                // the first practice game of each game had its guide: not again
                UnityEngine.PlayerPrefs.SetInt("guide." + view.game, 1); UnityEngine.PlayerPrefs.Save();
                // the first-game guide is done: its reward (once, the server checks)
                if (guided && net != null && net.Online) { guided = false; net.Send(new Dictionary<string, object> { ["t"] = "tutorialDone" }); }
            }
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
            case "voice": case "rtc": case "start": case "bot": case "kick": case "setup": case "cancel":
                return true;
        }
        return false;
    }

    /// A room for friends: you get a code to send them; 0 is a friendly game with no stake.
    public void CreateRoom(string mode, int stake)
    {
        if (!CanPlayOnline(mode)) return;
        if (offline != null) { offline = null; if (view != null) ExitGame(); }
        if (net == null || !net.Online) { Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (stake > 0 && user != null && user.Num("coins") < stake) { Toast(Loc.T("رصيدك مش كفاية للرهان ده")); return; }
        Send(new Dictionary<string, object> { ["t"] = "create", ["mode"] = mode, ["target"] = 0.0, ["stake"] = (double)stake });
    }
    public void JoinRoom(string code)
    {
        if (!CanPlayOnline()) return;
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

    /// Watching a friend's game or a tournament match (not playing in it).
    public bool Watching { get; private set; }
    public void Watch(string pid = null, string table = null)
    {
        var m = new Dictionary<string, object> { ["t"] = "watch" };
        if (pid != null) m["pid"] = pid; if (table != null) m["table"] = table;
        Send(m);
    }

    /// "Leave" during an online match: first say what it costs (the match and the stake, to the other side).
    public void AskLeave()
    {
        var v = view;
        bool live = v != null && !Practicing && !Watching && v.you >= 0 && (v.phase == "play" || v.phase == "rend");
        if (!live) { Leave(); return; }
        int sides = Modes.Sides(v.mode);
        string others = "";
        for (int i = 0; i < v.seats.Count; i++) if (i != v.you && v.seats[i] != null && (sides != 2 || TeamOf(v.mode, i) != TeamOf(v.mode, v.you))) others += (others.Length > 0 ? "، " : "") + GameUi.NameOf(v, i);
        string coins = v.stake > 0 ? Loc.Short(v.stake) + " " + Loc.T("كوين") : "";
        string text = sides == 2
            ? Loc.T("لو خرجت دلوقتي هتخسر الماتش") + (coins != "" ? " " + Loc.T("و") + " " + coins : "") + "، " + Loc.T("و") + " " + others + " " + Loc.T("هيكسب") + (coins != "" ? " " + Loc.T("الكوينز") : "") + "."
            : Loc.T("لو خرجت دلوقتي هتخسر الماتش") + (coins != "" ? " " + Loc.T("و") + " " + coins : "") + "، " + Loc.T("والكمبيوتر هيكمل مكانك") + ".";
        menus.Confirm(Loc.T("تخرج من الماتش؟"), text, Loc.T("اخرج وخسّر"), Loc.T("كمّل اللعب"), Leave);
    }
    static int TeamOf(string mode, int seat) => mode == "team" ? seat % 2 : seat;

    public void Leave()
    {
        if (Watching) { Watching = false; if (view != null) leftTableId = view.id; Send(new Dictionary<string, object> { ["t"] = "unwatch" }); ExitGame(); return; }
        if (view != null) leftTableId = view.id;
        bool finished = view != null && view.phase == "mend";
        bool played = view != null && view.you >= 0;
        if (view != null && !finished) Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
        // a game finished or left counts towards the ad between games (one game in every two by default)
        if (played && !AdFree() && !Recording) Ads.BetweenGames(null);
    }
    bool AdFree() => user != null && user.Num("adFreeUntil") > LocalMs();

    public void PlayAgain()
    {
        if (view != null && view.phase == "mend") Send(new Dictionary<string, object> { ["t"] = "leave" });
        ExitGame();
        // an ad between games (not for players on the ad-free plan), then the next game
        bool adFree = AdFree();
        System.Action next = () => { if (lastStake == 0) Practice(lastMode); else Quick(lastMode, lastStake); };
        if (adFree || Recording) next(); else Ads.BetweenGames(next);
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
        // both ends show the same number: either way is the same move, so play it on the shorter arm
        if (Domino.LeftVal(v.line) == Domino.RightVal(v.line)) { Play(id, v.line.L.Count <= v.line.R.Count ? 'L' : 'R'); return; }
        sel = id; choose = ends;
        Redraw();
    }

    /// A tile dragged from your hand and let go over the table: it goes on the end it was dropped nearest to.
    public void DropTile(int id, Vector2 screen)
    {
        var v = view;
        if (v == null || !v.MyTurn) return;
        if (v.open >= 0 && v.line.Empty && id != v.open) { Toast(Loc.T("افتح بالـ ") + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1]); return; }
        var ends = Domino.EndsFor(v.line, id);
        if (ends.Count == 0) { Toast(Loc.T("الحجر ده مش هيركب")); return; }
        if (ends.Count == 1) { Play(id, ends[0]); return; }
        char best = dominoTable.NearestEnd(v.line, id, ends, screen);
        Play(id, best);
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

    /// Quick chat: your emote goes to the table (in practice it stays here, and the computer may answer).
    public void SendEmote(string k)
    {
        if (view == null) return;
        if (offline != null) { ShowEmote(view.you, k); StartCoroutine(game.chat.BotReply(view, k)); return; }
        Send(new Dictionary<string, object> { ["t"] = "emote", ["k"] = k });
    }

    public void ShowEmote(int seat, string k)
    {
        if (view == null || seat < 0) return;
        if (seat < view.seats.Count && view.seats[seat] != null && seat != view.you && Menus.IsMuted(view.seats[seat].pid)) return;   // you muted them
        game.ShowEmote(view, seat, k);
        var who = dominoTable.CharAtSeat(seat);
        if (who != null && who.gameObject.activeInHierarchy) { if (k == "gg" || k == "clap" || k == "love" || k == "fire") who.Win(); else who.Gesture(who.transform.position + who.Forward * 2f + Vector3.up * 3f); }
    }

    /// The title you wear, under your level on the home screen.
    RectTransform myTitle, homeRoot; string myTitleId;
    void ShowMyTitle()
    {
        string id = user?.Str("title");
        if (id == myTitleId && myTitle != null) return;
        if (myTitle != null) Destroy(myTitle.gameObject);
        myTitle = null; myTitleId = id;
        if (string.IsNullOrEmpty(id) || homeRoot == null) return;
        myTitle = Titles.Badge(homeRoot, id, 11);
        if (myTitle == null) { myTitleId = null; return; }
        myTitle.Place(new Vector2(1, 1), new Vector2(1, 1), myTitle.sizeDelta, new Vector2(-14, -80));
        myTitle.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Tap(); menus?.OpenCollection(6); });
        myTitle.Find("Edge").GetComponent<Image>().raycastTarget = true;
    }

    public void Toast(string s) => Toast(s, 2.6f);
    public void Toast(string s, float seconds)
    {
        toastText.Set(s);
        toastUntil = Time.time + seconds;
    }

    void BuildHome()
    {
        ui = Ui.MakeCanvas("Home", 10);
        var root = Ui.Rect("Home", ui.transform).Safe();
        homeRoot = root;
        homeGroup = root.gameObject.AddComponent<CanvasGroup>();
        homeGroup.alpha = 0;

        // the logo, top left
        var logo = Ui.Picture(root, "Logo", Resources.Load<Texture2D>("UI/Logo"));
        logo.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, 77), new Vector2(8, -4));

        // you: name and coins, top right
        var me = Ui.Box(root, "Me", Ui.Panel);
        me.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(230, 40), new Vector2(-14, -10));
        // tap yourself: your own player card
        me.gameObject.AddComponent<Button>().onClick.AddListener(() => { Sfx.Tap(); menus?.OpenMyCard(); });
        nameText = Ui.Label(me.transform, "…", 15, Color.white, TextAnchor.MiddleRight, Ui.Bold);
        nameText.rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(120, 30), new Vector2(-12, 0));
        var coin = Ui.Box(me.transform, "Coin", Ui.Gold);
        coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 18), new Vector2(12, 0));
        coinsText = Ui.Label(me.transform, "—", 15, Ui.GoldLight, TextAnchor.MiddleLeft, Ui.Bold);
        coinsText.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 30), new Vector2(36, 0));

        // your level, under your name
        var lv = Ui.Box(root, "Level", Ui.Gold);
        lv.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(64, 20), new Vector2(-14, -54));
        levelText = Ui.Label(lv.transform, "", 11, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold); levelText.rectTransform.Fill();
        levelBox = lv.gameObject; levelBox.SetActive(false);

        // connection
        statusText = Ui.Label(root, "جاري الاتصال…", 12, Ui.GoldLight, TextAnchor.MiddleRight);
        statusText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(200, 20), new Vector2(-86, -54));

        // the five games, down the right side (under your level, connection and title badge)
        var list = Ui.Rect("Games", root).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(170, 230), new Vector2(-14, -104));
        var title = Ui.Label(list, "اختار لعبتك", 15, Ui.GoldLight, TextAnchor.MiddleRight, Ui.Bold);
        title.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(170, 24), new Vector2(0, 0));
        for (int i = 0; i < HomeGames.Length; i++)
        {
            var g = HomeGames[i];
            var b = Ui.Button(list, GameNames[g], Ui.Panel, Ui.GoldLight, 18, () => Pick(g));
            ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(170, 36), new Vector2(0, -28 - i * 40));
            gameButtons[g] = b.GetComponent<Image>();
            var outline = b.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.35f);
        }

        // a hint at the bottom
        var hint = Ui.Label(root, "اضغط على أي ترابيزة في القهوة", 13, new Color(1, 1, 1, 0.75f));
        hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 24), new Vector2(0, 12));
        // straight into a free game against the computer (the first thing a new player sees)
        var now = Ui.Button(root, "العب دلوقتي", Ui.Gold, Ui.Ink, 20, () =>
        {
            Sfx.Tap();
            string g = LastGame;
            var modes = GameUi.GameModes[g];
            string mode = PlayerPrefs.GetString("mode." + g, modes[0].mode);
            if (System.Array.FindIndex(modes, m => m.mode == mode) < 0) mode = modes[0].mode;
            // signed in: straight into an online game (real people, the computer fills empty seats after a few
            // seconds); a guest plays the computer on the phone
            if ((SignedIn || guestsOnline) && net != null && net.Online) Quick(mode, GameUi.Stakes[Mathf.Clamp(PlayerPrefs.GetInt("ahwa.stake", 0), 0, GameUi.Stakes.Length - 1)]);
            else Practice(mode);
        });
        ((RectTransform)now.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 50), new Vector2(-30, 42));
        now.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.45f);
        playNow = (RectTransform)now.transform;

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
            menus.OpenDaily(); yield return new WaitForSeconds(0.8f); Still("d1-daily.png");
            Send(new Dictionary<string, object> { ["t"] = "daily" }); yield return new WaitForSeconds(1.2f); Still("d2-daily-got.png");
            menus.Close();
            menus.ShowSeasonPromo(); yield return new WaitForSeconds(0.8f); Still("x17-season-promo.png");
            menus.OpenMissions(); yield return new WaitForSeconds(0.8f); Still("x1-missions.png");
            menus.OpenSeason(); yield return new WaitForSeconds(0.8f); Still("x2-season.png");
            menus.OpenFriends(); yield return new WaitForSeconds(1.2f); Still("x3-friends.png");
            menus.OpenTourney(); yield return new WaitForSeconds(1.2f); Still("x4-tourney.png");
            menus.OpenMyCard(); yield return new WaitForSeconds(1.2f); Still("x5-card.png");
            menus.OpenCollection(6); yield return new WaitForSeconds(0.8f); Still("x13-titles.png");
            menus.OpenShop(6); yield return new WaitForSeconds(0.8f); Still("x15-shop-win.png");
            game.Celebrate(30, "win_fireworks"); yield return new WaitForSeconds(0.9f); Still("x16-fireworks.png");
            menus.ShopGame = 1; menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("x6-shop-boards-tawla.png");
            menus.ShopGame = 2; menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("x7-shop-boards-chess.png");
            menus.ShopGame = 1; menus.OpenShop(2); yield return new WaitForSeconds(0.8f); Still("x8-shop-checkers.png");
            menus.ShopGame = 2; menus.OpenShop(2); yield return new WaitForSeconds(0.8f); Still("x9-shop-pieces.png");
            menus.ShopGame = 0; menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("x10-shop-domino-board.png");
            menus.OpenShop(3); yield return new WaitForSeconds(0.8f); Still("x11-shop-dice.png");
            menus.OpenCollection(0); yield return new WaitForSeconds(0.8f); Still("x12-collection.png");
            menus.Close();
            menus.OpenSettings(); yield return new WaitForSeconds(0.8f); Still("m1-settings.png");
            menus.OpenShop(0); yield return new WaitForSeconds(0.8f); Still("m2-shop-coins.png");
            menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("m3-shop-tiles.png");
            menus.OpenLeaders(0); yield return new WaitForSeconds(2.5f); Still("m4-leaders.png");
            menus.OpenLeaders(1); yield return new WaitForSeconds(2f); Still("m4b-leaders-country.png");
            menus.OpenLeaders(2); yield return new WaitForSeconds(2f); Still("m4c-leaders-gov.png");
            menus.OpenLeaders(3); yield return new WaitForSeconds(2f); Still("m4d-leaders-titles.png");
            menus.OpenHowTo(0); yield return new WaitForSeconds(0.8f); Still("m5-howto.png");
            menus.Close();
            menus.WatchAd(); yield return new WaitForSeconds(1.5f); Still("m6-ad.png");
            yield return new WaitForSeconds(5f); Still("m7-ad-reward.png");
            Loc.Set(true); yield return new WaitForSeconds(0.8f); Still("m8-home-en.png");
            menus.OpenMyCard(); yield return new WaitForSeconds(1.2f); Still("x14-card-en.png");
            menus.OpenShop(1); yield return new WaitForSeconds(0.8f); Still("m9-shop-en.png");
            menus.OpenHowTo(0); yield return new WaitForSeconds(0.8f); Still("m10-howto-en.png");
            menus.OpenSettings(); yield return new WaitForSeconds(0.8f); Still("m11-settings-en.png"); menus.OpenSupport(); yield return new WaitForSeconds(0.8f); Still("m12-support-en.png");
            menus.Close(); Loc.Set(false);
            // the café's five tables, then estimation against the computer (it runs on the server): pass or call,
            // say a number, play the cards that may be played
            game.HidePicker(); cafe.Focus(null); yield return new WaitForSeconds(2.5f); Still("e0-cafe.png");
            Pick("estimation"); yield return new WaitForSeconds(2.5f); Still("e1-picker.png");
            Practice("estimation");
            t0 = Time.realtimeSinceStartup;
            while ((view == null || view.game != "estimation") && Time.realtimeSinceStartup - t0 < 30) yield return null;
            Debug.Log("[test] estimation: " + (view == null ? "no table" : view.game + " " + view.phase));
            if (view != null && view.game == "estimation")
            {
                float pe = 0; int lastVe = -1; var phases = new HashSet<string>();
                while (pe < 45f && view != null && view.phase != "mend")
                {
                    var g = view.estimation;
                    if (g != null && view.MyTurn && view.v != lastVe)
                    {
                        lastVe = view.v;
                        yield return new WaitForSeconds(1.1f);
                        pe += 1.1f;
                        g = view?.estimation;
                        if (view == null || g == null || !view.MyTurn) continue;
                        string ph = g.Str("phase");
                        if (phases.Add(ph)) Still("e2-" + ph + ".png");
                        var ok = TableView.IntList(g.Arr(ph == "est" ? "estOptions" : "legal"));
                        var a = ph == "call" ? new Dictionary<string, object> { ["t"] = "call", ["n"] = 0 }
                            : ph == "est" ? new Dictionary<string, object> { ["t"] = "est", ["n"] = ok.Count > 0 ? ok[0] : 0 }
                            : new Dictionary<string, object> { ["t"] = "card", ["id"] = ok.Count > 0 ? ok[0] : -1 };
                        Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });
                    }
                    pe += Time.deltaTime;
                    yield return null;
                }
                Still("e3-estimation-later.png");
                if (errorsSeen > 0) Debug.Log("[test] estimation: " + errorsSeen + " errors");
                Leave();
                yield return new WaitForSeconds(1f);
            }
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
                SendEmote("nice"); yield return new WaitForSeconds(2.2f); Still("q-chat.png");
                Still("7-later.png");
                yield return new WaitForSeconds(2.5f); Still("v-board-view.png");
                Debug.Log("[test] board view: players shown " + (dominoTable.CharAtSeat(1) != null && dominoTable.CharAtSeat(1).gameObject.activeSelf));
                yield return new WaitForSeconds(0.6f);
                yield return new WaitForSeconds(0.5f);
            }
            Loc.Set(true);   // the board games in English (screenshots for the store in both languages)
            // the board games, each a practice game against the computer, played by the test for a while
            foreach (var (mode, tag) in new[] { ("tawla", "tawla"), ("chess", "chess"), ("ludo4", "ludo"), ("mahbousa", "mahbousa") })
            {
                // the board games in some of the new skins (mahbousa stays classic, to compare)
                Skins.Force.Clear();
                if (tag == "tawla") { Skins.Force["tawla"] = "tawla_pharaoh"; Skins.Force["checkers"] = "checkers_gold"; Skins.Force["dice"] = "dice_emerald"; }
                if (tag == "chess") { Skins.Force["chess"] = "chess_marble"; Skins.Force["pieces"] = "pieces_porcelain"; }
                if (tag == "ludo") Skins.Force["dice"] = "dice_pharaoh";
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
            Loc.Set(false);
            // every other way to play, each for a few seconds (not filmed): the log says whether it plays
            foreach (var mode in new[] { "ffa2", "ffa3", "ffa4", "tawla31", "gulbahar", "chess1", "chess3", "chess10", "ludo2", "ludo3", "ludoq2", "ludoq4", "ludom2", "ludom4" })
            {
                recordOn = false;
                if (view != null) Leave();
                yield return new WaitForSeconds(0.3f);
                Practice(mode);
                float tm = Time.realtimeSinceStartup;
                while ((view == null || view.mode != mode) && Time.realtimeSinceStartup - tm < 10) yield return null;
                if (view == null || view.mode != mode) { Debug.Log("[test] " + mode + ": NO GAME"); continue; }
                int v0 = view.v, mine = 0;
                bool sawOther = false;
                for (float t = 0; t < 4f && view != null; t += Time.deltaTime)
                {
                    if (view.turn >= 0 && view.turn != view.you) sawOther = true;
                    if (view.MyTurn)
                    {
                        if (view.Board) boardGame.AutoMove();
                        else foreach (var id in view.hand)
                        {
                            var ends = Domino.EndsFor(view.line, id);
                            if ((view.open >= 0 && view.line.Empty && id != view.open) || ends.Count == 0) continue;
                            Play(id, ends[0]); break;
                        }
                        mine++;
                        yield return new WaitForSeconds(0.4f);
                    }
                    yield return null;
                }
                Debug.Log($"[test] {mode}: ok, {view?.seats.Count} seats, phase {view?.phase}, {(view != null ? view.v - v0 : 0)} updates, my moves {mine}, computer played {sawOther}");
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
        offline = null;
        Application.Quit();
        // the desktop test player sometimes hangs while shutting down: make sure it ends
        yield return new WaitForSecondsRealtime(5);
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    static int Stakes0() => GameUi.Stakes[0];
}
