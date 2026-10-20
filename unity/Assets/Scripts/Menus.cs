// The home screen's menus, over the café: the shop, the leaderboard, how to play, settings (sound, vibration,
// language, account), the "watch an ad for 5,000 coins" button, the language switch and Google sign-in.
// Everything talks to the same server as the web app: lb, buySkin / equipSkin, adStart / adWatched,
// ticket (Google), logout.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Menus : MonoBehaviour
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

        // down the left side: shop, leaderboard, how to play, settings
        var rail = Ui.Rect("Rail", home).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(64, 290), new Vector2(10, -88));
        RailButton(rail, 0, "shop", "المتجر", () => OpenShop(0));
        RailButton(rail, 1, "trophy", "المتصدرين", () => OpenLeaders(0));
        RailButton(rail, 2, "help", "طريقة اللعب", () => OpenHowTo(0));
        RailButton(rail, 3, "settings", "الإعدادات", OpenSettings);

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

    void RailButton(RectTransform rail, int i, string icon, string label, Action onClick)
    {
        var b = Ui.Button(rail, "", Ui.Panel, Ui.GoldLight, 10, () => { Sfx.Tap(); onClick(); });
        var r = ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, 64), new Vector2(0, -i * 72));
        b.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.3f);
        var ic = Ui.Picture(r, "Icon", Icon(icon)); ic.color = Ui.Gold;
        ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(30, 30), new Vector2(0, -8));
        var t = Ui.Label(r, label, 10, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        t.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(60, 18), new Vector2(0, 5));
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
        refreshOpen?.Invoke();
    }

    public void OnMessage(Dictionary<string, object> m)
    {
        switch (m.Str("t"))
        {
            case "welcome":
                var login = m.Obj("login");
                if (login != null) googleOn = login.Bool("google");
                if (pendingTicket != null) { app.Send(new Dictionary<string, object> { ["t"] = "ticket", ["ticket"] = pendingTicket }); pendingTicket = null; }
                break;
            case "signedIn":
                app.Toast(Loc.T("أهلاً! اتسجلت بحساب جوجل"));
                Sfx.Coins();
                break;
            case "bonus":
                app.Toast(Loc.T("مكافأة") + " +" + Loc.Short((long)m.Num("coins")));
                Sfx.Coins();
                break;
            case "lb":
                lbRows = m.Arr("rows"); lbMe = m.Obj("me");
                if (m.Str("game") == lbGame) refreshOpen?.Invoke();
                break;
            case "adTicket":
                if (m.Bool("ssv")) { app.Toast(Loc.T("الإعلان مش جاهز، جرّب تاني")); break; }   // the server only pays AdMob's own signed calls
                StartCoroutine(PlayAd(m.Str("ticket")));
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
            Toggle(list, "الاهتزاز", Sfx.VibrateOn, v => { Sfx.VibrateOn = v; if (v) Sfx.Buzz(); });
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
            var ver = Row(list, 22);
            Ui.Label(ver, "Board Battles  ·  " + Application.version, 11, new Color(1, 1, 1, 0.4f)).rectTransform.Fill();
        };
        refreshOpen();
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
            var area = Tabs(body, new[] { "عملات", "أحجار الدومينو", "زهر", "بدون إعلانات" }, shopTab, i => { shopTab = i; refreshOpen(); });
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
                    BuyButton(c, p.egp + " " + Loc.T("ج.م"), Ui.Gold, Ui.Ink, () => app.Toast(Loc.T("الشراء بالفلوس جاي قريباً في نسخة Unity")));
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
            else
            {
                var c = ShopCard(row, 0, 1);
                c.sizeDelta = new Vector2(300, c.sizeDelta.y);
                var vi = Ui.Picture(c, "Icon", Icon("video")); vi.color = Ui.Gold; vi.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(50, 50), new Vector2(0, -16));
                Ui.Label(c, "بدون إعلانات", 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 30), new Vector2(0, -76));
                BuyButton(c, "199 " + Loc.T("ج.م") + " / " + Loc.T("شهرياً"), Ui.Gold, Ui.Ink, () => app.Toast(Loc.T("الشراء بالفلوس جاي قريباً في نسخة Unity")));
            }
        };
        refreshOpen();
    }

    bool Owns(string id) { var l = user?.Arr("skins"); return l != null && l.Contains(id); }
    string Equipped(string cat) => user?.Obj("equip")?.Str(cat);

    static RectTransform ShopCard(RectTransform row, int i, int n)
    {
        const float W = 122, Gap = 10;
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

    static readonly string[] GameIds = { "domino", "tawla", "chess", "ludo" };

    public void OpenLeaders(int tab)
    {
        lbTab = tab; lbGame = GameIds[tab]; lbRows = null; lbMe = null;
        app.Send(new Dictionary<string, object> { ["t"] = "lb", ["game"] = lbGame });
        Panel(Loc.T("المتصدرين"), out var body, new Vector2(600, 340));
        refreshOpen = () =>
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            var area = Tabs(body, new[] { "دومينو", "طاولة", "شطرنج", "لودو" }, lbTab, i => { Close(); OpenLeaders(i); });
            // the column heads
            var head = Ui.Rect("Head", area).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(560, 22), Vector2.zero);
            LbCells(head, Loc.T("الترتيب"), "", Loc.T("التقييم"), Loc.T("مكسب"), new Color(1, 1, 1, 0.5f), 12);
            var listArea = Ui.Rect("List", area); listArea.anchorMin = Vector2.zero; listArea.anchorMax = Vector2.one; listArea.offsetMin = new Vector2(0, lbMe != null ? 40 : 0); listArea.offsetMax = new Vector2(0, -24);
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
                LbCells(row, rank.ToString(), r.Str("name"), ((int)r.Num("rating")).ToString(), ((int)r.Num("wins")).ToString(),
                    rank == 1 ? Ui.Gold : Color.white, 14);
            }
            if (lbMe != null)
            {
                var mine = Ui.Box(area, "Me", new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.25f)).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 34), Vector2.zero);
                LbCells(mine, ((int)lbMe.Num("rank")).ToString(), Loc.T("مركزك") + " · " + lbMe.Str("name"), ((int)lbMe.Num("rating")).ToString(), ((int)lbMe.Num("wins")).ToString(), Ui.Gold, 14);
            }
        };
        refreshOpen();
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
        for (float t = 5f; t > 0; t -= Time.unscaledDeltaTime)
        {
            count.Set(Loc.T("المكافأة بعد") + " " + Mathf.CeilToInt(t));
            logo.rectTransform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 3) * 0.03f);
            yield return null;
        }
        app.Send(new Dictionary<string, object> { ["t"] = "adWatched", ["ticket"] = ticket });
        count.Set(Loc.T("+٥٬٠٠٠ عملة"));
        yield return new WaitForSecondsRealtime(0.9f);
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

    void OnLink(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("ahwagames://auth")) return;
        int q = url.IndexOf("ticket=", StringComparison.Ordinal);
        if (q < 0) return;
        var ticket = Uri.UnescapeDataString(url.Substring(q + 7).Split('&')[0]);
        if (app.net != null && app.net.Online) app.Send(new Dictionary<string, object> { ["t"] = "ticket", ["ticket"] = ticket });
        else pendingTicket = ticket;
    }
}
