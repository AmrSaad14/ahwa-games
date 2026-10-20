// Tawla, as in shared/games/tawla.ts (عادة, regular backgammon) and shared/games/tables.ts (محبوسة, ٣١, جلبهار).
// Points 0..23; player 0 moves from high to low and bears off past point 0.
using System;
using System.Collections.Generic;
using System.Linq;

public struct TStep { public int from, to, die; public bool hit; public TStep(int f, int t, int d, bool h = false) { from = f; to = t; die = d; hit = h; } }

/// What the board table needs from either kind of tawla.
public interface ITawla
{
    int Turn { get; }
    int[] Rolled { get; }
    ITawla Roll(Random rnd);
    List<TStep> NextSteps();
    ITawla PlayStep(int from, int to);
    bool TurnDone();
    ITawla EndTurn();
    (int winner, int points, string kind)? Winner();
    TStep? BotStep(Random rnd);
    int OpponentBar { get; }   // checkers of the player not moving that are on the bar (to see a hit)
    Dictionary<string, object> View(int seat);
}

/* ---------------- عادة ---------------- */
public class Aada : ITawla
{
    public const int BAR = -1, OFF = 99;
    public int[] pts = new int[24];
    public int[] bar = new int[2], off = new int[2];
    public int turn;
    public List<int> dice = new List<int>();
    public int[] rolled;
    public List<List<TStep>> seqs;

    public int Turn => turn;
    public int[] Rolled => rolled;
    public int OpponentBar => bar[1 - turn];

    public static Aada Start(int first)
    {
        var s = new Aada { turn = first };
        s.pts[23] = 2; s.pts[12] = 5; s.pts[7] = 3; s.pts[5] = 5;
        s.pts[0] = -2; s.pts[11] = -5; s.pts[16] = -3; s.pts[18] = -5;
        return s;
    }

    Aada Clone() => new Aada { pts = (int[])pts.Clone(), bar = (int[])bar.Clone(), off = (int[])off.Clone(), turn = turn, dice = new List<int>(dice), rolled = rolled, seqs = null };

    static int Sign(int p) => p == 0 ? 1 : -1;
    bool Mine(int p, int i) => pts[i] * Sign(p) > 0;
    int Count(int p, int i) => Math.Max(0, pts[i] * Sign(p));
    bool Blocked(int p, int i) => pts[i] * Sign(p) <= -2;

    bool AllHome(int p)
    {
        if (bar[p] > 0) return false;
        for (int i = 0; i < 24; i++) { if (!Mine(p, i)) continue; if (p == 0 ? i > 5 : i < 18) return false; }
        return true;
    }

    List<TStep> SingleSteps(int die)
    {
        int p = turn; var o = new List<TStep>();
        if (bar[p] > 0)
        {
            int to = p == 0 ? 24 - die : die - 1;
            if (!Blocked(p, to)) o.Add(new TStep(BAR, to, die, pts[to] * Sign(p) == -1));
            return o;
        }
        bool home = AllHome(p);
        for (int i = 0; i < 24; i++)
        {
            if (!Mine(p, i)) continue;
            int to = p == 0 ? i - die : i + die;
            if (to >= 0 && to <= 23) { if (!Blocked(p, to)) o.Add(new TStep(i, to, die, pts[to] * Sign(p) == -1)); }
            else if (home)
            {
                int dist = p == 0 ? i + 1 : 24 - i;
                if (dist == die) o.Add(new TStep(i, OFF, die));
                else if (die > dist)
                {
                    bool further = false;
                    if (p == 0) { for (int j = i + 1; j <= 5; j++) if (Mine(p, j)) further = true; }
                    else { for (int j = i - 1; j >= 18; j--) if (Mine(p, j)) further = true; }
                    if (!further) o.Add(new TStep(i, OFF, die));
                }
            }
        }
        return o;
    }

    Aada Apply(TStep st)
    {
        int p = turn, o = 1 - p, sg = Sign(p);
        var n = Clone();
        if (st.from == BAR) n.bar[p]--; else n.pts[st.from] -= sg;
        if (st.to == OFF) n.off[p]++;
        else
        {
            if (n.pts[st.to] * sg == -1) { n.pts[st.to] = 0; n.bar[o]++; }
            n.pts[st.to] += sg;
        }
        n.dice.Remove(st.die);
        return n;
    }

