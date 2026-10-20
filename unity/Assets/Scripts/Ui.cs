// Building blocks for the game's screens, made in code (no hand-made prefabs): panels with rounded
// corners, text in the café's fonts (Arabic shaped), buttons and images.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Ui
{
    public static readonly Color Gold = new Color32(0xf3, 0xcd, 0x62, 0xff);
    public static readonly Color GoldLight = new Color32(0xff, 0xe9, 0xb0, 0xff);
    public static readonly Color Ink = new Color32(0x1b, 0x12, 0x0c, 0xff);
    public static readonly Color Panel = new Color32(0x1b, 0x12, 0x0c, 0xd9);
    public static readonly Color Bordeaux = new Color32(0x4a, 0x0b, 0x16, 0xff);

    static Font body, bold, display;
    public static Font Body => body ??= Load("Fonts/PlexArabic");
    public static Font Bold => bold ??= Load("Fonts/PlexArabicBold");
    public static Font Display => display ??= Load("Fonts/ReemKufi");
    static Font Load(string path)
    {
        var f = Resources.Load<Font>(path);
        if (f == null) { Debug.LogWarning("[ui] missing font " + path); f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        return f;
    }

    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(844, 390);   // a phone held sideways
        s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        s.matchWidthOrHeight = 1f;
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return c;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// Anchors a rect: anchor min/max in 0..1, then size and offset from the anchor point.
    public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    public static RectTransform Fill(this RectTransform r, float inset = 0)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        return r;
    }

    static Sprite rounded;
    /// A white rounded rectangle, sliced so it stretches to any size.
    public static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int S = 64, R = 24;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(R - x - 0.5f, x + 0.5f - (S - R))), dy = Mathf.Max(0, Mathf.Max(R - y - 0.5f, y + 0.5f - (S - R)));
                    float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            rounded = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
            return rounded;
        }
    }

    public static Image Box(Transform parent, string name, Color color, bool round = true)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        if (round) { img.sprite = Rounded; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 2f; }
        return img;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, Font font = null)
    {
        var r = Rect("Text", parent);
        var t = r.gameObject.AddComponent<Text>();
        t.font = font ?? Body;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = Arabic.Fix(Loc.T(text));
        Loc.Track(t, text);   // changes language with the rest
        return t;
    }

    public static void Set(this Text t, string text) { Loc.Untrack(t); t.text = Arabic.Fix(text); }

    public static RawImage Picture(Transform parent, string name, Texture tex)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<RawImage>();
        img.texture = tex;
        img.raycastTarget = false;
        return img;
    }

    public static UnityEngine.UI.Button Button(Transform parent, string label, Color bg, Color fg, int size, Action onClick)
    {
        var img = Box(parent, "Button", bg);
        var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
        var colors = b.colors; colors.pressedColor = new Color(0.8f, 0.8f, 0.8f); colors.fadeDuration = 0.08f; b.colors = colors;
        b.onClick.AddListener(() => onClick?.Invoke());
        var t = Label(img.transform, label, size, fg, TextAnchor.MiddleCenter, Bold);
        t.rectTransform.Fill();
        return b;
    }

    /// A soft round glow (for the splash), white at the centre fading out.
    public static Texture2D Glow(int size = 128)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    /// The splash background: bordeaux, brighter in the middle.
    public static Texture2D Radial(Color inner, Color mid, Color outer, int size = 256)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.58f;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx * 0.7f + dy * dy) * 2f);
                px[y * size + x] = d < 0.45f ? Color.Lerp(inner, mid, d / 0.45f) : Color.Lerp(mid, outer, (d - 0.45f) / 0.55f);
            }
        t.SetPixels(px); t.Apply();
        return t;
    }

    /// Light rays from the centre (rotating behind the splash emblem).
    public static Texture2D Rays(int size = 512, int count = 16)
    {
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx) / (Mathf.PI * 2) * count;
                float ray = (ang - Mathf.Floor(ang)) < 0.27f ? 1f : 0f;
                float fade = Mathf.Clamp01(1 - r) * Mathf.Clamp01(r * 6);
                px[y * size + x] = new Color32(255, 210, 122, (byte)(ray * fade * 26));
            }
        t.SetPixels32(px); t.Apply();
        return t;
    }

    public static float Ease(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
    /// Overshoots a little before settling (a "pop").
    public static float Back(float t) { t = Mathf.Clamp01(t); const float c = 1.9f; return 1 + (c + 1) * Mathf.Pow(t - 1, 3) + c * Mathf.Pow(t - 1, 2); }
}
