// The game's screens over the 3D café: choosing a way to play and a stake, searching for players,
// and at the table: scores, names over the players, the turn timer, your hand, and the round and match results.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameUi : MonoBehaviour
{
    public static readonly int[] Stakes = { 5000, 20000, 50000, 100000, 250000, 500000, 1000000 };
    static readonly string[] DominoModes = { "ffa2", "ffa3", "ffa4", "team" };
    static readonly Dictionary<string, string> ModeNames = new Dictionary<string, string>
    {
        ["ffa2"] = "فردي", ["ffa3"] = "٣ لاعبين", ["ffa4"] = "٤ لاعبين", ["team"] = "زوجي ٢ ضد ٢",
    };

    App app;
    Canvas canvas;
    RectTransform root;

    // picking a game: its ways to play, the stake, and quick match / with a friend / practice
    RectTransform picker, modeRow, friendPanel; Text pickerTitle, stakeText, pickerNote; GameObject stakeRow;
    InputField codeInput;
    public string game = "domino", mode = "ffa2"; public int stakeIndex;
    // a room for friends, before the game starts
    RectTransform lobby, lobbySeats; Text lobbyCode, lobbyNote; GameObject startButton;
    string lobbyKey = "";

    // searching
    RectTransform queuePanel; Text queueText, queueSub; float botsAtLocal;

    // at the table
    RectTransform hud, handBar, endButtons, resultPanel, matchPanel;
    Text scoreText, roundText, turnText, resultTitle, resultSub, matchTitle, matchSub, payoutText;
    Image timerFill;
    readonly Text[] seatTags = new Text[4];
    readonly Image[] seatTagBg = new Image[4];
    Text endL, endR;
    GameObject rollButton;
    RawImage micIcon; Image micButton;

    void ToggleMic()
    {
        Sfx.Tap();
        var v = Voice.I; if (v == null) return;
        if (!v.Joined) StartCoroutine(JoinThenShow(v));
        else { v.SetMicMuted(!v.MicMuted); ShowMic(); app.Toast(Loc.T(v.MicMuted ? "الميك مقفول" : "الصوت شغال")); }
    }
    System.Collections.IEnumerator JoinThenShow(Voice v) { yield return v.Join(); ShowMic(); }
    public void ShowMic()
    {
        var v = Voice.I;
        bool on = v != null && v.Joined && !v.MicMuted;
        micIcon.texture = Menus.Icon(on ? "mic" : "micoff");
        micIcon.color = on ? Ui.Ink : Ui.GoldLight;
        micButton.color = on ? Ui.Gold : new Color(0.1f, 0.06f, 0.04f, 0.85f);
    }
    string handKey = "";

    public void Build(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Game", 20);
        root = Ui.Rect("Root", canvas.transform).Fill();
        stakeIndex = Mathf.Clamp(PlayerPrefs.GetInt("ahwa.stake", 0), 0, Stakes.Length - 1);
        BuildPicker(); BuildQueue(); BuildHud(); BuildLobby();
        picker.gameObject.SetActive(false); queuePanel.gameObject.SetActive(false); hud.gameObject.SetActive(false); lobby.gameObject.SetActive(false);
    }

    /* ---------- choosing the game ---------- */

    /// The ways to play each game (the server's modes) and their names.
    public static readonly Dictionary<string, (string mode, string name)[]> GameModes = new Dictionary<string, (string, string)[]>
    {
        ["domino"] = new[] { ("ffa2", "فردي"), ("ffa3", "٣ لاعبين"), ("ffa4", "٤ لاعبين"), ("team", "زوجي ٢ ضد ٢") },
        ["tawla"] = new[] { ("tawla", "عادة"), ("mahbousa", "محبوسة"), ("tawla31", "٣١") },
        ["chess"] = new[] { ("chess", "٥ دقايق") },
        ["ludo"] = new[] { ("ludo2", "٢ لاعبين"), ("ludo3", "٣ لاعبين"), ("ludo4", "٤ لاعبين") },
    };
    public static int Players(string mode) =>
        mode == "ffa3" || mode == "ludo3" ? 3 : mode == "ffa4" || mode == "team" || mode == "ludo4" ? 4 : 2;

    void BuildPicker()
    {
        // in the middle of the screen, clear of the buttons down the left and the games down the right
        var card = Ui.Box(root, "Picker", new Color(0.1f, 0.06f, 0.04f, 0.95f));
        picker = card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 296), new Vector2(-40, -14));
        card.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f);
        card.raycastTarget = true;
        pickerTitle = Ui.Label(picker, "", 24, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        pickerTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 34), new Vector2(0, -8));
        var x = Ui.Button(picker, "", new Color(1, 1, 1, 0.08f), Color.white, 10, () => { Sfx.Tap(); HidePicker(); app.cafe.Shot(null); });
        ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(32, 32), new Vector2(-8, -8));
        Ui.Picture(x.transform, "X", Menus.Icon("close")).rectTransform.Fill(8);
        modeRow = Ui.Rect("Modes", picker).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 40), new Vector2(0, -50));
        // the stake: − value +
        stakeRow = Ui.Rect("Stake", picker).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 40), new Vector2(0, -98)).gameObject;
        var minus = Ui.Button(stakeRow.transform, "−", Ui.Panel, Ui.Gold, 22, () => { stakeIndex = Mathf.Max(0, stakeIndex - 1); RefreshPicker(); });
        ((RectTransform)minus.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 38), new Vector2(10, 0));
        var plus = Ui.Button(stakeRow.transform, "+", Ui.Panel, Ui.Gold, 22, () => { stakeIndex = Mathf.Min(Stakes.Length - 1, stakeIndex + 1); RefreshPicker(); });
        ((RectTransform)plus.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(44, 38), new Vector2(-10, 0));
        stakeText = Ui.Label(stakeRow.transform, "", 18, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        stakeText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 38), Vector2.zero);
        // three ways to play
        var q = Ui.Button(picker, "لعب سريع", Ui.Gold, Ui.Ink, 20, () => { Sfx.Tap(); app.Quick(mode, Stakes[stakeIndex]); });
        ((RectTransform)q.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(360, 46), new Vector2(0, 82));
        var f = Ui.Button(picker, "العب مع صاحبك", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 16, () => { Sfx.Tap(); friendPanel.gameObject.SetActive(true); });
        ((RectTransform)f.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(176, 40), new Vector2(-92, 34));
        var p = Ui.Button(picker, "تدريب", Ui.Panel, Ui.GoldLight, 16, () => { Sfx.Tap(); app.Practice(mode); });
        ((RectTransform)p.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(176, 40), new Vector2(92, 34));
        p.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.4f);
        pickerNote = Ui.Label(picker, "", 12, new Color(1, 1, 1, 0.7f));
        pickerNote.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(380, 22), new Vector2(0, 8));

        // with a friend: make a room (and send them its code), or type the code they sent you
        var fp = Ui.Box(picker, "Friend", new Color(0.1f, 0.06f, 0.04f, 0.99f));
        friendPanel = fp.rectTransform; friendPanel.Fill(); fp.raycastTarget = true;
        var ft = Ui.Label(friendPanel, "العب مع صاحبك", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        ft.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 34), new Vector2(0, -8));
        var mk = Ui.Button(friendPanel, "اعمل غرفة وابعت الكود", Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); friendPanel.gameObject.SetActive(false); app.CreateRoom(mode, stakeIndex < 0 ? 0 : Stakes[stakeIndex]); });
        ((RectTransform)mk.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, 44), new Vector2(0, -52));
        var free = Ui.Button(friendPanel, "غرفة ودية (من غير رهان)", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 15, () => { Sfx.Tap(); friendPanel.gameObject.SetActive(false); app.CreateRoom(mode, 0); });
        ((RectTransform)free.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, 38), new Vector2(0, -102));
        var or = Ui.Label(friendPanel, "أو ادخل بكود صاحبك", 14, Ui.GoldLight);
        or.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, 22), new Vector2(0, -146));
        codeInput = CodeField(friendPanel);
        ((RectTransform)codeInput.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 42), new Vector2(-70, -174));
        var join = Ui.Button(friendPanel, "ادخل", Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); var c = codeInput.text.Trim(); if (c.Length >= 4) { friendPanel.gameObject.SetActive(false); app.JoinRoom(c); } });
        ((RectTransform)join.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(120, 42), new Vector2(110, -174));
        var back = Ui.Button(friendPanel, "رجوع", Ui.Panel, Ui.GoldLight, 15, () => { Sfx.Tap(); friendPanel.gameObject.SetActive(false); });
        ((RectTransform)back.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 36), new Vector2(0, 12));
        friendPanel.gameObject.SetActive(false);
    }

    static InputField CodeField(Transform parent)
    {
        var bg = Ui.Box(parent, "Code", new Color(1, 1, 1, 0.92f));
        bg.raycastTarget = true;
        var text = Ui.Label(bg.transform, "", 20, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold);
        text.rectTransform.Fill(6); text.raycastTarget = false; text.supportRichText = false;
        var hint = Ui.Label(bg.transform, "الكود", 16, new Color(0, 0, 0, 0.35f), TextAnchor.MiddleCenter, Ui.Body);
        hint.rectTransform.Fill(6);
        var f = bg.gameObject.AddComponent<InputField>();
        f.textComponent = text; f.placeholder = hint; f.characterLimit = 8;
        f.contentType = InputField.ContentType.Alphanumeric;
        f.onValueChanged.AddListener(v => { var low = v.ToLowerInvariant(); if (low != v) f.text = low; });
        return f;
    }

    public void ShowPicker(string g, string gameName)
    {
        game = g;
        picker.gameObject.SetActive(true);
        friendPanel.gameObject.SetActive(false);
        pickerTitle.Set(gameName);
        var modes = GameModes[g];
        mode = PlayerPrefs.GetString("mode." + g, modes[0].mode);
        if (Array.FindIndex(modes, m => m.mode == mode) < 0) mode = modes[0].mode;
        for (int i = modeRow.childCount - 1; i >= 0; i--) Destroy(modeRow.GetChild(i).gameObject);
        float w = Mathf.Min(120, 372f / modes.Length - 6);
        for (int i = 0; i < modes.Length; i++)
        {
            var m = modes[i].mode;
            int k = Loc.En ? i : modes.Length - 1 - i;
            var b = Ui.Button(modeRow, modes[i].name, Ui.Panel, Ui.GoldLight, 14, () => { Sfx.Tap(); mode = m; PlayerPrefs.SetString("mode." + game, m); RefreshPicker(); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(w, 36), new Vector2((k - (modes.Length - 1) / 2f) * (w + 6), 0));
            b.name = "Mode " + m;
        }
        pickerNote.Set(Loc.T("الرهان: كل لاعب يدفع، والفايز ياخد") + "  ·  " + Loc.T("التدريب ببلاش"));
        RefreshPicker();
    }
    public void HidePicker() { picker.gameObject.SetActive(false); }
    public bool PickerOpen => picker.gameObject.activeSelf;

    void RefreshPicker()
    {
        PlayerPrefs.SetInt("ahwa.stake", stakeIndex);
        foreach (Transform t in modeRow)
        {
            bool on = t.name == "Mode " + mode;
            t.GetComponent<Image>().color = on ? Ui.Gold : Ui.Panel;
            t.GetComponentInChildren<Text>().color = on ? Ui.Ink : Ui.GoldLight;
        }
        stakeText.Set(Loc.T("رهان ") + Short(Stakes[stakeIndex]));
    }

    /* ---------- a room for friends ---------- */

    void BuildLobby()
    {
        var card = Ui.Box(root, "Lobby", new Color(0.1f, 0.06f, 0.04f, 0.95f));
        lobby = card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420, 300), new Vector2(0, -16));
        card.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f);
        card.raycastTarget = true;
        var t = Ui.Label(lobby, "غرفة صحابك", 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 30), new Vector2(0, -6));
        lobbyCode = Ui.Label(lobby, "", 30, Color.white, TextAnchor.MiddleCenter, Ui.Display);
        lobbyCode.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(220, 40), new Vector2(-60, -40));
        var copy = Ui.Button(lobby, "انسخ", Ui.Panel, Ui.GoldLight, 14, () => { Sfx.Tap(); GUIUtility.systemCopyBuffer = app.view?.code ?? ""; app.Toast(Loc.T("الكود اتنسخ")); });
        ((RectTransform)copy.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, 34), new Vector2(100, -43));
        var share = Ui.Button(lobby, "شارك", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 14, () => { Sfx.Tap(); Share(app.view); });
        ((RectTransform)share.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, 34), new Vector2(176, -43));
        lobbySeats = Ui.Rect("Seats", lobby).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(390, 150), new Vector2(0, -86));
        lobbyNote = Ui.Label(lobby, "", 12, new Color(1, 1, 1, 0.7f));
        lobbyNote.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 20), new Vector2(0, 58));
        var st = Ui.Button(lobby, "ابدأ اللعب", Ui.Gold, Ui.Ink, 18, () => { Sfx.Tap(); app.StartRoom(); });
        ((RectTransform)st.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 42), new Vector2(60, 12));
        startButton = st.gameObject;
        var lv = Ui.Button(lobby, "خروج", Ui.Panel, Ui.GoldLight, 15, () => { Sfx.Tap(); app.Leave(); });
        ((RectTransform)lv.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(110, 42), new Vector2(-112, 12));
    }

    void RenderLobby(TableView v)
    {
        bool host = v.Me != null && v.Me.host;
        bool full = true;
        var key = new System.Text.StringBuilder(v.id + "|" + Loc.En);
        for (int i = 0; i < v.seats.Count; i++) { var s = v.seats[i]; key.Append(s == null ? "-" : s.name + s.bot + s.ch); if (s == null) full = false; }
        lobbyCode.text = (v.code ?? "").ToUpperInvariant();
        startButton.SetActive(host);
        startButton.GetComponent<Button>().interactable = full;
        lobbyNote.Set(host ? (full ? Loc.T("كله جاهز، ابدأ") : Loc.T("ابعت الكود لصحابك، أو حط الكمبيوتر مكانهم")) : Loc.T("مستنيين صاحب الغرفة يبدأ"));
        if (key.ToString() == lobbyKey) return;
        lobbyKey = key.ToString();
        for (int i = lobbySeats.childCount - 1; i >= 0; i--) Destroy(lobbySeats.GetChild(i).gameObject);
        for (int i = 0; i < v.seats.Count; i++)
        {
            var s = v.seats[i]; int seat = i;
            var row = Ui.Box(lobbySeats, "Seat", new Color(1, 1, 1, 0.06f));
            row.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(390, 32), new Vector2(0, -i * 37));
            string name = s == null ? Loc.T("مكان فاضي…") : NameOf(v, i) + (s.bot ? "  ·  " + Loc.T("كمبيوتر") : "") + (s.host ? "  ·  " + Loc.T("صاحب الغرفة") : "");
            var lt = Ui.Label(row.transform, name, 14, s == null ? new Color(1, 1, 1, 0.5f) : Color.white, Loc.En ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, Ui.Bold);
            lt.rectTransform.Fill(10);
            if (s == null && host)
            {
                var b = Ui.Button(row.transform, "+ كمبيوتر", Ui.Gold, Ui.Ink, 12, () => { Sfx.Tap(); app.AddBot(seat); });
                ((RectTransform)b.transform).Place(new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(92, 26), new Vector2(Loc.En ? -4 : 4, 0));
            }
        }
    }

    /// Android's share sheet with an invitation and the code (elsewhere: copy it).
    void Share(TableView v)
    {
        if (v == null || string.IsNullOrEmpty(v.code)) return;
        string text = (Loc.En ? "Join my table in Board Battles! Room code: " : "تعالى العب معايا في بورد باتلز! كود الغرفة: ") + v.code.ToUpperInvariant();
        GUIUtility.systemCopyBuffer = v.code;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            using (var cls = new AndroidJavaClass("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", cls.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", cls.GetStatic<string>("EXTRA_TEXT"), text);
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var chooser = cls.CallStatic<AndroidJavaObject>("createChooser", intent, "Board Battles"))
                    activity.Call("startActivity", chooser);
            }
            return;
        }
        catch (Exception e) { Debug.LogWarning("[share] " + e.Message); }
