// Ludo for 2 to 4 players, as in shared/games/ludo.ts (classic, quick and master).
// A pawn's progress: -1 in base, 0..50 on the shared track, 51..55 its home column, 56 home; -2 (master) just before its start.
using System;
using System.Collections.Generic;
using System.Linq;

public class LudoState
{
    public const int TRACK = 52, HOME = 56, LAP = -2;
    public static readonly int[] COLOR_OFFSET = { 0, 13, 26, 39 };
    public static readonly HashSet<int> SAFE = new HashSet<int> { 0, 8, 13, 21, 26, 34, 39, 47 };

    public string variant = "classic";
    public int[] captures;
    public int[] colors;
    public int[][] pawns;
    public int turn;
    public int die = -1;          // -1: not rolled yet
    public int sixes;
    public List<int> finished = new List<int>();

    public static string VariantOf(string mode) => mode.StartsWith("ludoq") ? "quick" : mode.StartsWith("ludom") ? "master" : "classic";

    public static LudoState New(int players, string variant, int first)
    {
        var colors = players == 2 ? new[] { 0, 2 } : players == 3 ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 3 };
        var start = variant == "quick" ? new[] { 0, -1, -1, -1 } : new[] { -1, -1, -1, -1 };
        return new LudoState { variant = variant, captures = new int[colors.Length], colors = colors, pawns = colors.Select(_ => (int[])start.Clone()).ToArray(), turn = first };
    }

    LudoState Clone() => new LudoState { variant = variant, captures = (int[])captures.Clone(), colors = colors, pawns = pawns.Select(a => (int[])a.Clone()).ToArray(), turn = turn, die = die, sixes = sixes, finished = new List<int>(finished) };

    public int Square(int seat, int prog)
    {
        if (prog == LAP) return (COLOR_OFFSET[colors[seat]] + 51) % TRACK;
        if (prog < 0 || prog > 50) return -1;
        return (COLOR_OFFSET[colors[seat]] + prog) % TRACK;
    }

    bool Circling(int seat, int p) => variant == "master" && captures[seat] == 0 && (p == LAP || (p >= 0 && p <= 50));

    public List<int> Movable()
    {
        var o = new List<int>();
        if (die < 0) return o;
        int seat = turn;
        for (int i = 0; i < 4; i++)
        {
            int p = pawns[seat][i];
            if (p == HOME) continue;
            if (p == -1) { if (die == 6) o.Add(i); continue; }
            if (Circling(seat, p)) { o.Add(i); continue; }
            if (p + die <= HOME) o.Add(i);
        }
        return o;
    }

    int Advance(int seat, int p, int d)
    {
        if (p == -1) return 0;
        if (Circling(seat, p))
        {
            int rel = (p == LAP ? 51 : p) + d;
            if (rel <= 50) return rel;
            int r = rel % 52;
            return r == 51 ? LAP : r;
        }
        return p + d;
    }

    public LudoState Roll(Random rnd)
    {
        int d = 1 + rnd.Next(6);
        var n = Clone(); n.die = d; n.sixes = d == 6 ? sixes + 1 : sixes;
        return n;
    }

    LudoState NextTurn()
    {
        var n = Clone();
        int cnt = pawns.Length, t = turn;
        for (int k = 0; k < cnt; k++) { t = (t + 1) % cnt; if (!finished.Contains(t)) break; }
        n.turn = t; n.die = -1; n.sixes = 0;
        return n;
    }

    public LudoState PassTurn()
    {
        if (die == 6 && sixes < 3) { var n = Clone(); n.die = -1; return n; }   // a 6 still earns another roll
        return NextTurn();
    }

    public LudoState Move(int pawn, out List<(int seat, int pawn)> captured, out bool home)
    {
        captured = new List<(int, int)>(); home = false;
        int seat = turn, d = die;
        if (d < 0 || !Movable().Contains(pawn)) return null;
        var n = Clone();
        int cur = n.pawns[seat][pawn];
        int next = Advance(seat, cur, d);
        n.pawns[seat][pawn] = next;
        int sq = Square(seat, next);
        if (sq >= 0 && !SAFE.Contains(sq))
        {
            for (int os = 0; os < pawns.Length; os++)
            {
                if (os == seat) continue;
                for (int pi = 0; pi < 4; pi++) if (Square(os, pawns[os][pi]) == sq) captured.Add((os, pi));
            }
            // two or more pawns of one colour form a block that can't be captured
            var caps = captured;
            captured = caps.Where(c => caps.Count(x => x.seat == c.seat) < 2).ToList();
            foreach (var c in captured) n.pawns[c.seat][c.pawn] = -1;
        }
        home = next == HOME;
        n.captures[seat] += captured.Count;
        bool done = variant == "quick" ? home : n.pawns[seat].All(p => p == HOME);
        if (done && !n.finished.Contains(seat)) n.finished.Add(seat);
        bool again = (d == 6 && sixes < 3) || captured.Count > 0 || home;
        if (n.finished.Contains(seat) || !again || sixes >= 3) n = n.NextTurn();
        else { n.die = -1; n.sixes = d == 6 ? sixes : 0; }
        return n;
    }

    public int Winner() => finished.Count > 0 ? finished[0] : -1;

    public int BotPawn(Random rnd)
    {
        var opts = Movable();
        if (opts.Count == 0) return -1;
        int best = opts[0]; double bestV = double.NegativeInfinity;
        int Prog(int x) => x == LAP ? 51 : x;
        foreach (var i in opts)
        {
            var r = Move(i, out var cap, out var home);
            int cur = pawns[turn][i], nxt = r.pawns[turn][i];
            double v = cap.Count * (variant == "master" && captures[turn] == 0 ? 160 : 100) + (home ? (variant == "quick" ? 400 : 80) : 0) + (cur == -1 ? 60 : 0) + Prog(nxt) * 0.6;
            if (cur >= 0 && nxt >= 0 && nxt < Prog(cur) && nxt <= 50) v -= 30;
            int sq = Square(turn, nxt);
            if (sq >= 0 && SAFE.Contains(sq)) v += 15;
            if (nxt > 50) v += 25;
            if (sq >= 0 && !SAFE.Contains(sq))
            {
                for (int os = 0; os < pawns.Length; os++)
                {
                    if (os == turn) continue;
                    foreach (var p in pawns[os]) { int o = Square(os, p); if (o >= 0) { int dd = (sq - o + TRACK) % TRACK; if (dd >= 1 && dd <= 6) v -= 20; } }
                }
            }
            v += rnd.NextDouble() * 3;
            if (v > bestV) { bestV = v; best = i; }
        }
        return best;
    }

    public Dictionary<string, object> View(int seat)
    {
        var d = new Dictionary<string, object>
        {
            ["colors"] = colors.ToList(), ["pawns"] = pawns.Select(a => (object)a.ToList()).ToList(), ["turn"] = turn,
            ["finished"] = new List<int>(finished), ["you"] = seat,
            ["movable"] = seat == turn && sixes < 3 ? Movable() : new List<int>(), ["variant"] = variant, ["captures"] = captures.ToList(),
        };
        if (die >= 0) d["die"] = die;
        return d;
    }
}
