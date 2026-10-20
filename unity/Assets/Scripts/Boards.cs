// The three board games in 3D, laid out exactly like the web game (client/src/boards.ts):
//  - chess: a wooden board with boxwood and ebony Staunton pieces,
//  - tawla (backgammon): 24 inlaid points, the bar, the bearing-off tray, ivory and dark checkers, two dice,
//  - ludo: the printed cross with its 52-square track, coloured home columns, bases, turned pawns and a die.
// Every board lives under `spin`, turned so your side is nearest you. Positions are worked out in the board's
// own frame: x to your right, z away from you, y up from the table top.
using System.Collections.Generic;
using UnityEngine;

public struct Pick
{
    public string kind;   // square | point | pawn | dice | none
    public int v;
    public static Pick None => new Pick { kind = "none", v = -1 };
}

public abstract class Board
{
    public GameObject root;
    public Transform spin, pieces, marks;
    public string game;
    public float topY;              // the playing surface's height above the table
    protected int lastK = -1;

    protected Board(string game, Transform parent)
    {
        this.game = game;
        root = new GameObject("Board " + game);
        root.transform.SetParent(parent, false);
        spin = new GameObject("Spin").transform; spin.SetParent(root.transform, false);
        pieces = new GameObject("Pieces").transform; pieces.SetParent(spin, false);
        marks = new GameObject("Marks").transform; marks.SetParent(spin, false);
    }

    /// Draw the view: pieces where they are, and the marks (selection, where it can go, the last move).
    public abstract void Render(TableView v, int sel, List<int> targets, List<int> movable);
    /// What's under a point of the playing surface (in `spin` space).
    public abstract Pick PickAt(Vector3 local);
    /// Where a pick is, in the world (for a hand reaching out to it).
    public abstract Vector3 Spot(TableView v, int kindValue);

    protected static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject); }

    protected GameObject Mark(Mesh mesh, Color c, Vector3 at, float scale = 1f)
    {
        var go = Art.Obj("Mark", marks, mesh, Art.Glow(c), false);
        go.transform.localPosition = at; go.transform.localScale = Vector3.one * scale;
        return go;
    }
    static Mesh quadUp;
    protected static Mesh QuadUp
    {
        get
        {
            if (quadUp != null) return quadUp;
            quadUp = new Mesh();
            quadUp.SetVertices(new List<Vector3> { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
            quadUp.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); quadUp.RecalculateNormals();
            return quadUp;
        }
    }
}

/* =================================================================== chess */

public class ChessBoard : Board
{
    public const float S = 1.15f;                       // one square
    const float FrameW = 0.55f, PS = 0.98f;              // frame width, piece scale
    static Material whiteMat, blackMat, whiteDark, blackDark, felt;
    static readonly Dictionary<string, Mesh[]> meshes = new Dictionary<string, Mesh[]>();
    readonly Dictionary<int, GameObject> bySq = new Dictionary<int, GameObject>();

    public ChessBoard(Transform parent) : base("chess", parent)
    {
        topY = 0.3f;
        float B = S * 8;
        var frameTex = Art.WoodTex(512, 512, Art.Hex("#5a2d18"), Art.Hex("#2e130a"), Art.Hex("#7a4428"), 60, 3.3f);
        Art.Box("Frame", spin, B + 2 * FrameW, 0.3f, B + 2 * FrameW, Vector3.zero, Art.Mat(Color.white, 0.62f, 0, frameTex));
        // a boxwood stringing line round the squares
        Art.Box("Line", spin, B + 0.12f, 0.302f, B + 0.12f, Vector3.zero, Art.Mat(Art.Hex("#e8cf9c"), 0.5f));
        // maple and walnut squares, each with its own piece of grain
        var light = new[] { Art.Hex("#e7c993"), Art.Hex("#c9a46a"), Art.Hex("#f4dfb2") };
        var dark = new[] { Art.Hex("#7a4a26"), Art.Hex("#4e2c14"), Art.Hex("#9a6236") };
        var tex = Art.Paint(512, 512, (x, y) =>
        {
            int f = x / 64, r = y / 64;
            var w = (f + r) % 2 == 1 ? light : dark;
            var c = Art.Wood(x * 3 + f * 97, y * 3 + r * 53, w[0], w[1], w[2], 50, f * 0.7f + r * 1.3f);
            if (x % 64 == 0 || y % 64 == 0) c *= 0.75f;
            return c;
        });
        Art.Sheet("Squares", spin, B, B, new Vector3(0, 0.303f, 0), Art.Mat(new Color(0.85f, 0.83f, 0.8f), 0.4f, 0, tex));
    }

    /// Square to board position: a1 at your bottom-left when you play white (the board turns for black).
    public static Vector3 Pos(int sq) => new Vector3(((sq & 7) - 3.5f) * S, 0.3f, ((sq >> 3) - 3.5f) * S);

    public override Pick PickAt(Vector3 l)
    {
        int f = Mathf.FloorToInt(l.x / S + 4), r = Mathf.FloorToInt(l.z / S + 4);
        if (f < 0 || f > 7 || r < 0 || r > 7) return Pick.None;
        return new Pick { kind = "square", v = r * 8 + f };
    }
    public override Vector3 Spot(TableView v, int sq) => spin.TransformPoint(Pos(sq) + Vector3.up * 0.4f);

