// Domino practice on the phone: the server's domino table (shared/table.ts) and its computer player (shared/bot.ts).
using System;
using System.Collections.Generic;
using System.Linq;

public class OfflineDomino : OfflineGame
{
    readonly List<SeatInfo> seats;
    readonly int n, target;
    string phase = "lobby";
    List<int>[] hands;
    List<int> yard = new List<int>();
    Domino.Line line = new Domino.Line();
    int turn = -1, passes, round, starter = -1, opening = -1;
    int[] scores;
    int[] lastPlayedValue;
    double turnEndsAt, nextAt;
    Dictionary<string, object> last, result, match;

    public OfflineDomino(string mode, string name, int ch) : base(mode)
    {
        n = Modes.Players(mode);
        target = Modes.DefaultTarget(mode);
        seats = MakeSeats(n, name, ch);
        scores = new int[Modes.TeamCount(mode)];
        StartRound();
        Bump();
    }

    static int Pips(int id) => Domino.Tiles[id][0] + Domino.Tiles[id][1];
    int NextSeat(int s) => (s + 1) % n;

    void StartRound()
    {
        round++;
        bool three = mode == "ffa3";
        var deck = Enumerable.Range(0, 28).Where(i => !three || i != 0).ToList();
        for (int i = deck.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (deck[i], deck[j]) = (deck[j], deck[i]); }
        int size = three ? 9 : 7;
        hands = new List<int>[n];
        for (int s = 0; s < n; s++) { hands[s] = deck.GetRange(0, size); deck.RemoveRange(0, size); hands[s].Sort(); }
        yard = deck;
        line = new Domino.Line();
        passes = 0; result = null;
        lastPlayedValue = Enumerable.Repeat(-1, n).ToArray();
        int start = starter;
        opening = -1;
        if (start < 0)
        {
            // the heaviest double opens; with no double at all, the heaviest tile
            int best = -1;
            for (int s = 0; s < n; s++) foreach (var id in hands[s])
            {
                int val = Domino.IsDouble(id) ? 100 + Domino.Tiles[id][0] : Pips(id);
                if (val > best) { best = val; start = s; opening = id; }
            }
        }
        starter = start;
        phase = "play";
        last = new Dictionary<string, object> { ["t"] = "deal", ["s"] = start, ["k"] = ++v };
        BeginTurn(start);
    }

    void BeginTurn(int seat)
    {
        turn = seat;
        turnEndsAt = seats[seat].bot ? 0 : Now + TurnMs;
    }

    static void Place(Domino.Line l, int id, char e)
    {
        int a = Domino.Tiles[id][0], b = Domino.Tiles[id][1];
        if (e == 'C') { l.f = id; l.fo0 = a; l.fo1 = b; return; }
        int val = e == 'L' ? Domino.LeftVal(l) : Domino.RightVal(l);
        var t = new Domino.ArmTile(id, val, a == val ? b : a);
        if (e == 'L') l.L.Add(t); else l.R.Add(t);
    }

    public override string Act(Dictionary<string, object> a)
    {
        if (phase != "play") return "not_playing";
        if (turn != 0) return "not_your_turn";
        var err = DoMove(0, a);
        if (err == null) Bump();
        return err;
    }

    string DoMove(int seat, Dictionary<string, object> a)
    {
        var hand = hands[seat];
        var t = S(a, "t");
        if (t == "play")
        {
            int id = N(a, "id");
            if (!hand.Contains(id)) return "no_tile";
            if (line.Empty && opening >= 0 && id != opening) return "must_open";
            var ends = Domino.EndsFor(line, id);
            if (ends.Count == 0) return "illegal_move";
            var es = S(a, "e");
            char e = es.Length > 0 && ends.Contains(es[0]) ? es[0] : ends.Count == 1 ? ends[0] : ' ';
            if (e == ' ') return "choose_end";
            Place(line, id, e);
            if (e == 'L') lastPlayedValue[seat] = line.L[line.L.Count - 1].outer;
            if (e == 'R') lastPlayedValue[seat] = line.R[line.R.Count - 1].outer;
            hand.Remove(id);
            passes = 0;
            last = new Dictionary<string, object> { ["t"] = "play", ["s"] = seat, ["id"] = id, ["e"] = e.ToString(), ["k"] = ++v };
            if (hand.Count == 0) { EndRound(seat, false); return null; }
            BeginTurn(NextSeat(seat));
            return null;
        }
        if (t == "draw")
        {
            if (Domino.CanPlay(line, hand)) return "can_play";
            if (yard.Count == 0) return "yard_empty";
            hand.Add(yard[yard.Count - 1]); yard.RemoveAt(yard.Count - 1);
            hand.Sort();
            last = new Dictionary<string, object> { ["t"] = "draw", ["s"] = seat, ["k"] = ++v };
            if (turnEndsAt > 0) turnEndsAt = Math.Max(turnEndsAt, Now + Math.Min(5000, TurnMs));
            return null;
        }
        if (t == "pass")
        {
            if (Domino.CanPlay(line, hand)) return "can_play";
            if (yard.Count > 0) return "must_draw";
            passes++;
            last = new Dictionary<string, object> { ["t"] = "pass", ["s"] = seat, ["k"] = ++v };
            if (passes >= n) { EndRound(-1, true); return null; }
            BeginTurn(NextSeat(seat));
            return null;
        }
        return "bad_action";
    }

