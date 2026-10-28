// Titles: a short name under a player's name, each with its own design (a pill with a gradient, a border, an
// icon and maybe a moving effect). The catalog comes from the server (the dashboard can change it at any time);
// Badge() draws one anywhere: the player card, a seat at the table, the leaderboard, the home screen.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Titles
{
    public class Def
    {
        public string id, ar, en, icon, effect, monthly;
        public Color bg0, bg1, fg, border;
        public string Name => Loc.En ? en : ar;
    }

    static readonly Dictionary<string, Def> all = new Dictionary<string, Def>();
    public static readonly List<Def> List = new List<Def>();
    /// The catalog arrived or changed: anything showing titles redraws.
    public static event Action Changed;
    /// Goes up whenever the catalog or the language changes (what's drawn may be stale).
    public static int Version { get; private set; }
    static Titles() { Loc.Changed += () => { Version++; Changed?.Invoke(); }; }

    public static void Set(List<object> list)
    {
        all.Clear(); List.Clear();
        if (list != null)
            foreach (var o in list)
            {
                if (!(o is Dictionary<string, object> d)) continue;
                var s = d.Obj("style"); var bg = s?.Arr("bg");
                var t = new Def
                {
                    id = d.Str("id"), ar = d.Str("ar"), en = d.Str("en"), monthly = d.Str("monthly"),
                    icon = s?.Str("icon") ?? "none", effect = s?.Str("effect") ?? "none",
                    bg0 = Hex(bg != null && bg.Count > 0 ? bg[0] as string : null, new Color(0.3f, 0.2f, 0.1f)),
                    bg1 = Hex(bg != null && bg.Count > 1 ? bg[1] as string : null, new Color(0.15f, 0.1f, 0.05f)),
                    fg = Hex(s?.Str("fg"), Color.white), border = Hex(s?.Str("border"), Ui.Gold),
                };
                if (string.IsNullOrEmpty(t.id)) continue;
                all[t.id] = t; List.Add(t);
            }
        Version++;
        Changed?.Invoke();
    }

    public static Def Get(string id) => !string.IsNullOrEmpty(id) && all.TryGetValue(id, out var d) ? d : null;

    static Color Hex(string h, Color d) => !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out var c) ? c : d;

    /// Draws a title as a pill, centred on its anchored position (size: the text size; the pill is about
    /// twice as tall). Returns null if the title isn't known (yet).
    public static RectTransform Badge(Transform parent, string id, int size = 12)
    {
        var t = Get(id); if (t == null) return null;
        float h = Mathf.Round(size * 1.85f), pad = size * 0.8f, iconW = t.icon == "none" ? 0 : size * 1.25f, gap = iconW > 0 ? size * 0.35f : 0;

        var root = Ui.Rect("Title", parent);
        // the text first, to measure it
        var label = Ui.Label(root, "", size, t.fg, TextAnchor.MiddleCenter, Ui.Bold);
        label.Set(t.Name);
        float tw = Mathf.Ceil(label.preferredWidth);
        float w = pad * 2 + iconW + gap + tw;
        root.sizeDelta = new Vector2(w, h);

        // a soft glow behind it (the glow effect breathes it)
        Image glow = null;
        if (t.effect == "glow")
        {
            glow = Ui.Box(root, "Glow", new Color(t.border.r, t.border.g, t.border.b, 0.3f));
            glow.rectTransform.anchorMin = Vector2.zero; glow.rectTransform.anchorMax = Vector2.one;
            glow.rectTransform.offsetMin = new Vector2(-4, -4); glow.rectTransform.offsetMax = new Vector2(4, 4);
            glow.raycastTarget = false;
        }
        // the border, then the gradient inside it
        var edge = Ui.Box(root, "Edge", t.border); edge.raycastTarget = false;
        edge.rectTransform.anchorMin = Vector2.zero; edge.rectTransform.anchorMax = Vector2.one; edge.rectTransform.offsetMin = edge.rectTransform.offsetMax = Vector2.zero;
        var fill = Ui.Box(edge.transform, "Fill", Color.white); fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
        float b = Mathf.Max(1f, size / 9f);
        fill.rectTransform.offsetMin = new Vector2(b, b); fill.rectTransform.offsetMax = new Vector2(-b, -b);
        var grad = fill.gameObject.AddComponent<UiGradient>(); grad.a = t.bg0; grad.b = t.bg1;

        // the shine: a light that sweeps across, clipped to the pill
        RectTransform shine = null;
        if (t.effect == "shine")
        {
            fill.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var s = Ui.Picture(fill.transform, "Shine", ShineTex);
            shine = s.rectTransform;
            shine.anchorMin = shine.anchorMax = new Vector2(0.5f, 0.5f);
            shine.sizeDelta = new Vector2(h * 1.4f, h * 2.2f);
            shine.localRotation = Quaternion.Euler(0, 0, -20);
        }

        // the icon and the text, in reading order
        label.transform.SetParent(root, false);
        label.transform.SetAsLastSibling();
        float x0 = -w / 2 + pad;
        bool rtl = !Loc.En;
        if (iconW > 0)
        {
            var ic = Ui.Picture(root, "Icon", Menus.Icon("t_" + t.icon)); ic.color = t.fg;
            float ix = rtl ? w / 2 - pad - iconW / 2 : x0 + iconW / 2;
            ic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(iconW, iconW), new Vector2(ix, 0));
        }
        float tx = rtl ? -w / 2 + pad + tw / 2 : x0 + iconW + gap + tw / 2;
        label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(tw + 4, h), new Vector2(tx, 0));

        if (t.effect != "none")
        {
            var fx = root.gameObject.AddComponent<TitleFx>();
            fx.kind = t.effect; fx.shine = shine; fx.glow = glow; fx.width = w;
        }
        return root;
    }

    static Texture2D shineTex;
    static Texture2D ShineTex
    {
        get
        {
            if (shineTex != null) return shineTex;
            const int W = 64;
            shineTex = new Texture2D(W, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < W; x++) { float d = Mathf.Abs(x - (W - 1) / 2f) / (W / 2f); shineTex.SetPixel(x, 0, new Color(1, 1, 1, 0.55f * Mathf.Clamp01(1 - d * d))); }
            shineTex.Apply();
            return shineTex;
        }
    }
}