    public override void Render(TableView v, int sel, List<int> targets, List<int> movable)
    {
        var c = v.chess; if (c == null) return;
        spin.localRotation = Quaternion.Euler(0, c.Str("you") == "b" ? 180 : 0, 0);
        Clear(pieces); Clear(marks); bySq.Clear();
        var board = c.Arr("board");
        var last = c.Obj("last");
        int lastFrom = last != null ? (int)last.Num("from") : -1, lastTo = last != null ? (int)last.Num("to") : -1;
        if (board != null)
            for (int sq = 0; sq < board.Count && sq < 64; sq++)
            {
                var p = board[sq] as string;
                if (string.IsNullOrEmpty(p)) continue;
                var g = Piece(p, pieces);
                g.transform.localPosition = Pos(sq);
                bySq[sq] = g;
            }
        if (lastFrom >= 0) { Mark(QuadUp, new Color(0.95f, 0.76f, 0.3f, 0.35f), Pos(lastFrom) + Vector3.up * 0.006f, S); Mark(QuadUp, new Color(0.95f, 0.76f, 0.3f, 0.45f), Pos(lastTo) + Vector3.up * 0.006f, S); }
        int check = (int)c.Num("check", -1);
        if (check >= 0) Mark(QuadUp, new Color(0.83f, 0.23f, 0.17f, 0.6f), Pos(check) + Vector3.up * 0.007f, S);
        if (sel >= 0) Mark(QuadUp, new Color(0.23f, 0.64f, 0.57f, 0.6f), Pos(sel) + Vector3.up * 0.008f, S);
        if (targets != null)
            foreach (var t in targets)
            {
                bool occupied = board != null && t < board.Count && !string.IsNullOrEmpty(board[t] as string);
                if (occupied) Mark(Art.Ring(0.38f, 0.5f), new Color(0.12f, 0.43f, 0.39f, 0.85f), Pos(t) + Vector3.up * 0.01f);
                else Mark(Art.Disc(0.17f), new Color(0.12f, 0.43f, 0.39f, 0.8f), Pos(t) + Vector3.up * 0.01f);
            }
        // the move just made slides in
        if (v.last != null && v.last.k != lastK)
        {
            lastK = v.last.k;
            if (v.last.t == "move" && v.last.from >= 0 && bySq.TryGetValue(v.last.to, out var moved))
            {
                var h = moved.AddComponent<Hop>(); h.from = Pos(v.last.from); h.to = Pos(v.last.to); h.height = 0.5f;
            }
        }
    }

    static readonly Vector2[] Base = { V(0, 0), V(0.36f, 0), V(0.385f, 0.015f), V(0.385f, 0.055f), V(0.365f, 0.075f), V(0.335f, 0.09f), V(0.345f, 0.115f), V(0.33f, 0.135f), V(0.28f, 0.16f) };
    static Vector2 V(float r, float y) => new Vector2(r, y);
    static Vector2[] With(params Vector2[] rest) { var l = new List<Vector2>(Base); l.AddRange(rest); return l.ToArray(); }

    static Mesh[] MeshesFor(string t)
    {
        if (meshes.TryGetValue(t, out var m)) return m;
        var list = new List<Mesh>();
        switch (t)
        {
            case "p": list.Add(Art.Lathe(With(V(0.22f, 0.2f), V(0.15f, 0.3f), V(0.12f, 0.38f), V(0.2f, 0.41f), V(0.21f, 0.435f), V(0.11f, 0.46f), V(0.1f, 0.48f), V(0.15f, 0.51f), V(0.175f, 0.58f), V(0.15f, 0.66f), V(0.08f, 0.7f), V(0, 0.71f)))); break;
            case "r":
                list.Add(Art.Lathe(With(V(0.27f, 0.22f), V(0.25f, 0.4f), V(0.235f, 0.58f), V(0.25f, 0.64f), V(0.3f, 0.68f), V(0.3f, 0.71f), V(0.27f, 0.73f), V(0.29f, 0.76f), V(0.3f, 0.88f), V(0.22f, 0.88f), V(0.21f, 0.84f), V(0, 0.84f))));
                list.Add(Crenels()); break;
            case "n":
                list.Add(Art.Lathe(With(V(0.27f, 0.22f), V(0.25f, 0.27f), V(0.3f, 0.3f), V(0.3f, 0.33f), V(0, 0.33f))));
                list.Add(KnightHead()); break;
            case "b": list.Add(Art.Lathe(With(V(0.24f, 0.22f), V(0.17f, 0.36f), V(0.13f, 0.52f), V(0.12f, 0.6f), V(0.21f, 0.63f), V(0.22f, 0.66f), V(0.12f, 0.69f), V(0.11f, 0.71f), V(0.16f, 0.76f), V(0.19f, 0.86f), V(0.165f, 0.97f), V(0.1f, 1.04f), V(0.05f, 1.08f), V(0.06f, 1.1f), V(0.07f, 1.14f), V(0.04f, 1.18f), V(0, 1.19f)))); break;
            case "q":
                list.Add(Art.Lathe(With(V(0.27f, 0.22f), V(0.19f, 0.38f), V(0.14f, 0.58f), V(0.13f, 0.72f), V(0.23f, 0.75f), V(0.24f, 0.78f), V(0.14f, 0.81f), V(0.14f, 0.84f), V(0.2f, 0.9f), V(0.27f, 1.0f), V(0.25f, 1.02f), V(0.14f, 1.03f), V(0.12f, 1.08f), V(0.06f, 1.12f), V(0, 1.13f))));
                list.Add(Art.Lathe(new[] { V(0, 1.11f), V(0.05f, 1.12f), V(0.07f, 1.18f), V(0.05f, 1.24f), V(0, 1.25f) }, 16)); break;
            default:   // king
                list.Add(Art.Lathe(With(V(0.28f, 0.22f), V(0.2f, 0.38f), V(0.15f, 0.6f), V(0.14f, 0.76f), V(0.24f, 0.79f), V(0.25f, 0.82f), V(0.15f, 0.85f), V(0.15f, 0.88f), V(0.22f, 0.96f), V(0.25f, 1.03f), V(0.2f, 1.06f), V(0.13f, 1.09f), V(0.09f, 1.13f), V(0, 1.14f))));
                list.Add(Cross()); break;
        }
        return meshes[t] = list.ToArray();
    }

