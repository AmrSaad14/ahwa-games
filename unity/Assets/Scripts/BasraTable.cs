// Basra (البصرة) at the table: the floor cards in the middle, your hand at the bottom, the others' cards face down
// at their places, and how the deal stands. Tap a card in your hand to play it on your turn; the server works out
// what it takes (shared/games/basra.ts). Played cards fly to the floor; what was taken flies to whoever took it,
// and a basra gets its own big moment.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BasraTable : MonoBehaviour
{
    App app;
    Canvas canvas;
    RectTransform root, floorArea, handArea, info;
    readonly RectTransform[] backs = new RectTransform[4];
    Text infoText;
    int lastK = -1;
    string key = "";
    static readonly Dictionary<int, Texture2D> faces = new Dictionary<int, Texture2D>();
    static Texture2D back;

    public static Texture2D Face(int id)
    {
        if (id < 0) return back ??= Resources.Load<Texture2D>("UI/Cards/back");
        if (!faces.TryGetValue(id, out var t)) faces[id] = t = Resources.Load<Texture2D>("UI/Cards/c" + id);
        return t;
    }

    public void Init(App a)
    {
        app = a;
        canvas = Ui.MakeCanvas("Basra", 15);
        root = Ui.Rect("Basra", canvas.transform).Safe();
        floorArea = Ui.Rect("Floor", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 190), new Vector2(0, 22));
        handArea = Ui.Rect("Hand", root).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(420, 120), new Vector2(0, 6));
        for (int i = 1; i < 4; i++) backs[i] = Ui.Rect("Backs" + i, root);
        backs[1].Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(70, 160), new Vector2(-116, 20));   // to your right
        backs[2].Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(200, 70), new Vector2(0, -62));    // across
        backs[3].Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 160), new Vector2(124, 20));    // to your left
        var ib = Ui.Box(root, "Info", new Color(0.1f, 0.06f, 0.04f, 0.82f));
        info = ib.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(178, 86), new Vector2(12, 12));
        infoText = Ui.Label(info, "", 12, Color.white, TextAnchor.MiddleCenter, Ui.Bold); infoText.rectTransform.Fill(6);
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        canvas.gameObject.SetActive(false);
    }

    public void Hide() { if (canvas != null) canvas.gameObject.SetActive(false); key = ""; lastK = -1; }

    /// Where a seat sits on the screen, from your point of view (0 you, 1 right, 2 across, 3 left; two players: across).
    int Rel(TableView v, int seat)
    {
        int n = v.seats.Count; int r = ((seat - v.you) % n + n) % n;
        return n == 2 && r == 1 ? 2 : r;
    }
    Vector2 SeatPoint(int rel)
    {
        var half = root.rect.size / 2;
        switch (rel)
        {
            case 0: return new Vector2(0, -half.y + 70);
            case 1: return new Vector2(half.x - 120, 20);
            case 2: return new Vector2(0, half.y - 90);
            default: return new Vector2(-half.x + 120, 20);
        }
    }

    public void Render(TableView v)
    {
        var b = v.basra;
        if (b == null) { Hide(); return; }
        if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
        var hand = TableView.IntList(b.Arr("hand")); var floor = TableView.IntList(b.Arr("floor")); var counts = TableView.IntList(b.Arr("counts"));
        var scores = TableView.IntList(b.Arr("scores")); var taken = TableView.IntList(b.Arr("taken")); var basras = TableView.IntList(b.Arr("basras"));
        bool myTurn = v.MyTurn && v.phase == "play";
        // the last play: animate it once
        var last = v.last;
        if (last != null && last.k != lastK && last.t == "card")
        {
            int prev = lastK; lastK = last.k;
            if (prev >= 0) StartCoroutine(PlayAnim(v, last));
        }
        else if (last != null) lastK = last.k;
        string k = string.Join(",", hand) + "|" + string.Join(",", floor) + "|" + string.Join(",", counts) + "|" + myTurn + "|" + v.v;
        if (k == key) return;
        key = k;
        // the floor, in rows of up to eight
        foreach (Transform c in floorArea) Destroy(c.gameObject);
        const float FW = 58, FH = 81;
        int rows = (floor.Count + 7) / 8;
        for (int i = 0; i < floor.Count; i++)
        {
            int r = i / 8, cnum = i % 8, inRow = Mathf.Min(8, floor.Count - r * 8);
            var img = Ui.Picture(floorArea, "Card", Face(floor[i])).rectTransform;
            img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(FW, FH);
            img.anchoredPosition = new Vector2((cnum - (inRow - 1) / 2f) * (FW + 6), ((rows - 1) / 2f - r) * (FH + 6));
            img.localRotation = Quaternion.Euler(0, 0, ((floor[i] * 37) % 9 - 4) * 0.8f);
        }
        if (floor.Count == 0)
        {
            var empty = Ui.Label(floorArea, Loc.T("الأرض فاضية"), 13, new Color(1, 1, 1, 0.45f));
            empty.rectTransform.Fill();
        }
        // your hand: tap to play
        foreach (Transform c in handArea) Destroy(c.gameObject);
        const float HW = 74, HH = 104;
        for (int i = 0; i < hand.Count; i++)
        {
            int id = hand[i];
            var btn = Ui.Button(handArea, "", new Color(0, 0, 0, 0), Color.white, 10, () => Play(id));
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0); rt.sizeDelta = new Vector2(HW, HH);
            rt.anchoredPosition = new Vector2((i - (hand.Count - 1) / 2f) * (HW + 8), HH / 2 + (myTurn ? 12 : 4));
            var img = Ui.Picture(rt, "Face", Face(id)); img.rectTransform.Fill();
            if (!myTurn) img.color = new Color(0.82f, 0.82f, 0.82f);
        }
        // the others' cards, face down at their places
        for (int i = 1; i < 4; i++) foreach (Transform c in backs[i]) Destroy(c.gameObject);
        for (int s = 0; s < v.seats.Count; s++)
        {
            if (s == v.you || s >= counts.Count) continue;
            int rel = Rel(v, s); var area = backs[rel];
            bool side = rel == 1 || rel == 3;
            for (int i = 0; i < counts[s]; i++)
            {
                var img = Ui.Picture(area, "Back", Face(-1)).rectTransform;
                img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f); img.sizeDelta = new Vector2(36, 50);
                img.anchoredPosition = side ? new Vector2(0, (i - (counts[s] - 1) / 2f) * 22) : new Vector2((i - (counts[s] - 1) / 2f) * 24, 0);
                img.localRotation = Quaternion.Euler(0, 0, side ? 90 : 0);
            }
        }
        // the deal: points over the deals, cards taken and basras this deal, the pack
        int teams = (int)b.Num("teams"), mine = v.you >= 0 ? (teams == 2 && v.seats.Count == 4 ? v.you % 2 : v.you) : 0, other = teams == 2 ? 1 - mine : (mine == 0 ? 1 : 0);
        string Us(List<int> x) => (x.Count > mine ? x[mine] : 0) + " – " + (x.Count > other ? x[other] : 0);
        infoText.Set(Loc.T("النقط") + ": " + Us(scores) + "  (" + Loc.T("لـ") + " " + (int)b.Num("target") + ")\n"
            + Loc.T("الورق") + ": " + Us(taken) + "  ·  " + Loc.T("بصرات") + ": " + Us(basras) + "\n"
            + Loc.T("الكوتشينة") + ": " + (int)b.Num("deck") + "  ·  " + Loc.T("دور") + " " + (int)b.Num("deal")
            + ((int)b.Num("carry") > 0 ? "\n" + Loc.T("مترحّل") + ": " + (int)b.Num("carry") : ""));
    }

    void Play(int id)
    {
        var v = app.view;
        if (v == null || !v.MyTurn || v.phase != "play") { Sfx.Tap(); return; }
        Sfx.Tap();
        app.Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = new Dictionary<string, object> { ["t"] = "card", ["id"] = id } });
    }

    /// The card flies from the player to the floor; if it took cards, they all go to the player; a basra shines.
    IEnumerator PlayAnim(TableView v, LastAction last)
    {
        var from = SeatPoint(Rel(v, last.s));
        var fly = Ui.Picture(root, "Fly", Face(last.card)).rectTransform;
        fly.anchorMin = fly.anchorMax = new Vector2(0.5f, 0.5f); fly.sizeDelta = new Vector2(62, 87);
        var to = (Vector2)floorArea.anchoredPosition;
        for (float t = 0; t < 0.28f; t += Time.deltaTime) { if (fly == null) yield break; fly.anchoredPosition = Vector2.Lerp(from, to, Ui.Back(t / 0.28f)); yield return null; }
        Sfx.Clack();
        if (last.took != null && last.took.Count > 0)
        {
            yield return new WaitForSeconds(0.18f);
            var pile = new List<RectTransform> { fly };
            for (int i = 0; i < last.took.Count; i++)
            {
                var c = Ui.Picture(root, "Took", Face(last.took[i])).rectTransform;
                c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f); c.sizeDelta = new Vector2(58, 81);
                c.anchoredPosition = to + new Vector2((i - (last.took.Count - 1) / 2f) * 30, 0);
                pile.Add(c);
            }
            if (last.basra > 0) { StartCoroutine(BasraBurst(last.basra)); yield return new WaitForSeconds(0.5f); }
            var start = new List<Vector2>(); foreach (var p in pile) start.Add(p.anchoredPosition);
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                for (int i = 0; i < pile.Count; i++) if (pile[i] != null) { pile[i].anchoredPosition = Vector2.Lerp(start[i], from, t / 0.35f); pile[i].localScale = Vector3.one * Mathf.Lerp(1, 0.5f, t / 0.35f); }
                yield return null;
            }
            foreach (var p in pile) if (p != null) Destroy(p.gameObject);
            Sfx.Slap();
        }
        else { yield return new WaitForSeconds(0.1f); if (fly != null) Destroy(fly.gameObject); }
        if (last.deal) ShowDeal(app.view);
    }

    IEnumerator BasraBurst(int points)
    {
        Sfx.Coins(); Sfx.Chime(); Sfx.Buzz();
        var t = Ui.Label(root, points >= 20 ? Loc.T("بصرة ولد!") + "  +" + points : Loc.T("بصرة!") + "  +" + points, 44, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform;
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f); t.sizeDelta = new Vector2(420, 70); t.anchoredPosition = new Vector2(0, 110);
        t.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.05f, 0.05f, 0.9f);
        for (float x = 0; x < 1.4f; x += Time.deltaTime)
        {
            if (t == null) yield break;
            t.localScale = Vector3.one * (x < 0.25f ? Ui.Back(x / 0.25f) : 1);
            var g = t.GetComponent<Text>(); g.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, x > 1f ? 1 - (x - 1f) / 0.4f : 1);
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    /// A deal ended: what each side made.
    void ShowDeal(TableView v)
    {
        var d = v?.basra?.Obj("lastDeal"); if (d == null) return;
        var pts = TableView.IntList(d.Arr("points")); var cards = TableView.IntList(d.Arr("cards"));
        int mine = v.you >= 0 ? (v.seats.Count == 4 ? v.you % 2 : v.you) : 0, other = v.seats.Count == 4 ? 1 - mine : (mine == 0 ? 1 : 0);
        if (pts.Count < 2) return;
        app.Toast(Loc.T("خلص الدور") + ":  " + Loc.T("إنت") + " +" + pts[mine] + " (" + cards[mine] + " " + Loc.T("ورقة") + ")  ·  " + Loc.T("هم") + " +" + pts[other] + " (" + cards[other] + ")", 4f);
    }
}