    /* ---------- the computer player (shared/bot.ts) ---------- */

    Dictionary<string, object> ChooseMove(int seat)
    {
        var hand = hands[seat];
        if (line.Empty && opening >= 0 && hand.Contains(opening))
            return new Dictionary<string, object> { ["t"] = "play", ["id"] = opening, ["e"] = "C" };
        int partner = mode == "team" ? (seat + 2) % 4 : -1;
        int partnerLast = partner >= 0 ? lastPlayedValue[partner] : -1;
        var options = new List<(int id, char e)>();
        foreach (var id in hand) foreach (var e in Domino.EndsFor(line, id)) options.Add((id, e));
        if (options.Count == 0) return Do(yard.Count > 0 ? "draw" : "pass");
        int level = Level;
        if (level == 0 && Rand() < 0.7)
        {
            // easy: mostly any tile that fits
            var pick = options[rng.Next(options.Count)];
            return new Dictionary<string, object> { ["t"] = "play", ["id"] = pick.id, ["e"] = pick.e.ToString() };
        }
        var seen = new int[7];
        var visible = new List<int>(hand);
        if (!line.Empty) { visible.Add(line.f); foreach (var x in line.L) visible.Add(x.id); foreach (var x in line.R) visible.Add(x.id); }
        foreach (var id in visible) { int a0 = Domino.Tiles[id][0], b0 = Domino.Tiles[id][1]; seen[a0]++; if (a0 != b0) seen[b0]++; }
        int Hold(int val, int without) => hand.Count(id => id != without && (Domino.Tiles[id][0] == val || Domino.Tiles[id][1] == val));
        var best = options[0]; double bestScore = double.NegativeInfinity;
        foreach (var o in options)
        {
            int a = Domino.Tiles[o.id][0], b = Domino.Tiles[o.id][1];
            int openNew, otherEnd;
            if (o.e == 'C') { openNew = b; otherEnd = a; }
            else
            {
                int val = o.e == 'L' ? Domino.LeftVal(line) : Domino.RightVal(line);
                openNew = a == val ? b : a;
                otherEnd = o.e == 'L' ? Domino.RightVal(line) : Domino.LeftVal(line);
            }
            double s = a + b;
            if (a == b) s += 6;
            s += Hold(openNew, o.id) * 3 + Hold(otherEnd, o.id) * 1.5;
            s += seen[openNew] * 1.2;
            if (partnerLast >= 0 && o.e != 'C')
            {
                int val = o.e == 'L' ? Domino.LeftVal(line) : Domino.RightVal(line);
                if (val == partnerLast) s -= 2;
            }
            s += Rand() * (level == 2 ? 0.2 : 1.5);
            if (level == 2 && o.e != 'C') s += seen[openNew] * 0.8;   // hard: leaves ends the others can't match
            if (s > bestScore) { bestScore = s; best = o; }
        }
        return new Dictionary<string, object> { ["t"] = "play", ["id"] = best.id, ["e"] = best.e.ToString() };
    }

    void BotPlay(int seat, bool timedOut = false)
    {
        if (phase != "play" || turn != seat) return;
        var m = ChooseMove(seat);
        if (DoMove(seat, m) != null) DoMove(seat, Do(yard.Count > 0 ? "draw" : "pass"));
        if (timedOut && last != null) last["auto"] = true;
        Bump();
    }

