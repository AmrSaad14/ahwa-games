// The player's look at the game table, from the shop and the season pass:
//  - a hat on their character (a tarboosh in a colour of their choice; the men wear it, over their own hat),
//  - the cloth on their game table (seen by them),
//  - the drink by their place (seen by everyone at the table).
using System.Collections.Generic;
using UnityEngine;

public static class Look
{
    /// The cloth you chose for your table (set from your account).
    public static string MyFelt;

    /* ---------- hats ---------- */

    static readonly Dictionary<string, Color> HatColors = new Dictionary<string, Color>
    {
        ["hat_fez_red"] = new Color(0.62f, 0.05f, 0.08f), ["hat_fez_black"] = new Color(0.07f, 0.07f, 0.08f),
        ["hat_fez_green"] = new Color(0.08f, 0.36f, 0.18f), ["hat_fez_navy"] = new Color(0.09f, 0.13f, 0.34f),
        ["hat_fez_white"] = new Color(0.93f, 0.91f, 0.86f), ["hat_fez_gold"] = new Color(0.86f, 0.64f, 0.18f),
    };
    public static Color HatColor(string id) => id != null && HatColors.TryGetValue(id, out var c) ? c : new Color(0.62f, 0.05f, 0.08f);
    static bool Man(int ch) => ch == 0 || ch == 1 || ch == 2 || ch == 5;

    static Transform modelFez; static Transform modelHead;
    /// Am Fathi's tarboosh (with its tassel), to copy onto others.
    static void FindModel()
    {
        if (modelFez != null) return;
        var prefab = Resources.Load<GameObject>("People/AmFathi");
        if (prefab == null) return;
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Tarboosh") && t.GetComponent<Renderer>() != null) { modelFez = t; break; }
        if (modelFez != null) modelHead = modelFez.parent;
    }

