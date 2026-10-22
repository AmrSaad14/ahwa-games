// Practice games played entirely on the phone, against computer players, with no server.
// Each game is a C# port of the server's rules (shared/*.ts) and produces the same "table" view the server
// sends, so the café, the boards, the HUD and the input code all work unchanged.
using System;
using System.Collections.Generic;

public abstract class OfflineGame
{
    /// The view changed: App sends it through the same path as a server message.
    public bool dirty = true;
    protected readonly string id = "practice-" + DateTime.UtcNow.Ticks.ToString("x");
    protected readonly string mode;
    protected readonly Random rng = new Random();
    protected int v;

    // the server's practice settings (server/config.ts)
    protected const double TurnMs = 15000, NextRoundMs = 7000, AutoMoveMs = 900, BotLo = 900, BotHi = 1700;

    // one pending timer, like the server tables' setTimeout
    double timerAt = -1; Action timerFn;
    protected void At(double ms, Action fn) { timerAt = Now + Math.Max(0, ms); timerFn = fn; }
    protected void ClearTimer() { timerAt = -1; timerFn = null; }

    protected static double Now => App.LocalMs();
    protected double Rand() => rng.NextDouble();

    protected OfflineGame(string mode) { this.mode = mode; }

    public abstract Dictionary<string, object> View();
    /// A move from the person playing (seat 0). Returns an error code, or null when it was played.
    public abstract string Act(Dictionary<string, object> a);

    public virtual void Tick()
    {
        if (timerFn != null && Now >= timerAt)
        {
            var fn = timerFn; timerFn = null; timerAt = -1;
            fn();
        }
    }

    /// How well the computer plays in practice: 0 easy, 1 normal, 2 hard (picked in the game menu).
    public static int Level
    {
        get => UnityEngine.PlayerPrefs.GetInt("ai.level", 1);
        set { UnityEngine.PlayerPrefs.SetInt("ai.level", Math.Max(0, Math.Min(2, value))); UnityEngine.PlayerPrefs.Save(); }
    }

    public static OfflineGame Create(string mode, string name, int ch)
    {
        if (Modes.GameOf(mode) == "domino") return new OfflineDomino(mode, name, ch);
        return new OfflineBoard(mode, name, ch);
    }

    /* ---------- shared helpers ---------- */

    public class SeatInfo { public string name = ""; public int ch; public bool bot; }

    /// You in seat 0, the computer in the rest, each a different café regular.
    protected static List<SeatInfo> MakeSeats(int n, string name, int ch)
    {
        var seats = new List<SeatInfo> { new SeatInfo { name = name ?? "", ch = ch, bot = false } };
        var used = new List<int> { ch };
        for (int i = 1; i < n; i++)
        {
            int c = 5;
            foreach (var k in new[] { 5, 0, 1, 2, 3, 4 }) if (!used.Contains(k)) { c = k; break; }
            used.Add(c);
            seats.Add(new SeatInfo { ch = c, bot = true });
        }
        return seats;
    }

    protected static Dictionary<string, object> SeatDict(SeatInfo s, int i, int count, int team) => new Dictionary<string, object>
    {
        ["name"] = s.name, ["ch"] = s.ch, ["bot"] = s.bot, ["away"] = false, ["online"] = true, ["count"] = count,
        ["team"] = team, ["host"] = i == 0, ["rating"] = 1000, ["mic"] = false, ["pid"] = "",
    };

    public static int N(Dictionary<string, object> a, string k, int def = -1)
    {
        if (a == null || !a.TryGetValue(k, out var o) || o == null) return def;
        if (o is double d) return (int)d;
        if (o is int i) return i;
        if (o is long l) return (int)l;
        if (o is float f) return (int)f;
        return def;
    }
    public static string S(Dictionary<string, object> a, string k) => a != null && a.TryGetValue(k, out var o) && o is string s ? s : "";
    public static List<int> Ints(Dictionary<string, object> a, string k)
    {
        var r = new List<int>();
        if (a == null || !a.TryGetValue(k, out var o) || o == null) return r;
        if (o is List<int> li) return new List<int>(li);
        if (o is System.Collections.IList l) foreach (var x in l) r.Add(x is double d ? (int)d : x is int i ? i : 0);
        return r;
    }
    public static Dictionary<string, object> Do(string t) => new Dictionary<string, object> { ["t"] = t };
}

/// Which game a mode belongs to, how many play, and how far a match goes (shared/engine.ts).
public static class Modes
{
    public static string GameOf(string m)
    {
        switch (m)
        {
            case "tawla": case "mahbousa": case "tawla31": case "gulbahar": return "tawla";
            case "chess1": case "chess3": case "chess": case "chess10": return "chess";
        }
        if (m.StartsWith("ludo")) return "ludo";
        return "domino";
    }
    public static int Players(string m)
    {
        switch (m)
        {
            case "ffa2": case "tawla": case "mahbousa": case "tawla31": case "gulbahar": case "ludo2": case "ludoq2": case "ludom2": return 2;
            case "ffa3": case "ludo3": return 3;
        }
        if (GameOf(m) == "chess") return 2;
        return 4;
    }
    public static int DefaultTarget(string m)
    {
        var g = GameOf(m);
        if (g == "domino") return m == "team" ? 151 : 101;
        if (g == "tawla") return m == "tawla31" ? 31 : 3;
        return 1;
    }
    public static int TeamOf(string m, int seat) => m == "team" ? seat % 2 : seat;
    public static int TeamCount(string m) => m == "team" ? 2 : Players(m);
    public static int ChessMinutes(string m) => m == "chess1" ? 1 : m == "chess3" ? 3 : m == "chess10" ? 10 : 5;
    public static int ChessIncrement(string m) => m == "chess1" ? 0 : m == "chess10" ? 5 : 2;
}