/// A left-to-right colour gradient on any UI graphic (the title's background).
public class UiGradient : BaseMeshEffect
{
    public Color a = Color.white, b = Color.white;
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        var r = graphic.rectTransform.rect;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            float t = r.width > 0 ? Mathf.InverseLerp(r.xMin, r.xMax, v.position.x) : 0;
            if (!Loc.En) t = 1 - t;   // the gradient follows the reading direction
            v.color = Color.Lerp(a, b, t);
            vh.SetUIVertex(v, i);
        }
    }
}

/// The title's moving effect: shine (a light sweeps across), glow (it breathes), pulse (it beats gently).
public class TitleFx : MonoBehaviour
{
    public string kind;
    public RectTransform shine;
    public Image glow;
    public float width;
    float t0;

    void OnEnable() { t0 = Time.unscaledTime + UnityEngine.Random.value; }

    void Update()
    {
        float t = Time.unscaledTime - t0;
        switch (kind)
        {
            case "shine":
                if (shine == null) return;
                float p = (t % 2.6f) / 1.6f;   // sweeps for 1.6 s, then rests
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.75f, width * 0.75f, Mathf.Clamp01(p)), 0);
                shine.gameObject.SetActive(p <= 1);
                break;
            case "glow":
                if (glow == null) return;
                var c = glow.color; c.a = 0.12f + 0.38f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI));
                glow.color = c;
                break;
            case "pulse":
                float s = 1 + 0.05f * Mathf.Sin(t * Mathf.PI * 1.25f);
                transform.localScale = new Vector3(s, s, 1);
                break;
        }
    }
}
