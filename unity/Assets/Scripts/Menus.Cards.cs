// Celebration cards: a big moment (a new title, a monthly prize, a weekly prize, coming back) shown on its own,
// with a button to share a picture of it (WhatsApp and the rest) with an invite link.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    /// A card: a headline, a line under it, maybe a title's badge, your face and name; "share" sends a picture of it.
    public void ShowCard(string headline, string line, string titleId, bool share, string shareText = null)
    {
        Panel("", out var body, new Vector2(560, 330));
        // light rays behind
        var glow = Ui.Picture(body, "Glow", Ui.Glow(256)); glow.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.35f);
        glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(420, 260), new Vector2(0, -90));
        // you
        int ch = (int)(user?.Num("ch") ?? 0);
        var frame = Ui.Box(body, "Frame", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(84, 104), new Vector2(0, 6));
        Ui.Picture(frame, "Face", Face(ch)).rectTransform.Fill(3);
        var nm = Ui.Label(body, "", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold); nm.Set(user?.Str("name") ?? "");
        nm.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(400, 22), new Vector2(0, -102));
        float y = -128;
        var b = Titles.Badge(body, titleId, 17);
        if (b != null) { b.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), b.sizeDelta, new Vector2(0, y - 18)); y -= 40; StartCoroutine(Bounce(b)); }
        var h = Ui.Label(body, "", 26, Ui.Gold, TextAnchor.MiddleCenter, Ui.Display); h.Set(headline);
        h.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(520, 40), new Vector2(0, y));
        var l = Ui.Label(body, "", 15, Color.white, TextAnchor.MiddleCenter, Ui.Bold); l.Set(line);
        l.horizontalOverflow = HorizontalWrapMode.Wrap;
        l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 44), new Vector2(0, y - 40));
        // the game's name, for the picture
        var brand = Ui.Label(body, "", 11, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, Ui.Bold); brand.Set(Loc.T("بورد باتلز") + " · Board Battles");
        brand.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 16), new Vector2(0, 46));

        var buttons = new List<GameObject>();
        if (share)
        {
            var sb = Ui.Button(body, "شارك", new Color32(0x2f, 0x6b, 0x3a, 0xff), Color.white, 16, () =>
            {
                Sfx.Tap();
                string code = user?.Str("code") ?? "", link = Growth.InviteLink(code);
                string text = (shareText ?? headline + " " + line) + (Loc.En ? "\nPlay with me in Board Battles: " : "\nتعالى العب معايا في بورد باتلز: ") + link;
                StartCoroutine(Growth.ShareScreen(text, () => { foreach (var x in buttons) x.SetActive(false); }, () => { foreach (var x in buttons) if (x != null) x.SetActive(true); }));
            });
            ((RectTransform)sb.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 40), new Vector2(Loc.En ? -82 : 82, 0));
            buttons.Add(sb.gameObject);
        }
        var ok = Ui.Button(body, "تمام", Ui.Gold, Ui.Ink, 16, () => { Sfx.Tap(); Close(); });
        ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 40), new Vector2(share ? (Loc.En ? 82 : -82) : 0, 0));
        buttons.Add(ok.gameObject);
        // the panel's own close button is left out of the picture too
        var x0 = open?.GetComponentsInChildren<Button>(true);
        if (x0 != null) foreach (var x in x0) if (x.name == "Button" && x.transform.parent != null && x.transform.parent.name == "Sheet") buttons.Add(x.gameObject);
        Sfx.Chime();
    }

    /// The event on now: what's special, how far you are toward its title, its offers in the shop.
    public void OpenEvent()
    {
        var ev = user?.Obj("event"); if (ev == null) return;
        string name = Loc.En ? ev.Str("en") : ev.Str("ar");
        Panel(name, out var body, new Vector2(540, 330));
        double left = ev.Num("to") - App.LocalMs();
        int days = Mathf.Max(0, (int)(left / 86_400_000)), hours = Mathf.Max(0, (int)(left % 86_400_000 / 3_600_000));
        var lines = new List<string>();
        if (ev.Num("spMult", 1) > 1) lines.Add(Loc.T("نقط الموسم") + " ×" + ev.Num("spMult"));
        if (ev.Num("missionMult", 1) > 1) lines.Add(Loc.T("كوينز المهام") + " ×" + ev.Num("missionMult"));
        lines.Add(Loc.T("سكينز مخصوص في المتجر لحد آخر الحدث"));
        var l = Ui.Label(body, "", 16, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        l.Set(string.Join("\n", lines) + "\n" + Loc.T("فاضل") + " " + days + Loc.T("ي") + " " + hours + Loc.T("س"));
        l.horizontalOverflow = HorizontalWrapMode.Wrap;
        l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 110), new Vector2(0, -2));
        // the title for taking part
        var t = Titles.Get(ev.Str("titleId"));
        int need = (int)ev.Num("titleGames"), have = (int)ev.Num("games");
        if (t != null && need > 0)
        {
            var b = Titles.Badge(body, t.id, 15);
            b.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), b.sizeDelta, new Vector2(0, -136));
            var p = Ui.Label(body, "", 13, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
            p.Set(have >= need ? Loc.T("اللقب بتاعك!") : Loc.T("العب") + " " + need + " " + Loc.T("ماتش أونلاين وخد اللقب") + "  ·  " + Mathf.Min(have, need) + " / " + need);
            p.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(500, 22), new Vector2(0, -164));
            var track = Ui.Box(body, "Track", new Color(1, 1, 1, 0.1f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 10), new Vector2(0, -188));
            var fill = Ui.Box(track, "Fill", Ui.Gold).rectTransform;
            float k = Mathf.Clamp01(have / (float)need);
            fill.anchorMin = new Vector2(Loc.En ? 0 : 1 - k, 0); fill.anchorMax = new Vector2(Loc.En ? k : 1, 1); fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
        var shop = Ui.Button(body, "عروض المتجر", Ui.Gold, Ui.Ink, 16, () => { Sfx.Tap(); Close(); OpenShop(1); });
        ((RectTransform)shop.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(180, 42), Vector2.zero);
    }

    /// The season pass, shown when the game opens: a few of its skins, what it gives, its price.
    public void ShowSeasonPromo()
    {
        var s = user?.Obj("season"); if (s == null) return;
        Panel(Loc.T("الموسم المميز"), out var body, new Vector2(600, 340));
        // a glow behind, and five of the season's own skins in a row
        var glow = Ui.Picture(body, "Glow", Ui.Glow(256)); glow.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.3f);
        glow.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(520, 200), new Vector2(0, -60));
        var picks = new List<string>();
        foreach (var o in s.Arr("tiers") ?? new List<object>())
        {
            var p = (o as Dictionary<string, object>)?.Obj("prem"); string id = p?.Str("skin");
            var d = Skins.Get(id); if (d != null && d.pass && !picks.Contains(id)) picks.Add(id);
        }
        if (picks.Count > 5) picks = picks.GetRange(picks.Count - 5, 5);   // the best ones are at the end of the track
        for (int i = 0; i < picks.Count; i++)
        {
            int k = Loc.En ? i : picks.Count - 1 - i;
            var card = Ui.Box(body, "Skin", Card).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(96, 104), new Vector2((k - (picks.Count - 1) / 2f) * 104, -6));
            var pic = Ui.Picture(card, "Look", Skins.Picture(picks[i], out var tint)); pic.color = tint;
            pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, 60), new Vector2(0, -8));
            var nm = Ui.Label(card, Skins.Name(picks[i]), 11, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
            nm.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(92, 30), new Vector2(0, 4));
            nm.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
        var l = Ui.Label(body, "", 16, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        l.Set(Loc.T("سكين في كل مستوى (٣٠ سكين) + بدون إعلانات لآخر الموسم"));
        l.horizontalOverflow = HorizontalWrapMode.Wrap;
        l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 44), new Vector2(0, -122));
        double left = s.Num("endsAt") - App.LocalMs();
        var t = Ui.Label(body, "", 12, Ui.GoldLight, TextAnchor.MiddleCenter);
        int daysLeft = Mathf.Max(0, (int)(left / 86_400_000));
        t.Set(Loc.En ? "The season ends in " + daysLeft + (daysLeft == 1 ? " day" : " days") : Loc.T("الموسم بيخلص بعد") + " " + daysLeft + " يوم");
        t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 20), new Vector2(0, -166));
        var buy = Ui.Button(body, Loc.T("افتح المميز") + "  " + (int)s.Num("egp", 299) + " " + Loc.T("ج.م"), Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); Close(); Billing.Buy("season_pass"); });
        ((RectTransform)buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 44), new Vector2(Loc.En ? 128 : -128, 0));
        var see = Ui.Button(body, "شوف الموسم", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 15, () => { Sfx.Tap(); Close(); OpenSeason(); });
        ((RectTransform)see.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 44), new Vector2(Loc.En ? -70 : 70, 0));
        var later = Ui.Button(body, "بعدين", new Color(1, 1, 1, 0.06f), new Color(1, 1, 1, 0.6f), 14, () => { Sfx.Tap(); Close(); });
        ((RectTransform)later.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(100, 44), new Vector2(Loc.En ? -210 : 210, 0));
    }

    /// New here? An easy first game of dominoes with hints, and a gift at the end.
    void OfferGuide()
    {
        Panel(Loc.T("أهلاً بيك في القهوة!"), out var body, new Vector2(520, 300));
        int ch = (int)(user?.Num("ch") ?? 0);
        var frame = Ui.Box(body, "Frame", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f)).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(76, 94), new Vector2(0, 0));
        Ui.Picture(frame, "Face", Face(ch)).rectTransform.Fill(3);
        var l = Ui.Label(body, "", 16, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        l.Set(Loc.T("أول مرة هنا؟ العب دور دومينو سهل ضد الكمبيوتر وإحنا هنقولك تعمل إيه خطوة بخطوة") + "\n" + Loc.T("وفي الآخر خد") + " " + Loc.Short((long)(user?.Num("tutorialCoins", 10000) ?? 10000)) + " " + Loc.T("كوين هدية"));
        l.horizontalOverflow = HorizontalWrapMode.Wrap;
        l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(470, 84), new Vector2(0, -104));
        var go = Ui.Button(body, "يلا نلعب", Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); Close(); Track("guide_start"); app.StartGuide(); });
        ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 44), new Vector2(Loc.En ? 90 : -90, 0));
        var later = Ui.Button(body, "بعدين", new Color(1, 1, 1, 0.1f), Ui.GoldLight, 15, () => { Sfx.Tap(); Close(); Track("guide_skip"); });
        ((RectTransform)later.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(130, 44), new Vector2(Loc.En ? -90 : 90, 0));
    }

    /// Something worth counting in the dashboard's analytics (only a few names are kept).
    void Track(string name) { if (app.net != null && app.net.Online) app.Send(new Dictionary<string, object> { ["t"] = "track", ["name"] = name }); }

    static IEnumerator Bounce(RectTransform r)
    {
        for (float t = 0; t < 0.5f; t += Time.unscaledDeltaTime) { if (r == null) yield break; r.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, Ui.Back(t / 0.5f)); yield return null; }
        if (r != null) r.localScale = Vector3.one;
    }
}