    List<List<TStep>> Sequences()
    {
        var all = new List<List<TStep>>();
        void Rec(Aada st, List<TStep> path)
        {
            bool moved = false;
            foreach (var d in st.dice.Distinct().ToList())
                foreach (var step in st.SingleSteps(d))
                {
                    moved = true;
                    Rec(st.Apply(step), new List<TStep>(path) { step });
                }
            if (!moved) all.Add(path);
        }
        Rec(this, new List<TStep>());
        int maxLen = all.Max(q => q.Count);
        var best = all.Where(q => q.Count == maxLen).ToList();
        if (maxLen == 1 && dice.Count == 2 && dice[0] != dice[1])
        {
            int hi = Math.Max(dice[0], dice[1]);
            if (best.Any(q => q[0].die == hi)) best = best.Where(q => q[0].die == hi).ToList();
        }
        return best;
    }

    public ITawla Roll(Random rnd)
    {
        int a = 1 + rnd.Next(6), b = 1 + rnd.Next(6);
        var n = Clone();
        n.rolled = new[] { a, b };
        n.dice = a == b ? new List<int> { a, a, a, a } : new List<int> { a, b };
        n.seqs = n.Sequences();
        return n;
    }

    static bool Same(TStep a, TStep b) => a.from == b.from && a.to == b.to && a.die == b.die;

    public List<TStep> NextSteps()
    {
        var o = new List<TStep>();
        if (seqs == null) return o;
        foreach (var q in seqs) if (q.Count > 0 && !o.Any(x => Same(x, q[0]))) o.Add(q[0]);
        return o;
    }

    public ITawla PlayStep(int from, int to)
    {
        if (seqs == null) return null;
        var cand = NextSteps().Where(x => x.from == from && x.to == to).OrderBy(x => x.die).ToList();
        if (cand.Count == 0) return null;
        var step = cand[0];
        var n = Apply(step);
        n.seqs = seqs.Where(q => q.Count > 0 && Same(q[0], step)).Select(q => q.Skip(1).ToList()).ToList();
        if (n.seqs.Count == 0 || n.seqs.All(q => q.Count == 0)) n.seqs = new List<List<TStep>> { new List<TStep>() };
        return n;
    }

    public bool TurnDone() => seqs != null && seqs.All(q => q.Count == 0);

    public ITawla EndTurn() { var n = Clone(); n.turn = 1 - turn; n.dice = new List<int>(); n.rolled = null; n.seqs = null; return n; }

    public (int winner, int points, string kind)? Winner()
    {
        for (int p = 0; p < 2; p++) if (off[p] == 15) { bool mars = off[1 - p] == 0; return (p, mars ? 2 : 1, mars ? "mars" : "win"); }
        return null;
    }

    public int PipCount(int p)
    {
        int n = bar[p] * 25;
        for (int i = 0; i < 24; i++) n += Count(p, i) * (p == 0 ? i + 1 : 24 - i);
        return n;
    }

    double Score(int p, Random rnd)
    {
        int o = 1 - p;
        double v = (PipCount(o) - PipCount(p)) + off[p] * 6 + bar[o] * 18 - bar[p] * 18;
        for (int i = 0; i < 24; i++)
        {
            int c = Count(p, i);
            if (c >= 2) v += (p == 0 ? i <= 6 : i >= 17) ? 7 : 4;
            if (c == 1)
            {
                int danger = 0;
                for (int j = 0; j < 24; j++) if (Count(o, j) > 0 && (p == 0 ? j < i : j > i) && Math.Abs(i - j) <= 12) danger++;
                v -= 3 + danger * 1.5 + (bar[o] > 0 ? 4 : 0);
            }
        }
        return v;
    }

    public TStep? BotStep(Random rnd)
    {
        if (seqs == null) return null;
        var opts = seqs.Where(q => q.Count > 0).ToList();
        if (opts.Count == 0) return null;
        var best = opts[0]; double bestV = double.NegativeInfinity;
        foreach (var q in opts)
        {
            var st = this;
            foreach (var step in q) st = st.Apply(step);
            double v = st.Score(turn, rnd) + rnd.NextDouble() * 0.5;
            if (v > bestV) { bestV = v; best = q; }
        }
        return best[0];
    }

    public Dictionary<string, object> View(int seat)
    {
        bool mine = seat == turn;
        var d = new Dictionary<string, object>
        {
            ["pts"] = pts.ToList(), ["bar"] = bar.ToList(), ["off"] = off.ToList(), ["turn"] = turn, ["you"] = seat,
            ["rolled"] = rolled?.ToList(), ["dice"] = new List<int>(dice),
            ["steps"] = mine ? NextSteps().Select(x => (object)new List<int> { x.from, x.to }).ToList() : new List<object>(),
            ["pips"] = new List<int> { PipCount(0), PipCount(1) }, ["variant"] = "aada",
        };
        if (mine && rolled != null) d["combos"] = TawlaCombos.Of(this, OFF);
        return d;
    }
}

