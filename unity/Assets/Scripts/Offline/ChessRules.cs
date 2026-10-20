// Full chess rules (shared/games/chess.ts): legal moves with castling, en passant and promotion, check,
// mate, stalemate and draws, plus the same small alpha-beta computer player. Squares 0..63, a1 = 0, a8 = 56.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public struct CMove { public int from, to; public char promo; public CMove(int f, int t, char p = '\0') { from = f; to = t; promo = p; } }

public class ChessPos
{
    public char[] b = new char[64];     // '\0' or a piece letter: PNBRQK white, pnbrqk black
    public char turn = 'w';
    public string castle = "KQkq";
    public int ep = -1, half, full = 1;
    public List<string> hist = new List<string>();
}

public static class Chess
{
    static readonly int[][] Nn = { new[] { 2, 1 }, new[] { 1, 2 }, new[] { -1, 2 }, new[] { -2, 1 }, new[] { -2, -1 }, new[] { -1, -2 }, new[] { 1, -2 }, new[] { 2, -1 } };
    static readonly int[][] KD = { new[] { 1, 0 }, new[] { 1, 1 }, new[] { 0, 1 }, new[] { -1, 1 }, new[] { -1, 0 }, new[] { -1, -1 }, new[] { 0, -1 }, new[] { 1, -1 } };
    static readonly int[][] ROOK = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
    static readonly int[][] BISHOP = { new[] { 1, 1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { -1, -1 } };
    static readonly int[][] QUEEN = ROOK.Concat(BISHOP).ToArray();
    static int Rk(int s) => s >> 3;
    static int Fl(int s) => s & 7;
    static bool On(int r, int f) => r >= 0 && r < 8 && f >= 0 && f < 8;
    static char ColorOf(char p) => p == '\0' ? '\0' : char.IsUpper(p) ? 'w' : 'b';
    static char Other(char c) => c == 'w' ? 'b' : 'w';

    public static ChessPos Start()
    {
        var s = new ChessPos();
        const string back = "RNBQKBNR";
        for (int f = 0; f < 8; f++)
        {
            s.b[f] = back[f]; s.b[8 + f] = 'P';
            s.b[48 + f] = 'p'; s.b[56 + f] = char.ToLower(back[f]);
        }
        s.hist.Add(Key(s));
        return s;
    }

    static string Key(ChessPos s)
    {
        var sb = new StringBuilder(72);
        foreach (var p in s.b) sb.Append(p == '\0' ? '.' : p);
        sb.Append(s.turn).Append(s.castle).Append(s.ep);
        return sb.ToString();
    }

    public static bool Attacked(char[] b, int sq, char by)
    {
        int r = Rk(sq), f = Fl(sq);
        char P = by == 'w' ? 'P' : 'p', N = by == 'w' ? 'N' : 'n', K = by == 'w' ? 'K' : 'k';
        char R = by == 'w' ? 'R' : 'r', B = by == 'w' ? 'B' : 'b', Q = by == 'w' ? 'Q' : 'q';
        int pr = by == 'w' ? r - 1 : r + 1;
        foreach (var df in new[] { -1, 1 }) if (On(pr, f + df) && b[pr * 8 + f + df] == P) return true;
        foreach (var d in Nn) if (On(r + d[0], f + d[1]) && b[(r + d[0]) * 8 + f + d[1]] == N) return true;
        foreach (var d in KD) if (On(r + d[0], f + d[1]) && b[(r + d[0]) * 8 + f + d[1]] == K) return true;
        for (int pass = 0; pass < 2; pass++)
        {
            var dirs = pass == 0 ? ROOK : BISHOP;
            char a1 = pass == 0 ? R : B;
            foreach (var d in dirs)
            {
                int rr = r + d[0], ff = f + d[1];
                while (On(rr, ff))
                {
                    char p = b[rr * 8 + ff];
                    if (p != '\0') { if (p == a1 || p == Q) return true; break; }
                    rr += d[0]; ff += d[1];
                }
            }
        }
        return false;
    }

    public static int KingSquare(char[] b, char c) => Array.IndexOf(b, c == 'w' ? 'K' : 'k');
    public static bool InCheck(ChessPos s) => InCheck(s, s.turn);
    public static bool InCheck(ChessPos s, char c) { int k = KingSquare(s.b, c); return k >= 0 && Attacked(s.b, k, Other(c)); }

    static List<CMove> Pseudo(ChessPos s)
    {
        var o = new List<CMove>(48);
        var b = s.b; char me = s.turn, them = Other(me);
        void Add(int from, int to)
        {
            if (char.ToLower(b[from]) == 'p' && (Rk(to) == 7 || Rk(to) == 0)) foreach (var pr in "qrbn") o.Add(new CMove(from, to, pr));
            else o.Add(new CMove(from, to));
        }
        for (int sq = 0; sq < 64; sq++)
        {
            char p = b[sq]; if (p == '\0' || ColorOf(p) != me) continue;
            int r = Rk(sq), f = Fl(sq); char t = char.ToLower(p);
            if (t == 'p')
            {
                int dir = me == 'w' ? 1 : -1, start = me == 'w' ? 1 : 6;
                if (On(r + dir, f) && b[(r + dir) * 8 + f] == '\0')
                {
                    Add(sq, (r + dir) * 8 + f);
                    if (r == start && b[(r + 2 * dir) * 8 + f] == '\0') o.Add(new CMove(sq, (r + 2 * dir) * 8 + f));
                }
                foreach (var df in new[] { -1, 1 })
                {
                    if (!On(r + dir, f + df)) continue;
                    int to = (r + dir) * 8 + f + df;
                    if ((b[to] != '\0' && ColorOf(b[to]) == them) || to == s.ep) Add(sq, to);
                }
            }
            else if (t == 'n' || t == 'k')
            {
                foreach (var d in t == 'n' ? Nn : KD)
                {
                    if (!On(r + d[0], f + d[1])) continue;
                    int to = (r + d[0]) * 8 + f + d[1];
                    if (b[to] == '\0' || ColorOf(b[to]) == them) o.Add(new CMove(sq, to));
                }
            }
            else
            {
                var dirs = t == 'r' ? ROOK : t == 'b' ? BISHOP : QUEEN;
                foreach (var d in dirs)
                {
                    int rr = r + d[0], ff = f + d[1];
                    while (On(rr, ff))
                    {
                        int to = rr * 8 + ff;
                        if (b[to] != '\0') { if (ColorOf(b[to]) == them) o.Add(new CMove(sq, to)); break; }
                        o.Add(new CMove(sq, to));
                        rr += d[0]; ff += d[1];
                    }
                }
            }
        }
        // castling
        int home = me == 'w' ? 0 : 56; char K = me == 'w' ? 'K' : 'k', Rr = me == 'w' ? 'R' : 'r';
        if (b[home + 4] == K && !Attacked(b, home + 4, them))
        {
            char ks = me == 'w' ? 'K' : 'k', qs = me == 'w' ? 'Q' : 'q';
            if (s.castle.IndexOf(ks) >= 0 && b[home + 5] == '\0' && b[home + 6] == '\0' && b[home + 7] == Rr && !Attacked(b, home + 5, them) && !Attacked(b, home + 6, them)) o.Add(new CMove(home + 4, home + 6));
            if (s.castle.IndexOf(qs) >= 0 && b[home + 3] == '\0' && b[home + 2] == '\0' && b[home + 1] == '\0' && b[home] == Rr && !Attacked(b, home + 3, them) && !Attacked(b, home + 2, them)) o.Add(new CMove(home + 4, home + 2));
        }
        return o;
    }

    /// Plays a move and returns the new position (the input is not changed). Does not check legality.
    public static ChessPos Make(ChessPos s, CMove m, bool keepHistory = true)
    {
        var b = (char[])s.b.Clone(); char p = b[m.from], t = char.ToLower(p), me = s.turn;
        string castle = s.castle; int ep = -1, half = s.half + 1;
        char captured = b[m.to];
        if (t == 'p' || captured != '\0') half = 0;
        if (t == 'p' && m.to == s.ep && captured == '\0') b[m.to + (me == 'w' ? -8 : 8)] = '\0';
        b[m.to] = m.promo != '\0' ? (me == 'w' ? char.ToUpper(m.promo) : m.promo) : p;
        b[m.from] = '\0';
        if (t == 'p' && Math.Abs(m.to - m.from) == 16) ep = (m.to + m.from) / 2;
        if (t == 'k')
        {
            castle = me == 'w' ? castle.Replace("K", "").Replace("Q", "") : castle.Replace("k", "").Replace("q", "");
            if (m.to - m.from == 2) { b[m.from + 1] = b[m.from + 3]; b[m.from + 3] = '\0'; }
            if (m.from - m.to == 2) { b[m.from - 1] = b[m.from - 4]; b[m.from - 4] = '\0'; }
        }
        if (m.from == 0 || m.to == 0) castle = castle.Replace("Q", "");
        if (m.from == 7 || m.to == 7) castle = castle.Replace("K", "");
        if (m.from == 56 || m.to == 56) castle = castle.Replace("q", "");
        if (m.from == 63 || m.to == 63) castle = castle.Replace("k", "");
        var n = new ChessPos { b = b, turn = Other(me), castle = castle, ep = ep, half = half, full = s.full + (me == 'b' ? 1 : 0) };
        if (keepHistory) { n.hist = new List<string>(s.hist); n.hist.Add(Key(n)); }
        else n.hist = null;
        return n;
    }

    public static List<CMove> Legal(ChessPos s) => Pseudo(s).Where(m => !InCheck(Make(s, m, false), s.turn)).ToList();

    /// null while playing; otherwise how it ended and who won ('w', 'b', or '\0' for a draw).
    public static (string kind, char winner)? Outcome(ChessPos s)
    {
        if (Legal(s).Count == 0) return InCheck(s) ? ("mate", Other(s.turn)) : ("stalemate", '\0');
        if (s.half >= 100) return ("fifty", '\0');
        if (s.hist != null && s.hist.Count > 0)
        {
            var k = s.hist[s.hist.Count - 1];
            if (s.hist.Count(x => x == k) >= 3) return ("repetition", '\0');
        }
        var rest = s.b.Where(p => p != '\0' && char.ToLower(p) != 'k').ToList();
        if (rest.Count == 0 || (rest.Count == 1 && "bnBN".IndexOf(rest[0]) >= 0)) return ("material", '\0');
        return null;
    }

    /* ---------- computer player ---------- */

    static int Val(char t) => t == 'p' ? 100 : t == 'n' ? 320 : t == 'b' ? 330 : t == 'r' ? 500 : t == 'q' ? 900 : 0;
    static readonly int[] CENTER = { 0, 1, 2, 3, 3, 2, 1, 0 };
    static int Pst(char t, int sq, char c)
    {
        int r = c == 'w' ? Rk(sq) : 7 - Rk(sq), f = Fl(sq);
        switch (t)
        {
            case 'p': return r * 8 + (f >= 2 && f <= 5 ? r * 2 : 0);
            case 'n': return (CENTER[r] + CENTER[f]) * 6 - 10;
            case 'b': return (CENTER[r] + CENTER[f]) * 4;
            case 'r': return r == 6 ? 20 : 0;
            case 'q': return (CENTER[r] + CENTER[f]) * 2;
            case 'k': return r == 0 ? (f == 6 || f == 2 ? 25 : 5) : -15 * r;
        }
        return 0;
    }
    static int Evaluate(ChessPos s)
    {
        int score = 0;
        for (int sq = 0; sq < 64; sq++)
        {
            char p = s.b[sq]; if (p == '\0') continue;
            char c = ColorOf(p), t = char.ToLower(p);
            int v = Val(t) + Pst(t, sq, c);
            score += c == 'w' ? v : -v;
        }
        return s.turn == 'w' ? score : -score;
    }
    static List<CMove> Order(ChessPos s, List<CMove> ms)
    {
        int K(CMove m) => (s.b[m.to] != '\0' ? 10 * Val(char.ToLower(s.b[m.to])) - Val(char.ToLower(s.b[m.from])) : 0) + (m.promo == 'q' ? 800 : 0);
        return ms.OrderByDescending(K).ToList();    // a stable sort, like the server's
    }
    static int Search(ChessPos s, int depth, int alpha, int beta, int ply)
    {
        var ms = Legal(s);
        if (ms.Count == 0) return InCheck(s) ? -100000 + ply : 0;
        if (depth == 0) return Evaluate(s);
        foreach (var m in Order(s, ms))
        {
            int v = -Search(Make(s, m, false), depth - 1, -beta, -alpha, ply + 1);
            if (v >= beta) return beta;
            if (v > alpha) alpha = v;
        }
        return alpha;
    }
    public static CMove BestMove(ChessPos s, int depth, Random rng)
    {
        var moves = Legal(s);
        for (int i = moves.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (moves[i], moves[j]) = (moves[j], moves[i]); }
        var ms = Order(s, moves);
        var best = ms[0]; int bestV = int.MinValue + 1;
        foreach (var m in ms)
        {
            int v = -Search(Make(s, m, false), depth - 1, -1000000, -bestV, 1);
            if (v > bestV) { bestV = v; best = m; }
        }
        return best;
    }
}
