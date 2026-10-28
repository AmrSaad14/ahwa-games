// The social side of the home screen: player cards (anyone you tap: at the table, in the leaderboard, in your
// friends list, or yourself), your friends list with who's online and one-tap invites, a friend's invitation,
// and muting a player's chat on this phone.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    static readonly string[] Faces = { "AmFathi", "CaptainSami", "Ziko", "Mona", "Nada", "HajjMahmoud" };
    /// Your friend code (shared in invites and results).
    public string MyCode => user?.Str("code") ?? "";
    public static Texture2D Face(int ch) => Resources.Load<Texture2D>("UI/Faces/" + Faces[Mathf.Clamp(ch, 0, Faces.Length - 1)]);

    /* ---------- muting a player (on this phone) ---------- */

    static HashSet<string> muted;
    public static bool IsMuted(string pid)
    {
        if (string.IsNullOrEmpty(pid)) return false;
        if (muted == null) muted = new HashSet<string>(PlayerPrefs.GetString("muted", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        return muted.Contains(pid);
    }
    static void SetMuted(string pid, bool on)
    {
        IsMuted(pid);
        if (on) muted.Add(pid); else muted.Remove(pid);
        PlayerPrefs.SetString("muted", string.Join(",", muted)); PlayerPrefs.Save();
    }

    /* ---------- player cards ---------- */

    Dictionary<string, object> card;
    int cardSeat = -1;          // the seat at the table the card was opened from (-1: elsewhere)
    Action refreshCard;

    /// A player's card, from their seat at the table.
    public void OpenCardSeat(int seat)
    {
        var v = app.view;
        if (v == null || seat < 0 || seat >= v.seats.Count || v.seats[seat] == null) return;
        var s = v.seats[seat];
        if (s.bot) { app.Toast(Loc.T("ده لاعب كمبيوتر")); return; }
        if (app.Practicing) return;
        cardSeat = seat;
        ShowCardLoading(GameUi.NameOf(v, seat), s.ch);
        app.Send(new Dictionary<string, object> { ["t"] = "profileOf", ["seat"] = (double)seat });
    }

    /// A player's card, from the leaderboard or the friends list (their public id).
    public void OpenCard(string pid, string name = "", int ch = 0)
    {
        if (string.IsNullOrEmpty(pid)) return;
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        cardSeat = -1;
        ShowCardLoading(name, ch);
        app.Send(new Dictionary<string, object> { ["t"] = "profileOf", ["pid"] = pid });
    }

    /// Your own card (tap your name on the home screen).
    public void OpenMyCard() { if (user != null) OpenCard(user.Str("pid"), user.Str("name"), (int)user.Num("ch")); }

    void ShowCardLoading(string name, int ch)
    {
        card = null;
        Panel(Loc.T("بطاقة اللاعب"), out var body, new Vector2(620, 344));
        refreshCard = refreshOpen = () => DrawCard(body, name, ch);
        refreshOpen();
    }

    void DrawCard(RectTransform body, string name, int ch)
    {
        foreach (Transform c in body) Destroy(c.gameObject);
        var c0 = card;
        if (c0 != null) { name = c0.Str("name"); ch = (int)c0.Num("ch"); }
        // the portrait and who they are, at the reading side
        var side = Ui.Rect("Who", body).Place(new Vector2(Loc.En ? 0 : 1, 1), new Vector2(Loc.En ? 0 : 1, 1), new Vector2(170, 280), Vector2.zero);
        var frame = Ui.Box(side, "Frame", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.85f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(124, 154), new Vector2(0, -2));
        var face = Ui.Picture(frame, "Face", Face(ch)); face.rectTransform.Fill(3);
        if (c0 != null)
        {
            var dot = Ui.Picture(frame, "Online", GameUi.Disc.texture);
            dot.color = c0.Bool("online") ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);
            dot.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(16, 16), new Vector2(-6, -6));
        }
        Ui.Label(side, name, 18, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(170, 26), new Vector2(0, -160));
        if (c0 == null) { Ui.Label(body, Loc.T("جاري التحميل…"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
        // their title, under the name
        float dy = 0;
        var ttl = Titles.Badge(side, c0.Str("title"), 12);
        if (ttl != null)
        {
            float tw = Mathf.Min(ttl.sizeDelta.x, 168), sc = tw / ttl.sizeDelta.x;
            ttl.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), ttl.sizeDelta, new Vector2(0, -199));
            ttl.localScale = new Vector3(sc, sc, 1);
            dy = 26;
        }
        var lvl = Ui.Box(side, "Level", Ui.Gold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(84, 20), new Vector2(0, -188 - dy));
        Ui.Label(lvl, Loc.T("مستوى") + " " + (int)c0.Num("level"), 12, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
        string status = c0.Bool("playing") ? Loc.T("بيلعب دلوقتي") : c0.Bool("online") ? Loc.T("متصل") : Loc.T("مش متصل");
        Ui.Label(side, status, 12, Ui.GoldLight).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(170, 18), new Vector2(0, -212 - dy));
        var since = DateTimeOffset.FromUnixTimeMilliseconds((long)c0.Num("since")).ToLocalTime();
        Ui.Label(side, Loc.T("من") + " " + since.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), 11, new Color(1, 1, 1, 0.55f)).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(170, 16), new Vector2(0, -230 - dy));

        // the numbers: eight tiles in two rows
        var stats = Ui.Rect("Stats", body);
        stats.anchorMin = new Vector2(Loc.En ? 0 : 0, 0); stats.anchorMax = new Vector2(1, 1);
        stats.offsetMin = new Vector2(Loc.En ? 180 : 0, 52); stats.offsetMax = new Vector2(Loc.En ? 0 : -180, 0);
        long won = (long)c0.Num("coinsWon"), lost = (long)c0.Num("coinsLost"), net = (long)c0.Num("net");
        var tiles = new (string label, string value, Color color)[]
        {
            ("ماتشات", ((int)c0.Num("games")).ToString("N0"), Color.white),
            ("مكسب", ((int)c0.Num("wins")).ToString("N0"), new Color(0.6f, 0.92f, 0.55f)),
            ("خسارة", ((int)c0.Num("losses")).ToString("N0"), new Color(1f, 0.62f, 0.55f)),
            ("نسبة المكسب", (int)c0.Num("winRate") + "%", Ui.Gold),
            ("كوينز كسبها", "+" + Loc.Short(won), new Color(0.6f, 0.92f, 0.55f)),
            ("كوينز خسرها", "−" + Loc.Short(lost), new Color(1f, 0.62f, 0.55f)),
            ("صافي المكسب", (net >= 0 ? "+" : "−") + Loc.Short(Math.Abs(net)), net >= 0 ? Ui.Gold : new Color(1f, 0.62f, 0.55f)),
            ("أكبر مكسب", Loc.Short((long)c0.Num("bestWin")), Ui.GoldLight),
        };
        const float TW = 98, TH = 62, GX = 6, GY = 6;
        for (int i = 0; i < tiles.Length; i++)
        {
            int col = i % 4, row = i / 4, k = Loc.En ? col : 3 - col;
            var t = Ui.Box(stats, "Stat", Card).rectTransform;
            t.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(TW, TH), new Vector2((k - 1.5f) * (TW + GX), -row * (TH + GY)));
            Ui.Label(t, tiles[i].value, 19, tiles[i].color, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(TW, 30), new Vector2(0, -6));
            Ui.Label(t, tiles[i].label, 11, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(TW, 20), new Vector2(0, 5));
        }
        // each game: rating and record
        var pg = c0.Obj("perGame");
        var lines = new List<string>();
        foreach (var g in new[] { "domino", "tawla", "chess", "ludo" })
        {
            var r = pg?.Obj(g); if (r == null) continue;
            lines.Add(Loc.T(App.GameNames[g]) + " " + (int)r.Num("rating") + "  (" + (int)r.Num("wins") + "/" + (int)r.Num("games") + ")");
        }
        long tc = (long)c0.Num("tourneyCoins") + (long)c0.Num("weeklyCoins");
        if (tc > 0) lines.Add(Loc.T("جوايز البطولات") + " " + Loc.Short(tc));
        var per = Ui.Label(stats, lines.Count > 0 ? string.Join("   ·   ", lines) : Loc.T("لسه ملعبش أونلاين"), 12, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        per.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(420, 34), new Vector2(0, 2));
        per.horizontalOverflow = HorizontalWrapMode.Wrap; per.resizeTextForBestFit = true; per.resizeTextMinSize = 9; per.resizeTextMaxSize = 12;

        // what you can do: add or remove the friend, mute, report
        var bar = Ui.Rect("Actions", body).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 40), new Vector2(0, 0));
        string pid = c0.Str("pid");
        var buttons = new List<(string label, Color bg, Color fg, Action act)>();
        if (c0.Bool("me"))
        {
            buttons.Add(("شارك كودك", Ui.Gold, Ui.Ink, () => ShareInvite()));
        }
        else
        {
            string fr = c0.Str("friend");
            if (fr == "friend") buttons.Add(("إلغاء الصداقة", new Color(0.42f, 0.16f, 0.16f, 1), Color.white, () => FriendSend("friendRemove", pid)));
            else if (fr == "outgoing") buttons.Add(("الطلب اتبعت · إلغاء", new Color(1, 1, 1, 0.1f), Ui.GoldLight, () => FriendSend("friendRemove", pid)));
            else if (fr == "incoming") buttons.Add(("قبول الصداقة", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, () => app.Send(new Dictionary<string, object> { ["t"] = "friendAnswer", ["pid"] = pid, ["yes"] = true })));
            else buttons.Add(("أضف صديق", Ui.Gold, Ui.Ink, () => FriendSend("friendAdd", pid)));
            bool m = IsMuted(pid);
            buttons.Add((m ? "إلغاء الكتم" : "كتم الشات", new Color(1, 1, 1, 0.1f), Ui.GoldLight, () => { SetMuted(pid, !IsMuted(pid)); app.Toast(Loc.T(IsMuted(pid) ? "مش هتشوف رسايله" : "هتشوف رسايله تاني")); refreshOpen?.Invoke(); }));
            buttons.Add(("بلاغ", new Color(1, 1, 1, 0.1f), new Color(1f, 0.65f, 0.55f), () => Report(pid)));
            if (fr == "friend" && c0.Bool("playing") && cardSeat < 0) buttons.Insert(0, ("تفرّج", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, () => { Close(); app.Watch(pid); }));
        }
        float bw = buttons.Count > 3 ? 132 : 168;
        for (int i = 0; i < buttons.Count; i++)
        {
            var b = buttons[i];
            int k = Loc.En ? i : buttons.Count - 1 - i;
            var btn = Ui.Button(bar, b.label, b.bg, b.fg, 14, () => { Sfx.Tap(); b.act(); });
            ((RectTransform)btn.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(bw, 36), new Vector2((k - (buttons.Count - 1) / 2f) * (bw + 8), 0));
        }
    }

    void FriendSend(string t, string pid)
    {
        app.Send(new Dictionary<string, object> { ["t"] = t, ["pid"] = pid });
        // the card comes back with the new state (asked again to be sure)
        StartCoroutine(AskCardAgain(pid));
    }
    System.Collections.IEnumerator AskCardAgain(string pid)
    {
        yield return new WaitForSeconds(0.35f);
        if (refreshOpen == refreshCard && card != null && card.Str("pid") == pid) app.Send(new Dictionary<string, object> { ["t"] = "profileOf", ["pid"] = pid });
    }

    void Report(string pid)
    {
        var m = new Dictionary<string, object> { ["t"] = "report", ["reason"] = "spam" };
        if (cardSeat >= 0 && app.view != null) m["seat"] = (double)cardSeat; else m["pid"] = pid;
        app.Send(m);
        SetMuted(pid, true);
        app.Toast(Loc.T("وصلنا البلاغ، شكراً. ومش هتشوف رسايله"));
        Close();
    }

    /* ---------- friends ---------- */

    List<object> friendRows;
    int incoming;

    public void OpenFriends()
    {
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        app.Send(new Dictionary<string, object> { ["t"] = "friends" });
        app.Send(new Dictionary<string, object> { ["t"] = "groups" });
        Panel(Loc.T("صحابك"), out var body, new Vector2(640, 372));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "صحابك", "قهوة الشلة" }, friendsTab, i => { friendsTab = i; if (i == 0) groupOpen = null; refreshOpen(); });
            if (friendsTab == 1) DrawGroups(area); else DrawFriends(area);
        };
        refreshOpen();
    }

    void DrawFriends(RectTransform body)
    {
        foreach (Transform c in body) Destroy(c.gameObject);
        // your code, and a link to invite friends who don't have the game yet
        var top = Ui.Box(body, "You", Card).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 40), Vector2.zero);
        string code = user?.Str("code") ?? "";
        At(Ui.Label(top, Loc.T("كودك") + ":  " + code, 16, Ui.Gold, StartAlign, Ui.Bold).rectTransform, 12, 170, 36);
        var cp = Ui.Button(top, "انسخ", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 13, () => { Sfx.Tap(); GUIUtility.systemCopyBuffer = code; app.Toast(Loc.T("الكود اتنسخ")); });
        At((RectTransform)cp.transform, 186, 64, 30);
        var inv = Ui.Button(top, "ادعي صحابك  +50K", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 13, () => { Sfx.Tap(); ShareInvite(); });
        At((RectTransform)inv.transform, 258, 160, 30);
        var field = Field(top, Loc.T("كود صاحبك"), 13, false);
        At((RectTransform)field.transform, 424, 100, 30);
        field.characterLimit = 8;
        var add = Ui.Button(top, "أضف", Ui.Gold, Ui.Ink, 13, () =>
        {
            Sfx.Tap(); var c = field.text.Trim();
            if (c.Length >= 4) app.Send(new Dictionary<string, object> { ["t"] = "friendAdd", ["code"] = c });
        });
        At((RectTransform)add.transform, 530, 62, 30);

        var listArea = Ui.Rect("List", body); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = Vector2.zero; listArea.offsetMax = new Vector2(0, -48);
        if (friendRows == null) { Ui.Label(listArea, Loc.T("جاري التحميل…"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
        if (friendRows.Count == 0) { Ui.Label(listArea, Loc.T("لسه مفيش صحاب. ابعت كودك أو دوس على أي لاعب في الترابيزة وضيفه"), 14, Ui.GoldLight).rectTransform.Fill(); return; }
        var list = Scroll(listArea, 5);
        foreach (var o in friendRows)
        {
            if (!(o is Dictionary<string, object> f)) continue;
            string st = f.Str("status"), pid = f.Str("pid"), id = f.Str("id"), name = f.Str("name");
            int ch = (int)f.Num("ch");
            bool on = f.Bool("online"), playing = f.Bool("playing");
            var row = Row(list, 46, Card);
            // tap the row: their card
            var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(pid, name, ch); });
            var face = Ui.Picture(row, "Face", Face(ch)); At(face.rectTransform, 6, 34, 40);
            var dot = Ui.Picture(row, "Dot", GameUi.Disc.texture); dot.color = on ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.45f, 0.45f, 0.45f);
            At(dot.rectTransform, 34, 10, 10); dot.rectTransform.anchoredPosition += new Vector2(0, -14);
            At(Ui.Label(row, name, 15, Color.white, StartAlign, Ui.Bold).rectTransform, 48, 200, 22).anchoredPosition += new Vector2(0, 9);
            string sub = st == "incoming" ? Loc.T("عايز يبقى صاحبك") : st == "outgoing" ? Loc.T("الطلب اتبعت") : playing ? Loc.T("بيلعب دلوقتي") : on ? Loc.T("متصل") : Loc.T("مش متصل");
            At(Ui.Label(row, sub, 12, on || st == "incoming" ? Ui.Gold : new Color(1, 1, 1, 0.55f), StartAlign).rectTransform, 48, 200, 18).anchoredPosition += new Vector2(0, -10);
            if (st == "incoming")
            {
                var yes = Ui.Button(row, "قبول", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 13, () => { Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "friendAnswer", ["id"] = id, ["yes"] = true }); });
                At((RectTransform)yes.transform, 420, 80, 32);
                var no = Ui.Button(row, "رفض", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 13, () => { Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "friendAnswer", ["id"] = id, ["yes"] = false }); });
                At((RectTransform)no.transform, 506, 70, 32);
            }
            else if (st == "friend")
            {
                // one tap: a room for the two of you in the game you play most, and the invitation goes out
                var go = Ui.Button(row, "ادعيه يلعب", on ? Ui.Gold : new Color(1, 1, 1, 0.08f), on ? Ui.Ink : new Color(1, 1, 1, 0.5f), 13, () =>
                {
                    Sfx.Tap();
                    if (!on) { app.Toast(Loc.T("صاحبك مش متصل دلوقتي")); return; }
                    app.InviteFriend(id);
                    Close();
                });
                At((RectTransform)go.transform, 450, 126, 32);
                // in a game now: watch it
                if (playing)
                {
                    var w = Ui.Button(row, "تفرّج", new Color(1, 1, 1, 0.12f), Ui.GoldLight, 13, () => { Sfx.Tap(); Close(); app.Watch(pid); });
                    At((RectTransform)w.transform, 340, 100, 32);
                }
            }
        }
    }

    /// Share your invite link (Google Play for friends who don't have the game: you both get 50K).
    public void ShareInvite()
    {
        string code = user?.Str("code") ?? "";
        string link = Growth.InviteLink(code);
        string text = Loc.En
            ? "Play Dominoes, Backgammon, Chess and Ludo with me in Board Battles! Use my code " + code + " and we both get 50K coins: " + link
            : "تعالى العب معايا دومينو وطاولة وشطرنج ولودو في بورد باتلز! استخدم كودي " + code + " وإحنا الاتنين ناخد ٥٠ ألف كوين: " + link;
        Growth.Share(text);
    }

    /* ---------- a friend invites you ---------- */

    void ShowInvited(Dictionary<string, object> m)
    {
        var from = m.Obj("from");
        string code = m.Str("code"), mode = m.Str("mode");
        string game = Modes.GameOf(mode);
        Sfx.Chime(); Sfx.Buzz();
        if (app.view != null && !app.Practicing) { app.Toast(from.Str("name") + " " + Loc.T("بيدعوك تلعب") + " " + Loc.T(App.GameNames[game])); return; }
        Panel(Loc.T("دعوة للعب"), out var body, new Vector2(460, 250));
        refreshOpen = null;
        var face = Ui.Picture(body, "Face", Face((int)from.Num("ch")));
        face.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(72, 90), new Vector2(0, 0));
        Ui.Label(body, from.Str("name") + " " + Loc.T("بيدعوك تلعب") + " " + Loc.T(App.GameNames[game]), 17, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, 28), new Vector2(0, -96));
        if (!string.IsNullOrEmpty(m.Str("group")))
        {
            var gl = Ui.Label(body, "", 13, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold); gl.Set("☕ " + m.Str("group"));
            gl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, 20), new Vector2(0, -144));
        }
        long stake = (long)m.Num("stake");
        if (stake > 0) Ui.Label(body, Loc.T("رهان") + " " + Loc.Short(stake), 13, Ui.GoldLight).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, 20), new Vector2(0, -124));
        var yes = Ui.Button(body, "ادخل العب", Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); Close(); app.JoinRoom(code); });
        ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 42), new Vector2(Loc.En ? 90 : -90, 0));
        var no = Ui.Button(body, "بعدين", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 15, () => { Sfx.Tap(); Close(); });
        ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 42), new Vector2(Loc.En ? -90 : 90, 0));
    }
}