    static Mesh Crenels()
    {
        // six blocks round the top of the rook
        var combine = new List<CombineInstance>();
        var cube = Art.Prim(PrimitiveType.Cube);
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f + 30f;
            var mtx = Matrix4x4.TRS(Quaternion.Euler(0, a, 0) * new Vector3(0.26f, 0.925f, 0), Quaternion.Euler(0, a, 0), new Vector3(0.09f, 0.09f, 0.13f));
            combine.Add(new CombineInstance { mesh = cube, transform = mtx });
        }
        var m = new Mesh(); m.CombineMeshes(combine.ToArray(), true, true); m.RecalculateBounds();
        return m;
    }

    static Mesh Cross()
    {
        var cube = Art.Prim(PrimitiveType.Cube);
        var m = new Mesh();
        m.CombineMeshes(new[]
        {
            new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(0, 1.27f, 0), Quaternion.identity, new Vector3(0.075f, 0.28f, 0.075f)) },
            new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(0, 1.3f, 0), Quaternion.identity, new Vector3(0.22f, 0.075f, 0.075f)) },
        }, true, true);
        return m;
    }

    /// The knight's head: a carved horse's profile, narrowing toward the muzzle, facing away from its owner.
    static Mesh KnightHead()
    {
        var o = new List<Vector2> { new Vector2(-0.25f, 0), new Vector2(0.25f, 0) };
        Art.Bezier(o, new Vector2(0.25f, 0), new Vector2(0.22f, 0.18f), new Vector2(0.3f, 0.36f), new Vector2(0.33f, 0.52f));      // chest
        Art.Bezier(o, new Vector2(0.33f, 0.52f), new Vector2(0.36f, 0.7f), new Vector2(0.3f, 0.86f), new Vector2(0.18f, 0.95f));   // back of the head
        o.Add(new Vector2(0.16f, 1.04f)); o.Add(new Vector2(0.08f, 0.97f));                                                         // ear
        Art.Bezier(o, new Vector2(0.08f, 0.97f), new Vector2(-0.05f, 0.98f), new Vector2(-0.2f, 0.9f), new Vector2(-0.33f, 0.72f)); // forehead
        o.Add(new Vector2(-0.41f, 0.6f));
        Art.Quad2(o, new Vector2(-0.41f, 0.6f), new Vector2(-0.43f, 0.53f), new Vector2(-0.36f, 0.51f));                          // muzzle
        Art.Bezier(o, new Vector2(-0.36f, 0.51f), new Vector2(-0.25f, 0.5f), new Vector2(-0.14f, 0.52f), new Vector2(-0.08f, 0.46f)); // jaw
        Art.Bezier(o, new Vector2(-0.08f, 0.46f), new Vector2(-0.16f, 0.34f), new Vector2(-0.26f, 0.2f), new Vector2(-0.25f, 0.02f)); // throat
        // drop near-duplicate points
        var pts = new List<Vector2>();
        foreach (var p in o) if (pts.Count == 0 || (pts[pts.Count - 1] - p).sqrMagnitude > 1e-5f) pts.Add(p);
        if ((pts[0] - pts[pts.Count - 1]).sqrMagnitude < 1e-5f) pts.RemoveAt(pts.Count - 1);
        var m = Art.Extrude(pts, 0.26f);
        // turn it so the muzzle (shape -x) faces +z, the thickness runs along x; then carve it narrower to the front and top
        var vs = m.vertices;
        for (int i = 0; i < vs.Length; i++)
        {
            var s = vs[i];
            float x = s.z, y = s.y + 0.31f, z = -s.x;
            float front = Mathf.Clamp01((z + 0.05f) / 0.45f), top = Mathf.Clamp01((y - 1.05f) / 0.3f);
            x *= (1 - 0.5f * front) * (1 - 0.35f * top);
            vs[i] = new Vector3(x, y, z);
        }
        m.vertices = vs;   // (x, y, z) → (z, y, −x) is a turn, not a mirror: the triangles still face out
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    public static GameObject Piece(string p, Transform parent)
    {
        bool white = p == p.ToUpperInvariant();
        if (whiteMat == null)
        {
            whiteMat = Art.Mat(Color.white, 0.68f, 0, Art.WoodTex(128, 256, Art.Hex("#e3c592"), Art.Hex("#b98d55"), Art.Hex("#f3dfb6"), 30, 1.1f));
            blackMat = Art.Mat(Color.white, 0.78f, 0, Art.WoodTex(128, 256, Art.Hex("#1d130e"), Art.Hex("#0a0604"), Art.Hex("#3a281d"), 30, 2.2f));
            whiteDark = Art.Mat(Art.Hex("#6b4a26"), 0.4f); blackDark = Art.Mat(Art.Hex("#050302"), 0.4f);
            felt = Art.Mat(Art.Hex("#1f4a2c"), 0.05f);
        }
        var g = new GameObject("Piece " + p); g.transform.SetParent(parent, false);
        var ms = MeshesFor(p.ToLowerInvariant());
        for (int i = 0; i < ms.Length; i++) Art.Obj("Part", g.transform, ms[i], white ? whiteMat : blackMat);
        var baize = Art.Obj("Felt", g.transform, Art.Disc(0.35f), felt, false); baize.transform.localPosition = Vector3.up * 0.002f; baize.transform.localRotation = Quaternion.Euler(180, 0, 0);
        g.transform.localScale = Vector3.one * PS;
        if (!white) g.transform.localRotation = Quaternion.Euler(0, 180, 0);   // knights face the other side
        return g;
    }
}

