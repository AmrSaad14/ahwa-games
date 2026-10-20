// Playing chess, tawla and ludo: the board on the big table, and your taps turned into moves.
//  - chess: tap one of your pieces, then one of the squares it can go to (pawns promote to a queen),
//  - tawla: roll, then tap a checker (or the bar) and one of the places it can go (a point, or the tray),
//  - ludo: roll, then tap one of the pawns that can move.
// The server checks every move; it also plays a forced pass by itself.
using System.Collections.Generic;
using UnityEngine;

public class BoardGame : MonoBehaviour
{
    App app;
    Transform tableRoot;
    Board board;
    TableView view;
    int sel = -1000;
    readonly List<int> targets = new List<int>();
    int lastK = -1;

    public void Init(App a)
    {
        app = a;
        tableRoot = new GameObject("BoardGame").transform;
        // on the big table, turned so "away from you" runs away from the camera
        tableRoot.rotation = Quaternion.LookRotation(-a.cafe.Front, Vector3.up);
    }

    public void Show(bool on)
    {
        if (!on && board != null) { Destroy(board.root); board = null; }
        view = null; sel = -1000; targets.Clear();
    }

    public void Set(TableView v)
    {
        if (board == null || board.game != v.game)
        {
            if (board != null) Destroy(board.root);
            board = v.game == "chess" ? new ChessBoard(tableRoot) : v.game == "tawla" ? (Board)new TawlaBoard(tableRoot) : new LudoBoard(tableRoot);
        }
        bool turnChanged = view == null || view.turn != v.turn || view.v != v.v;
        view = v;
        if (turnChanged) Validate();
        board.Render(v, sel, targets, Movable());
        // a move was just made: a sound, and the player's hand goes out to where it landed
        if (v.last != null && v.last.k != lastK)
        {
            lastK = v.last.k;
            var t = v.last.t;
            if (t == "roll") Sfx.Dice();
            else if (t == "move" || t == "step" || t == "pawn") Sfx.Clack();
            var who = app.dominoTable.CharAtSeat(v.last.s);
            if (who != null && (t == "move" || t == "step" || t == "roll" || t == "pawn"))
            {
                int spot = t == "move" || t == "step" ? v.last.to : t == "pawn" ? v.last.i : 0;
                var at = t == "roll" ? board.root.transform.position : board.Spot(v, spot);
                who.Gesture(at);
            }
        }
    }

    /// The selection still makes sense? (After a move, or when it's no longer your turn, it goes.)
    void Validate()
    {
        if (view == null || !view.MyTurn) { sel = -1000; targets.Clear(); return; }
        if (sel != -1000 && !FromsNow().Contains(sel)) { sel = -1000; targets.Clear(); }
        else if (sel != -1000) { targets.Clear(); targets.AddRange(TargetsFrom(sel)); }
    }

    /* ---------- what can move ---------- */

    List<int> FromsNow()
    {
        var r = new List<int>();
        if (view == null || !view.MyTurn) return r;
        if (view.game == "chess")
            foreach (var m in view.chess?.Arr("legal") ?? new List<object>()) { var a = m as List<object>; if (a != null && a.Count >= 2) { int f = (int)(double)a[0]; if (!r.Contains(f)) r.Add(f); } }
        else if (view.game == "tawla")
        {
            foreach (var m in Steps()) if (!r.Contains(m.from)) r.Add(m.from);
        }
        else if (view.game == "ludo")
            r.AddRange(TableView.IntList(view.ludo?.Arr("movable")));
        return r;
    }

    struct Step { public int from, to; public List<int> via; }
    List<Step> Steps()
    {
        var r = new List<Step>();
        var s = view?.tawla; if (s == null) return r;
        foreach (var o in s.Arr("steps") ?? new List<object>()) { var a = TableView.IntList(o as List<object>); if (a.Count >= 2) r.Add(new Step { from = a[0], to = a[1] }); }
        foreach (var o in s.Arr("combos") ?? new List<object>())
        {
            var a = o as List<object>;
            if (a == null || a.Count < 3) continue;
            int f = (int)(double)a[0], t = (int)(double)a[1];
            if (r.Exists(x => x.from == f && x.to == t)) continue;
            r.Add(new Step { from = f, to = t, via = TableView.IntList(a[2] as List<object>) });
        }
        return r;
    }

    List<int> TargetsFrom(int from)
    {
        var r = new List<int>();
        if (view.game == "chess")
            foreach (var m in view.chess?.Arr("legal") ?? new List<object>()) { var a = m as List<object>; if (a != null && a.Count >= 2 && (int)(double)a[0] == from) r.Add((int)(double)a[1]); }
        else if (view.game == "tawla")
            foreach (var st in Steps()) if (st.from == from && !r.Contains(st.to)) r.Add(st.to);
        return r;
    }

