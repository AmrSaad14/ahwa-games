// Chess, tawla and ludo practice on the phone: the server's board table (shared/boardtable.ts) with the
// game adapters of shared/games/defs.ts, against computer players.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// One game's rules, as the board table drives them (defs.ts GameDef).
public abstract class BoardRules
{
    public virtual double ClockMs => 0;
    public abstract void NewGame(int players, int first, Random rnd);
    public abstract int ToMove();
    /// Plays a move; on success the state changes and the event is returned, otherwise an error code.
    public abstract string Act(int seat, Dictionary<string, object> a, Random rnd, out Dictionary<string, object> ev);
    /// The move a computer player makes. May run on a worker thread (it only reads the state).
    public abstract Dictionary<string, object> Auto(int seat, Random rnd);
    /// A move the game makes by itself after a short pause (nothing to choose).
    public abstract Dictionary<string, object> Forced(int seat);
    public abstract (int winner, int points, string kind)? Over();
    public abstract Dictionary<string, object> View(int seat);

    public static BoardRules For(string mode)
    {
        var g = Modes.GameOf(mode);
        if (g == "chess") return new ChessDef(mode);
        if (g == "ludo") return new LudoDef(LudoState.VariantOf(mode));
        return new TawlaDef(mode == "tawla" ? "aada" : mode);
    }

    protected static Dictionary<string, object> Ev(string t, int seat) => new Dictionary<string, object> { ["t"] = t, ["s"] = seat };
}

class ChessDef : BoardRules
{
    ChessPos c; int white; int lastFrom = -1, lastTo = -1; List<string> captured = new List<string>();
    readonly double clock;
    public ChessDef(string mode) { clock = Modes.ChessMinutes(mode) * 60000.0; }
    public override double ClockMs => clock;
    public override void NewGame(int players, int first, Random rnd) { c = Chess.Start(); white = first; lastFrom = lastTo = -1; captured = new List<string>(); }
    public override int ToMove() => c.turn == 'w' ? white : 1 - white;

    public override string Act(int seat, Dictionary<string, object> a, Random rnd, out Dictionary<string, object> ev)
    {
        ev = null;
        if (OfflineGame.S(a, "t") != "move") return "bad_action";
        int from = OfflineGame.N(a, "from"), to = OfflineGame.N(a, "to");
        var legal = Chess.Legal(c).Where(m => m.from == from && m.to == to).ToList();
        if (legal.Count == 0) return "illegal_move";
        var ps = OfflineGame.S(a, "promo"); char want = ps.Length > 0 ? ps[0] : 'q';
        var mv = legal.Any(x => x.promo == want) ? legal.First(x => x.promo == want) : legal[0];
        char taken = c.b[mv.to];
        if (taken == '\0' && char.ToLower(c.b[mv.from]) == 'p' && mv.to == c.ep) taken = c.turn == 'w' ? 'p' : 'P';
        var n = Chess.Make(c, mv);
        c = n; lastFrom = mv.from; lastTo = mv.to;
        if (taken != '\0') captured = new List<string>(captured) { taken.ToString() };
        ev = Ev("move", seat); ev["from"] = mv.from; ev["to"] = mv.to; if (mv.promo != '\0') ev["promo"] = mv.promo.ToString();
        ev["capture"] = taken != '\0'; ev["check"] = Chess.InCheck(n);
        return null;
    }

    public override Dictionary<string, object> Auto(int seat, Random rnd)
    {
        var m = Chess.BestMove(c, 3, rnd);
        var a = new Dictionary<string, object> { ["t"] = "move", ["from"] = m.from, ["to"] = m.to };
        if (m.promo != '\0') a["promo"] = m.promo.ToString();
        return a;
    }
    public override Dictionary<string, object> Forced(int seat) => null;
    public override (int winner, int points, string kind)? Over()
    {
        var o = Chess.Outcome(c); if (o == null) return null;
        int w = o.Value.winner == '\0' ? -1 : o.Value.winner == 'w' ? white : 1 - white;
        return (w, 1, o.Value.kind);
    }
    public override Dictionary<string, object> View(int seat)
    {
        string you = seat < 0 ? null : seat == white ? "w" : "b";
        bool myTurn = you != null && you[0] == c.turn;
        var legal = new List<object>();
        if (myTurn) foreach (var m in Chess.Legal(c))
        {
            if (m.promo != '\0' && m.promo != 'q') continue;
            legal.Add(m.promo != '\0' ? new List<object> { m.from, m.to, "p" } : new List<object> { m.from, m.to });
        }
        var d = new Dictionary<string, object>
        {
            ["board"] = c.b.Select(p => (object)(p == '\0' ? "" : p.ToString())).ToList(), ["turn"] = c.turn.ToString(), ["you"] = you, ["white"] = white,
            ["legal"] = legal, ["captured"] = new List<string>(captured), ["moveNo"] = c.full,
            ["check"] = Chess.InCheck(c) ? Chess.KingSquare(c.b, c.turn) : -1,
        };
        if (lastFrom >= 0) d["last"] = new Dictionary<string, object> { ["from"] = lastFrom, ["to"] = lastTo };
        return d;
    }
}

