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
    RectTransform picker, modeRow, friendPanel, diffRow; Text pickerTitle, stakeText, pickerNote; GameObject stakeRow;
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
    Image timerFill, turnChipBg; Text timerSecs; RectTransform turnChip;
    static Sprite ring, disc;
    /// A filled circle (the timer's dark face).
    public static Sprite Disc
    {
        get
        {
            if (disc != null) return disc;
            const int S = 96;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - S / 2f) * (x + 0.5f - S / 2f) + (y + 0.5f - S / 2f) * (y + 0.5f - S / 2f));
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(S / 2f - 0.5f - d + 0.5f) * 255));
                }
            t.SetPixels32(px); t.Apply();
            return disc = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }
    int lastBeep = -1;
    public QuickChat chat;

    /// A chat bubble by the player who sent it: over their name, or above your timer for you.
    public void ShowEmote(TableView v, int seat, string k)
    {
        var tex = QuickChat.Emoji(k);
        if (tex == null) return;
        Vector2 at;
        if (seat == v.you) at = new Vector2(-root.rect.width / 2 + 120, -root.rect.height / 2 + (v.Board ? 150 : 200));
        else
        {
            int pos = System.Array.IndexOf(app.dominoTable.posToSeat, seat);
            if (pos <= 0 || !seatTagBg[pos].gameObject.activeSelf) at = new Vector2(0, root.rect.height / 2 - 110);
            else at = seatTagBg[pos].rectTransform.anchoredPosition + new Vector2(0, 40);
        }
        bool words = QuickChat.Phrases.TryGetValue(k, out var ph);
        var bub = Ui.Box(root, "Bubble", new Color(1, 1, 1, 0.96f)).rectTransform;
        bub.anchorMin = bub.anchorMax = new Vector2(0.5f, 0.5f);
        bub.sizeDelta = new Vector2(words ? 160 : 56, 46); bub.anchoredPosition = at;
        var e = Ui.Picture(bub, "E", tex); e.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(36, 36), new Vector2(words ? 6 : 10, 0));
        if (words) Ui.Label(bub, Loc.En ? ph.en : ph.ar, 14, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(112, 40), new Vector2(-6, 0));
        StartCoroutine(Pop(bub));
    }

    System.Collections.IEnumerator Pop(RectTransform r)
    {
        var g = r.gameObject.AddComponent<CanvasGroup>(); g.blocksRaycasts = false;
        for (float t = 0; t < 2.8f; t += Time.deltaTime)
        {
            if (r == null) yield break;
            r.localScale = Vector3.one * (t < 0.2f ? Ui.Back(t / 0.2f) : 1);
            g.alpha = t > 2.3f ? 1 - (t - 2.3f) / 0.5f : 1;
            yield return null;
        }
        if (r != null) Destroy(r.gameObject);
    }
    RawImage viewIcon;
    /// The button shows the view you'd switch to.
    public void ShowViewIcon() { if (viewIcon != null) viewIcon.texture = Menus.Icon(Cafe.BoardView ? "scene" : "view"); }
    /// A thin circle, for the turn timer.
    static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int S = 96; const float R = 44, W = 5.5f;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - S / 2f) * (x + 0.5f - S / 2f) + (y + 0.5f - S / 2f) * (y + 0.5f - S / 2f));
                    float a = Mathf.Clamp01(W / 2 - Mathf.Abs(d - (R - W / 2)) + 0.5f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return ring = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
    }
    readonly Text[] seatTags = new Text[4];
    readonly Image[] seatTagBg = new Image[4];
    readonly RawImage[] seatCpu = new RawImage[4];
    // each player's title, under their name tag
    readonly RectTransform[] seatTitle = new RectTransform[4];
    readonly string[] seatTitleId = new string[4];
    readonly int[] seatTitleVer = new int[4];
    // the end of a match: play again with new people, the same people (rematch), or the tournament
    GameObject againButton, homeButton, rematchButton, shareButton; Text againText, rematchText;
    RectTransform rulesButton; Text rulesText;
    string rematchFor = ""; bool rematchGone; int rematchVotes, rematchNeed; bool rematchMine;
    string celebrated = "";
    Text endL, endR;
    GameObject rollButton, undoButton;
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
        ["tawla"] = new[] { ("tawla", "عادة"), ("mahbousa", "محبوسة"), ("tawla31", "٣١"), ("gulbahar", "جلبهار") },
        ["chess"] = new[] { ("chess1", "دقيقة"), ("chess3", "٣ دقايق"), ("chess", "٥ دقايق"), ("chess10", "١٠ دقايق") },
        ["ludo"] = new[] { ("ludo2", "٢ لاعبين"), ("ludo3", "٣ لاعبين"), ("ludo4", "٤ لاعبين"), ("ludoq2", "سريع ٢"), ("ludoq4", "سريع ٤"), ("ludom2", "ماستر ٢"), ("ludom4", "ماستر ٤") },
    };
    public static int Players(string mode) =>
        mode == "ffa3" || mode == "ludo3" ? 3 : mode == "ffa4" || mode == "team" || mode.EndsWith("4") ? 4 : 2;

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
        // how well the computer plays in practice
        diffRow = Ui.Rect("Level", picker).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(380, 28), new Vector2(0, -144));
        var dl = Ui.Label(diffRow, "مستوى الكمبيوتر", 12, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, Ui.Bold);
        dl.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110, 26), new Vector2(Loc.En ? -128 : 128, 0));
        string[] levels = { "سهل", "عادي", "صعب" };
        for (int i = 0; i < 3; i++)
        {
            int lv = i;
            var lb = Ui.Button(diffRow, levels[i], Ui.Panel, Ui.GoldLight, 13, () => { Sfx.Tap(); OfflineGame.Level = lv; RefreshPicker(); });
            ((RectTransform)lb.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(72, 26), new Vector2((Loc.En ? i - 0.6f : 0.6f - i) * 78 + (Loc.En ? 40 : -40), 0));
            lb.name = "Level " + i;
        }
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
        // up to four in a row; a second row when there are more (ludo's classic, quick and master)
        const int PerRow = 4;
        int rows = (modes.Length + PerRow - 1) / PerRow;
        picker.sizeDelta = new Vector2(400, 326 + (rows - 1) * 40);
        picker.anchoredPosition = new Vector2(-40, rows > 1 ? 2 : -12);
        diffRow.anchoredPosition = new Vector2(0, -144 - (rows - 1) * 40);
        modeRow.sizeDelta = new Vector2(380, rows * 40);
        ((RectTransform)stakeRow.transform).anchoredPosition = new Vector2(0, -98 - (rows - 1) * 40);
        for (int i = 0; i < modes.Length; i++)
        {
            var m = modes[i].mode;
            int row = i / PerRow, inRow = Mathf.Min(PerRow, modes.Length - row * PerRow), col = i % PerRow;
            float w = Mathf.Min(120, 372f / inRow - 6);
            int k = Loc.En ? col : inRow - 1 - col;
            var b = Ui.Button(modeRow, modes[i].name, Ui.Panel, Ui.GoldLight, 14, () => { Sfx.Tap(); mode = m; PlayerPrefs.SetString("mode." + game, m); RefreshPicker(); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w, 34), new Vector2((k - (inRow - 1) / 2f) * (w + 6), -2 - row * 40));
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
        foreach (Transform t in diffRow)
        {
            if (!t.name.StartsWith("Level ")) continue;
            bool on = t.name == "Level " + OfflineGame.Level;
            t.GetComponent<Image>().color = on ? Ui.Gold : Ui.Panel;
            t.GetComponentInChildren<Text>().color = on ? Ui.Ink : Ui.GoldLight;
        }
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
        lobbyCode.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(160, 40), new Vector2(18, -40));
        var copy = Ui.Button(lobby, "انسخ", Ui.Panel, Ui.GoldLight, 14, () => { Sfx.Tap(); GUIUtility.systemCopyBuffer = app.view?.code ?? ""; app.Toast(Loc.T("الكود اتنسخ")); });
        ((RectTransform)copy.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, 34), new Vector2(100, -43));
        var share = Ui.Button(lobby, "شارك", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 14, () => { Sfx.Tap(); Share(app.view); });
        ((RectTransform)share.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(70, 34), new Vector2(176, -43));
        // house rule for dominoes: with drawing (the usual) or the block game (من غير سحب); the host picks
        var rules = Ui.Button(lobby, "", Ui.Panel, Ui.GoldLight, 12, () =>
        {
            var v = app.view; if (v == null || v.game != "domino" || v.Me == null || !v.Me.host) return;
            Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "setup", ["block"] = !v.block });
        });
        rulesButton = ((RectTransform)rules.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, 26), new Vector2(-120, -42));
        rulesText = rules.GetComponentInChildren<Text>();
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
        rulesButton.gameObject.SetActive(v.game == "domino");
        rulesText.Set(Loc.T(v.block ? "من غير سحب" : "بالسحب") + (host ? "  ⇄" : ""));
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
        string link = app.net != null ? app.net.HttpBase + "/join/" + v.code.ToUpperInvariant() : "";
        string text = (Loc.En ? "Play Board Battles with me! Tap to join my table: " : "تعالى العب معايا بورد باتلز! دوس هنا وادخل ترابيزتي: ") + link
            + (Loc.En ? "\nRoom code: " : "\nكود الغرفة: ") + v.code.ToUpperInvariant();
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
            // a computer player is marked with a chip
            seatCpu[p] = Ui.Picture(bg.transform, "Cpu", Menus.Icon("cpu"));
            seatCpu[p].rectTransform.Place(new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(18, 18), new Vector2(-3, 0));
            seatCpu[p].color = new Color(1, 1, 1, 0.8f);
            // tap a player: their card (stats, add friend, mute, report)
            int pos = p;
            bg.gameObject.AddComponent<Button>().onClick.AddListener(() => { int seat = app.dominoTable.posToSeat[pos]; if (seat >= 0) { Sfx.Tap(); app.menus.OpenCardSeat(seat); } });
            bg.gameObject.SetActive(false);
        }

        // whose turn and the time left: a small card in the bottom corner, off the board — a ring that runs down
        // around the seconds, and a line saying whose turn it is (or what to do on yours)
        // a round timer on the left edge, clear of the board and the tiles, with whose turn it is under it
        turnChip = Ui.Rect("Turn", hud).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(64, 64), new Vector2(14, 34));
        var discGo = new GameObject("Disc", typeof(RectTransform), typeof(Image));
        discGo.transform.SetParent(turnChip, false);
        turnChipBg = discGo.GetComponent<Image>(); turnChipBg.sprite = Disc; turnChipBg.raycastTarget = false;
        turnChipBg.rectTransform.Fill();
        var track = Ui.Picture(turnChip, "Track", Ring.texture); track.color = new Color(1, 1, 1, 0.16f);
        track.rectTransform.Fill(4);
        var ringGo = new GameObject("Ring", typeof(RectTransform), typeof(Image));
        ringGo.transform.SetParent(turnChip, false);
        timerFill = ringGo.GetComponent<Image>();
        timerFill.sprite = Ring; timerFill.type = Image.Type.Filled; timerFill.fillMethod = Image.FillMethod.Radial360;
        timerFill.fillOrigin = (int)Image.Origin360.Top; timerFill.fillClockwise = false; timerFill.raycastTarget = false;
        timerFill.rectTransform.Fill(4);
        timerSecs = Ui.Label(turnChip, "", 20, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        timerSecs.rectTransform.Fill();
        turnText = Ui.Label(turnChip, "", 12, Ui.Gold, TextAnchor.UpperLeft, Ui.Bold);
        turnText.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(130, 34), new Vector2(2, -4));
        turnText.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
        turnText.horizontalOverflow = HorizontalWrapMode.Wrap; turnText.resizeTextMaxSize = 13; turnText.resizeTextMinSize = 9; turnText.resizeTextForBestFit = true;
        handBar = Ui.Rect("Hand", hud).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(760, 86), new Vector2(0, 6));

        // voice chat: turn the mic on (then tap to mute and unmute)
        var mic = Ui.Button(hud, "", new Color(0.1f, 0.06f, 0.04f, 0.85f), Color.white, 10, ToggleMic);
        ((RectTransform)mic.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(46, 46), new Vector2(-12, -44));
        micIcon = Ui.Picture(mic.transform, "Mic", Menus.Icon("micoff")); micIcon.rectTransform.Fill(9); micIcon.color = Ui.GoldLight;
        micButton = mic.GetComponent<Image>();

        // quick chat: phrases and emojis
        chat = gameObject.AddComponent<QuickChat>();
        chat.Build(app, hud);

        // the view: the whole scene, or just the board from above
        var vb = Ui.Button(hud, "", new Color(0.1f, 0.06f, 0.04f, 0.85f), Color.white, 10, () =>
        {
            Sfx.Tap();
            app.cafe.SetView(!Cafe.BoardView); app.dominoTable.ApplyView(); ShowViewIcon();
            app.Toast(Loc.T(Cafe.BoardView ? "عرض اللوح بس" : "عرض كامل"));
        });
        ((RectTransform)vb.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(46, 46), new Vector2(-12, -96));
        vb.gameObject.SetActive(false);   // one view for everyone now: straight down on the board
        viewIcon = Ui.Picture(vb.transform, "View", Menus.Icon("view")); viewIcon.rectTransform.Fill(10); viewIcon.color = Ui.GoldLight;
        ShowViewIcon();

        // tawla and ludo: throw the dice
        var roll = Ui.Button(hud, "ارمي الزهر", Ui.Gold, Ui.Ink, 20, () => { app.boardGame.Roll(); });
        ((RectTransform)roll.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(170, 56), new Vector2(-18, 22));   // at the side, clear of the board
        roll.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.4f);
        rollButton = roll.gameObject; rollButton.SetActive(false);
        // tawla: take back this turn's moves
        var undo = Ui.Button(hud, "رجّع", Ui.Panel, Ui.GoldLight, 17, () => app.boardGame.Undo());
        ((RectTransform)undo.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(120, 46), new Vector2(-18, 22));
        undo.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        undoButton = undo.gameObject; undoButton.SetActive(false);

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
        var again = Ui.Button(matchPanel, "العب تاني", Ui.Gold, Ui.Ink, 17, () =>
        {
            // in the tournament: back to the bracket; otherwise a new game
            if (app.view != null && app.menus.IsTourneyTable(app.view.id)) { app.Leave(); app.menus.OpenTourney(); }
            else app.PlayAgain();
        });
        ((RectTransform)again.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 44), new Vector2(90, 18));
        againButton = again.gameObject; againText = again.GetComponentInChildren<Text>();
        var home = Ui.Button(matchPanel, "خروج", Ui.Panel, Ui.GoldLight, 17, () => app.Leave());
        ((RectTransform)home.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 44), new Vector2(-90, 18));
        homeButton = home.gameObject;
        // the same people again (matchmade games)
        var re = Ui.Button(matchPanel, "ماتش تاني معاهم", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 15, () =>
        {
            if (rematchMine) return;
            Sfx.Tap(); rematchMine = true; app.Send(new Dictionary<string, object> { ["t"] = "rematch" }); ShowRematch();
        });
        ((RectTransform)re.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 44), Vector2.zero);
        rematchButton = re.gameObject; rematchText = re.GetComponentInChildren<Text>();
        // share the result (a picture of it, with your invite link)
        var sh = Ui.Button(matchPanel, "", new Color(1, 1, 1, 0.1f), Color.white, 10, ShareResult);
        ((RectTransform)sh.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(38, 38), new Vector2(-8, -8));
        var shi = Ui.Picture(sh.transform, "Share", Menus.Icon("share")); shi.rectTransform.Fill(8); shi.color = Ui.GoldLight;
        shareButton = sh.gameObject;
        matchPanel.gameObject.SetActive(false);
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        if (!on) { matchPanel.gameObject.SetActive(false); resultPanel.gameObject.SetActive(false); lobby.gameObject.SetActive(false); handKey = ""; lobbyKey = ""; }
        else ShowMic();
    }

    /* ---------- the end of a match: buttons, rematch, celebrating, sharing ---------- */

    void MatchEnded(TableView v, bool won)
    {
        bool tourney = app.menus.IsTourneyTable(v.id);
        bool canRematch = !app.Practicing && !v.isPrivate && !tourney && !app.Watching;
        againButton.SetActive(!app.Watching);   // an onlooker can only go home
        if (rematchFor != v.id) { rematchFor = v.id; rematchGone = false; rematchMine = false; rematchVotes = 0; rematchNeed = 0; }
        againText.Set(Loc.T(tourney ? "البطولة" : canRematch ? "لاعبين جداد" : "العب تاني"));
        // three buttons when a rematch is possible: leave · the same people · new people
        var a = (RectTransform)againButton.transform; var h = (RectTransform)homeButton.transform;
        if (canRematch && !rematchGone) { h.anchoredPosition = new Vector2(Loc.En ? -128 : 128, 18); h.sizeDelta = new Vector2(110, 44); a.anchoredPosition = new Vector2(Loc.En ? 128 : -128, 18); a.sizeDelta = new Vector2(120, 44); }
        else { h.anchoredPosition = new Vector2(Loc.En ? -90 : 90, 18); h.sizeDelta = new Vector2(150, 44); a.anchoredPosition = new Vector2(Loc.En ? 90 : -90, 18); a.sizeDelta = new Vector2(170, 44); }
        ((RectTransform)rematchButton.transform).anchoredPosition = new Vector2(0, 18);
        rematchButton.SetActive(canRematch && !rematchGone);
        ShowRematch();
        if (celebrated != v.id + ":match")
        {
            celebrated = v.id + ":match";
            // the winner's own celebration (a 'win' skin), seen by everyone at the table
            string style = null;
            if (v.match != null) for (int s = 0; s < v.seats.Count; s++) if (v.seats[s] != null && !v.seats[s].bot && v.TeamOf(s) == v.match.team) { style = v.seats[s].win; if (s == v.you) break; }
            if (won) { Celebrate(40, style); Growth.Won(); }
            else if (!string.IsNullOrEmpty(style) && style != "win_coins") Celebrate(26, style);
        }
    }

    void ShowRematch()
    {
        if (rematchText == null) return;
        string n = rematchNeed > 0 ? "  " + rematchVotes + "/" + rematchNeed : "";
        rematchText.Set(rematchMine ? Loc.T("مستنيهم") + n : Loc.T("ماتش تاني معاهم") + n);
    }

    /// The server: who asked for a rematch so far (and whether someone left).
    public void OnRematch(Dictionary<string, object> m)
    {
        var seats = m.Arr("seats");
        rematchVotes = seats != null ? seats.Count : 0; rematchNeed = (int)m.Num("need");
        if (m.Bool("gone") && !rematchGone) { rematchGone = true; app.Toast(Loc.T("حد من اللاعبين مشي")); if (app.view != null) MatchEnded(app.view, false); }
        else if (rematchVotes > 0 && !rematchMine) app.Toast(Loc.T("عايزين ماتش تاني معاك!"));
        ShowRematch();
    }

    /// A picture of the result with a line to go with it (and your invite link).
    void ShareResult()
    {
        var v = app.view; if (v == null || v.match == null) return;
        Sfx.Tap();
        bool won = v.you >= 0 && v.match.team == v.TeamOf(v.you);
        string game = Loc.T(App.GameNames.TryGetValue(v.game, out var gn) ? gn : "دومينو");
        string other = "";
        for (int i = 0; i < v.seats.Count; i++) if (i != v.you && v.seats[i] != null) { other = NameOf(v, i); break; }
        string score = string.Join("–", v.match.scores);
        string code = app.menus.MyCode;
        string link = Growth.InviteLink(code);
        string text = Loc.En
            ? (won ? "I beat " + other + " " + score + " at " + game + " in Board Battles! " : "What a game of " + game + " in Board Battles! ") + "Play with me (code " + code + ", 50K coins for you): " + link
            : (won ? "كسبت " + other + " " + score + " في ال" + game + " على بورد باتلز! " : "ماتش " + game + " جامد على بورد باتلز! ") + "تعالى العب معايا (كودي " + code + " وخد ٥٠ ألف كوين): " + link;
        app.StartCoroutine(Growth.ShareScreen(text, () => shareButton.SetActive(false), () => shareButton.SetActive(true)));
    }

    /// Coins raining down: a handful for a round, a shower for a match, more for a big win.
    public void Celebrate(int n, string style = null)
    {
        if (Perf.Saver) n = Mathf.Min(n, 14);
        style = string.IsNullOrEmpty(style) ? "win_coins" : style;
        if (style == "win_coins") Sfx.Coins(); else Sfx.Chime();
        var layer = Ui.Rect("Coins", root).Fill();
        layer.SetAsLastSibling();
        StartCoroutine(Rain(layer, n, style));
    }

    // one bit of a celebration: where it is, how it moves, how long it lives
    class Bit { public RectTransform r; public Graphic g; public Vector2 p, v; public float gravity, spin, delay, life, sway, phase; }

    /// A celebration: coins rain (the classic), confetti flutters, gold stars fall, fireworks burst, lanterns or balloons rise.
    System.Collections.IEnumerator Rain(RectTransform layer, int n, string style)
    {
        float W = root.rect.width, H = root.rect.height;
        var bits = new List<Bit>();
        Color[] party = { new Color(0.95f, 0.3f, 0.35f), new Color(0.3f, 0.75f, 0.45f), new Color(0.35f, 0.55f, 0.95f), Ui.Gold, new Color(0.9f, 0.45f, 0.85f), new Color(1f, 0.6f, 0.2f) };
        Bit Add(Texture tex, Color c, float size, bool round = true)
        {
            Graphic g;
            if (tex != null) { var pi = Ui.Picture(layer, "Bit", tex); pi.color = c; g = pi; }
            else { var bx = Ui.Box(layer, "Bit", c, round); bx.raycastTarget = false; g = bx; }
            g.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size, size), new Vector2(0, H));
            var b = new Bit { r = g.rectTransform, g = g, life = 3.2f, phase = UnityEngine.Random.value * 6f };
            bits.Add(b); return b;
        }
        float R(float a, float b) => UnityEngine.Random.Range(a, b);
        switch (style)
        {
            case "win_confetti":
                for (int i = 0; i < n * 2; i++) { var b = Add(null, party[i % party.Length], R(7, 12), false); b.p = new Vector2(R(-W / 2, W / 2), H / 2 + 20); b.v = new Vector2(R(-30, 30), R(-60, 0)); b.gravity = 160; b.spin = R(4, 9); b.delay = R(0, 1.2f); b.sway = R(30, 70); b.life = 4f; }
                break;
            case "win_stars":
                for (int i = 0; i < n; i++) { var b = Add(Menus.Icon("t_star"), Color.Lerp(Ui.Gold, Color.white, R(0, 0.4f)), R(16, 30)); b.p = new Vector2(R(-W / 2, W / 2), H / 2 + 20); b.v = new Vector2(R(-20, 20), R(-40, 0)); b.gravity = 260; b.spin = R(1, 3); b.delay = R(0, 1f); }
                break;
            case "win_fireworks":
                for (int burst = 0; burst < 5; burst++)
                {
                    var at = new Vector2(R(-W * 0.35f, W * 0.35f), R(0, H * 0.3f)); var c = party[burst % party.Length]; float d = burst * 0.45f;
                    for (int i = 0; i < Mathf.Max(10, n / 3); i++) { float ang = i * Mathf.PI * 2 / Mathf.Max(10, n / 3); var b = Add(Menus.Icon("t_star"), c, R(8, 13)); b.p = at; b.v = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R(140, 220); b.gravity = 120; b.delay = d; b.life = d + 1.6f; }
                }
                break;
            case "win_lanterns":
                for (int i = 0; i < Mathf.Max(8, n / 3); i++) { var b = Add(Menus.Icon("t_lantern"), party[(i * 2 + 3) % party.Length], R(26, 40)); b.p = new Vector2(R(-W / 2, W / 2), -H / 2 - 30); b.v = new Vector2(0, R(90, 150)); b.gravity = 0; b.sway = R(15, 30); b.delay = R(0, 1.4f); b.life = 4.5f; }
                break;
            case "win_balloons":
                for (int i = 0; i < Mathf.Max(10, n / 2); i++) { var b = Add(GameUi.Disc.texture, party[i % party.Length], R(26, 38)); b.r.sizeDelta = new Vector2(b.r.sizeDelta.x, b.r.sizeDelta.x * 1.2f); b.p = new Vector2(R(-W / 2, W / 2), -H / 2 - 30); b.v = new Vector2(0, R(110, 170)); b.sway = R(10, 25); b.delay = R(0, 1.2f); b.life = 4.2f; }
                break;
            default:   // coins
                for (int i = 0; i < n; i++) { var b = Add(Menus.Icon("coin"), Color.white, R(18, 30)); b.p = new Vector2(R(-W / 2, W / 2), H / 2 + 30); b.v = new Vector2(R(-40, 40), R(-60, 20)); b.gravity = 900; b.spin = R(6, 14); b.delay = R(0, 0.9f); b.life = 3.2f; }
                break;
        }
        foreach (var b in bits) b.r.anchoredPosition = new Vector2(0, H * 2);   // out of sight until its turn
        float end = 0; foreach (var b in bits) end = Mathf.Max(end, b.life);
        for (float t = 0; t < end && layer != null; t += Time.deltaTime)
        {
            foreach (var b in bits)
            {
                float k = t - b.delay; if (k < 0 || b.r == null) continue;
                var pos = b.p + b.v * k + new Vector2(0, -0.5f * b.gravity * k * k);
                if (b.sway > 0) pos.x += Mathf.Sin(k * 2.2f + b.phase) * b.sway;
                b.r.anchoredPosition = pos;
                if (b.spin > 0) b.r.localScale = new Vector3(Mathf.Cos(k * b.spin), 1, 1);
                float fade = Mathf.Clamp01((b.life - t) / 0.6f);
                var c = b.g.color; c.a = fade; b.g.color = c;
            }
            yield return null;
        }
        if (layer != null) Destroy(layer.gameObject);
    }

    public void ShowPayout(string kind, double delta)
    {
        if (delta > 0) Celebrate(Mathf.Clamp(16 + (int)(delta / 2500), 16, 80));
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

    // watching someone else's game: a banner with the way out
    RectTransform watchBanner;
    void ShowWatching(TableView v)
    {
        if (watchBanner == null)
        {
            var bg = Ui.Box(hud, "Watching", new Color(0.1f, 0.06f, 0.04f, 0.88f));
            watchBanner = bg.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 38), new Vector2(0, -60));
            bg.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
            var eye = Ui.Picture(watchBanner, "Eye", Menus.Icon("view")); eye.color = Ui.Gold;
            eye.rectTransform.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(22, 22), new Vector2(Loc.En ? 10 : -10, 0));
            var t = Ui.Label(watchBanner, "بتتفرج", 14, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150, 34), new Vector2(Loc.En ? -20 : 20, 0));
            var x = Ui.Button(watchBanner, "خروج", Ui.Gold, Ui.Ink, 13, () => { Sfx.Tap(); app.Leave(); });
            ((RectTransform)x.transform).Place(new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(76, 30), new Vector2(Loc.En ? -4 : 4, 0));
        }
        watchBanner.gameObject.SetActive(app.Watching);
    }

    public void Render(TableView v, int sel, List<char> choose)
    {
        ShowWatching(v);
        lobby.gameObject.SetActive(v.phase == "lobby");
        if (v.phase == "lobby") { RenderLobby(v); turnText.Set(""); rollButton.SetActive(false); undoButton.SetActive(false); }
        if (v.Board) { RenderBoard(v); return; }
        rollButton.SetActive(false); undoButton.SetActive(false);
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
            if (v.MyTurn) msg = choose != null && choose.Count > 1 ? Loc.T("دوس على المكان اللي بينوّر أو اسحب الحجر له") : v.open >= 0 && v.line.Empty ? Loc.T("افتح بالـ ") + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1] : Loc.T("دورك");
            else if (v.turn >= 0) msg = Say(v, v.turn, "turn");
            if (v.last != null && v.last.t == "pass" && v.last.s != v.you && v.last.s >= 0) msg = Say(v, v.last.s, "pass");
        }
        turnText.Set(msg);

        // the two ends, when the chosen tile fits both
        bool two = sel >= 0 && choose != null && choose.Count > 1;
        endButtons.gameObject.SetActive(false);   // no buttons: tap the glowing spot on the table, or drag the tile there
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
            MatchEnded(v, won);
        }
        else if (rend && v.result != null && v.you >= 0 && v.result.team == v.TeamOf(v.you) && celebrated != v.id + ":" + v.round) { celebrated = v.id + ":" + v.round; Celebrate(12); }
    }

    static readonly Dictionary<string, string> BoardModeNames = new Dictionary<string, string>
    {
        ["tawla"] = "طاولة عادة", ["mahbousa"] = "محبوسة", ["tawla31"] = "٣١", ["gulbahar"] = "جلبهار",
        ["chess"] = "شطرنج", ["chess1"] = "شطرنج", ["chess3"] = "شطرنج", ["chess10"] = "شطرنج",
        ["ludo2"] = "لودو", ["ludo3"] = "لودو", ["ludo4"] = "لودو",
        ["ludoq2"] = "لودو سريع", ["ludoq4"] = "لودو سريع", ["ludom2"] = "لودو ماستر", ["ludom4"] = "لودو ماستر",
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
        undoButton.SetActive(!app.boardGame.NeedsRoll() && app.boardGame.CanUndo);
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
            MatchEnded(v, won);
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
        // whose turn, and the time left
        float ms = (float)(v.turnEndsAt - app.ServerNow());
        bool timed = v.phase == "play" && v.turnMs > 0 && v.turnEndsAt > 0;
        turnChip.gameObject.SetActive(v.phase == "play" && !string.IsNullOrEmpty(turnText.text));
        timerFill.fillAmount = timed ? Mathf.Clamp01(ms / (float)v.turnMs) : 1;
        bool hurry = timed && ms < 5000;
        timerFill.color = hurry ? new Color(1f, 0.42f, 0.32f) : v.MyTurn ? Ui.Gold : new Color(1, 1, 1, 0.75f);
        int secs = timed ? Mathf.Max(0, Mathf.CeilToInt(ms / 1000f)) : -1;
        timerSecs.text = secs >= 0 ? secs.ToString() : "";
        // the last five seconds of your turn: a beep at five, a tick each second after
        if (v.MyTurn && secs >= 1 && secs <= 5 && secs != lastBeep) { if (secs == 5) { Sfx.Alert(); Sfx.Buzz(); } else Sfx.Tick(); }
        lastBeep = v.MyTurn ? secs : -1;
        timerSecs.color = hurry ? new Color(1f, 0.55f, 0.45f) : Color.white;
        turnChipBg.color = v.MyTurn ? new Color(0.26f, 0.17f, 0.05f, 0.95f) : new Color(0.08f, 0.05f, 0.03f, 0.88f);
        if (hurry && v.MyTurn) turnChip.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(Time.time * 12));
        else turnChip.localScale = Vector3.one;
        // chess: your own clock, ticking
        if (v.Board && v.clocks != null && v.you >= 0) roundText.Set(Loc.T("وقتك") + "  " + Clock(v, v.you));
        // names over the players
        var table = app.dominoTable;
        var cam = app.cafe.cam;
        for (int pos = 0; pos < 4; pos++)
        {
            int seat = table.posToSeat[pos];
            bool show = (pos > 0 || v.you < 0) && seat >= 0 && seat < v.seats.Count && v.seats[seat] != null;
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
            string extra = "";
            if (!s.bot && s.back > 0) extra = "  ·  " + Loc.T("بيرجع") + " " + Mathf.Max(0, Mathf.CeilToInt((float)((s.back - app.ServerNow()) / 1000))) + Loc.T("ث");
            else if (!s.bot && s.away) extra = "  ·  " + Loc.T("الكمبيوتر بيلعب مكانه");
            seatTags[pos].Set((v.Board ? NameOf(v, seat) + (v.clocks != null ? "  ·  " + Clock(v, seat) : "") : NameOf(v, seat) + "  ·  " + s.count) + extra);
            seatCpu[pos].gameObject.SetActive(s.bot);
            string tid = s.bot ? null : s.title;
            if (tid != seatTitleId[pos] || seatTitleVer[pos] != Titles.Version || (seatTitle[pos] == null && !string.IsNullOrEmpty(tid)))
            {
                if (seatTitle[pos] != null) Destroy(seatTitle[pos].gameObject);
                seatTitleId[pos] = tid; seatTitleVer[pos] = Titles.Version;
                seatTitle[pos] = Titles.Badge(seatTagBg[pos].transform, tid, 10);
                if (seatTitle[pos] != null) seatTitle[pos].Place(new Vector2(0.5f, 0), new Vector2(0.5f, 1), seatTitle[pos].sizeDelta, new Vector2(0, -3));
            }
            bool active = v.phase == "play" && v.turn == seat;
            seatTagBg[pos].color = active ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f) : new Color(0.1f, 0.06f, 0.04f, 0.8f);
            seatTags[pos].color = active ? Ui.Ink : Color.white;
        }
    }
}