/* =================================================================== tawla */

public class TawlaBoard : Board
{
    public const int BAR = -1, OFF = 99;
    const float TW = 11.6f, TD = 7.8f, EDGE = 0.25f, TRAY = 1.0f, BARW = 0.7f, CR = 0.34f, RAIL_H = 0.22f, FIELD_Y = 0.401f;
    static readonly float COLW = (TW - TRAY - BARW - EDGE * 2) / 12f;
    static Mesh checker;
    static Material[] checkerMats;
    Dictionary<string, object> s;

    public TawlaBoard(Transform parent) : base("tawla", parent)
    {
        topY = FIELD_Y;
        spin.localScale = Vector3.one * 0.95f;    // a little smaller than the web board, so it fits the table with room for hands
        var side = Art.Mat(Color.white, 0.6f, 0, Art.WoodTex(512, 128, Art.Hex("#6b3a1e"), Art.Hex("#3d1f0e"), Art.Hex("#8c5530"), 50, 4.2f));
        Art.Box("Box", spin, TW, FIELD_Y, TD, Vector3.zero, side);
        // the playing fields: olive wood with inlaid points, and the felt-lined tray
        Color field0 = Art.Hex("#c9a46c"), field1 = Art.Hex("#a7834f"), field2 = Art.Hex("#dcbd88");
        Color ptA0 = Art.Hex("#3b2414"), ptA1 = Art.Hex("#24140a"), ptA2 = Art.Hex("#5a3a22");
        Color ptB0 = Art.Hex("#8e2a1c"), ptB1 = Art.Hex("#5e180e"), ptB2 = Art.Hex("#b0402c");
        const int W = 1024, H = 688;
        var tex = Art.Paint(W, H, (px, py) =>
        {
            float x = (px + 0.5f) / W * TW - TW / 2;              // board x
            float zf = (py + 0.5f) / H * TD - TD / 2;             // board z (away from you is +)
            float zw = -zf;                                       // the web game's z (toward you is +)
            var wood = Art.Wood(px, py, field0, field1, field2, 70, 0.5f);
            // outside the fields: the box's own wood shows (the rails sit on top)
            if (Mathf.Abs(x) > TW / 2 - EDGE || Mathf.Abs(zf) > TD / 2 - EDGE) return side.color * Art.Wood(px, py, Art.Hex("#6b3a1e"), Art.Hex("#3d1f0e"), Art.Hex("#8c5530"), 50, 4.2f);
            if (x > TW / 2 - EDGE - TRAY) return Art.Hex("#1e3b26") * (0.9f + Mathf.PerlinNoise(px * 0.3f, py * 0.3f) * 0.15f);
            // the points: a long triangle from each side toward the middle
            for (int c = 0; c < 12; c++)
            {
                float cx = ColX(c), half = COLW / 2 * 0.92f, len = TD * 0.42f;
                if (Mathf.Abs(x - cx) > half) continue;
                bool near = zw > 0;
                float from = TD / 2 - EDGE, d = from - Mathf.Abs(zw);   // distance in from the edge
                if (d < 0 || d > len) continue;
                float w = half * (1 - d / len);
                if (Mathf.Abs(x - cx) > w) continue;
                bool red = (c + (near ? 0 : 1)) % 2 == 1;
                var pc = red ? Art.Wood(px * 2, py * 2, ptB0, ptB1, ptB2, 60, 2.1f) : Art.Wood(px * 2, py * 2, ptA0, ptA1, ptA2, 60, 1.3f);
                if (Mathf.Abs(x - cx) > w - 0.02f) pc = Color.Lerp(pc, Art.Hex("#e7d3a8"), 0.6f);   // a pale inlay line
                return pc;
            }
            return wood;
        });
        Art.Sheet("Field", spin, TW, TD, new Vector3(0, FIELD_Y + 0.001f, 0), Art.Mat(new Color(0.68f, 0.66f, 0.64f), 0.25f, 0, tex));
        // the rails: four sides, the middle bar and the tray's divider, with a mother-of-pearl strip on top
        var railTop = Art.Mat(Art.Hex("#bdb3a2"), 0.55f, 0.05f);
        void Rail(float x, float z, float w, float d)
        {
            Art.Box("Rail", spin, w, RAIL_H, d, new Vector3(x, FIELD_Y, z), side);
            Art.Box("Pearl", spin, Mathf.Max(0.04f, w - 0.1f), 0.01f, Mathf.Max(0.04f, d - 0.1f), new Vector3(x, FIELD_Y + RAIL_H, z), railTop);
        }
        Rail(0, TD / 2 - EDGE / 2, TW, EDGE); Rail(0, -TD / 2 + EDGE / 2, TW, EDGE);
        Rail(-TW / 2 + EDGE / 2, 0, EDGE, TD - 2 * EDGE); Rail(TW / 2 - EDGE / 2, 0, EDGE, TD - 2 * EDGE);
        Rail(BarX, 0, BARW, TD - 2 * EDGE);
        Art.Box("Divider", spin, 0.08f, RAIL_H, TD - 2 * EDGE, new Vector3(TW / 2 - EDGE - TRAY, FIELD_Y, 0), side);
        var brass = Art.Mat(Art.Hex("#c9a25a"), 0.75f, 1f);
        foreach (var z in new[] { -TD / 4, TD / 4 })
        {
            var hinge = Art.Obj("Hinge", spin, Art.Prim(PrimitiveType.Cylinder), brass);
            hinge.transform.localPosition = new Vector3(BarX, FIELD_Y + RAIL_H + 0.03f, z);
            hinge.transform.localRotation = Quaternion.Euler(90, 0, 0);
            hinge.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);
        }
    }

    static float BarX => -TW / 2 + EDGE + 6 * COLW + BARW / 2;
    static float TrayX => TW / 2 - EDGE - TRAY / 2;
    static float ColX(int c) => -TW / 2 + EDGE + COLW * (c + 0.5f) + (c >= 6 ? BARW : 0);

    /// Where the k-th checker of a stack on point i sits: five in a row from the edge, more piled on top.
    static Vector3 StackPos(int i, int k)
    {
        bool near = i <= 11; int c = near ? 11 - i : i - 12;
        int row = k % 5, layer = k / 5;
        float d = TD / 2 - EDGE - CR - row * CR * 2;
        return new Vector3(ColX(c), 0.47f + layer * 0.145f, near ? -d : d);
    }
    static Vector3 BarPos(int p, int k) => new Vector3(BarX, FIELD_Y + RAIL_H + 0.08f + k * 0.15f, p == 0 ? -0.7f : 0.7f);
    static Vector3 OffPos(int p, int k)
    {
        float z = TD / 2 - EDGE - 0.15f - k * 0.16f;
        return new Vector3(TrayX, 0.42f, p == 0 ? -z : z);
    }

    GameObject Checker(int p, Vector3 at, bool flat = true)
    {
        if (checker == null)
        {
            checker = Art.Lathe(new[] { new Vector2(0, -0.07f), new Vector2(CR - 0.025f, -0.07f), new Vector2(CR, -0.05f), new Vector2(CR, 0.045f), new Vector2(CR - 0.02f, 0.07f), new Vector2(CR * 0.78f, 0.07f), new Vector2(CR * 0.74f, 0.058f), new Vector2(CR * 0.7f, 0.07f), new Vector2(CR * 0.3f, 0.07f), new Vector2(CR * 0.27f, 0.062f), new Vector2(CR * 0.24f, 0.07f), new Vector2(0, 0.07f) }, 40);
            checkerMats = new[] { Art.Mat(Art.Hex("#efe3c8"), 0.72f), Art.Mat(Art.Hex("#3a160e"), 0.8f) };
        }
        var go = Art.Obj("Checker", pieces, checker, checkerMats[p]);
        go.transform.localPosition = at;
        if (!flat) { go.transform.localRotation = Quaternion.Euler(90, 0, 0); go.transform.localScale = new Vector3(1, 0.6f, 1); }
        return go;
    }

    public override Pick PickAt(Vector3 l)
    {
        if (Mathf.Abs(l.x - BarX) < BARW / 2) return new Pick { kind = "point", v = BAR };
        if (l.x > TW / 2 - EDGE - TRAY) return new Pick { kind = "point", v = OFF };
        int best = 0; float bd = float.MaxValue;
        for (int c = 0; c < 12; c++) { float d = Mathf.Abs(ColX(c) - l.x); if (d < bd) { bd = d; best = c; } }
        return new Pick { kind = "point", v = l.z < 0 ? 11 - best : 12 + best };
    }

    public override Vector3 Spot(TableView v, int p)
    {
        if (s == null) return spin.position;
        int turn = (int)s.Num("turn");
        var pts = TableView.IntList(s.Arr("pts"));
        Vector3 at = p == OFF ? OffPos(turn, 0) : p == BAR ? BarPos(turn, 0) : StackPos(p, Mathf.Max(0, Mathf.Abs(pts.Count > p ? pts[p] : 0) - 1));
        return spin.TransformPoint(at);
    }

    public override void Render(TableView v, int sel, List<int> targets, List<int> movable)
    {
        s = v.tawla; if (s == null) return;
        int you = (int)s.Num("you", 0);
        spin.localRotation = Quaternion.Euler(0, you == 1 ? 180 : 0, 0);
        Clear(pieces); Clear(marks);
        var pts = TableView.IntList(s.Arr("pts"));
        var bar = TableView.IntList(s.Arr("bar")); var off = TableView.IntList(s.Arr("off"));
        var pinned = s.Arr("pinned") != null ? TableView.IntList(s.Arr("pinned")) : null;
        int turn = (int)s.Num("turn");
        bool newMove = v.last != null && v.last.k != lastK;
        GameObject arrived = null;
        int lastTo = v.last != null && v.last.t == "step" ? v.last.to : -1000;
        for (int i = 0; i < 24 && i < pts.Count; i++)
        {
            int n = Mathf.Abs(pts[i]), p = pts[i] > 0 ? 0 : 1;
            int trapped = pinned != null && i < pinned.Count ? pinned[i] : -1;
            int bas = 0;
            if (trapped >= 0 && n > 0) { Checker(trapped, StackPos(i, 0) + new Vector3(0.06f, -0.02f, 0)); bas = 1; }
            for (int k = 0; k < n; k++)
            {
                var ck = Checker(p, StackPos(i, k + bas));
                if (i == lastTo && k == n - 1) arrived = ck;
            }
        }
        for (int p = 0; p < 2; p++)
        {
            for (int k = 0; p < bar.Count && k < bar[p]; k++) Checker(p, BarPos(p, k));
            for (int k = 0; p < off.Count && k < off[p]; k++) { var co = Checker(p, OffPos(p, k), false); if (lastTo == OFF && v.last.s == p && k == off[p] - 1) arrived = co; }
        }
        // where the top checker of a point is (for the marks)
        Vector3 Top(int i)
        {
            if (i == BAR) return BarPos(turn, Mathf.Max(0, (bar.Count > turn ? bar[turn] : 1) - 1));
            int under = pinned != null && i < pinned.Count && pinned[i] >= 0 && pts[i] != 0 ? 1 : 0;
            return StackPos(i, Mathf.Max(0, Mathf.Abs(pts[i]) - 1) + under);
        }
        foreach (var f in movable ?? new List<int>()) if (f == BAR || (f >= 0 && f < 24)) Mark(Art.Ring(CR * 0.88f, CR * 1.3f), new Color(1f, 0.81f, 0.43f, 0.95f), Top(f) + Vector3.up * 0.09f);
        if (sel != -1000 && (sel == BAR || (sel >= 0 && sel < 24))) Mark(Art.Ring(CR * 0.9f, CR * 1.3f), new Color(0.23f, 0.64f, 0.57f, 0.95f), Top(sel) + Vector3.up * 0.1f);
        foreach (var t in targets ?? new List<int>())
        {
            if (t == OFF) { var g = Mark(Art.Disc(CR), new Color(1f, 0.81f, 0.43f, 0.6f), OffPos(turn, Mathf.Min(14, off.Count > turn ? off[turn] : 0)) + Vector3.up * 0.02f); g.transform.localScale = new Vector3(1.3f, 1, 2.2f); continue; }
            if (t < 0 || t >= 24) continue;
            int sign = turn == 0 ? 1 : -1;
            int mine = pts[t] * sign > 0 ? Mathf.Abs(pts[t]) : 0;
            Mark(Art.Disc(CR), new Color(1f, 0.81f, 0.43f, 0.6f), StackPos(t, mine) + Vector3.up * -0.01f);
        }
        // the dice, on the right half
        var rolled = s.Arr("rolled");
        if (rolled != null && rolled.Count == 2)
        {
            var dice = TableView.IntList(s.Arr("dice"));
            int a = (int)(double)rolled[0], b = (int)(double)rolled[1];
            for (int i = 0; i < 2; i++)
            {
                int val = i == 0 ? a : b;
                int usedCount = (a == b ? 4 : 2) - dice.Count;
                bool used = a == b ? usedCount >= (i == 0 ? 2 : 4) : !dice.Contains(val);
                var d = Art.Die(pieces, val, used ? 0.4f : 0.5f, Art.Hex("#f6efe0"), Art.Hex("#1a0f0a"));
                d.transform.localPosition = new Vector3(ColX(9) + (i == 1 ? 0.45f : -0.45f), used ? 0.62f : 0.68f, 0);
                d.transform.localRotation = Quaternion.Euler(0, i * 23 - 11, 0);
                if (newMove && v.last.t == "roll") d.AddComponent<Tumble>();
            }
        }
        if (newMove)
        {
            lastK = v.last.k;
            if (v.last.t == "step" && arrived != null && v.last.from != -1000)
            {
                var from = v.last.from == BAR ? BarPos(v.last.s, 0) : StackPos(v.last.from, Mathf.Abs(v.last.from >= 0 && v.last.from < pts.Count ? pts[v.last.from] : 0));
                var h = arrived.AddComponent<Hop>(); h.from = from; h.to = arrived.transform.localPosition; h.height = 0.8f;
            }
        }
    }
}

