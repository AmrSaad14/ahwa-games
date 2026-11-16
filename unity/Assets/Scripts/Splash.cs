// The opening splash for Board Battles, the same as the web page's loading screen
// (WebGLTemplates/BoardBattles/index.html): the crest floating in a glow over turning rays, "The Egyptian café,
// at your table", the five games, a gold bar with the percentage, and a tip. It opens onto the café when it's ready.
// In the web version the page's own loading screen stays up instead (one intro, not two): the game sends it its
// progress and tells it when the café is ready (Plugins/WebGL/BBSocket.jslib).
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class Splash : MonoBehaviour
{
    const float MinTime = 1.6f;
    static readonly Vector2 LogoSize = new Vector2(133, 155);   // the crest (Resources/UI/Logo, 600 × 700)
    static readonly Color Cream = new Color32(0xf6, 0xea, 0xd0, 0xff);

    static readonly string[] Games = { "دومينو", "طاولة", "شطرنج", "لودو", "استيميشن" };
    static readonly string[] Tips =
    {
        "«العب دلوقتي» بيلاقيلك ترابيزة في ثواني.",
        "خش بجوجل عشان كوينزك ومستواك يفضلوا معاك على أي جهاز.",
        "العب مع صحابك: افتح أوضة وابعتلهم الكود.",
        "المهام اليومية بتديك كوينز زيادة كل يوم.",
        "اطلع في الترتيب واكسب لقب لاعب الشهر في محافظتك.",
        "لفّ العجلة واكسب جايزة مجانية.",
    };

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void BBWeb_Progress(float p);
    [DllImport("__Internal")] static extern void BBWeb_Ready();
#endif

    Canvas canvas;
    Camera splashCam;
    CanvasGroup group, columnGroup;
    RectTransform rays, glow, logo, barFill;
    RawImage raysImg, glowImg;
    Text pct, what, tip;
    float t0, readyAt = -1, openAt = -1, shown;
    bool shots, web, setting;
    int shot, tipAt;

    public bool Opened => openAt >= 0;
    public void Ready() { if (readyAt < 0) readyAt = Time.time; }
    /// How much of the loading is done (0..1): the bar follows it.
    float progress = -1;
    public void Progress(float p) { progress = Mathf.Max(progress, Mathf.Clamp01(p)); }

    public void Show()
    {
        shots = App.Recording;
#if UNITY_WEBGL && !UNITY_EDITOR
        web = true;
        t0 = Time.time;
        return;   // the page's loading screen is the splash
#endif
        // its own camera: until the café's camera exists nothing else draws, and some phones show black
        var cg = new GameObject("SplashCam", typeof(Camera));
        cg.transform.SetParent(transform, false);
        splashCam = cg.GetComponent<Camera>();
        splashCam.clearFlags = CameraClearFlags.SolidColor; splashCam.backgroundColor = new Color32(0x1c, 0x06, 0x09, 0xff);
        splashCam.cullingMask = 0; splashCam.depth = -100;
        canvas = Ui.MakeCanvas("Splash", 100);
        var root = Ui.Rect("Splash", canvas.transform).Fill();
        group = root.gameObject.AddComponent<CanvasGroup>();
        Ui.Picture(root, "Background", Ui.Radial(new Color32(0x7a, 0x14, 0x24, 0xff), new Color32(0x4a, 0x0b, 0x16, 0xff), new Color32(0x1c, 0x06, 0x09, 0xff))).rectTransform.Fill();

        raysImg = Ui.Picture(root, "Rays", Ui.Rays());
        rays = raysImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1400, 1400), new Vector2(0, 60));
        glowImg = Ui.Picture(root, "Glow", Ui.Glow());
        glowImg.color = new Color(1f, 0.81f, 0.43f, 0f);
        glow = glowImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300, 300), new Vector2(0, 60));

        // the column, centred: crest, line, games, bar, tip (sizes as on the web page held sideways on a phone)
        var col = Ui.Rect("Column", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(560, 280), Vector2.zero);
        columnGroup = col.gameObject.AddComponent<CanvasGroup>(); columnGroup.alpha = 0;
        bool en = Loc.En;
        Font font = en ? Ui.Latin : Ui.Body, bold = en ? Ui.LatinBold : Ui.Bold;

        var logoImg = Ui.Picture(col, "Logo", Resources.Load<Texture2D>("UI/Logo"));
        logo = logoImg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), LogoSize, new Vector2(0, 62));

        var tag = Ui.Label(col, "القهوة المصرية على ترابيزتك", 14, Cream, TextAnchor.MiddleCenter, bold);
        tag.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 20), new Vector2(0, -36));

        // the five games, as outlined pills in one row
        var row = Ui.Rect("Games", col).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(540, 22), new Vector2(0, -64));
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 7; h.childAlignment = TextAnchor.MiddleCenter; h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
        foreach (var g in Games)
        {
            var pill = Ui.Rect(g, row);
            var ring = pill.gameObject.AddComponent<Image>();
            ring.sprite = Ui.RoundedRing; ring.type = Image.Type.Sliced; ring.pixelsPerUnitMultiplier = 2.6f;
            ring.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.55f); ring.raycastTarget = false;
            var label = Ui.Label(pill, g, 10, Ui.Gold, TextAnchor.MiddleCenter, bold);
            Object.Destroy(label.GetComponent<FitText>());
            label.rectTransform.Fill();
            var le = pill.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = label.preferredWidth + 22; le.preferredHeight = 22;
        }

        // the bar, with what's happening and the percentage under it
        var bar = Ui.Box(col, "Bar", new Color(1, 1, 1, 0.12f));
        bar.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 6), new Vector2(0, -90));
        var fill = Ui.Box(bar.transform, "Fill", Ui.Gold);
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0.03f, 1); barFill.offsetMin = barFill.offsetMax = Vector2.zero;
        what = Ui.Label(col, "بنجهّز القهوة…", 10, new Color(Cream.r, Cream.g, Cream.b, 0.75f), en ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, font);
        what.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(200, 14), new Vector2(en ? -30 : 30, -103));
        pct = Ui.Label(col, "0%", 10, new Color(Cream.r, Cream.g, Cream.b, 0.75f), en ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, font);
        pct.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(60, 14), new Vector2(en ? 100 : -100, -103));

        tip = Ui.Label(col, Tips[Random.Range(0, Tips.Length)], 11, new Color(Cream.r, Cream.g, Cream.b, 0.85f), TextAnchor.MiddleCenter, bold);
        tip.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 30), new Vector2(0, -126));
        tipAt = 0;

        t0 = Time.time;
        Update();
    }

    static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

    void Update()
    {
        float t = Time.time - t0;
        if (web) { WebUpdate(t); return; }
        if (canvas == null) return;

        // rays turn slowly; the glow breathes behind the crest; the crest floats (as on the web page)
        raysImg.color = new Color(1, 1, 1, Ui.Ease(Seg(t, 0f, 0.8f)) * 0.9f);
        rays.localRotation = Quaternion.Euler(0, 0, -t * 4f);
        glowImg.color = new Color(1f, 0.81f, 0.43f, 0.32f + 0.08f * Mathf.Sin(t * 1.6f));
        float a = Ui.Ease(Seg(t, 0.05f, 0.45f));
        columnGroup.alpha = a;
        logo.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, Ui.Back(Seg(t, 0.05f, 0.5f)));
        float f = Mathf.Sin(t * Mathf.PI * 2 / 3.2f);
        logo.anchoredPosition = new Vector2(0, 62 + 4f * Mathf.Max(0, f));
        logo.localRotation = Quaternion.Euler(0, 0, -0.6f * Mathf.Max(0, f));

        // the bar: follows the loading, creeping along meanwhile; full when the café is ready
        float creep = 0.9f * (1 - Mathf.Exp(-t * 0.5f));
        float target = readyAt < 0 ? Mathf.Max(creep * 0.5f, progress) : 1f;
        float p = Mathf.Max(0.03f, Mathf.Lerp(barFill.anchorMax.x, target, readyAt < 0 ? 0.12f : 0.25f));
        barFill.anchorMax = new Vector2(p, 1);
        int shownPct = Mathf.RoundToInt(p * 100);
        if (shownPct != (int)shown) { shown = shownPct; pct.Set(shownPct + "%"); }
        if (!setting && p >= 0.9f) { setting = true; what.Set(Loc.T("بنرصّ الترابيزات…")); }

        // a new tip every few seconds
        int ti = (int)(t / 4.2f);
        if (ti != tipAt) { tipAt = ti; if (ti > 0) tip.Set(Loc.T(Tips[(Random.Range(0, Tips.Length))])); }

        if (shots && shot == 0 && t > 0.6f) { shot++; App.Still("1-splash-clash.png"); }
        if (shots && shot == 1 && t > 1.4f) { shot++; App.Still("2-splash.png"); }

        // open onto the café
        if (openAt < 0 && readyAt >= 0 && t >= MinTime && (!shots || shot >= 2)) openAt = Time.time + 0.3f;
        if (openAt >= 0 && Time.time > openAt)
        {
            float o = Mathf.Clamp01((Time.time - openAt) / 0.6f);
            group.alpha = 1 - Ui.Ease(o);
            group.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, Ui.Ease(o));
            group.blocksRaycasts = false;
            if (o >= 1) { Destroy(canvas.gameObject); canvas = null; if (splashCam != null) Destroy(splashCam.gameObject); }
        }
        if (t > 90f && openAt < 0) openAt = Time.time;   // never stuck on the splash
    }

    /// The web version: the page shows the loading; the game only says how far it is and when it's ready.
    void WebUpdate(float t)
    {
        if (openAt >= 0) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { BBWeb_Progress(readyAt >= 0 ? 1f : Mathf.Max(0, progress)); } catch { }
#endif
        if ((readyAt >= 0 && t >= 0.2f) || t > 90f)
        {
            openAt = Time.time;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { BBWeb_Ready(); } catch { }
#endif
        }
    }
}
