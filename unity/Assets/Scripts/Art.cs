// Small toolkit for the board games' 3D: physically based materials, textures painted in code (wood,
// felt, cards), turned pieces (a profile spun round, like on a lathe), extruded shapes, rings and dice.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class Art
{
    static Shader std;
    public static Shader Std => std != null ? std : (std = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Specular"));

    /// A Standard (physically based) material: colour or texture, how glossy, how metallic.
    public static Material Mat(Color c, float smooth = 0.5f, float metal = 0f, Texture tex = null)
    {
        var m = new Material(Std) { color = c };
        if (tex != null) m.mainTexture = tex;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (m.HasProperty("_Shininess")) m.SetFloat("_Shininess", Mathf.Lerp(0.05f, 0.9f, smooth));
        return m;
    }

    /// See-through overlay (highlights, rings): unlit-looking, drawn over the board.
    public static Material Glow(Color c)
    {
        var m = new Material(TileArt.Clear) { color = c };
        m.renderQueue = 3100;
        return m;
    }

    public static Texture2D Paint(int w, int h, Func<int, int, Color> f, bool mips = true)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
        t.SetPixels(px); t.Apply(mips);
        return t;
    }

    /// Wood grain at a point: long fibres along x, a slow wave, darker growth rings.
    public static Color Wood(float x, float y, Color b, Color dark, Color light, float fibres = 40f, float seed = 0f)
    {
        float wave = Mathf.PerlinNoise(x * 0.004f + seed, y * 0.02f + seed) * 6f;
        float g = Mathf.Sin((y * fibres * 0.01f + wave) * 3.1f) * 0.5f + 0.5f;
        float fine = Mathf.PerlinNoise(x * 0.08f + seed * 3, y * 0.9f + seed) ;
        var c = Color.Lerp(b, dark, Mathf.Pow(g, 3f) * 0.75f);
        c = Color.Lerp(c, light, Mathf.Pow(1 - g, 6f) * 0.35f);
        return c * (0.9f + fine * 0.2f);
    }
    public static Texture2D WoodTex(int w, int h, Color b, Color dark, Color light, float fibres = 40f, float seed = 0f) =>
        Paint(w, h, (x, y) => Wood(x, y, b, dark, light, fibres, seed));

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    /* ---------- meshes ---------- */

    static readonly Dictionary<PrimitiveType, Mesh> prims = new Dictionary<PrimitiveType, Mesh>();
    public static Mesh Prim(PrimitiveType p)
    {
        if (prims.TryGetValue(p, out var m)) return m;
        var go = GameObject.CreatePrimitive(p);
        m = go.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(go);
        prims[p] = m;
        return m;
    }

    public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
        return go;
    }

    /// A box of size (w, h, d) whose bottom sits at y.
    public static GameObject Box(string name, Transform parent, float w, float h, float d, Vector3 at, Material mat)
    {
        var go = Obj(name, parent, Prim(PrimitiveType.Cube), mat);
        go.transform.localScale = new Vector3(w, h, d);
        go.transform.localPosition = at + Vector3.up * h / 2;
        return go;
    }

    /// A flat rectangle facing up, size (w, d), at local position `at` (its texture's top toward +z).
    public static GameObject Sheet(string name, Transform parent, float w, float d, Vector3 at, Material mat)
    {
        var go = Obj(name, parent, Prim(PrimitiveType.Quad), mat, false);
        go.transform.localPosition = at;
        go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        go.transform.localScale = new Vector3(w, d, 1);
        return go;
    }

    /// A shape turned on a lathe: the profile (radius, height) spun round the y axis.
    public static Mesh Lathe(Vector2[] prof, int seg = 36)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        int n = prof.Length;
        for (int s = 0; s <= seg; s++)
        {
            float a = s * Mathf.PI * 2 / seg, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            for (int i = 0; i < n; i++) { vs.Add(new Vector3(prof[i].x * ca, prof[i].y, prof[i].x * sa)); uv.Add(new Vector2(s / (float)seg, i / (float)(n - 1))); }
        }
        for (int s = 0; s < seg; s++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = s * n + i, b = (s + 1) * n + i;
                tr.Add(a); tr.Add(a + 1); tr.Add(b);
                tr.Add(b); tr.Add(a + 1); tr.Add(b + 1);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(tr, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        FaceOut(m);
        return m;
    }

    /// Makes sure a closed mesh's triangles face away from its middle (flips them all if most face in).
    public static void FaceOut(Mesh m)
    {
        var v = m.vertices; var t = m.triangles; var c = m.bounds.center;
        int outCount = 0, inCount = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            var mid = (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3 - c; mid.y = 0;
            if (mid.sqrMagnitude < 1e-6f) continue;
            if (Vector3.Dot(n, mid) >= 0) outCount++; else inCount++;
        }
        if (inCount > outCount)
        {
            for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            m.triangles = t; m.RecalculateNormals();
        }
    }

    /// A flat shape (in the x-y plane) pushed out `depth` along z, centred on z = 0.
    public static Mesh Extrude(List<Vector2> poly, float depth)
    {
        if (Area(poly) < 0) poly.Reverse();   // counter-clockwise
        var cap = Triangulate(poly);
        var vs = new List<Vector3>(); var tr = new List<int>();
        int n = poly.Count; float h = depth / 2;
        // front and back caps
        for (int i = 0; i < n; i++) vs.Add(new Vector3(poly[i].x, poly[i].y, -h));
        for (int i = 0; i < n; i++) vs.Add(new Vector3(poly[i].x, poly[i].y, h));
        for (int i = 0; i < cap.Count; i += 3) { tr.Add(cap[i]); tr.Add(cap[i + 2]); tr.Add(cap[i + 1]); }
        for (int i = 0; i < cap.Count; i += 3) { tr.Add(n + cap[i]); tr.Add(n + cap[i + 1]); tr.Add(n + cap[i + 2]); }
        // the sides, each with its own vertices so the edge stays crisp
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n, b = vs.Count;
            vs.Add(new Vector3(poly[i].x, poly[i].y, -h)); vs.Add(new Vector3(poly[j].x, poly[j].y, -h));
            vs.Add(new Vector3(poly[j].x, poly[j].y, h)); vs.Add(new Vector3(poly[i].x, poly[i].y, h));
            tr.Add(b); tr.Add(b + 1); tr.Add(b + 2); tr.Add(b); tr.Add(b + 2); tr.Add(b + 3);
        }
        var m = new Mesh(); m.SetVertices(vs); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        FaceOutward3D(m);
        return m;
    }

    /// Like FaceOut, measured from the centre in all three directions.
    static void FaceOutward3D(Mesh m)
    {
        var v = m.vertices; var t = m.triangles; var c = m.bounds.center;
        float score = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            var n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            score += Vector3.Dot(n, (v[t[i]] + v[t[i + 1]] + v[t[i + 2]]) / 3 - c);
        }
        if (score < 0) { for (int i = 0; i < t.Length; i += 3) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; } m.triangles = t; m.RecalculateNormals(); }
    }

    static float Area(List<Vector2> p) { float a = 0; for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; a += p[i].x * q.y - q.x * p[i].y; } return a / 2; }

    /// Ear clipping for a simple counter-clockwise polygon.
    static List<int> Triangulate(List<Vector2> p)
    {
        var idx = new List<int>(); for (int i = 0; i < p.Count; i++) idx.Add(i);
        var tris = new List<int>();
        int guard = 0;
        while (idx.Count > 3 && guard++ < 10000)
        {
            bool cut = false;
            for (int k = 0; k < idx.Count; k++)
            {
                int a = idx[(k + idx.Count - 1) % idx.Count], b = idx[k], c = idx[(k + 1) % idx.Count];
                var A = p[a]; var B = p[b]; var C = p[c];
                if ((B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x) <= 1e-9f) continue;   // reflex corner
                bool inside = false;
                foreach (int o in idx) { if (o == a || o == b || o == c) continue; if (InTri(p[o], A, B, C)) { inside = true; break; } }
                if (inside) continue;
                tris.Add(a); tris.Add(b); tris.Add(c); idx.RemoveAt(k); cut = true; break;
            }
            if (!cut) break;
        }
        if (idx.Count == 3) { tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]); }
        return tris;
    }
    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    /// Points along a cubic Bézier curve (without its first point).
    public static void Bezier(List<Vector2> o, Vector2 p0, Vector2 c1, Vector2 c2, Vector2 p1, int steps = 10)
    {
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1 - t;
            o.Add(u * u * u * p0 + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * p1);
        }
    }
    public static void Quad2(List<Vector2> o, Vector2 p0, Vector2 c, Vector2 p1, int steps = 6)
    {
        for (int i = 1; i <= steps; i++) { float t = i / (float)steps, u = 1 - t; o.Add(u * u * p0 + 2 * u * t * c + t * t * p1); }
    }

    /// A flat ring (inner and outer radius), facing up.
    static readonly Dictionary<string, Mesh> rings = new Dictionary<string, Mesh>();
    public static Mesh Ring(float r0, float r1, int seg = 40)
    {
        string key = r0 + ":" + r1;
        if (rings.TryGetValue(key, out var m)) return m;
        var vs = new List<Vector3>(); var tr = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = -i * Mathf.PI * 2 / seg;
            var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            vs.Add(d * r0); vs.Add(d * r1);
            if (i < seg) { int b = i * 2; tr.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 }); }
        }
        m = new Mesh(); m.SetVertices(vs); m.SetTriangles(tr, 0); m.RecalculateNormals();
        if (m.normals[0].y < 0) { tr.Reverse(); m.SetTriangles(tr, 0); m.RecalculateNormals(); }
        return rings[key] = m;
    }
    /// A flat disc facing up.
    public static Mesh Disc(float r, int seg = 32) => Ring(0.0001f, r, seg);

    /* ---------- dice ---------- */

    static readonly int[][] Pips =
    {
        new int[0], new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 },
    };
    static readonly Dictionary<string, Texture2D> dieFaces = new Dictionary<string, Texture2D>();
    static Texture2D DieFace(int v, Color face, Color pip)
    {
        string key = v + face.ToString() + pip;
        if (dieFaces.TryGetValue(key, out var t)) return t;
        const int S = 64; float r = 6.2f;
        t = Paint(S, S, (x, y) =>
        {
            var c = face;
            foreach (int p in Pips[v])
            {
                float cx = 16 + (p % 3) * 16, cy = 16 + (p / 3) * 16;
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                c = Color.Lerp(c, pip, Mathf.Clamp01(r - d + 0.5f));
            }
            float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(S - 1 - x, S - 1 - y));
            return c * (edge < 3 ? 0.85f + edge * 0.05f : 1f);
        });
        return dieFaces[key] = t;
    }

    /// A die showing `v` on top: a cube with a painted face on each side (opposite faces add up to 7).
    public static GameObject Die(Transform parent, int v, float size, Color face, Color pip)
    {
        var root = new GameObject("Die"); root.transform.SetParent(parent, false);
        var body = Obj("Body", root.transform, Prim(PrimitiveType.Cube), Mat(face, 0.7f));
        body.transform.localScale = Vector3.one * size * 0.98f;
        v = Mathf.Clamp(v, 1, 6);
        // top, bottom, and four sides
        int[] side = v == 1 || v == 6 ? new[] { 2, 3, 5, 4 } : v == 2 || v == 5 ? new[] { 1, 3, 6, 4 } : new[] { 1, 2, 6, 5 };
        (Vector3 n, int val)[] faces =
        {
            (Vector3.up, v), (Vector3.down, 7 - v),
            (Vector3.forward, side[0]), (Vector3.right, side[1]), (Vector3.back, side[2]), (Vector3.left, side[3]),
        };
        foreach (var (n, val) in faces)
        {
            var q = Obj("Face" + val, root.transform, Prim(PrimitiveType.Quad), Mat(Color.white, 0.75f, 0, DieFace(val, face, pip)), false);
            q.transform.localPosition = n * size * 0.501f;
            q.transform.localRotation = Quaternion.LookRotation(-n, n == Vector3.up || n == Vector3.down ? Vector3.forward : Vector3.up);
            q.transform.localScale = Vector3.one * size * 0.96f;
        }
        return root;
    }
}