/* =================================================================== ludo */

public class LudoBoard : Board
{
    const float CELL = 0.7f;
    const int TRACK = 52, HOME = 56, LAP = -2;
    static readonly int[] COLOR_OFFSET = { 0, 13, 26, 39 };
    static readonly HashSet<int> Stars = new HashSet<int> { 8, 21, 34, 47 };
    public static readonly Color[] Colors = { Art.Hex("#c8402f"), Art.Hex("#2f8a4a"), Art.Hex("#e0b030"), Art.Hex("#2f6fb8") };
    static readonly Vector2Int[] BaseOrigin = { new Vector2Int(0, 0), new Vector2Int(9, 0), new Vector2Int(9, 9), new Vector2Int(0, 9) };
    static readonly Vector2[] BaseSpots = { new Vector2(1.5f, 1.5f), new Vector2(3.5f, 1.5f), new Vector2(1.5f, 3.5f), new Vector2(3.5f, 3.5f) };
    static List<Vector2Int> path;
    static Vector2Int[][] homeCol;
    static Mesh pawnMesh;
    static Material[] pawnMats;
    readonly List<(GameObject go, int i)> movableNow = new List<(GameObject, int)>();
    readonly Dictionary<string, Vector3> prevPos = new Dictionary<string, Vector3>();
    GameObject die;

