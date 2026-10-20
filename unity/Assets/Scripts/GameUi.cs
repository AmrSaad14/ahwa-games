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

    // picking a game
    RectTransform picker; Text pickerTitle, stakeText, pickerNote; readonly List<(string mode, Image bg, Text label)> modeButtons = new List<(string, Image, Text)>();
    GameObject quickButton, stakeRow;
    public string mode = "ffa2"; public int stakeIndex;

    // searching
    RectTransform queuePanel; Text queueText, queueSub; float botsAtLocal;

    // at the table
    RectTransform hud, handBar, endButtons, resultPanel, matchPanel;
    Text scoreText, roundText, turnText, resultTitle, resultSub, matchTitle, matchSub, payoutText;
    Image timerFill;
    readonly Text[] seatTags = new Text[4];
    readonly Image[] seatTagBg = new Image[4];
    Text endL, endR;
    string handKey = "";

    public void Build(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Game", 20);
        root = Ui.Rect("Root", canvas.transform).Fill();
        stakeIndex = Mathf.Clamp(PlayerPrefs.GetInt("ahwa.stake", 0), 0, Stakes.Length - 1);
        mode = PlayerPrefs.GetString("ahwa.mode", "ffa2");
        if (Array.IndexOf(DominoModes, mode) < 0) mode = "ffa2";
        BuildPicker(); BuildQueue(); BuildHud();
        picker.gameObject.SetActive(false); queuePanel.gameObject.SetActive(false); hud.gameObject.SetActive(false);
    }

    /* ---------- choosing the game ---------- */

    void BuildPicker()
    {
        var card = Ui.Box(root, "Picker", new Color(0.1f, 0.06f, 0.04f, 0.93f));
        picker = card.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(300, 300), new Vector2(14, -10));
        card.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.4f);
        pickerTitle = Ui.Label(picker, "", 24, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        pickerTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 34), new Vector2(0, -10));
        for (int i = 0; i < DominoModes.Length; i++)
        {
            var m = DominoModes[i];
            var b = Ui.Button(picker, ModeNames[m], Ui.Panel, Ui.GoldLight, 15, () => { mode = m; PlayerPrefs.SetString("ahwa.mode", m); RefreshPicker(); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(130, 38), new Vector2(i % 2 == 0 ? 68 : -68, -56 - (i / 2) * 46));
            modeButtons.Add((m, b.GetComponent<Image>(), b.GetComponentInChildren<Text>()));
        }
        // the stake: − value +
        stakeRow = Ui.Rect("Stake", picker).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 40), new Vector2(0, -158)).gameObject;
        var minus = Ui.Button(stakeRow.transform, "−", Ui.Panel, Ui.Gold, 22, () => { stakeIndex = Mathf.Max(0, stakeIndex - 1); RefreshPicker(); });
        ((RectTransform)minus.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 38), new Vector2(10, 0));
        var plus = Ui.Button(stakeRow.transform, "+", Ui.Panel, Ui.Gold, 22, () => { stakeIndex = Mathf.Min(Stakes.Length - 1, stakeIndex + 1); RefreshPicker(); });
        ((RectTransform)plus.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(44, 38), new Vector2(-10, 0));
        stakeText = Ui.Label(stakeRow.transform, "", 18, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        stakeText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(170, 38), Vector2.zero);
        var q = Ui.Button(picker, "لعب سريع", Ui.Gold, Ui.Ink, 20, () => app.Quick(mode, Stakes[stakeIndex]));
        ((RectTransform)q.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(260, 48), new Vector2(0, 50));
        quickButton = q.gameObject;
        pickerNote = Ui.Label(picker, "", 13, new Color(1, 1, 1, 0.7f));
        pickerNote.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(280, 24), new Vector2(0, 16));
    }

    public void ShowPicker(string game, string gameName)
    {
        picker.gameObject.SetActive(true);
        pickerTitle.Set(gameName);
        bool domino = game == "domino";
        foreach (var mb in modeButtons) mb.bg.gameObject.SetActive(domino);
        stakeRow.SetActive(domino); quickButton.SetActive(domino);
        pickerNote.Set(domino ? Loc.T("الرهان: كل لاعب يدفع، والفايز ياخد") : Loc.T("قريباً في نسخة Unity"));
        RefreshPicker();
    }
    public void HidePicker() { picker.gameObject.SetActive(false); }

    void RefreshPicker()
    {
        PlayerPrefs.SetInt("ahwa.stake", stakeIndex);
        foreach (var mb in modeButtons) { bool on = mb.mode == mode; mb.bg.color = on ? Ui.Gold : Ui.Panel; mb.label.color = on ? Ui.Ink : Ui.GoldLight; }
        stakeText.Set(Loc.T("رهان ") + Short(Stakes[stakeIndex]));
    }

    public static string Short(long n) => n >= 1000000 ? (n / 1000000f).ToString("0.#") + "M" : n >= 1000 ? (n / 1000f).ToString("0.#") + "K" : n.ToString();

    /* ---------- searching ---------- */

    void BuildQueue()
    {
        var card = Ui.Box(root, "Queue", new Color(0.1f, 0.06f, 0.04f, 0.95f));
        queuePanel = card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340, 170), Vector2.zero);
        queueText = Ui.Label(queuePanel, "البحث عن لاعبين", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
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
        var again = Ui.Button(matchPanel, "العب مرة أخرى", Ui.Gold, Ui.Ink, 17, () => app.PlayAgain());
        ((RectTransform)again.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 44), new Vector2(90, 18));
        var home = Ui.Button(matchPanel, "خروج", Ui.Panel, Ui.GoldLight, 17, () => app.Leave());
        ((RectTransform)home.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 44), new Vector2(-90, 18));
        matchPanel.gameObject.SetActive(false);
    }

    public void ShowHud(bool on)
    {
        hud.gameObject.SetActive(on);
        if (!on) { matchPanel.gameObject.SetActive(false); resultPanel.gameObject.SetActive(false); handKey = ""; }
    }

    public void ShowPayout(string kind, double delta)
    {
        payoutText.Set(delta > 0 ? "+" + Short((long)delta) + Loc.T(" عملة") : delta < 0 ? "−" + Short((long)-delta) + Loc.T(" عملة") : "");
        payoutText.color = delta >= 0 ? new Color(0.6f, 0.9f, 0.5f) : new Color(1f, 0.6f, 0.5f);
    }

    string NameOf(TableView v, int seat) => seat == v.you ? Loc.T("أنت") : seat >= 0 && seat < v.seats.Count && v.seats[seat] != null ? v.seats[seat].name : "";

    public void Render(TableView v, int sel, List<char> choose)
    {
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
            if (v.MyTurn) msg = choose != null && choose.Count > 1 ? Loc.T("الحجر يناسب الطرفين. اختار المكان.") : v.open >= 0 && v.line.Empty ? Loc.T("افتح بالـ ") + Domino.Tiles[v.open][0] + "|" + Domino.Tiles[v.open][1] : Loc.T("دورك");
            else if (v.turn >= 0) msg = NameOf(v, v.turn) + Loc.T(" يلعب");
            if (v.last != null && v.last.t == "pass" && v.last.s != v.you && v.last.s >= 0) msg = NameOf(v, v.last.s) + Loc.T(" قال باص");
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
            string title = r.winner < 0 ? Loc.T("تعادل. لا أحد يسجل.") : v.mode == "team" ? ((v.you >= 0 && r.team == v.TeamOf(v.you)) ? Loc.T("فريقك فاز بالجولة") : Loc.T("الفريق التاني فاز بالجولة")) : NameOf(v, r.winner) + Loc.T(" فاز بالجولة");
            resultTitle.Set(title);
            resultSub.Set((r.blocked ? Loc.T("اللعبة مقفولة  ·  ") : "") + (r.points > 0 ? "+" + r.points + Loc.T(" نقطة") : ""));
        }
        matchPanel.gameObject.SetActive(mend);
        if (mend)
        {
            var m = v.match;
            bool won = v.you >= 0 && m.team == v.TeamOf(v.you);
            matchTitle.Set(m.team < 0 ? Loc.T("تعادل") : won ? (v.mode == "team" ? Loc.T("فريقك فاز!") : Loc.T("فزت بالمباراة!")) : (v.mode == "team" ? Loc.T("الفريق التاني فاز") : NameOf(v, m.team) + Loc.T(" فاز بالمباراة")));
            matchSub.Set(string.Join("  ·  ", m.scores));
        }
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
        }
    }

    void Update()
    {
        if (queuePanel.gameObject.activeSelf)
        {
            float left = botsAtLocal - Time.time;
            queueText.Set(left > 0 ? Loc.T("سينضم لاعبو الكمبيوتر خلال ") + Mathf.CeilToInt(left) + Loc.T(" ث") : Loc.T("البحث عن لاعبين"));
        }
        var v = app.view;
        if (!hud.gameObject.activeSelf || v == null) return;
        // the turn timer
        float ms = (float)(v.turnEndsAt - app.ServerNow());
        timerFill.transform.parent.gameObject.SetActive(v.phase == "play" && v.turnMs > 0);
        timerFill.rectTransform.localScale = new Vector3(Mathf.Clamp01(ms / (float)v.turnMs), 1, 1);
        timerFill.color = ms < 5000 ? new Color(1f, 0.45f, 0.35f) : Ui.Gold;
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
            seatTags[pos].Set(s.name + "  ·  " + s.count);
            bool active = v.phase == "play" && v.turn == seat;
            seatTagBg[pos].color = active ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f) : new Color(0.1f, 0.06f, 0.04f, 0.8f);
            seatTags[pos].color = active ? Ui.Ink : Color.white;
        }
    }
}