/// Moves an object from one place to another along a little arc (a piece being played), then stops.
public class Hop : MonoBehaviour
{
    public Vector3 from, to; public float height = 0.6f, time = 0.35f;
    float t;
    void Start() { transform.localPosition = from; }
    void Update()
    {
        t += Time.deltaTime / time;
        float k = Mathf.Clamp01(t), e = 1 - Mathf.Pow(1 - k, 3);
        transform.localPosition = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(k * Mathf.PI) * height;
        if (t >= 1) { transform.localPosition = to; Destroy(this); }
    }
}

/// A die tumbling as it lands, then settling face up.
public class Tumble : MonoBehaviour
{
    public float time = 0.7f, height = 0.8f; Vector3 rest; Quaternion restRot; float t; Vector3 spin;
    void Start() { rest = transform.localPosition; restRot = transform.localRotation; spin = new Vector3(UnityEngine.Random.Range(500f, 900f), UnityEngine.Random.Range(300f, 700f), UnityEngine.Random.Range(200f, 500f)); }
    void Update()
    {
        t += Time.deltaTime / time;
        float k = Mathf.Clamp01(t);
        transform.localPosition = rest + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3)) * height * (1 - k);
        transform.localRotation = k < 1 ? Quaternion.Euler(spin * Time.time) : restRot;
        if (t >= 1) { transform.localPosition = rest; transform.localRotation = restRot; Destroy(this); }
    }
}

/// Bobs a piece up and down a little: this one can move.
public class Bob : MonoBehaviour
{
    Vector3 rest; float phase;
    void Start() { rest = transform.localPosition; phase = UnityEngine.Random.value * 6f; }
    void Update() { transform.localPosition = rest + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 4f + phase)) * 0.12f; }
}