/* ---------------- محبوسة, ٣١ and جلبهار ---------------- */
public class Tables : ITawla
{
    public const int OFF = 99;
    public string v;
    public int[][] c = { new int[24], new int[24] };
    public int[] pinned = Enumerable.Repeat(-1, 24).ToArray();
    public int[] off = new int[2];
    public int turn;
    public int[] rolled;
    public List<int> dice = new List<int>(), later = new List<int>();
    public int[] rolls = new int[2];
    public int carry = -1;

    public int Turn => turn;
    public int[] Rolled => rolled;
    public int OpponentBar => 0;

    bool SameWay => v != "mahbousa";
    public int PointOf(int p, int r) => p == 0 ? r - 1 : SameWay ? (r - 1 + 12) % 24 : 24 - r;
    public int PathOf(int p, int pt) => p == 0 ? pt + 1 : SameWay ? ((pt - 12 + 24) % 24) + 1 : 24 - pt;

    public static Tables Start(string variant, int first)
    {
        var s = new Tables { v = variant, turn = first };
        s.c[0][s.PointOf(0, 24)] = 15; s.c[1][s.PointOf(1, 24)] = 15;
        return s;
    }

    Tables Clone() => new Tables
    {
        v = v, c = new[] { (int[])c[0].Clone(), (int[])c[1].Clone() }, pinned = (int[])pinned.Clone(), off = (int[])off.Clone(), turn = turn,
        rolled = rolled, dice = new List<int>(dice), later = new List<int>(later), rolls = (int[])rolls.Clone(), carry = carry,
    };

    bool MovableAt(int p, int pt)
    {
        int mine = c[p][pt];
        if (mine == 0) return false;
        return !(pinned[pt] == p && mine == 1 && c[1 - p][pt] > 0);
    }

    bool AllHome(int p) { for (int pt = 0; pt < 24; pt++) if (c[p][pt] > 0 && PathOf(p, pt) > 6) return false; return true; }

    bool PostilionBlocks(int p, int fromR)
    {
        if (v != "tawla31" || fromR != 24) return false;
        int outN = 0; bool passed = false;
        for (int pt = 0; pt < 24; pt++)
        {
            int n = c[p][pt]; if (n == 0) continue;
            int r = PathOf(p, pt);
            if (r < 24) { outN += n; if (r < 12) passed = true; }
        }
        return outN > 0 && !passed && off[p] == 0;
    }

    bool MakesWall(int p)
    {
        int o = 1 - p, run = 0; bool behind = false;
        for (int r = 24; r >= 1; r--)
        {
            int pt = PointOf(o, r);
            if (c[o][pt] > 0) { behind = true; run = 0; continue; }
            if (c[p][pt] > 0) { run++; if (run >= 6 && behind) return true; } else run = 0;
        }
        return false;
    }

    bool CanLand(int p, int pt)
    {
        int o = 1 - p;
        if (c[o][pt] == 0) return true;
        return v == "mahbousa" && c[o][pt] == 1 && c[p][pt] == 0;
    }

    List<TStep> SingleSteps(int die)
    {
        int p = turn; var o = new List<TStep>();
        bool home = AllHome(p);
        int highest = 0;
        if (home) for (int pt = 0; pt < 24; pt++) if (c[p][pt] > 0) highest = Math.Max(highest, PathOf(p, pt));
        for (int pt = 0; pt < 24; pt++)
        {
            if (!MovableAt(p, pt)) continue;
            int r = PathOf(p, pt), t = r - die;
            if (PostilionBlocks(p, r)) continue;
            if (t >= 1)
            {
                int to = PointOf(p, t);
                if (!CanLand(p, to)) continue;
                if (v == "tawla31" && Apply(new TStep(pt, to, die)).MakesWall(p)) continue;
                o.Add(new TStep(pt, to, die));
            }
            else if (home && (t == 0 || r == highest)) o.Add(new TStep(pt, OFF, die));
        }
        return o;
    }

