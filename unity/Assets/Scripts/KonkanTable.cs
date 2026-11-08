// Konkan (الكونكان) at the table: your cards at the bottom (tap to pick them), the pack and the thrown pile in the
// middle, everyone's laid-down melds above, the others' cards face down. A turn: tap the pack to draw (or the pile
// to take the top card), pick cards and make groups, lay them down (51 the first time), add to melds on the table,
// then pick one card and throw it. The server keeps the rules (shared/games/konkan.ts); the checks here are only
// so the buttons can say what's possible.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class KonkanTable : MonoBehaviour
{
    App app;
    Canvas canvas;
    RectTransform root, handArea, meldArea, stageArea, pileArea, buttons;
    readonly RectTransform[] backs = new RectTransform[4];
    readonly Text[] tags = new Text[4];
    Text infoText, stageText;
    Button groupBtn, downBtn, throwBtn, backBtn, sortBtn;
    int lastK = -1;
    string key = "";
    readonly List<int> selected = new List<int>();
    readonly List<List<int>> staged = new List<List<int>>();   // groups made, waiting to go down together
    readonly List<int> order = new List<int>();                // your cards in the order you hold them
    bool bySuit = true;
    int deal = -1;

    public void Init(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Konkan", 15);
        root = Ui.Rect("Konkan", canvas.transform).Safe();
        handArea = Ui.Rect("Hand", root).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(560, 84), new Vector2(0, 2));
        stageArea = Ui.Rect("Staged", root).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(520, 46), new Vector2(0, 92));
        stageText = Ui.Label(root, "", 12, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        stageText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(400, 18), new Vector2(0, 140));
        meldArea = Ui.Rect("Melds", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 150), new Vector2(95, 40));
        pileArea = Ui.Rect("Piles", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(130, 80), new Vector2(-185, 40));
        for (int i = 1; i < 4; i++) backs[i] = Ui.Rect("Backs" + i, root);
        backs[1].Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(50, 120), new Vector2(-112, 30));
        backs[2].Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 46), new Vector2(0, -54));
        backs[3].Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(50, 120), new Vector2(120, 30));
        for (int i = 0; i < 4; i++)
        {
            tags[i] = Ui.Label(root, "", 11, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
            tags[i].rectTransform.anchorMin = tags[i].rectTransform.anchorMax = new Vector2(0.5f, 0.5f); tags[i].rectTransform.sizeDelta = new Vector2(110, 18);
            tags[i].gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.9f);
        }
        var ib = Ui.Box(root, "Info", new Color(0.1f, 0.06f, 0.04f, 0.82f));
        var info = ib.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(132, 92), new Vector2(8, 8));
        infoText = Ui.Label(info, "", 11, Color.white, TextAnchor.MiddleCenter, Ui.Bold); infoText.rectTransform.Fill(5);
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        // the buttons, down the right of your cards
        buttons = Ui.Rect("Buttons", root).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(96, 196), new Vector2(-8, 8));
        Button Btn(string label, Color bg, Color fg, int i, System.Action on)
        {
            var b = Ui.Button(buttons, label, bg, fg, 14, on);
            ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(96, 34), new Vector2(0, i * 39));
            return b;
        }
        throwBtn = Btn("ارمي", Ui.Gold, Ui.Ink, 0, Throw);
        downBtn = Btn("نزّل", new Color(0.35f, 0.6f, 0.3f), Color.white, 1, Down);
        groupBtn = Btn("مجموعة", Ui.Panel, Ui.GoldLight, 2, Group);
        backBtn = Btn("رجّع الورقة", Ui.Panel, Ui.GoldLight, 3, Back);
        sortBtn = Btn("رتّب", Ui.Panel, Ui.GoldLight, 4, () => { Sfx.Tap(); bySuit = !bySuit; Sort(); key = ""; if (app.view != null) Render(app.view); });
        canvas.gameObject.SetActive(false);
    }

    public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); key = ""; lastK = -1; selected.Clear(); staged.Clear(); order.Clear(); deal = -1; }

    int Rel(TableView v, int seat)
    {
        int n = v.seats.Count, y = v.you >= 0 ? v.you : 0; int r = ((seat - y) % n + n) % n;
        if (n == 2 && r == 1) return 2;
        if (n == 3 && r == 2) return 3;
        return r;
    }
    Vector2 SeatPoint(int rel)
    {
        var half = root.rect.size / 2;
        switch (rel) { case 0: return new Vector2(0, -half.y + 50); case 1: return new Vector2(half.x - 100, 30); case 2: return new Vector2(0, half.y - 70); default: return new Vector2(-half.x + 100, 30); }
    }

    /* ---------- the rules the buttons need (the same as the server's) ---------- */
    public static bool IsJoker(int id) => id >= 104;
    public static int RankOf(int id) => IsJoker(id) ? 0 : id % 13 + 1;
    public static int SuitOf(int id) => IsJoker(id) ? -1 : (id % 52) / 13;
    public static int FaceOf(int id) => IsJoker(id) ? 52 : id % 52;

    static bool IsSet(List<int> cards)
    {
        var real = cards.Where(c => !IsJoker(c)).ToList();
        if (cards.Count < 3 || cards.Count > 4 || real.Count == 0) return false;
        if (real.Any(c => RankOf(c) != RankOf(real[0]))) return false;
        return real.Select(SuitOf).Distinct().Count() == real.Count;
    }
    /// The lowest place and length of a run (ace low 1 or high 14), or len 0 when it isn't one.
    static (int lo, int len) RunShape(List<int> cards)
    {
        var real = cards.Where(c => !IsJoker(c)).ToList(); int jokers = cards.Count - real.Count;
        if (cards.Count < 3 || real.Count == 0) return (0, 0);
        int suit = SuitOf(real[0]); if (real.Any(c => SuitOf(c) != suit)) return (0, 0);
        foreach (bool high in new[] { false, true })
        {
            var vals = real.Select(c => RankOf(c) == 1 && high ? 14 : RankOf(c)).OrderBy(x => x).ToList();
            if (vals.Distinct().Count() != vals.Count) continue;
            int span = vals[vals.Count - 1] - vals[0] + 1, gaps = span - vals.Count;
            if (gaps > jokers) continue;
            int lo = vals[0], len = span + (jokers - gaps), hi = lo + len - 1, top = high ? 14 : 13;
            if (hi > top) { lo -= hi - top; hi = top; }
            if (lo < (high ? 2 : 1)) continue;
            return (lo, len);
        }
        return (0, 0);
    }
    public static string Kind(List<int> cards) => IsSet(cards) ? "set" : RunShape(cards).len > 0 ? "run" : null;
    public static int Value(List<int> cards)
    {
        int V(int r) => r == 1 || r == 14 ? 11 : r >= 10 ? 10 : r;
        if (IsSet(cards)) return V(RankOf(cards.First(c => !IsJoker(c)))) * cards.Count;
        var (lo, len) = RunShape(cards); int t = 0;
        for (int r = lo; r < lo + len; r++) t += r == 1 ? 1 : V(r);
        return t;
    }

    void Sort()
    {
        order.Sort((a, b) =>
        {
            if (IsJoker(a) != IsJoker(b)) return IsJoker(a) ? 1 : -1;
            return bySuit ? (SuitOf(a) * 20 + RankOf(a)).CompareTo(SuitOf(b) * 20 + RankOf(b)) : (RankOf(a) * 10 + SuitOf(a)).CompareTo(RankOf(b) * 10 + SuitOf(b));
        });
    }

    public void Render(TableView v)
    {
        var k = v.konkan;
        if (k == null) { Hide(); return; }
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        var hand = TableView.IntList(k.Arr("hand")); var counts = TableView.IntList(k.Arr("counts"));
        string stage = k.Str("stage"); int top = (int)k.Num("top", -1), took = (int)k.Num("took", -1), stock = (int)k.Num("stock");
        bool myTurn = v.MyTurn && v.phase == "play";
        bool opened = k.Arr("opened") is List<object> op && v.you >= 0 && v.you < op.Count && op[v.you] is bool ob && ob;
        int dl = (int)k.Num("deal");
        if (dl != deal) { deal = dl; selected.Clear(); staged.Clear(); order.Clear(); }
        // keep your order: cards that left go, new cards come in at the end (then sorted)
        bool added = false;
        order.RemoveAll(c => !hand.Contains(c));
        foreach (var c in hand) if (!order.Contains(c)) { order.Add(c); added = true; }
        if (added) Sort();
        selected.RemoveAll(c => !hand.Contains(c));
        foreach (var g in staged) g.RemoveAll(c => !hand.Contains(c));
        staged.RemoveAll(g => g.Count == 0);
        if (!myTurn) { staged.Clear(); }
        var last = v.last;
        if (last != null && last.k != lastK)
        {
            int prev = lastK; lastK = last.k;
            if (prev >= 0) StartCoroutine(Anim(v, last));
        }
        string key2 = string.Join(",", order) + "|" + string.Join(",", selected) + "|" + string.Join(";", staged.Select(g => string.Join(",", g))) + "|" + v.v + "|" + myTurn;
        if (key2 == key) return;
        key = key2;
        var inStage = staged.SelectMany(g => g).ToList();

        // the pack and the pile
        foreach (Transform c in pileArea) Destroy(c.gameObject);
        bool canDraw = myTurn && stage == "draw";
        var packBtn = Ui.Button(pileArea, "", new Color(0, 0, 0, 0), Color.white, 10, Draw);
        ((RectTransform)packBtn.transform).Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(52, 73), new Vector2(0, 0));
        var pk = Ui.Picture(packBtn.transform, "Pack", BasraTable.Face(-1)); pk.rectTransform.Fill();
        if (canDraw) packBtn.gameObject.AddComponent<Outline>().effectColor = Ui.Gold;
        var pc = Ui.Label(packBtn.transform, stock.ToString(), 13, Color.white, TextAnchor.LowerCenter, Ui.Bold); pc.rectTransform.Fill(3);
        pc.gameObject.AddComponent<Shadow>().effectColor = Color.black;
        if (top >= 0)
        {
            var pileBtn = Ui.Button(pileArea, "", new Color(0, 0, 0, 0), Color.white, 10, Take);
            ((RectTransform)pileBtn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(52, 73), new Vector2(0, 0));
            var pf = Ui.Picture(pileBtn.transform, "Top", BasraTable.Face(FaceOf(top))); pf.rectTransform.Fill();
            if (canDraw) pileBtn.gameObject.AddComponent<Outline>().effectColor = Ui.Gold;
        }

        // the melds on the table: tap one with a card picked to add it
        foreach (Transform c in meldArea) Destroy(c.gameObject);
        var melds = k.Arr("melds") ?? new List<object>();
        const float MW = 30, MH = 42, MS = 15;
        float x = 0, y = 0, rowH = MH + 8, width = meldArea.rect.width > 0 ? meldArea.rect.width : 380;
        float scale = melds.Count > 12 ? 0.8f : 1f;
        for (int i = 0; i < melds.Count; i++)
        {
            var cards = TableView.IntList(melds[i].Arr("cards"));
            float w = (MW + (cards.Count - 1) * MS) * scale;
            if (x + w > width) { x = 0; y -= rowH * scale; }
            int mi = i;
            var mb = Ui.Button(meldArea, "", new Color(0, 0, 0, 0.18f), Color.white, 10, () => AddTo(mi));
            var mr = (RectTransform)mb.transform;
            mr.anchorMin = mr.anchorMax = new Vector2(0, 1); mr.pivot = new Vector2(0, 1);
            mr.sizeDelta = new Vector2(w + 4, MH * scale + 4); mr.anchoredPosition = new Vector2(x, y);
            for (int j = 0; j < cards.Count; j++)
            {
                var img = Ui.Picture(mr, "C", BasraTable.Face(FaceOf(cards[j]))).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0, 0.5f); img.pivot = new Vector2(0, 0.5f);
                img.sizeDelta = new Vector2(MW, MH) * scale; img.anchoredPosition = new Vector2(2 + j * MS * scale, 0);
            }
            x += w + 10;
        }

        // the groups you've made this turn, waiting to go down
        foreach (Transform c in stageArea) Destroy(c.gameObject);
        float sx = 0; int total = 0;
        float stotal = staged.Sum(g => 26 + (g.Count - 1) * 13 + 10) - 10;
        for (int gi = 0; gi < staged.Count; gi++)
        {
            var g = staged[gi]; total += Value(g);
            int gix = gi;
            float w = 26 + (g.Count - 1) * 13;
            var gb = Ui.Button(stageArea, "", new Color(0.35f, 0.6f, 0.3f, 0.35f), Color.white, 10, () => { Sfx.Tap(); staged.RemoveAt(gix); key = ""; Render(app.view); });
            var gr = (RectTransform)gb.transform; gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = new Vector2(w + 4, 40); gr.anchoredPosition = new Vector2(-stotal / 2 + sx + w / 2, 0);
            var ordered = Kind(g) == "run" ? OrderRun(g) : g;
            for (int j = 0; j < ordered.Count; j++)
            {
                var img = Ui.Picture(gr, "C", BasraTable.Face(FaceOf(ordered[j]))).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0, 0.5f); img.pivot = new Vector2(0, 0.5f);
                img.sizeDelta = new Vector2(26, 36); img.anchoredPosition = new Vector2(2 + j * 13, 0);
            }
            sx += w + 10;
        }
        stageText.Set(staged.Count == 0 ? "" : opened ? Loc.T("المجموع") + " " + total : Loc.T("المجموع") + " " + total + " / 51" + (total < 51 ? "  ·  " + Loc.T("محتاج ٥١ عشان تنزل") : ""));
        stageText.color = !opened && total < 51 ? new Color(1f, 0.7f, 0.5f) : new Color(0.6f, 0.95f, 0.55f);

        // your cards: tap to pick
        foreach (Transform c in handArea) Destroy(c.gameObject);
        var shown = order.Where(c => !inStage.Contains(c)).ToList();
        const float W = 48, H = 67; float step = Mathf.Min(34, (540 - W) / Mathf.Max(1, shown.Count - 1));
        for (int i = 0; i < shown.Count; i++)
        {
            int id = shown[i]; bool sel = selected.Contains(id);
            var btn = Ui.Button(handArea, "", new Color(0, 0, 0, 0), Color.white, 10, () => Pick(id));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(W, H);
            rt.anchoredPosition = new Vector2((i - (shown.Count - 1) / 2f) * step, H / 2 + (sel ? 16 : 3));
            var img = Ui.Picture(rt, "Face", BasraTable.Face(FaceOf(id))); img.rectTransform.Fill();
            if (id == took) btn.gameObject.AddComponent<Outline>().effectColor = new Color(0.4f, 0.9f, 1f);
            if (sel) img.color = new Color(1f, 0.97f, 0.8f);
        }

        // the others: their cards face down, their name's tag says how many and whether they've laid down
        for (int i = 1; i < 4; i++) foreach (Transform c in backs[i]) Destroy(c.gameObject);
        var opList = k.Arr("opened");
        var scores = TableView.IntList(k.Arr("scores"));
        for (int i = 0; i < 4; i++) tags[i].gameObject.SetActive(false);
        for (int s = 0; s < counts.Count; s++)
        {
            int rel = Rel(v, s);
            bool down = opList != null && s < opList.Count && opList[s] is bool b && b;
            if (rel != 0)
            {
                int n = Mathf.Min(counts[s], 16); bool side = rel != 2;
                for (int i = 0; i < n; i++)
                {
                    var img = Ui.Picture(backs[rel], "Back", BasraTable.Face(-1)).rectTransform;
                    img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(26, 36);
                    img.anchoredPosition = side ? new Vector2(0, (i - (n - 1) / 2f) * 5) : new Vector2((i - (n - 1) / 2f) * 9, 0);
                    img.localRotation = Quaternion.Euler(0, 0, side ? 90 : 0);
                }
                tags[rel].gameObject.SetActive(true);
                tags[rel].Set(counts[s] + " " + Loc.T("ورقة") + (down ? "  ·  " + Loc.T("نزل") : ""));
                var p = SeatPoint(rel);
                tags[rel].rectTransform.anchoredPosition = p + (rel == 2 ? new Vector2(140, 0) : new Vector2(0, -78));
            }
        }

        // the buttons
        bool play = myTurn && stage == "play";
        throwBtn.gameObject.SetActive(play);
        throwBtn.interactable = selected.Count == 1 && took < 0 && staged.Count == 0;
        downBtn.gameObject.SetActive(play);
        downBtn.interactable = staged.Count > 0 || (selected.Count >= 3 && Kind(selected) != null && (opened || Value(selected) >= 51));
        groupBtn.gameObject.SetActive(play);
        groupBtn.interactable = selected.Count >= 3;
        backBtn.gameObject.SetActive(play && took >= 0);
        foreach (var b in new[] { throwBtn, downBtn, groupBtn }) b.GetComponent<Image>().color = b.interactable ? (b == throwBtn ? Ui.Gold : b == downBtn ? new Color(0.35f, 0.6f, 0.3f) : Ui.Panel) : new Color(0.3f, 0.26f, 0.22f, 0.7f);

        // the score: points against each player (the lowest wins), the target
        string sc = "";
        for (int s = 0; s < scores.Count; s++) sc += (s == v.you ? Loc.T("إنت") : v.seats.Count > s && v.seats[s] != null ? First(v.seats[s].name) : "") + " " + scores[s] + "\n";
        infoText.Set(sc + Loc.T("لـ") + " " + (int)k.Num("target") + "  ·  " + Loc.T("دور") + " " + dl);
    }

    static string First(string n) { if (string.IsNullOrEmpty(n)) return ""; int sp = n.IndexOf(' '); return sp > 0 && sp < n.Length - 1 ? n.Substring(sp + 1) : n; }

    static List<int> OrderRun(List<int> cards)
    {
        var (lo, len) = RunShape(cards); if (len == 0) return cards;
        var real = cards.Where(c => !IsJoker(c)).ToList(); var jokers = cards.Where(IsJoker).ToList();
        var outp = new List<int>();
        for (int r = lo; r < lo + len; r++)
        {
            int want = r == 14 ? 1 : r; int i = real.FindIndex(c => RankOf(c) == want);
            if (i >= 0) { outp.Add(real[i]); real.RemoveAt(i); } else if (jokers.Count > 0) { outp.Add(jokers[jokers.Count - 1]); jokers.RemoveAt(jokers.Count - 1); }
        }
        return outp;
    }

    /* ---------- what you do ---------- */
    bool Mine(out Dictionary<string, object> k)
    {
        k = app.view?.konkan;
        return k != null && app.view.MyTurn && app.view.phase == "play";
    }
    void Act(Dictionary<string, object> a) => app.Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });
    void Refresh() { key = ""; if (app.view != null) Render(app.view); }

    void Pick(int id)
    {
        Sfx.Tap();
        if (selected.Contains(id)) selected.Remove(id); else selected.Add(id);
        Refresh();
    }
    void Draw()
    {
        if (!Mine(out var k) || k.Str("stage") != "draw") { Sfx.Tap(); return; }
        Sfx.Tap(); Act(new Dictionary<string, object> { ["t"] = "draw" });
    }
    void Take()
    {
        if (!Mine(out var k) || k.Str("stage") != "draw") { Sfx.Tap(); return; }
        Sfx.Tap(); Act(new Dictionary<string, object> { ["t"] = "take" });
        app.Toast(Loc.T("الورقة اللي خدتها لازم تنزل بيها في نفس الدور"), 3f);
    }
    void Back() { if (!Mine(out _)) return; Sfx.Tap(); Act(new Dictionary<string, object> { ["t"] = "back" }); }
    void Group()
    {
        if (!Mine(out _)) return;
        var g = selected.ToList();
        if (Kind(g) == null) { Sfx.Tap(); app.Toast(Loc.T("دي مش مجموعة: ٣ أو ٤ من نفس الرقم بأشكال مختلفة، أو ٣ ورا بعض من نفس الشكل")); return; }
        Sfx.Clack(); staged.Add(g); selected.Clear(); Refresh();
    }
    void Down()
    {
        if (!Mine(out var k)) return;
        if (selected.Count >= 3 && Kind(selected) != null) { staged.Add(selected.ToList()); selected.Clear(); }
        if (staged.Count == 0) return;
        bool opened = k.Arr("opened") is List<object> op && app.view.you < op.Count && op[app.view.you] is bool ob && ob;
        int total = staged.Sum(g => Value(g));
        if (!opened && total < 51) { Sfx.Tap(); app.Toast(Loc.T("أول نزلة لازم تكون ٥١ أو أكتر") + "  (" + total + ")"); Refresh(); return; }
        var melds = staged.Select(g => (object)g.Select(c => (object)(double)c).ToList()).ToList();
        Act(new Dictionary<string, object> { ["t"] = "down", ["melds"] = melds });
        staged.Clear(); Refresh();
    }
    void AddTo(int meld)
    {
        if (!Mine(out var k)) return;
        if (selected.Count != 1) { Sfx.Tap(); app.Toast(Loc.T("اختار ورقة واحدة وبعدين دوس على المجموعة اللي عايز تحطها فيها")); return; }
        Sfx.Clack();
        Act(new Dictionary<string, object> { ["t"] = "add", ["id"] = selected[0], ["meld"] = meld });
        selected.Clear();
    }
    void Throw()
    {
        if (!Mine(out var k) || selected.Count != 1) return;
        if ((int)k.Num("took", -1) >= 0) { app.Toast(Loc.T("الورقة اللي خدتها لازم تنزل بيها في نفس الدور")); return; }
        Sfx.Tap();
        Act(new Dictionary<string, object> { ["t"] = "throw", ["id"] = selected[0] });
        selected.Clear();
    }

    /* ---------- what the others do ---------- */
    IEnumerator Anim(TableView v, LastAction last)
    {
        int rel = Rel(v, last.s);
        switch (last.t)
        {
            case "draw": case "take": Sfx.Slap(); break;
            case "down": Sfx.Coins(); if (rel != 0) app.Toast(Name(v, last.s) + " " + Loc.T("نزل!"), 2f); break;
            case "add": Sfx.Clack(); break;
            case "throw":
                Sfx.Clack();
                if (rel != 0 && last.card >= 0)
                {
                    var fly = Ui.Picture(root, "Fly", BasraTable.Face(FaceOf(last.card))).rectTransform;
                    fly.anchorMin = fly.anchorMax = new Vector2(0.5f, 0.5f); fly.sizeDelta = new Vector2(52, 73);
                    Vector2 from = SeatPoint(rel), to = pileArea.anchoredPosition + new Vector2(39, 0);
                    for (float x = 0; x < 0.25f; x += Time.deltaTime) { if (fly == null) yield break; fly.anchoredPosition = Vector2.Lerp(from, to, Ui.Back(x / 0.25f)); yield return null; }
                    if (fly != null) Destroy(fly.gameObject);
                }
                break;
        }
        if (last.deal) ShowDeal(app.view);
    }

    static string Name(TableView v, int s) => s >= 0 && s < v.seats.Count && v.seats[s] != null ? First(v.seats[s].name) : "";

    void ShowDeal(TableView v)
    {
        var d = v?.konkan?.Obj("lastDeal"); if (d == null) return;
        int w = (int)d.Num("winner", -1); var pts = TableView.IntList(d.Arr("points"));
        string s = (w == v.you ? Loc.T("خلّصت ورقك! كسبت الدور") : Name(v, w) + " " + Loc.T("خلّص ورقه"));
        if (v.you >= 0 && v.you < pts.Count && w != v.you) s += "  ·  " + Loc.T("عليك") + " " + pts[v.you];
        app.Toast(s, 4.5f);
        if (w == v.you) { Sfx.Coins(); Sfx.Chime(); }
    }
}
