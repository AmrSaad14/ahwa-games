// Reasons to come back: today's three missions, the season pass (30 tiers, free and premium), the weekly
// tournament (sign up, the bracket, your match), and the server messages for all of them.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    GameObject missionsDot, seasonDot, friendsDot, tourneyDot;

    /// The skin worn for a kind (its id), or null for the classic one.
    string Wearing(string cat) => user?.Obj("equip")?.Str(cat, null);

    /// Red dots: a mission or season tier to collect, a friend request, a tournament you're in about to start.
    void UpdateDots()
    {
        if (user == null) return;
        bool m = false;
        foreach (var o in user.Arr("missions") ?? new List<object>()) if (o is Dictionary<string, object> d && !d.Bool("claimed") && d.Num("have") >= d.Num("need")) m = true;
        // the weekly chest, ready to open, lights the missions too
        var chest = user.Obj("chest");
        if (chest != null) { var steps = chest.Arr("steps"); var op = Ints(chest.Arr("opened")); if (steps != null) for (int k = 0; k < steps.Count; k++) if (chest.Num("done") >= (double)steps[k] && !op.Contains(k)) m = true; }
        missionsDot.SetActive(m);
        seasonDot.SetActive(SeasonOn && SeasonClaimable() > 0);
        friendsDot.SetActive(incoming > 0);
        tourneyDot.SetActive(tourney != null && (tourney.Str("state") == "running" && tourney.Obj("you") != null && tourney.Obj("you").Bool("alive")));
    }

    /* ---------- messages ---------- */

    void OnMessageMore(Dictionary<string, object> m)
    {
        switch (m.Str("t"))
        {
            case "welcome":
                app.Send(new Dictionary<string, object> { ["t"] = "tourney" });
                app.Send(new Dictionary<string, object> { ["t"] = "friends" });
                break;
            case "friends":
                friendRows = m.Arr("rows") ?? new List<object>();
                incoming = 0;
                foreach (var o in friendRows) if (o is Dictionary<string, object> f && f.Str("status") == "incoming") incoming++;
                UpdateDots();
                refreshOpen?.Invoke();
                break;
            case "friendNews":
            {
                string k = m.Str("kind"), n = m.Str("name");
                app.Toast(k == "request" ? n + " " + Loc.T("عايز يبقى صاحبك") : k == "accepted" ? n + " " + Loc.T("بقى صاحبك") : k == "sent" ? Loc.T("طلب الصداقة اتبعت لـ") + " " + n : n + " " + Loc.T("بقى صاحبك"));
                if (k == "request" || k == "accepted") Sfx.Chime();
                break;
            }
            case "invited": ShowInvited(m); break;
            case "profileCard":
                card = m.Obj("card");
                if (refreshCard != null && refreshOpen == refreshCard) refreshOpen();
                break;
            case "missionDone":
                app.Toast(Loc.T("خلصت مهمة! خد جايزتك") + "  ·  " + MissionText(FindMission(m.Str("id"))));
                Sfx.Chime();
                break;
            case "tutorialReward":
                Sfx.Coins();
                ShowCard(Loc.T("أحسنت!"), Loc.T("خلصت أول دور. هدية") + ": +" + Loc.Short((long)m.Num("coins")) + " " + Loc.T("كوين") + "\n" + Loc.T("جرّب تلعب أونلاين ضد ناس حقيقية"), null, false);
                break;
            case "chestOpened":
                Sfx.Coins(); Sfx.Chime();
                app.Toast(Loc.T("فتحت الصندوق!") + "  +" + Loc.Short((long)m.Num("coins")) + "  ·  +" + (int)m.Num("xp") + " XP");
                break;
            case "comeback":
                // back after a while: a warm welcome and a gift
                Sfx.Coins();
                ShowCard(Loc.T("وحشتنا!"), Loc.T("هدية رجوعك للقهوة") + ": +" + Loc.Short((long)m.Num("coins")) + " " + Loc.T("كوين"), null, false);
                break;
            case "missionClaimed":
                Sfx.Coins();
                app.Toast("+" + Loc.Short((long)m.Num("coins")) + "  ·  +" + (int)m.Num("xp") + " XP");
                break;
            case "seasonTier":
                app.Toast(Loc.T("وصلت مستوى") + " " + (int)m.Num("tier") + " " + Loc.T("في الموسم!"));
                Sfx.Chime();
                break;
            case "seasonClaimed":
                Sfx.Coins();
                app.Toast(m.Str("skin", null) != null ? Loc.T("جالك") + " " + SkinName(m.Str("skin")) : "+" + Loc.Short((long)m.Num("coins")));
                break;
            case "tourney":
                tourney = m.Obj("view");
                UpdateDots();
                if (refreshTourney != null && refreshOpen == refreshTourney) refreshOpen();
                break;
            case "tourneyMatch":
                tourneyTable = m.Str("table"); tourneyRound = (int)m.Num("round");
                if (app.view != null && app.view.id == tourneyTable) break;   // already there
                Sfx.Chime(); Sfx.Buzz();
                if (app.view == null || app.Practicing) { if (app.Practicing) app.StopPractice(); app.Send(new Dictionary<string, object> { ["t"] = "tourneyGo" }); }
                else app.Toast(Loc.T("ماتشك في البطولة جاهز! افتح البطولة وادخل"));
                break;
            case "rematch":
                app.game.OnRematch(m);
                break;
            case "referral":
                Sfx.Coins(); Sfx.Buzz();
                app.Toast(m.Str("you") == "inviter" ? m.Str("name") + " " + Loc.T("لعب أول ماتش، خدتوا انتو الاتنين") + " " + Loc.Short((long)m.Num("coins"))
                    : Loc.T("هدية دعوة صاحبك") + " +" + Loc.Short((long)m.Num("coins")));
                break;
        }
    }

    /* ---------- missions ---------- */

    static readonly Dictionary<string, (string ar, string en)> GameWords = new Dictionary<string, (string, string)>
    {
        ["domino"] = ("دومينو", "dominoes"), ["tawla"] = ("طاولة", "backgammon"), ["chess"] = ("شطرنج", "chess"), ["ludo"] = ("لودو", "ludo"), ["any"] = ("أي لعبة", "any game"),
    };
    static readonly string[] ArDigits = { "٠", "١", "٢", "٣", "٤", "٥", "٦", "٧", "٨", "٩" };
    static string ArNum(int n) { var s = n.ToString(); var o = ""; foreach (var c in s) o += char.IsDigit(c) ? ArDigits[c - '0'] : c.ToString(); return o; }

    Dictionary<string, object> FindMission(string id)
    {
        foreach (var o in user?.Arr("missions") ?? new List<object>()) if (o is Dictionary<string, object> d && d.Str("id") == id) return d;
        return null;
    }

    /// "Win 2 games of dominoes", "العب ٣ أدوار أي لعبة أونلاين".
    static string MissionText(Dictionary<string, object> d)
    {
        if (d == null) return "";
        string kind = d.Str("kind"), game = d.Str("game");
        int need = (int)d.Num("need");
        bool online = d.Bool("online");
        (string ar, string en) w = GameWords.TryGetValue(game, out var x) ? x : (game, game);
        if (kind == "gift") return Loc.En ? "Send a gift to someone at your table" : "ابعت هدية لحد على الترابيزة";
        if (kind == "friend") return Loc.En ? "Play a game" + (game == "any" ? "" : " of " + w.en) + " with a friend" : "العب دور " + (game == "any" ? "" : w.ar + " ") + "مع صاحبك";
        if (kind == "bigwin") return Loc.En ? "Win a game with a 50K+ stake" : "اكسب دور على رهان ٥٠ ألف أو أكتر";
        if (Loc.En)
            return (kind == "win" ? "Win " : "Play ") + need + (need == 1 ? " game" : " games") + (game == "any" ? "" : " of " + w.en) + (online ? " online" : "");
        string n = ArNum(need);
        string games = need == 1 ? "دور" : need == 2 ? "دورين" : n + " أدوار";
        if (need == 2) return (kind == "win" ? "اكسب " : "العب ") + games + " " + w.ar + (online ? " أونلاين" : "");
        return (kind == "win" ? "اكسب " : "العب ") + (need == 1 ? "دور" : games) + " " + w.ar + (online ? " أونلاين" : "");
    }

    public void OpenMissions()
    {
        Panel(Loc.T("مهام النهارده"), out var body, new Vector2(600, 392));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var ms = user?.Arr("missions");
            if (ms == null || ms.Count == 0) { Ui.Label(body, Loc.T(app.net != null && app.net.Online ? "جاري التحميل…" : "مش متصل بالسيرفر لسه"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
            for (int i = 0; i < ms.Count; i++)
            {
                if (!(ms[i] is Dictionary<string, object> d)) continue;
                int have = (int)d.Num("have"), need = Mathf.Max(1, (int)d.Num("need"));
                bool done = have >= need, got = d.Bool("claimed");
                var row = Ui.Box(body, "Mission", got ? new Color(0.18f, 0.42f, 0.23f, 0.5f) : Card).rectTransform;
                row.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 70), new Vector2(0, -i * 78));
                var icon = Ui.Picture(row, "Icon", Icon("target")); icon.color = done ? Ui.Gold : new Color(1, 1, 1, 0.5f);
                At(icon.rectTransform, 12, 34, 34);
                At(Ui.Label(row, MissionText(d), 16, Color.white, StartAlign, Ui.Bold).rectTransform, 56, 300, 24).anchoredPosition += new Vector2(0, 14);
                // progress
                var track = Ui.Box(row, "Track", new Color(1, 1, 1, 0.1f)).rectTransform; At(track, 56, 220, 10).anchoredPosition += new Vector2(0, -12);
                var fill = Ui.Box(track, "Fill", done ? Ui.Gold : new Color(0.55f, 0.8f, 0.5f)).rectTransform;
                fill.anchorMin = new Vector2(Loc.En ? 0 : 1 - Mathf.Clamp01(have / (float)need), 0); fill.anchorMax = new Vector2(Loc.En ? Mathf.Clamp01(have / (float)need) : 1, 1);
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                At(Ui.Label(row, Mathf.Min(have, need) + " / " + need, 12, Ui.GoldLight, StartAlign).rectTransform, 284, 60, 18).anchoredPosition += new Vector2(0, -12);
                // the reward, and collecting it
                At(Ui.Label(row, "+" + Loc.Short((long)d.Num("coins")) + "  ·  +" + (int)d.Num("xp") + " XP", 13, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 340, 100, 20).anchoredPosition += new Vector2(0, 14);
                string id = d.Str("id");
                var b = Ui.Button(row, got ? "اتاخدت" : done ? "خد الجايزة" : "لسه", done && !got ? Ui.Gold : new Color(1, 1, 1, 0.08f), done && !got ? Ui.Ink : new Color(1, 1, 1, 0.55f), 14,
                    () => { if (!done || got) return; Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "mission", ["id"] = id }); });
                At((RectTransform)b.transform, 446, 104, 34).anchoredPosition += new Vector2(0, -8);
                if (done && !got) StartCoroutine(Pulse((RectTransform)b.transform));
            }
            // the weekly chest: missions collected this week open it in three steps
            var chest = user.Obj("chest");
            if (chest != null)
            {
                int doneW = (int)chest.Num("done");
                var steps = chest.Arr("steps") ?? new List<object>(); var coinsL = chest.Arr("coins") ?? new List<object>();
                var opened = Ints(chest.Arr("opened"));
                var bar = Ui.Box(body, "Chest", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.1f)).rectTransform;
                bar.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 62), new Vector2(0, -238));
                var ci = Ui.Picture(bar, "Icon", Icon("chest")); ci.color = Ui.Gold; At(ci.rectTransform, 10, 36, 36);
                var cl = Ui.Label(bar, "", 13, Ui.Gold, StartAlign, Ui.Bold);
                cl.Set(Loc.T("صندوق الأسبوع") + "\n" + doneW + " " + Loc.T("مهام خلصت"));
                cl.horizontalOverflow = HorizontalWrapMode.Wrap;
                At(cl.rectTransform, 52, 150, 52);
                for (int k = 0; k < steps.Count; k++)
                {
                    int need = (int)(double)steps[k], step = k;
                    long c = k < coinsL.Count ? (long)(double)coinsL[k] : 0;
                    bool can = doneW >= need, got = opened.Contains(k);
                    var b = Ui.Button(bar, got ? "✓" : can ? Loc.T("افتح") + " +" + Loc.Short(c) : need + " " + Loc.T("مهام") + " · " + Loc.Short(c),
                        got ? new Color(0.18f, 0.42f, 0.23f, 0.8f) : can ? Ui.Gold : new Color(1, 1, 1, 0.08f), got ? Color.white : can ? Ui.Ink : new Color(1, 1, 1, 0.55f), 12,
                        () => { if (!can || got) return; Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "chestOpen", ["step"] = (double)step }); });
                    At((RectTransform)b.transform, 214 + k * 112, 104, 40);
                    if (can && !got) StartCoroutine(Pulse((RectTransform)b.transform));
                }
            }
            double resets = user.Num("missionsAt");
            long left = Math.Max(0, (long)(resets - App.LocalMs())) / 1000;
            Ui.Label(body, Loc.T("مهام جديدة بعد") + " " + (left / 3600) + ":" + (left / 60 % 60).ToString("00") + "  ·  " + Loc.T("الماتشات ضد الكمبيوتر بتتحسب كمان"), 12, new Color(1, 1, 1, 0.6f)).rectTransform
                .Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 18), new Vector2(0, 0));
        };
        refreshOpen();
    }

    /* ---------- the season pass ---------- */

    // the track comes from the server (the dashboard can change it); a tier's reward on one track
    (long coins, string skin) TierReward(int n, bool prem)
    {
        var tiers = user?.Obj("season")?.Arr("tiers");
        if (tiers == null || n < 1 || n > tiers.Count || !(tiers[n - 1] is Dictionary<string, object> t)) return (0, null);
        var r = t.Obj(prem ? "prem" : "free");
        if (r == null) return (0, null);
        string skin = r.Str("skin");
        return string.IsNullOrEmpty(skin) ? ((long)r.Num("coins"), null) : (0, skin);
    }

    int SeasonClaimable()
    {
        var s = user?.Obj("season"); if (s == null) return 0;
        int tier = (int)s.Num("tier"), n = 0;
        var free = Ints(s.Arr("free")); var prem = Ints(s.Arr("prem"));
        for (int i = 1; i <= tier; i++) { if (!free.Contains(i)) n++; if (s.Bool("premium") && !prem.Contains(i)) n++; }
        return n;
    }
    static List<int> Ints(List<object> l) { var o = new List<int>(); if (l != null) foreach (var x in l) if (x is double d) o.Add((int)d); return o; }

    public void OpenSeason(int scrollTo = -1)
    {
        if (!SeasonOn) { app.Toast(Loc.T("الموسم مش متاح دلوقتي")); return; }
        Panel(Loc.T("الموسم"), out var body, new Vector2(720, 350));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var s = user?.Obj("season");
            if (s == null) { Ui.Label(body, Loc.T(app.net != null && app.net.Online ? "جاري التحميل…" : "مش متصل بالسيرفر لسه"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
            int sp = (int)s.Num("sp"), tier = (int)s.Num("tier");
            bool premium = s.Bool("premium");
            var free = Ints(s.Arr("free")); var prem = Ints(s.Arr("prem"));
            // the season's name and time left, your tier and the points to the next
            long left = Math.Max(0, (long)(s.Num("endsAt") - App.LocalMs())) / 1000;
            string month = s.Str("id");
            Ui.Label(body, Loc.T("الموسم") + " " + month + "  ·  " + Loc.T("فاضل") + " " + (left / 86400) + Loc.T("ي") + " " + (left / 3600 % 24) + Loc.T("س"), 13, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(680, 18), new Vector2(0, 2));
            var head = Ui.Rect("Head", body).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(680, 50), new Vector2(0, -20));
            At(Ui.Label(head, Loc.T("المستوى") + " " + tier + " / 30", 20, Ui.Gold, StartAlign, Ui.Display).rectTransform, 4, 170, 30);
            var bar = Ui.Box(head, "Bar", new Color(1, 1, 1, 0.1f)).rectTransform; At(bar, 180, 210, 12);
            float frac = tier >= 30 ? 1 : (sp % 100) / 100f;
            var fill = Ui.Box(bar, "Fill", Ui.Gold).rectTransform;
            fill.anchorMin = new Vector2(Loc.En ? 0 : 1 - frac, 0); fill.anchorMax = new Vector2(Loc.En ? frac : 1, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            At(Ui.Label(head, tier >= 30 ? Loc.T("خلصت الموسم!") : (sp % 100) + " / 100 SP", 12, Ui.GoldLight, StartAlign).rectTransform, 396, 86, 20);
            if (premium)
            {
                var pb = Ui.Box(head, "Premium", Ui.Gold).rectTransform; At(pb, 540, 140, 30);
                Ui.Label(pb, "المميز مفتوح", 14, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
            }
            else
            {
                // the premium track: 299 EGP on Google Play, or with coins
                var buy = Ui.Button(head, Loc.T("افتح المميز") + "  " + (int)s.Num("egp", 299) + " " + Loc.T("ج.م"), Ui.Gold, Ui.Ink, 13, () => { Sfx.Tap(); Billing.Buy("season_pass"); });
                At((RectTransform)buy.transform, 486, 120, 34);
                var withCoins = Ui.Button(head, Loc.T("أو") + " " + Loc.Short((long)s.Num("price", 750_000)), new Color(1, 1, 1, 0.08f), Ui.Gold, 12, () =>
                {
                    Sfx.Tap();
                    if (user.Num("coins") < s.Num("price", 750_000)) { app.Toast(Loc.T("رصيدك مش كفاية")); return; }
                    app.Send(new Dictionary<string, object> { ["t"] = "seasonBuy" });
                });
                At((RectTransform)withCoins.transform, 612, 70, 34);
            }
            // the track: tiers side by side, free above, premium below
            var area = Ui.Rect("Track", body); area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(0, 18); area.offsetMax = new Vector2(0, -76);
            var labels = Ui.Rect("Rows", area).Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(56, 230), Vector2.zero);
            Ui.Label(labels, "مجاني", 12, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(56, 20), new Vector2(0, -60));
            Ui.Label(labels, "مميز", 12, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(56, 20), new Vector2(0, -158));
            var view = Ui.Box(area, "View", new Color(0, 0, 0, 0.001f), false).rectTransform;
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one; view.offsetMin = new Vector2(Loc.En ? 60 : 0, 0); view.offsetMax = new Vector2(Loc.En ? 0 : -60, 0);
            view.gameObject.AddComponent<RectMask2D>();
            const float CW = 86;
            var content = Ui.Rect("Content", view);
            content.anchorMin = new Vector2(Loc.En ? 0 : 1, 0); content.anchorMax = new Vector2(Loc.En ? 0 : 1, 1); content.pivot = new Vector2(Loc.En ? 0 : 1, 0.5f);
            content.sizeDelta = new Vector2(30 * CW, 0); content.anchoredPosition = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.viewport = view; sr.content = content; sr.vertical = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30;
            for (int n = 1; n <= 30; n++)
            {
                int k = n;
                float x = (n - 1) * CW * (Loc.En ? 1 : -1);
                var col = Ui.Rect("Tier", content).Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(CW - 6, 236), new Vector2(x, 0));
                bool reached = n <= tier;
                var num = Ui.Box(col, "N", reached ? Ui.Gold : new Color(1, 1, 1, 0.1f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(34, 22), Vector2.zero);
                Ui.Label(num, n.ToString(), 13, reached ? Ui.Ink : Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
                RewardCell(col, -28, n, false, reached, free.Contains(n), true);
                RewardCell(col, -128, n, true, reached, prem.Contains(n), premium);
            }
            // start at the first tier still to collect (or where you are)
            int focus = scrollTo > 0 ? scrollTo : Mathf.Max(1, Mathf.Min(tier, FirstUnclaimed(tier, free, prem, premium)));
            float want = Mathf.Max(0, (focus - 2) * CW);
            content.anchoredPosition = new Vector2(Loc.En ? -want : want, 0);
            Ui.Label(body, Loc.T("كل ماتش ومهمة ومكافأة يومية بتديك نقط للموسم"), 11, new Color(1, 1, 1, 0.55f)).rectTransform
                .Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(660, 16), new Vector2(0, 0));
        };
        refreshOpen();
    }

    static int FirstUnclaimed(int tier, List<int> free, List<int> prem, bool premium)
    {
        for (int i = 1; i <= tier; i++) if (!free.Contains(i) || (premium && !prem.Contains(i))) return i;
        return tier;
    }

    void RewardCell(RectTransform col, float y, int n, bool premTrack, bool reached, bool got, bool open)
    {
        var (coins, skin) = TierReward(n, premTrack);
        var cell = Ui.Box(col, "Reward", got ? new Color(0.18f, 0.42f, 0.23f, 0.75f) : premTrack ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.14f) : Card).rectTransform;
        cell.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(80, 94), new Vector2(0, y));
        if (skin != null)
        {
            var pic = Ui.Picture(cell, "Skin", SkinPicture(skin, out var tint)); pic.color = tint;
            pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, 40), new Vector2(0, -6));
            var nm = Ui.Label(cell, SkinName(skin), 10, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
            nm.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(78, 16), new Vector2(0, -48));
        }
        else
        {
            var ci = Ui.Picture(cell, "Coin", Icon("coin")); ci.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(32, 32), new Vector2(0, -8));
            Ui.Label(cell, Loc.Short(coins), 14, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(78, 18), new Vector2(0, -44));
        }
        string label = got ? "اتاخدت" : !open ? "مقفول" : reached ? "خد" : "";
        if (label == "") return;
        bool can = reached && open && !got;
        var b = Ui.Button(cell, label, can ? Ui.Gold : new Color(1, 1, 1, 0.08f), can ? Ui.Ink : new Color(1, 1, 1, 0.6f), 12, () =>
        {
            if (!can) { if (!open) app.Toast(Loc.T("افتح المميز الأول")); return; }
            Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "seasonClaim", ["tier"] = (double)n, ["track"] = premTrack ? "prem" : "free" });
        });
        ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(70, 24), new Vector2(0, 4));
    }

    /* ---------- skins of the new kinds: names and pictures (the shop and the season use them) ---------- */

    static string SkinName(string id) => Skins.Name(id);
    /// A small picture for a skin, and the colour to show it in.
    static Texture SkinPicture(string id, out Color tint) => Skins.Picture(id, out tint);

    /* ---------- the weekly tournament ---------- */

    Dictionary<string, object> tourney;
    public string tourneyTable; int tourneyRound;
    Action refreshTourney;
    public bool IsTourneyTable(string id) => !string.IsNullOrEmpty(id) && id == tourneyTable;

    static string ModeName(string mode) => mode == "ffa2" ? Loc.T("دومينو فردي") : mode == "tawla" ? Loc.T("طاولة") : mode == "chess3" ? Loc.T("شطرنج ٣ دقايق") : mode == "ludoq2" ? Loc.T("لودو سريع") : mode;

    public void OpenTourney()
    {
        if (!TourneyOn) { app.Toast(Loc.T("البطولات مش متاحة دلوقتي")); return; }
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        app.Send(new Dictionary<string, object> { ["t"] = "tourney" });
        Panel(Loc.T("بطولة الأسبوع"), out var body, new Vector2(720, 350));
        refreshTourney = refreshOpen = () => DrawTourney(body);
        refreshOpen();
    }

    void DrawTourney(RectTransform body)
    {
        foreach (Transform c in body) Destroy(c.gameObject);
        var t = tourney;
        if (t == null) { Ui.Label(body, Loc.T("جاري التحميل…"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
        string state = t.Str("state");
        var when = DateTimeOffset.FromUnixTimeMilliseconds((long)t.Num("startsAt")).ToLocalTime();
        // the line at the top: the game, when, how many
        string[] daysAr = { "الحد", "الاتنين", "التلات", "الأربع", "الخميس", "الجمعة", "السبت" }, daysEn = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        int hr = when.Hour % 12 == 0 ? 12 : when.Hour % 12;
        string whenText = (Loc.En ? daysEn : daysAr)[(int)when.DayOfWeek] + " " + hr + ":" + when.Minute.ToString("00") + " " + (when.Hour < 12 ? (Loc.En ? "AM" : "ص") : (Loc.En ? "PM" : "م"));
        Ui.Label(body, ModeName(t.Str("mode")) + "  ·  " + whenText + "  ·  " + (int)t.Num("count") + " " + Loc.T("لاعب"), 15, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(680, 22), Vector2.zero);
        var area = Ui.Rect("Area", body); area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one; area.offsetMin = new Vector2(0, 50); area.offsetMax = new Vector2(0, -28);
        var bottom = Ui.Rect("Bottom", body).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(680, 44), Vector2.zero);
        if (state == "open")
        {
            // what's to win, and signing up
            var prizes = Ints(t.Arr("prizes"));
            string[] places = { "الأول", "التاني", "التالت والرابع", "من ٥ لـ ٨" };
            long[] amounts = { prizes.Count > 0 ? prizes[0] : 0, prizes.Count > 1 ? prizes[1] : 0, prizes.Count > 2 ? prizes[2] : 0, prizes.Count > 4 ? prizes[4] : 0 };
            for (int i = 0; i < 4; i++)
            {
                int k = Loc.En ? i : 3 - i;
                var c = Ui.Box(area, "Prize", i == 0 ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f) : Card).rectTransform;
                c.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, 120), new Vector2((k - 1.5f) * 160, -4));
                var ic = Ui.Picture(c, "Cup", Icon(i == 0 ? "cup" : "trophy")); ic.color = i == 0 ? Ui.Ink : Ui.Gold;
                ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(38, 38), new Vector2(0, -10));
                Ui.Label(c, places[i], 13, i == 0 ? Ui.Ink : Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(146, 20), new Vector2(0, -54));
                Ui.Label(c, Loc.Short(amounts[i]), 22, i == 0 ? Ui.Ink : Ui.Gold, TextAnchor.MiddleCenter, Ui.Display).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(146, 30), new Vector2(0, -78));
            }
            long left = Math.Max(0, (long)(t.Num("startsAt") - App.LocalMs())) / 1000;
            Ui.Label(area, Loc.T("خروج المغلوب، ماتش واحد كل دور، وكل اللي يلعب ياخد") + " " + Loc.Short(10_000) + "  ·  " + Loc.T("تبدأ بعد") + " " + (left / 86400) + Loc.T("ي") + " " + (left / 3600 % 24) + Loc.T("س") + " " + (left / 60 % 60) + Loc.T("د"), 12, new Color(1, 1, 1, 0.7f))
                .rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(680, 20), new Vector2(0, 0));
            bool reg = t.Bool("registered");
            var b = Ui.Button(bottom, reg ? "إنت مشترك  ·  انسحب" : "اشترك ببلاش", reg ? new Color(1, 1, 1, 0.1f) : Ui.Gold, reg ? Ui.GoldLight : Ui.Ink, 16, () =>
            {
                Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = reg ? "tourneyLeave" : "tourneyJoin" });
            });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240, 40), Vector2.zero);
            if (!reg) StartCoroutine(Pulse((RectTransform)b.transform));
            return;
        }
        // running or done: the bracket, round by round
        var rounds = t.Arr("rounds") ?? new List<object>();
        int R = rounds.Count;
        float colW = Mathf.Min(170, 680f / Mathf.Max(1, R));
        for (int r = 0; r < R; r++)
        {
            var ms = rounds[r] as List<object>; if (ms == null) continue;
            int k = Loc.En ? r : R - 1 - r;
            float x = (k - (R - 1) / 2f) * colW;
            string title = r == R - 1 ? Loc.T("النهائي") : r == R - 2 ? Loc.T("نص النهائي") : r == R - 3 ? Loc.T("ربع النهائي") : Loc.T("دور") + " " + (r + 1);
            Ui.Label(area, title, 11, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(colW, 16), new Vector2(x, 2));
            float h = (area.rect.height > 0 ? area.rect.height : 240) - 20;
            float slot = h / Mathf.Max(1, ms.Count);
            for (int i = 0; i < ms.Count; i++)
            {
                var m = ms[i] as Dictionary<string, object>; if (m == null) continue;
                float y = -18 - slot * (i + 0.5f);
                bool live = m.Bool("live");
                double w = m.TryGetValue("winner", out var wv) && wv is double wd ? wd : -1;
                var box = Ui.Box(area, "Match", live ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.25f) : Card).rectTransform;
                float bh = Mathf.Min(40, slot - 4);
                box.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(colW - 8, bh), new Vector2(x, y));
                EntrantLine(box, m.Obj("a"), w == 0, w == 1, true, bh);
                EntrantLine(box, m.Obj("b"), w == 1, w == 0, false, bh);
            }
        }
        var you = t.Obj("you");
        if (state == "running" && you != null && you.Bool("alive"))
        {
            bool atIt = app.view != null && app.view.id == tourneyTable;
            var go = Ui.Button(bottom, atIt ? "إنت في ماتشك" : "ادخل ماتشك", Ui.Gold, Ui.Ink, 16, () => { Sfx.Tap(); Close(); app.Send(new Dictionary<string, object> { ["t"] = "tourneyGo" }); });
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220, 40), Vector2.zero);
        }
        else
        {
            string line = state == "done" ? "" : you != null ? Loc.T("خرجت من البطولة، حظ أحسن الأسبوع الجاي") : Loc.T("البطولة شغالة دلوقتي");
            if (state == "done")
            {
                var res = t.Arr("results") ?? new List<object>();
                var parts = new List<string>();
                foreach (var o in res) if (o is Dictionary<string, object> x && x.Num("place") <= 2) parts.Add((x.Num("place") == 1 ? Loc.T("البطل") : Loc.T("التاني")) + ": " + x.Str("name") + (x.Bool("me") ? " (" + Loc.T("إنت") + ")" : ""));
                line = string.Join("   ·   ", parts);
            }
            Ui.Label(bottom, line, 14, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Fill();
        }
    }

    void EntrantLine(RectTransform box, Dictionary<string, object> e, bool won, bool lost, bool top, float bh)
    {
        string name = e == null ? "…" : e.Str("name");
        bool me = e != null && e.Bool("me");
        var l = Ui.Label(box, name, 11, won ? Ui.Gold : lost ? new Color(1, 1, 1, 0.4f) : me ? Color.white : new Color(1, 1, 1, 0.85f), StartAlign, won || me ? Ui.Bold : Ui.Body);
        l.rectTransform.anchorMin = new Vector2(0, top ? 0.5f : 0); l.rectTransform.anchorMax = new Vector2(1, top ? 1 : 0.5f);
        l.rectTransform.offsetMin = new Vector2(6, 0); l.rectTransform.offsetMax = new Vector2(-6, 0);
        l.horizontalOverflow = HorizontalWrapMode.Wrap; l.resizeTextForBestFit = true; l.resizeTextMinSize = 7; l.resizeTextMaxSize = 11;
    }
}
