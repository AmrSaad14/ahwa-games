// Leagues (bronze to diamond, per game and season) and the governorate leaderboard, as two tabs of the
// leaderboard, plus the governorate picker in Settings.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    Dictionary<string, object> govData;

    static readonly (string id, string ar, string en, Color color, long reward, int from)[] Leagues =
    {
        ("bronze", "برونز", "Bronze", new Color(0.80f, 0.50f, 0.20f), 5_000, 0),
        ("silver", "فضة", "Silver", new Color(0.78f, 0.80f, 0.84f), 15_000, 300),
        ("gold", "دهب", "Gold", new Color(1f, 0.82f, 0.36f), 40_000, 700),
        ("platinum", "بلاتين", "Platinum", new Color(0.50f, 0.88f, 0.86f), 100_000, 1200),
        ("diamond", "ماس", "Diamond", new Color(0.55f, 0.72f, 1f), 250_000, 1800),
    };
    static int LeagueIndex(string id) { for (int i = 0; i < Leagues.Length; i++) if (Leagues[i].id == id) return i; return 0; }
    static string LeagueName(string id) { var l = Leagues[LeagueIndex(id)]; return Loc.En ? l.en : l.ar; }

    static readonly string[,] Govs =
    {
        { "cairo", "القاهرة", "Cairo" }, { "giza", "الجيزة", "Giza" }, { "alex", "الإسكندرية", "Alexandria" }, { "qalyubia", "القليوبية", "Qalyubia" },
        { "sharqia", "الشرقية", "Sharqia" }, { "dakahlia", "الدقهلية", "Dakahlia" }, { "gharbia", "الغربية", "Gharbia" }, { "monufia", "المنوفية", "Monufia" },
        { "beheira", "البحيرة", "Beheira" }, { "kafr", "كفر الشيخ", "Kafr El Sheikh" }, { "damietta", "دمياط", "Damietta" }, { "portsaid", "بورسعيد", "Port Said" },
        { "ismailia", "الإسماعيلية", "Ismailia" }, { "suez", "السويس", "Suez" }, { "fayoum", "الفيوم", "Faiyum" }, { "benisuef", "بني سويف", "Beni Suef" },
        { "minya", "المنيا", "Minya" }, { "assiut", "أسيوط", "Asyut" }, { "sohag", "سوهاج", "Sohag" }, { "qena", "قنا", "Qena" },
        { "luxor", "الأقصر", "Luxor" }, { "aswan", "أسوان", "Aswan" }, { "redsea", "البحر الأحمر", "Red Sea" }, { "newvalley", "الوادي الجديد", "New Valley" },
        { "matrouh", "مطروح", "Matrouh" }, { "northsinai", "شمال سيناء", "North Sinai" }, { "southsinai", "جنوب سيناء", "South Sinai" },
    };
    static string GovName(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Govs.GetLength(0); i++) if (Govs[i, 0] == id) return Loc.En ? Govs[i, 2] : Govs[i, 1];
        return id;
    }

    /// The leagues tab: your league in each game this season, the points to the next, and the rewards.
    void DrawLeagues(RectTransform area)
    {
        var mine = user?.Obj("leagues");
        var info = Ui.Label(area, "", 12, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        info.Set(Loc.T("كل لعبة ليها دوري. اكسب أونلاين عشان تطلع. في آخر الشهر بتاخد جايزة أعلى دوري وصلته، والنقط بتبدأ من الأول"));
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        info.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 34), Vector2.zero);
        var games = new[] { "domino", "tawla", "chess", "ludo" };
        for (int i = 0; i < games.Length; i++)
        {
            var g = games[i];
            var l = mine?.Obj(g);
            int idx = l != null ? LeagueIndex(l.Str("league")) : 0;
            double lp = l?.Num("lp") ?? 0;
            var lg = Leagues[idx];
            int k = Loc.En ? i : 3 - i;
            var card = Ui.Box(area, "League", Card).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(134, 150), new Vector2((k - 1.5f) * 140, -40));
            Ui.Label(card, Loc.T(App.GameNames[g]), 14, Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(130, 22), new Vector2(0, -4));
            var medal = Ui.Picture(card, "Medal", Icon("t_medal")); medal.color = lg.color;
            medal.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(54, 54), new Vector2(0, -28));
            Ui.Label(card, Loc.En ? lg.en : lg.ar, 16, lg.color, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(130, 22), new Vector2(0, -84));
            // the points towards the next league
            bool top = idx == Leagues.Length - 1;
            int from = lg.from, next = top ? lg.from : Leagues[idx + 1].from;
            float f = top ? 1 : Mathf.Clamp01((float)((lp - from) / Mathf.Max(1, next - from)));
            var bg = Ui.Box(card, "Bar", new Color(0, 0, 0, 0.35f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(110, 8), new Vector2(0, -112));
            var fill = Ui.Box(bg, "Fill", lg.color).rectTransform;
            fill.anchorMin = new Vector2(Loc.En ? 0 : 1 - f, 0); fill.anchorMax = new Vector2(Loc.En ? f : 1, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
            Ui.Label(card, top ? (int)lp + " LP" : (int)lp + " / " + next + " LP", 11, Ui.GoldLight, TextAnchor.MiddleCenter).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(130, 18), new Vector2(0, -124));
        }
        // the rewards, league by league
        var rw = new List<string>();
        foreach (var l in Leagues) rw.Add((Loc.En ? l.en : l.ar) + " " + Loc.Short(l.reward));
        var rl = Ui.Label(area, "", 12, Color.white, TextAnchor.MiddleCenter);
        rl.Set(Loc.T("جوايز آخر الموسم") + ":  " + string.Join("  ·  ", rw));
        rl.horizontalOverflow = HorizontalWrapMode.Wrap;
        rl.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 34), new Vector2(0, 4));
    }

    /// The governorates tab: this week's governorates by score; tap one for its best players.
    void DrawGovs(RectTransform area)
    {
        if (govData == null) { Ui.Label(area, Loc.T("جاري التحميل…"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
        string mine = govData.Str("mine"), shown = govData.Str("gov"), last = govData.Str("lastWinner");
        var info = Ui.Label(area, "", 12, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        info.Set(Loc.T("محافظة الأسبوع: لاعيبتها اللي لعبوا") + " " + (int)govData.Num("minGames") + " " + Loc.T("ماتشات ياخدوا") + " " + Loc.Short((long)govData.Num("prize")) + " " + Loc.T("كوين")
            + (string.IsNullOrEmpty(last) ? "" : "  ·  " + Loc.T("الأسبوع اللي فات") + ": " + GovName(last))
            + (string.IsNullOrEmpty(mine) ? "  ·  " + Loc.T("اختار محافظتك من الإعدادات") : ""));
        info.horizontalOverflow = HorizontalWrapMode.Wrap;
        info.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 32), Vector2.zero);
        // two columns: the governorates (reading side) and the chosen one's players
        var left = Ui.Rect("Govs", area); left.anchorMin = new Vector2(Loc.En ? 0 : 0.5f, 0); left.anchorMax = new Vector2(Loc.En ? 0.5f : 1, 1); left.offsetMin = new Vector2(2, 0); left.offsetMax = new Vector2(-2, -36);
        var right = Ui.Rect("Players", area); right.anchorMin = new Vector2(Loc.En ? 0.5f : 0, 0); right.anchorMax = new Vector2(Loc.En ? 1 : 0.5f, 1); right.offsetMin = new Vector2(2, 0); right.offsetMax = new Vector2(-2, -36);
        var govs = govData.Arr("govs") ?? new List<object>();
        if (govs.Count == 0) Ui.Label(left, Loc.T("مفيش نتايج لسه"), 14, Ui.GoldLight).rectTransform.Fill();
        else
        {
            var gl = Scroll(left, 3);
            int rank = 0;
            foreach (var o in govs)
            {
                if (!(o is Dictionary<string, object> g)) continue;
                rank++;
                string id = g.Str("gov");
                var row = Row(gl, 30, id == shown ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
                var t = Ui.Label(row, "", 13, id == mine ? Ui.Gold : Color.white, StartAlign, Ui.Bold);
                t.Set(rank + ".  " + GovName(id) + (id == mine ? "  ★" : ""));
                t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
                var p = Ui.Label(row, ((int)g.Num("points")).ToString("N0"), 13, Ui.GoldLight, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Bold);
                p.rectTransform.Fill(); p.rectTransform.offsetMin = new Vector2(10, 0); p.rectTransform.offsetMax = new Vector2(-10, 0);
                var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
                hit.onClick.AddListener(() => { Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "govBoard", ["gov"] = id }); });
            }
        }
        var players = govData.Arr("players") ?? new List<object>();
        var head = Ui.Label(right, GovName(shown) ?? "", 14, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        head.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(270, 22), Vector2.zero);
        var pa = Ui.Rect("List", right); pa.anchorMin = Vector2.zero; pa.anchorMax = Vector2.one; pa.offsetMin = Vector2.zero; pa.offsetMax = new Vector2(0, -24);
        if (players.Count == 0) { Ui.Label(pa, Loc.T("مفيش لاعيبة لسه الأسبوع ده"), 13, Ui.GoldLight).rectTransform.Fill(); return; }
        var pl = Scroll(pa, 3);
        foreach (var o in players)
        {
            if (!(o is Dictionary<string, object> r)) continue;
            var row = Row(pl, 30, r.Bool("me") ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.22f) : Card);
            var t = Ui.Label(row, "", 13, Color.white, StartAlign, Ui.Bold);
            t.Set((int)r.Num("rank") + ".  " + r.Str("name"));
            t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(10, 0); t.rectTransform.offsetMax = new Vector2(-10, 0);
            var p = Ui.Label(row, ((int)r.Num("points")).ToString(), 13, Ui.GoldLight, Loc.En ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Ui.Bold);
            p.rectTransform.Fill(); p.rectTransform.offsetMin = new Vector2(10, 0); p.rectTransform.offsetMax = new Vector2(-10, 0);
            string rpid = r.Str("pid"), rname = r.Str("name"); int rch = (int)r.Num("ch");
            var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(rpid, rname, rch); });
        }
    }

    /// Settings: which governorate you play for.
    void OpenGovPicker()
    {
        Panel(Loc.T("محافظتك"), out var body, new Vector2(600, 340));
        var list = Scroll(body, 4);
        RectTransform line = null;
        for (int i = 0; i < Govs.GetLength(0); i++)
        {
            if (i % 3 == 0) line = Row(list, 38);
            string id = Govs[i, 0];
            int k = Loc.En ? i % 3 : 2 - i % 3;
            bool on = user?.Str("gov") == id;
            var b = Ui.Button(line, Loc.En ? Govs[i, 2] : Govs[i, 1], on ? Ui.Gold : Card, on ? Ui.Ink : Color.white, 14, () =>
            {
                Sfx.Tap();
                app.Send(new Dictionary<string, object> { ["t"] = "setGov", ["gov"] = id });
                if (user != null) user["gov"] = id;
                Close(); OpenSettings();
            });
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(k / 3f, 0); rt.anchorMax = new Vector2((k + 1) / 3f, 1); rt.offsetMin = new Vector2(3, 2); rt.offsetMax = new Vector2(-3, -2);
        }
    }
}