    static List<Vector2Int> Path
    {
        get
        {
            if (path != null) return path;
            var p = new List<Vector2Int>();
            for (int c = 1; c <= 5; c++) p.Add(new Vector2Int(c, 6));
            for (int r = 5; r >= 0; r--) p.Add(new Vector2Int(6, r));
            p.Add(new Vector2Int(7, 0)); p.Add(new Vector2Int(8, 0));
            for (int r = 1; r <= 5; r++) p.Add(new Vector2Int(8, r));
            for (int c = 9; c <= 14; c++) p.Add(new Vector2Int(c, 6));
            p.Add(new Vector2Int(14, 7)); p.Add(new Vector2Int(14, 8));
            for (int c = 13; c >= 9; c--) p.Add(new Vector2Int(c, 8));
            for (int r = 9; r <= 14; r++) p.Add(new Vector2Int(8, r));
            p.Add(new Vector2Int(7, 14)); p.Add(new Vector2Int(6, 14));
            for (int r = 13; r >= 9; r--) p.Add(new Vector2Int(6, r));
            for (int c = 5; c >= 0; c--) p.Add(new Vector2Int(c, 8));
            p.Add(new Vector2Int(0, 7)); p.Add(new Vector2Int(0, 6));
            homeCol = new[]
            {
                new[] { new Vector2Int(1, 7), new Vector2Int(2, 7), new Vector2Int(3, 7), new Vector2Int(4, 7), new Vector2Int(5, 7) },
                new[] { new Vector2Int(7, 1), new Vector2Int(7, 2), new Vector2Int(7, 3), new Vector2Int(7, 4), new Vector2Int(7, 5) },
                new[] { new Vector2Int(13, 7), new Vector2Int(12, 7), new Vector2Int(11, 7), new Vector2Int(10, 7), new Vector2Int(9, 7) },
                new[] { new Vector2Int(7, 13), new Vector2Int(7, 12), new Vector2Int(7, 11), new Vector2Int(7, 10), new Vector2Int(7, 9) },
            };
            return path = p;
        }
    }

