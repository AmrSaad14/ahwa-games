// The leaderboard: one ranking for the whole game (every game together, online: 3 points a win, 1 for playing),
// all time or this month, over everyone, your country or your governorate; and the monthly titles: on the first
// day of each month the top of last month's ranking wears a title for 30 days. Every text here stays on one line.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    static readonly string[] RankScopes = { "all", "country", "gov" };
    static readonly long[] MonthlyEarnerPrizes = { 3_000_000, 2_000_000, 1_000_000 };   // the month's top three
    string lbPeriod = "all", lbWhere;
    Dictionary<string, object> titleData;
    System.Action pickerBack;   // where the country and governorate pickers go back to

    static readonly string[,] Countries =
    {
        { "EG", "مصر", "Egypt" }, { "SA", "السعودية", "Saudi Arabia" }, { "AE", "الإمارات", "UAE" }, { "KW", "الكويت", "Kuwait" },
        { "QA", "قطر", "Qatar" }, { "BH", "البحرين", "Bahrain" }, { "OM", "عمان", "Oman" }, { "JO", "الأردن", "Jordan" },
        { "LB", "لبنان", "Lebanon" }, { "SY", "سوريا", "Syria" }, { "IQ", "العراق", "Iraq" }, { "PS", "فلسطين", "Palestine" },
        { "LY", "ليبيا", "Libya" }, { "TN", "تونس", "Tunisia" }, { "DZ", "الجزائر", "Algeria" }, { "MA", "المغرب", "Morocco" },
        { "SD", "السودان", "Sudan" }, { "YE", "اليمن", "Yemen" },
    };
    static string CountryName(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Countries.GetLength(0); i++) if (Countries[i, 0] == id) return Loc.En ? Countries[i, 2] : Countries[i, 1];
        return id;
    }

    /// The phone's country (two letters), sent once with hello so the country ranking works from the first game.
    static bool countryRead; static string deviceCountry;
    public static string DeviceCountry()
    {
        if (!countryRead) { countryRead = true; deviceCountry = ReadCountry(); }
        return deviceCountry;
    }
    static string ReadCountry()
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var loc = new AndroidJavaClass("java.util.Locale"))
            using (var def = loc.CallStatic<AndroidJavaObject>("getDefault"))
            {
                var c = def.Call<string>("getCountry");
                if (!string.IsNullOrEmpty(c) && c.Length == 2) return c.ToUpperInvariant();
            }
