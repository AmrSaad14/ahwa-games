// Sculpting with shapes: a surface is the set of points where a "distance field" is zero. Capsules, ellipsoids and
// rounded boxes are blended with smooth unions (flesh flowing from one shape into the next), carved with smooth
// subtractions, cut with planes, then turned into a mesh with surface nets and smoothed onto the true surface.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingsBattle.Forge
{
    public enum Shape { Capsule, Ellipsoid, Box }

    /// One shape: attached to a bone (for skin weights), with a colour painted where it is the nearest shape.
    public class Prim
    {
        public Shape shape;
        public Vector3 a, b;            // capsule ends (a = b for a ball); ellipsoid / box centre in a
        public float ra, rb;            // capsule radii at a and b
        public Vector3 size;            // ellipsoid radii, box half size
        public float round;             // box corner rounding
        public Quaternion rot = Quaternion.identity;   // ellipsoid / box orientation
        public int bone;
        public Color color = Color.clear;   // clear: the layer's own colour
        public bool carve;              // cut away instead of add
        public float carveK = 0.006f;

        public static Prim Cap(int bone, Vector3 a, Vector3 b, float ra, float rb = -1) => new Prim { shape = Shape.Capsule, bone = bone, a = a, b = b, ra = ra, rb = rb < 0 ? ra : rb };
        public static Prim Ball(int bone, Vector3 c, float r) => new Prim { shape = Shape.Capsule, bone = bone, a = c, b = c, ra = r, rb = r };
        public static Prim Ell(int bone, Vector3 c, Vector3 radii, Quaternion? rot = null) => new Prim { shape = Shape.Ellipsoid, bone = bone, a = c, size = radii, rot = rot ?? Quaternion.identity };
        public static Prim RBox(int bone, Vector3 c, Vector3 half, float round, Quaternion? rot = null) => new Prim { shape = Shape.Box, bone = bone, a = c, size = half, round = round, rot = rot ?? Quaternion.identity };
        public Prim Paint(Color c) { color = c; return this; }
        public Prim Cut(float k = 0.006f) { carve = true; carveK = k; return this; }

        Quaternion inv; bool invSet;
        public float Dist(Vector3 p)
        {
            switch (shape)
            {
                case Shape.Capsule:
                {
                    var ab = b - a; float l2 = Vector3.Dot(ab, ab);
                    float t = l2 < 1e-12f ? 0 : Mathf.Clamp01(Vector3.Dot(p - a, ab) / l2);
                    return Vector3.Distance(p, a + ab * t) - Mathf.Lerp(ra, rb, t);
                }
                case Shape.Ellipsoid:
                {
                    if (!invSet) { inv = Quaternion.Inverse(rot); invSet = true; }
                    var q = inv * (p - a);
                    var k0v = new Vector3(q.x / size.x, q.y / size.y, q.z / size.z);
                    var k1v = new Vector3(q.x / (size.x * size.x), q.y / (size.y * size.y), q.z / (size.z * size.z));
                    float k0 = k0v.magnitude, k1 = k1v.magnitude;
                    return k1 < 1e-9f ? -Mathf.Min(size.x, Mathf.Min(size.y, size.z)) : k0 * (k0 - 1) / k1;
                }
                default:
                {
                    if (!invSet) { inv = Quaternion.Inverse(rot); invSet = true; }
                    var q = inv * (p - a);
                    var d = new Vector3(Mathf.Abs(q.x) - size.x + round, Mathf.Abs(q.y) - size.y + round, Mathf.Abs(q.z) - size.z + round);
                    var o = new Vector3(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0), Mathf.Max(d.z, 0));
                    return o.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0) - round;
                }
            }
        }

        public void Grow(ref Vector3 lo, ref Vector3 hi, float pad)
        {
            float r = shape == Shape.Capsule ? Mathf.Max(ra, rb) : Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 1.75f;
            foreach (var c in shape == Shape.Capsule ? new[] { a, b } : new[] { a })
            {
                lo = Vector3.Min(lo, c - Vector3.one * (r + pad));
                hi = Vector3.Max(hi, c + Vector3.one * (r + pad));
            }
        }
    }

    /// A layer of the character (skin, a garment, hair...): shapes blended with smoothness k, made `grow` thicker,
    /// optionally cut by a function (positive where the layer is removed).
    public class Layer
    {
        public string name;
        public List<Prim> prims = new List<Prim>();
        public float k = 0.02f, grow;
        public Func<Vector3, float> clip;
        public Color color = Color.white;
        public float cell = 0.012f;          // mesh detail (meters)

        static float SMin(float a, float b, float k)
        {
            if (k <= 0) return Mathf.Min(a, b);
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }
        static float SMaxSub(float d, float c, float k)
        {
            // carve shape c out of d (smooth)
            float h = Mathf.Clamp01(0.5f - 0.5f * (d + c) / k);
            return Mathf.Lerp(d, -c, h) + k * h * (1 - h);
        }

        public float Field(Vector3 p)
        {
            float d = 1e9f;
            foreach (var q in prims) if (!q.carve) d = SMin(d, q.Dist(p), k);
            foreach (var q in prims) if (q.carve) d = SMaxSub(d, q.Dist(p), q.carveK);
            d -= grow;
            if (clip != null) d = Mathf.Max(d, clip(p));
            return d;
        }

        public Bounds Extent()
        {
            var lo = Vector3.one * 1e9f; var hi = -lo;
            foreach (var q in prims) if (!q.carve) q.Grow(ref lo, ref hi, grow + k + cell * 2);
            var b = new Bounds(); b.SetMinMax(lo, hi);
            return b;
        }
    }

    public static class Mesher
    {
        /// Surface nets: one vertex per grid cell the surface passes through, joined into quads across every
        /// grid edge the surface crosses, then pulled onto the surface along the field's gradient.
        public static void Build(Layer L, List<Vector3> verts, List<Vector3> norms, List<int> tris)
        {
            verts.Clear(); norms.Clear(); tris.Clear();
            var box = L.Extent();
            float h = L.cell;
            int nx = Mathf.CeilToInt(box.size.x / h) + 2, ny = Mathf.CeilToInt(box.size.y / h) + 2, nz = Mathf.CeilToInt(box.size.z / h) + 2;
            var o = box.min - Vector3.one * h * 0.5f;
            var val = new float[nx * ny * nz];
            int Idx(int x, int y, int z) => (z * ny + y) * nx + x;
            // coarse skip: far from every shape the field is simply "far"
            for (int z = 0; z < nz; z++)
                for (int y = 0; y < ny; y++)
                    for (int x = 0; x < nx; x++)
                        val[Idx(x, y, z)] = L.Field(o + new Vector3(x * h, y * h, z * h));
            var cellV = new int[nx * ny * nz];
            for (int i = 0; i < cellV.Length; i++) cellV[i] = -1;
            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            var corner = new float[8];
            var cpos = new Vector3[8];
            for (int z = 0; z < nz - 1; z++)
                for (int y = 0; y < ny - 1; y++)
                    for (int x = 0; x < nx - 1; x++)
                    {
                        int mask = 0;
                        for (int c = 0; c < 8; c++)
                        {
                            int cx = x + (c & 1), cy = y + ((c >> 1) & 1), cz = z + ((c >> 2) & 1);
                            corner[c] = val[Idx(cx, cy, cz)];
                            cpos[c] = o + new Vector3(cx * h, cy * h, cz * h);
                            if (corner[c] < 0) mask |= 1 << c;
                        }
                        if (mask == 0 || mask == 255) continue;
                        var sum = Vector3.zero; int n = 0;
                        for (int e = 0; e < 12; e++)
                        {
                            float a = corner[edges[e, 0]], b = corner[edges[e, 1]];
                            if ((a < 0) == (b < 0)) continue;
                            float t = a / (a - b);
                            sum += Vector3.Lerp(cpos[edges[e, 0]], cpos[edges[e, 1]], t); n++;
                        }
                        cellV[Idx(x, y, z)] = verts.Count;
                        verts.Add(sum / n);
                    }
            // quads across crossed edges
            for (int z = 1; z < nz - 1; z++)
                for (int y = 1; y < ny - 1; y++)
                    for (int x = 1; x < nx - 1; x++)
                    {
                        float v0 = val[Idx(x, y, z)];
                        // edge along x to (x+1,y,z): cells sharing it differ in y and z
                        if (x < nx - 1) Quad(v0, val[Idx(x + 1, y, z)], cellV[Idx(x, y - 1, z - 1)], cellV[Idx(x, y, z - 1)], cellV[Idx(x, y, z)], cellV[Idx(x, y - 1, z)], tris);
                        if (y < ny - 1) Quad(v0, val[Idx(x, y + 1, z)], cellV[Idx(x - 1, y, z - 1)], cellV[Idx(x - 1, y, z)], cellV[Idx(x, y, z)], cellV[Idx(x, y, z - 1)], tris);
                        if (z < nz - 1) Quad(v0, val[Idx(x, y, z + 1)], cellV[Idx(x - 1, y - 1, z)], cellV[Idx(x, y - 1, z)], cellV[Idx(x, y, z)], cellV[Idx(x - 1, y, z)], tris);
                    }
            // onto the surface, and the normals from the field
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i];
                for (int it = 0; it < 3; it++)
                {
                    float d = L.Field(p); var g = Grad(L, p, h * 0.25f);
                    float gg = g.sqrMagnitude; if (gg < 1e-10f) break;
                    var step = g * (d / gg);
                    if (step.magnitude > h) step = step.normalized * h;
                    p -= step;
                }
                verts[i] = p;
                norms.Add(Grad(L, p, h * 0.5f).normalized);
            }
            // every triangle faces outward
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = verts[tris[i]]; var b = verts[tris[i + 1]]; var c = verts[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                var g = norms[tris[i]] + norms[tris[i + 1]] + norms[tris[i + 2]];
                if (g.sqrMagnitude < 1e-8f) g = Grad(L, (a + b + c) / 3f, h);
                if (Vector3.Dot(n, g) < 0) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; }
            }
            // a vertex the field gave no direction takes the average of its faces
            var acc = new Vector3[verts.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = verts[tris[i]]; var b = verts[tris[i + 1]]; var c = verts[tris[i + 2]];
                var n = Vector3.Cross(b - a, c - a);
                acc[tris[i]] += n; acc[tris[i + 1]] += n; acc[tris[i + 2]] += n;
            }
            for (int i = 0; i < verts.Count; i++)
                if (norms[i].sqrMagnitude < 0.5f || Vector3.Dot(norms[i], acc[i]) < 0) norms[i] = acc[i].sqrMagnitude > 0 ? acc[i].normalized : Vector3.up;
        }

        static void Quad(float a, float b, int c0, int c1, int c2, int c3, List<int> tris)
        {
            if ((a < 0) == (b < 0)) return;
            if (c0 < 0 || c1 < 0 || c2 < 0 || c3 < 0) return;
            tris.Add(c0); tris.Add(c1); tris.Add(c2);
            tris.Add(c0); tris.Add(c2); tris.Add(c3);
        }

        public static Vector3 Grad(Layer L, Vector3 p, float e)
        {
            return new Vector3(
                L.Field(p + new Vector3(e, 0, 0)) - L.Field(p - new Vector3(e, 0, 0)),
                L.Field(p + new Vector3(0, e, 0)) - L.Field(p - new Vector3(0, e, 0)),
                L.Field(p + new Vector3(0, 0, e)) - L.Field(p - new Vector3(0, 0, e)));
        }
    }
}
