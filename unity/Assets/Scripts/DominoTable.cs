// The domino game in 3D: the big table in the middle of the café, the players in their chairs around it,
// the line of play laid out like at a real table, opponents' tiles standing on edge facing them,
// the boneyard in a corner and a gold ring in front of whoever's turn it is.
using System.Collections.Generic;
using UnityEngine;

public class DominoTable : MonoBehaviour
{
    public const float TS = 0.62f;            // one layout unit in the world (a tile is 2 long)
    public const float T = 6f;                // half the side of the table
    const float SEAT_R = T + 1.5f;
    static readonly float XMax = (T - 1.9f) / TS;
    public static readonly string[] CharNames = { "AmFathi", "CaptainSami", "Ziko", "Mona", "Nada", "HajjMahmoud" };

    Cafe cafe;
    Vector3 C, F, R, U = Vector3.up;          // table centre, toward you, to your right, up
    GameObject table;
    Transform lineG, propsG, ghostG;
    GameObject ring;
    readonly Person[] chars = new Person[4];
    readonly int[] charCh = { -1, -1, -1, -1 };
    readonly GameObject[] chairs = new GameObject[4];
    public readonly int[] posToSeat = { -1, -1, -1, -1 };
    readonly HashSet<int> known = new HashSet<int>();
    readonly Dictionary<int, float> dropAt = new Dictionary<int, float>();
    readonly List<(float x, float z, float r, int y)> yard = new List<(float, float, float, int)>();
    TableView view;
    string viewKey = "", resultKey = "";
    public int sel = -1;
    public List<char> choose;

    public void Init(Cafe c)
    {
        cafe = c;
        C = Vector3.zero;
        F = c.Front; R = Vector3.Cross(U, -F);
        var prefab = Resources.Load<GameObject>("Art/MainTable");
        if (prefab != null) { table = Instantiate(prefab, C, Quaternion.identity); table.name = "MainTable"; table.SetActive(false); }
        // a lamp hanging over the game table
        if (table != null)
        {
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.type = LightType.Point; lamp.color = new Color(1f, 0.8f, 0.55f); lamp.intensity = 2.6f; lamp.range = 22f;
            lamp.shadows = LightShadows.None;
            lamp.transform.SetParent(table.transform, false);
            lamp.transform.position = C + Vector3.up * 9f + F * 1.5f;
        }
        lineG = new GameObject("Line").transform;
        propsG = new GameObject("Props").transform;
        ghostG = new GameObject("Ghosts").transform;
        ring = MakeRing();
        ring.SetActive(false);
    }

    public Vector3 SeatPoint(int pos) => pos == 0 ? C + F * SEAT_R : pos == 1 ? C - R * SEAT_R : pos == 2 ? C - F * SEAT_R : C + R * SEAT_R;
    static int[] PositionsFor(int n) => n == 2 ? new[] { 0, 2 } : n == 3 ? new[] { 0, 1, 3 } : new[] { 0, 1, 2, 3 };

    /// Where a point of the layout (tile units) is on the table.
    Vector3 At(float x, float y, float lift = 0) => C + R * (x * TS) + F * (y * TS) + U * (TileArt.H * TS / 2 + lift);
    Quaternion Along(float ang)
    {
        var dir = R * Mathf.Cos(ang) + F * Mathf.Sin(ang);
        return Quaternion.LookRotation(Vector3.Cross(dir, U), U);
    }

    public void Show(bool on)
    {
        if (table != null) table.SetActive(on);
        lineG.gameObject.SetActive(on); propsG.gameObject.SetActive(on); ghostG.gameObject.SetActive(on);
        if (!on)
        {
            ring.SetActive(false);
            for (int p = 0; p < 4; p++) SetChar(p, -1);
            view = null; viewKey = ""; known.Clear(); dropAt.Clear(); yard.Clear();
        }
    }

    public void Set(TableView v, int sel, List<char> choose)
    {
        bool newRound = view == null || view.round != v.round || view.id != v.id;
        if (newRound) { known.Clear(); dropAt.Clear(); yard.Clear(); }
        view = v; this.sel = sel; this.choose = choose;
        Seat();
        Rebuild();
    }