class TawlaDef : BoardRules
{
    readonly string variant; ITawla s;
    public TawlaDef(string variant) { this.variant = variant; }
    public override void NewGame(int players, int first, Random rnd) { s = variant == "aada" ? (ITawla)Aada.Start(first) : Tables.Start(variant, first); }
    public override int ToMove() => s.Turn;

    public override string Act(int seat, Dictionary<string, object> a, Random rnd, out Dictionary<string, object> ev)
    {
        ev = null;
        var t = OfflineGame.S(a, "t");
        if (t == "roll")
        {
            if (s.Rolled != null) return "already_rolled";
            s = s.Roll(rnd);
            ev = Ev("roll", seat); ev["dice"] = s.Rolled.ToList();
            return null;
        }
        if (t == "step")
        {
            if (s.Rolled == null) return "must_roll";
            int from = OfflineGame.N(a, "from", -1000), to = OfflineGame.N(a, "to", -1000);
            var via = OfflineGame.Ints(a, "via");
            if (via.Count > 3) return "illegal_move";
            var path = new List<int> { from }; path.AddRange(via); path.Add(to);
            int barBefore = s.OpponentBar;
            var pinnedBefore = (s as Tables)?.pinned;
            ITawla n = s;
            for (int i = 1; i < path.Count && n != null; i++) n = n.Turn == s.Turn ? n.PlayStep(path[i - 1], path[i]) : null;
            if (n == null) return "illegal_move";
            bool hit = n.OpponentBar > barBefore;
            if (n is Tables tn && variant == "mahbousa")
            {
                // trapped a lone checker of the opponent's on the way
                for (int i = 1; i < path.Count; i++) { int p = path[i]; if (p != Tables.OFF && tn.pinned[p] == 1 - seat && pinnedBefore[p] != 1 - seat) hit = true; }
            }
            s = n.TurnDone() && n.Winner() == null ? n.EndTurn() : n;
            ev = Ev("step", seat); ev["from"] = from; ev["to"] = to; ev["hit"] = hit;
            return null;
        }
        if (t == "pass")
        {
            if (s.Rolled == null || !s.TurnDone()) return "must_move";
            var dice = s.Rolled.ToList();
            s = s.EndTurn();
            ev = Ev("pass", seat); ev["dice"] = dice;
            return null;
        }
        return "bad_action";
    }

    public override Dictionary<string, object> Auto(int seat, Random rnd)
    {
        if (s.Rolled == null) return OfflineGame.Do("roll");
        var st = s.BotStep(rnd);
        if (st == null) return OfflineGame.Do("pass");
        return new Dictionary<string, object> { ["t"] = "step", ["from"] = st.Value.from, ["to"] = st.Value.to };
    }
    public override Dictionary<string, object> Forced(int seat) => s.Rolled != null && s.TurnDone() ? OfflineGame.Do("pass") : null;
    public override (int winner, int points, string kind)? Over() => s.Winner();
    public override Dictionary<string, object> View(int seat) => s.View(seat);
}

class LudoDef : BoardRules
{
    readonly string variant; LudoState s;
    public LudoDef(string variant) { this.variant = variant; }
    public override void NewGame(int players, int first, Random rnd) { s = LudoState.New(players, variant, first); }
    public override int ToMove() => s.turn;

