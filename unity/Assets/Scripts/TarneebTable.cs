// Tarneeb (الطرنيب) at the table: your 13 cards at the bottom (the ones you may play stand up), the trick in the
// middle, the others' cards face down at their places, the bidding and the trump, and the score. The rules are
// the server's (shared/games/tarneeb.ts): bid 7…13 or pass, the winner names the trump, follow suit.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TarneebTable : MonoBehaviour
{
    App app;
    Canvas canvas;
    RectTransform root, handArea, trickArea, bidPanel, trumpPanel;
    readonly RectTransform[] backs = new RectTransform[4];
    readonly Text[] bubbles = new Text[4];
    Text infoText;
    int lastK = -1;
    string key = "";
    bool showingTrick;
    static readonly string[] SuitSym = { "♠", "♥", "♦", "♣" };
    static readonly string[] SuitName = { "سبيت", "كبة", "ديناري", "سباتي" };
    public static Color SuitColor(int s) => s == 1 || s == 2 ? new Color(0.78f, 0.15f, 0.18f) : new Color(0.1f, 0.1f, 0.14f);

    public void Init(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Tarneeb", 15);
        root = Ui.Rect("Tarneeb", canvas.transform).Safe();
        trickArea = Ui.Rect("Trick", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(240, 180), new Vector2(0, 26));
        handArea = Ui.Rect("Hand", root).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(520, 96), new Vector2(0, 4));
        for (int i = 1; i < 4; i++) backs[i] = Ui.Rect("Backs" + i, root);
        backs[1].Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(60, 150), new Vector2(-112, 24));
        backs[2].Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(240, 54), new Vector2(0, -56));
        backs[3].Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 150), new Vector2(122, 24));
        for (int i = 0; i < 4; i++)
        {
            var b = Ui.Box(root, "Bid" + i, new Color(0.98f, 0.93f, 0.8f, 0.95f));
            var rt = b.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(64, 26);
            bubbles[i] = Ui.Label(rt, "", 14, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold); bubbles[i].rectTransform.Fill(2);
            b.gameObject.SetActive(false);
        }
        var ib = Ui.Box(root, "Info", new Color(0.1f, 0.06f, 0.04f, 0.82f));
        var info = ib.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 80), new Vector2(12, 12));
        infoText = Ui.Label(info, "", 12, Color.white, TextAnchor.MiddleCenter, Ui.Bold); infoText.rectTransform.Fill(6);
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        // bidding: pass, or 7 … 13
        var bp = Ui.Box(root, "Bidding", new Color(0.1f, 0.06f, 0.04f, 0.92f));
        bidPanel = bp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(430, 92), new Vector2(0, 30));
        bp.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        Ui.Label(bidPanel, "هتشتري كام؟", 15, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(400, 28), new Vector2(0, -4));
        for (int n = 6; n <= 13; n++)
        {
            int bid = n == 6 ? 0 : n;
            var b = Ui.Button(bidPanel, n == 6 ? "باص" : n.ToString(), n == 6 ? Ui.Panel : Ui.Gold, n == 6 ? Ui.GoldLight : Ui.Ink, 17, () => Bid(bid));
            b.name = "B" + bid;
            ((RectTransform)b.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(n == 6 ? 64 : 44, 40), new Vector2(n == 6 ? 10 : 80 + (n - 7) * 49, 10));
        }
        bidPanel.gameObject.SetActive(false);
        // naming the trump
        var tp = Ui.Box(root, "Trump", new Color(0.1f, 0.06f, 0.04f, 0.92f));
        trumpPanel = tp.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(330, 100), new Vector2(0, 30));
        tp.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        Ui.Label(trumpPanel, "اختار الطرنيب", 15, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(300, 28), new Vector2(0, -4));
        for (int s = 0; s < 4; s++)
        {
            int suit = s;
            var b = Ui.Button(trumpPanel, SuitSym[s], new Color(0.98f, 0.95f, 0.88f), SuitColor(s), 30, () => Trump(suit));
            ((RectTransform)b.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(66, 56), new Vector2(15 + s * 78, 10));
        }
        trumpPanel.gameObject.SetActive(false);
        canvas.gameObject.SetActive(false);
    }

    public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); key = ""; lastK = -1; }

    /// Where a seat sits on the screen, from your point of view (0 you, 1 right, 2 across, 3 left).
    static int Rel(TableView v, int seat) { int y = v.you >= 0 ? v.you : 0; return ((seat - y) % 4 + 4) % 4; }
    Vector2 SeatPoint(int rel)
    {
        var half = root.rect.size / 2;
        switch (rel) { case 0: return new Vector2(0, -half.y + 60); case 1: return new Vector2(half.x - 110, 24); case 2: return new Vector2(0, half.y - 80); default: return new Vector2(-half.x + 110, 24); }
    }
    static Vector2 TrickSpot(int rel) => rel == 0 ? new Vector2(0, -44) : rel == 1 ? new Vector2(64, 0) : rel == 2 ? new Vector2(0, 44) : new Vector2(-64, 0);
    Vector2 TrickCenter => trickArea.anchoredPosition;

    public void Render(TableView v)
    {
        var t = v.tarneeb;
        if (t == null) { Hide(); return; }
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        var hand = TableView.IntList(t.Arr("hand")); var legal = TableView.IntList(t.Arr("legal")); var counts = TableView.IntList(t.Arr("counts"));
        string phase = t.Str("phase"); int turn = (int)t.Num("turn", -1), trump = (int)t.Num("trump", -1), bid = (int)t.Num("bid"), bidder = (int)t.Num("bidder", -1);
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
            if (prev >= 0 && last.t == "bid") Sfx.Tap();
            if (prev >= 0 && last.t == "trump") { Sfx.Chime(); app.Toast(Loc.T("الطرنيب") + ": " + SuitSym[Mathf.Clamp(last.suit, 0, 3)] + " " + Loc.T(SuitName[Mathf.Clamp(last.suit, 0, 3)]), 2.6f); }
        }
        string k = string.Join(",", hand) + "|" + string.Join(",", legal) + "|" + string.Join(",", counts) + "|" + phase + "|" + turn + "|" + v.v + "|" + showingTrick;
        if (k == key) return;
        key = k;
        // the bidding and the trump, for you when it's your turn
        bidPanel.gameObject.SetActive(myTurn && phase == "bid");
        if (myTurn && phase == "bid")
        {
            int passes = 0; foreach (var b in t.Arr("bids") ?? new List<object>()) if (b is double d && d == 0) passes++;
            foreach (Transform c in bidPanel)
            {
                if (!c.name.StartsWith("B")) continue;
                int n = int.Parse(c.name.Substring(1));
                bool ok = n == 0 ? !(passes == 3 && bidder < 0) : n > bid;
                c.GetComponent<Button>().interactable = ok;
                c.GetComponent<Image>().color = ok ? (n == 0 ? Ui.Panel : Ui.Gold) : new Color(0.35f, 0.3f, 0.25f, 0.6f);
            }
        }
        trumpPanel.gameObject.SetActive(myTurn && phase == "trump");
        // the bids so far, by each player
        var bids = t.Arr("bids");
        for (int s = 0; s < 4; s++)
        {
            int rel = Rel(v, s);
            var bub = bubbles[rel];
            object bo = bids != null && s < bids.Count ? bids[s] : null;
            bool show = phase == "bid" && bo is double;
            bool bidderTag = phase != "bid" && s == bidder;
            bub.transform.parent.gameObject.SetActive(show || bidderTag);
            if (show) bub.Set((double)bo == 0 ? Loc.T("باص") : ((int)(double)bo).ToString());
            else if (bidderTag) bub.Set(Loc.T("اشترى") + " " + bid.ToString() + (trump >= 0 ? " " + SuitSym[trump] : ""));
            if (bidderTag && trump >= 0) bub.color = SuitColor(trump); else bub.color = Ui.Ink;
            var p = SeatPoint(rel);
            ((RectTransform)bub.transform.parent).anchoredPosition = rel == 0 ? new Vector2(0, -root.rect.height / 2 + 118) : p + (rel == 2 ? new Vector2(150, 0) : new Vector2(0, 98));
            ((RectTransform)bub.transform.parent).sizeDelta = new Vector2(bidderTag ? 92 : 56, 24);
        }
        // the trick in the middle
        foreach (Transform c in trickArea) Destroy(c.gameObject);
        if (!showingTrick)
            foreach (var o in t.Arr("trick") ?? new List<object>())
            {
                int seat = (int)o.Num("seat"), card = (int)o.Num("card");
                var img = Ui.Picture(trickArea, "Card", BasraTable.Face(card)).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(52, 73);
                img.anchoredPosition = TrickSpot(Rel(v, seat));
            }
        // your hand: the cards you may play stand up
        foreach (Transform c in handArea) Destroy(c.gameObject);
        const float W = 56, H = 78; float step = Mathf.Min(38, (430 - W) / Mathf.Max(1, hand.Count - 1));   // clear of the score box on the left
        for (int i = 0; i < hand.Count; i++)
        {
            int id = hand[i]; bool can = myTurn && phase == "play" && legal.Contains(id);
            var btn = Ui.Button(handArea, "", new Color(0, 0, 0, 0), Color.white, 10, () => Play(id));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(W, H);
            rt.anchoredPosition = new Vector2((i - (hand.Count - 1) / 2f) * step, H / 2 + (can ? 14 : 2));
            var img = Ui.Picture(rt, "Face", BasraTable.Face(id)); img.rectTransform.Fill();
            if (myTurn && phase == "play" && !can) img.color = new Color(0.62f, 0.62f, 0.62f);
        }
        // the others' cards, face down
        for (int i = 1; i < 4; i++) foreach (Transform c in backs[i]) Destroy(c.gameObject);
        for (int s = 0; s < 4 && s < counts.Count; s++)
        {
            if (s == v.you) continue;
            int rel = Rel(v, s); if (rel == 0) continue;
            bool side = rel != 2; int n = counts[s];
            for (int i = 0; i < n; i++)
            {
                var img = Ui.Picture(backs[rel], "Back", BasraTable.Face(-1)).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(30, 42);
                img.anchoredPosition = side ? new Vector2(0, (i - (n - 1) / 2f) * 8) : new Vector2((i - (n - 1) / 2f) * 12, 0);
                img.localRotation = Quaternion.Euler(0, 0, side ? 90 : 0);
            }
        }
        // the score: tricks this deal and points over the deals, for your team and theirs
        var tricks = TableView.IntList(t.Arr("tricks")); var scores = TableView.IntList(t.Arr("scores"));
        int us = (v.you >= 0 ? v.you : 0) % 2, them = 1 - us;
        string Pair(List<int> x) => (x.Count > us ? x[us] : 0) + " – " + (x.Count > them ? x[them] : 0);
        infoText.Set(Loc.T("النقط") + ": " + Pair(scores) + "  (" + Loc.T("لـ") + " " + (int)t.Num("target") + ")\n"
            + Loc.T("اللمّات") + ": " + Pair(tricks) + "\n"
            + (trump >= 0 ? Loc.T("الطرنيب") + ": " + SuitSym[trump] + "  ·  " + Loc.T("المشترى") + " " + bid : Loc.T("دور") + " " + (int)t.Num("deal")));
    }

    void Bid(int n) { if (!(app.view?.MyTurn ?? false)) return; Sfx.Tap(); Act(new Dictionary<string, object> { ["t"] = "bid", ["n"] = n }); }
    void Trump(int s) { if (!(app.view?.MyTurn ?? false)) return; Sfx.Tap(); Act(new Dictionary<string, object> { ["t"] = "trump", ["suit"] = s }); }
    void Play(int id)
    {
        var v = app.view; var t = v?.tarneeb;
        if (t == null || !v.MyTurn || t.Str("phase") != "play") { Sfx.Tap(); return; }
        if (!TableView.IntList(t.Arr("legal")).Contains(id)) { Sfx.Tap(); app.Toast(Loc.T("لازم تنزل من نفس النوع")); return; }
        Sfx.Tap();
        Act(new Dictionary<string, object> { ["t"] = "card", ["id"] = id });
    }
    void Act(Dictionary<string, object> a) => app.Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });

    IEnumerator PlayAnim(TableView v, LastAction last)
    {
        int rel = Rel(v, last.s);
        if (rel == 0) { Sfx.Clack(); yield break; }   // yours appears at once
        var fly = Ui.Picture(root, "Fly", BasraTable.Face(last.card)).rectTransform;
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
        showingTrick = true; key = "";
        var cards = new List<RectTransform>();
        foreach (var o in lt)
        {
            int seat = (int)o.Num("seat"), card = (int)o.Num("card");
            var img = Ui.Picture(root, "Trick", BasraTable.Face(card)).rectTransform;
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
        if (app.view != null && app.view.tarneeb != null) Render(app.view);
        var d = app.view?.tarneeb?.Obj("lastDeal");
        if (v.last != null && v.last.deal && d != null) ShowDeal(app.view, d);
    }

    void ShowDeal(TableView v, Dictionary<string, object> d)
    {
        var pts = TableView.IntList(d.Arr("points")); var tr = TableView.IntList(d.Arr("tricks"));
        int us = (v.you >= 0 ? v.you : 0) % 2, them = 1 - us; if (pts.Count < 2) return;
        bool ourBid = (int)d.Num("bidder") % 2 == us, made = d.Bool("made");
        string head = ourBid ? (made ? Loc.T("جبتوا اللي اشتريتوه!") : Loc.T("ماجبتوش اللي اشتريتوه")) : (made ? Loc.T("هم جابوا اللي اشتروه") : Loc.T("كسرتوهم!"));
        app.Toast(head + "  " + Loc.T("إنتو") + " " + tr[us] + " (" + (pts[us] >= 0 ? "+" : "") + pts[us] + ")  ·  " + Loc.T("هم") + " " + tr[them] + " (" + (pts[them] >= 0 ? "+" : "") + pts[them] + ")", 4.5f);
    }
}