#endif
            var r = System.Globalization.RegionInfo.CurrentRegion;
            if (r != null && r.TwoLetterISORegionName.Length == 2 && r.TwoLetterISORegionName != "IV") return r.TwoLetterISORegionName.ToUpperInvariant();
        }
        catch { }
        return null;
    }

    public void OpenLeaders(int tab)
    {
        lbTab = Mathf.Clamp(tab, 0, 3); lbRows = null; lbMe = null;
        lbGame = lbTab < 3 ? RankScopes[lbTab] : "titles";
        AskRank();
        Panel(Loc.T("المتصدرين"), out var body, new Vector2(600, 340));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "العام", "الدولة", "المحافظة", "الألقاب" }, lbTab, i => { Close(); OpenLeaders(i); });
            if (lbGame == "titles") DrawTitles(area); else DrawRanking(area);
        };
        refreshOpen();
    }

    void AskRank(bool again = true)
    {
        if (lbGame == "titles") { titleData = null; app.Send(new Dictionary<string, object> { ["t"] = "titleBoard" }); }
        else app.Send(new Dictionary<string, object> { ["t"] = "rank", ["period"] = lbPeriod, ["scope"] = lbGame });
        if (again) StartCoroutine(AskRankAgain(lbGame, lbPeriod));
    }

    /// No answer yet after a moment (a dropped message, a sleepy server)? Ask once more.
    System.Collections.IEnumerator AskRankAgain(string scope, string period)
    {
        yield return new WaitForSeconds(1.5f);
        if (open == null || lbGame != scope || lbPeriod != period) yield break;
        if (scope == "titles" ? titleData == null : lbRows == null) { Debug.Log("[menus] asking for the leaderboard again"); AskRank(false); }
    }

    /// The server's answer: the ranking, or the titles.
    void OnRank(Dictionary<string, object> m)
    {
        if (m.Str("scope") != lbGame || m.Str("period") != lbPeriod) return;   // an answer for another tab, overtaken
        lbRows = m.Arr("rows") ?? new List<object>(); lbMe = m.Obj("me"); lbWhere = m.Str("where");
        Debug.Log($"[menus] ranking {lbGame} {lbPeriod}: {lbRows.Count} rows");
        refreshOpen?.Invoke();
    }
    void OnTitleBoard(Dictionary<string, object> m)
    {
        titleData = m;
        if (lbGame == "titles") refreshOpen?.Invoke();
    }

    /// One line of text from the reading side, never wrapping.
    Text Line(RectTransform parent, string text, int size, Color c, float fromStart, float width, float height = 26, TextAnchor? align = null, Font font = null)
    {
        var t = Ui.Label(parent, "", size, c, align ?? StartAlign, font ?? Ui.Bold);
        t.Set(text);
        FitText.OneLine(t);
        At(t.rectTransform, fromStart, width, height);
        return t;
    }

    void DrawRanking(RectTransform area)
    {
        string scope = lbGame;
        // the bar: all time / this month, and what the board is about
        for (int k = 0; k < 2; k++)
        {
            string per = k == 0 ? "all" : "month";
            var b = Ui.Button(area, k == 0 ? "كل الوقت" : "الشهر ده", lbPeriod == per ? Ui.Gold : Ui.Panel, lbPeriod == per ? Ui.Ink : Ui.GoldLight, 13, () =>
            {
                if (lbPeriod == per) return;
                Sfx.Tap(); lbPeriod = per; lbRows = null; lbMe = null;
                AskRank(); refreshOpen?.Invoke();
            });
            FitText.OneLine(b.GetComponentInChildren<Text>());
            ((RectTransform)b.transform).Place(new Vector2(Loc.En ? 1 : 0, 1), new Vector2(Loc.En ? 1 : 0, 1), new Vector2(96, 26), new Vector2((Loc.En ? -1 : 1) * (6 + (1 - k) * 100), 0));
        }
        var bar = Ui.Rect("Bar", area).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 26), Vector2.zero);
        string mineCountry = user?.Str("country"), mineGov = user?.Str("gov");
        if (scope == "all")
        {
            string info = lbPeriod == "month"
                ? Loc.T("آخر الشهر") + ": " + Loc.T("الأول") + " " + Loc.Short(MonthlyEarnerPrizes[0]) + " · " + Loc.T("التاني") + " " + Loc.Short(MonthlyEarnerPrizes[1]) + " · " + Loc.T("التالت") + " " + Loc.Short(MonthlyEarnerPrizes[2])
                : Loc.T("كل الألعاب أونلاين: المكسب ٣ نقط واللعب نقطة");
            Line(bar, info, 12, Ui.Gold, 4, 340);
        }
        else
        {
            // where: your country or governorate, and a button to change it
            string where = scope == "country" ? CountryName(lbWhere ?? mineCountry) : GovName(lbWhere ?? mineGov);
            var change = Ui.Button(bar, "غيّر", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 12, () => { Sfx.Tap(); OpenPlace(scope); });
            FitText.OneLine(change.GetComponentInChildren<Text>());
            At((RectTransform)change.transform, 4, 64, 24);
            Line(bar, where ?? Loc.T(scope == "country" ? "اختار بلدك" : "اختار محافظتك"), 14, Ui.Gold, 76, 260);
        }
        // nothing to show yet: no country or governorate picked (governorates are in Egypt only)
        if (scope == "country" && string.IsNullOrEmpty(mineCountry)) { EmptyPick(area, "اختار بلدك عشان تشوف ترتيب بلدك", "اختار بلدك", scope); return; }
        if (scope == "gov" && string.IsNullOrEmpty(mineGov))
        {
            if (!string.IsNullOrEmpty(mineCountry) && mineCountry != "EG") { Empty(area, "ترتيب المحافظات لمصر بس"); return; }
            EmptyPick(area, "اختار محافظتك عشان تشوف ترتيبها", "اختار محافظتك", scope); return;
        }
        // the column heads, the rows, and your own place at the bottom
        var head = Ui.Rect("Head", area).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 22), new Vector2(0, -30));
        RankCells(head, Loc.T("الترتيب"), "", Loc.T("نقط"), Loc.T("مكسب"), new Color(1, 1, 1, 0.5f), 12, false);
        var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one;
        listArea.offsetMin = new Vector2(0, lbMe != null ? 38 : 0); listArea.offsetMax = new Vector2(0, -54);
        if (lbRows == null || lbRows.Count == 0) { Empty(listArea, lbRows == null ? "جاري التحميل…" : "مفيش نتايج لسه. العب أونلاين وكسّب نقط"); return; }
        var list = Scroll(listArea, 4);
        foreach (var o in lbRows)
        {
            if (!(o is Dictionary<string, object> r)) continue;
            int rank = (int)r.Num("rank");
            var row = Row(list, 32, r.Bool("me") ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
            var nameCell = RankCells(row, rank.ToString(), r.Str("name"), ((int)r.Num("rating")).ToString("N0"), ((int)r.Num("wins")).ToString("N0"), rank == 1 ? Ui.Gold : Color.white, 14, true);
            Avatar(row, (int)r.Num("ch"), r.Str("photo"), 58, 28);
            // their title, after the name (the name gives way so both stay on the line)
            var tb = Titles.Badge(row, r.Str("title"), 9);
            if (tb != null)
            {
                float room = 254 - tb.sizeDelta.x - 6;
                nameCell.rectTransform.sizeDelta = new Vector2(room, nameCell.rectTransform.sizeDelta.y);
                float nx = 92 + Mathf.Min(nameCell.preferredWidth, room) + 6;
                At(tb, nx, tb.sizeDelta.x, tb.sizeDelta.y);
            }
            string rpid = r.Str("pid"), rname = r.Str("name"); int rch = (int)r.Num("ch");
            if (!string.IsNullOrEmpty(rpid)) { var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None; hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(rpid, rname, rch); }); }
        }
        if (lbMe != null)
        {
            var mine = Ui.Box(area, "Me", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.25f)).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 34), Vector2.zero);
            RankCells(mine, ((int)lbMe.Num("rank")).ToString("N0"), Loc.T("مركزك") + " · " + lbMe.Str("name"), ((int)lbMe.Num("rating")).ToString("N0"), ((int)lbMe.Num("wins")).ToString("N0"), Ui.Gold, 14, true);
            Avatar(mine, (int)lbMe.Num("ch"), user?.Str("photo"), 58, 28);
        }
    }

    Text RankCells(RectTransform row, string rank, string name, string points, string wins, Color c, int size, bool face)
    {
        Line(row, rank, size, c, 6, 50, 30, TextAnchor.MiddleCenter);
        var n = Line(row, name, size, c, face ? 92 : 66, face ? 254 : 280, 30);
        Line(row, points, size, c, 360, 90, 30, TextAnchor.MiddleCenter);
        Line(row, wins, size, c, 460, 90, 30, TextAnchor.MiddleCenter);
        return n;
    }

    void Empty(RectTransform area, string text)
    {
        var t = Ui.Label(area, "", 15, Ui.GoldLight, TextAnchor.MiddleCenter); t.Set(Loc.T(text)); FitText.OneLine(t);
        t.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(540, 30), new Vector2(0, -10));
    }
    void EmptyPick(RectTransform area, string text, string button, string scope)
    {
        Empty(area, text);
        var b = Ui.Button(area, button, Ui.Gold, Ui.Ink, 14, () => { Sfx.Tap(); OpenPlace(scope); });
        FitText.OneLine(b.GetComponentInChildren<Text>());
        ((RectTransform)b.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180, 36), new Vector2(0, -50));
    }

    /// The country or governorate picker, coming back here after.
    void OpenPlace(string scope)
    {
        int tab = lbTab;
        pickerBack = () => OpenLeaders(tab);
        Close();
        if (scope == "country") OpenCountryPicker(); else OpenGovPicker();
    }

    /// Settings (or the leaderboard): which country you play for.
    void OpenCountryPicker()
    {
        Panel(Loc.T("بلدك"), out var body, new Vector2(600, 340));
        var list = Scroll(body, 4);
        RectTransform line = null;
        for (int i = 0; i < Countries.GetLength(0); i++)
        {
            if (i % 3 == 0) line = Row(list, 38);
            string id = Countries[i, 0];
            int k = Loc.En ? i % 3 : 2 - i % 3;
            bool on = user?.Str("country") == id;
            var b = Ui.Button(line, Loc.En ? Countries[i, 2] : Countries[i, 1], on ? Ui.Gold : Card, on ? Ui.Ink : Color.white, 14, () =>
            {
                Sfx.Tap();
                app.Send(new Dictionary<string, object> { ["t"] = "setCountry", ["country"] = id });
                if (user != null) { user["country"] = id; if (id != "EG") user["gov"] = ""; }
                Close(); PickerDone();
            });
            FitText.OneLine(b.GetComponentInChildren<Text>());
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(k / 3f, 0); rt.anchorMax = new Vector2((k + 1) / 3f, 1); rt.offsetMin = new Vector2(3, 2); rt.offsetMax = new Vector2(-3, -2);
        }
    }
    /// After picking: back to where the picker was opened from (the leaderboard), or Settings.
    void PickerDone()
    {
        var back = pickerBack; pickerBack = null;
        if (back != null) back(); else OpenSettings();
    }

    /// The titles tab: who wears the monthly titles now, and who leads this month's race for yours.
    void DrawTitles(RectTransform area)
    {
        var info = Line(area, Loc.T("يوم ١ في كل شهر: الأول في ترتيب الشهر بياخد اللقب ٣٠ يوم"), 12, Ui.Gold, 0, 560, 24, TextAnchor.MiddleCenter);
        info.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 24), Vector2.zero);
        var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = Vector2.zero; listArea.offsetMax = new Vector2(0, -28);
        if (titleData == null) { Empty(listArea, "جاري التحميل…"); return; }
        if (!titleData.Bool("on")) { Empty(listArea, "ألقاب الشهر متوقفة دلوقتي"); return; }
        var list = Scroll(listArea, 4);
        double now = App.LocalMs();
        // this month's race: who leads for each title you can win
        Section(list, Loc.T("السباق الشهر ده") + " · " + Loc.T("لازم") + " " + (int)titleData.Num("minGames") + " " + Loc.T("ماتشات على الأقل"));
        foreach (var o in titleData.Arr("race") ?? new List<object>())
        {
            if (!(o is Dictionary<string, object> r)) continue;
            var lead = r.Obj("leader");
            var row = Row(list, 32, lead != null && lead.Bool("me") ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
            float x = TitleCell(row, r.Str("title"), r.Str("where"));
            Line(row, lead == null ? Loc.T("مفيش متصدر لسه") : Loc.T("المتصدر") + ": " + lead.Str("name") + " (" + (int)lead.Num("points") + " " + Loc.T("نقطة") + ")", 13, lead == null ? Ui.GoldLight : Color.white, x, 548 - x, 30);
        }
        // the holders now
        var holders = titleData.Arr("holders") ?? new List<object>();
        Section(list, Loc.T("أصحاب الألقاب دلوقتي"));
        if (holders.Count == 0) { var none = Row(list, 32, Card); Line(none, Loc.T("لسه محدش خد لقب. أول لقب يوم ١ في الشهر الجاي"), 13, Ui.GoldLight, 10, 540, 30); }
        foreach (var o in holders)
        {
            if (!(o is Dictionary<string, object> h)) continue;
            var row = Row(list, 32, h.Bool("me") ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
            float x = TitleCell(row, h.Str("title"), h.Str("where"));
            Avatar(row, (int)h.Num("ch"), h.Str("photo"), x, 26);
            int days = Mathf.Max(1, Mathf.CeilToInt((float)((h.Num("until") - now) / 86_400_000)));
            Line(row, h.Str("name"), 13, Color.white, x + 32, 400 - x, 30);
            Line(row, Loc.En ? days + (days == 1 ? " day left" : " days left") : "فاضل " + days + " يوم", 12, Ui.GoldLight, 440, 108, 30, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Body);
            string rpid = h.Str("pid"), rname = h.Str("name"); int rch = (int)h.Num("ch");
            if (!string.IsNullOrEmpty(rpid)) { var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None; hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(rpid, rname, rch); }); }
        }
    }

    void Section(RectTransform list, string text)
    {
        var row = Row(list, 24);
        Line(row, text, 12, new Color(1, 1, 1, 0.55f), 6, 548, 22);
    }

    /// A title's badge and where it is for (a country or a governorate); returns where the rest of the row starts.
    float TitleCell(RectTransform row, string title, string where)
    {
        float x = 8;
        var b = Titles.Badge(row, title, 9);
        if (b != null) { At(b, x, b.sizeDelta.x, b.sizeDelta.y); x += b.sizeDelta.x + 6; }
        string place = string.IsNullOrEmpty(where) ? null : (title == "champ_country" ? CountryName(where) : GovName(where));
        if (place != null) { Line(row, place, 12, Ui.Gold, x, 96, 30); x += 100; }
        return x;
    }
}