    public override string Act(int seat, Dictionary<string, object> a, Random rnd, out Dictionary<string, object> ev)
    {
        ev = null;
        var t = OfflineGame.S(a, "t");
        if (t == "roll")
        {
            if (s.die >= 0) return "already_rolled";
            s = s.Roll(rnd);
            ev = Ev("roll", seat); ev["dice"] = new List<int> { s.die };
            return null;
        }
        if (t == "pawn")
        {
            int i = OfflineGame.N(a, "i");
            var n = s.Move(i, out var captured, out var home);
            if (n == null) return "illegal_move";
            s = n;
            ev = Ev("pawn", seat); ev["i"] = i; ev["home"] = home;
            ev["captured"] = captured.Select(c => (object)new Dictionary<string, object> { ["seat"] = c.seat, ["pawn"] = c.pawn }).ToList();
            return null;
        }
        if (t == "pass")
        {
            if (s.die < 0 || (s.Movable().Count > 0 && s.sixes < 3)) return "must_move";
            int die = s.die;
            if (s.sixes >= 3) s.die = 0;
            s = s.PassTurn();
            ev = Ev("pass", seat); ev["dice"] = new List<int> { die };
            return null;
        }
        return "bad_action";
    }

    public override Dictionary<string, object> Auto(int seat, Random rnd)
    {
        if (s.die < 0) return OfflineGame.Do("roll");
        int p = s.sixes >= 3 ? -1 : s.BotPawn(rnd);
        return p < 0 ? OfflineGame.Do("pass") : new Dictionary<string, object> { ["t"] = "pawn", ["i"] = p };
    }
    public override Dictionary<string, object> Forced(int seat)
    {
        if (s.die < 0) return null;
        var m = s.Movable();
        if (s.sixes >= 3 || m.Count == 0) return OfflineGame.Do("pass");
        return m.Count == 1 ? new Dictionary<string, object> { ["t"] = "pawn", ["i"] = m[0] } : null;
    }
    public override (int winner, int points, string kind)? Over() { int w = s.Winner(); return w >= 0 ? (w, 1, "win") : ((int, int, string)?)null; }
    public override Dictionary<string, object> View(int seat) => s.View(seat);
}

public class OfflineBoard : OfflineGame
{
    readonly string game;
    readonly List<SeatInfo> seats;
    readonly int n, target;
    readonly BoardRules def;
    string phase = "lobby";
    int[] scores;
    int round, lastWinner = -1;
    Dictionary<string, object> last, result, match;
    double turnEndsAt, nextAt, clockFrom;
    double[] clocks;
    Task<Dictionary<string, object>> thinking;   // a computer player working out its move
    int thinkingFor = -1;

    public OfflineBoard(string mode, string name, int ch) : base(mode)
    {
        game = Modes.GameOf(mode);
        n = Modes.Players(mode);
        target = Modes.DefaultTarget(mode);
        seats = MakeSeats(n, name, ch);
        scores = new int[n];
        def = BoardRules.For(mode);
        StartGame();
        Bump();
    }

    void StartGame()
    {
        round++;
        result = null;
        int first = lastWinner >= 0 ? lastWinner : rng.Next(n);
        def.NewGame(n, game == "chess" ? first % 2 : first, rng);
        phase = "play";
        if (def.ClockMs > 0) clocks = Enumerable.Repeat(def.ClockMs, n).ToArray();
        last = new Dictionary<string, object> { ["t"] = "start", ["s"] = def.ToMove(), ["k"] = ++v };
        BeginTurn();
    }

    void BeginTurn()
    {
        int seat = def.ToMove();
        clockFrom = Now;
        if (clocks != null) turnEndsAt = clockFrom + clocks[seat];
        else turnEndsAt = seats[seat].bot ? 0 : Now + TurnMs;
    }

    public override string Act(Dictionary<string, object> a)
    {
        if (phase != "play") return "not_playing";
        if (def.ToMove() != 0) return "not_your_turn";
        var err = Apply(0, a, null);
        if (err == null) Bump();
        return err;
    }

    string Apply(int seat, Dictionary<string, object> a, string flag)
    {
        int before = def.ToMove();
        var err = def.Act(seat, a, rng, out var ev);
        if (err != null) return err;
        if (clocks != null) clocks[seat] = Math.Max(0, clocks[seat] - (Now - clockFrom)) + Modes.ChessIncrement(mode) * 1000;
        ev["k"] = ++v;
        if (flag != null) ev[flag] = true;
        last = ev;
        var end = def.Over();
        if (end != null) { EndGame(end.Value.winner, end.Value.points, end.Value.kind); return null; }
        if (def.ToMove() != before || clocks == null) BeginTurn();
        return null;
    }

