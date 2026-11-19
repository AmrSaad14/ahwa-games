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
    const float FrameW = 0.55f, PS = 1.1f;              // frame width, piece scale
    static Material whiteMat, blackMat, felt;
    static string pieceSkin;
    static readonly Dictionary<string, (Mesh mesh, bool accent)[]> meshes = new Dictionary<string, (Mesh, bool)[]>();
    readonly Dictionary<int, GameObject> bySq = new Dictionary<int, GameObject>();
    readonly Dictionary<int, (string p, GameObject go)> kept = new Dictionary<int, (string, GameObject)>();
    string keptSkin;
    string[] prevBoard;

    public ChessBoard(Transform parent) : base("chess", parent)
    {
        topY = 0.3f;
        float B = S * 8;
        var L = Skins.Chess(Skins.Worn("chess"));
        var frameMat = L.metalFrame ? Art.Mat(L.frame[0], 0.7f, 0.85f) : Art.Mat(Color.white, 0.62f, 0, Art.WoodTex(512, 512, L.frame[0], L.frame[1], L.frame[2], 60, 3.3f));
        Art.Box("Frame", spin, B + 2 * FrameW, 0.3f, B + 2 * FrameW, Vector3.zero, frameMat);
        // a stringing line round the squares
        Art.Box("Line", spin, B + 0.12f, 0.302f, B + 0.12f, Vector3.zero, Art.Mat(L.line, 0.5f));
        // the squares (the worn skin's): each with its own piece of grain, marble, or plain
        var light = L.light; var dark = L.dark;
        var tex = Art.Paint(512, 512, (x, y) =>
        {
            int f = x / 64, r = y / 64;
            var w = (f + r) % 2 == 1 ? light : dark;
            var c = L.kind == "wood" ? Art.Wood(x * 3 + f * 97, y * 3 + r * 53, w[0], w[1], w[2], 50, f * 0.7f + r * 1.3f)
                  : L.kind == "marble" ? Skins.Marble(x * 2 + f * 97, y * 2 + r * 53, w, f * 0.7f + r * 1.3f)
                  : w[0] * (0.97f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.06f);
            return c;
        });
        // matte, so the light never washes a square out
        Art.Sheet("Squares", spin, B, B, new Vector3(0, 0.303f, 0), Art.Mat(Color.white, L.kind == "wood" ? 0.3f : 0.12f, 0, tex));   // a lacquered wooden board keeps a soft sheen
        coordsFor = null;
    }

    // the files (a–h) and ranks (1–8) along the frame, the right way up for you (they don't turn with the board)
    Transform coords; string coordsFor;
    void Coords(bool black)
    {
        string key = black ? "b" : "w";
        if (coordsFor == key) return;
        coordsFor = key;
        if (coords != null) Object.Destroy(coords.gameObject);
        coords = new GameObject("Coords").transform; coords.SetParent(root.transform, false);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var ink = Skins.Chess(Skins.Worn("chess")).line;
        float edge = S * 4 + FrameW * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            int f = black ? 7 - i : i, r = black ? 7 - i : i;
            Label(coords, font, ((char)('a' + f)).ToString(), new Vector3((i - 3.5f) * S, 0.31f, -edge), ink);
            Label(coords, font, (r + 1).ToString(), new Vector3(-edge, 0.31f, (i - 3.5f) * S), ink);
        }
    }
    static void Label(Transform parent, Font font, string text, Vector3 at, Color c)
    {
        var go = new GameObject("Coord " + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text; tm.font = font; tm.fontSize = 64; tm.characterSize = 0.045f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = c; tm.fontStyle = FontStyle.Bold;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
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
        bool black = c.Str("you") == "b";
        spin.localRotation = Quaternion.Euler(0, black ? 180 : 0, 0);
        Coords(black);
        Clear(marks);
        var board = c.Arr("board");
        var last = c.Obj("last");
        int lastFrom = last != null ? (int)last.Num("from") : -1, lastTo = last != null ? (int)last.Num("to") : -1;
        // the pieces are kept from one drawing to the next: only the ones that changed square are made again
        // (each piece is a dozen parts with their outlines; making all 32 on every update was the costly part)
        string skin = Skins.Worn("pieces");
        if (skin != keptSkin) { foreach (var kv in kept) if (kv.Value.go != null) Object.Destroy(kv.Value.go); kept.Clear(); keptSkin = skin; }
        bySq.Clear();
        for (int sq = 0; sq < 64; sq++)
        {
            var p = board != null && sq < board.Count ? board[sq] as string : null;
            kept.TryGetValue(sq, out var k);
            if (string.IsNullOrEmpty(p)) { if (k.go != null) Object.Destroy(k.go); kept.Remove(sq); continue; }
            if (k.go != null && k.p == p)
            {
                var bob = k.go.GetComponent<Bob>(); if (bob != null) Object.DestroyImmediate(bob);
                if (k.go.GetComponent<Hop>() == null) k.go.transform.localPosition = Pos(sq);
                bySq[sq] = k.go; continue;
            }
            if (k.go != null) Object.Destroy(k.go);
            var g = Piece(p, pieces);
            g.transform.localPosition = Pos(sq);
            kept[sq] = (p, g); bySq[sq] = g;
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
        // the piece you picked stands up off the board
        if (sel >= 0 && bySq.TryGetValue(sel, out var picked) && picked.GetComponent<Hop>() == null) { picked.transform.localPosition = Pos(sel) + Vector3.up * 0.16f; picked.AddComponent<Bob>(); }
        // the move just made slides in (a knight jumps); anything it took tips over and goes
        if (v.last != null && v.last.k != lastK)
        {
            lastK = v.last.k;
            if (v.last.t == "move" && v.last.from >= 0 && bySq.TryGetValue(v.last.to, out var moved))
            {
                bool knight = board != null && v.last.to < board.Count && (board[v.last.to] as string ?? "").ToLowerInvariant() == "n";
                var h = moved.AddComponent<Hop>(); h.from = Pos(v.last.from); h.to = Pos(v.last.to); h.height = knight ? 0.7f : 0.06f; h.time = knight ? 0.38f : 0.3f;
                // castling: the rook comes across too
                if ((board[v.last.to] as string ?? "").ToLowerInvariant() == "k" && Mathf.Abs((v.last.to & 7) - (v.last.from & 7)) == 2)
                {
                    int rank = v.last.to & ~7, rf = (v.last.to & 7) == 6 ? rank + 7 : rank, rt = (v.last.to & 7) == 6 ? rank + 5 : rank + 3;
                    if (bySq.TryGetValue(rt, out var rook)) { var rh = rook.AddComponent<Hop>(); rh.from = Pos(rf); rh.to = Pos(rt); rh.height = 0.06f; rh.time = 0.3f; }
                }
                if (v.last.capture && prevBoard != null)
                {
                    // what stood where it landed (or, en passant, beside it)
                    int at = !string.IsNullOrEmpty(prevBoard[v.last.to]) ? v.last.to : (v.last.from & ~7) | (v.last.to & 7);
                    if (at >= 0 && at < 64 && !string.IsNullOrEmpty(prevBoard[at]))
                    {
                        var ghost = Piece(prevBoard[at], pieces);
                        ghost.transform.localPosition = Pos(at);
                        var f = ghost.AddComponent<Fall>();
                        Object.Destroy(ghost, 0.7f);
                    }
                }
            }
        }
        if (board != null) { prevBoard = new string[64]; for (int sq = 0; sq < 64 && sq < board.Count; sq++) prevBoard[sq] = board[sq] as string; }
    }

    public static int Worth(string p) { switch (p.ToLowerInvariant()) { case "q": return 9; case "r": return 5; case "b": return 3; case "n": return 3; default: return 1; } }

    // The pieces, drawn to read at a glance from the chair like the pictures on the screens: a broad base, a
    // clear outline round each piece (dark round the light ones, gold round the dark), and big, simple tops —
    // the pawn's ball, the rook's battlements, the bishop's mitre with its notch, the queen's crown of balls,
    // the king's cross, the horse's head.
    static readonly Vector2[] Base = { V(0, 0), V(0.4f, 0), V(0.42f, 0.02f), V(0.42f, 0.07f), V(0.39f, 0.095f), V(0.35f, 0.11f), V(0.36f, 0.14f), V(0.33f, 0.165f), V(0.27f, 0.19f) };
    static Vector2 V(float r, float y) => new Vector2(r, y);
    static Vector2[] With(params Vector2[] rest) { var l = new List<Vector2>(Base); l.AddRange(rest); return l.ToArray(); }
    /// A ball on the axis: radius r, centre at height y (closed at the bottom so it sits on what's under it).
    public static Mesh Ball(float r, float y, int seg = 24)
    {
        var pr = new List<Vector2> { V(0, y - r) };
        for (int a = -80; a <= 90; a += 10) pr.Add(V(Mathf.Cos(a * Mathf.Deg2Rad) * r, y + Mathf.Sin(a * Mathf.Deg2Rad) * r));
        pr[pr.Count - 1] = V(0, y + r);
        return Art.Lathe(pr.ToArray(), seg);
    }
    static Mesh Block(Vector3 at, Vector3 size, float turn = 0)
    {
        var m = new Mesh();
        m.CombineMeshes(new[] { new CombineInstance { mesh = Art.Prim(PrimitiveType.Cube), transform = Matrix4x4.TRS(at, Quaternion.Euler(0, turn, 0), size) } }, true, true);
        m.RecalculateBounds();
        return m;
    }

    /// The parts of a piece; an accent part is in the outline's colour (the bishop's notch).
    static (Mesh mesh, bool accent)[] MeshesFor(string t)
    {
        if (meshes.TryGetValue(t, out var cached)) return cached;
        var list = new List<(Mesh, bool)>();
        switch (t)
        {
            case "p":
                list.Add((Art.Lathe(With(V(0.24f, 0.22f), V(0.17f, 0.3f), V(0.12f, 0.38f), V(0.21f, 0.41f), V(0.22f, 0.44f), V(0.12f, 0.47f), V(0.11f, 0.5f), V(0, 0.5f))), false));
                list.Add((Ball(0.18f, 0.64f), false)); break;
            case "r":
                list.Add((Art.Lathe(With(V(0.29f, 0.22f), V(0.26f, 0.32f), V(0.24f, 0.62f), V(0.27f, 0.66f), V(0.33f, 0.7f), V(0.33f, 0.84f), V(0.24f, 0.84f), V(0.23f, 0.79f), V(0, 0.79f))), false));
                list.Add((Crenels(), false)); break;
            case "n":
                list.Add((Art.Lathe(With(V(0.3f, 0.22f), V(0.28f, 0.27f), V(0.33f, 0.3f), V(0.33f, 0.34f), V(0, 0.34f))), false));
                list.Add((KnightHead(), false)); break;
            case "b":
                list.Add((Art.Lathe(With(V(0.26f, 0.22f), V(0.18f, 0.34f), V(0.13f, 0.5f), V(0.12f, 0.6f), V(0.23f, 0.63f), V(0.24f, 0.67f), V(0.13f, 0.7f),
                    V(0.17f, 0.74f), V(0.215f, 0.82f), V(0.22f, 0.9f), V(0.2f, 0.98f), V(0.15f, 1.06f), V(0.08f, 1.12f), V(0, 1.14f))), false));
                list.Add((Ball(0.075f, 1.19f), false));
                // the mitre's notch: a dark cut across the front
                list.Add((Block(new Vector3(0, 0.95f, 0.13f), new Vector3(0.05f, 0.2f, 0.16f), 0), true)); break;
            case "q":
                list.Add((Art.Lathe(With(V(0.29f, 0.22f), V(0.2f, 0.38f), V(0.15f, 0.58f), V(0.14f, 0.72f), V(0.25f, 0.75f), V(0.26f, 0.79f), V(0.15f, 0.82f),
                    V(0.16f, 0.86f), V(0.24f, 0.96f), V(0.3f, 1.04f), V(0.27f, 1.06f), V(0.17f, 1.08f), V(0.16f, 1.13f), V(0.08f, 1.17f), V(0, 1.18f))), false));
                {
                    // the crown's nine balls as one mesh (one draw, one outline)
                    var b = Ball(0.06f, 0, 12); var ci = new CombineInstance[9];
                    for (int i = 0; i < 9; i++) { float a = i * 40f * Mathf.Deg2Rad; ci[i] = new CombineInstance { mesh = b, transform = Matrix4x4.Translate(new Vector3(Mathf.Cos(a) * 0.27f, 1.08f, Mathf.Sin(a) * 0.27f)) }; }
                    var mm = new Mesh(); mm.CombineMeshes(ci, true, true); mm.RecalculateBounds(); list.Add((mm, false));
                }
                list.Add((Ball(0.085f, 1.25f), false)); break;
            default:   // king
                list.Add((Art.Lathe(With(V(0.3f, 0.22f), V(0.21f, 0.38f), V(0.16f, 0.6f), V(0.15f, 0.76f), V(0.26f, 0.79f), V(0.27f, 0.83f), V(0.16f, 0.86f),
                    V(0.17f, 0.9f), V(0.25f, 0.99f), V(0.28f, 1.07f), V(0.23f, 1.11f), V(0.14f, 1.14f), V(0.1f, 1.18f), V(0, 1.19f))), false));
                list.Add((Cross(), false)); break;
        }
        var arr = list.ToArray();
        meshes[t] = arr;
        return arr;
    }

    static Mesh Crenels()
    {
        // four big battlements round the top of the rook, gaps between them
        var combine = new List<CombineInstance>();
        var cube = Art.Prim(PrimitiveType.Cube);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            var mtx = Matrix4x4.TRS(Quaternion.Euler(0, a, 0) * new Vector3(0.255f, 0.9f, 0), Quaternion.Euler(0, a, 0), new Vector3(0.12f, 0.14f, 0.22f));
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
            new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(0, 1.33f, 0), Quaternion.identity, new Vector3(0.1f, 0.34f, 0.1f)) },
            new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(new Vector3(0, 1.37f, 0), Quaternion.identity, new Vector3(0.3f, 0.1f, 0.1f)) },
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
        var m = Inflate(pts, 0.15f, 0.13f);
        // turn it so the muzzle (shape -x) faces +z, the thickness runs along x; then carve it narrower to the front and top
        var vs = m.vertices;
        for (int i = 0; i < vs.Length; i++)
        {
            var s = vs[i];
            float x = s.z, y = s.y + 0.31f, z = -s.x;
            float front = Mathf.Clamp01((z + 0.05f) / 0.45f), top = Mathf.Clamp01((y - 1.05f) / 0.3f);
            x *= (1 - 0.5f * front) * (1 - 0.35f * top);
            vs[i] = new Vector3(-z, y, x);   // then a quarter turn, so from your chair you see the horse side on
        }
        m.vertices = vs;   // (x, y, z) → (z, y, −x) is a turn, not a mirror: the triangles still face out
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    /// A flat outline blown up like a cushion: thickest in the middle, rounding off to a soft edge all the way
    /// round (so the horse's head has a rounded cheek and neck, not the flat sides of a cut-out). The outline lies
    /// in x/y; the thickness (up to ±half) runs along z, reaching full thickness `round` in from the edge.
    static Mesh Inflate(List<Vector2> outline, float half, float round)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in outline) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
        const int N = 44;
        float step = Mathf.Max(maxX - minX, maxY - minY) / (N - 1);
        int nx = Mathf.CeilToInt((maxX - minX) / step) + 3, ny = Mathf.CeilToInt((maxY - minY) / step) + 3;
        float ox = minX - step, oy = minY - step;
        bool Inside(Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                var a = outline[i]; var b = outline[j];
                if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }
        Vector2 Nearest(Vector2 q, out float dist)
        {
            Vector2 best = q; dist = float.MaxValue;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                var a = outline[j]; var b = outline[i]; var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                var c = a + ab * t; float d = (q - c).magnitude;
                if (d < dist) { dist = d; best = c; }
            }
            return best;
        }
        var inside = new bool[nx, ny];
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) inside[i, j] = Inside(new Vector2(ox + i * step, oy + j * step));
        // a cell is drawn when any corner is inside; its corners outside are pulled onto the outline (a smooth edge)
        var verts = new List<Vector3>(); var tris = new List<int>();
        var index = new int[nx, ny, 2];
        for (int i = 0; i < nx; i++) for (int j = 0; j < ny; j++) { index[i, j, 0] = -1; index[i, j, 1] = -1; }
        int Vert(int i, int j, int side)
        {
            if (index[i, j, side] >= 0) return index[i, j, side];
            var q = new Vector2(ox + i * step, oy + j * step);
            float d; var near = Nearest(q, out d);
            float z;
            if (!inside[i, j]) { q = near; z = 0; }
            else { float k = Mathf.Clamp01(d / round); z = half * Mathf.Sqrt(k * (2 - k)); }   // a quarter circle in profile
            verts.Add(new Vector3(q.x, q.y, side == 0 ? z : -z));
            return index[i, j, side] = verts.Count - 1;
        }
        for (int i = 0; i < nx - 1; i++)
            for (int j = 0; j < ny - 1; j++)
            {
                if (!(inside[i, j] || inside[i + 1, j] || inside[i, j + 1] || inside[i + 1, j + 1])) continue;
                for (int side = 0; side < 2; side++)
                {
                    int a = Vert(i, j, side), b = Vert(i + 1, j, side), c = Vert(i + 1, j + 1, side), e = Vert(i, j + 1, side);
                    if (side == 0) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(e); tris.Add(c); }
                    else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(e); }
                }
            }
        var m = new Mesh();
        if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(verts); m.SetTriangles(tris, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        Art.FaceOutward3D(m);
        return m;
    }

    /// A copy of a mesh pushed out along its normals and turned inside out: drawn behind the piece, only its rim
    /// shows, as an outline.
    public static Mesh Shell(Mesh src, float d)
    {
        var m = new Mesh();
        var vs = src.vertices; var ns = src.normals;
        for (int i = 0; i < vs.Length; i++) vs[i] += (i < ns.Length ? ns[i] : Vector3.zero) * d;
        m.vertices = vs;
        var tr = src.triangles;
        for (int i = 0; i < tr.Length; i += 3) { int x = tr[i + 1]; tr[i + 1] = tr[i + 2]; tr[i + 2] = x; }
        m.triangles = tr; m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    static readonly Dictionary<Mesh, Mesh> shells = new Dictionary<Mesh, Mesh>();
    static Material edgeLight, edgeDark, accentLight, accentDark;

    public static GameObject Piece(string p, Transform parent)
    {
        bool white = p == p.ToUpperInvariant();
        string skin = Skins.Worn("pieces");
        if (whiteMat == null || pieceSkin != skin)
        {
            pieceSkin = skin;
            var (w, b) = Skins.Pieces(skin);
            whiteMat = Skins.Mat(skin + ":w", w, 1); blackMat = Skins.Mat(skin + ":b", b, 2);
            felt = Art.Mat(Art.Hex("#1f4a2c"), 0.05f);
            // the outlines: dark brown round the light pieces, gold round the dark ones
            Material Edge(Color c) { var m = new Material(TileArt.Clear) { color = c }; m.renderQueue = 2450; return m; }
            edgeLight = Edge(Art.Hex("#2a1a10")); edgeDark = Edge(Art.Hex("#d9b46a"));
            accentLight = Art.Mat(Art.Hex("#3a2a20"), 0.3f); accentDark = Art.Mat(Art.Hex("#d9b46a"), 0.6f, 0.6f);
        }
        var g = new GameObject("Piece " + p); g.transform.SetParent(parent, false);
        foreach (var (mesh, accent) in MeshesFor(p.ToLowerInvariant()))
        {
            Art.Obj("Part", g.transform, mesh, accent ? (white ? accentLight : accentDark) : white ? whiteMat : blackMat);
            if (accent || Perf.Level < Perf.Balanced2) continue;   // the outlines: a second drawing of each part, kept for phones that can afford it
            if (!shells.TryGetValue(mesh, out var sh)) shells[mesh] = sh = Shell(mesh, 0.022f);
            Art.Obj("Edge", g.transform, sh, white ? edgeLight : edgeDark, false);
        }
        var baize = Art.Obj("Felt", g.transform, Art.Disc(0.38f), felt, false); baize.transform.localPosition = Vector3.up * 0.002f; baize.transform.localRotation = Quaternion.Euler(180, 0, 0);
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
    static string checkerSkin;
    Dictionary<string, object> s;

    public TawlaBoard(Transform parent) : base("tawla", parent)
    {
        topY = FIELD_Y;
        spin.localScale = Vector3.one * 0.95f;    // a little smaller than the web board, so it fits the table with room for hands
        var L = Skins.Tawla(Skins.Worn("tawla"));
        var side = Art.Mat(Color.white, 0.6f, 0, Art.WoodTex(512, 128, L.box[0], L.box[1], L.box[2], 50, 4.2f));
        Art.Box("Box", spin, TW, FIELD_Y, TD, Vector3.zero, side);
        // the playing fields (the worn skin's woods) with inlaid points, and the felt-lined tray
        Color field0 = L.field[0], field1 = L.field[1], field2 = L.field[2];
        Color ptA0 = L.ptA[0], ptA1 = L.ptA[1], ptA2 = L.ptA[2];
        Color ptB0 = L.ptB[0], ptB1 = L.ptB[1], ptB2 = L.ptB[2];
        int W = Mathf.RoundToInt(1024 * Perf.PaintScale), H = Mathf.RoundToInt(688 * Perf.PaintScale);
        var tex = Art.Paint(W, H, (px, py) =>
        {
            float x = (px + 0.5f) / W * TW - TW / 2;              // board x
            float zf = (py + 0.5f) / H * TD - TD / 2;             // board z (away from you is +)
            float zw = -zf;                                       // the web game's z (toward you is +)
            var wood = Art.Wood(px, py, field0, field1, field2, 70, 0.5f);
            // outside the fields: the box's own wood shows (the rails sit on top)
            if (Mathf.Abs(x) > TW / 2 - EDGE || Mathf.Abs(zf) > TD / 2 - EDGE) return side.color * Art.Wood(px, py, L.box[0], L.box[1], L.box[2], 50, 4.2f);
            if (x > TW / 2 - EDGE - TRAY) return L.tray * (0.9f + Mathf.PerlinNoise(px * 0.3f, py * 0.3f) * 0.15f);
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
                if (Mathf.Abs(x - cx) > w - 0.02f) pc = Color.Lerp(pc, L.line, 0.6f);   // a pale inlay line
                return pc;
            }
            return wood;
        });
        Art.Sheet("Field", spin, TW, TD, new Vector3(0, FIELD_Y + 0.001f, 0), Art.Mat(new Color(0.68f, 0.66f, 0.64f), 0.25f, 0, tex));
        // the rails: four sides, the middle bar and the tray's divider, each with a strip of mother-of-pearl mosaic on
        // top (pearl diamonds set in dark wood between two fine brass lines, like the inlaid boards of Damascus and Cairo)
        var mosaic = Mosaic(L);
        var railMats = new Dictionary<int, Material>();
        void Rail(float x, float z, float w, float d)
        {
            Art.Box("Rail", spin, w, RAIL_H, d, new Vector3(x, FIELD_Y, z), side);
            float len = Mathf.Max(w, d) - 0.1f, wide = Mathf.Max(0.04f, Mathf.Min(w, d) - 0.1f);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / wide));
            if (!railMats.TryGetValue(n, out var m)) { m = Art.Mat(Color.white, 0.62f, 0.05f, mosaic); m.mainTextureScale = new Vector2(n, 1); railMats[n] = m; }
            var strip = Art.Sheet("Pearl", spin, len, wide, new Vector3(x, FIELD_Y + RAIL_H + 0.003f, z), m);
            if (d > w) strip.transform.localRotation = Quaternion.Euler(90, 90, 0);   // the pattern runs along the rail
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

    /// One square of the rails' mosaic (it repeats along them): a pearl diamond, shimmering a little pink and
    /// green, in dark wood, a brass line along each edge.
    static Texture2D Mosaic(Skins.TawlaLook L)
    {
        const int S = 64;
        var t = Art.Paint(S, S, (x, y) =>
        {
            if (y < 3 || y >= S - 3) return Color.Lerp(Art.Hex("#c9a25a"), Color.white, y == 1 || y == S - 2 ? 0.25f : 0f);   // brass
            float u = Mathf.Abs(x + 0.5f - S / 2f) / (S / 2f), v = Mathf.Abs(y + 0.5f - S / 2f) / (S / 2f - 3);
            float dd = u + v;   // a diamond touching the four sides
            if (dd > 0.86f) return Color.Lerp(L.box[1], L.box[0], Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.6f);   // the dark wood around
            if (dd > 0.78f) return Color.Lerp(L.box[1], Color.black, 0.3f);                                          // the cut line
            // mother-of-pearl: small pieces, each a slightly different sheen
            float n = Mathf.PerlinNoise(x * 0.09f + 11f, y * 0.09f + 3f), cellN = Mathf.PerlinNoise(Mathf.Floor(x / 7f) * 1.7f, Mathf.Floor(y / 7f) * 2.3f);
            var pink = new Color(1f, 0.9f, 0.94f); var green = new Color(0.86f, 1f, 0.95f);
            var c = Color.Lerp(L.pearl, n > 0.5f ? pink : green, Mathf.Abs(n - 0.5f) * 0.7f);
            c *= 0.9f + cellN * 0.2f;
            c.a = 1;
            return c;
        });
        t.wrapMode = TextureWrapMode.Repeat;
        return t;
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
        string skin = Skins.Worn("checkers");
        if (checkerMats == null || checkerSkin != skin)
        {
            checkerSkin = skin;
            var (a, b) = Skins.Checkers(skin);
            checkerMats = new[] { Skins.Mat(skin + ":a", a, 1), Skins.Mat(skin + ":b", b, 2) };
        }
        if (checker == null)
        {
            checker = Art.Lathe(new[] { new Vector2(0, -0.07f), new Vector2(CR - 0.025f, -0.07f), new Vector2(CR, -0.05f), new Vector2(CR, 0.045f), new Vector2(CR - 0.02f, 0.07f), new Vector2(CR * 0.78f, 0.07f), new Vector2(CR * 0.74f, 0.058f), new Vector2(CR * 0.7f, 0.07f), new Vector2(CR * 0.3f, 0.07f), new Vector2(CR * 0.27f, 0.062f), new Vector2(CR * 0.24f, 0.07f), new Vector2(0, 0.07f) }, 40);
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
                var (dFace, dPip) = Skins.Dice(Skins.Worn("dice"));
                var d = Art.Die(pieces, val, used ? 0.4f : 0.5f, dFace, dPip);
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
    static Mesh pawnMesh, pawnHead, pawnBand, pawnEdge, headEdge;
    static Material[] pawnMats;
    static Material pawnWhite, pawnOutline;
    readonly List<(GameObject go, int i)> movableNow = new List<(GameObject, int)>();
    static Material shadowMat;
    static Mesh shadowDisc;
    float rolledAt = -10;
    readonly Dictionary<string, (GameObject go, int color)> pawnGo = new Dictionary<string, (GameObject, int)>();
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
        int PX = Mathf.RoundToInt(1050 * Perf.PaintScale / 15f) * 15; float cpx = PX / 15f;   // finer on good phones
        var pathIndex = new Dictionary<Vector2Int, int>();
        for (int i = 0; i < path.Count; i++) pathIndex[path[i]] = i;
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
                if (pathIndex.TryGetValue(new Vector2Int(cx, cy), out int idx) && Stars.Contains(idx) && Star(fx - 0.5f, fy - 0.5f, 0.32f)) c = star;
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

    /// Where a pawn of a colour stands at a progress (-1 base, 0…50 track, LAP, 51…55 home column, 56 home).
    static Vector3 ProgPos(int color, int prog, int i)
    {
        if (prog == LAP) { var c = path[(COLOR_OFFSET[color] + 51) % TRACK]; return CellPos(c.x, c.y); }
        if (prog < 0) { var o = BaseOrigin[color]; var s = BaseSpots[i]; return CellPos(o.x + s.x, o.y + s.y); }
        if (prog <= 50) { var c = path[(COLOR_OFFSET[color] + prog) % TRACK]; return CellPos(c.x, c.y); }
        if (prog < HOME) { var c = homeCol[color][prog - 51]; return CellPos(c.x, c.y); }
        var ctr = CellPos(7, 7);
        var dir = new[] { new Vector2(-1, 0), new Vector2(0, -1), new Vector2(1, 0), new Vector2(0, 1) }[color];
        return ctr + new Vector3(dir.x * 0.55f + (i - 1.5f) * 0.12f * (dir.y != 0 ? 1 : 0), 0, -(dir.y * 0.55f + (i - 1.5f) * 0.12f * (dir.x != 0 ? 1 : 0)));
    }
    Vector3 PawnPos(List<int> colors, List<List<int>> pawns, int seat, int i) => ProgPos(colors[seat], pawns[seat][i], i);

    /// The squares a pawn walks through from one progress to the next, one per pip of the die.
    static List<Vector3> Walk(int color, int from, int to, int i)
    {
        var pts = new List<Vector3> { ProgPos(color, from, i) };
        if (from < 0 && from != LAP) { pts.Add(ProgPos(color, to, i)); return pts; }          // out of the base: one hop
        int rf = from == LAP ? 51 : from;
        if (to != LAP && to > rf && to - rf <= 6)
            for (int p = rf + 1; p <= to; p++) pts.Add(ProgPos(color, p, i));              // straight on (the track, then the home column)
        else
        {
            // going round again (master): count on round the track
            int rt = to == LAP ? 51 : to, n = ((rt - rf) % 52 + 52) % 52;
            if (n == 0 || n > 6) { pts.Add(ProgPos(color, to, i)); return pts; }
            for (int k2 = 1; k2 <= n; k2++) { int r = (rf + k2) % 52; pts.Add(ProgPos(color, r == 51 ? LAP : r, i)); }
        }
        return pts;
    }

    /// Where a pawn would land with this die (to show you before you choose).
    static int Dest(int prog, int die, bool circling)
    {
        if (prog == -1) return 0;
        if (circling && (prog == LAP || (prog >= 0 && prog <= 50)))
        {
            int rel = (prog == LAP ? 51 : prog) + die;
            if (rel <= 50) return rel;
            int r = rel % 52; return r == 51 ? LAP : r;
        }
        return prog + die;
    }

    // the walks under way: pawn → its path, when it started, and how long it waits first (a taken pawn waits for
    // the one that took it to arrive)
    readonly Dictionary<string, (List<Vector3> pts, float start, float delay)> walks = new Dictionary<string, (List<Vector3>, float, float)>();
    readonly Dictionary<string, int> prevProg = new Dictionary<string, int>();

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
        int you = (int)s.Num("you", -1), turn = (int)s.Num("turn", -1);
        int myColor = you >= 0 && you < colors.Count ? colors[you] : 3;
        spin.localRotation = Quaternion.Euler(0, -90 * new[] { 1, 2, 3, 0 }[myColor], 0);   // your base at the bottom-left
        Clear(marks); movableNow.Clear();
        if (die != null) { Object.Destroy(die); die = null; }
        var seen = new HashSet<string>();
        if (pawnMesh == null)
        {
            // the pawn: a broad flared foot, a cone, a collar with a white band, a big ball head, in glossy
            // plastic with a dark outline, so each one reads at a glance from the chair
            pawnMesh = Art.Lathe(new[] { new Vector2(0, 0), new Vector2(0.27f, 0), new Vector2(0.285f, 0.02f), new Vector2(0.28f, 0.06f), new Vector2(0.24f, 0.085f), new Vector2(0.2f, 0.1f), new Vector2(0.17f, 0.16f), new Vector2(0.14f, 0.26f), new Vector2(0.115f, 0.36f), new Vector2(0.1f, 0.42f), new Vector2(0.17f, 0.44f), new Vector2(0.18f, 0.47f), new Vector2(0.11f, 0.5f), new Vector2(0, 0.5f) }, 40);
            pawnHead = ChessBoard.Ball(0.16f, 0.61f, 28);
            var ring = new List<Vector2>();
            for (int a = 0; a <= 360; a += 30) ring.Add(new Vector2(0.172f + Mathf.Cos(a * Mathf.Deg2Rad) * 0.022f, 0.455f + Mathf.Sin(a * Mathf.Deg2Rad) * 0.022f));
            pawnBand = Art.Lathe(ring.ToArray(), 40);
            pawnEdge = ChessBoard.Shell(pawnMesh, 0.02f); headEdge = ChessBoard.Shell(pawnHead, 0.02f);
            pawnMats = new Material[4];
            for (int c = 0; c < 4; c++) pawnMats[c] = Art.Mat(Colors[c], 0.82f, 0.05f);
            pawnWhite = Art.Mat(Art.Hex("#fbf6ea"), 0.7f);
            pawnOutline = new Material(TileArt.Clear) { color = Art.Hex("#1a120c") }; pawnOutline.renderQueue = 2450;
        }
        bool newMove = v.last != null && v.last.k != lastK;
        // a pawn just moved: it walks the squares; anyone it knocked off goes home once it gets there
        if (newMove && v.last.t == "pawn" && v.last.s >= 0 && v.last.s < pawns.Count && v.last.s < colors.Count)
        {
            int seat = v.last.s, i = v.last.i;
            string id = seat + ":" + i;
            if (i >= 0 && i < pawns[seat].Count && prevProg.TryGetValue(id, out int from) && from != pawns[seat][i])
            {
                var w = Walk(colors[seat], from, pawns[seat][i], i);
                // a move made straight after the throw (the only pawn that could go): let the die land first
                float wait = Mathf.Max(0, 0.6f - (Time.time - rolledAt));
                walks[id] = (w, Time.time, wait);
                float arrive = wait + (w.Count - 1) * 0.17f;
                for (int os = 0; os < pawns.Count && os < colors.Count; os++)
                    for (int oi = 0; oi < pawns[os].Count; oi++)
                    {
                        string oid = os + ":" + oi;
                        if (os == seat || pawns[os][oi] != -1 || !prevProg.TryGetValue(oid, out int was) || was == -1) continue;
                        walks[oid] = (new List<Vector3> { ProgPos(colors[os], was, oi), ProgPos(colors[os], -1, oi) }, Time.time, arrive);
                    }
            }
        }
        // the turn: a glow round the base of whoever is playing
        if (turn >= 0 && turn < colors.Count)
        {
            var o = BaseOrigin[colors[turn]]; var cc = Colors[colors[turn]];
            var ctr = CellPos(o.x + 2.5f, o.y + 2.5f) + Vector3.up * 0.004f;
            float side = CELL * 6, th = 0.09f;
            foreach (var (dx, dz, sx, sz) in new[] { (0f, side / 2, side, th), (0f, -side / 2, side, th), (side / 2, 0f, th, side), (-side / 2, 0f, th, side) })
            {
                var g = Mark(QuadUp, new Color(1f, 0.9f, 0.55f, 0.85f), ctr + new Vector3(dx, 0, dz));
                g.transform.localScale = new Vector3(sx, 1, sz);
            }
        }
        var bySpot = new Dictionary<string, int>();
        for (int seat = 0; seat < pawns.Count && seat < colors.Count; seat++)
            for (int i = 0; i < pawns[seat].Count; i++)
            {
                var pos = PawnPos(colors, pawns, seat, i);
                string key = Mathf.RoundToInt(pos.x * 10) + "," + Mathf.RoundToInt(pos.z * 10);
                bySpot.TryGetValue(key, out int k); bySpot[key] = k + 1;
                if (k > 0 && pawns[seat][i] != -1) pos += new Vector3((k % 2 == 1 ? 1 : -1) * 0.14f, 0, k > 1 ? 0.14f : -0.14f);
                // the pawns are kept from one drawing to the next (made once, moved after)
                string id = seat + ":" + i;
                seen.Add(id);
                if (!pawnGo.TryGetValue(id, out var kept) || kept.go == null || kept.color != colors[seat])
                {
                    if (kept.go != null) Object.Destroy(kept.go);
                    var ng = Art.Obj("Pawn", pieces, pawnMesh, pawnMats[colors[seat]]);
                    Art.Obj("Head", ng.transform, pawnHead, pawnMats[colors[seat]]);
                    Art.Obj("Band", ng.transform, pawnBand, pawnWhite, false);
                    if (Perf.Level >= Perf.Balanced2) { Art.Obj("Edge", ng.transform, pawnEdge, pawnOutline, false); Art.Obj("HeadEdge", ng.transform, headEdge, pawnOutline, false); }
                    ng.transform.localScale = Vector3.one * 1.25f;
                    // a soft shadow under it, so it reads against the card
                    var sh = Art.Obj("Shadow", ng.transform, shadowDisc ??= Art.Disc(0.27f), shadowMat ??= Art.Glow(new Color(0, 0, 0, 0.28f)), false);
                    sh.transform.localPosition = Vector3.up * 0.003f;
                    kept = (ng, colors[seat]); pawnGo[id] = kept;
                }
                var g = kept.go;
                foreach (var old in g.GetComponents<HopPath>()) Object.DestroyImmediate(old);
                foreach (var old in g.GetComponents<Bob>()) Object.DestroyImmediate(old);
                g.transform.localPosition = pos;
                bool walking = false;
                if (walks.TryGetValue(id, out var wk))
                {
                    var hp = g.AddComponent<HopPath>();
                    hp.points = new List<Vector3>(wk.pts); hp.points[hp.points.Count - 1] = pos; hp.start = wk.start; hp.delay = wk.delay;
                    if (wk.pts.Count == 2 && pawns[seat][i] == -1) { hp.height = 0.9f; hp.step = 0.4f; hp.sound = false; }   // knocked home
                    if (Time.time - wk.start >= hp.Duration) { walks.Remove(id); Object.Destroy(hp); }
                    else walking = true;
                }
                prevProg[id] = pawns[seat][i];
                if (seat == you && movable != null && movable.Contains(i))
                {
                    movableNow.Add((g, i));
                    if (!walking) g.AddComponent<Bob>();
                    Mark(Art.Ring(0.26f, 0.36f), new Color(1f, 0.81f, 0.43f, 0.95f), new Vector3(pos.x, 0.31f, pos.z));
                    // where it would land
                    if (s.TryGetValue("die", out var dvv) && dvv is double dd0)
                    {
                        bool circling = s.Str("variant") == "master" && TableView.IntList(s.Arr("captures")).Count > seat && TableView.IntList(s.Arr("captures"))[seat] == 0;
                        int to = Dest(pawns[seat][i], (int)dd0, circling);
                        if (to <= HOME)
                        {
                            var dst = ProgPos(colors[seat], to, i);
                            Mark(Art.Disc(0.22f), new Color(Colors[colors[seat]].r, Colors[colors[seat]].g, Colors[colors[seat]].b, 0.55f), new Vector3(dst.x, 0.312f, dst.z));
                            Mark(Art.Ring(0.22f, 0.27f), new Color(1f, 1f, 1f, 0.8f), new Vector3(dst.x, 0.313f, dst.z));
                        }
                    }
                }
            }
        foreach (var key in new List<string>(pawnGo.Keys)) if (!seen.Contains(key)) { if (pawnGo[key].go != null) Object.Destroy(pawnGo[key].go); pawnGo.Remove(key); }
        die = null;
        bool showDie = s.TryGetValue("die", out var dv) && dv is double || (v.last != null && (v.last.t == "roll" || v.last.t == "pass"));
        if (showDie)
        {
            int val = dv is double dd ? (int)dd : v.last != null && v.last.dice.Count > 0 ? v.last.dice[0] : 1;
            var dsk = Skins.Worn("dice");
            var (lFace, lPip) = Skins.Dice(dsk);
            die = Art.Die(pieces, Mathf.Clamp(val, 1, 6), 0.62f, lFace, dsk == "dice_ivory" ? Art.Hex("#c8402f") : lPip);
            die.transform.localPosition = CellPos(7, 7) + Vector3.up * 0.32f;
            if (newMove && v.last.t == "roll") { die.AddComponent<Tumble>(); rolledAt = Time.time; }
        }
        else if (you >= 0 && turn == you)
        {
            // your throw: the die waits in the middle, glowing, for a tap
            var dsk = Skins.Worn("dice");
            var (lFace, lPip) = Skins.Dice(dsk);
            die = Art.Die(pieces, 6, 0.62f, lFace, dsk == "dice_ivory" ? Art.Hex("#c8402f") : lPip);
            die.transform.localPosition = CellPos(7, 7) + Vector3.up * 0.32f;
            die.AddComponent<Bob>();
            Mark(Art.Ring(0.5f, 0.62f), new Color(1f, 0.85f, 0.45f, 0.9f), CellPos(7, 7) + Vector3.up * 0.01f);
        }
        if (newMove) lastK = v.last.k;
    }
}