    List<int> Movable()
    {
        if (view == null || !view.MyTurn) return new List<int>();
        if (view.game == "ludo") return TableView.IntList(view.ludo?.Arr("movable"));
        if (view.game == "tawla" && sel == -1000 && NeedsRoll() == false) return FromsNow();
        return new List<int>();
    }

    /// Your turn, and the dice still to throw?
    public bool NeedsRoll()
    {
        if (view == null || !view.MyTurn) return false;
        if (view.game == "tawla") return view.tawla != null && !(view.tawla.TryGetValue("rolled", out var r) && r is List<object>);
        if (view.game == "ludo") return view.ludo != null && !(view.ludo.TryGetValue("die", out var d) && d is double);
        return false;
    }

    /* ---------- playing ---------- */

    public void Roll()
    {
        if (!NeedsRoll()) return;
        Act(new Dictionary<string, object> { ["t"] = "roll" });
    }

    void Act(Dictionary<string, object> a) => app.Send(new Dictionary<string, object> { ["t"] = "act", ["a"] = a });

    /// A tap on the screen while at a board.
    public void Tap(Vector3 screen)
    {
        if (board == null || view == null || !view.MyTurn) return;
        var ray = app.cafe.cam.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.up, board.spin.TransformPoint(new Vector3(0, board.topY, 0)));
        if (!plane.Raycast(ray, out float dist)) return;
        var local = board.spin.InverseTransformPoint(ray.GetPoint(dist));
        var pick = board.PickAt(local);
        if (NeedsRoll()) { Roll(); return; }   // a tap anywhere on the board throws the dice
        if (view.game == "ludo")
        {
            if (pick.kind == "pawn") Act(new Dictionary<string, object> { ["t"] = "pawn", ["i"] = (double)pick.v });
            return;
        }
        if (pick.kind != "square" && pick.kind != "point") return;
        int p = pick.v;
        if (sel != -1000 && targets.Contains(p))
        {
            if (view.game == "chess")
            {
                var a = new Dictionary<string, object> { ["t"] = "move", ["from"] = (double)sel, ["to"] = (double)p };
                foreach (var m in view.chess?.Arr("legal") ?? new List<object>())
                {
                    var l = m as List<object>;
                    if (l != null && l.Count >= 3 && (int)(double)l[0] == sel && (int)(double)l[1] == p) a["promo"] = "q";
                }
                Act(a);
            }
            else
            {
                var st = Steps().Find(x => x.from == sel && x.to == p);
                var a = new Dictionary<string, object> { ["t"] = "step", ["from"] = (double)sel, ["to"] = (double)p };
                if (st.via != null && st.via.Count > 0) { var via = new List<object>(); foreach (var x in st.via) via.Add((double)x); a["via"] = via; }
                Act(a);
            }
            sel = -1000; targets.Clear();
            Sfx.Tap();
        }
        else if (FromsNow().Contains(p))
        {
            sel = sel == p ? -1000 : p;
            targets.Clear(); if (sel != -1000) targets.AddRange(TargetsFrom(sel));
            Sfx.Tap();
        }
        else { sel = -1000; targets.Clear(); }
        board.Render(view, sel, targets, Movable());
        app.game.Render(view, -1, null);   // the hint under the board changes with the selection
    }

    /// The test run plays by itself: roll, or make the first move there is.
    public void AutoMove()
    {
        if (view == null || !view.MyTurn) return;
        if (NeedsRoll()) { Roll(); return; }
        if (view.game == "chess")
        {
            var legal = view.chess?.Arr("legal");
            if (legal != null && legal.Count > 0 && legal[0] is List<object> m && m.Count >= 2)
            {
                var a = new Dictionary<string, object> { ["t"] = "move", ["from"] = m[0], ["to"] = m[1] };
                if (m.Count >= 3) a["promo"] = "q";
                Act(a);
            }
        }
        else if (view.game == "tawla") { var st = Steps(); if (st.Count > 0) Act(new Dictionary<string, object> { ["t"] = "step", ["from"] = (double)st[0].from, ["to"] = (double)st[0].to }); }
        else if (view.game == "ludo") { var mv = TableView.IntList(view.ludo?.Arr("movable")); if (mv.Count > 0) Act(new Dictionary<string, object> { ["t"] = "pawn", ["i"] = (double)mv[0] }); }
    }

    /* ---------- for the screens ---------- */

    /// What to tell you about your turn.
    public string TurnHint()
    {
        if (view == null || !view.MyTurn) return null;
        if (NeedsRoll()) return Loc.T("دورك: ارمي الزهر");
        if (view.game == "chess") return view.chess != null && view.chess.Num("check", -1) >= 0 ? Loc.T("كش! احمي الملك") : sel != -1000 ? Loc.T("اختار المكان") : Loc.T("دورك: حرّك قطعة");
        if (view.game == "tawla") return sel != -1000 ? Loc.T("اختار المكان") : Loc.T("حرّك قشاطاتك");
        if (view.game == "ludo") return Loc.T("اختار قطعة تتحرك");
        return Loc.T("دورك");
    }
}