    /// A grid cell (column, row; row 0 is the far edge of the printed card) on the board.
    static Vector3 CellPos(float x, float y) => new Vector3((x + 0.5f - 7.5f) * CELL, 0.3f, -(y + 0.5f - 7.5f) * CELL);

    public LudoBoard(Transform parent) : base("ludo", parent)
    {
        topY = 0.3f;
        float size = CELL * 15;
        var _ = Path;
        Art.Box("Board", spin, size + 0.6f, 0.3f, size + 0.6f, Vector3.zero, Art.Mat(Color.white, 0.7f, 0, Art.WoodTex(256, 256, Art.Hex("#5a2d18"), Art.Hex("#2e130a"), Art.Hex("#7a4428"), 50, 5.5f)));
        // the printed card
        var cellOf = new Dictionary<Vector2Int, Color>();
        Color track = Art.Hex("#f7f1e3"), line = Art.Hex("#3a2a20"), star = Art.Hex("#b9a68a");
        for (int i = 0; i < path.Count; i++) { int st = System.Array.IndexOf(COLOR_OFFSET, i); cellOf[path[i]] = st >= 0 ? Colors[st] : track; }
        for (int c = 0; c < 4; c++) foreach (var h in homeCol[c]) cellOf[h] = Colors[c];
        const int PX = 1050; float cpx = PX / 15f;
        var tex = Art.Paint(PX, PX, (px, py) =>
        {
            float gx = (px + 0.5f) / cpx, gy = (PX - 1 - py + 0.5f) / cpx;   // grid coordinates, row 0 at the top (far side)
            int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
            var c = Art.Hex("#efe4cc");
            // the four bases
            for (int b = 0; b < 4; b++)
            {
                var o = BaseOrigin[b];
                if (gx >= o.x && gx < o.x + 6 && gy >= o.y && gy < o.y + 6)
                {
                    c = Colors[b];
                    if (gx >= o.x + 1 && gx < o.x + 5 && gy >= o.y + 1 && gy < o.y + 5)
                    {
                        c = Art.Hex("#f7f1e3");
                        foreach (var s in BaseSpots) if ((new Vector2(gx, gy) - new Vector2(o.x + s.x + 0.5f, o.y + s.y + 0.5f)).magnitude < 0.42f) c = Colors[b];
                    }
                    return c * (0.96f + Mathf.PerlinNoise(px * 0.2f, py * 0.2f) * 0.06f);
                }
            }
            // the centre: four triangles pointing in
            if (gx >= 6 && gx < 9 && gy >= 6 && gy < 9)
            {
                float dx = gx - 7.5f, dy = gy - 7.5f;
                int tri = Mathf.Abs(dx) > Mathf.Abs(dy) ? (dx < 0 ? 0 : 2) : (dy < 0 ? 1 : 3);
                return Colors[tri];
            }
            if (cellOf.TryGetValue(new Vector2Int(cx, cy), out var cc))
            {
                c = cc;
                float fx = gx - cx, fy = gy - cy;
                if (fx < 0.03f || fy < 0.03f || fx > 0.97f || fy > 0.97f) c = line;
                int idx = path.IndexOf(new Vector2Int(cx, cy));
                if (idx >= 0 && Stars.Contains(idx) && Star(fx - 0.5f, fy - 0.5f, 0.32f)) c = star;
            }
            return c * (0.97f + Mathf.PerlinNoise(px * 0.15f, py * 0.15f) * 0.05f);
        });
        Art.Sheet("Card", spin, size, size, new Vector3(0, 0.301f, 0), Art.Mat(new Color(0.85f, 0.84f, 0.82f), 0.1f, 0, tex));
    }

    static bool Star(float x, float y, float r)
    {
        float a = Mathf.Atan2(y, x), d = Mathf.Sqrt(x * x + y * y);
        float k = Mathf.Repeat(a + Mathf.PI / 2, Mathf.PI * 2 / 5) / (Mathf.PI * 2 / 5);
        return d < Mathf.Lerp(r * 0.45f, r, Mathf.Abs(k - 0.5f) * 2);
    }

