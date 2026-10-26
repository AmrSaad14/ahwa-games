// A Cairo living room (الصالون) for the "engagement visit" advert: gilded sofa and armchairs in maroon brocade
// under clear plastic covers, a glass coffee table with tea and basbousa, a crystal chandelier, heavy curtains,
// a china cabinet (النيش), the parents' wedding photo, a framed verse and a wall clock. Built in code, far from
// the café (x = 400), on the same floor height, at the people's scale (1 metre = 5 units).
using System.Collections.Generic;
using UnityEngine;

public class Salon : MonoBehaviour
{
    public const float M = Person.Scale;                       // units per metre
    public static readonly Vector3 Origin = new Vector3(400, Person.FloorY, 0);
    public const float Width = 6.4f, Depth = 5.2f, Height = 3.0f; // metres

    // where the four sit (world positions, facing given) and the props the film moves
    public Vector3 seatFather, seatGroom, seatMother, seatBride;
    public Vector3 faceFather, faceGroom, faceMother, faceBride;
    public Vector3 tableTop;                                    // middle of the coffee table's glass, world
    public Transform tawlaBox, clockHour, clockMinute;
    public Light chandelierLight, lampLight, moonLight, fillLight;
    readonly List<Renderer> bulbs = new List<Renderer>();
    Material bulbOn, bulbOff, windowDusk, windowNight;
    Renderer windowGlass;

    Transform root;
    Vector3 W(float x, float y, float z) => root.TransformPoint(new Vector3(x, y, z) * M);

    // ---------- materials ----------
    static Material Unlit(Color c, Texture t = null)
    {
        var m = new Material(Shader.Find("Sprites/Default")) { color = c };
        if (t != null) m.mainTexture = t;
        return m;
    }
    static Texture2D Tile(Texture2D t) { t.wrapMode = TextureWrapMode.Repeat; return t; }
    static float N(float x, float y) => Mathf.PerlinNoise(x, y);

    Material gold, velvet, plastic, glass, wallpaper, parquet, rug, cream, wood, darkWood, curtain, white, linen, brass;

    void Materials()
    {
        gold = Art.Mat(new Color(0.86f, 0.66f, 0.3f), 0.72f, 0.85f);
        brass = Art.Mat(new Color(0.72f, 0.52f, 0.22f), 0.6f, 0.8f);
        // maroon brocade: velvet with a small gold diamond, a little sheen
        velvet = Art.Mat(Color.white, 0.32f, 0f, Tile(Art.Paint(128, 128, (x, y) =>
        {
            float dx = Mathf.Abs((x % 32) - 16) / 16f, dy = Mathf.Abs((y % 32) - 16) / 16f;
            float d = dx + dy; bool line = Mathf.Abs(d - 0.7f) < 0.07f; bool dot = d < 0.15f;
            var b = new Color(0.45f, 0.06f, 0.1f) * (0.86f + N(x * 0.3f, y * 0.3f) * 0.22f);
            return line || dot ? Color.Lerp(b, new Color(0.78f, 0.56f, 0.24f), 0.55f) : b;
        })));
        velvet.mainTextureScale = new Vector2(3, 3);
        plastic = new Material(TileArt.Clear) { color = new Color(1f, 1f, 1f, 0.09f) };
        plastic.renderQueue = 3050;
        glass = new Material(TileArt.Clear) { color = new Color(0.78f, 0.9f, 0.92f, 0.28f) };
        glass.renderQueue = 3040;
        // cream wallpaper with a soft damask
        wallpaper = Art.Mat(Color.white, 0.12f, 0f, Tile(Art.Paint(256, 256, (x, y) =>
        {
            float u = (x % 64) / 64f - 0.5f, v = (y % 96) / 96f - 0.5f;
            float r = Mathf.Sqrt(u * u * 2.2f + v * v);
            float motif = Mathf.Abs(Mathf.Sin(r * 18f + Mathf.Atan2(v, u) * 3f)) < 0.25f && r < 0.42f ? 1 : 0;
            var b = new Color(0.93f, 0.86f, 0.72f) * (0.96f + N(x * 0.05f, y * 0.05f) * 0.06f);
            return Color.Lerp(b, new Color(0.84f, 0.74f, 0.56f), motif * 0.55f);
        })));
        // herringbone parquet
        parquet = Art.Mat(Color.white, 0.45f, 0f, Tile(Art.Paint(512, 512, (x, y) =>
        {
            int bw = 32, bl = 128;
            int k = (x / bw + y / bw) % 2; int along = k == 0 ? (y + (x / bw) * bw) % bl : (x + (y / bw) * bw) % bl;
            int plank = k == 0 ? (y + (x / bw) * bw) / bl * 7 + x / bw : (x + (y / bw) * bw) / bl * 5 + y / bw;
            float shade = 0.82f + (plank * 37 % 11) / 11f * 0.22f;
            bool gap = along < 2 || (k == 0 ? x % bw < 1 : y % bw < 1);
            var c = Art.Wood(k == 0 ? y : x, k == 0 ? x : y, new Color(0.55f, 0.33f, 0.17f), new Color(0.34f, 0.19f, 0.09f), new Color(0.7f, 0.47f, 0.27f), 30, plank * 0.37f) * shade;
            return gap ? c * 0.55f : c;
        })));
        parquet.mainTextureScale = new Vector2(Width / 1.6f, Depth / 1.6f);
        // a red Persian-style rug: border bands, a field of small motifs, a medallion
        rug = Art.Mat(Color.white, 0.1f, 0f, Art.Paint(512, 360, (x, y) =>
        {
            float u = x / 511f, v = y / 359f;
            float edge = Mathf.Min(Mathf.Min(u, 1 - u) * 1.42f, Mathf.Min(v, 1 - v));
            var red = new Color(0.55f, 0.08f, 0.08f); var navy = new Color(0.1f, 0.12f, 0.28f); var ivory = new Color(0.9f, 0.82f, 0.64f);
            Color c;
            if (edge < 0.02f) c = ivory * 0.85f;
            else if (edge < 0.09f) c = (Mathf.Sin(u * 80) * Mathf.Sin(v * 80) > 0.2f) ? ivory * 0.8f : navy;
            else if (edge < 0.105f) c = ivory * 0.8f;
            else
            {
                float du = (u - 0.5f) * 1.42f, dv = v - 0.5f; float r = Mathf.Abs(du) + Mathf.Abs(dv);
                if (r < 0.2f) c = r < 0.08f ? ivory * 0.85f : (Mathf.Sin(r * 90) > 0 ? navy : new Color(0.75f, 0.5f, 0.2f));
                else c = (Mathf.Sin(u * 60) * Mathf.Sin(v * 44) > 0.6f) ? navy * 1.2f : red;
            }
            return c * (0.9f + N(x * 0.2f, y * 0.2f) * 0.15f);
        }));
        cream = Art.Mat(new Color(0.95f, 0.91f, 0.82f), 0.3f);
        white = Art.Mat(new Color(0.97f, 0.96f, 0.93f), 0.55f);
        linen = Art.Mat(new Color(0.93f, 0.9f, 0.84f), 0.15f);
        wood = Art.Mat(Color.white, 0.55f, 0f, Art.WoodTex(256, 128, new Color(0.45f, 0.24f, 0.11f), new Color(0.26f, 0.12f, 0.05f), new Color(0.6f, 0.36f, 0.18f)));
        darkWood = Art.Mat(Color.white, 0.6f, 0f, Art.WoodTex(256, 128, new Color(0.24f, 0.12f, 0.06f), new Color(0.13f, 0.06f, 0.03f), new Color(0.36f, 0.2f, 0.1f)));
        curtain = Art.Mat(Color.white, 0.3f, 0f, Tile(Art.Paint(64, 256, (x, y) => new Color(0.42f, 0.05f, 0.09f) * (0.85f + N(x * 0.2f, y * 0.02f) * 0.25f))));
        bulbOn = Unlit(new Color(1f, 0.9f, 0.68f)); bulbOff = Unlit(new Color(0.35f, 0.32f, 0.28f));
        windowDusk = Unlit(Color.white, Art.Paint(32, 64, (x, y) => Color.Lerp(new Color(0.95f, 0.55f, 0.32f), new Color(0.2f, 0.25f, 0.5f), y / 63f)));
        windowNight = Unlit(Color.white, Art.Paint(32, 64, (x, y) => Color.Lerp(new Color(0.06f, 0.08f, 0.2f), new Color(0.02f, 0.03f, 0.08f), y / 63f)));
    }

