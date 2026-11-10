// Estimation (إستيميشن) at the table: your 13 cards at the bottom (the ones you may play stand up), the trick in
// the middle, the others' cards face down at their places, each player's call, then what they said they'd take
// and what they've taken, and the scores. The rules are the server's (shared/games/estimation.ts):
//  1. the call: 4 … 13 tricks with a trump (♣ ♦ ♥ ♠, then صن: no trump), higher each time, or pass;
//  2. the others say how many tricks they'll take (the four can't add up to 13);
//  3. the play: follow suit; the highest trump, else the highest of the suit led, takes the trick.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EstimationTable : MonoBehaviour
{
    App app;
    Canvas canvas;
    RectTransform root, handArea, trickArea, callPanel, estPanel;
    readonly RectTransform[] backs = new RectTransform[4];
    readonly Image[] bubbleBg = new Image[4];
    readonly Text[] bubbles = new Text[4];
    Text infoText, callTitle, estTitle, estNote;
    Button callGo;
    readonly Dictionary<int, Button> numButtons = new Dictionary<int, Button>(), suitButtons = new Dictionary<int, Button>(), estButtons = new Dictionary<int, Button>();
    int lastK = -1;
    string key = "";
    bool showingTrick;
    int selN = -1, selSuit = -1;
    const int SANS = 4;
    static readonly string[] TrumpSym = { "♠", "♥", "♦", "♣", "صن" };
    static readonly Color Dim = new Color(0.35f, 0.3f, 0.25f, 0.6f);
    static int TrumpRank(int suit) => suit == SANS ? 4 : 3 - suit;
    static bool Beats(int n, int suit, int bid, int cur) => n > bid || (n == bid && cur >= 0 && TrumpRank(suit) > TrumpRank(cur));
    static string Sym(int suit) => suit >= 0 && suit <= SANS ? TrumpSym[suit] : "";
    static Color SymColor(int suit) => suit == SANS ? Ui.Ink : CardArt.SuitColor(suit);

    public void Init(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Estimation", 15);
        root = Ui.Rect("Estimation", canvas.transform).Safe();
        CardArt.Cloth(root);
        trickArea = Ui.Rect("Trick", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240, 180), new Vector2(0, 26));
        handArea = Ui.Rect("Hand", root).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(520, 96), new Vector2(0, 4));
        for (int i = 1; i < 4; i++) backs[i] = Ui.Rect("Backs" + i, root);
        backs[1].Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(60, 150), new Vector2(-112, 24));
        backs[2].Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(240, 54), new Vector2(0, -56));
        backs[3].Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 150), new Vector2(122, 24));
        for (int i = 0; i < 4; i++)
        {
            var b = Ui.Box(root, "Said" + i, new Color(0.98f, 0.93f, 0.8f, 0.95f));
            var rt = b.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(104, 26);
            bubbleBg[i] = b;
            bubbles[i] = Ui.Label(rt, "", 13, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold); bubbles[i].rectTransform.Fill(2);
            b.raycastTarget = false; bubbles[i].raycastTarget = false;
            b.gameObject.SetActive(false);
        }
        // the scores, bottom left (clear of your hand)
        var ib = Ui.Box(root, "Info", new Color(0.1f, 0.06f, 0.04f, 0.84f));
        var info = ib.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(186, 104), new Vector2(10, 10));
        ib.raycastTarget = false;
        infoText = Ui.Label(info, "", 11, Color.white, TextAnchor.MiddleCenter, Ui.Bold); infoText.rectTransform.Fill(5);
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap; infoText.raycastTarget = false;

        // the call: how many tricks (4 … 13), with which trump, or pass
        var cp = Ui.Box(root, "Call", new Color(0.1f, 0.06f, 0.04f, 0.94f));
        callPanel = cp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(476, 176), new Vector2(0, 34));
        cp.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        callTitle = Ui.Label(callPanel, "هتطلب كام لمّة، وإيه الطرنيب؟", 15, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        callTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(450, 26), new Vector2(0, -4));
        for (int n = 4; n <= 13; n++)
        {
            int k = n;
            var b = Ui.Button(callPanel, n.ToString(), Ui.Gold, Ui.Ink, 17, () => { selN = k; Sfx.Tap(); key = ""; Render(app.view); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, 36), new Vector2((n - 8.5f) * 45, -34));
            numButtons[n] = b;
        }
        for (int s = 0; s <= SANS; s++)
        {
            int k = s;
            // low to high, left to right: ♣ ♦ ♥ ♠ صن
            int col = TrumpRank(s);
            var b = Ui.Button(callPanel, TrumpSym[s], new Color(0.98f, 0.95f, 0.88f), SymColor(s), s == SANS ? 17 : 26, () => { selSuit = k; Sfx.Tap(); key = ""; Render(app.view); });
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(58, 44), new Vector2((col - 2) * 66, -78));
            suitButtons[s] = b;
        }
        var pass = Ui.Button(callPanel, "باص", Ui.Panel, Ui.GoldLight, 17, () => Act(new Dictionary<string, object> { ["t"] = "call", ["n"] = 0 }));
        ((RectTransform)pass.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 38), new Vector2(-80, 8));
        callGo = Ui.Button(callPanel, "اطلب", Ui.Gold, Ui.Ink, 17, () =>
        {
            if (selN < 4 || selSuit < 0) return;
            Act(new Dictionary<string, object> { ["t"] = "call", ["n"] = selN, ["suit"] = selSuit });
        });
        ((RectTransform)callGo.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(150, 38), new Vector2(70, 8));
        callPanel.gameObject.SetActive(false);

        // the estimate: 0 … the caller's number
        var ep = Ui.Box(root, "Estimate", new Color(0.1f, 0.06f, 0.04f, 0.94f));
        estPanel = ep.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 116), new Vector2(0, 34));
        ep.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        estTitle = Ui.Label(estPanel, "هتاخد كام لمّة؟", 15, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        estTitle.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(540, 26), new Vector2(0, -4));
        for (int n = 0; n <= 13; n++)
        {
            int k = n;
            var b = Ui.Button(estPanel, n.ToString(), Ui.Gold, Ui.Ink, 16, () => Act(new Dictionary<string, object> { ["t"] = "est", ["n"] = k }));
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(34, 36), new Vector2((n - 6.5f) * 38, -34));
            estButtons[n] = b;
        }
        estNote = Ui.Label(estPanel, "", 12, Ui.GoldLight, TextAnchor.MiddleCenter);
        estNote.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(540, 24), new Vector2(0, 8));
        estPanel.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }

    public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); key = ""; lastK = -1; showingTrick = false; }

    /// Where a seat sits on the screen, from your point of view (0 you, 1 right, 2 across, 3 left).
    static int Rel(TableView v, int seat) { int y = v.you >= 0 ? v.you : 0; return ((seat - y) % 4 + 4) % 4; }
    Vector2 SeatPoint(int rel)
    {
        var half = root.rect.size / 2;
        switch (rel) { case 0: return new Vector2(0, -half.y + 60); case 1: return new Vector2(half.x - 110, 24); case 2: return new Vector2(0, half.y - 80); default: return new Vector2(-half.x + 110, 24); }
    }
    static Vector2 TrickSpot(int rel) => rel == 0 ? new Vector2(0, -44) : rel == 1 ? new Vector2(64, 0) : rel == 2 ? new Vector2(0, 44) : new Vector2(-64, 0);
    Vector2 TrickCenter => trickArea.anchoredPosition;
    static int? Said(object o) => o is double d ? (int)d : (int?)null;
    string Name(TableView v, int s)
    {
        if (s == v.you) return Loc.T("إنت");
        var n = s >= 0 && s < v.seats.Count ? v.seats[s].name ?? "" : "";
        return n.Length > 9 ? n.Substring(0, 9) : n;
    }

    public void Render(TableView v)
    {
        var t = v?.estimation;
        if (t == null) { Hide(); return; }
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        var hand = TableView.IntList(t.Arr("hand")); var legal = TableView.IntList(t.Arr("legal")); var counts = TableView.IntList(t.Arr("counts"));
        var estOk = TableView.IntList(t.Arr("estOptions"));
        string phase = t.Str("phase");
        int turn = (int)t.Num("turn", -1), suit = (int)t.Num("suit", -1), bid = (int)t.Num("bid"), caller = (int)t.Num("caller", -1);
        bool myTurn = v.MyTurn && v.phase == "play";
        var last = v.last;
        if (last != null && last.k != lastK)
        {
            int prev = lastK; lastK = last.k;
            if (prev >= 0 && last.t == "card")
            {
                if (last.trick >= 0) StartCoroutine(TrickAnim(v, t));
                else StartCoroutine(PlayAnim(v, last));
            }
            if (prev >= 0 && (last.t == "call" || last.t == "est")) Sfx.Tap();
            if (prev >= 0 && last.t == "call" && last.redeal) app.Toast(Loc.T("الكل قال باص: توزيع جديد"), 2.6f);
            else if (prev >= 0 && last.t == "call" && phase == "est" && caller >= 0)
            {
                Sfx.Chime();
                app.Toast(Loc.T("الطلب") + ": " + bid + " " + (suit == SANS ? Loc.T("صن") : Sym(suit) + " " + Loc.T(CardArt.SuitName[Mathf.Clamp(suit, 0, 3)])) + "  ·  " + Name(v, caller), 2.8f);
            }
        }
        string k = string.Join(",", hand) + "|" + string.Join(",", legal) + "|" + string.Join(",", counts) + "|" + phase + "|" + turn + "|" + v.v + "|" + showingTrick + "|" + selN + "|" + selSuit + "|" + root.rect.size;
        if (k == key) return;
        key = k;

        // the call, for you when it's your turn
        bool calling = myTurn && phase == "call";
        callPanel.gameObject.SetActive(calling);
        if (calling)
        {
            int minN = Mathf.Max(4, bid);
            bool canSame = bid >= 4 && suit != SANS;   // as many tricks, with a higher trump
            if (selN < 0 || !(selN > bid || (selN == bid && canSame))) selN = bid >= 4 && canSame ? bid : Mathf.Max(minN, bid + 1);
            if (selN > 13) selN = 13;
            if (selSuit < 0 || !Beats(selN, selSuit, bid, suit)) { selSuit = -1; for (int s = 0; s <= SANS; s++) if (Beats(selN, s, bid, suit)) { selSuit = s; break; } }
            foreach (var kv in numButtons)
            {
                bool ok = kv.Key > bid || (kv.Key == bid && canSame);
                kv.Value.interactable = ok;
                kv.Value.GetComponent<Image>().color = !ok ? Dim : kv.Key == selN ? Color.white : Ui.Gold;
            }
            foreach (var kv in suitButtons)
            {
                bool ok = Beats(selN, kv.Key, bid, suit);
                kv.Value.interactable = ok;
                kv.Value.GetComponent<Image>().color = !ok ? Dim : kv.Key == selSuit ? Ui.Gold : new Color(0.98f, 0.95f, 0.88f);
            }
            bool go = selN >= 4 && selSuit >= 0 && Beats(selN, selSuit, bid, suit);
            callGo.interactable = go;
            callGo.GetComponentInChildren<Text>().Set(go ? Loc.T("اطلب") + " " + selN + " " + Sym(selSuit) : Loc.T("اطلب"));
            callTitle.Set(bid >= 4 ? Loc.T("الطلب دلوقتي") + " " + bid + " " + Sym(suit) + " · " + Loc.T("اطلب أعلى أو قول باص") : Loc.T("هتطلب كام لمّة، وإيه الطرنيب؟"));
        }
        else { selN = -1; selSuit = -1; }

        // the estimate
        bool estimating = myTurn && phase == "est";
        estPanel.gameObject.SetActive(estimating);
        if (estimating)
        {
            int said = 0, left = 0;
            foreach (var o in t.Arr("ests") ?? new List<object>()) { var x = Said(o); if (x.HasValue) said += x.Value; else left++; }
            foreach (var kv in estButtons)
            {
                bool shown = kv.Key <= bid, ok = estOk.Contains(kv.Key);
                kv.Value.gameObject.SetActive(shown);
                kv.Value.interactable = ok;
                kv.Value.GetComponent<Image>().color = ok ? Ui.Gold : Dim;
                ((RectTransform)kv.Value.transform).anchoredPosition = new Vector2((kv.Key - bid / 2f) * 38, -34);
            }
            estTitle.Set(Loc.T("هتاخد كام لمّة؟") + "  (" + Loc.T("الطلب") + " " + bid + " " + Sym(suit) + ")");
            estNote.Set(Loc.T("مجموع اللي اتقال") + ": " + said + (left == 1 ? "  ·  " + Loc.T("إنت الأخير: المجموع ماينفعش يبقى ١٣") : ""));
        }

        // what each player called, then said and took
        var calls = t.Arr("calls"); var ests = t.Arr("ests"); var tricks = TableView.IntList(t.Arr("tricks"));
        for (int s = 0; s < 4; s++)
        {
            int rel = Rel(v, s);
            var bub = bubbles[rel]; var bg = bubbleBg[rel];
            string text = null;
            if (phase == "call")
            {
                object c = calls != null && s < calls.Count ? calls[s] : null;
                if (c is double d && d == 0) text = Loc.T("باص");
                else if (c is Dictionary<string, object> cd) text = (int)cd.Num("n") + " " + Sym((int)cd.Num("suit", -1));
            }
            else
            {
                var x = ests != null && s < ests.Count ? Said(ests[s]) : null;
                text = (s == caller ? "★ " : "") + Loc.T("قال") + " " + (x.HasValue ? x.Value.ToString() : "…");
                if (phase == "play") text += " · " + Loc.T("أخد") + " " + (s < tricks.Count ? tricks[s] : 0);
            }
            bg.gameObject.SetActive(text != null);
            if (text == null) continue;
            bub.Set(text);
            bool mine = s == caller && phase != "call";
            bg.color = mine ? Ui.Gold : new Color(0.98f, 0.93f, 0.8f, 0.95f);
            var p = SeatPoint(rel);
            var brt = bg.rectTransform;
            brt.anchoredPosition = rel == 0 ? new Vector2(0, -root.rect.height / 2 + 118) : p + (rel == 2 ? new Vector2(170, 0) : new Vector2(0, 98));
            brt.sizeDelta = new Vector2(phase == "play" ? 118 : 84, 24);
        }

        // the trick in the middle
        foreach (Transform c in trickArea) Destroy(c.gameObject);
        if (!showingTrick)
            foreach (var o in t.Arr("trick") ?? new List<object>())
            {
                int seat = (int)o.Num("seat"), card = (int)o.Num("card");
                var img = Ui.Picture(trickArea, "Card", CardArt.Face(card)).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(52, 73);
                img.anchoredPosition = TrickSpot(Rel(v, seat));
            }

        // your hand: the cards you may play stand up
        foreach (Transform c in handArea) Destroy(c.gameObject);
        const float W = 56, H = 78; float step = Mathf.Min(38, (420 - W) / Mathf.Max(1, hand.Count - 1));   // clear of the score box on the left
        bool playing = myTurn && phase == "play";
        for (int i = 0; i < hand.Count; i++)
        {
            int id = hand[i]; bool can = playing && legal.Contains(id);
            var btn = Ui.Button(handArea, "", new Color(0, 0, 0, 0), Color.white, 10, () => Play(id));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(W, H);
            rt.anchoredPosition = new Vector2((i - (hand.Count - 1) / 2f) * step, H / 2 + (can ? 14 : 2));
            var img = Ui.Picture(rt, "Face", CardArt.Face(id)); img.rectTransform.Fill();
            img.raycastTarget = false;
            if (playing && !can) img.color = new Color(0.62f, 0.62f, 0.62f);
        }

        // the others' cards, face down
        for (int i = 1; i < 4; i++) foreach (Transform c in backs[i]) Destroy(c.gameObject);
        for (int s = 0; s < 4 && s < counts.Count; s++)
        {
            int rel = Rel(v, s); if (rel == 0) continue;
            bool side = rel != 2; int n = counts[s];
            for (int i = 0; i < n; i++)
            {
                var img = Ui.Picture(backs[rel], "Back", CardArt.Face(-1)).rectTransform;
                img.GetComponent<RawImage>().raycastTarget = false;
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(30, 42);
                img.anchoredPosition = side ? new Vector2(0, (i - (n - 1) / 2f) * 8) : new Vector2((i - (n - 1) / 2f) * 12, 0);
                img.localRotation = Quaternion.Euler(0, 0, side ? 90 : 0);
            }
        }

        // the scores: this deal's call, then everyone's points (you first)
        var scores = TableView.IntList(t.Arr("scores"));
        int deal = (int)t.Num("deal", 1), deals = (int)t.Num("deals", 5);
        var sb = new System.Text.StringBuilder();
        sb.Append(deal <= deals ? Loc.T("دور") + " " + deal + "/" + deals : Loc.T("دور زيادة للتعادل"));
        if (phase != "call" && caller >= 0) sb.Append("  ·  " + bid + " " + Sym(suit));
        for (int r = 0; r < 4; r++)
        {
            int s = ((v.you >= 0 ? v.you : 0) + r) % 4;
            sb.Append("\n" + Name(v, s) + ": " + (s < scores.Count ? scores[s] : 0));
        }
        infoText.Set(sb.ToString());
    }

    void Play(int id)
    {
        var v = app.view; var t = v?.estimation;
        if (t == null || !v.MyTurn || t.Str("phase") != "play") { Sfx.Tap(); return; }
        if (!TableView.IntList(t.Arr("legal")).Contains(id)) { Sfx.Tap(); app.Toast(Loc.T("لازم تنزل من نفس النوع")); return; }
        Act(new Dictionary<string, object> { ["t"] = "card", ["id"] = id });
    }
    void Act(Dictionary<string, object> a)
    {
        if (!(app.view?.MyTurn ?? false)) return;
        Sfx.Tap();
        app.Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });
    }

    IEnumerator PlayAnim(TableView v, LastAction last)
    {
        int rel = Rel(v, last.s);
        if (rel == 0) { Sfx.Clack(); yield break; }   // yours appears at once
        var fly = Ui.Picture(root, "Fly", CardArt.Face(last.card)).rectTransform;
        fly.anchorMin = fly.anchorMax = new Vector2(0.5f, 0.5f); fly.sizeDelta = new Vector2(52, 73);
        Vector2 from = SeatPoint(rel), to = TrickCenter + TrickSpot(rel);
        for (float x = 0; x < 0.22f; x += Time.deltaTime) { if (fly == null) yield break; fly.anchoredPosition = Vector2.Lerp(from, to, Ui.Back(x / 0.22f)); yield return null; }
        Sfx.Clack();
        if (fly != null) Destroy(fly.gameObject);
    }

    /// The fourth card: the whole trick shows for a moment, then goes to whoever took it.
    IEnumerator TrickAnim(TableView v, Dictionary<string, object> t)
    {
        var lt = t.Arr("lastTrick"); if (lt == null) yield break;
        int winner = (int)t.Num("lastWinner", -1);
        bool dealOver = v.last != null && v.last.deal;
        showingTrick = true; key = "";
        var cards = new List<RectTransform>();
        foreach (var o in lt)
        {
            int seat = (int)o.Num("seat"), card = (int)o.Num("card");
            var img = Ui.Picture(root, "Trick", CardArt.Face(card)).rectTransform;
            img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(52, 73);
            img.anchoredPosition = TrickCenter + TrickSpot(Rel(v, seat));
            cards.Add(img);
        }
        Sfx.Clack();
        yield return new WaitForSeconds(0.9f);
        var to = SeatPoint(Rel(v, winner));
        var start = new List<Vector2>(); foreach (var c in cards) start.Add(c.anchoredPosition);
        for (float x = 0; x < 0.3f; x += Time.deltaTime)
        {
            for (int i = 0; i < cards.Count; i++) if (cards[i] != null) { cards[i].anchoredPosition = Vector2.Lerp(start[i], to, x / 0.3f); cards[i].localScale = Vector3.one * Mathf.Lerp(1, 0.5f, x / 0.3f); }
            yield return null;
        }
        foreach (var c in cards) if (c != null) Destroy(c.gameObject);
        Sfx.Slap();
        showingTrick = false; key = "";
        if (app.view != null && app.view.estimation != null) Render(app.view);
        var d = app.view?.estimation?.Obj("lastDeal");
        if (dealOver && d != null) ShowDeal(app.view, d);
    }

    /// The deal is over: what you said, what you took, and your points.
    void ShowDeal(TableView v, Dictionary<string, object> d)
    {
        var pts = TableView.IntList(d.Arr("points")); var tr = TableView.IntList(d.Arr("tricks")); var es = TableView.IntList(d.Arr("ests"));
        int me = v.you >= 0 ? v.you : 0;
        if (pts.Count < 4 || tr.Count < 4 || es.Count < 4) return;
        bool made = tr[me] == es[me];
        string head = made ? Loc.T("جبت اللي قلته!") : Loc.T("ماجبتش اللي قلته");
        if (made) Sfx.Chime();
        app.Toast(head + "  " + Loc.T("قلت") + " " + es[me] + " · " + Loc.T("أخدت") + " " + tr[me] + "  (" + (pts[me] >= 0 ? "+" : "") + pts[me] + ")", 4.5f);
    }
}