    void Seat()
    {
        var v = view;
        var chs = new[] { -1, -1, -1, -1 };
        for (int i = 0; i < 4; i++) posToSeat[i] = -1;
        int n = v.seats.Count, bas = v.you >= 0 ? v.you : 0;
        var list = PositionsFor(n);
        for (int k = 0; k < n && k < list.Length; k++)
        {
            int seat = (bas + k) % n, pos = list[k];
            posToSeat[pos] = seat;
            var s = v.seats[seat];
            // you are the camera: your own chair stays empty so nothing blocks your view
            if (s != null && !(pos == 0 && v.you >= 0)) chs[pos] = s.ch;
        }
        for (int p = 0; p < 4; p++) SetChar(p, chs[p]);
    }

    void SetChar(int pos, int ch)
    {
        if (charCh[pos] == ch) return;
        if (chars[pos] != null) { Destroy(chars[pos].gameObject); chars[pos] = null; }
        if (chairs[pos] != null) { Destroy(chairs[pos]); chairs[pos] = null; }
        charCh[pos] = ch;
        if (ch < 0 || ch >= CharNames.Length) return;
        var at = SeatPoint(pos);
        chairs[pos] = cafe.MakeChair(at, C);
        var p = Person.Make(CharNames[ch], at);
        if (p == null) return;
        p.reach = SEAT_R - T + 0.55f;
        p.Face(C);
        chars[pos] = p;
    }

    public Person CharAtSeat(int seat) { int pos = System.Array.IndexOf(posToSeat, seat); return pos >= 0 ? chars[pos] : null; }
    public Vector3 HeadOfSeat(int seat)
    {
        int pos = System.Array.IndexOf(posToSeat, seat);
        if (pos < 0) return C;
        var c = chars[pos];
        if (c != null && c.Head != null) return c.Head.position + U * 1.1f;
        return C + (SeatPoint(pos) - C) * 0.8f + U * 0.5f;
    }