    static void Tint(Renderer r, Color c, float metal = 0)
    {
        foreach (var m in r.materials)
        {
            if (m.HasProperty("baseColorFactor")) m.SetColor("baseColorFactor", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (metal > 0) { if (m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", metal); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal); }
        }
    }

    /// Puts the player's hat on their character at the table (`hat_classic`: the character as made).
    public static void Hat(Person p, int ch, string hat)
    {
        if (p == null || string.IsNullOrEmpty(hat) || hat == "hat_classic" || !Man(ch) || p.Head == null) return;
        var color = HatColor(hat);
        float metal = hat == "hat_fez_gold" ? 0.6f : 0;
        // their own hat: Am Fathi's tarboosh changes colour; Hajj Mahmoud's emma comes off for the tarboosh
        Renderer own = null;
        foreach (var r in p.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.StartsWith("Tarboosh")) own = r;
            else if (r.name.StartsWith("Emma")) r.enabled = false;
        }
        if (own != null) { Tint(own, color, metal); return; }
        FindModel();
        if (modelFez == null) return;
        var fez = Object.Instantiate(modelFez.gameObject, p.Head, false);
        fez.name = "TarbooshWorn";
        // the same place on the head as on Am Fathi's (the model's pose relative to its head bone)
        fez.transform.localPosition = modelFez.localPosition;
        fez.transform.localRotation = modelFez.localRotation;
        fez.transform.localScale = modelFez.localScale;
        foreach (var r in fez.GetComponentsInChildren<Renderer>(true)) if (r.name.StartsWith("Tarboosh") || r.gameObject == fez) Tint(r, color, metal);
        // the tassel comes along when it was a sibling of the tarboosh
        if (modelHead != null)
            foreach (Transform sib in modelHead)
                if (sib.name.StartsWith("Tassel") && sib.GetComponent<Renderer>() != null)
                {
                    var t = Object.Instantiate(sib.gameObject, p.Head, false);
                    t.transform.localPosition = sib.localPosition; t.transform.localRotation = sib.localRotation; t.transform.localScale = sib.localScale;
                }
    }

    /* ---------- the table cloth ---------- */

    static readonly Dictionary<string, Texture2D> felts = new Dictionary<string, Texture2D>();
    static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; }

    /// The cloth's picture: plain baize with a little texture, a royal one with a gold border, or a kilim.
    public static Texture2D FeltTexture(string id)
    {
        if (felts.TryGetValue(id, out var t)) return t;
        const int S = 256;
        t = new Texture2D(S, S, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        var px = new Color[S * S];
        Color baseC = id == "felt_green" ? new Color(0.07f, 0.3f, 0.16f) : id == "felt_red" ? new Color(0.42f, 0.05f, 0.07f)
            : id == "felt_blue" ? new Color(0.07f, 0.16f, 0.36f) : id == "felt_royal" ? new Color(0.3f, 0.03f, 0.08f) : new Color(0.55f, 0.16f, 0.1f);
        var gold = new Color(0.86f, 0.66f, 0.26f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = (Hash(x, y) - 0.5f) * 0.06f + (Hash(x / 3, y / 3) - 0.5f) * 0.04f;
                var c = baseC * (1 + n);
                int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(S - 1 - x, S - 1 - y));   // distance to the edge
                if (id == "felt_royal")
                {
                    if (e >= 10 && e < 14 || e >= 18 && e < 20) c = gold * (0.9f + n);
                    // a diamond lattice, faint
                    if (e > 26 && ((x + y) % 32 == 0 || (x - y + 512) % 32 == 0)) c = Color.Lerp(c, gold, 0.25f);
                }
                else if (id == "felt_kilim")
                {
                    // bands of steps and diamonds, like a woven kilim
                    int band = (y / 32) % 4;
                    var pal = new[] { new Color(0.55f, 0.13f, 0.09f), new Color(0.12f, 0.2f, 0.36f), new Color(0.8f, 0.6f, 0.25f), new Color(0.92f, 0.86f, 0.74f) };
                    int cx = x % 32 - 16, cy = y % 32 - 16;
                    bool diamond = Mathf.Abs(cx) + Mathf.Abs(cy) < 11, inner = Mathf.Abs(cx) + Mathf.Abs(cy) < 5;
                    c = (inner ? pal[(band + 2) % 4] : diamond ? pal[(band + 1) % 4] : pal[band]) * (1 + n);
                    if (e < 8) c = pal[3] * (1 + n);
                }
                else if (id == "felt_marble")
                {
                    // white Carrara marble with grey veins, a gold edge
                    c = Skins.Marble(x * 2, y * 2, new[] { new Color(0.93f, 0.92f, 0.9f), new Color(0.55f, 0.55f, 0.58f), new Color(1f, 1f, 0.99f) }, 3.1f);
                    if (e >= 6 && e < 9) c = gold * (0.95f + n);
                }
                else if (e >= 6 && e < 8) c = Color.Lerp(c, gold, 0.55f);   // a thin gold piping
                px[y * S + x] = c;
            }
        t.SetPixels(px); t.Apply(true);
        felts[id] = t;
        return t;
    }

    /// A square of cloth on the table top (half side `half`), or none for the plain wooden table.
    public static GameObject Felt(string id, Transform parent, Vector3 center, float half, Quaternion rot)
    {
        if (string.IsNullOrEmpty(id) || id == "felt_wood") return null;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Felt";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.position = center + Vector3.up * 0.012f;
        go.transform.rotation = rot * Quaternion.Euler(90, 0, 0);
        go.transform.localScale = new Vector3(half * 2, half * 2, 1);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = Art.Mat(Color.white, 0.04f, 0, FeltTexture(id));
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /* ---------- the drink ---------- */

    static readonly Dictionary<string, Color> DrinkColors = new Dictionary<string, Color>
    {
        ["drink_tea"] = new Color(0.55f, 0.16f, 0.03f), ["drink_mint"] = new Color(0.55f, 0.5f, 0.12f), ["drink_coffee"] = new Color(0.16f, 0.08f, 0.04f),
        ["drink_karkade"] = new Color(0.5f, 0.02f, 0.08f), ["drink_sahlab"] = new Color(0.94f, 0.89f, 0.78f), ["drink_qasab"] = new Color(0.68f, 0.7f, 0.28f),
    };
    public static Color DrinkColor(string id) => id != null && DrinkColors.TryGetValue(id, out var c) ? c : DrinkColors["drink_tea"];

    /// A glass of the drink on its saucer, standing at `at` on the table.
    public static GameObject Drink(string id, Transform parent, Vector3 at, float scale)
    {
        var prefab = Resources.Load<GameObject>("Art/TeaGlass");
        if (prefab == null) return null;
        var g = Object.Instantiate(prefab, parent);
        g.name = "Drink";
        g.transform.position = at;
        g.transform.localScale = Vector3.one * scale;
        // the liquid is the mesh a little narrower than the glass: tint it
        Renderer liquid = null; float best = float.MaxValue;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var b = mf.sharedMesh.bounds.size;
            float score = Mathf.Abs(b.x - 0.37f) + Mathf.Abs(b.y - 0.45f);
            if (score < best) { best = score; liquid = r; }
        }
        if (liquid != null && best < 0.2f) { var c = DrinkColor(id); c.a = id == "drink_sahlab" ? 0.97f : 0.88f; Tint(liquid, c); }
        foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        return g;
    }
}