    void EndGame(int winner, int points, string kind)
    {
        result = new Dictionary<string, object> { ["winner"] = winner, ["points"] = points, ["kind"] = kind };
        turnEndsAt = 0;
        if (winner >= 0) { scores[winner] += points; lastWinner = winner; }
        last = new Dictionary<string, object> { ["t"] = "end", ["s"] = winner, ["k"] = ++v };
        bool done = game != "tawla" || winner < 0 || scores.Max() >= target;
        if (done)
        {
            phase = "mend";
            int best = scores.Max(), leaders = scores.Count(x => x == best);
            match = new Dictionary<string, object> { ["team"] = winner < 0 && leaders > 1 ? -1 : Array.IndexOf(scores, best), ["scores"] = scores.ToList(), ["deltas"] = seats.Select(_ => (object)null).ToList() };
            nextAt = 0;
        }
        else { phase = "rend"; nextAt = Now + NextRoundMs; }
    }

    void BotMove(int seat, Dictionary<string, object> a, string flag)
    {
        if (phase != "play" || def.ToMove() != seat) return;
        if (a == null || Apply(seat, a, flag) != null) Apply(seat, Do("pass"), flag);
        Bump();
    }

    /// Starts a computer player thinking (chess runs on a worker thread so the café keeps moving).
    void Think(int seat)
    {
        thinkingFor = seat;
        var r = new Random(rng.Next());
        if (game == "chess") thinking = Task.Run(() => def.Auto(seat, r));
        else thinking = Task.FromResult(def.Auto(seat, r));
    }

    void Schedule()
    {
        ClearTimer();
        thinking = null; thinkingFor = -1;
        if (phase == "play")
        {
            int seat = def.ToMove(); var s = seats[seat];
            var forced = def.Forced(seat);
            if (forced != null) At(AutoMoveMs, () => { if (phase == "play" && def.ToMove() == seat) Apply(seat, forced, "forced"); Bump(); });
            else if (s.bot) { Think(seat); At(BotLo + Rand() * (BotHi - BotLo), () => Fire(seat, null)); }
            else if (clocks != null)
            {
                // chess: the clock decides; running out of time loses
                At(clockFrom + clocks[seat] - Now, () => { clocks[seat] = 0; EndGame(1 - seat, 1, "time"); Bump(); });
            }
            else At(turnEndsAt - Now, () => { Think(seat); Fire(seat, "auto"); });
        }
        else if (phase == "rend") At(nextAt - Now, () => { StartGame(); Bump(); });
    }

    string pendingFlag; int pendingSeat = -1;
    void Fire(int seat, string flag)
    {
        // the move is played once the computer has finished thinking
        pendingSeat = seat; pendingFlag = flag;
    }

    public override void Tick()
    {
        base.Tick();
        if (pendingSeat >= 0 && thinking != null && thinking.IsCompleted && thinkingFor == pendingSeat)
        {
            int seat = pendingSeat; var flag = pendingFlag;
            pendingSeat = -1;
            Dictionary<string, object> a = null;
            try { a = thinking.Result; } catch (Exception e) { UnityEngine.Debug.LogWarning("[practice] bot: " + e.Message); }
            BotMove(seat, a, flag);
        }
    }

    void Bump() { v++; dirty = true; pendingSeat = -1; Schedule(); }

    public override Dictionary<string, object> View()
    {
        int turn = phase == "play" ? def.ToMove() : -1;
        var sv = new List<object>();
        for (int i = 0; i < n; i++) sv.Add(SeatDict(seats[i], i, 0, i));
        var view = new Dictionary<string, object>
        {
            ["game"] = game, ["id"] = id, ["mode"] = mode, ["target"] = target, ["private"] = true, ["stake"] = 0, ["phase"] = phase, ["you"] = 0,
            ["seats"] = sv, ["turn"] = turn, ["turnEndsAt"] = turnEndsAt, ["turnMs"] = clocks != null ? def.ClockMs : TurnMs,
            ["round"] = round, ["scores"] = scores.ToList(), ["last"] = last, ["result"] = result, ["match"] = match,
            ["nextAt"] = nextAt, ["now"] = Now, ["v"] = v,
        };
        if (clocks != null) view["clocks"] = clocks.Select((c, i) => (object)(i == turn && phase == "play" ? Math.Max(0, c - (Now - clockFrom)) : c)).ToList();
        view[game] = def.View(0);
        return view;
    }
}