    Vector3 PawnPos(List<int> colors, List<List<int>> pawns, int seat, int i)
    {
        int color = colors[seat], prog = pawns[seat][i];
        if (prog == LAP) { var c = path[(COLOR_OFFSET[color] + 51) % TRACK]; return CellPos(c.x, c.y); }
        if (prog < 0) { var o = BaseOrigin[color]; var s = BaseSpots[i]; return CellPos(o.x + s.x, o.y + s.y); }
        if (prog <= 50) { var c = path[(COLOR_OFFSET[color] + prog) % TRACK]; return CellPos(c.x, c.y); }
        if (prog < HOME) { var c = homeCol[color][prog - 51]; return CellPos(c.x, c.y); }
        var ctr = CellPos(7, 7);
        var dir = new[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) }[color];
        return ctr + new Vector3(dir.x * 0.55f + (i - 1.5f) * 0.12f * (dir.y != 0 ? 1 : 0), 0, -(dir.y * 0.55f + (i - 1.5f) * 0.12f * (dir.x != 0 ? 1 : 0)));
    }

    public override Pick PickAt(Vector3 l)
    {
        (GameObject go, int i) best = (null, -1); float bd = 0.6f;
        foreach (var m in movableNow) { var p = m.go.transform.localPosition; float d = new Vector2(p.x - l.x, p.z - l.z).magnitude; if (d < bd) { bd = d; best = m; } }
        if (best.go != null) return new Pick { kind = "pawn", v = best.i };
        if (die != null && new Vector2(die.transform.localPosition.x - l.x, die.transform.localPosition.z - l.z).magnitude < 1.0f) return new Pick { kind = "dice", v = 0 };
        return Pick.None;
    }
    public override Vector3 Spot(TableView v, int i)
    {
        foreach (var m in movableNow) if (m.i == i) return m.go.transform.position;
        return spin.TransformPoint(CellPos(7, 7));
    }

    public override void Render(TableView v, int sel, List<int> targets, List<int> movable)
    {
        var s = v.ludo; if (s == null) return;
        var colors = TableView.IntList(s.Arr("colors"));
        var pawns = new List<List<int>>();
        foreach (var a in s.Arr("pawns") ?? new List<object>()) pawns.Add(TableView.IntList(a as List<object>));
        int you = (int)s.Num("you", -1);
        int myColor = you >= 0 && you < colors.Count ? colors[you] : 3;
        spin.localRotation = Quaternion.Euler(0, -90 * new[] { 1, 2, 3, 0 }[myColor], 0);   // your base at the bottom-left
        Clear(pieces); Clear(marks); movableNow.Clear();
        if (pawnMesh == null)
        {
            pawnMesh = Art.Lathe(new[] { new Vector2(0, 0), new Vector2(0.25f, 0), new Vector2(0.265f, 0.02f), new Vector2(0.26f, 0.05f), new Vector2(0.2f, 0.09f), new Vector2(0.14f, 0.16f), new Vector2(0.105f, 0.26f), new Vector2(0.1f, 0.32f), new Vector2(0.16f, 0.34f), new Vector2(0.165f, 0.365f), new Vector2(0.1f, 0.38f), new Vector2(0.09f, 0.4f), new Vector2(0.13f, 0.43f), new Vector2(0.15f, 0.49f), new Vector2(0.13f, 0.56f), new Vector2(0.07f, 0.6f), new Vector2(0, 0.61f) }, 36);
            pawnMats = new Material[4];
            for (int c = 0; c < 4; c++) pawnMats[c] = Art.Mat(Colors[c], 0.85f);
        }
        bool newMove = v.last != null && v.last.k != lastK;
        var bySpot = new Dictionary<string, int>();
        for (int seat = 0; seat < pawns.Count && seat < colors.Count; seat++)
            for (int i = 0; i < pawns[seat].Count; i++)
            {
                var pos = PawnPos(colors, pawns, seat, i);
                string key = Mathf.RoundToInt(pos.x * 10) + "," + Mathf.RoundToInt(pos.z * 10);
                bySpot.TryGetValue(key, out int k); bySpot[key] = k + 1;
                if (k > 0 && pawns[seat][i] != -1) pos += new Vector3((k % 2 == 1 ? 1 : -1) * 0.14f, 0, k > 1 ? 0.14f : -0.14f);
                var g = Art.Obj("Pawn", pieces, pawnMesh, pawnMats[colors[seat]]);
                g.transform.localPosition = pos;
                string id = seat + ":" + i;
                if (newMove && prevPos.TryGetValue(id, out var prev) && (prev - pos).sqrMagnitude > 0.0001f) { var h = g.AddComponent<Hop>(); h.from = prev; h.to = pos; h.height = 0.7f; h.time = 0.4f; }
                prevPos[id] = pos;
                if (seat == you && movable != null && movable.Contains(i))
                {
                    movableNow.Add((g, i));
                    if (g.GetComponent<Hop>() == null) g.AddComponent<Bob>();
                    Mark(Art.Ring(0.24f, 0.32f), new Color(1f, 0.81f, 0.43f, 0.9f), new Vector3(pos.x, 0.31f, pos.z));
                }
            }
        die = null;
        bool showDie = s.TryGetValue("die", out var dv) && dv is double || (v.last != null && (v.last.t == "roll" || v.last.t == "pass"));
        if (showDie)
        {
            int val = dv is double dd ? (int)dd : v.last != null && v.last.dice.Count > 0 ? v.last.dice[0] : 1;
            die = Art.Die(pieces, Mathf.Clamp(val, 1, 6), 0.55f, Art.Hex("#f6efe0"), Art.Hex("#c8402f"));
            die.transform.localPosition = CellPos(7, 7) + Vector3.up * 0.3f;
            if (newMove && v.last.t == "roll") die.AddComponent<Tumble>();
        }
        if (newMove) lastK = v.last.k;
    }
}
