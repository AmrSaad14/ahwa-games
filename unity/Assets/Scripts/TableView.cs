// A game table as one player sees it (the server's "table" message), read from JSON.
using System.Collections.Generic;

public class SeatView
{
    public string name, pid, hat, drink, title;
    public int ch, count, team, rating;
    public bool bot, away, online, host, mic, muted;
    public double back;   // dropped out: when the computer takes over (server time, ms), 0 when here
}

public class LastAction
{
    public string t;     // domino: deal | play | draw | pass | round | match; boards: start | move | roll | step | pawn | pass | end
    public int s = -1, id = -1, k;
    public char e;
    public bool auto, forced;
    // the board games
    public int from = -1000, to = -1000, i = -1;
    public List<int> dice = new List<int>();
    public bool capture, check, hit, home;
}

public class RoundResult
{
    public int winner = -1, team = -1, points;
    public bool blocked;
    public List<int> pips = new List<int>();
    public List<List<int>> hands = new List<List<int>>();
}

public class MatchResult
{
    public int team = -1;
    public List<int> scores = new List<int>();
    public List<double?> deltas = new List<double?>();
}

public class TableView
{
    public string game, id, code, mode, phase;
    public int target, stake, you, yard, turn, round, v;
    public int open = -1;
    public double turnEndsAt, turnMs, nextAt, now;
    public bool isPrivate;
    public List<SeatView> seats = new List<SeatView>();
    public List<int> hand = new List<int>();
    public List<int> scores = new List<int>();
    public Domino.Line line = new Domino.Line();
    public LastAction last;
    public RoundResult result;
    public MatchResult match;
    public Dictionary<string, object> raw;   // everything, for the board games
    // the board games: who won the game just ended (and how), chess clocks, and the game's own state
    public int endWinner = -1, endPoints; public string endKind;
    public List<double> clocks;
    public Dictionary<string, object> chess, tawla, ludo;
    public bool Board => game != "domino";

    static int I(object o, string k, int def = 0) => (int)o.Num(k, def);
    static List<int> Ints(List<object> a) { var r = new List<int>(); if (a != null) foreach (var x in a) r.Add(x is double d ? (int)d : 0); return r; }

    public static TableView Parse(Dictionary<string, object> m)
    {
        var v = new TableView { raw = m };
        v.game = m.Str("game"); v.id = m.Str("id"); v.code = m.Str("code", null); v.mode = m.Str("mode"); v.phase = m.Str("phase");
        v.target = I(m, "target"); v.stake = I(m, "stake"); v.you = I(m, "you", -1); v.yard = I(m, "yard"); v.turn = I(m, "turn", -1); v.round = I(m, "round"); v.v = I(m, "v");
        v.open = m.TryGetValue("open", out var op) && op is double od ? (int)od : -1;
        v.turnEndsAt = m.Num("turnEndsAt"); v.turnMs = m.Num("turnMs"); v.nextAt = m.Num("nextAt"); v.now = m.Num("now");
        v.isPrivate = m.Bool("private");
        foreach (var s in m.Arr("seats") ?? new List<object>())
        {
            if (!(s is Dictionary<string, object> d)) { v.seats.Add(null); continue; }
            v.seats.Add(new SeatView
            {
                name = d.Str("name"), pid = d.Str("pid"), ch = I(d, "ch"), count = I(d, "count"), team = I(d, "team"), rating = I(d, "rating"),
                bot = d.Bool("bot"), away = d.Bool("away"), online = d.Bool("online"), host = d.Bool("host"), mic = d.Bool("mic"),
                hat = d.Str("hat"), drink = d.Str("drink"), muted = d.Bool("muted"), back = d.Num("back"), title = d.Str("title"),
            });
        }
        v.hand = Ints(m.Arr("hand"));
        v.scores = Ints(m.Arr("scores"));
        var line = m.Obj("line");
        if (line != null)
        {
            if (line.TryGetValue("f", out var f) && f is double fd)
            {
                v.line.f = (int)fd;
                var fo = Ints(line.Arr("fo"));
                if (fo.Count == 2) { v.line.fo0 = fo[0]; v.line.fo1 = fo[1]; }
            }
            foreach (var t in line.Arr("L") ?? new List<object>()) { var a = Ints(t as List<object>); if (a.Count == 3) v.line.L.Add(new Domino.ArmTile(a[0], a[1], a[2])); }
            foreach (var t in line.Arr("R") ?? new List<object>()) { var a = Ints(t as List<object>); if (a.Count == 3) v.line.R.Add(new Domino.ArmTile(a[0], a[1], a[2])); }
        }
        var last = m.Obj("last");
        if (last != null)
        {
            var e = last.Str("e");
            v.last = new LastAction { t = last.Str("t"), s = I(last, "s", -1), id = I(last, "id", -1), k = I(last, "k"), e = e.Length > 0 ? e[0] : ' ', auto = last.Bool("auto"), forced = last.Bool("forced"),
                from = I(last, "from", -1000), to = I(last, "to", -1000), i = I(last, "i", -1), dice = Ints(last.Arr("dice")),
                capture = last.Bool("capture"), check = last.Bool("check"), hit = last.Bool("hit"), home = last.Bool("home") };
        }
        var res = m.Obj("result");
        if (res != null && v.game == "domino")
        {
            v.result = new RoundResult { winner = I(res, "winner", -1), team = I(res, "team", -1), points = I(res, "points"), blocked = res.Bool("blocked"), pips = Ints(res.Arr("pips")) };
            foreach (var h in res.Arr("hands") ?? new List<object>()) v.result.hands.Add(Ints(h as List<object>));
        }
        if (res != null && v.game != "domino") { v.endWinner = I(res, "winner", -1); v.endPoints = I(res, "points"); v.endKind = res.Str("kind"); }
        if (m.Arr("clocks") is List<object> cl) { v.clocks = new List<double>(); foreach (var c in cl) v.clocks.Add(c is double cd ? cd : 0); }
        v.chess = m.Obj("chess"); v.tawla = m.Obj("tawla"); v.ludo = m.Obj("ludo");
        var match = m.Obj("match");
        if (match != null)
        {
            v.match = new MatchResult { team = I(match, "team", -1), scores = Ints(match.Arr("scores")) };
            foreach (var d in match.Arr("deltas") ?? new List<object>()) v.match.deltas.Add(d is double dd ? dd : (double?)null);
        }
        return v;
    }

    public bool MyTurn => phase == "play" && you >= 0 && turn == you;
    public SeatView Me => you >= 0 && you < seats.Count ? seats[you] : null;
    public int TeamOf(int seat) => game == "domino" ? Domino.TeamOf(mode, seat) : seat;
    public int TeamCount => mode == "team" ? 2 : seats.Count;
    public static List<int> IntList(List<object> a) => Ints(a);
}
