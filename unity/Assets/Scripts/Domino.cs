// Domino rules and table layout, the same as the server's (shared/engine.ts, shared/layout.ts).
// Double-six set: 28 tiles, identified by index 0..27.
using System;
using System.Collections.Generic;

public static class Domino
{
    public static readonly int[][] Tiles = Build();
    static int[][] Build()
    {
        var t = new List<int[]>();
        for (int a = 0; a <= 6; a++) for (int b = a; b <= 6; b++) t.Add(new[] { a, b });
        return t.ToArray();
    }
    public static bool IsDouble(int id) => Tiles[id][0] == Tiles[id][1];

    /// One tile on an arm of the line: tile id, inner value (touching), outer value (open).
    public struct ArmTile { public int id, inner, outer; public ArmTile(int i, int a, int b) { id = i; inner = a; outer = b; } }

    public class Line
    {
        public int f = -1;             // first tile (-1: empty table)
        public int fo0, fo1;           // its orientation: fo0 faces left, fo1 faces right
        public List<ArmTile> L = new List<ArmTile>(), R = new List<ArmTile>();
        public bool Empty => f < 0;
    }

    public static int LeftVal(Line l) => l.L.Count > 0 ? l.L[l.L.Count - 1].outer : l.fo0;
    public static int RightVal(Line l) => l.R.Count > 0 ? l.R[l.R.Count - 1].outer : l.fo1;

    /// Which ends ('L', 'R', or 'C' for the first tile) a tile can go on; empty: it doesn't fit.
    public static List<char> EndsFor(Line l, int id)
    {
        var o = new List<char>();
        if (l.Empty) { o.Add('C'); return o; }
        int a = Tiles[id][0], b = Tiles[id][1], lv = LeftVal(l), rv = RightVal(l);
        if (a == lv || b == lv) o.Add('L');
        if (a == rv || b == rv) o.Add('R');
        return o;
    }

    public static bool CanPlay(Line l, IList<int> hand) { foreach (var id in hand) if (EndsFor(l, id).Count > 0) return true; return false; }

    /// Where a tile sits on the table, in tile units: a tile is 2 long and 1 wide. x grows to the right,
    /// y toward the bottom player; ang is the direction of the long axis; a is drawn on the tail end, b on the head.
    public struct Placement { public int id, a, b; public float x, y, ang; }

    class Arm
    {
        float px, py, dx, dy; bool vertical; float hdir; readonly int sign; readonly float xMax;
        public readonly List<Placement> Out = new List<Placement>();
        public Arm(int sign, float firstExtent, float xMax) { this.sign = sign; this.xMax = xMax; px = sign * firstExtent; py = 0; dx = sign; dy = 0; hdir = sign; }

        void Corner(ArmTile t, float nx, float ny)
        {
            Out.Add(new Placement { id = t.id, a = t.inner, b = t.outer, x = px + dx * 0.5f + nx * 0.5f, y = py + dy * 0.5f + ny * 0.5f, ang = (float)Math.Atan2(ny, nx) });
            px = px + dx * 0.5f + nx * 1.5f; py = py + dy * 0.5f + ny * 1.5f;
            dx = nx; dy = ny;
        }

        public void Put(ArmTile t)
        {
            // a double always lies across the line (never turns a corner itself), as on a real table;
            // the tile after it turns the corner if there is no room left
            bool dbl = t.inner == t.outer;
            if (!dbl && vertical) { vertical = false; hdir = -hdir; Corner(t, hdir, 0); return; }
            if (!dbl && Math.Abs(px + dx * 2) > xMax) { vertical = true; Corner(t, 0, sign); return; }
            if (dbl)
            {
                Out.Add(new Placement { id = t.id, a = t.inner, b = t.outer, x = px + dx * 0.5f, y = py + dy * 0.5f, ang = (float)Math.Atan2(dy, dx) + (float)Math.PI / 2 });
                px += dx; py += dy;
            }
            else
            {
                Out.Add(new Placement { id = t.id, a = t.inner, b = t.outer, x = px + dx, y = py + dy, ang = (float)Math.Atan2(dy, dx) });
                px += dx * 2; py += dy * 2;
            }
        }
    }

    public static List<Placement> LayoutLine(Line line, float xMax)
    {
        var o = new List<Placement>();
        if (line.Empty) return o;
        bool dbl = line.fo0 == line.fo1;
        float ext = dbl ? 0.5f : 1;
        o.Add(new Placement { id = line.f, a = line.fo0, b = line.fo1, x = 0, y = 0, ang = dbl ? (float)Math.PI / 2 : 0 });
        var r = new Arm(1, ext, xMax); foreach (var t in line.R) r.Put(t); o.AddRange(r.Out);
        var l = new Arm(-1, ext, xMax); foreach (var t in line.L) l.Put(t); o.AddRange(l.Out);
        return o;
    }

    /// Where a tile would land if played on an end (the glowing hint).
    public static Placement Preview(Line line, int id, char end, float xMax)
    {
        int a = Tiles[id][0], b = Tiles[id][1];
        if (end == 'C' || line.Empty) return new Placement { id = id, a = a, b = b, x = 0, y = 0, ang = a == b ? (float)Math.PI / 2 : 0 };
        int v = end == 'L' ? LeftVal(line) : RightVal(line);
        int o = a == v ? b : a;
        float ext = line.fo0 == line.fo1 ? 0.5f : 1;
        var arm = new Arm(end == 'L' ? -1 : 1, ext, xMax);
        foreach (var t in end == 'L' ? line.L : line.R) arm.Put(t);
        arm.Put(new ArmTile(id, v, o));
        return arm.Out[arm.Out.Count - 1];
    }

    public static int TeamOf(string mode, int seat) => mode == "team" ? seat % 2 : seat;
    public static int Players(string mode) => mode == "ffa2" ? 2 : mode == "ffa3" ? 3 : 4;
}
