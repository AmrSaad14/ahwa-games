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
    bool googleOn = true, appleOn;
    string pendingTicket;
    // the monthly prizes: what a winner gets, and the chip that shows when you won one
    int prizeEgp = 500; bool prizeOn = true;
    GameObject prizeChip; Text prizeChipText;
    RectTransform eventChip; RawImage eventIcon; Text eventText;
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
        var rail = Ui.Rect("Rail", home).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, 290), new Vector2(10, -80));
        railButtons.Add(RailButton(rail, 0, "gift", "يومي", OpenDaily)); dailyDot = Dot(railButtons[0]);
        railButtons.Add(RailButton(rail, 1, "target", "المهام", OpenMissions)); missionsDot = Dot(railButtons[1]);
        railButtons.Add(seasonButton = RailButton(rail, 2, "pass", "الموسم", () => OpenSeason())); seasonDot = Dot(seasonButton);
        railButtons.Add(RailButton(rail, 3, "friends", "صحابك", OpenFriends)); friendsDot = Dot(railButtons[3]);
        railButtons.Add(RailButton(rail, 4, "shop", "المتجر", () => OpenShop(0)));
        railButtons.Add(RailButton(rail, 5, "chest", "مجموعتي", () => OpenCollection(0)));
        // along the top, beside the logo: the tournament, the leaderboard, how to play, settings
        var top = Ui.Rect("Top", home).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, 54), new Vector2(82, -8));
        topButtons.Add(tourneyButton = TopButton(top, 0, "cup", "البطولة", () => OpenTourney())); tourneyDot = Dot(tourneyButton);
        topButtons.Add(TopButton(top, 1, "trophy", "المتصدرين", () => OpenLeaders(0)));
        topButtons.Add(TopButton(top, 2, "help", "طريقة اللعب", () => OpenHowTo(0)));
        topButtons.Add(TopButton(top, 3, "settings", "الإعدادات", OpenSettings));
        // the monthly prize: shown when you won one (to give your Vodafone Cash number), or one is on its way
        var prize = Chip(home, "trophy", "جايزتك", new Vector2(-252, -56), 128, new Color32(0xc0, 0x1f, 0x2a, 0xf0), Color.white, () => { Sfx.Tap(); OpenPrizes(); });
        prizeChip = prize.gameObject; prizeChipText = prize.GetComponentInChildren<Text>(); prizeChip.SetActive(false);
        // an event on (Ramadan, Eid…): a banner at the top with what's special
        var evb = Ui.Button(home, "", new Color32(0x3a, 0x1c, 0x66, 0xf0), Ui.Gold, 13, () => { Sfx.Tap(); OpenEvent(); });
        eventChip = ((RectTransform)evb.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 38), new Vector2(0, -64));
        evb.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.6f);
        eventIcon = Ui.Picture(eventChip, "Icon", Icon("t_lantern")); eventIcon.color = Ui.Gold;
        eventIcon.rectTransform.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(24, 24), new Vector2(Loc.En ? 10 : -10, 0));
        eventText = evb.GetComponentInChildren<Text>(); eventText.rectTransform.offsetMin = new Vector2(30, 0); eventText.rectTransform.offsetMax = new Vector2(-30, 0);
        eventChip.gameObject.SetActive(false);

        // along the top, left of your name and coins: watch an ad, sign in, language
        var ad = Chip(home, "video", "+2K", new Vector2(-252, -10), 98, new Color32(0x2f, 0x6b, 0x3a, 0xf0), Color.white, WatchAd);
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
    // the home screen's buttons, so a section the dashboard switched off can be hidden and the rest close up
    readonly List<RectTransform> railButtons = new List<RectTransform>(), topButtons = new List<RectTransform>();
    RectTransform seasonButton, tourneyButton;
    /// The tournament section and the season pass, as the server says (the dashboard switches them).
    public bool TourneyOn { get; private set; } = true;
    public bool SeasonOn { get; private set; } = true;

    void ApplyFeatures(Dictionary<string, object> f)
    {
        TourneyOn = f == null || f.Bool("tourney");
        SeasonOn = f == null || f.Bool("season");
        seasonButton.gameObject.SetActive(SeasonOn);
        tourneyButton.gameObject.SetActive(TourneyOn);
        int k = 0;
        foreach (var b in railButtons) if (b.gameObject.activeSelf) b.anchoredPosition = new Vector2(0, -(k++) * 48);
        k = 0;
        foreach (var b in topButtons) if (b.gameObject.activeSelf) b.anchoredPosition = new Vector2((k++) * 60, 0);
    }

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
        var r = ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, 44), new Vector2(0, -i * 48));
        b.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.3f);
        var ic = Ui.Picture(r, "Icon", Icon(icon)); ic.color = Ui.Gold;
        ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(22, 22), new Vector2(0, -4));
        var t = Ui.Label(r, label, 9, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(60, 16), new Vector2(0, 1));
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
        bool signed = !string.IsNullOrEmpty(u.Str("google")) || !string.IsNullOrEmpty(u.Str("apple")) || !string.IsNullOrEmpty(u.Str("phone")) || !string.IsNullOrEmpty(u.Str("email"));
        signChip.SetActive(!signed);
        ApplyFeatures(u.Obj("features"));
        // the event on now: its limited skins in the shop, and the banner
        var ev = u.Obj("event");
        Skins.EventKind = ev?.Str("kind");
        eventChip.gameObject.SetActive(ev != null);
        if (ev != null)
        {
            eventIcon.texture = Icon(ev.Str("kind") == "eid" ? "t_crescent" : "t_lantern");
            double sp = ev.Num("spMult", 1);
            eventText.Set((Loc.En ? ev.Str("en") : ev.Str("ar")) + (sp > 1 ? "  ·  " + Loc.T("نقط الموسم") + " ×" + sp : ""));
        }
        var pz = u.Obj("prize");
        if (pz != null) { prizeOn = pz.Bool("on"); prizeEgp = (int)pz.Num("egp", prizeEgp); }
        // a prize to claim (or one on its way): the red chip on the home screen
        int waiting = 0, coming = 0;
        foreach (var o in u.Arr("prizes") ?? new List<object>()) if (o is Dictionary<string, object> p) { if (p.Str("status") == "unclaimed") waiting++; else if (p.Str("status") == "pending") coming++; }
        prizeChip.SetActive(waiting + coming > 0);
        prizeChipText.Set(Loc.T(waiting > 0 ? "جايزتك" : "جايزتك جاية"));
        int left = (int)u.Num("adsLeft");
        adReward = (long)u.Num("adReward", adReward);
        adText.Set(left > 0 ? AdLabel : "0");
        var eq = u.Obj("equip");
        Skins.SetUser(u);
        Billing.SetUser(u.Str("id"));
        TileArt.SetSkin(Skins.Worn("tiles"));
        Look.MyFelt = Skins.Worn("felt");
        var d = u.Obj("daily");
        bool ready = d != null && d.Bool("ready");
        dailyDot.SetActive(ready);
        UpdateDots();
        // a friend's invite code from Google Play (or a link) waiting: credit them, once the account is new
        // a new player: the first-game guide, offered once (an easy game of dominoes with hints, and a gift)
        if (!u.Bool("tutorial") && u.Num("games") == 0 && PlayerPrefs.GetInt("guideOffered", 0) == 0 && !IsOpen && app.view == null && !App.Recording && !AdFilm.Any) { PlayerPrefs.SetInt("guideOffered", 1); OfferGuide(); }
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
                if (login != null) { googleOn = login.Bool("google"); appleOn = login.Bool("apple"); }
                if (pendingTicket != null) { app.Send(new Dictionary<string, object> { ["t"] = "ticket", ["ticket"] = pendingTicket }); pendingTicket = null; }
                if (pendingJoin != null) { var j = pendingJoin; pendingJoin = null; StartCoroutine(JoinSoon(j)); }
                break;
            case "signedIn":
                app.Toast(Loc.T(m.Str("via") == "apple" ? "أهلاً! اتسجلت بحساب أبل" : "أهلاً! اتسجلت بحساب جوجل"));
                if (open != null) Close();
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
                if (app.view == null) ShowCard(Loc.T("مبروك!"), Loc.T("جيت المركز") + " " + (int)m.Num("rank") + " " + Loc.T("في ترتيب الأسبوع") + "  ·  +" + Loc.Short((long)m.Num("coins")) + " " + Loc.T("كوين"), null, true);
                else app.Toast(Loc.T("مبروك! جيت المركز") + " " + (int)m.Num("rank") + "  +" + Loc.Short((long)m.Num("coins")));
                Sfx.Coins(); Sfx.Buzz();
                break;
            case "lb":
                lbRows = m.Arr("rows") ?? new List<object>(); lbMe = m.Obj("me");
                if (m.Str("game") == "week") lbEndsAt = m.Num("endsAt");
                lbMonth = m.Obj("month");
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
            case "supportSent":
                app.Toast(Loc.T("وصلتنا رسالتك، شكراً! هنرد عليك قريب"));
                Sfx.Chime();
                break;
            case "prizeClaimed":
                app.Toast(Loc.T("وصلنا رقمك! هنحولك الجايزة خلال يومين شغل"), 6f);
                Sfx.Coins(); Sfx.Buzz();
                break;
            case "prizeWon":
                // you were the best of a game last month: the money, and where to put your number
                {
                    var tt = Titles.List.Find(x => x.monthly == m.Str("game"));
                    string head = Loc.T("كسبت") + " " + (int)m.Num("egp") + " " + Loc.T("جنيه") + "!";
                    if (app.view == null) ShowCard(head, (tt != null ? tt.Name + ". " : "") + Loc.T("دوس على «جايزتك» واكتب رقم فودافون كاش"), tt?.id, true, head + " " + (tt != null ? tt.Name : ""));
                    else app.Toast(head + " 🏆", 6f);
                    Sfx.Coins();
                }
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
            case "prize_gone": app.Toast(Loc.T("الجايزة دي مش متاحة")); return true;
            case "prize_waiting": app.Toast(Loc.T("رقمك وصلنا، الجايزة في الطريق")); return true;
            case "bad_amount": app.Toast(Loc.T("المبلغ مش مظبوط")); return true;
            case "slow_down": app.Toast(Loc.T("استنى شوية وجرّب تاني")); return true;
            case "support_empty": app.Toast(Loc.T("اكتب رسالتك الأول")); return true;
            case "chat_muted": app.Toast(Loc.T("الشات مقفول عندك شوية بسبب بلاغات")); return true;
            case "mission_not_done": app.Toast(Loc.T("لسه المهمة مخلصتش")); return true;
            case "already_claimed": app.Toast(Loc.T("خدتها خلاص")); return true;
            case "tier_locked": app.Toast(Loc.T("لسه موصلتش للمستوى ده")); return true;
            case "season_off": app.Toast(Loc.T("الموسم مش متاح دلوقتي")); return true;
            case "tourney_off": app.Toast(Loc.T("البطولات مش متاحة دلوقتي")); return true;
            case "not_playing": app.Toast(Loc.T("مش بيلعب دلوقتي")); return true;
            case "not_watchable": app.Toast(Loc.T("الماتش ده مينفعش تتفرج عليه")); return true;
            case "in_game": app.Toast(Loc.T("إنت في ماتش دلوقتي")); return true;
            case "group_not_found": app.Toast(Loc.T("الشلة مش موجودة")); return true;
            case "group_full": app.Toast(Loc.T("الشلة مليانة")); return true;
            case "too_many_groups": app.Toast(Loc.T("إنت في ٥ شلل خلاص")); return true;
            case "not_member": app.Toast(Loc.T("مش في الشلة دي")); return true;
            case "bad_title": app.Toast(Loc.T("اللقب ده مش عندك")); return true;
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

    bool deleteArmed;
    public void OpenSettings()
    {
        deleteArmed = false;
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
            string g = user?.Str("google"), ap = user?.Str("apple"), ph = user?.Str("phone"), em = user?.Str("email");
            string who = !string.IsNullOrEmpty(g) ? g : !string.IsNullOrEmpty(ap) ? ap : !string.IsNullOrEmpty(em) ? em : ph;
            var note = Ui.Label(acc, "", 13, Ui.GoldLight, StartAlign);
            note.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(480, 22), new Vector2(0, -34));
            if (string.IsNullOrEmpty(who))
            {
                note.Set(Loc.T("بتلعب كضيف. سجّل عشان فلوسك وتقدمك يتحفظوا."));
                // Google, and Apple when the server has it
                var b = Ui.Button(acc, "الدخول بجوجل", Color.white, Ui.Ink, 14, () => SignInWith("google"));
                var br = ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 32), new Vector2(appleOn ? (Loc.En ? -106 : 106) : 0, 6));
                var gi = Ui.Picture(br, "G", Icon("google")); gi.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 20), new Vector2(8, 0));
                if (appleOn)
                {
                    var ab = Ui.Button(acc, "الدخول بأبل", Color.black, Color.white, 14, () => SignInWith("apple"));
                    ((RectTransform)ab.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 32), new Vector2(Loc.En ? 106 : -106, 6));
                }
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
            // the privacy policy and the terms (pages on the server), and deleting your account
            string web = app.net != null ? app.net.HttpBase : "https://ahwa-games.a7medhussien.com";
            var lg = Row(list, 44, Card);
            At(Ui.Label(lg, "الخصوصية والشروط", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 200, 40);
            var pv = Ui.Button(lg, "الخصوصية", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 14, () => { Sfx.Tap(); Application.OpenURL(web + "/privacy"); });
            var tm = Ui.Button(lg, "الشروط", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 14, () => { Sfx.Tap(); Application.OpenURL(web + "/terms"); });
            At((RectTransform)pv.transform, 280, 100, 32); At((RectTransform)tm.transform, 384, 100, 32);
            if (Ads.PrivacyOptionsRequired)
            {
                var adp = Row(list, 44, Card);
                At(Ui.Label(adp, "خصوصية الإعلانات", 16, Color.white, StartAlign, Ui.Bold).rectTransform, 14, 200, 40);
                var apb = Ui.Button(adp, "غيّر", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 14, () => { Sfx.Tap(); Ads.ShowPrivacyOptions(); });
                At((RectTransform)apb.transform, 384, 100, 32);
            }
            var del = Row(list, 44, Card);
            At(Ui.Label(del, "امسح حسابي", 16, new Color(1f, 0.6f, 0.55f), StartAlign, Ui.Bold).rectTransform, 14, 200, 40);
            var dl = Ui.Button(del, deleteArmed ? "متأكد؟ دوس تاني" : "امسح", new Color32(0x8a, 0x22, 0x1c, 0xff), Color.white, 14, () =>
            {
                Sfx.Tap();
                if (!deleteArmed) { deleteArmed = true; app.Toast(Loc.T("الحساب والكوينز والسكينز هيتمسحوا خالص. دوس تاني للتأكيد")); refreshOpen(); return; }
                deleteArmed = false;
                app.Send(new Dictionary<string, object> { ["t"] = "delete" });
                app.Toast(Loc.T("اتمسح حسابك. بدأت من جديد كضيف"));
                Close();
            });
            At((RectTransform)dl.transform, 334, 150, 32);
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

    /* ---------- monthly prizes: Vodafone Cash for the best player of each game ---------- */

    public void OpenPrizes()
    {
        Panel(Loc.T("جوايز الشهر"), out var body, new Vector2(640, 350));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            bool signed = user != null && (!string.IsNullOrEmpty(user.Str("google")) || !string.IsNullOrEmpty(user.Str("apple")) || !string.IsNullOrEmpty(user.Str("phone")) || !string.IsNullOrEmpty(user.Str("email")));
            // how it works
            var top = Ui.Box(body, "How", Card).rectTransform;
            top.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 50), new Vector2(0, 0));
            At(Ui.Picture(top, "Cup", Icon("trophy")).rectTransform, 12, 30, 30).GetComponent<RawImage>().color = Ui.Gold;
            var how = Ui.Label(top, "", 13, Ui.Gold, StartAlign, Ui.Bold);
            how.Set(prizeOn ? Loc.T("كل شهر، الأول في ترتيب الشهر لكل لعبة بيكسب") + " " + prizeEgp + " " + Loc.T("جنيه على فودافون كاش + لقب لاعب الشهر") : Loc.T("جوايز الشهر متوقفة دلوقتي"));
            how.horizontalOverflow = HorizontalWrapMode.Wrap; how.resizeTextForBestFit = true; how.resizeTextMinSize = 9; how.resizeTextMaxSize = 13;
            At(how.rectTransform, 50, 540, 44);

            var listArea = Ui.Rect("List", body); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(0, 26); listArea.offsetMax = new Vector2(0, -58);
            var rows = Scroll(listArea, 6);
            var mine = user?.Arr("prizes") ?? new List<object>();
            if (mine.Count == 0)
                Ui.Label(Row(rows, 60), "لسه مكسبتش جايزة. العب أونلاين واطلع الأول في ترتيب الشهر!", 14, Ui.GoldLight).rectTransform.Fill();
            foreach (var o in mine)
            {
                if (!(o is Dictionary<string, object> p)) continue;
                string st = p.Str("status"), game = p.Str("game");
                int id = (int)p.Num("id"), egp = (int)p.Num("egp");
                bool open = st == "unclaimed";
                var row = Row(rows, open ? 100 : 40, open ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.16f) : Card);
                var what = Ui.Label(row, "", 15, Color.white, StartAlign, Ui.Bold);
                var tt = Titles.List.Find(x => x.monthly == game);
                what.Set(egp + " " + Loc.T("جنيه") + "  ·  " + (tt != null ? tt.Name : game) + "  ·  " + p.Str("month"));
                what.rectTransform.Place(new Vector2(Loc.En ? 0 : 1, 1), new Vector2(Loc.En ? 0 : 1, 1), new Vector2(420, 36), new Vector2(Loc.En ? 12 : -12, -2));
                string stText = st == "pending" ? Loc.T("بيتحول…") + " " + p.Str("phone") : st == "paid" ? Loc.T("اتحولت") + " ✓" : st == "rejected" ? Loc.T("اترفضت") : st == "expired" ? Loc.T("انتهى وقتها") : Loc.T("اكتب رقمك");
                var stl = Ui.Label(row, "", 13, st == "paid" ? new Color(0.5f, 0.95f, 0.5f) : st == "rejected" || st == "expired" ? new Color(1f, 0.55f, 0.5f) : Ui.Gold, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Bold);
                stl.Set(stText);
                stl.rectTransform.Place(new Vector2(Loc.En ? 1 : 0, 1), new Vector2(Loc.En ? 1 : 0, 1), new Vector2(170, 36), new Vector2(Loc.En ? -10 : 10, -2));
                if (!open) continue;
                if (!string.IsNullOrEmpty(p.Str("note"))) { var nt = Ui.Label(row, "", 11, new Color(1f, 0.7f, 0.6f), TextAnchor.MiddleCenter); nt.Set(p.Str("note")); nt.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(580, 16), new Vector2(0, -34)); }
                if (!signed)
                {
                    var hint = Ui.Label(row, "سجّل دخول الأول عشان نقدر نحولك الجايزة", 13, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
                    hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(330, 40), new Vector2(Loc.En ? -120 : 120, 8));
                    var sb = Ui.Button(row, "تسجيل الدخول بجوجل", Color.white, Ui.Ink, 14, SignIn);
                    ((RectTransform)sb.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 40), new Vector2(Loc.En ? 180 : -180, 8));
                    continue;
                }
                var field = WalletField(row);
                ((RectTransform)field.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(300, 42), new Vector2(Loc.En ? -90 : 90, 8));
                var go = Ui.Button(row, "ابعت الرقم", Ui.Gold, Ui.Ink, 16, () =>
                {
                    Sfx.Tap();
                    var phone = field.text.Trim();
                    if (phone.Length < 10) { app.Toast(Loc.T("اكتب رقم فودافون صحيح يبدأ بـ 010")); return; }
                    PlayerPrefs.SetString("wallet", phone);
                    app.Send(new Dictionary<string, object> { ["t"] = "prizeClaim", ["id"] = (double)id, ["phone"] = phone });
                });
                ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 42), new Vector2(Loc.En ? 160 : -160, 8));
            }
            var terms = Ui.Label(body, "", 10, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter);
            terms.Set(Loc.T("لازم يكون عندك ١٨ سنة وساكن في مصر. اللعب ببلاش، ومفيش حاجة بتتشترى عشان تكسب"));
            terms.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 20), new Vector2(0, 2));
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

    // coins are not sold: the shop's first tab is free coins (a rewarded ad); coins also come from games, the daily
    // reward, missions, the season track and tournaments
    long adReward = 2_000;
    string AdLabel => "+" + Loc.Short(adReward);

    // the shop's skin tabs: boards and pieces each for dominoes, tawla and chess; then dice, hats, drinks
    static readonly string[] GameTabs = { "دومينو", "طاولة", "شطرنج" };
    static readonly string[] BoardCats = { "felt", "tawla", "chess" }, PieceCats = { "tiles", "checkers", "pieces" };
    int shopGame, invTab, invGame;
    public int ShopGame { set => shopGame = value; }
    static string CatOf(int tab, int game) => tab == 0 ? BoardCats[game] : tab == 1 ? PieceCats[game] : tab == 2 ? "dice" : tab == 3 ? "hat" : tab == 4 ? "drink" : "win";

    public void OpenShop(int tab)
    {
        shopTab = tab;
        Panel(Loc.T("المتجر"), out var body, new Vector2(700, 372));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "كوينز ببلاش", "ترابيزات", "قطع", "زهر", "طربوش", "مشروب", "احتفال", "الموسم المميز" }, shopTab, i => { shopTab = i; refreshOpen(); });
            // your coins, bottom corner; your collection, the other corner
            var have = Ui.Label(area, Loc.T("رصيدك") + ": " + ((long)(user?.Num("coins") ?? 0)).ToString("N0"), 13, Ui.GoldLight, StartAlign, Ui.Bold);
            have.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(640, 20), new Vector2(0, -6));
            var mine = Ui.Button(area, Loc.T("مجموعتي"), new Color(1, 1, 1, 0.08f), Ui.Gold, 12, () => { Sfx.Tap(); Close(); OpenCollection(shopTab >= 1 && shopTab <= 6 ? shopTab - 1 : 0); });
            ((RectTransform)mine.transform).Place(new Vector2(Loc.En ? 1 : 0, 0), new Vector2(Loc.En ? 1 : 0, 0), new Vector2(110, 24), new Vector2(Loc.En ? -60 : 60, -4));
            var row = Ui.Rect("Cards", area); row.anchorMin = Vector2.zero; row.anchorMax = Vector2.one; row.offsetMin = new Vector2(0, 22); row.offsetMax = Vector2.zero;
            if (shopTab == 0)
            {
                // free coins: watch an ad (coins can't be bought)
                int left = (int)(user?.Num("adsLeft") ?? 0);
                var c = ShopCard(row, 0, 1);
                c.sizeDelta = new Vector2(320, c.sizeDelta.y);
                var vi = Ui.Picture(c, "Icon", Icon("video")); vi.color = Ui.Gold; vi.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(50, 50), new Vector2(0, -14));
                Ui.Label(c, Loc.T("شوف إعلان وخد") + " " + Loc.Short(adReward) + " " + Loc.T("كوين"), 18, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 30), new Vector2(0, -70));
                Ui.Label(c, Loc.T("فاضلك النهارده") + ": " + left, 12, Ui.GoldLight, TextAnchor.MiddleCenter).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 20), new Vector2(0, -96));
                BuyButton(c, left > 0 ? Loc.T("شوف الإعلان") : Loc.T("خلصت إعلانات النهارده"), left > 0 ? Ui.Gold : new Color(1, 1, 1, 0.1f), left > 0 ? Ui.Ink : Ui.GoldLight, () => { if (left > 0) WatchAd(); else app.Toast(Loc.T("خلصت إعلانات النهارده، تعالى بكرة")); });
            }
            else if (shopTab <= 6)
            {
                int t = shopTab - 1;
                if (t <= 1) row = GameSwitch(row, shopGame, g => { shopGame = g; refreshOpen(); });
                string cat = CatOf(t, shopGame);
                var items = new List<Skins.Def>(Skins.Of(cat)).FindAll(Skins.OnSale);
                for (int i = 0; i < items.Count; i++) SkinCard(ShopCard(row, i, items.Count), items[i], false);
                var note = Ui.Label(area, Loc.T(Note(cat)), 11, new Color(1, 1, 1, 0.55f));
                note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(460, 16), new Vector2(0, 14));
            }
            else
            {
                var c = ShopCard(row, 0, 1);
                c.sizeDelta = new Vector2(300, c.sizeDelta.y);
                // the season pass: a skin at every tier, and no ads until the season ends (there's no ad-free plan on its own)
                var season = user?.Obj("season");
                bool prem = season != null && season.Bool("premium");
                if (!SeasonOn)
                {
                    Ui.Label(c, "الموسم مش متاح دلوقتي", 15, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
                    return;
                }
                c.sizeDelta = new Vector2(340, c.sizeDelta.y);
                var vi = Ui.Picture(c, "Icon", Icon("pass")); vi.color = Ui.Gold; vi.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(44, 44), new Vector2(0, -10));
                Ui.Label(c, "الموسم المميز", 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(320, 28), new Vector2(0, -58));
                Ui.Label(c, "سكين في كل مستوى (٣٠ سكين) + بدون إعلانات لآخر الموسم", 12, Color.white, TextAnchor.MiddleCenter).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(330, 36), new Vector2(0, -88));
                if (prem) BuyButton(c, Loc.T("المميز مفتوح"), new Color(1, 1, 1, 0.1f), Ui.GoldLight, () => { Sfx.Tap(); Close(); OpenSeason(); });
                else BuyButton(c, (int)(season?.Num("egp", 299) ?? 299) + " " + Loc.T("ج.م"), Ui.Gold, Ui.Ink, () => Billing.Buy("season_pass"));
            }
        };
        refreshOpen();
    }

    /// Your collection: every skin you have (the classic ones too), kind by kind, to wear with a tap.
    public void OpenCollection(int tab)
    {
        invTab = Mathf.Clamp(tab, 0, 6);
        Panel(Loc.T("مجموعتي"), out var body, new Vector2(700, 372));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "ترابيزات", "قطع", "زهر", "طربوش", "مشروب", "احتفال", "ألقاب" }, invTab, i => { invTab = i; refreshOpen(); });
            if (invTab == 6) { DrawMyTitles(area); return; }
            var row = Ui.Rect("Cards", area); row.anchorMin = Vector2.zero; row.anchorMax = Vector2.one; row.offsetMin = new Vector2(0, 22); row.offsetMax = Vector2.zero;
            if (invTab <= 1) row = GameSwitch(row, invGame, g => { invGame = g; refreshOpen(); });
            string cat = CatOf(invTab, invGame);
            var all = new List<Skins.Def>(Skins.Of(cat));
            var have = all.FindAll(d => Skins.Owns(d.id));
            for (int i = 0; i < have.Count; i++) SkinCard(ShopCard(row, i, have.Count), have[i], true);
            // how many of this kind you have, all kinds together, and the way to the shop for the rest
            int total = 0, mineAll = 0; foreach (var d in Skins.All) { total++; if (Skins.Owns(d.id)) mineAll++; }
            var count = Ui.Label(area, Loc.T("عندك") + " " + have.Count + " / " + all.Count + "   ·   " + Loc.T("كل مجموعتك") + " " + mineAll + " / " + total, 13, Ui.GoldLight, StartAlign, Ui.Bold);
            count.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(640, 20), new Vector2(0, -6));
            var shop = Ui.Button(area, Loc.T("المتجر"), Ui.Gold, Ui.Ink, 12, () => { Sfx.Tap(); Close(); shopGame = invGame; OpenShop(invTab <= 5 ? invTab + 1 : 0); });
            ((RectTransform)shop.transform).Place(new Vector2(Loc.En ? 1 : 0, 0), new Vector2(Loc.En ? 1 : 0, 0), new Vector2(110, 24), new Vector2(Loc.En ? -60 : 60, -4));
        };
        refreshOpen();
    }

    /// Your titles: wear one (it shows under your name for everyone), or none.
    void DrawMyTitles(RectTransform area)
    {
        var mine = user?.Arr("titles") ?? new List<object>();
        string worn = user?.Str("title");
        var how = Ui.Label(area, "", 11, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter);
        how.Set(Loc.T("الألقاب بتيجي من البطولات والإدارة، وأحسن لاعب في كل لعبة كل شهر بياخد لقب «لاعب الشهر»"));
        how.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(640, 18), new Vector2(0, -4));
        var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(0, 20); listArea.offsetMax = Vector2.zero;
        var have = new List<Dictionary<string, object>>();
        foreach (var o in mine) if (o is Dictionary<string, object> d && Titles.Get(d.Str("id")) != null) have.Add(d);
        if (have.Count == 0)
        {
            Ui.Label(listArea, "لسه معندكش ألقاب", 15, Ui.GoldLight).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 30), new Vector2(0, -40));
            // what the monthly titles look like: something to play for
            int k = 0;
            foreach (var t in Titles.List)
            {
                if (string.IsNullOrEmpty(t.monthly)) continue;
                var b = Titles.Badge(listArea, t.id, 12);
                b.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), b.sizeDelta, new Vector2(((k % 2) - 0.5f) * 250, -100 - (k / 2) * 40));
                k++;
            }
            return;
        }
        var list = Scroll(listArea, 4);
        foreach (var d in have)
        {
            string id = d.Str("id");
            bool on = id == worn;
            var row = Row(list, 44, on ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.18f) : Card);
            var b = Titles.Badge(row, id, 13);
            At(b, 12, b.sizeDelta.x, b.sizeDelta.y);
            double until = d.Num("until");
            if (until > 0)
            {
                var u = DateTimeOffset.FromUnixTimeMilliseconds((long)until).ToLocalTime();
                var ul = Ui.Label(row, "", 11, new Color(1, 1, 1, 0.55f), StartAlign);
                ul.Set(Loc.T("لحد") + " " + u.ToString("dd/MM/yyyy"));
                At(ul.rectTransform, 24 + b.sizeDelta.x, 140, 28);
            }
            var btn = Ui.Button(row, on ? "اقلعه" : "البسه", on ? new Color(1, 1, 1, 0.1f) : Ui.Gold, on ? Ui.GoldLight : Ui.Ink, 13, () =>
            {
                Sfx.Tap();
                app.Send(new Dictionary<string, object> { ["t"] = "wearTitle", ["id"] = on ? null : id });
            });
            ((RectTransform)btn.transform).Place(new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(Loc.En ? 1 : 0, 0.5f), new Vector2(110, 32), new Vector2(Loc.En ? -8 : 8, 0));
        }
    }

    /// Dominoes · tawla · chess, above the cards; returns the space left for them.
    RectTransform GameSwitch(RectTransform row, int sel, Action<int> pick)
    {
        for (int i = 0; i < GameTabs.Length; i++)
        {
            int k = Loc.En ? i : GameTabs.Length - 1 - i, idx = i;
            var b = Ui.Button(row, GameTabs[i], i == sel ? Ui.Gold : new Color(1, 1, 1, 0.06f), i == sel ? Ui.Ink : new Color(1, 1, 1, 0.7f), 12, () => { Sfx.Tap(); pick(idx); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(96, 24), new Vector2((k - 1) * 102, 0));
        }
        var rest = Ui.Rect("Below", row); rest.anchorMin = Vector2.zero; rest.anchorMax = Vector2.one; rest.offsetMin = Vector2.zero; rest.offsetMax = new Vector2(0, -26);
        return rest;
    }

    static string Note(string cat) => cat == "felt" ? "ترابيزة الدومينو بتبان ليك وانت بتلعب" : cat == "tawla" ? "الطاولة بتبان ليك وانت بتلعب" : cat == "chess" ? "رقعة الشطرنج بتبان ليك وانت بتلعب"
        : cat == "tiles" ? "قطع الدومينو اللي في إيدك وعلى الترابيزة" : cat == "checkers" ? "قشاط الطاولة بتاعك" : cat == "pieces" ? "عساكر الشطرنج بتاعتك"
        : cat == "dice" ? "الزهر في الطاولة واللودو" : cat == "win" ? "احتفالك بيظهر لكل اللي على الترابيزة لما تكسب" : cat == "hat" ? "الطربوش بيبان لكل اللي على الترابيزة (للرجالة)" : "المشروب بيبان قدامك لكل اللي على الترابيزة";

    /// One skin's card: its picture and name, and wear / worn / buy / from the season.
    void SkinCard(RectTransform c, Skins.Def s, bool collection)
    {
        float w = c.sizeDelta.x;
        var pic = Ui.Picture(c, "Look", Skins.Picture(s.id, out var tint)); pic.color = tint;
        var size = s.cat == "tiles" ? new Vector2(Mathf.Min(96, w - 12), Mathf.Min(48, (w - 12) / 2)) : new Vector2(Mathf.Min(64, w - 16), Mathf.Min(64, w - 16));
        pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), size, new Vector2(0, s.cat == "tiles" ? -26 : -12));
        var nm = Ui.Label(c, Loc.T(s.name), 13, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        nm.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w - 4, 36), new Vector2(0, -96));
        nm.horizontalOverflow = HorizontalWrapMode.Wrap;
        if (s.ev != null)
        {
            var tag = Ui.Label(c, Loc.T(s.ev == "ramadan" ? "عرض رمضان" : "عرض العيد"), 10, new Color(1f, 0.75f, 0.35f), TextAnchor.MiddleCenter, Ui.Bold);
            tag.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w - 8, 14), new Vector2(0, -2));
        }
        if (s.pass)
        {
            var tag = Ui.Label(c, Loc.T("الموسم"), 10, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
            tag.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(w - 8, 14), new Vector2(0, -2));
        }
        bool owned = Skins.Owns(s.id), wearing = Skins.Worn(s.cat) == s.id;
        if (wearing) BuyButton(c, Loc.T("متلبس"), new Color(1, 1, 1, 0.1f), Ui.GoldLight, null);
        else if (owned) BuyButton(c, Loc.T("البس"), new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, () => app.Send(new Dictionary<string, object> { ["t"] = "equipSkin", ["id"] = s.id }));
        else if (s.pass) BuyButton(c, Loc.T("من الموسم"), new Color(1, 1, 1, 0.08f), Ui.Gold, () => { Close(); OpenSeason(); });
        else BuyButton(c, Loc.Short(s.price), Ui.Gold, Ui.Ink, () =>
        {
            if ((user?.Num("coins") ?? 0) < s.price) { app.Toast(Loc.T("رصيدك مش كفاية")); return; }
            app.Send(new Dictionary<string, object> { ["t"] = "buySkin", ["id"] = s.id }); Sfx.Coins();
        }, true);
    }

    static RectTransform ShopCard(RectTransform row, int i, int n)
    {
        const float Gap = 10;
        float W = Mathf.Min(122, (660f - (n - 1) * Gap) / Mathf.Max(1, n));
        int k = Loc.En ? i : n - 1 - i;
        var c = Ui.Box(row, "Card", Card).rectTransform;
        c.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(W, 176), new Vector2((k - (n - 1) / 2f) * (W + Gap), 4));
        return c;
    }

    static void BuyButton(RectTransform card, string label, Color bg, Color fg, Action onClick, bool coin = false)
    {
        var b = Ui.Button(card, label, bg, fg, 14, () => { Sfx.Tap(); onClick?.Invoke(); });
        var r = ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(card.sizeDelta.x - 16, 32), new Vector2(0, 8));
        if (onClick == null) b.interactable = false;
        if (coin)
        {
            // the coin on the left, the price beside it (narrow cards: a smaller coin, the text kept clear of it)
            bool narrow = card.sizeDelta.x < 100;
            var ci = Ui.Picture(r, "Coin", Icon("coin")); ci.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(narrow ? 14 : 18, narrow ? 14 : 18), new Vector2(narrow ? 5 : 8, 0));
            var t = b.GetComponentInChildren<Text>();
            if (t != null) { t.rectTransform.offsetMin = new Vector2(narrow ? 18 : 22, t.rectTransform.offsetMin.y); if (narrow) t.fontSize = 13; }
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
    Dictionary<string, object> lbMonth;   // this month's race for the game's "player of the month" title

    public void OpenLeaders(int tab)
    {
        lbTab = tab; lbGame = GameIds[tab]; lbRows = null; lbMe = null; lbMonth = null;
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
            else if (lbMonth != null && lbMonth.Bool("on") && Titles.Get(lbMonth.Str("title")) != null)
            {
                // the race for this game's title this month: the title, who leads, where you are
                var race = Ui.Box(area, "Month", new Color(1, 1, 1, 0.05f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 30), Vector2.zero);
                var b = Titles.Badge(race, lbMonth.Str("title"), 10);
                b.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), b.sizeDelta, new Vector2(Loc.En ? 6 : -6, 0));
                string lead = lbMonth.Str("leader");
                int place = (int)lbMonth.Num("place");
                string line = Loc.T("لأعلى نقط الشهر") + (prizeOn ? " + " + prizeEgp + " " + Loc.T("جنيه") : "") + "  ·  " + (string.IsNullOrEmpty(lead) ? Loc.T("مفيش متصدر لسه") : Loc.T("المتصدر") + ": " + lead + " (" + (int)lbMonth.Num("leaderPoints") + ")")
                    + (place > 0 ? "  ·  " + Loc.T("مركزك") + " " + place + " (" + (int)lbMonth.Num("points") + ")" : "");
                var txt = Ui.Label(race, "", 11, Ui.GoldLight, StartAlign, Ui.Bold); txt.Set(line);
                txt.rectTransform.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(548 - b.sizeDelta.x - 18, 28), new Vector2(Loc.En ? b.sizeDelta.x + 14 : -(b.sizeDelta.x + 14), 0));
                txt.horizontalOverflow = HorizontalWrapMode.Wrap; txt.resizeTextForBestFit = true; txt.resizeTextMinSize = 8; txt.resizeTextMaxSize = 11;
                top = 34;
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
                var nameCell = LbCells(row, rank.ToString(), r.Str("name") + lvl, ((int)r.Num("rating")).ToString(), last, rank == 1 ? Ui.Gold : Color.white, 14);
                // their title, after the name
                var tb = Titles.Badge(row, r.Str("title"), 9);
                if (tb != null) { float nx = 66 + Mathf.Min(nameCell.preferredWidth, 280 - tb.sizeDelta.x - 6) + 6; At(tb, nx, tb.sizeDelta.x, tb.sizeDelta.y); }
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

    static Text LbCells(RectTransform row, string rank, string name, string rating, string wins, Color c, int size)
    {
        At(Ui.Label(row, rank, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 6, 50, 30);
        var n = Ui.Label(row, name, size, c, StartAlign, Ui.Bold);
        At(n.rectTransform, 66, 280, 30);
        At(Ui.Label(row, rating, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 360, 90, 30);
        At(Ui.Label(row, wins, size, c, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 460, 90, 30);
        return n;
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

    /// The "Sign in" chip: Google straight away, or a choice of Google and Apple when the server has both.
    void SignIn()
    {
        Sfx.Tap();
        if (!appleOn) { SignInWith("google"); return; }
        Panel(Loc.T("سجّل دخولك"), out var body, new Vector2(420, 250));
        refreshOpen = null;
        Ui.Label(body, "عشان كوينزك وتقدمك يتحفظوا وتلعب من أي موبايل", 13, Ui.GoldLight).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(390, 22), new Vector2(0, -2));
        var g = Ui.Button(body, "كمّل بجوجل", Color.white, Ui.Ink, 16, () => SignInWith("google"));
        var gr = ((RectTransform)g.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 46), new Vector2(0, -40));
        var gi = Ui.Picture(gr, "G", Icon("google")); gi.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 24), new Vector2(12, 0));
        var a = Ui.Button(body, "كمّل بأبل", Color.black, Color.white, 16, () => SignInWith("apple"));
        ((RectTransform)a.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 46), new Vector2(0, -98));
        a.gameObject.AddComponent<Outline>().effectColor = new Color(1, 1, 1, 0.35f);
    }

    /// The provider's page opens in the browser; the server sends the player back with a one-time ticket
    /// (the app link ahwagames://auth?ticket=…), handled in OnLink.
    void SignInWith(string provider)
    {
        Sfx.Tap();
        if (provider == "google" && !googleOn) { app.Toast(Loc.T("تسجيل الدخول بجوجل مش متاح على السيرفر ده")); return; }
        if (provider == "apple" && !appleOn) { app.Toast(Loc.T("تسجيل الدخول بأبل مش متاح على السيرفر ده")); return; }
        if (app.net == null) return;
        Application.OpenURL(app.net.HttpBase + "/auth/" + provider + "/start?to=app");
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
