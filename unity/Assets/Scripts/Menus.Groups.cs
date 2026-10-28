// Friends' café groups ("قهوة الشلة"): saved friends who play together. Make one, share its code, see who's
// online and the group's ranking this week, open a table for the group (everyone online is invited), and
// watch a member's game.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    List<object> groupRows;                  // your groups
    Dictionary<string, object> groupOpen;    // the group you're looking at
    int friendsTab;                          // 0 friends · 1 groups

    void OnGroupMessage(Dictionary<string, object> m)
    {
        switch (m.Str("t"))
        {
            case "groups": groupRows = m.Arr("rows") ?? new List<object>(); if (friendsTab == 1) refreshOpen?.Invoke(); break;
            case "group": groupOpen = m.Obj("group"); if (friendsTab == 1) refreshOpen?.Invoke(); break;
        }
    }

    /// The friends panel's second tab.
    void DrawGroups(RectTransform area)
    {
        if (groupOpen != null) { DrawGroup(area, groupOpen); return; }
        // make one, or join one by its code
        var top = Ui.Box(area, "New", Card).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 40), Vector2.zero);
        var name = Field(top, Loc.T("اسم الشلة"), 13, false); name.characterLimit = 24;
        At((RectTransform)name.transform, 8, 150, 30);
        var mk = Ui.Button(top, "+ شلة جديدة", Ui.Gold, Ui.Ink, 13, () =>
        {
            Sfx.Tap(); var n = name.text.Trim();
            if (n.Length < 2) { app.Toast(Loc.T("اكتب اسم للشلة")); return; }
            app.Send(new Dictionary<string, object> { ["t"] = "groupCreate", ["name"] = n });
        });
        At((RectTransform)mk.transform, 164, 120, 30);
        var code = Field(top, Loc.T("كود الشلة"), 13, false); code.characterLimit = 8;
        At((RectTransform)code.transform, 330, 140, 30);
        var jn = Ui.Button(top, "ادخل", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 13, () =>
        {
            Sfx.Tap(); var c = code.text.Trim();
            if (c.Length >= 4) app.Send(new Dictionary<string, object> { ["t"] = "groupJoin", ["code"] = c });
        });
        At((RectTransform)jn.transform, 478, 110, 30);

        var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = Vector2.zero; listArea.offsetMax = new Vector2(0, -48);
        if (groupRows == null) { Ui.Label(listArea, Loc.T("جاري التحميل…"), 15, Ui.GoldLight).rectTransform.Fill(); return; }
        if (groupRows.Count == 0)
        {
            var t = Ui.Label(listArea, "", 14, Ui.GoldLight); t.Set(Loc.T("اعمل شلة لصحابك: ترابيزة بضغطة واحدة، وترتيب بينكم كل أسبوع"));
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.rectTransform.Fill(20);
            return;
        }
        var list = Scroll(listArea, 5);
        foreach (var o in groupRows)
        {
            if (!(o is Dictionary<string, object> g)) continue;
            int id = (int)g.Num("id");
            var row = Row(list, 46, Card);
            var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => { Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "groupView", ["id"] = (double)id }); });
            var ic = Ui.Picture(row, "Icon", Icon("friends")); ic.color = Ui.Gold; At(ic.rectTransform, 10, 30, 30);
            var n = Ui.Label(row, "", 16, Color.white, StartAlign, Ui.Bold); n.Set(g.Str("name"));
            At(n.rectTransform, 50, 260, 22).anchoredPosition += new Vector2(0, 9);
            var sub = Ui.Label(row, "", 12, Ui.GoldLight, StartAlign);
            sub.Set((int)g.Num("members") + " " + Loc.T("أعضاء") + "  ·  " + (int)g.Num("online") + " " + Loc.T("متصلين") + "  ·  " + g.Str("code"));
            At(sub.rectTransform, 50, 300, 18).anchoredPosition += new Vector2(0, -10);
            var open = Ui.Button(row, "افتح", Ui.Gold, Ui.Ink, 13, () => { Sfx.Tap(); app.Send(new Dictionary<string, object> { ["t"] = "groupView", ["id"] = (double)id }); });
            At((RectTransform)open.transform, 480, 100, 32);
        }
    }

    static readonly string[] GroupModes = { "ffa4", "tawla", "chess", "ludo4" };

    void DrawGroup(RectTransform area, Dictionary<string, object> g)
    {
        int id = (int)g.Num("id");
        string code = g.Str("code"), gname = g.Str("name");
        // the head: back, name and code, share
        var head = Ui.Box(area, "Head", Card).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 40), Vector2.zero);
        var back = Ui.Button(head, Loc.En ? "‹ Back" : "رجوع ›", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 13, () => { Sfx.Tap(); groupOpen = null; app.Send(new Dictionary<string, object> { ["t"] = "groups" }); refreshOpen?.Invoke(); });
        At((RectTransform)back.transform, 6, 76, 30);
        var nm = Ui.Label(head, "", 16, Ui.Gold, StartAlign, Ui.Bold); nm.Set(gname + "  ·  " + code);
        At(nm.rectTransform, 90, 250, 34);
        var share = Ui.Button(head, "ادعي للشلة", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 13, () =>
        {
            Sfx.Tap();
            string link = Growth.InviteLink(user?.Str("code") ?? "");
            Growth.Share(Loc.En ? "Join our café group \"" + gname + "\" in Board Battles: code " + code + "\n" + link : "ادخل شلتنا «" + gname + "» في بورد باتلز: الكود " + code + "\n" + link);
        });
        At((RectTransform)share.transform, 346, 120, 30);
        var leave = Ui.Button(head, "اخرج", new Color(0.42f, 0.16f, 0.16f, 1), Color.white, 13, () =>
        {
            Sfx.Tap(); groupOpen = null;
            app.Send(new Dictionary<string, object> { ["t"] = "groupLeave", ["id"] = (double)id });
        });
        At((RectTransform)leave.transform, 474, 70, 30);
        // play: a table for the group in each game
        var play = Ui.Rect("Play", area).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(600, 34), new Vector2(0, -46));
        var pl = Ui.Label(play, "", 13, Ui.GoldLight, StartAlign, Ui.Bold); pl.Set(Loc.T("ترابيزة للشلة") + ":");
        At(pl.rectTransform, 6, 110, 30);
        string[] games = { "domino", "tawla", "chess", "ludo" };
        for (int i = 0; i < games.Length; i++)
        {
            string mode = GroupModes[i];
            var b = Ui.Button(play, App.GameNames[games[i]], Ui.Gold, Ui.Ink, 13, () => { Sfx.Tap(); Close(); app.Send(new Dictionary<string, object> { ["t"] = "groupPlay", ["id"] = (double)id, ["mode"] = mode }); });
            At((RectTransform)b.transform, 120 + i * 120, 112, 30);
        }
        // the week's ranking among the group
        var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(0, 18); listArea.offsetMax = new Vector2(0, -86);
        var list = Scroll(listArea, 4);
        foreach (var o in g.Arr("members") ?? new List<object>())
        {
            if (!(o is Dictionary<string, object> mb)) continue;
            bool me = mb.Bool("me"), on = mb.Bool("online"), playing = mb.Bool("playing");
            int rank = (int)mb.Num("rank");
            string pid = mb.Str("pid"), mname = mb.Str("name"); int ch = (int)mb.Num("ch");
            var row = Row(list, 40, me ? new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.2f) : Card);
            var hit = row.gameObject.AddComponent<Button>(); hit.transition = Selectable.Transition.None;
            hit.onClick.AddListener(() => { Sfx.Tap(); OpenCard(pid, mname, ch); });
            At(Ui.Label(row, rank.ToString(), 15, rank == 1 ? Ui.Gold : Color.white, TextAnchor.MiddleCenter, Ui.Bold).rectTransform, 4, 30, 30);
            var face = Ui.Picture(row, "Face", Face(ch)); At(face.rectTransform, 36, 28, 34);
            var dot = Ui.Picture(row, "Dot", GameUi.Disc.texture); dot.color = on ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.45f, 0.45f, 0.45f);
            At(dot.rectTransform, 60, 9, 9); dot.rectTransform.anchoredPosition += new Vector2(0, -12);
            var n = Ui.Label(row, "", 14, Color.white, StartAlign, Ui.Bold); n.Set(mname);
            At(n.rectTransform, 74, 200, 30);
            var tb = Titles.Badge(row, mb.Str("title"), 9);
            if (tb != null) At(tb, 74 + Mathf.Min(n.preferredWidth, 160) + 8, tb.sizeDelta.x, tb.sizeDelta.y);
            var pts = Ui.Label(row, "", 13, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
            pts.Set((int)mb.Num("points") + " " + Loc.T("نقط") + "  ·  " + (int)mb.Num("wins") + "/" + (int)mb.Num("games"));
            At(pts.rectTransform, 340, 130, 30);
            if (playing && !me)
            {
                var w = Ui.Button(row, "تفرّج", new Color(1, 1, 1, 0.12f), Ui.GoldLight, 12, () => { Sfx.Tap(); Close(); app.Watch(pid); });
                At((RectTransform)w.transform, 500, 84, 28);
            }
        }
        var foot = Ui.Label(area, "", 11, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        foot.Set(Loc.T("ترتيب الأسبوع: ٣ نقط للمكسب ونقطة للعب، في الماتشات الأونلاين"));
        foot.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(600, 16), new Vector2(0, 0));
    }
}