    void EndRound(int winner, bool blocked)
    {
        var pips = hands.Select(h => h.Sum(Pips)).ToArray();
        int nt = Modes.TeamCount(mode);
        var teamPips = new int[nt];
        for (int s = 0; s < n; s++) teamPips[Modes.TeamOf(mode, s)] += pips[s];
        int team = -1, points = 0;
        if (!blocked) team = Modes.TeamOf(mode, winner);
        else
        {
            int min = teamPips.Min();
            var lows = Enumerable.Range(0, nt).Where(t => teamPips[t] == min).ToList();
            if (lows.Count == 1)
            {
                team = lows[0];
                var members = Enumerable.Range(0, n).Where(s => Modes.TeamOf(mode, s) == team).ToList();
                winner = members.Aggregate(members[0], (b, s) => pips[s] < pips[b] ? s : b);
            }
            else winner = -1;
        }
        if (team >= 0)
        {
            for (int t = 0; t < nt; t++) if (t != team) points += teamPips[t];
            scores[team] += points;
        }
        result = new Dictionary<string, object>
        {
            ["winner"] = winner, ["team"] = team, ["blocked"] = blocked, ["points"] = points, ["pips"] = pips.ToList(),
            ["hands"] = hands.Select(h => (object)new List<int>(h)).ToList(),
        };
        starter = winner >= 0 ? winner : -1;
        turn = -1; turnEndsAt = 0;
        last = new Dictionary<string, object> { ["t"] = "round", ["s"] = winner, ["k"] = ++v };
        if (scores.Max() >= target)
        {
            int best = scores.Max();
            phase = "mend";
            match = new Dictionary<string, object> { ["team"] = Array.IndexOf(scores, best), ["scores"] = scores.ToList(), ["deltas"] = seats.Select(_ => (object)null).ToList() };
            last = new Dictionary<string, object> { ["t"] = "match", ["s"] = winner, ["k"] = ++v };
            nextAt = 0;
        }
        else { phase = "rend"; nextAt = Now + NextRoundMs; }
    }

    void Schedule()
    {
        ClearTimer();
        if (phase == "play")
        {
            int seat = turn; var s = seats[seat];
            bool drawing = last != null && S(last, "t") == "draw" && N(last, "s") == seat;
            if (s.bot) At(drawing ? BotLo * 0.5 : BotLo + Rand() * (BotHi - BotLo), () => BotPlay(seat));
            else if (!Domino.CanPlay(line, hands[seat]))
            {
                // nothing fits: draw for you (one tile at a time, so you see it) or pass when the boneyard is empty
                At(drawing ? AutoMoveMs * 0.72 : AutoMoveMs, () =>
                {
                    DoMove(seat, Do(yard.Count > 0 ? "draw" : "pass"));
                    if (last != null) last["forced"] = true;
                    if (turn == seat && phase == "play" && Domino.CanPlay(line, hands[seat])) turnEndsAt = Now + TurnMs;
                    Bump();
                });
            }
            else At(turnEndsAt - Now, () => BotPlay(seat, true));
        }
        else if (phase == "rend") At(nextAt - Now, () => { StartRound(); Bump(); });
    }

    void Bump() { v++; dirty = true; Schedule(); }

    public override Dictionary<string, object> View()
    {
        bool reveal = phase == "rend" || phase == "mend";
        var sv = new List<object>();
        for (int i = 0; i < n; i++) sv.Add(SeatDict(seats[i], i, hands?[i]?.Count ?? 0, Modes.TeamOf(mode, i)));
        var lv = new Dictionary<string, object>
        {
            ["L"] = line.L.Select(x => (object)new List<int> { x.id, x.inner, x.outer }).ToList(),
            ["R"] = line.R.Select(x => (object)new List<int> { x.id, x.inner, x.outer }).ToList(),
        };
        if (!line.Empty) { lv["f"] = line.f; lv["fo"] = new List<int> { line.fo0, line.fo1 }; }
        var view = new Dictionary<string, object>
        {
            ["game"] = "domino", ["id"] = id, ["mode"] = mode, ["target"] = target, ["private"] = true, ["stake"] = 0, ["phase"] = phase, ["you"] = 0,
            ["seats"] = sv, ["hand"] = hands != null ? new List<int>(hands[0]) : new List<int>(),
            ["line"] = lv, ["yard"] = yard.Count, ["turn"] = turn, ["turnEndsAt"] = turnEndsAt, ["turnMs"] = TurnMs,
            ["round"] = round, ["scores"] = scores.ToList(), ["last"] = last, ["result"] = reveal ? result : null, ["match"] = match,
            ["nextAt"] = nextAt, ["now"] = Now, ["v"] = v,
        };
        if (line.Empty && opening >= 0) view["open"] = opening;
        return view;
    }
}