    Tables Apply(TStep st)
    {
        var n = Clone(); int p = turn, o = 1 - p;
        n.c[p][st.from]--;
        if (n.c[p][st.from] == 0 && n.pinned[st.from] == o) n.pinned[st.from] = -1;
        if (st.to == OFF) n.off[p]++;
        else
        {
            if (v == "mahbousa" && n.c[o][st.to] == 1 && n.c[p][st.to] == 0) n.pinned[st.to] = o;
            n.c[p][st.to]++;
        }
        return n;
    }

    string Key() => string.Join(",", c[0]) + "|" + string.Join(",", c[1]) + "|" + string.Join(",", pinned) + "|" + off[0] + "," + off[1];

    List<List<TStep>> Sequences()
    {
        var results = new List<List<TStep>>();
        int best = 0;
        var seen = new HashSet<string>();
        void Go(Tables st, List<int> ds, List<TStep> path)
        {
            var sorted = ds.OrderBy(x => x).ToList();
            var k = st.Key() + "#" + string.Join("", sorted);
            if (!seen.Add(k)) return;
            bool moved = false;
            foreach (var d in ds.Distinct().ToList())
                foreach (var step in st.SingleSteps(d))
                {
                    moved = true;
                    var rest = new List<int>(ds); rest.Remove(d);
                    Go(st.Apply(step), rest, new List<TStep>(path) { step });
                }
            if (!moved)
            {
                if (path.Count > best) { best = path.Count; results.Clear(); }
                if (path.Count == best) results.Add(path);
            }
        }
        Go(this, dice, new List<TStep>());
        if (best == 1 && dice.Count == 2 && dice[0] != dice[1])
        {
            int big = Math.Max(dice[0], dice[1]);
            if (results.Any(q => q[0].die == big)) return results.Where(q => q[0].die == big).ToList();
        }
        return results;
    }

    public List<TStep> NextSteps()
    {
        var o = new List<TStep>();
        if (rolled == null || dice.Count == 0) return o;
        var seenK = new HashSet<int>();
        foreach (var q in Sequences()) if (q.Count > 0 && seenK.Add(q[0].from * 1000 + q[0].to)) o.Add(q[0]);
        return o;
    }

    public ITawla Roll(Random rnd)
    {
        int a = 1 + rnd.Next(6), b = 1 + rnd.Next(6);
        var n = Clone();
        n.rolled = new[] { a, b };
        n.rolls[turn]++;
        n.later = new List<int>();
        if (a != b) n.dice = new List<int> { a, b };
        else
        {
            n.dice = new List<int> { a, a, a, a };
            if (v == "gulbahar" && n.rolls[turn] > 3) for (int d = a + 1; d <= 6; d++) n.later.AddRange(new[] { d, d, d, d });
        }
        return n;
    }

    public ITawla PlayStep(int from, int to)
    {
        var cand = NextSteps().Where(x => x.from == from && x.to == to).ToList();
        if (cand.Count == 0) return null;
        var n = Apply(cand[0]);
        n.dice.Remove(cand[0].die);
        return n;
    }

    public bool TurnDone() => rolled != null && (dice.Count == 0 || NextSteps().Count == 0);

    public ITawla EndTurn() => EndTurnT();
    Tables EndTurnT()
    {
        var n = Clone();
        bool blocked = n.dice.Count > 0;
        if (n.v == "gulbahar" && n.rolled != null && n.rolled[0] == n.rolled[1])
        {
            if (!blocked && n.later.Count > 0)
            {
                n.dice = n.later.Take(4).ToList(); n.later = n.later.Skip(4).ToList();
                if (n.NextSteps().Count > 0) return n;
                return n.EndTurnT();
            }
            if (blocked && n.carry < 0 && n.rolls[n.turn] > 3)
            {
                int o = 1 - n.turn;
                n.turn = o; n.carry = o;
                if (n.NextSteps().Count > 0) return n;
                if (n.later.Count > 0) { n.dice = n.later.Take(4).ToList(); n.later = n.later.Skip(4).ToList(); if (n.NextSteps().Count > 0) return n; }
                n.dice = new List<int>(); n.later = new List<int>();
            }
        }
        if (n.carry >= 0)
        {
            n.turn = n.carry; n.carry = -1; n.rolled = null; n.dice = new List<int>(); n.later = new List<int>();
            return n;
        }
        n.turn = 1 - n.turn; n.rolled = null; n.dice = new List<int>(); n.later = new List<int>();
        return n;
    }

