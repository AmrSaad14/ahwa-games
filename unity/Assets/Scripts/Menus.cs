// The home screen's menus, over the café: the shop, the leaderboard, how to play, settings (sound, vibration,
// language, account), the "watch an ad for 5,000 coins" button, the language switch and Google sign-in.
// Everything talks to the same server as the web app: lb, buySkin / equipSkin, adStart / adWatched,
// ticket (Google), logout.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus : MonoBehaviour
{
    App app;
    Canvas canvas;
    RectTransform open;                 // the panel showing now (null: none)
    Action refreshOpen;                 // redraws it (new data, new language)
    Text adText, langText, signText, coinsText;
    GameObject signChip;
    Dictionary<string, object> user;
    bool googleOn = true;
    string pendingTicket;
    // cash out: your requests and the rate, from the server
    List<object> cashRows; long cashCoins = 1_000_000; int cashEgp = 10, cashMax = 10, cashMillions = 1;
    InputField walletField;

    // the leaderboard currently asked for
    string lbGame = "domino"; List<object> lbRows; Dictionary<string, object> lbMe;
    int shopTab, howTab, lbTab;
    bool adShowing;

    static readonly Color Card = new Color(1, 1, 1, 0.06f);
    static readonly Color Sheet = new Color32(0x22, 0x15, 0x0e, 0xf6);

    public static Texture2D Icon(string name) => Resources.Load<Texture2D>("UI/Icons/" + name);

    /* ---------- the home screen's buttons ---------- */

    public void Build(App a, RectTransform home)
    {
        app = a;
        canvas = Ui.MakeCanvas("Menus", 40);

        // down the left side: what's waiting for you today, your friends and the shop
        var rail = Ui.Rect("Rail", home).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, 300), new Vector2(10, -88));
        dailyDot = Dot(RailButton(rail, 0, "gift", "يومي", OpenDaily));
        missionsDot = Dot(RailButton(rail, 1, "target", "المهام", OpenMissions));
        seasonDot = Dot(RailButton(rail, 2, "pass", "الموسم", () => OpenSeason()));
        friendsDot = Dot(RailButton(rail, 3, "friends", "صحابك", OpenFriends));
        RailButton(rail, 4, "shop", "المتجر", () => OpenShop(0));
        // along the top, beside the logo: the tournament, the leaderboard, how to play, settings
        var top = Ui.Rect("Top", home).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, 54), new Vector2(82, -8));
        tourneyDot = Dot(TopButton(top, 0, "cup", "البطولة", () => OpenTourney()));
        TopButton(top, 1, "trophy", "المتصدرين", () => OpenLeaders(0));
        TopButton(top, 2, "help", "طريقة اللعب", () => OpenHowTo(0));
        TopButton(top, 3, "settings", "الإعدادات", OpenSettings);
        // (cash out to Vodafone Cash: OpenCashout, switched off for now)

        // along the top, left of your name and coins: watch an ad, sign in, language
        var ad = Chip(home, "video", "+5K", new Vector2(-252, -10), 98, new Color32(0x2f, 0x6b, 0x3a, 0xf0), Color.white, WatchAd);
        adText = ad.GetComponentInChildren<Text>();
        var lang = Chip(home, "globe", Loc.En ? "ع" : "EN", new Vector2(-358, -10), 70, Ui.Panel, Ui.GoldLight, () => { Sfx.Tap(); Loc.Set(!Loc.En); });
        langText = lang.GetComponentInChildren<Text>();
        var sign = Chip(home, "google", "دخول", new Vector2(-436, -10), 92, new Color(1, 1, 1, 0.94f), Ui.Ink, SignIn);
        signChip = sign.gameObject; signText = sign.GetComponentInChildren<Text>();

        Loc.Changed += () => { langText.Set(Loc.En ? "ع" : "EN"); refreshOpen?.Invoke(); };
        Application.deepLinkActivated += OnLink;
        if (!string.IsNullOrEmpty(Application.absoluteURL)) OnLink(Application.absoluteURL);
    }

    /// A red dot in the corner of a button: something is waiting there.
    static GameObject Dot(RectTransform b)
    {
        var dot = Ui.Picture(b, "Dot", GameUi.Disc.texture);
        dot.color = new Color32(0xe5, 0x3b, 0x3b, 0xff);
        dot.rectTransform.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(14, 14), new Vector2(-6, -6));
        dot.gameObject.SetActive(false);
        return dot.gameObject;
    }

    RectTransform TopButton(RectTransform row, int i, string icon, string label, Action onClick)
    {
        var b = Ui.Button(row, "", Ui.Panel, Ui.GoldLight, 10, () => { Sfx.Tap(); onClick(); });
        var r = ((RectTransform)b.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, 52), new Vector2(i * 60, 0));
        b.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.3f);
        var ic = Ui.Picture(r, "Icon", Icon(icon)); ic.color = Ui.Gold;
        ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(24, 24), new Vector2(0, -6));
        var t = Ui.Label(r, label, 9, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(60, 16), new Vector2(0, 2));
        return r;
    }

    RectTransform RailButton(RectTransform rail, int i, string icon, string label, Action onClick)
    {
        var b = Ui.Button(rail, "", Ui.Panel, Ui.GoldLight, 10, () => { Sfx.Tap(); onClick(); });
        var r = ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, 54), new Vector2(0, -i * 60));
        b.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.3f);
        var ic = Ui.Picture(r, "Icon", Icon(icon)); ic.color = Ui.Gold;
        ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(26, 26), new Vector2(0, -6));
        var t = Ui.Label(r, label, 9, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(62, 16), new Vector2(0, 3));
        return r;
    }

    Button Chip(RectTransform parent, string icon, string label, Vector2 pos, float w, Color bg, Color fg, Action onClick)
    {
        var b = Ui.Button(parent, "", bg, fg, 14, onClick);
        var r = ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(w, 40), pos);
        var ic = Ui.Picture(r, "Icon", Icon(icon)); ic.color = icon == "google" ? Color.white : fg;
        ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 22), new Vector2(9, 0));
        var t = b.GetComponentInChildren<Text>();
        t.Set(Loc.T(label));
        Loc.Track(t, label);
        t.rectTransform.offsetMin = new Vector2(30, 0);
        return b;
    }

    /* ---------- what the server says ---------- */

    public void OnUser(Dictionary<string, object> u)
    {
        user = u;
        bool signed = !string.IsNullOrEmpty(u.Str("google")) || !string.IsNullOrEmpty(u.Str("phone")) || !string.IsNullOrEmpty(u.Str("email"));
        signChip.SetActive(!signed);
        int left = (int)u.Num("adsLeft");
        adText.Set(left > 0 ? "+5K" : "0");
        var eq = u.Obj("equip");
        TileArt.SetSkin(eq != null ? eq.Str("tiles") : null);
        Look.MyFelt = Wearing("felt");
        var d = u.Obj("daily");
        bool ready = d != null && d.Bool("ready");
        dailyDot.SetActive(ready);
        UpdateDots();
        // a friend's invite code from Google Play (or a link) waiting: credit them, once the account is new
        if (Growth.PendingCode != null && u.Bool("canRefer")) { app.Send(new Dictionary<string, object> { ["t"] = "referral", ["code"] = Growth.PendingCode }); Growth.PendingCode = null; }
        // the first time today's reward is waiting, show the track by itself (on the home screen, nothing else open)
        if (ready && !dailyShown && app.view == null && !IsOpen && !App.Recording) { dailyShown = true; OpenDaily(); return; }
        refreshOpen?.Invoke();
    }

    public void OnMessage(Dictionary<string, object> m)
    {
        OnMessageMore(m);
        switch (m.Str("t"))
        {
            case "welcome":
                var login = m.Obj("login");
                if (login != null) googleOn = login.Bool("google");
                if (pendingTicket != null) { app.Send(new Dictionary<string, object> { ["t"] = "ticket", ["ticket"] = pendingTicket }); pendingTicket = null; }
                if (pendingJoin != null) { var j = pendingJoin; pendingJoin = null; StartCoroutine(JoinSoon(j)); }
                break;
            case "signedIn":
                app.Toast(Loc.T("أهلاً! اتسجلت بحساب جوجل"));
                Sfx.Coins();
                break;
            case "bonus":
                // today's reward was collected (or, on an older server, given as you connected)
                dailyGot = (long)m.Num("coins"); dailyGotDay = (int)m.Num("streak", 1);
                Sfx.Coins(); Sfx.Buzz();
                app.Toast("+" + dailyGot.ToString("N0") + " " + Loc.T("عملة"));
                if (refreshDaily != null && refreshOpen == refreshDaily) refreshOpen(); else if (!IsOpen && app.view == null) OpenDaily();
                break;
            case "xp":
                if (m.Bool("levelUp")) { app.Toast(Loc.T("مستوى جديد!") + "  " + Loc.T("مستوى") + " " + (int)m.Num("level")); Sfx.Coins(); }
                else app.Toast("+" + (int)m.Num("gained") + " XP");
                break;
            case "weeklyPrize":
                app.Toast(Loc.T("مبروك! جيت المركز") + " " + (int)m.Num("rank") + "  +" + Loc.Short((long)m.Num("coins")));
                Sfx.Coins(); Sfx.Buzz();
                break;
            case "lb":
                lbRows = m.Arr("rows") ?? new List<object>(); lbMe = m.Obj("me");
                if (m.Str("game") == "week") lbEndsAt = m.Num("endsAt");
                Debug.Log($"[menus] leaderboard {m.Str("game")}: {lbRows.Count} rows (showing {lbGame})");
                if (m.Str("game") == lbGame) refreshOpen?.Invoke();
                break;
            case "adTicket":
            {
                string ticket = m.Str("ticket"); bool ssv = m.Bool("ssv");
                if (Ads.Available)
                {
                    // a real rewarded video; the coins come from the server once it is watched (AdMob tells the server
                    // itself when server-side verification is on)
                    adShowing = true;
                    Ads.Rewarded(user?.Str("id"), ticket, watched =>
                    {
                        adShowing = false;
                        if (!watched) { app.Toast(Loc.T("الإعلان مش جاهز، جرّب تاني")); return; }
                        if (!ssv) app.Send(new Dictionary<string, object> { ["t"] = "adWatched", ["ticket"] = ticket });
                    });
                }
                else if (ssv) app.Toast(Loc.T("الإعلان مش جاهز، جرّب تاني"));
                else StartCoroutine(PlayAd(ticket));   // off the phone: the game's own short spot
                break;
            }
            case "cashouts":
                cashRows = m.Arr("rows") ?? new List<object>();
                var rate = m.Obj("rate");
                if (rate != null) { cashCoins = (long)rate.Num("coins", 1_000_000); cashEgp = (int)rate.Num("egp", 10); }
                cashMax = (int)m.Num("maxMillions", 10);
                refreshOpen?.Invoke();
                break;
            case "supportSent":
                app.Toast(Loc.T("وصلتنا رسالتك، شكراً! هنرد عليك قريب"));
                Sfx.Chime();
                break;
            case "cashoutDone":
                app.Toast(Loc.T("طلبك وصل! هيتحولك") + " " + (int)m.Num("egp") + " " + Loc.T("ج.م") + " " + Loc.T("خلال يوم عمل واحد"));
                Sfx.Coins(); Sfx.Buzz();
                break;
            case "adReward":
                app.Toast("+" + ((long)m.Num("coins")).ToString("N0") + " " + Loc.T("عملة"));
                Sfx.Coins();
                break;
        }
    }

    public bool OnError(string code)
    {
        switch (code)
        {
            case "ad_limit": app.Toast(Loc.T("خلصت إعلانات النهارده، تعالى بكرة")); return true;
            case "ticket_expired": app.Toast(Loc.T("تسجيل الدخول بجوجل مش متاح على السيرفر ده")); return true;
            case "sign_in_first": app.Toast(Loc.T("سجّل دخول الأول عشان تقدر تسحب")); return true;
            case "bad_wallet": app.Toast(Loc.T("اكتب رقم فودافون صحيح يبدأ بـ 010")); return true;
            case "cashout_pending": app.Toast(Loc.T("عندك طلب لسه بيتحول، استنى لما يخلص")); return true;
            case "bad_amount": app.Toast(Loc.T("المبلغ مش مظبوط")); return true;
            case "slow_down": app.Toast(Loc.T("استنى شوية وجرّب تاني")); return true;
            case "support_empty": app.Toast(Loc.T("اكتب رسالتك الأول")); return true;
            case "chat_muted": app.Toast(Loc.T("الشات مقفول عندك شوية بسبب بلاغات")); return true;
            case "mission_not_done": app.Toast(Loc.T("لسه المهمة مخلصتش")); return true;
            case "already_claimed": app.Toast(Loc.T("خدتها خلاص")); return true;
            case "tier_locked": app.Toast(Loc.T("لسه موصلتش للمستوى ده")); return true;
            case "not_premium": app.Toast(Loc.T("افتح الموسم المميز الأول")); return true;
            case "already_premium": app.Toast(Loc.T("الموسم المميز مفتوح عندك")); return true;
            case "tourney_started": app.Toast(Loc.T("البطولة بدأت خلاص")); return true;
            case "no_match": app.Toast(Loc.T("مفيش ماتش ليك دلوقتي")); return true;
            case "bad_code": app.Toast(Loc.T("الكود ده مش صحيح")); return true;
            case "already_referred": app.Toast(Loc.T("دخلت كود صاحبك قبل كده")); return true;
            case "too_late": app.Toast(Loc.T("كود الدعوة للاعبين الجداد بس")); return true;
            case "friend_self": app.Toast(Loc.T("ده كودك انت")); return true;
            case "friend_not_found": app.Toast(Loc.T("مفيش لاعب بالكود ده")); return true;
            case "friend_offline": app.Toast(Loc.T("صاحبك مش متصل دلوقتي")); return true;
            case "rematch_coins": app.Toast(Loc.T("حد رصيده مش كفاية للماتش تاني")); return true;
            case "not_owned": app.Toast(Loc.T("لسه مش معاك")); return true;
        }
        return false;
    }

    /* ---------- panels ---------- */

    RectTransform Panel(string title, out RectTransform body, Vector2? size = null)
    {
        Close();
        var root = Ui.Rect("Panel", canvas.transform).Fill();
        var back = Ui.Box(root, "Backdrop", new Color(0, 0, 0, 0.62f), false);
        back.rectTransform.Fill();
        back.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var sz = size ?? new Vector2(640, 340);
        var sheet = Ui.Box(root, "Sheet", Sheet);
        sheet.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sz, new Vector2(0, -6));
        sheet.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f);
        sheet.raycastTarget = true;
        var t = Ui.Label(sheet.transform, title, 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(400, 36), new Vector2(0, -6));
        var x = Ui.Button(sheet.transform, "", new Color(1, 1, 1, 0.08f), Color.white, 10, Close);
        ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(34, 34), new Vector2(-8, -8));
        var xi = Ui.Picture(x.transform, "X", Icon("close")); xi.rectTransform.Fill(9);
        body = Ui.Rect("Body", sheet.transform);
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one; body.offsetMin = new Vector2(16, 14); body.offsetMax = new Vector2(-16, -48);
        open = root;
        StartCoroutine(Pop(sheet.rectTransform));
        return root;
    }

    static IEnumerator Pop(RectTransform r)
    {
        for (float t = 0; t < 0.18f; t += Time.unscaledDeltaTime) { if (r == null) yield break; r.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, Ui.Back(t / 0.18f)); yield return null; }
        if (r != null) r.localScale = Vector3.one;
    }

    public void Close()
    {
        if (open != null) Destroy(open.gameObject);
        open = null; refreshOpen = null;
    }
    public bool IsOpen => open != null;

    /// A row of tabs across the top of a panel body; returns the area under them.
    RectTransform Tabs(RectTransform body, string[] labels, int sel, Action<int> pick)
    {
        float w = Mathf.Min(150, (body.rect.width > 0 ? body.rect.width : 600) / labels.Length - 6);
        for (int i = 0; i < labels.Length; i++)
        {
            int k = Loc.En ? i : labels.Length - 1 - i;   // Arabic reads right to left: the first tab on the right
            int idx = i;
            var b = Ui.Button(body, labels[i], i == sel ? Ui.Gold : new Color(1, 1, 1, 0.07f), i == sel ? Ui.Ink : Ui.GoldLight, 14, () => { Sfx.Tap(); pick(idx); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w, 30), new Vector2((k - (labels.Length - 1) / 2f) * (w + 6), 0));
        }
        var area = Ui.Rect("Area", body);
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = Vector2.zero; area.offsetMax = new Vector2(0, -38);
        return area;
    }

    /// A list that scrolls up and down.
    static RectTransform Scroll(RectTransform parent, float spacing = 6)
    {
        var view = Ui.Box(parent, "View", new Color(0, 0, 0, 0.001f), false);
        view.rectTransform.Fill();
        view.gameObject.AddComponent<RectMask2D>();
        var content = Ui.Rect("Content", view.transform);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var lay = content.gameObject.AddComponent<VerticalLayoutGroup>();
        lay.spacing = spacing; lay.childControlHeight = true; lay.childControlWidth = true; lay.childForceExpandHeight = false; lay.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.viewport = view.rectTransform; sr.content = content; sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 20;
        return content;
    }

    static RectTransform Row(RectTransform list, float h, Color? bg = null)
    {
        var r = bg.HasValue ? Ui.Box(list, "Row", bg.Value).rectTransform : Ui.Rect("Row", list);
        r.gameObject.AddComponent<LayoutElement>().preferredHeight = h;
        return r;
    }

    /// Placed from the reading side: from the right in Arabic, from the left in English.
    static RectTransform At(RectTransform r, float fromStart, float width, float height)
    {
        float s = Loc.En ? 1 : -1;
        return r.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(width, height), new Vector2(s * fromStart, 0));
    }
    static TextAnchor StartAlign => Loc.En ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

    /* ---------- settings ---------- */

    public void OpenSettings()
    {
        Panel(Loc.T("الإعدادات"), out var body, new Vector2(560, 330));
        var area = Ui.Rect("Area", body); area.Fill();
        refreshOpen = () =>
        {
            foreach (Transform c in area) Destroy(c.gameObject);
            var list = Scroll(area, 8);
            Toggle(list, "الصوت", Sfx.SoundOn, v => { Sfx.SoundOn = v; if (v) Sfx.Tap(); });
            Toggle(list, "الموسيقى", Music.On, v => { Music.On = v; });
            Toggle(list, "أصوات القهوة", Music.CafeOn, v => { Music.CafeOn = v; });
            Toggle(list, "الاهتزاز", Sfx.VibrateOn, v => { Sfx.VibrateOn = v; if (v) Sfx.Buzz(); });
            Toggle(list, "توفير البطارية", Perf.Saver, v => { Perf.Saver = v; });
            // how the game table is seen
            var vr = Row(list, 44, Card);
            At(Ui.Label(vr, "طريقة العرض", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 160, 40);
            var full = Ui.Button(vr, "كامل", !Cafe.BoardView ? Ui.Gold : new Color(1, 1, 1, 0.08f), !Cafe.BoardView ? Ui.Ink : Ui.GoldLight, 14, () => { SetView(false); });
            var brd = Ui.Button(vr, "اللوح بس", Cafe.BoardView ? Ui.Gold : new Color(1, 1, 1, 0.08f), Cafe.BoardView ? Ui.Ink : Ui.GoldLight, 14, () => { SetView(true); });
            At((RectTransform)full.transform, 280, 100, 32); At((RectTransform)brd.transform, 384, 100, 32);
            // language
            var lr = Row(list, 44, Card);
            At(Ui.Label(lr, "اللغة", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 160, 40);
            var ar = Ui.Button(lr, "العربية", !Loc.En ? Ui.Gold : new Color(1, 1, 1, 0.08f), !Loc.En ? Ui.Ink : Ui.GoldLight, 14, () => Loc.Set(false));
            var en = Ui.Button(lr, "English", Loc.En ? Ui.Gold : new Color(1, 1, 1, 0.08f), Loc.En ? Ui.Ink : Ui.GoldLight, 14, () => Loc.Set(true));
            At((RectTransform)ar.transform, 280, 100, 32); At((RectTransform)en.transform, 384, 100, 32);
            // the account
            var acc = Row(list, 96, Card);
            var t = Ui.Label(acc, "الحساب", 16, Color.white, StartAlign, Ui.Bold);
            t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(480, 26), new Vector2(0, -8));
            string g = user?.Str("google"), ph = user?.Str("phone"), em = user?.Str("email");
            string who = !string.IsNullOrEmpty(g) ? g : !string.IsNullOrEmpty(em) ? em : ph;
            var note = Ui.Label(acc, "", 13, Ui.GoldLight, StartAlign);
            note.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(480, 22), new Vector2(0, -34));
            if (string.IsNullOrEmpty(who))
            {
                note.Set(Loc.T("بتلعب كضيف. سجّل عشان فلوسك وتقدمك يتحفظوا."));
                var b = Ui.Button(acc, "تسجيل الدخول بجوجل", Color.white, Ui.Ink, 14, SignIn);
                var br = ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 32), new Vector2(0, 6));
                var gi = Ui.Picture(br, "G", Icon("google")); gi.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(8, 0));
            }
            else
            {
                note.Set(Loc.T("متسجل بـ") + " " + who);
                var b = Ui.Button(acc, "تسجيل الخروج", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 14, () => { app.Send(new Dictionary<string, object> { ["t"] = "logout" }); Close(); });
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(180, 32), new Vector2(0, 6));
            }
            // a new player: the friend code of who invited you (you both get 50K after your first game)
            if (user != null && user.Bool("canRefer"))
            {
                var rr = Row(list, 44, Card);
                At(Ui.Label(rr, "كود الدعوة", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 150, 40);
                var f = Field(rr, Loc.T("كود صاحبك"), 14, false);
                At((RectTransform)f.transform, 170, 140, 32);
                f.characterLimit = 8;
                var go = Ui.Button(rr, "تأكيد", Ui.Gold, Ui.Ink, 14, () => { Sfx.Tap(); var c = f.text.Trim(); if (c.Length >= 4) app.Send(new Dictionary<string, object> { ["t"] = "referral", ["code"] = c }); });
                At((RectTransform)go.transform, 320, 90, 32);
            }
            // help: a message straight to the team
            var sup = Row(list, 44, Card);
            At(Ui.Label(sup, "الدعم الفني", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 200, 40);
            var sb = Ui.Button(sup, "كلّمنا", Ui.Gold, Ui.Ink, 14, OpenSupport);
            At((RectTransform)sb.transform, 384, 100, 32);
            var ver = Row(list, 22);
            Ui.Label(ver, "Board Battles  ·  " + Application.version, 11, new Color(1, 1, 1, 0.4f)).rectTransform.Fill();
            var by = Row(list, 20);
            Ui.Label(by, "Powered by Qimma Labs  ·  2027", 11, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.7f), TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
        };
        refreshOpen();
    }

    /* ---------- the daily reward ---------- */

    static readonly long[] Streak = { 5_000, 8_000, 12_000, 18_000, 25_000, 35_000, 50_000 };

    GameObject dailyDot;
    bool dailyShown, dailyAsking;
    long dailyGot; int dailyGotDay;
    Action refreshDaily;
    Text dailyCountdown; long dailyNextAt;

    static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// The daily reward: a track of seven days that grows for each day in a row you come back. Today's card
    /// waits for you to collect it; once collected it counts down to tomorrow's.
    public void OpenDaily()
    {
        Panel(Loc.T("مكافأة النهارده"), out var body, new Vector2(660, 312));
        var area = Ui.Rect("Area", body); area.Fill();
        refreshDaily = refreshOpen = () =>
        {
            foreach (Transform c in area) Destroy(c.gameObject);
            dailyCountdown = null;
            var d = user?.Obj("daily");
            bool online = app.net != null && app.net.Online && user != null;
            int day = d != null ? Mathf.Max(1, (int)d.Num("day", 1)) : Mathf.Max(1, dailyGotDay);
            bool ready = d != null ? d.Bool("ready") : false;
            bool collected = d != null ? !ready : dailyGot > 0;
            long coins = d != null ? (long)d.Num("coins") : Streak[Mathf.Clamp(day, 1, 7) - 1];
            dailyNextAt = d != null ? (long)d.Num("nextAt") : 0;
            if (!collected && !ready) dailyAsking = false;
            int today = Mathf.Clamp(day, 1, 7);

            Ui.Label(area, Loc.T("ارجع كل يوم والمكافأة تكبر") + "  ·  " + Loc.T("يوم") + " " + day, 14, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(620, 24), new Vector2(0, 0));
            RectTransform nowCard = null;
            for (int i = 0; i < 7; i++)
            {
                int k = Loc.En ? i : 6 - i;
                bool past = i + 1 < today, now = i + 1 == today;
                bool got = past || (now && collected);
                var bg = now && !collected ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.95f) : got ? new Color(0.18f, 0.42f, 0.23f, 0.92f) : Card;
                var c = Ui.Box(area, "Day", bg).rectTransform;
                c.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, now ? 126 : 110), new Vector2((k - 3) * 87, now ? -34 : -42));
                if (now) { nowCard = c; c.gameObject.AddComponent<Outline>().effectColor = new Color(1, 0.95f, 0.7f, 0.9f); }
                var ink = now && !collected ? Ui.Ink : Ui.GoldLight;
                Ui.Label(c, Loc.T("يوم") + " " + (i + 1), 12, ink, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(78, 20), new Vector2(0, -6));
                // the reward grows: one coin, then a little pile, then a big one
                int pile = i < 2 ? 1 : i < 5 ? 2 : 3;
                for (int p = 0; p < pile; p++)
                {
                    float sz = now ? 34 : 28;
                    var ci = Ui.Picture(c, "Coin", Icon("coin"));
                    ci.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sz, sz), new Vector2((p - (pile - 1) / 2f) * 12, 4 + p % 2 * 5));
                    if (got && !now) ci.color = new Color(1, 1, 1, 0.55f);
                }
                if (got)
                {
                    var tick = Ui.Label(c, Loc.T("اتاخدت"), 11, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
                    tick.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(78, 18), new Vector2(0, -20));
                }
                Ui.Label(c, Loc.Short(Streak[i]), now ? 17 : 14, now && !collected ? Ui.Ink : Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(78, 22), new Vector2(0, 7));
            }
            if (day > 7)
                Ui.Label(area, Loc.T("من يوم ٧ وطالع: 50K كل يوم"), 12, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter).rectTransform
                    .Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 18), new Vector2(0, 52));

            Button b;
            if (!online)
            {
                b = Ui.Button(area, Loc.T("جاري الاتصال…"), new Color(1, 1, 1, 0.1f), Ui.GoldLight, 16, () => Sfx.Tap());
            }
            else if (ready)
            {
                b = Ui.Button(area, dailyAsking ? "…" : Loc.T("خد") + " +" + Loc.Short(coins), Ui.Gold, Ui.Ink, 18, () =>
                {
                    if (dailyAsking) return;
                    Sfx.Tap(); dailyAsking = true;
                    app.Send(new Dictionary<string, object> { ["t"] = "daily" });
                    refreshOpen?.Invoke();
                });
                if (nowCard != null) StartCoroutine(Pulse(nowCard));
            }
            else
            {
                b = Ui.Button(area, "", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 15, () => { Sfx.Tap(); Close(); });
                dailyCountdown = b.GetComponentInChildren<Text>();
                TickDaily();
            }
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 44), new Vector2(0, 2));
            if (dailyGot > 0 && nowCard != null)
            {
                // just collected: the card pops and the coins rise from it
                StartCoroutine(Pop(nowCard));
                var plus = Ui.Label(area, "+" + Loc.Short(dailyGot), 26, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
                plus.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
                plus.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(160, 36), nowCard.anchoredPosition + new Vector2(0, -20));
                StartCoroutine(Rise(plus));
                dailyGot = 0; dailyAsking = false;
            }
        };
        refreshOpen();
    }

    void TickDaily()
    {
        if (dailyCountdown == null) return;
        long left = Math.Max(0, dailyNextAt - NowMs()) / 1000;
        if (left <= 0) { dailyCountdown.Set(Loc.T("المكافأة جاهزة!")); return; }
        dailyCountdown.Set(Loc.T("المكافأة الجاية بعد") + "  " + (left / 3600).ToString("0") + ":" + (left / 60 % 60).ToString("00") + ":" + (left % 60).ToString("00"));
    }

    float dailyTick;
    void Update()
    {
        if (dailyCountdown == null) return;
        dailyTick += Time.unscaledDeltaTime;
        if (dailyTick < 0.5f) return;
        dailyTick = 0; TickDaily();
    }

    static IEnumerator Pulse(RectTransform r)
    {
        for (float t = 0; r != null; t += Time.unscaledDeltaTime) { r.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(t * 5)); yield return null; }
    }

    static IEnumerator Rise(Text t)
    {
        var r = t.rectTransform; var from = r.anchoredPosition;
        for (float k = 0; k < 1.4f; k += Time.unscaledDeltaTime)
        {
            if (t == null) yield break;
            r.anchoredPosition = from + new Vector2(0, 60 * Ui.Ease(k / 1.4f));
            var c = t.color; c.a = k < 1 ? 1 : 1 - (k - 1) / 0.4f; t.color = c;
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    /* ---------- contact support ---------- */

    public void OpenSupport()
    {
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        Panel(Loc.T("الدعم الفني"), out var body, new Vector2(560, 330));
        refreshOpen = null;
        Ui.Label(body, "اكتب مشكلتك أو اقتراحك وهنرد عليك في أقرب وقت", 14, Ui.GoldLight, TextAnchor.MiddleCenter).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(520, 24), new Vector2(0, 0));
        var msg = Field(body, "رسالتك…", 15, true);
        ((RectTransform)msg.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(520, 130), new Vector2(0, -30));
        var contact = Field(body, "إيميلك أو رقمك (اختياري)", 14, false);
        ((RectTransform)contact.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(520, 38), new Vector2(0, -168));
        contact.text = user?.Str("google") ?? "";
        var send = Ui.Button(body, "ابعت", Ui.Gold, Ui.Ink, 17, () =>
        {
            Sfx.Tap();
            var text = msg.text.Trim();
            if (text.Length < 3) { app.Toast(Loc.T("اكتب رسالتك الأول")); return; }
            app.Send(new Dictionary<string, object> { ["t"] = "support", ["text"] = text, ["contact"] = contact.text.Trim(), ["app"] = "android " + Application.version + (Loc.En ? " en" : " ar") });
            Close();
        });
        ((RectTransform)send.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(180, 40), new Vector2(0, 0));
    }

    static InputField Field(Transform parent, string hintText, int size, bool multi)
    {
        var bg = Ui.Box(parent, "Field", new Color(1, 1, 1, 0.94f));
        bg.raycastTarget = true;
        var text = Ui.Label(bg.transform, "", size, Ui.Ink, multi ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, Ui.Body);
        text.rectTransform.Fill(8); text.raycastTarget = false; text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        var hint = Ui.Label(bg.transform, hintText, size, new Color(0, 0, 0, 0.38f), multi ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, Ui.Body);
        hint.rectTransform.Fill(8);
        var f = bg.gameObject.AddComponent<InputField>();
        f.textComponent = text; f.placeholder = hint;
        f.lineType = multi ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        f.characterLimit = multi ? 2000 : 120;
        return f;
    }

    void SetView(bool board)
    {
        Sfx.Tap();
        if (app.cafe != null) app.cafe.SetView(board); else Cafe.BoardView = board;
        app.dominoTable?.ApplyView(); app.game?.ShowViewIcon();
        refreshOpen?.Invoke();
    }

    void Toggle(RectTransform list, string label, bool on, Action<bool> set)
    {
        var r = Row(list, 44, Card);
        At(Ui.Label(r, label, 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 200, 40);
        Button b = null;
        b = Ui.Button(r, on ? "تشغيل" : "إيقاف", on ? Ui.Gold : new Color(1, 1, 1, 0.08f), on ? Ui.Ink : Ui.GoldLight, 14, () =>
        {
            on = !on; set(on);
            b.GetComponent<Image>().color = on ? Ui.Gold : new Color(1, 1, 1, 0.08f);
            var t = b.GetComponentInChildren<Text>(); t.Set(Loc.T(on ? "تشغيل" : "إيقاف")); t.color = on ? Ui.Ink : Ui.GoldLight;
        });
        At((RectTransform)b.transform, 384, 100, 32);
    }

    /* ---------- cash out: coins to Vodafone Cash ---------- */

    public void OpenCashout()
    {
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        app.Send(new Dictionary<string, object> { ["t"] = "cashouts" });
        Panel(Loc.T("اسحب على فودافون كاش"), out var body, new Vector2(640, 350));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            long coins = (long)(user?.Num("coins") ?? 0);
            int canMillions = (int)Math.Min(cashMax, coins / Math.Max(1, cashCoins));
            cashMillions = Mathf.Clamp(cashMillions, 1, Math.Max(1, canMillions));
            bool signed = user != null && (!string.IsNullOrEmpty(user.Str("google")) || !string.IsNullOrEmpty(user.Str("phone")) || !string.IsNullOrEmpty(user.Str("email")));

            // the rate and your balance
            var top = Ui.Box(body, "Rate", Card).rectTransform;
            top.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 54), new Vector2(0, 0));
            var ci = Ui.Picture(top, "Coin", Icon("coin"));
            At(ci.rectTransform, 12, 34, 34);
            At(Ui.Label(top, Loc.T("كل") + " " + cashCoins.ToString("N0") + " " + Loc.T("كوين") + " = " + cashEgp + " " + Loc.T("جنيه"), 17, Ui.Gold, StartAlign, Ui.Bold).rectTransform, 54, 330, 30);
            var bal = Ui.Label(top, Loc.T("رصيدك") + ": " + coins.ToString("N0"), 14, Ui.GoldLight, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Bold);
            bal.rectTransform.Place(new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(210, 30), new Vector2(Loc.En ? -12 : 12, 0));

            var form = Ui.Rect("Form", body);
            form.anchorMin = Vector2.zero; form.anchorMax = Vector2.one; form.offsetMin = new Vector2(0, 0); form.offsetMax = new Vector2(0, -62);
            if (!signed)
            {
                Ui.Label(form, "سجّل دخول الأول عشان تقدر تسحب فلوسك، وعشان كوينزك تتحفظ", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
                    .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(580, 34), new Vector2(0, -8));
                var b = Ui.Button(form, "تسجيل الدخول بجوجل", Color.white, Ui.Ink, 15, SignIn);
                var br = ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(250, 40), new Vector2(0, -50));
                var gi = Ui.Picture(br, "G", Icon("google")); gi.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 22), new Vector2(10, 0));
            }
            else if (coins < cashCoins)
            {
                Ui.Label(form, Loc.T("فاضلك") + " " + (cashCoins - coins).ToString("N0") + " " + Loc.T("كوين وتقدر تسحب") + " " + cashEgp + " " + Loc.T("جنيه"), 17, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
                    .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(580, 34), new Vector2(0, -14));
                // how far you are
                var bar = Ui.Box(form, "Bar", new Color(1, 1, 1, 0.08f)).rectTransform;
                bar.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(460, 16), new Vector2(0, -58));
                var fill = Ui.Box(bar, "Fill", Ui.Gold).rectTransform;
                fill.anchorMin = new Vector2(0, 0); fill.anchorMax = new Vector2(Mathf.Clamp01(coins / (float)cashCoins), 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
                Ui.Label(form, "العب واكسب، وخد مكافأتك اليومية، وشوف إعلانات عشان تجمع أسرع", 13, Ui.GoldLight, TextAnchor.MiddleCenter).rectTransform
                    .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(580, 26), new Vector2(0, -86));
            }
            else
            {
                // how much: − N million = 10·N EGP +
                var amt = Ui.Rect("Amount", form).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 44), new Vector2(0, -2));
                var minus = Ui.Button(amt, "−", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 24, () => { Sfx.Tap(); cashMillions = Math.Max(1, cashMillions - 1); refreshOpen(); });
                ((RectTransform)minus.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44, 40), new Vector2(Loc.En ? -170 : 170, 0));
                var plus = Ui.Button(amt, "+", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 24, () => { Sfx.Tap(); cashMillions = Math.Min(Math.Max(1, canMillions), cashMillions + 1); refreshOpen(); });
                ((RectTransform)plus.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44, 40), new Vector2(Loc.En ? 170 : -170, 0));
                Ui.Label(amt, (cashMillions * cashCoins).ToString("N0") + " " + Loc.T("كوين") + "  =  " + (cashMillions * cashEgp) + " " + Loc.T("جنيه"), 19, Ui.Gold, TextAnchor.MiddleCenter, Ui.Display).rectTransform
                    .Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(290, 40), Vector2.zero);
                // the Vodafone Cash number
                walletField = WalletField(form);
                ((RectTransform)walletField.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 44), new Vector2(Loc.En ? -82 : 82, -56));
                var go = Ui.Button(form, "اطلب السحب", Ui.Gold, Ui.Ink, 17, () =>
                {
                    Sfx.Tap();
                    var phone = walletField.text.Trim();
                    if (phone.Length < 10) { app.Toast(Loc.T("اكتب رقم فودافون صحيح يبدأ بـ 010")); return; }
                    PlayerPrefs.SetString("wallet", phone);
                    app.Send(new Dictionary<string, object> { ["t"] = "cashout", ["phone"] = phone, ["millions"] = (double)cashMillions });
                });
                ((RectTransform)go.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, 44), new Vector2(Loc.En ? 160 : -160, -56));
            }
            // the promise
            var note = Ui.Label(form, Loc.T("الفلوس بتتحول على محفظة فودافون كاش خلال يوم عمل واحد"), 13, new Color(0.75f, 0.95f, 0.75f), TextAnchor.MiddleCenter, Ui.Bold);
            note.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 24), new Vector2(0, -112));
            // your requests
            var list = Ui.Rect("Requests", form);
            list.anchorMin = Vector2.zero; list.anchorMax = Vector2.one; list.offsetMin = Vector2.zero; list.offsetMax = new Vector2(0, -140);
            var rows = Scroll(list, 4);
            if (cashRows == null) Ui.Label(Row(rows, 24), "جاري التحميل…", 12, new Color(1, 1, 1, 0.5f)).rectTransform.Fill();
            else if (cashRows.Count == 0) Ui.Label(Row(rows, 24), "لسه مطلبتش سحب", 12, new Color(1, 1, 1, 0.5f)).rectTransform.Fill();
            else foreach (var o in cashRows)
            {
                var r = o as Dictionary<string, object>; if (r == null) continue;
                var st = r.Str("status");
                var row = Row(rows, 30, Card);
                string when = DateTimeOffset.FromUnixTimeMilliseconds((long)r.Num("ts")).ToLocalTime().ToString("dd/MM HH:mm");
                At(Ui.Label(row, "#" + (int)r.Num("id") + "  ·  " + (int)r.Num("egp") + " " + Loc.T("ج.م") + "  ·  " + r.Str("phone") + "  ·  " + when, 13, Color.white, StartAlign).rectTransform, 12, 420, 28);
                var stl = Ui.Label(row, st == "paid" ? "اتحولت" : st == "rejected" ? "اترفض (الكوينز رجعت)" : "بيتحول…", 13,
                    st == "paid" ? new Color(0.5f, 0.95f, 0.5f) : st == "rejected" ? new Color(1f, 0.55f, 0.5f) : Ui.Gold, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Bold);
                stl.rectTransform.Place(new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(170, 28), new Vector2(Loc.En ? -10 : 10, 0));
            }
        };
        refreshOpen();
    }

    static InputField WalletField(Transform parent)
    {
        var bg = Ui.Box(parent, "Wallet", new Color(1, 1, 1, 0.94f));
        bg.raycastTarget = true;
        var text = Ui.Label(bg.transform, "", 19, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold);
        text.rectTransform.Fill(6); text.raycastTarget = false; text.supportRichText = false;
        var hint = Ui.Label(bg.transform, "رقم فودافون كاش 010xxxxxxxx", 14, new Color(0, 0, 0, 0.38f), TextAnchor.MiddleCenter, Ui.Body);
        hint.rectTransform.Fill(6);
        var f = bg.gameObject.AddComponent<InputField>();
        f.textComponent = text; f.placeholder = hint; f.characterLimit = 14;
        f.contentType = InputField.ContentType.IntegerNumber;
        f.keyboardType = TouchScreenKeyboardType.PhonePad;
        f.text = PlayerPrefs.GetString("wallet", "");
        return f;
    }

    /* ---------- the shop ---------- */

    struct SkinItem { public string id, name; public long price; public Color face, pip; }
    static readonly SkinItem[] TileSkins =
    {
        new SkinItem { id = "tiles_ivory", name = "عاجي", price = 0 },
        new SkinItem { id = "tiles_ebony", name = "أبنوس", price = 150_000 },
        new SkinItem { id = "tiles_marble", name = "رخام", price = 300_000 },
        new SkinItem { id = "tiles_jade", name = "يشم", price = 450_000 },
        new SkinItem { id = "tiles_gold", name = "ذهب", price = 900_000 },
    };
    static readonly SkinItem[] DiceSkins =
    {
        new SkinItem { id = "dice_ivory", name = "عاجي", price = 0, face = new Color(0.96f, 0.93f, 0.85f), pip = new Color(0.1f, 0.07f, 0.05f) },
        new SkinItem { id = "dice_ruby", name = "ياقوت", price = 100_000, face = new Color(0.62f, 0.07f, 0.12f), pip = Color.white },
        new SkinItem { id = "dice_obsidian", name = "سبج", price = 200_000, face = new Color(0.08f, 0.08f, 0.1f), pip = new Color(0.95f, 0.8f, 0.4f) },
        new SkinItem { id = "dice_turquoise", name = "فيروز", price = 300_000, face = new Color(0.16f, 0.62f, 0.64f), pip = Color.white },
        new SkinItem { id = "dice_gold", name = "ذهب", price = 600_000, face = new Color(0.93f, 0.75f, 0.35f), pip = new Color(0.3f, 0.15f, 0.05f) },
    };
    static readonly (string id, long coins, int egp)[] Packs = { ("coins_50k", 50_000, 200), ("coins_100k", 100_000, 379), ("coins_500k", 500_000, 599), ("coins_1m", 1_000_000, 899) };

    public void OpenShop(int tab)
    {
        shopTab = tab;
        Panel(Loc.T("المتجر"), out var body, new Vector2(680, 340));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "عملات", "دومينو", "زهر", "طربوش", "مفرش", "مشروب", "بدون إعلانات" }, shopTab, i => { shopTab = i; refreshOpen(); });
            // your coins, bottom corner
            var have = Ui.Label(area, Loc.T("رصيدك") + ": " + ((long)(user?.Num("coins") ?? 0)).ToString("N0"), 13, Ui.GoldLight, StartAlign, Ui.Bold);
            have.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(620, 20), new Vector2(0, -6));
            var row = Ui.Rect("Cards", area); row.anchorMin = Vector2.zero; row.anchorMax = Vector2.one; row.offsetMin = new Vector2(0, 18); row.offsetMax = Vector2.zero;
            if (shopTab == 0)
                for (int i = 0; i < Packs.Length; i++)
                {
                    var p = Packs[i];
                    var c = ShopCard(row, i, Packs.Length);
                    var ci = Ui.Picture(c, "Coin", Icon("coin")); ci.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(56, 56), new Vector2(0, -14));
                    Ui.Label(c, Loc.Short(p.coins), 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Display).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(140, 30), new Vector2(0, -74));
                    BuyButton(c, p.egp + " " + Loc.T("ج.م"), Ui.Gold, Ui.Ink, () => app.Toast(Loc.T("الشراء بالفلوس جاي قريب")));
                }
            else if (shopTab == 1 || shopTab == 2)
            {
                var items = shopTab == 1 ? TileSkins : DiceSkins;
                for (int i = 0; i < items.Length; i++)
                {
                    var s = items[i];
                    var c = ShopCard(row, i, items.Length);
                    Texture tex = shopTab == 1 ? TileArt.Preview(s.id) : DiceArt(s.face, s.pip);
                    var pic = Ui.Picture(c, "Look", tex);
                    pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), shopTab == 1 ? new Vector2(96, 48) : new Vector2(56, 56), new Vector2(0, shopTab == 1 ? -24 : -16));
                    Ui.Label(c, s.name, 16, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(110, 24), new Vector2(0, -80));
                    bool owned = s.price == 0 || Owns(s.id);
                    string cat = shopTab == 1 ? "tiles" : "dice";
                    bool wearing = Equipped(cat) == s.id || (string.IsNullOrEmpty(Equipped(cat)) && s.price == 0);
                    if (wearing) BuyButton(c, Loc.T("متلبس"), new Color(1, 1, 1, 0.1f), Ui.GoldLight, null);
                    else if (owned) BuyButton(c, Loc.T("البس"), new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, () => app.Send(new Dictionary<string, object> { ["t"] = "equipSkin", ["id"] = s.id }));
                    else BuyButton(c, Loc.Short(s.price), Ui.Gold, Ui.Ink, () => { app.Send(new Dictionary<string, object> { ["t"] = "buySkin", ["id"] = s.id }); Sfx.Coins(); }, true);
                }
            }
            else if (shopTab >= 3 && shopTab <= 5)
            {
                // your look: the hat on your character, the cloth on your table, the drink by your place
                string cat = shopTab == 3 ? "hat" : shopTab == 4 ? "felt" : "drink";
                var items = LookItems[cat];
                for (int i = 0; i < items.Length; i++)
                {
                    var (id, price, pass) = items[i];
                    var c = ShopCard(row, i, items.Length);
                    float w = c.sizeDelta.x;
                    var pic = Ui.Picture(c, "Look", SkinPicture(id, out var tint)); pic.color = tint;
                    pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(Mathf.Min(56, w - 16), Mathf.Min(56, w - 16)), new Vector2(0, -14));
                    var nm = Ui.Label(c, SkinName(id), 13, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
                    nm.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w - 4, 36), new Vector2(0, -74));
                    nm.horizontalOverflow = HorizontalWrapMode.Wrap;
                    bool owned = (price == 0 && !pass) || Owns(id);
                    string worn = Wearing(cat);
                    bool wearing = worn == id || (string.IsNullOrEmpty(worn) && price == 0 && !pass);
                    if (wearing) BuyButton(c, Loc.T("متلبس"), new Color(1, 1, 1, 0.1f), Ui.GoldLight, null);
                    else if (owned) BuyButton(c, Loc.T("البس"), new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, () => app.Send(new Dictionary<string, object> { ["t"] = "equipSkin", ["id"] = id }));
                    else if (pass) BuyButton(c, Loc.T("الموسم"), new Color(1, 1, 1, 0.08f), Ui.Gold, () => { Close(); OpenSeason(); });
                    else BuyButton(c, Loc.Short(price), Ui.Gold, Ui.Ink, () => { app.Send(new Dictionary<string, object> { ["t"] = "buySkin", ["id"] = id }); Sfx.Coins(); }, true);
                }
                var note = Ui.Label(area, Loc.T(cat == "felt" ? "المفرش بيبان ليك على ترابيزتك" : cat == "hat" ? "الطربوش بيبان لكل اللي على الترابيزة (للرجالة)" : "المشروب بيبان قدامك لكل اللي على الترابيزة"), 11, new Color(1, 1, 1, 0.55f));
                note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(620, 16), new Vector2(0, 12));
            }
            else
            {
                var c = ShopCard(row, 0, 1);
                c.sizeDelta = new Vector2(300, c.sizeDelta.y);
                var vi = Ui.Picture(c, "Icon", Icon("video")); vi.color = Ui.Gold; vi.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(50, 50), new Vector2(0, -16));
                Ui.Label(c, "بدون إعلانات", 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 30), new Vector2(0, -76));
                BuyButton(c, "199 " + Loc.T("ج.م") + " / " + Loc.T("شهرياً"), Ui.Gold, Ui.Ink, () => app.Toast(Loc.T("الشراء بالفلوس جاي قريب")));
            }
        };
        refreshOpen();
    }

    bool Owns(string id) { var l = user?.Arr("skins"); return l != null && l.Contains(id); }
    string Equipped(string cat) => user?.Obj("equip")?.Str(cat);

    /// The hat, cloth and drink in the shop (prices as on the server: shared/skins.ts); pass: from the season pass only.
    static readonly Dictionary<string, (string id, long price, bool pass)[]> LookItems = new Dictionary<string, (string, long, bool)[]>
    {
        ["hat"] = new[] { ("hat_classic", 0L, false), ("hat_fez_red", 120_000L, false), ("hat_fez_black", 180_000L, false), ("hat_fez_green", 220_000L, false), ("hat_fez_navy", 260_000L, false), ("hat_fez_white", 0L, true), ("hat_fez_gold", 0L, true) },
        ["felt"] = new[] { ("felt_wood", 0L, false), ("felt_green", 150_000L, false), ("felt_red", 150_000L, false), ("felt_blue", 200_000L, false), ("felt_royal", 0L, true), ("felt_kilim", 0L, true) },
        ["drink"] = new[] { ("drink_tea", 0L, false), ("drink_mint", 80_000L, false), ("drink_coffee", 80_000L, false), ("drink_karkade", 120_000L, false), ("drink_sahlab", 0L, true), ("drink_qasab", 0L, true) },
    };

    static RectTransform ShopCard(RectTransform row, int i, int n)
    {
        const float Gap = 10;
        float W = Mathf.Min(122, (640f - (n - 1) * Gap) / Mathf.Max(1, n));
        int k = Loc.En ? i : n - 1 - i;
        var c = Ui.Box(row, "Card", Card).rectTransform;
        c.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(W, 170), new Vector2((k - (n - 1) / 2f) * (W + Gap), 4));
        return c;
    }

    static void BuyButton(RectTransform card, string label, Color bg, Color fg, Action onClick, bool coin = false)
    {
        var b = Ui.Button(card, label, bg, fg, 14, () => { Sfx.Tap(); onClick?.Invoke(); });
        var r = ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(card.sizeDelta.x - 16, 32), new Vector2(0, 8));
        if (onClick == null) b.interactable = false;
        if (coin)
        {
            var ci = Ui.Picture(r, "Coin", Icon("coin")); ci.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 18), new Vector2(8, 0));
        }
    }

    static readonly Dictionary<string, Texture2D> diceArt = new Dictionary<string, Texture2D>();
    static Texture2D DiceArt(Color face, Color pip)
    {
        string key = face.ToString() + pip;
        if (diceArt.TryGetValue(key, out var t)) return t;
        const int S = 96; const float R = 18;
        t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[S * S];
        Vector2[] pips = { new Vector2(28, 28), new Vector2(68, 28), new Vector2(48, 48), new Vector2(28, 68), new Vector2(68, 68) };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(R - x, x - (S - 1 - R))), dy = Mathf.Max(0, Mathf.Max(R - y, y - (S - 1 - R)));
                float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                var c = face * (0.9f + 0.1f * (1 - y / (float)S));
                foreach (var p in pips) { float d = Vector2.Distance(new Vector2(x, y), p); c = Color.Lerp(c, pip, Mathf.Clamp01(8.5f - d)); }
                c.a = a; px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply();
        diceArt[key] = t;
        return t;
    }

    /* ---------- the leaderboard ---------- */

    static readonly string[] GameIds = { "domino", "tawla", "chess", "ludo", "week" };
    static readonly long[] WeeklyPrizes = { 1_000_000, 500_000, 300_000, 200_000, 150_000, 100_000, 75_000, 50_000, 40_000, 30_000 };
    double lbEndsAt;

    public void OpenLeaders(int tab)
    {
        lbTab = tab; lbGame = GameIds[tab]; lbRows = null; lbMe = null;
        app.Send(new Dictionary<string, object> { ["t"] = "lb", ["game"] = lbGame });
        StartCoroutine(AskAgain(lbGame));
        Panel(Loc.T("المتصدرين"), out var body, new Vector2(600, 340));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "دومينو", "طاولة", "شطرنج", "لودو", "الأسبوع" }, lbTab, i => { Close(); OpenLeaders(i); });
            bool week = lbGame == "week";
            float top = 0;
            if (week)
            {
                // the weekly prizes, and how long is left
                double left = lbEndsAt > 0 ? lbEndsAt - App.LocalMs() : 0;
                int d = (int)(left / 86_400_000), h = (int)(left % 86_400_000 / 3_600_000);
                var info = Ui.Label(area, Loc.T("أول ١٠ بيكسبوا كوينز كل يوم اتنين") + "  ·  " + Loc.T("الأول") + " " + Loc.Short(WeeklyPrizes[0])
                    + (left > 0 ? "  ·  " + Loc.T("فاضل") + " " + d + Loc.T("ي") + " " + h + Loc.T("س") : ""), 12, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
                info.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 20), Vector2.zero);
                top = 22;
            }
            // the column heads
            var head = Ui.Rect("Head", area).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 22), new Vector2(0, -top));
            LbCells(head, Loc.T("الترتيب"), "", Loc.T(week ? "نقط" : "التقييم"), Loc.T(week ? "الجايزة" : "مكسب"), new Color(1, 1, 1, 0.5f), 12);
            var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(0, lbMe != null ? 40 : 0); listArea.offsetMax = new Vector2(0, -24 - top);
            if (lbRows == null || lbRows.Count == 0)
            {
                Ui.Label(listArea, lbRows == null ? Loc.T("جاري التحميل…") : Loc.T("مفيش نتايج لسه"), 15, Ui.GoldLight).rectTransform.Fill();
                return;
            }
            var list = Scroll(listArea, 4);
            foreach (var o in lbRows)
            {
                if (!(o is Dictionary<string, object> r)) continue;
                bool me = r.Bool("me");
                int rank = (int)r.Num("rank");
                var row = Row(list, 32, me ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
                string lvl = r.Num("level") > 0 ? "  ·  " + Loc.T("مستوى") + " " + (int)r.Num("level") : "";
                string last = week ? (rank <= WeeklyPrizes.Length ? Loc.Short(WeeklyPrizes[rank - 1]) : "—") : ((int)r.Num("wins")).ToString();
                LbCells(row, rank.ToString(), r.Str("name") + lvl, ((int)r.Num("rating")).ToString(), last, rank == 1 ? Ui.Gold : Color.white, 14);
                // tap a row: the player's card
                string rpid = r.Str("pid"), rname = r.Str("name"); int rch = (int)r.Num("ch");
                if (!string.IsNullOrEmpty(rpid)) { var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None; hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(rpid, rname, rch); }); }
            }
            if (lbMe != null)
            {
                var mine = Ui.Box(area, "Me", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.25f)).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 34), Vector2.zero);
                int myRank = (int)lbMe.Num("rank");
                string myLast = week ? (myRank <= WeeklyPrizes.Length ? Loc.Short(WeeklyPrizes[myRank - 1]) : "—") : ((int)lbMe.Num("wins")).ToString();
                LbCells(mine, myRank.ToString(), Loc.T("مركزك") + " · " + lbMe.Str("name"), ((int)lbMe.Num("rating")).ToString(), myLast, Ui.Gold, 14);
            }
        };
        refreshOpen();
    }

    /// No answer yet after a moment (a dropped message, a sleepy server)? Ask once more.
    IEnumerator AskAgain(string game)
    {
        yield return new WaitForSeconds(1f);
        if (lbRows == null && lbGame == game && open != null) { Debug.Log("[menus] asking for the leaderboard again"); app.Send(new Dictionary<string, object> { ["t"] = "lb", ["game"] = game }); }
    }

    static void LbCells(RectTransform row, string rank, string name, string rating, string wins, Color c, int size)
    {
        At(Ui.Label(row, rank, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 6, 50, 30);
        At(Ui.Label(row, name, size, c, StartAlign, Ui.Bold).rectTransform, 66, 280, 30);
        At(Ui.Label(row, rating, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 360, 90, 30);
        At(Ui.Label(row, wins, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 460, 90, 30);
    }

    /* ---------- how to play ---------- */

    static readonly string[][] HowAr =
    {
        new[] {
            "• كل لاعب بياخد ٧ أحجار، والباقي في النُّص (البنك).",
            "• في دورك حط حجر رقمه زي أحد طرفي الخط.",
            "• مفيش حجر يركب؟ اسحب من البنك، ولو خلص قول باص.",
            "• أول واحد يخلّص أحجاره يكسب الجولة.",
            "• بياخد مجموع النقط اللي في إيد التانيين.",
            "• لو اللعبة اتقفلت، الأقل نقط في إيده يكسب.",
            "• أول من يوصل للهدف (١٠١ مثلاً) يكسب المباراة والرهان.",
        },
        new[] {
            "• كل لاعب عنده ١٥ قشاط بيلفّوا عكس بعض.",
            "• ارمي الزهرين وحرّك قشاطاتك بعدد كل زهرة.",
            "• الدُّش (زهرين زي بعض) بيتلعب ٤ مرات.",
            "• القشاط لوحده في خانة ممكن يتضرب ويرجع للأول.",
            "• مينفعش تنزل في خانة فيها قشاطين أو أكتر للخصم.",
            "• لما كل قشاطاتك توصل بيتك، ابدأ تطلّعهم برّه.",
            "• أول واحد يطلّع الـ ١٥ يكسب.",
        },
        new[] {
            "• الأبيض يبدأ، وكل لاعب يحرّك قطعة واحدة في دوره.",
            "• العسكري لقدّام، والحصان على شكل L.",
            "• الفيل بالوِرب، والطابية مستقيم، والوزير الاتنين.",
            "• الملك خطوة واحدة في أي اتجاه.",
            "• كِش ملك: الملك مهدد ولازم تحميه.",
            "• كِش مات: الملك مهدد ومفيش مهرب، والمباراة خلصت.",
            "• التبييت والأخذ في المرور مسموحين.",
        },
        new[] {
            "• كل لاعب عنده ٤ قطع في البيت.",
            "• لازم ستة عشان تطلّع قطعة على الطريق.",
            "• حرّك قطعة بعدد الزهر، والستة بتديك دور زيادة.",
            "• لو نزلت على قطعة خصم بترجّعها بيتها.",
            "• الخانات المعلّمة بنجمة آمنة.",
            "• أول من يدخّل قطعه الأربعة النُّص يكسب.",
        },
    };
    static readonly string[][] HowEn =
    {
        new[] {
            "• Everyone gets 7 tiles; the rest stay in the boneyard.",
            "• On your turn, play a tile that matches an end of the line.",
            "• Nothing fits? Draw from the boneyard, or pass when it's empty.",
            "• The first to play all their tiles wins the round,",
            "  scoring the pips left in everyone else's hands.",
            "• A blocked game goes to the lightest hand.",
            "• First to the target (say 101) wins the match and the stake.",
        },
        new[] {
            "• Each player has 15 checkers moving in opposite directions.",
            "• Roll two dice and move by each die.",
            "• Doubles are played four times.",
            "• A single checker can be hit and sent back to the start.",
            "• You can't land on a point held by 2+ enemy checkers.",
            "• Once all your checkers are home, bear them off.",
            "• The first to bear off all 15 wins.",
        },
        new[] {
            "• White moves first; one piece per turn.",
            "• Pawns move forward, knights jump in an L.",
            "• Bishops go diagonally, rooks straight, the queen both.",
            "• The king moves one square in any direction.",
            "• Check: your king is attacked and must be saved.",
            "• Checkmate: no way out — the game is over.",
            "• Castling and en passant are allowed.",
        },
        new[] {
            "• Everyone has 4 pieces in their base.",
            "• Roll a six to bring a piece out onto the track.",
            "• Move by the die; a six gives you another roll.",
            "• Land on an opponent to send it back to its base.",
            "• Star squares are safe.",
            "• The first to get all 4 pieces home wins.",
        },
    };

    public void OpenHowTo(int tab)
    {
        howTab = tab;
        Panel(Loc.T("طريقة اللعب"), out var body, new Vector2(600, 340));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "دومينو", "طاولة", "شطرنج", "لودو" }, howTab, i => { howTab = i; refreshOpen(); });
            var list = Scroll(area, 2);
            foreach (var line in (Loc.En ? HowEn : HowAr)[howTab])
            {
                var r = Row(list, 30);
                var t = Ui.Label(r, line, 15, Color.white, StartAlign);
                t.rectTransform.Fill(8);
            }
        };
        refreshOpen();
    }

    /* ---------- watch an ad ---------- */

    public void WatchAd()
    {
        Sfx.Tap();
        if (adShowing) return;
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        if (user != null && user.Num("adsLeft") <= 0) { app.Toast(Loc.T("خلصت إعلانات النهارده، تعالى بكرة")); return; }
        app.Send(new Dictionary<string, object> { ["t"] = "adStart" });
    }

    /// The video ad. Until an ad network is set up for this version, the game shows its own five-second
    /// spot; when it ends, the server pays (one reward per ticket, ten a day).
    IEnumerator PlayAd(string ticket)
    {
        adShowing = true;
        var root = Ui.Rect("Ad", canvas.transform).Fill();
        var bg = Ui.Picture(root, "Bg", Ui.Radial(new Color32(0x7a, 0x14, 0x24, 0xff), new Color32(0x4a, 0x0b, 0x16, 0xff), new Color32(0x1c, 0x06, 0x09, 0xff)));
        bg.rectTransform.Fill(); bg.raycastTarget = true;
        var tag = Ui.Box(root, "Tag", new Color(0, 0, 0, 0.5f)); tag.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, 26), new Vector2(12, -12));
        Ui.Label(tag.transform, "إعلان", 13, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
        var logo = Ui.Picture(root, "Logo", Resources.Load<Texture2D>("UI/Logo"));
        logo.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180, 210), new Vector2(0, 40));
        Ui.Label(root, "العب بورد باتلز مع أصحابك", 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600, 30), new Vector2(0, -88));
        Ui.Label(root, "دومينو · طاولة · شطرنج · لودو", 15, Ui.GoldLight).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(600, 24), new Vector2(0, -116));
        var count = Ui.Label(root, "", 14, Color.white, TextAnchor.MiddleRight, Ui.Bold);
        count.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(240, 26), new Vector2(-14, -12));
        for (float t = 5f; t > 0; t -= Time.deltaTime)
        {
            count.Set(Loc.T("المكافأة بعد") + " " + Mathf.CeilToInt(t));
            logo.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(Time.time * 3) * 0.03f);
            yield return null;
        }
        app.Send(new Dictionary<string, object> { ["t"] = "adWatched", ["ticket"] = ticket });
        count.Set(Loc.T("+٥٬٠٠٠ عملة"));
        yield return new WaitForSeconds(0.9f);
        Destroy(root.gameObject);
        adShowing = false;
    }

    /* ---------- Google sign-in ---------- */

    void SignIn()
    {
        Sfx.Tap();
        if (!googleOn) { app.Toast(Loc.T("تسجيل الدخول بجوجل مش متاح على السيرفر ده")); return; }
        // Google's page opens in the browser; the server sends the player back with a one-time ticket
        // (boardbattles' app link, ahwagames://auth?ticket=…), handled in OnLink
        Application.OpenURL(app.net.HttpBase + "/auth/google/start?to=app");
    }

    string pendingJoin;
    IEnumerator JoinSoon(string code) { yield return new WaitForSeconds(0.5f); app.JoinRoom(code); }
    void OnLink(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        // a "Play with me" link: into the friend's room as soon as we're connected
        if (url.StartsWith("ahwagames://join"))
        {
            int c = url.IndexOf("code=", StringComparison.Ordinal);
            if (c < 0) return;
            var code = Uri.UnescapeDataString(url.Substring(c + 5).Split('&')[0]).Trim();
            if (code.Length < 4) return;
            Debug.Log("[menus] invite link");
            if (app.net != null && app.net.Online && user != null) app.JoinRoom(code); else pendingJoin = code;
            return;
        }
        if (!url.StartsWith("ahwagames://auth")) return;
        int q = url.IndexOf("ticket=", StringComparison.Ordinal);
        if (q < 0) return;
        var ticket = Uri.UnescapeDataString(url.Substring(q + 7).Split('&')[0]);
        if (app.net != null && app.net.Online) app.Send(new Dictionary<string, object> { ["t"] = "ticket", ["ticket"] = ticket });
        else pendingTicket = ticket;
    }
}
