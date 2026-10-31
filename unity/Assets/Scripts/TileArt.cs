// Domino tiles made in Unity: a rounded ivory body with bevelled edges, and faces drawn in code
// (two halves of pips with a brass line between them). Sizes in "board units" (a tile is 1.94 × 0.96 × 0.3).
using System.Collections.Generic;
using UnityEngine;

public static class TileArt
{
    public const float W = 1.94f, D = 0.96f, H = 0.3f;
    static readonly int[][] Pips =
    {
        new int[0], new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 1, 2, 6, 7, 8 },   // a six: two rows of three along the tile (2 columns × 3 rows when it stands upright)
    };

    static Mesh body, face;
    static Material bodyMat, ghostMat;
    static readonly Dictionary<int, Material> faceMats = new Dictionary<int, Material>();
    static readonly Dictionary<int, Texture2D> faceTex = new Dictionary<int, Texture2D>();
    public static Color Ivory = new Color(0.96f, 0.93f, 0.85f);
    public static Color PipInk = new Color(0.06f, 0.04f, 0.03f);
    static Color Divider = new Color(0.62f, 0.5f, 0.28f);
    static bool Veins;
    public static string SkinId { get; private set; } = "tiles_ivory";

    /// The tile look bought in the shop (the server's skin ids): face, pips, the line between, marble veins.
    public static void SetSkin(string id)
    {
        if (string.IsNullOrEmpty(id)) id = "tiles_ivory";
        if (id == SkinId) return;
        SkinId = id;
        Look(id, out Ivory, out PipInk, out Divider, out Veins);
        faceTex.Clear(); faceMats.Clear();
        if (bodyMat != null) bodyMat.color = Ivory;
        Changed?.Invoke();
    }
    public static event System.Action Changed;

    static void Look(string id, out Color face, out Color pip, out Color line, out bool veins)
    {
        veins = false;
        switch (id)
        {
            case "tiles_ebony": face = new Color(0.13f, 0.1f, 0.09f); pip = new Color(0.96f, 0.9f, 0.78f); line = new Color(0.85f, 0.68f, 0.3f); break;
            case "tiles_marble": face = new Color(0.93f, 0.93f, 0.95f); pip = new Color(0.12f, 0.13f, 0.2f); line = new Color(0.55f, 0.56f, 0.62f); veins = true; break;
            case "tiles_jade": face = new Color(0.3f, 0.6f, 0.46f); pip = new Color(0.98f, 0.96f, 0.88f); line = new Color(0.85f, 0.75f, 0.45f); break;
            case "tiles_gold": face = new Color(0.93f, 0.74f, 0.33f); pip = new Color(0.28f, 0.13f, 0.04f); line = new Color(0.55f, 0.35f, 0.1f); break;
            default: face = new Color(0.96f, 0.93f, 0.85f); pip = new Color(0.1f, 0.07f, 0.05f); line = new Color(0.62f, 0.5f, 0.28f); break;
        }
    }

    /// A 6 | 5 tile in another look, for the shop.
    public static Texture2D Preview(string id)
    {
        var keep = (Ivory, PipInk, Divider, Veins);
        Look(id, out Ivory, out PipInk, out Divider, out Veins);
        var t = Paint(6, 5);
        (Ivory, PipInk, Divider, Veins) = keep;
        return t;
    }

    public static Shader Lit => Shader.Find("Legacy Shaders/Specular") ?? Shader.Find("Standard");
    public static Shader Clear => Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Sprites/Default");

    /// The tile body: a rounded rectangle, bevelled top and bottom.
    public static Mesh Body
    {
        get
        {
            if (body != null) return body;
            const float r = 0.12f, bev = 0.05f; const int seg = 5;
            var ring = new List<Vector2>(); var nrm = new List<Vector2>();
            float hw = W / 2, hd = D / 2;
            Vector2[] corners = { new Vector2(hw - r, hd - r), new Vector2(-hw + r, hd - r), new Vector2(-hw + r, -hd + r), new Vector2(hw - r, -hd + r) };
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= seg; s++)
                {
                    float a = (c * 90f + s * 90f / seg) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    ring.Add(corners[c] + dir * r); nrm.Add(dir);
                }
            int n = ring.Count;
            // four rings: bottom inset, bottom, top, top inset
            float[] ys = { -H / 2, -H / 2 + bev, H / 2 - bev, H / 2 };
            float[] ins = { bev, 0, 0, bev };
            var v = new List<Vector3>(); var tri = new List<int>();
            for (int k = 0; k < 4; k++)
                for (int i = 0; i < n; i++) { var q = ring[i] - nrm[i] * ins[k]; v.Add(new Vector3(q.x, ys[k], q.y)); }
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < n; i++)
                {
                    int a = k * n + i, b = k * n + (i + 1) % n, c = (k + 1) * n + i, d = (k + 1) * n + (i + 1) % n;
                    tri.AddRange(new[] { a, c, b, b, c, d });
                }
            // caps (their own vertices, so they stay flat)
            int top = v.Count; v.Add(new Vector3(0, H / 2, 0)); for (int i = 0; i < n; i++) v.Add(v[3 * n + i]);
            for (int i = 0; i < n; i++) tri.AddRange(new[] { top, top + 1 + (i + 1) % n, top + 1 + i });
            int bot = v.Count; v.Add(new Vector3(0, -H / 2, 0)); for (int i = 0; i < n; i++) v.Add(v[i]);
            for (int i = 0; i < n; i++) tri.AddRange(new[] { bot, bot + 1 + i, bot + 1 + (i + 1) % n });
            // every triangle faces outward (whichever way the rings ran)
            for (int i = 0; i < tri.Count; i += 3)
            {
                Vector3 p0 = v[tri[i]], p1 = v[tri[i + 1]], p2 = v[tri[i + 2]];
                var nn = Vector3.Cross(p1 - p0, p2 - p0);
                var mid = (p0 + p1 + p2) / 3f;
                var outward = Mathf.Abs(mid.y) >= H / 2 - 1e-4f && Mathf.Abs(nn.y) > 0.9f * nn.magnitude ? new Vector3(0, Mathf.Sign(mid.y), 0) : new Vector3(mid.x, mid.y * 0.2f, mid.z);
                if (Vector3.Dot(nn, outward) < 0) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; }
            }
            body = new Mesh { name = "Tile" };
            body.SetVertices(v); body.SetTriangles(tri, 0);
            body.RecalculateNormals(); body.RecalculateBounds();
            return body;
        }
    }

    /// The printed face: a quad just above the top, u running along the tile (value a on the left half).
    public static Mesh Face
    {
        get
        {
            if (face != null) return face;
            float hw = W / 2 - 0.06f, hd = D / 2 - 0.06f, y = H / 2 + 0.003f;
            face = new Mesh { name = "TileFace" };
            face.vertices = new[] { new Vector3(-hw, y, -hd), new Vector3(hw, y, -hd), new Vector3(-hw, y, hd), new Vector3(hw, y, hd) };
            face.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            face.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            face.RecalculateNormals();
            return face;
        }
    }

    public static Material BodyMat => bodyMat ??= Make(Lit, Ivory);
    public static Material GhostMat => ghostMat ??= Make(Clear, new Color(1f, 0.85f, 0.4f, 0.55f));

    public static Material Make(Shader s, Color c)
    {
        var m = new Material(s) { color = c };
        if (m.HasProperty("_Shininess")) m.SetFloat("_Shininess", 0.35f);
        if (m.HasProperty("_SpecColor")) m.SetColor("_SpecColor", new Color(0.35f, 0.33f, 0.3f));
        return m;
    }

    /// The face of a tile showing a | b.
    public static Texture2D FaceTexture(int a, int b)
    {
        int key = a * 7 + b;
        if (faceTex.TryGetValue(key, out var t)) return t;
        t = Paint(a, b);
        faceTex[key] = t;
        return t;
    }

    static Texture2D Paint(int a, int b)
    {
        const int TW = 384, TH = 192;   // sharp pips close up
        var px = new Color[TW * TH];
        for (int y = 0; y < TH; y++)
            for (int x = 0; x < TW; x++)
            {
                var c = Ivory;
                if (Veins)
                {
                    float n = Mathf.PerlinNoise(x * 0.025f + 3.1f, y * 0.06f) * 6f + x * 0.03f;
                    float v = Mathf.Pow(1 - Mathf.Abs(Mathf.Sin(n)), 10f);
                    c = Color.Lerp(c, new Color(0.6f, 0.62f, 0.68f), v * 0.55f);
                }
                px[y * TW + x] = c;
            }
        // the line between the halves
        for (int y = 14; y < TH - 14; y++) for (int x = TW / 2 - 2; x < TW / 2 + 2; x++) px[y * TW + x] = Divider;
        DrawPips(px, TW, TH, a, 0);
        DrawPips(px, TW, TH, b, TW / 2);
        var t = new Texture2D(TW, TH, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        t.SetPixels(px); t.Apply(true);
        return t;
    }

    static void DrawPips(Color[] px, int TW, int TH, int n, int x0)
    {
        float cell = TH / 3.4f, r = TH * 0.095f;
        float cx0 = x0 + TW / 4f - cell, cy0 = TH / 2f - cell;
        foreach (var p in Pips[n])
        {
            float cx = cx0 + (p % 3) * cell, cy = cy0 + (p / 3) * cell;
            for (int y = (int)(cy - r - 2); y <= (int)(cy + r + 2); y++)
                for (int x = (int)(cx - r - 2); x <= (int)(cx + r + 2); x++)
                {
                    if (x < 0 || y < 0 || x >= TW || y >= TH) continue;
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float k = Mathf.Clamp01(r - d + 0.5f);
                    if (k > 0) px[y * TW + x] = Color.Lerp(px[y * TW + x], PipInk, k);
                }
        }
    }

    public static Material FaceMat(int a, int b)
    {
        int key = a * 7 + b;
        if (faceMats.TryGetValue(key, out var m)) return m;
        m = Make(Lit, Color.white);
        m.mainTexture = FaceTexture(a, b);
        faceMats[key] = m;
        return m;
    }

    /// A tile object: face up showing a (left) | b (right), or blank (face down / its back toward you).
    public static GameObject Make(int a, int b, bool faceUp = true)
    {
        var go = new GameObject("Tile");
        var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = Body;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = BodyMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        if (faceUp)
        {
            var f = new GameObject("Face");
            f.transform.SetParent(go.transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = Face;
            var fr = f.AddComponent<MeshRenderer>(); fr.sharedMaterial = FaceMat(a, b);
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return go;
    }
}