#endif
        app.Toast(Loc.T("الكود اتنسخ"));
    }

    public static string Short(long n) => n >= 1000000 ? (n / 1000000f).ToString("0.#") + "M" : n >= 1000 ? (n / 1000f).ToString("0.#") + "K" : n.ToString();

    /* ---------- searching ---------- */

    void BuildQueue()
    {
        var card = Ui.Box(root, "Queue", new Color(0.1f, 0.06f, 0.04f, 0.95f));
        queuePanel = card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340, 170), Vector2.zero);
        queueText = Ui.Label(queuePanel, "بندوّر على لاعبين…", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        queueText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(320, 34), new Vector2(0, -18));
        queueSub = Ui.Label(queuePanel, "", 15, Color.white);
        queueSub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(320, 26), new Vector2(0, -60));
        var c = Ui.Button(queuePanel, "إلغاء", Ui.Panel, Ui.GoldLight, 17, () => app.CancelQueue());
        ((RectTransform)c.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(140, 40), new Vector2(0, 16));
    }

    public void ShowQueue(Dictionary<string, object> q)
    {
        if (q == null) { queuePanel.gameObject.SetActive(false); return; }
        queuePanel.gameObject.SetActive(true);
        double botsAt = q.Num("botsAt"), now = q.Num("now");
        botsAtLocal = botsAt > 0 ? Time.time + (float)((botsAt - now) / 1000.0) : -1;
        queueSub.Set($"{(int)q.Num("count")} / {(int)q.Num("need")}");
    }

    /* ---------- at the table ---------- */

    void BuildHud()
    {
        hud = Ui.Rect("Hud", root).Fill();
        var leave = Ui.Button(hud, "خروج", new Color(0.1f, 0.06f, 0.04f, 0.85f), Ui.GoldLight, 15, () => app.Leave());
        ((RectTransform)leave.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, 34), new Vector2(12, -10));

        var sb = Ui.Box(hud, "Scores", new Color(0.1f, 0.06f, 0.04f, 0.85f));
        // the scores sit top left, beside "leave", so the player across the table is never covered
        sb.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, 34), new Vector2(100, -10));
        scoreText = Ui.Label(sb.transform, "", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        scoreText.rectTransform.Fill(6);
        roundText = Ui.Label(hud, "", 13, Ui.GoldLight, TextAnchor.MiddleRight);
        roundText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(200, 24), new Vector2(-14, -14));

        for (int p = 0; p < 4; p++)
        {
            var bg = Ui.Box(hud, "Seat" + p, new Color(0.1f, 0.06f, 0.04f, 0.8f));
            bg.rectTransform.Place(new Vector2(0, 0), new Vector2(0.5f, 0.5f), new Vector2(130, 30), Vector2.zero);
            seatTagBg[p] = bg;
            seatTags[p] = Ui.Label(bg.transform, "", 13, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
            seatTags[p].rectTransform.Fill(4);
            bg.gameObject.SetActive(false);
        }

        // your turn / whose turn, the timer, your hand
        turnText = Ui.Label(hud, "", 17, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        turnText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(500, 26), new Vector2(0, 104));
        turnText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);
        var tb = Ui.Box(hud, "Timer", new Color(1, 1, 1, 0.15f));
        tb.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 5), new Vector2(0, 96));
        timerFill = Ui.Box(tb.transform, "Fill", Ui.Gold);
        var tf = timerFill.rectTransform; tf.anchorMin = Vector2.zero; tf.anchorMax = Vector2.one; tf.offsetMin = tf.offsetMax = Vector2.zero; tf.pivot = new Vector2(0, 0.5f);
        handBar = Ui.Rect("Hand", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(760, 86), new Vector2(0, 6));

        // voice chat: turn the mic on (then tap to mute and unmute)
        var mic = Ui.Button(hud, "", new Color(0.1f, 0.06f, 0.04f, 0.85f), Color.white, 10, ToggleMic);
        ((RectTransform)mic.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(46, 46), new Vector2(-12, -44));
        micIcon = Ui.Picture(mic.transform, "Mic", Menus.Icon("micoff")); micIcon.rectTransform.Fill(9); micIcon.color = Ui.GoldLight;
        micButton = mic.GetComponent<Image>();

        // tawla and ludo: throw the dice
        var roll = Ui.Button(hud, "ارمي الزهر", Ui.Gold, Ui.Ink, 20, () => { app.boardGame.Roll(); });
        ((RectTransform)roll.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(170, 56), new Vector2(-18, 22));   // at the side, clear of the board
        roll.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.4f);
        rollButton = roll.gameObject; rollButton.SetActive(false);

        endButtons = Ui.Rect("Ends", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 40), new Vector2(0, 136));
        var bl = Ui.Button(endButtons, "شمال", Ui.Gold, Ui.Ink, 16, () => app.PlayEnd('L'));
        ((RectTransform)bl.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(140, 38), Vector2.zero);
        endL = bl.GetComponentInChildren<Text>();
        var br = Ui.Button(endButtons, "يمين", Ui.Gold, Ui.Ink, 16, () => app.PlayEnd('R'));
        ((RectTransform)br.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(140, 38), Vector2.zero);
        endR = br.GetComponentInChildren<Text>();
        endButtons.gameObject.SetActive(false);

        // the end of a round
        var rp = Ui.Box(hud, "Round", new Color(0.1f, 0.06f, 0.04f, 0.93f));
        resultPanel = rp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 130), new Vector2(0, 30));
        resultTitle = Ui.Label(resultPanel, "", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        resultTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(360, 34), new Vector2(0, -16));
        resultSub = Ui.Label(resultPanel, "", 15, Color.white);
        resultSub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(360, 60), new Vector2(0, -56));
        resultPanel.gameObject.SetActive(false);

        // the end of the match
        var mp = Ui.Box(hud, "Match", new Color(0.1f, 0.06f, 0.04f, 0.96f));
        matchPanel = mp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 220), new Vector2(0, 10));
        mp.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        matchTitle = Ui.Label(matchPanel, "", 26, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        matchTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 40), new Vector2(0, -16));
        matchSub = Ui.Label(matchPanel, "", 15, Color.white);
        matchSub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 30), new Vector2(0, -60));
        payoutText = Ui.Label(matchPanel, "", 20, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        payoutText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 30), new Vector2(0, -96));
        var again = Ui.Button(matchPanel, "العب تاني", Ui.Gold, Ui.Ink, 17, () => app.PlayAgain());
        ((RectTransform)again.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 44), new Vector2(90, 18));
        var home = Ui.Button(matchPanel, "خروج", Ui.Panel, Ui.GoldLight, 17, () => app.Leave());
        ((RectTransform)home.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 44), new Vector2(-90, 18));
        matchPanel.gameObject.SetActive(false);
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        if (!on) { matchPanel.gameObject.SetActive(false); resultPanel.gameObject.SetActive(false); lobby.gameObject.SetActive(false); handKey = ""; lobbyKey = ""; }
        else ShowMic();
    }

    public void ShowPayout(string kind, double delta)
    {
        payoutText.Set(delta > 0 ? "+" + Short((long)delta) + Loc.T(" عملة") : delta < 0 ? "−" + Short((long)-delta) + Loc.T(" عملة") : "");
        payoutText.color = delta >= 0 ? new Color(0.6f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.5f);
    }

    static readonly string[] CharAr = { "عم فتحي", "كابتن سامي", "زيكو", "منى", "ندى", "الحاج محمود" };
    static readonly string[] CharEn = { "Am Fathi", "Captain Samy", "Ziko", "Mona", "Nada", "Hajj Mahmoud" };
    /// Mona and Nada (and players who picked them) are spoken of as women.
    static bool Female(TableView v, int seat) => seat >= 0 && seat < v.seats.Count && v.seats[seat] != null && (v.seats[seat].ch == 3 || v.seats[seat].ch == 4);

    /// The game's sentences about a player, said the way people talk (you / him / her), in both languages.
    public static string Say(TableView v, int seat, string what)
    {
        bool you = seat == v.you, f = Female(v, seat);
        string n = NameOf(v, seat);
        if (Loc.En)
            switch (what)
            {
                case "turn": return you ? "Your turn" : n + "'s turn";
                case "pass": return you ? "You passed" : n + " passed";
                case "nomove": return you ? "You have no move" : n + " has no move";
                case "round": return you ? "You won the round!" : n + " won the round";
                case "match": return you ? "Congrats! You won the match" : n + " won the match";
            }
        switch (what)
        {
            case "turn": return you ? "دورك" : "دور " + n;
            case "pass": return you ? "قلت باص" : n + (f ? " قالت باص" : " قال باص");
            case "nomove": return you ? "مالكش لعب" : n + (f ? " مالهاش لعب" : " مالوش لعب");
            case "round": return you ? "كسبت الجولة!" : n + (f ? " كسبت الجولة" : " كسب الجولة");
            case "match": return you ? "مبروك! كسبت الماتش" : n + (f ? " كسبت الماتش" : " كسب الماتش");
        }
        return n;
    }
    static string TeamSay(bool mine, bool match) => Loc.En
        ? (mine ? (match ? "Congrats! Your team won the match" : "Your team won the round!") : (match ? "The other team won the match" : "The other team won the round"))
        : (mine ? (match ? "مبروك! فريقك كسب الماتش" : "فريقك كسب الجولة!") : (match ? "الفريق التاني كسب الماتش" : "الفريق التاني كسب الجولة"));

    /// A seat's name; computer players have none, so they go by their character (as in the web game).
    public static string NameOf(TableView v, int seat)
    {
        if (seat == v.you) return Loc.T("أنت");
        if (seat < 0 || seat >= v.seats.Count || v.seats[seat] == null) return "";
        var s = v.seats[seat];
        if (!string.IsNullOrEmpty(s.name)) return s.name;
        int ch = Mathf.Clamp(s.ch, 0, CharAr.Length - 1);
        return Loc.En ? CharEn[ch] : CharAr[ch];
    }

    public void Render(TableView v, int sel, List<char> choose)
    {
        lobby.gameObject.SetActive(v.phase == "lobby");
        if (v.phase == "lobby") { RenderLobby(v); turnText.Set(""); rollButton.SetActive(false); }
        if (v.Board) { RenderBoard(v); return; }
        rollButton.SetActive(false);
        // scores: each player, or the two teams
        var parts = new List<string>();
        if (v.mode == "team")
        {
            int mine = v.you >= 0 ? v.TeamOf(v.you) : 0;
            parts.Add(Loc.T("فريقك ") + (v.scores.Count > mine ? v.scores[mine] : 0));
            parts.Add(Loc.T("هم ") + (v.scores.Count > 1 - mine ? v.scores[1 - mine] : 0));
        }
        else for (int s = 0; s < v.seats.Count; s++) parts.Add(NameOf(v, s) + " " + (v.scores.Count > s ? v.scores[s] : 0));
        scoreText.Set(string.Join("   ·   ", parts));
        roundText.Set(Loc.T("الجولة ") + v.round + Loc.T("  ·  إلى ") + v.target + (v.stake > 0 ? Loc.T("  ·  رهان ") + Short(v.stake) : ""));

        // whose turn
        string msg = "";
        if (v.phase == "play")
        {
            if (v.MyTurn) msg = choose != null && choose.Count > 1 ? Loc.T("الحجر ينفع في الناحيتين، دوس على ناحية أو اسحبه لها") : v.open >= 0 && v.line.Empty ? Loc.T("افتح بالـ ") + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1] : Loc.T("دورك");
            else if (v.turn >= 0) msg = Say(v, v.turn, "turn");
            if (v.last != null && v.last.t == "pass" && v.last.s != v.you && v.last.s >= 0) msg = Say(v, v.last.s, "pass");
        }
        turnText.Set(msg);

        // the two ends, when the chosen tile fits both
        bool two = sel >= 0 && choose != null && choose.Count > 1;
        endButtons.gameObject.SetActive(two);
        if (two) { endL.Set(Loc.T("شمال · ") + Domino.LeftVal(v.line)); endR.Set(Loc.T("يمين · ") + Domino.RightVal(v.line)); }

        Hand(v, sel);

        // round over / match over
        bool rend = v.phase == "rend" && v.result != null, mend = v.phase == "mend" && v.match != null;
        resultPanel.gameObject.SetActive(rend);
        if (rend)
        {
            var r = v.result;
            string title = r.winner < 0 ? Loc.T("تعادل، محدش سجّل") : v.mode == "team" ? TeamSay(v.you >= 0 && r.team == v.TeamOf(v.you), false) : Say(v, r.winner, "round");
            resultTitle.Set(title);
            resultSub.Set((r.blocked ? Loc.T("اللعبة مقفولة  ·  ") : "") + (r.points > 0 ? "+" + r.points + Loc.T(" نقطة") : ""));
        }
        matchPanel.gameObject.SetActive(mend);
        if (mend)
        {
            var m = v.match;
            bool won = v.you >= 0 && m.team == v.TeamOf(v.you);
            matchTitle.Set(m.team < 0 ? Loc.T("تعادل") : v.mode == "team" ? TeamSay(won, true) : Say(v, m.team, "match"));
            matchSub.Set(string.Join("  ·  ", m.scores));
        }
    }

    static readonly Dictionary<string, string> BoardModeNames = new Dictionary<string, string>
    {
        ["tawla"] = "طاولة عادة", ["mahbousa"] = "محبوسة", ["tawla31"] = "٣١", ["gulbahar"] = "جلبهار",
        ["chess"] = "شطرنج", ["chess1"] = "شطرنج", ["chess3"] = "شطرنج", ["chess10"] = "شطرنج",
        ["ludo2"] = "لودو", ["ludo3"] = "لودو", ["ludo4"] = "لودو",
    };

    /// Chess, tawla and ludo: names and scores, whose turn, the dice button, and the results.
    void RenderBoard(TableView v)
    {
        var parts = new List<string>();
        for (int s = 0; s < v.seats.Count; s++) parts.Add(NameOf(v, s) + " " + (v.scores.Count > s ? v.scores[s] : 0));
        scoreText.Set(string.Join("   ·   ", parts));
        string modeName = BoardModeNames.TryGetValue(v.mode, out var mn) ? Loc.T(mn) : v.mode;
        roundText.Set(v.game == "tawla" ? modeName + "  ·  " + Loc.T("إلى") + " " + v.target + (v.stake > 0 ? Loc.T("  ·  رهان ") + Short(v.stake) : "")
            : modeName + (v.stake > 0 ? Loc.T("  ·  رهان ") + Short(v.stake) : ""));
        string msg = "";
        if (v.phase == "play")
        {
            if (v.MyTurn) msg = app.boardGame.TurnHint() ?? Loc.T("دورك");
            else if (v.turn >= 0) msg = Say(v, v.turn, "turn");
            if (!v.MyTurn && v.last != null && v.last.t == "pass" && v.last.s >= 0 && v.last.s != v.you) msg = Say(v, v.last.s, "nomove");
        }
        turnText.Set(msg);
        rollButton.SetActive(app.boardGame.NeedsRoll());
        endButtons.gameObject.SetActive(false);
        if (handKey != "board") { handKey = "board"; for (int i = handBar.childCount - 1; i >= 0; i--) Destroy(handBar.GetChild(i).gameObject); }
        // a game ended (tawla plays several to a target; chess and ludo are one game)
        bool rend = v.phase == "rend" && v.endWinner >= -1 && v.raw != null && v.raw.Obj("result") != null, mend = v.phase == "mend" && v.match != null;
        resultPanel.gameObject.SetActive(rend);
        if (rend)
        {
            resultTitle.Set(v.endWinner < 0 ? Loc.T("تعادل") : Say(v, v.endWinner, "round"));
            resultSub.Set((v.endKind == "mars" ? Loc.T("مارس!") + "  ·  " : "") + (v.endPoints > 0 ? "+" + v.endPoints + Loc.T(" نقطة") : ""));
        }
        matchPanel.gameObject.SetActive(mend);
        if (mend)
        {
            var m = v.match;
            bool won = v.you >= 0 && m.team == v.you;
            string kind = (v.endKind == "checkmate" || v.endKind == "mate") ? Loc.T("كش مات") : v.endKind == "stalemate" ? Loc.T("بات (تعادل)") : v.endKind == "time" ? Loc.T("الوقت خلص") : "";
            matchTitle.Set(m.team < 0 ? Loc.T("تعادل") : Say(v, m.team, "match"));
            matchSub.Set((kind != "" ? kind + "  ·  " : "") + string.Join("  ·  ", m.scores));
        }
    }

    /// A chess clock: what's left for a seat now (it runs only for the player to move).
    string Clock(TableView v, int seat)
    {
        if (v.clocks == null || seat >= v.clocks.Count) return "";
        double left = v.clocks[seat] - (v.phase == "play" && v.turn == seat ? app.ServerNow() - v.now : 0);
        int sec = Mathf.Max(0, Mathf.CeilToInt((float)(left / 1000)));
        return (sec / 60) + ":" + (sec % 60).ToString("00");
    }

    void Hand(TableView v, int sel)
    {
        string key = string.Join(",", v.hand) + "|" + sel + "|" + v.MyTurn + "|" + v.line.L.Count + "," + v.line.R.Count + "," + v.line.f + "|" + v.open;
        if (key == handKey) return;
        handKey = key;
        for (int i = handBar.childCount - 1; i >= 0; i--) Destroy(handBar.GetChild(i).gameObject);
        int n = v.hand.Count;
        const float w = 44, h = 84, gap = 8;
        float x0 = -(n * w + (n - 1) * gap) / 2 + w / 2;
        for (int i = 0; i < n; i++)
        {
            int id = v.hand[i];
            bool fits = v.MyTurn && (v.open >= 0 && v.line.Empty ? id == v.open : Domino.EndsFor(v.line, id).Count > 0);
            var bg = Ui.Box(handBar, "Tile", Color.white);
            bg.color = TileArt.Ivory;
            float lift = id == sel ? 22 : fits ? 8 : 0;
            bg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(w, h), new Vector2(x0 + i * (w + gap), lift));
            var face = Ui.Picture(bg.transform, "Face", TileArt.FaceTexture(Domino.Tiles[id][0], Domino.Tiles[id][1]));
            face.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(h - 6, w - 6), Vector2.zero);
            face.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            if (v.MyTurn && !fits) { bg.color = new Color(0.62f, 0.6f, 0.55f); face.color = new Color(0.65f, 0.65f, 0.65f); }
            if (id == sel) bg.gameObject.AddComponent<Outline>().effectColor = Ui.Gold;
            var b = bg.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => app.TapTile(id));
            if (fits)
            {
                // or drag it onto the table, next to the end it should go on
                var drag = bg.gameObject.AddComponent<UiDrag>();
                drag.home = handBar;
                drag.onDrop = p => app.DropTile(id, p);
            }
        }
    }

    void Update()
    {
        if (queuePanel.gameObject.activeSelf)
        {
            float left = botsAtLocal - Time.time;
            queueText.Set(left > 0 ? Loc.T("الكمبيوتر هيدخل يلعب بعد ") + Mathf.CeilToInt(left) + Loc.T(" ث") : Loc.T("بندوّر على لاعبين…"));
        }
        var v = app.view;
        if (!hud.gameObject.activeSelf || v == null) return;
        // the turn timer
        float ms = (float)(v.turnEndsAt - app.ServerNow());
        timerFill.transform.parent.gameObject.SetActive(v.phase == "play" && v.turnMs > 0);
        timerFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ms / (float)v.turnMs), 1, 1);
        timerFill.color = ms < 5000 ? new Color(1f, 0.45f, 0.35f) : Ui.Gold;
        // chess: your own clock, ticking
        if (v.Board && v.clocks != null && v.you >= 0) roundText.Set(Loc.T("وقتك") + "  " + Clock(v, v.you));
        // names over the players
        var table = app.dominoTable;
        var cam = app.cafe.cam;
        for (int pos = 0; pos < 4; pos++)
        {
            int seat = table.posToSeat[pos];
            bool show = pos > 0 && seat >= 0 && seat < v.seats.Count && v.seats[seat] != null;
            seatTagBg[pos].gameObject.SetActive(show);
            if (!show) continue;
            var s = v.seats[seat];
            var sp = cam.WorldToScreenPoint(table.HeadOfSeat(seat));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out var local);
            var rt = seatTagBg[pos].rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            // on screen, and clear of the top bar
            float halfH = root.rect.height / 2, halfW = root.rect.width / 2;
            local.y = Mathf.Min(local.y, halfH - 66); local.x = Mathf.Clamp(local.x, -halfW + 70, halfW - 70);
            rt.anchoredPosition = local;
            seatTags[pos].Set(v.Board ? NameOf(v, seat) + (v.clocks != null ? "  ·  " + Clock(v, seat) : "") : NameOf(v, seat) + "  ·  " + s.count);
            bool active = v.phase == "play" && v.turn == seat;
            seatTagBg[pos].color = active ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f) : new Color(0.1f, 0.06f, 0.04f, 0.8f);
            seatTags[pos].color = active ? Ui.Ink : Color.white;
        }
    }
}