    public (int winner, int points, string kind)? Winner()
    {
        if (v == "mahbousa") for (int p = 0; p < 2; p++)
        {
            int o = 1 - p, start = PointOf(o, 24);
            if (pinned[start] == o && c[o][start] == 1 && c[p][start] > 0) return (p, 2, "mother");
        }
        for (int p = 0; p < 2; p++)
        {
            if (off[p] < 15) continue;
            int o = 1 - p;
            if (v == "tawla31") return (p, 15 - off[o], "count");
            bool mars = off[o] == 0;
            return (p, mars ? 2 : 1, mars ? "mars" : "win");
        }
        return null;
    }

    public int PipCount(int p) { int n = 0; for (int pt = 0; pt < 24; pt++) n += c[p][pt] * PathOf(p, pt); return n; }

    double Evaluate(int p)
    {
        int o = 1 - p;
        double val = (PipCount(o) - PipCount(p)) + off[p] * 8;
        for (int pt = 0; pt < 24; pt++)
        {
            if (v == "mahbousa")
            {
                if (pinned[pt] == o) val += 18 + (PathOf(o, pt) > 18 ? 10 : 0);
                if (pinned[pt] == p) val -= 18;
                if (c[p][pt] == 1 && pinned[pt] != o && PathOf(p, pt) > 6)
                {
                    for (int d = 1; d <= 6; d++)
                    {
                        int r = PathOf(o, pt) + d;
                        if (r <= 24 && c[o][PointOf(o, r)] > 0 && MovableAt(o, PointOf(o, r))) { val -= 6; break; }
                    }
                }
                if (c[p][pt] >= 2) val += 2;
            }
            else if (c[p][pt] > 0) val += 1.5;
        }
        return val;
    }

    public TStep? BotStep(Random rnd)
    {
        var seqs = Sequences().Where(q => q.Count > 0).ToList();
        if (seqs.Count == 0) return null;
        var best = seqs[0]; double bv = double.NegativeInfinity;
        foreach (var q in seqs.Take(400))
        {
            var st = this;
            foreach (var step in q) st = st.Apply(step);
            double val = st.Evaluate(turn) + rnd.NextDouble() * 0.5;
            if (val > bv) { bv = val; best = q; }
        }
        return best[0];
    }

    public Dictionary<string, object> View(int seat)
    {
        bool mine = seat == turn;
        var pts = new List<int>();
        for (int i = 0; i < 24; i++)
        {
            int a = c[0][i], b = c[1][i];
            pts.Add(a > 0 && b > 0 ? (pinned[i] == 0 ? -b : a) : a > 0 ? a : -b);
        }
        var d = new Dictionary<string, object>
        {
            ["pts"] = pts, ["bar"] = new List<int> { 0, 0 }, ["off"] = off.ToList(), ["turn"] = turn, ["you"] = seat,
            ["rolled"] = rolled?.ToList(), ["dice"] = new List<int>(dice),
            ["steps"] = mine ? NextSteps().Select(x => (object)new List<int> { x.from, x.to }).ToList() : new List<object>(),
            ["pips"] = new List<int> { PipCount(0), PipCount(1) }, ["variant"] = v,
        };
        if (mine && rolled != null) d["combos"] = TawlaCombos.Of(this, OFF);
        if (v == "mahbousa") d["pinned"] = pinned.ToList();
        if (later.Count > 0) d["later"] = new List<int>(later);
        if (carry >= 0) d["carry"] = true;
        return d;
    }
}

/// Moves that carry one checker on with two or more dice in a row, each stop on the way a legal step
/// (defs.ts combosOf): [from, to, the points it passes through].
public static class TawlaCombos
{
    public static List<object> Of(ITawla s, int off)
    {
        var first = s.NextSteps();
        var single = new HashSet<int>(first.Select(x => x.from * 1000 + x.to));
        var outKeys = new HashSet<int>();
        var o = new List<object>();
        void Walk(ITawla st, int start, int at, List<int> via)
        {
            if (via.Count >= 3) return;
            foreach (var x in st.NextSteps())
            {
                if (x.from != at) continue;
                var n = st.PlayStep(at, x.to);
                if (n == null || n.Turn != s.Turn) continue;
                int key = start * 1000 + x.to;
                if (via.Count > 0 && !single.Contains(key) && outKeys.Add(key)) o.Add(new List<object> { start, x.to, new List<int>(via) });
                if (x.to != off) Walk(n, start, x.to, new List<int>(via) { x.to });
            }
        }
        foreach (var f in first.Select(x => x.from).Distinct().ToList()) Walk(s, f, f, new List<int>());
        return o;
    }
}