    static void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject); }

    void Rebuild()
    {
        var v = view;
        Clear(lineG); Clear(propsG); Clear(ghostG);
        ring.SetActive(false);
        if (v == null || v.phase == "lobby") return;
        float now = Time.time;
        foreach (var pl in Domino.LayoutLine(v.line, XMax))
        {
            var m = TileArt.Make(pl.a, pl.b);
            m.transform.SetParent(lineG, false);
            m.transform.localScale = Vector3.one * TS;
            m.transform.SetPositionAndRotation(At(pl.x, pl.y), Along(pl.ang));
            if (!known.Contains(pl.id)) { known.Add(pl.id); if (v.last != null && v.last.t == "play" && v.last.id == pl.id) dropAt[pl.id] = now; }
            if (dropAt.TryGetValue(pl.id, out var d) && now - d < 0.6f) m.AddComponent<Drop>().from = U * 1.2f;
        }
        if (sel >= 0 && choose != null)
            foreach (var e in choose)
            {
                var g = Domino.Preview(v.line, sel, e, XMax);
                var m = new GameObject("Ghost " + e);
                m.transform.SetParent(ghostG, false);
                m.AddComponent<MeshFilter>().sharedMesh = TileArt.Body;
                m.AddComponent<MeshRenderer>().sharedMaterial = TileArt.GhostMat;
                m.transform.localScale = Vector3.one * TS * 1.06f;
                m.transform.SetPositionAndRotation(At(g.x, g.y, 0.02f), Along(g.ang));
                var col = m.AddComponent<BoxCollider>(); col.size = new Vector3(TileArt.W, TileArt.H * 3, TileArt.D);
                m.AddComponent<GhostEnd>().end = e;
            }
        // opponents stand their tiles on edge, facing themselves
        for (int pos = 1; pos < 4; pos++)
        {
            int seat = posToSeat[pos];
            if (seat < 0 || seat >= v.seats.Count || v.seats[seat] == null) continue;
            int n = v.seats[seat].count;
            var toP = (SeatPoint(pos) - C).normalized;
            var perp = Vector3.Cross(U, toP);
            var bas = C + toP * (T - 0.75f);
            for (int i = 0; i < n; i++)
            {
                float off = (i - (n - 1) / 2f) * TS * 1.12f;
                var t = TileArt.Make(0, 0, false);
                t.transform.SetParent(propsG, false);
                t.transform.localScale = Vector3.one * TS;
                t.transform.position = bas + perp * off + U * (TileArt.W * TS / 2);
                var face = Quaternion.AngleAxis((i - (n - 1) / 2f) * 1.2f, U) * toP;
                t.transform.rotation = Quaternion.LookRotation(Vector3.Cross(U, face), face);
            }
        }
        // the boneyard: face down in a loose pile at the corner
        while (yard.Count < v.yard)
        {
            int k = yard.Count;
            yard.Add((T - 1.4f + (Random.value - 0.5f) * 1.4f, -(T - 1.8f) + (Random.value - 0.5f) * 2.4f, Random.value * 180f, k > 7 ? 1 : 0));
        }
        for (int i = 0; i < v.yard; i++)
        {
            var p = yard[i];
            var t = TileArt.Make(0, 0, false);
            t.transform.SetParent(propsG, false);
            t.transform.localScale = Vector3.one * TS;
            t.transform.position = C + R * p.x + F * p.z + U * (TileArt.H * TS / 2 + p.y * TileArt.H * TS);
            t.transform.rotation = Quaternion.AngleAxis(p.r, U);
        }
        if (v.phase == "play" && v.turn >= 0)
        {
            int pos = System.Array.IndexOf(posToSeat, v.turn);
            if (pos >= 0)
            {
                var dir = (SeatPoint(pos) - C).normalized;
                ring.transform.position = C + dir * (T - 0.6f) + U * 0.02f;
                ring.SetActive(true);
            }
        }
        // the end of a round: the winners cheer, the others take it badly
        string rk = v.phase == "rend" || v.phase == "mend" ? v.id + ":" + v.round + ":" + v.phase : "";
        if (rk != "" && rk != resultKey)
        {
            resultKey = rk;
            int wt = v.phase == "mend" && v.match != null ? v.match.team : v.result != null ? v.result.team : -1;
            for (int seat = 0; seat < v.seats.Count; seat++)
            {
                var who = CharAtSeat(seat);
                if (who == null || wt < 0) continue;
                if (v.TeamOf(seat) == wt) who.Win(); else who.Lose();
            }
        }
        // someone just played: their hand goes out to the tile
        if (v.last != null && v.last.t == "play" && v.last.k.ToString() != viewKey)
        {
            viewKey = v.last.k.ToString();
            var who = CharAtSeat(v.last.s);
            foreach (var pl in Domino.LayoutLine(v.line, XMax)) if (pl.id == v.last.id && who != null) who.Gesture(At(pl.x, pl.y));
        }
    }

    GameObject MakeRing()
    {
        var go = new GameObject("TurnRing");
        var mesh = new Mesh();
        const int seg = 48; float r0 = 0.75f, r1 = 1.05f;
        var vs = new List<Vector3>(); var tr = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2 / seg;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            vs.Add(d * r0); vs.Add(d * r1);
            if (i < seg) { int b = i * 2; tr.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 }); }
        }
        mesh.SetVertices(vs); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals();
        // face up whichever way the triangles turned
        if (mesh.normals[0].y < 0) { tr.Reverse(); mesh.SetTriangles(tr, 0); mesh.RecalculateNormals(); }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mat = TileArt.Make(TileArt.Clear, new Color(1f, 0.81f, 0.43f, 0.75f));
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    void Update()
    {
        if (ring.activeSelf) { var c = ring.GetComponent<MeshRenderer>().sharedMaterial; c.color = new Color(1f, 0.81f, 0.43f, 0.45f + Mathf.Sin(Time.time * 3f) * 0.25f); }
    }
}

/// A tile that was just played drops onto the table.
public class Drop : MonoBehaviour
{
    public Vector3 from;
    Vector3 to; float t;
    void Start() { to = transform.position; transform.position = to + from; }
    void Update()
    {
        t += Time.deltaTime / 0.35f;
        float k = Mathf.Clamp01(t); k = 1 - (1 - k) * (1 - k);
        transform.position = Vector3.Lerp(to + from, to, k);
        if (t >= 1) Destroy(this);
    }
}

/// A glowing spot where the chosen tile can go (tap it to play there).
public class GhostEnd : MonoBehaviour { public char end; }