    // ---------- shapes ----------
    static readonly Dictionary<string, Mesh> rounded = new Dictionary<string, Mesh>();
    /// A box with rounded edges and corners (cushions, frames), size in units, radius r.
    public static Mesh RoundedBox(Vector3 size, float r, int seg = 3)
    {
        string key = $"{size.x:0.###},{size.y:0.###},{size.z:0.###},{r:0.###},{seg}";
        if (rounded.TryGetValue(key, out var cached)) return cached;
        var h = size / 2;
        r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
        float[] Axis(float hh)
        {
            var l = new List<float>();
            for (int j = 0; j <= seg; j++) { float a = (1 - j / (float)seg) * Mathf.PI / 2; l.Add(-(hh - r) - r * Mathf.Sin(a)); }
            for (int j = seg; j >= 0; j--) { float a = (1 - j / (float)seg) * Mathf.PI / 2; l.Add((hh - r) + r * Mathf.Sin(a)); }
            return l.ToArray();
        }
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        var inner = new Vector3(h.x - r, h.y - r, h.z - r);
        for (int n = 0; n < 3; n++)
            for (int sg = -1; sg <= 1; sg += 2)
            {
                int u = (n + 1) % 3, v = (n + 2) % 3;
                var U = Axis(h[u]); var V = Axis(h[v]);
                int b0 = vs.Count;
                for (int i = 0; i < U.Length; i++)
                    for (int j = 0; j < V.Length; j++)
                    {
                        var p = Vector3.zero; p[n] = sg * h[n]; p[u] = U[i]; p[v] = V[j];
                        var c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var d = p - c; var dn = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero;
                        if (dn == Vector3.zero) dn[n] = sg;
                        vs.Add(c + dn * r); ns.Add(dn);
                        uv.Add(new Vector2((U[i] + h[u]) / (2 * h[u]), (V[j] + h[v]) / (2 * h[v])));
                    }
                for (int i = 0; i < U.Length - 1; i++)
                    for (int j = 0; j < V.Length - 1; j++)
                    {
                        int a = b0 + i * V.Length + j, b = a + V.Length, c = a + 1, d = b + 1;
                        if (sg > 0) { tr.Add(a); tr.Add(c); tr.Add(b); tr.Add(b); tr.Add(c); tr.Add(d); }
                        else { tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(b); tr.Add(d); tr.Add(c); }
                    }
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateBounds();
        // make sure the faces point out (the winding above depends on the axis order)
        var t = m.triangles; var vv = m.vertices; int outN = 0, inN = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            var nn = Vector3.Cross(vv[t[i + 1]] - vv[t[i]], vv[t[i + 2]] - vv[t[i]]);
            if (Vector3.Dot(nn, (vv[t[i]] + vv[t[i + 1]] + vv[t[i + 2]])) >= 0) outN++; else inN++;
        }
        if (inN > outN) { for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; } m.triangles = t; }
        rounded[key] = m;
        return m;
    }

    /// A box in metres: size, centre (local to the room), rounded by r metres (0: sharp).
    GameObject B(string name, Transform parent, Vector3 size, Vector3 center, Material mat, float r = 0, bool shadows = true)
    {
        GameObject g;
        if (r > 0) { g = Art.Obj(name, parent, RoundedBox(size * M, r * M), mat, shadows); }
        else { g = Art.Obj(name, parent, Art.Prim(PrimitiveType.Cube), mat, shadows); g.transform.localScale = size * M; }
        g.transform.localPosition = center * M;
        return g;
    }
    GameObject Lathe(string name, Transform parent, Vector2[] profM, Vector3 at, Material mat, int seg = 32)
    {
        var p = new Vector2[profM.Length]; for (int i = 0; i < p.Length; i++) p[i] = profM[i] * M;
        var g = Art.Obj(name, parent, Art.Lathe(p, seg), mat);
        g.transform.localPosition = at * M;
        return g;
    }
    Transform Group(string name, Vector3 at, float yawDeg = 0)
    {
        var g = new GameObject(name).transform; g.SetParent(root, false);
        g.localPosition = at * M; g.localRotation = Quaternion.Euler(0, yawDeg, 0);
        return g;
    }

    // ---------- the room ----------
    public void Build()
    {
        root = new GameObject("Salon").transform;
        root.position = Origin;
        Materials();
        float hw = Width / 2, hd = Depth / 2;
        // floor, rug, walls, ceiling, skirting and crown moulding
        B("Floor", root, new Vector3(Width, 0.04f, Depth), new Vector3(0, -0.02f, 0), parquet);
        B("Rug", root, new Vector3(3.0f, 0.012f, 2.1f), new Vector3(0, 0.006f, -0.9f), rug, 0, false);
        B("BackWall", root, new Vector3(Width, Height, 0.1f), new Vector3(0, Height / 2, -hd - 0.05f), wallpaper).GetComponent<Renderer>().material.mainTextureScale = new Vector2(Width / 0.7f, Height / 0.95f);
        B("FrontWall", root, new Vector3(Width, Height, 0.1f), new Vector3(0, Height / 2, hd + 0.05f), wallpaper).GetComponent<Renderer>().material.mainTextureScale = new Vector2(Width / 0.7f, Height / 0.95f);
        B("LeftWall", root, new Vector3(0.1f, Height, Depth), new Vector3(-hw - 0.05f, Height / 2, 0), wallpaper).GetComponent<Renderer>().material.mainTextureScale = new Vector2(Depth / 0.7f, Height / 0.95f);
        B("RightWall", root, new Vector3(0.1f, Height, Depth), new Vector3(hw + 0.05f, Height / 2, 0), wallpaper).GetComponent<Renderer>().material.mainTextureScale = new Vector2(Depth / 0.7f, Height / 0.95f);
        B("Ceiling", root, new Vector3(Width, 0.06f, Depth), new Vector3(0, Height + 0.03f, 0), cream, 0, false);
        foreach (var (sx, sz, len, rot) in new[] { (0f, -hd + 0.01f, Width, 0), (0f, hd - 0.01f, Width, 0), (-hw + 0.01f, 0f, Depth, 90), (hw - 0.01f, 0f, Depth, 90) })
        {
            var sk = B("Skirting", root, new Vector3(len, 0.12f, 0.02f), new Vector3(sx, 0.06f, sz), cream); sk.transform.localRotation = Quaternion.Euler(0, rot, 0);
            var cr = B("Crown", root, new Vector3(len, 0.1f, 0.06f), new Vector3(sx, Height - 0.05f, sz), cream); cr.transform.localRotation = Quaternion.Euler(0, rot, 0);
            var cg = B("CrownGold", root, new Vector3(len, 0.015f, 0.07f), new Vector3(sx, Height - 0.105f, sz), gold); cg.transform.localRotation = Quaternion.Euler(0, rot, 0);
        }
        Lathe("Rosette", root, new[] { new Vector2(0.001f, 0), new Vector2(0.35f, 0), new Vector2(0.3f, -0.03f), new Vector2(0.18f, -0.05f), new Vector2(0.001f, -0.06f) }, new Vector3(0, Height, -0.85f), cream);

        Sofa(new Vector3(0, 0, -1.98f));
        var mo = new Vector3(1.95f, 0, -0.85f); Armchair("MotherChair", mo, -108);
        var br = new Vector3(-1.95f, 0, -0.85f); Armchair("BrideChair", br, 108);
        CoffeeTable(new Vector3(0, 0, -0.85f));
        Chandelier(new Vector3(0, Height, -0.85f));
        Cabinet(new Vector3(-2.35f, 0, -hd + 0.24f));
        WindowAndCurtains(new Vector3(2.15f, 0, -hd));
        SideTableLamp(new Vector3(2.75f, 0, -1.75f));
        Frames();
        Clock(new Vector3(0, 2.2f, -hd + 0.03f));

        // where they sit
        seatFather = W(-0.5f, 0, -1.86f); faceFather = W(-0.5f, 0, 2f);
        seatGroom = W(0.5f, 0, -1.86f); faceGroom = W(0.5f, 0, 2f);
        seatMother = W(mo.x, 0, mo.z); faceMother = W(-0.6f, 0, 0.6f);
        seatBride = W(br.x, 0, br.z); faceBride = W(0.6f, 0, 0.6f);
        tableTop = W(0, 0.452f, -0.85f);
        Lights();
        SetClock(20f + 10f / 60);
        Debug.Log("[salon] built");
    }

    void Sofa(Vector3 at)
    {
        var g = Group("Sofa", at);
        float w = 2.3f, d = 0.88f;
        // the gilded frame: a base rail, a carved top rail on the back, arm fronts, short turned legs
        B("Base", g, new Vector3(w, 0.16f, d), new Vector3(0, 0.2f, 0), gold, 0.03f);
        B("BaseFront", g, new Vector3(w - 0.1f, 0.12f, 0.03f), new Vector3(0, 0.2f, d / 2), brass, 0.012f);
        B("Seat", g, new Vector3(w - 0.3f, 0.17f, d - 0.12f), new Vector3(0, 0.36f, 0.04f), velvet, 0.07f);
        B("SeatPlastic", g, new Vector3(w - 0.27f, 0.19f, d - 0.09f), new Vector3(0, 0.365f, 0.045f), plastic, 0.08f, false);
        B("Back", g, new Vector3(w - 0.2f, 0.62f, 0.16f), new Vector3(0, 0.72f, -d / 2 + 0.1f), velvet, 0.07f).transform.localRotation = Quaternion.Euler(-8, 0, 0);
        B("BackPlastic", g, new Vector3(w - 0.17f, 0.64f, 0.19f), new Vector3(0, 0.72f, -d / 2 + 0.1f), plastic, 0.08f, false).transform.localRotation = Quaternion.Euler(-8, 0, 0);
        B("TopRail", g, new Vector3(w - 0.1f, 0.07f, 0.09f), new Vector3(0, 1.05f, -d / 2 + 0.06f), gold, 0.03f);
        Lathe("Crest", g, new[] { new Vector2(0.001f, 0), new Vector2(0.09f, 0.02f), new Vector2(0.06f, 0.1f), new Vector2(0.001f, 0.14f) }, new Vector3(0, 1.07f, -d / 2 + 0.06f), gold, 16);
        foreach (var s in new[] { -1, 1 })
        {
            B("Arm", g, new Vector3(0.15f, 0.3f, d - 0.1f), new Vector3(s * (w / 2 - 0.08f), 0.45f, 0.02f), velvet, 0.06f);
            B("ArmGold", g, new Vector3(0.17f, 0.05f, d - 0.06f), new Vector3(s * (w / 2 - 0.08f), 0.62f, 0.02f), gold, 0.025f);
            B("ArmFront", g, new Vector3(0.12f, 0.4f, 0.06f), new Vector3(s * (w / 2 - 0.08f), 0.42f, d / 2 - 0.02f), gold, 0.03f);
            foreach (var z in new[] { -1, 1 })
                Lathe("Leg", g, new[] { new Vector2(0.001f, 0), new Vector2(0.025f, 0), new Vector2(0.04f, 0.06f), new Vector2(0.03f, 0.12f), new Vector2(0.001f, 0.13f) }, new Vector3(s * (w / 2 - 0.1f), 0, z * (d / 2 - 0.08f)), gold, 16);
        }
        // two cushions with gold tassel trim at the ends
        foreach (var s in new[] { -1, 1 })
        {
            var c = B("Cushion", g, new Vector3(0.42f, 0.4f, 0.13f), new Vector3(s * (w / 2 - 0.42f), 0.66f, -d / 2 + 0.24f), velvet, 0.12f);
            c.transform.localRotation = Quaternion.Euler(-14, s * 18, s * 6);
        }
    }

    void Armchair(string name, Vector3 at, float yaw)
    {
        var g = Group(name, at, yaw);
        float w = 0.9f, d = 0.82f;
        B("Base", g, new Vector3(w, 0.15f, d), new Vector3(0, 0.2f, 0), gold, 0.03f);
        B("Seat", g, new Vector3(w - 0.24f, 0.17f, d - 0.14f), new Vector3(0, 0.36f, 0.04f), velvet, 0.07f);
        B("SeatPlastic", g, new Vector3(w - 0.21f, 0.19f, d - 0.11f), new Vector3(0, 0.365f, 0.045f), plastic, 0.08f, false);
        B("Back", g, new Vector3(w - 0.16f, 0.7f, 0.15f), new Vector3(0, 0.76f, -d / 2 + 0.09f), velvet, 0.08f).transform.localRotation = Quaternion.Euler(-8, 0, 0);
        B("BackFrame", g, new Vector3(w - 0.06f, 0.07f, 0.08f), new Vector3(0, 1.13f, -d / 2 + 0.05f), gold, 0.03f);
        Lathe("Crest", g, new[] { new Vector2(0.001f, 0), new Vector2(0.07f, 0.02f), new Vector2(0.05f, 0.08f), new Vector2(0.001f, 0.12f) }, new Vector3(0, 1.15f, -d / 2 + 0.05f), gold, 16);
        foreach (var s in new[] { -1, 1 })
        {
            B("Arm", g, new Vector3(0.12f, 0.28f, d - 0.12f), new Vector3(s * (w / 2 - 0.06f), 0.45f, 0.02f), velvet, 0.05f);
            B("ArmGold", g, new Vector3(0.14f, 0.045f, d - 0.08f), new Vector3(s * (w / 2 - 0.06f), 0.61f, 0.02f), gold, 0.02f);
            foreach (var z in new[] { -1, 1 })
                Lathe("Leg", g, new[] { new Vector2(0.001f, 0), new Vector2(0.022f, 0), new Vector2(0.035f, 0.06f), new Vector2(0.026f, 0.12f), new Vector2(0.001f, 0.13f) }, new Vector3(s * (w / 2 - 0.08f), 0, z * (d / 2 - 0.08f)), gold, 16);
        }
    }

    void CoffeeTable(Vector3 at)
    {
        var g = Group("CoffeeTable", at);
        float w = 1.15f, d = 0.62f, top = 0.45f;
        B("Glass", g, new Vector3(w, 0.014f, d), new Vector3(0, top - 0.007f, 0), glass, 0.01f, false);
        B("RimF", g, new Vector3(w + 0.03f, 0.03f, 0.025f), new Vector3(0, top - 0.02f, d / 2), gold, 0.01f);
        B("RimB", g, new Vector3(w + 0.03f, 0.03f, 0.025f), new Vector3(0, top - 0.02f, -d / 2), gold, 0.01f);
        B("RimL", g, new Vector3(0.025f, 0.03f, d), new Vector3(-w / 2, top - 0.02f, 0), gold, 0.01f);
        B("RimR", g, new Vector3(0.025f, 0.03f, d), new Vector3(w / 2, top - 0.02f, 0), gold, 0.01f);
        B("Shelf", g, new Vector3(w - 0.1f, 0.012f, d - 0.1f), new Vector3(0, 0.1f, 0), glass, 0.005f, false);
        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
            Lathe("Leg", g, new[] { new Vector2(0.001f, 0), new Vector2(0.03f, 0), new Vector2(0.018f, 0.06f), new Vector2(0.014f, 0.3f), new Vector2(0.022f, top - 0.03f), new Vector2(0.001f, top - 0.03f) }, new Vector3(sx * (w / 2 - 0.04f), 0, sz * (d / 2 - 0.04f)), gold, 14);
        // a lace runner, tea for four, basbousa on a plate
        B("Runner", g, new Vector3(0.7f, 0.003f, 0.26f), new Vector3(0.05f, top + 0.0015f, 0.02f), linen, 0, false);
        foreach (var (x, z) in new[] { (-0.42f, 0.18f), (0.4f, 0.17f), (0.46f, -0.12f), (-0.36f, -0.16f) })
            Look.Drink("drink_tea", g, W(at.x + x, top, at.z + z), 1.25f);
        var plate = Art.Obj("Plate", g, Art.Lathe(new[] { new Vector2(0.001f, 0) * M, new Vector2(0.1f, 0) * M, new Vector2(0.14f, 0.015f) * M, new Vector2(0.13f, 0.018f) * M, new Vector2(0.001f, 0.006f) * M }, 32), white);
        plate.transform.localPosition = new Vector3(0.12f, top, 0.05f) * M;
        var cake = Art.Mat(new Color(0.85f, 0.55f, 0.22f), 0.55f);
        for (int i = 0; i < 6; i++)
        {
            var c = B("Basbousa", g, new Vector3(0.05f, 0.03f, 0.05f), new Vector3(0.12f + (i % 3 - 1) * 0.055f, top + 0.024f, 0.05f + (i / 3 - 0.5f) * 0.055f), cake, 0.006f);
            c.transform.localRotation = Quaternion.Euler(0, 45, 0);
        }
        // the tawla: a closed wooden case with an inlaid border, off the table until it is slammed down
        var tb = new GameObject("TawlaBox").transform; tb.SetParent(root, false);
        var inlay = Art.Mat(Color.white, 0.6f, 0f, Art.Paint(256, 160, (x, y) =>
        {
            float e = Mathf.Min(Mathf.Min(x, 255 - x), Mathf.Min(y, 159 - y));
            var c = Art.Wood(x, y, new Color(0.42f, 0.2f, 0.08f), new Color(0.24f, 0.1f, 0.04f), new Color(0.58f, 0.32f, 0.15f));
            if (e > 10 && e < 22) c = ((x / 6 + y / 6) % 2 == 0) ? new Color(0.92f, 0.88f, 0.8f) : new Color(0.3f, 0.15f, 0.06f);
            float cx = (x - 128) / 40f, cy = (y - 80) / 40f; if (Mathf.Abs(cx) + Mathf.Abs(cy) < 0.6f && Mathf.Abs(cx) + Mathf.Abs(cy) > 0.45f) c = new Color(0.9f, 0.86f, 0.78f);
            return c;
        }));
        var box = Art.Obj("Case", tb, RoundedBox(new Vector3(0.5f, 0.075f, 0.3f) * M, 0.012f * M), inlay);
        box.transform.localPosition = Vector3.up * 0.0375f * M;
        B("Hinge", tb, new Vector3(0.5f, 0.006f, 0.006f), new Vector3(0, 0.0375f, -0.152f), brass, 0.002f);
        tawlaBox = tb;
        tawlaBox.gameObject.SetActive(false);
    }

    void Chandelier(Vector3 at)
    {
        var g = Group("Chandelier", at);
        B("Chain", g, new Vector3(0.015f, 0.42f, 0.015f), new Vector3(0, -0.21f, 0), gold);
        Lathe("Body", g, new[] { new Vector2(0.001f, -0.42f), new Vector2(0.07f, -0.44f), new Vector2(0.11f, -0.55f), new Vector2(0.05f, -0.7f), new Vector2(0.09f, -0.78f), new Vector2(0.001f, -0.86f) }, Vector3.zero, gold, 24);
        var crystal = new Material(TileArt.Clear) { color = new Color(1f, 0.97f, 0.9f, 0.55f) };
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2 / 8; var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var arm = B("Arm", g, new Vector3(0.36f, 0.015f, 0.015f), dir * 0.2f + Vector3.up * -0.66f, gold);
            arm.transform.localRotation = Quaternion.FromToRotation(Vector3.right, dir + Vector3.up * 0.25f);
            Lathe("Cup", g, new[] { new Vector2(0.001f, 0), new Vector2(0.035f, 0.02f), new Vector2(0.025f, 0.03f), new Vector2(0.001f, 0.03f) }, dir * 0.38f + Vector3.up * -0.58f, gold, 12);
            var bulb = Art.Obj("Bulb", g, Art.Prim(PrimitiveType.Sphere), bulbOn, false);
            bulb.transform.localPosition = (dir * 0.38f + Vector3.up * -0.52f) * M; bulb.transform.localScale = new Vector3(0.035f, 0.06f, 0.035f) * M;
            bulbs.Add(bulb.GetComponent<Renderer>());
            for (int k = 0; k < 3; k++)
            {
                var drop = Art.Obj("Crystal", g, Art.Prim(PrimitiveType.Sphere), crystal, false);
                drop.transform.localPosition = (dir * (0.3f - k * 0.05f) + Vector3.up * (-0.72f - k * 0.04f)) * M;
                drop.transform.localScale = new Vector3(0.02f, 0.04f, 0.02f) * M;
            }
        }
    }

    void Cabinet(Vector3 at)
    {
        // النيش: a gilded glass cabinet of the best china, against the back wall
        var g = Group("Cabinet", at);
        float w = 1.05f, h = 1.95f, d = 0.42f;
        B("Body", g, new Vector3(w, h, d), new Vector3(0, h / 2, 0), darkWood, 0.02f);
        B("Inside", g, new Vector3(w - 0.12f, h - 0.5f, 0.02f), new Vector3(0, h / 2 + 0.12f, d / 2 - 0.02f), Art.Mat(new Color(0.5f, 0.1f, 0.12f), 0.2f));
        B("Top", g, new Vector3(w + 0.08f, 0.08f, d + 0.06f), new Vector3(0, h + 0.04f, 0), gold, 0.02f);
        Lathe("Crest", g, new[] { new Vector2(0.001f, 0), new Vector2(0.14f, 0.02f), new Vector2(0.1f, 0.12f), new Vector2(0.001f, 0.2f) }, new Vector3(0, h + 0.08f, 0.05f), gold, 18);
        for (int s = 0; s < 3; s++)
        {
            float y = 0.55f + s * 0.45f;
            B("Shelf", g, new Vector3(w - 0.12f, 0.015f, d - 0.06f), new Vector3(0, y, 0.02f), glass, 0, false);
            for (int i = 0; i < 4; i++)
            {
                var pl = Art.Obj("China", g, Art.Lathe(new[] { new Vector2(0.001f, 0) * M, new Vector2(0.09f, 0.0f) * M, new Vector2(0.1f, 0.012f) * M, new Vector2(0.001f, 0.006f) * M }, 24), white);
                pl.transform.localPosition = new Vector3(-0.33f + i * 0.22f, y + 0.1f, -0.1f) * M;
                pl.transform.localRotation = Quaternion.Euler(-75, 0, 0);
                var cup = Art.Obj("Cup", g, Art.Lathe(new[] { new Vector2(0.001f, 0) * M, new Vector2(0.025f, 0) * M, new Vector2(0.035f, 0.05f) * M, new Vector2(0.032f, 0.05f) * M, new Vector2(0.001f, 0.01f) * M }, 16), gold);
                cup.transform.localPosition = new Vector3(-0.33f + i * 0.22f, y + 0.008f, 0.06f) * M;
            }
        }
        B("Glass", g, new Vector3(w - 0.08f, h - 0.45f, 0.012f), new Vector3(0, h / 2 + 0.12f, d / 2 + 0.006f), glass, 0, false);
        B("Mullion", g, new Vector3(0.03f, h - 0.45f, 0.02f), new Vector3(0, h / 2 + 0.12f, d / 2 + 0.01f), gold, 0.008f);
        B("Drawer", g, new Vector3(w - 0.1f, 0.3f, 0.02f), new Vector3(0, 0.22f, d / 2 + 0.01f), darkWood, 0.01f);
        foreach (var s in new[] { -1, 1 }) Lathe("Knob", g, new[] { new Vector2(0.001f, 0), new Vector2(0.015f, 0.01f), new Vector2(0.001f, 0.025f) }, new Vector3(s * 0.22f, 0.22f, d / 2 + 0.02f), brass, 10).transform.localRotation = Quaternion.Euler(90, 0, 0);
    }

    void WindowAndCurtains(Vector3 at)
    {
        var g = Group("Window", at);
        var win = B("Sky", g, new Vector3(1.1f, 1.5f, 0.01f), new Vector3(0, 1.55f, 0.02f), windowDusk, 0, false);
        windowGlass = win.GetComponent<Renderer>();
        B("Frame", g, new Vector3(1.2f, 0.06f, 0.06f), new Vector3(0, 0.78f, 0.03f), cream, 0.01f);
        B("FrameT", g, new Vector3(1.2f, 0.06f, 0.06f), new Vector3(0, 2.32f, 0.03f), cream, 0.01f);
        B("Mullion", g, new Vector3(0.04f, 1.5f, 0.05f), new Vector3(0, 1.55f, 0.03f), cream, 0.01f);
        B("Rail", g, new Vector3(1.9f, 0.04f, 0.04f), new Vector3(0, 2.62f, 0.14f), gold, 0.015f);
        // heavy folds: a wavy sheet each side, a valance across the top
        foreach (var s in new[] { -1, 1 }) Folds(g, new Vector3(s * 0.62f, 0, 0.12f), 0.55f, 2.6f, 9);
        Folds(g, new Vector3(0, 2.3f, 0.17f), 1.95f, 0.38f, 22);
        foreach (var s in new[] { -1, 1 }) B("Tieback", g, new Vector3(0.3f, 0.04f, 0.2f), new Vector3(s * 0.62f, 1.05f, 0.16f), gold, 0.015f);
    }

    /// A curtain: a sheet of vertical folds, width × height metres, hanging from (center, top at at.y + height).
    GameObject Folds(Transform parent, Vector3 at, float width, float height, int folds)
    {
        int nx = folds * 6, ny = 6;
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float u = i / (float)nx, v = j / (float)ny;
                float z = Mathf.Sin(u * folds * Mathf.PI * 2) * 0.045f * (0.6f + 0.4f * v);
                vs.Add(new Vector3((u - 0.5f) * width, v * height, z) * M); uv.Add(new Vector2(u * folds, v));
            }
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(b); tr.Add(d); tr.Add(c);   // facing the room (+z)
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var go = Art.Obj("Curtain", parent, m, curtain);
        go.transform.localPosition = at * M;
        return go;
    }

    void SideTableLamp(Vector3 at)
    {
        var g = Group("SideTable", at);
        Lathe("Table", g, new[] { new Vector2(0.001f, 0), new Vector2(0.16f, 0), new Vector2(0.03f, 0.05f), new Vector2(0.025f, 0.52f), new Vector2(0.24f, 0.55f), new Vector2(0.24f, 0.58f), new Vector2(0.001f, 0.58f) }, Vector3.zero, darkWood, 24);
        Lathe("LampBase", g, new[] { new Vector2(0.001f, 0.58f), new Vector2(0.09f, 0.58f), new Vector2(0.11f, 0.68f), new Vector2(0.04f, 0.8f), new Vector2(0.015f, 0.95f), new Vector2(0.001f, 0.95f) }, Vector3.zero, brass, 20);
        var shade = Art.Mat(new Color(0.85f, 0.62f, 0.38f), 0.2f);
        Lathe("Shade", g, new[] { new Vector2(0.21f, 0.9f), new Vector2(0.11f, 1.15f), new Vector2(0.1f, 1.15f), new Vector2(0.2f, 0.9f) }, Vector3.zero, shade, 28);
        for (int i = 0; i < 28; i++)
        {
            float a = i * Mathf.PI * 2 / 28;
            B("Fringe", g, new Vector3(0.008f, 0.06f, 0.008f), new Vector3(Mathf.Cos(a) * 0.205f, 0.87f, Mathf.Sin(a) * 0.205f), gold, 0, false);
        }
        lampLight = new GameObject("LampLight").AddComponent<Light>();
        lampLight.transform.SetParent(g, false); lampLight.transform.localPosition = new Vector3(0, 1.0f, 0) * M;
        lampLight.type = LightType.Point; lampLight.color = new Color(1f, 0.7f, 0.42f); lampLight.range = 3.2f * M; lampLight.intensity = 1.2f;
        lampLight.shadows = LightShadows.Soft;
    }

    void Frames()
    {
        var hd = Depth / 2;
        // the parents' wedding photo (sepia, two figures), and a verse in a gilded frame (an ornament, no lettering)
        var photo = Art.Mat(Color.white, 0.4f, 0f, Art.Paint(96, 128, (x, y) =>
        {
            float u = x / 95f, v = y / 127f;
            var c = Color.Lerp(new Color(0.42f, 0.32f, 0.22f), new Color(0.82f, 0.7f, 0.52f), v * 0.6f + 0.2f);
            float bride = Mathf.Pow((u - 0.38f) / 0.13f, 2) + Mathf.Pow((v - 0.35f) / 0.38f, 2);
            float groom = Mathf.Pow((u - 0.63f) / 0.11f, 2) + Mathf.Pow((v - 0.38f) / 0.4f, 2);
            float hb = Mathf.Pow((u - 0.38f) / 0.06f, 2) + Mathf.Pow((v - 0.76f) / 0.06f, 2), hg = Mathf.Pow((u - 0.63f) / 0.055f, 2) + Mathf.Pow((v - 0.8f) / 0.055f, 2);
            if (bride < 1) c = new Color(0.93f, 0.88f, 0.78f);
            if (groom < 1) c = new Color(0.18f, 0.14f, 0.11f);
            if (hb < 1 || hg < 1) c = new Color(0.6f, 0.45f, 0.33f);
            float vig = Mathf.Clamp01(1.2f - Mathf.Sqrt(Mathf.Pow(u - 0.5f, 2) + Mathf.Pow(v - 0.5f, 2)) * 1.4f);
            return c * (0.55f + 0.45f * vig);
        }));
        Framed("WeddingPhoto", new Vector3(-1.05f, 1.72f, -hd + 0.03f), 0.42f, 0.56f, photo);
        var verse = Art.Mat(Color.white, 0.5f, 0.3f, Art.Paint(160, 96, (x, y) =>
        {
            float u = x / 159f - 0.5f, v = y / 95f - 0.5f;
            var c = new Color(0.08f, 0.22f, 0.15f);
            float wave = Mathf.Abs(v - Mathf.Sin(u * 18f) * 0.08f); if (wave < 0.03f && Mathf.Abs(u) < 0.38f) c = new Color(0.86f, 0.68f, 0.3f);
            float wave2 = Mathf.Abs(v + 0.18f - Mathf.Sin(u * 24f + 1) * 0.05f); if (wave2 < 0.02f && Mathf.Abs(u) < 0.3f) c = new Color(0.86f, 0.68f, 0.3f);
            if (Mathf.Abs(Mathf.Abs(u) - 0.46f) < 0.012f || Mathf.Abs(Mathf.Abs(v) - 0.42f) < 0.02f) c = new Color(0.86f, 0.68f, 0.3f);
            return c;
        }));
        Framed("Verse", new Vector3(1.05f, 1.75f, -hd + 0.03f), 0.75f, 0.45f, verse);
    }

    void Framed(string name, Vector3 c, float w, float h, Material pic)
    {
        var g = Group(name, c);
        B("Frame", g, new Vector3(w + 0.1f, h + 0.1f, 0.04f), Vector3.zero, gold, 0.015f);
        var p = Art.Obj("Picture", g, Art.Prim(PrimitiveType.Quad), pic, false);
        p.transform.localPosition = new Vector3(0, 0, 0.022f) * M; p.transform.localRotation = Quaternion.Euler(0, 180, 0);
        p.transform.localScale = new Vector3(w, h, 1) * M;
    }

    void Clock(Vector3 c)
    {
        var g = Group("Clock", c);
        B("Case", g, new Vector3(0.44f, 0.44f, 0.05f), Vector3.zero, darkWood, 0.2f);
        var face = Art.Obj("Face", g, Art.Disc(0.19f * M, 48), white, false);
        face.transform.localPosition = new Vector3(0, 0, 0.027f) * M; face.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var ink = Art.Mat(new Color(0.1f, 0.08f, 0.06f), 0.4f);
        for (int i = 0; i < 12; i++)
        {
            var mk = B("Mark", g, new Vector3(i % 3 == 0 ? 0.016f : 0.008f, 0.035f, 0.004f), Vector3.zero, ink, 0, false);
            mk.transform.localRotation = Quaternion.Euler(0, 0, i * 30); mk.transform.localPosition = (mk.transform.localRotation * Vector3.up * 0.155f + Vector3.forward * 0.03f) * M;
        }
        clockHour = new GameObject("Hour").transform; clockHour.SetParent(g, false); clockHour.localPosition = new Vector3(0, 0, 0.034f) * M;
        B("Hand", clockHour, new Vector3(0.016f, 0.1f, 0.004f), new Vector3(0, 0.045f, 0), ink, 0, false);
        clockMinute = new GameObject("Minute").transform; clockMinute.SetParent(g, false); clockMinute.localPosition = new Vector3(0, 0, 0.038f) * M;
        B("Hand", clockMinute, new Vector3(0.01f, 0.15f, 0.004f), new Vector3(0, 0.068f, 0), ink, 0, false);
        Lathe("Pin", g, new[] { new Vector2(0.001f, 0), new Vector2(0.012f, 0), new Vector2(0.001f, 0.01f) }, new Vector3(0, 0, 0.04f), brass, 10).transform.localRotation = Quaternion.Euler(90, 0, 0);
    }

    /// The clock's hands: hours as a decimal (20.17 is 8:10 pm). Seen from the room they turn clockwise.
    public void SetClock(float hours)
    {
        float h12 = hours % 12f, minutes = (hours % 1f) * 60f;
        if (clockHour != null) clockHour.localRotation = Quaternion.Euler(0, 0, h12 * 30f);
        if (clockMinute != null) clockMinute.localRotation = Quaternion.Euler(0, 0, minutes * 6f);
    }

    void Lights()
    {
        chandelierLight = new GameObject("ChandelierLight").AddComponent<Light>();
        chandelierLight.transform.position = W(0, Height - 0.75f, -0.85f);
        chandelierLight.type = LightType.Point; chandelierLight.color = new Color(1f, 0.8f, 0.55f);
        chandelierLight.range = 6.5f * M; chandelierLight.intensity = 1.7f; chandelierLight.shadows = LightShadows.Soft; chandelierLight.shadowStrength = 0.7f;
        fillLight = new GameObject("SalonFill").AddComponent<Light>();
        fillLight.type = LightType.Directional; fillLight.color = new Color(1f, 0.88f, 0.74f); fillLight.intensity = 0.35f; fillLight.shadows = LightShadows.None;
        fillLight.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.45f, -1f));
        moonLight = new GameObject("Moon").AddComponent<Light>();
        moonLight.type = LightType.Directional; moonLight.color = new Color(0.45f, 0.58f, 1f); moonLight.intensity = 0f; moonLight.shadows = LightShadows.Soft;
        moonLight.transform.rotation = Quaternion.LookRotation(new Vector3(-0.6f, -0.5f, 0.6f));
        Night(false);
    }

    /// Evening (the chandelier on) or 2 a.m. (only the lamp, the phone glow, blue moonlight).
    public void Night(bool on)
    {
        chandelierLight.enabled = !on;
        foreach (var b in bulbs) b.sharedMaterial = on ? bulbOff : bulbOn;
        fillLight.intensity = on ? 0.06f : 0.35f;
        moonLight.intensity = on ? 0.42f : 0f;
        lampLight.intensity = on ? 1.0f : 1.2f;
        if (windowGlass != null) windowGlass.sharedMaterial = on ? windowNight : windowDusk;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = on ? new Color(0.1f, 0.12f, 0.2f) : new Color(0.55f, 0.45f, 0.36f);
        RenderSettings.ambientEquatorColor = on ? new Color(0.07f, 0.07f, 0.11f) : new Color(0.4f, 0.3f, 0.22f);
        RenderSettings.ambientGroundColor = on ? new Color(0.03f, 0.03f, 0.05f) : new Color(0.18f, 0.12f, 0.08f);
    }

    /// Everything in the room on or off (the café's game footage is filmed in between).
    public void Show(bool on)
    {
        if (root != null) root.gameObject.SetActive(on);
        foreach (var l in new[] { chandelierLight, fillLight, moonLight }) if (l != null) l.gameObject.SetActive(on);
    }

    /// A phone: a dark slab with a lit screen on the side that faces `holder`.
    public static Transform Phone(string name)
    {
        var t = new GameObject(name).transform;
        var body = Art.Obj("Body", t, RoundedBox(new Vector3(0.075f, 0.155f, 0.009f) * M, 0.008f * M), Art.Mat(new Color(0.06f, 0.06f, 0.07f), 0.8f, 0.3f));
        var screen = Art.Obj("Screen", t, Art.Prim(PrimitiveType.Quad), Unlit(new Color(0.55f, 0.75f, 0.95f)), false);
        screen.transform.localPosition = new Vector3(0, 0, -0.0048f) * M; screen.transform.localScale = new Vector3(0.068f, 0.145f, 1) * M;
        return t;
    }
}
