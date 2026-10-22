// The opening splash for Board Battles: the gold-rimmed crest appears in a glow, a domino tile and a die fly in
// from the sides and clash with a spark, the crown drops onto the crest, the BOARD BATTLES ribbon unfurls under
// it, a loading bar fills, then it opens onto the café. The pictures are layers of the logo
// (design/logo, all drawn in the same 600 × 700 frame so they stack exactly).
using UnityEngine;
using UnityEngine.UI;

public class Splash : MonoBehaviour
{
    const float MinTime = 2.9f;
    static readonly Vector2 Frame = new Vector2(240, 280);   // the logo's frame on screen

    Canvas canvas;
    Camera splashCam;
    CanvasGroup group;
    RectTransform stage, rays, glow, barFill;
    RawImage raysImg, glowImg, shield, domino, die, spark, crown, ribbon;
    Text sub;
    CanvasGroup barGroup;
    float t0, readyAt = -1, openAt = -1;
    bool shots;
    int shot;

    public bool Opened => openAt >= 0;
    public void Ready() { if (readyAt < 0) readyAt = Time.time; }

    public void Show()
    {
        shots = App.Recording;
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

        raysImg = Ui.Picture(root, "Rays", Ui.Rays(256));   // small: the splash must be up at once
        rays = raysImg.rectTransform.Place(new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(1400, 1400), Vector2.zero);
        glowImg = Ui.Picture(root, "Glow", Ui.Glow());
        glowImg.color = new Color(1f, 0.81f, 0.43f, 0f);
        glow = glowImg.rectTransform.Place(new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(420, 420), Vector2.zero);

        stage = Ui.Rect("Stage", root).Place(new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.5f), Frame, Vector2.zero);
        shield = Layer("Shield", new Vector2(0.5f, 0.5f));
        domino = Layer("DominoLayer", new Vector2(0.4f, 0.46f));
        die = Layer("DieLayer", new Vector2(0.62f, 0.42f));
        spark = Layer("Spark2", new Vector2(0.5f, 1 - 380f / 700));
        crown = Layer("Crown", new Vector2(0.5f, 1 - 100f / 700));
        ribbon = Layer("Ribbon", new Vector2(0.5f, 1 - 590f / 700));

        sub = Ui.Label(root, "Dominoes  ·  Backgammon  ·  Chess  ·  Ludo", 16, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Body);
        sub.rectTransform.Place(new Vector2(0.5f, 0.54f), new Vector2(0.5f, 1), new Vector2(520, 30), new Vector2(0, -Frame.y / 2 - 4));
        sub.color = new Color(1, 0.91f, 0.69f, 0);

        var bar = Ui.Box(root, "Bar", new Color(1, 0.91f, 0.66f, 0.14f));
        bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 4), new Vector2(0, 22));
        barGroup = bar.gameObject.AddComponent<CanvasGroup>(); barGroup.alpha = 0;
        var fill = Ui.Box(bar.transform, "Fill", Ui.Gold);
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0, 1); barFill.offsetMin = barFill.offsetMax = Vector2.zero;

        t0 = Time.time;
        Update();
    }

    /// One layer of the logo, filling the frame, turning and scaling about `pivot` (a point in the frame, 0..1).
    RawImage Layer(string name, Vector2 pivot)
    {
        var img = Ui.Picture(stage, name, Resources.Load<Texture2D>("UI/" + name));
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Frame;
        r.pivot = pivot;
        r.anchoredPosition = Home(pivot);
        img.color = new Color(1, 1, 1, 0);
        return img;
    }
    static Vector2 Home(Vector2 pivot) => new Vector2((pivot.x - 0.5f) * Frame.x, (pivot.y - 0.5f) * Frame.y);

    static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

    void Update()
    {
        if (canvas == null) return;
        float t = Time.time - t0;

        // rays turn slowly; the glow swells behind the crest
        raysImg.color = new Color(1, 1, 1, Ui.Ease(Seg(t, 0.2f, 1.4f)));
        rays.localRotation = Quaternion.Euler(0, 0, -t * 9f);
        float g = Seg(t, 0.3f, 2.9f);
        float ga = g < 0.25f ? g / 0.25f : Mathf.Lerp(1f, 0.7f, (g - 0.25f) / 0.75f);
        glowImg.color = new Color(1f, 0.81f, 0.43f, ga * 0.5f);
        glow.localScale = Vector3.one * (g < 0.25f ? Mathf.Lerp(0.4f, 1.15f, g / 0.25f) : Mathf.Lerp(1.15f, 1f, (g - 0.25f) / 0.75f));

        // the crest
        float c = Seg(t, 0.05f, 0.55f);
        shield.color = new Color(1, 1, 1, Mathf.Clamp01(c * 2));
        shield.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, Ui.Back(c));

        // the domino from the left, the die from the right: they meet in the middle
        float s = Seg(t, 0.35f, 0.85f), sb = Ui.Back(s);
        Fly(domino, -1, sb, Mathf.Clamp01(s * 2.5f), 55f);
        Fly(die, 1, sb, Mathf.Clamp01(s * 2.5f), -65f);

        // the spark where they meet, and a little shake
        float k = Seg(t, 0.78f, 1.3f);
        float ka = k <= 0 || k >= 1 ? 0 : k < 0.3f ? k / 0.3f : 1 - (k - 0.3f) / 0.7f;
        spark.color = new Color(1, 1, 1, ka);
        spark.rectTransform.localScale = Vector3.one * (k < 0.3f ? Mathf.Lerp(0.2f, 1.2f, k / 0.3f) : Mathf.Lerp(1.2f, 1.5f, (k - 0.3f) / 0.7f));
        spark.rectTransform.localRotation = Quaternion.Euler(0, 0, -40f * k);
        float sh = Seg(t, 0.8f, 1.1f);
        stage.anchoredPosition = sh > 0 && sh < 1 ? new Vector2(Mathf.Sin(sh * 40f) * 5f * (1 - sh), Mathf.Cos(sh * 33f) * 3f * (1 - sh)) : Vector2.zero;

        // the crown drops onto the crest
        float cr = Seg(t, 1.0f, 1.55f), cb = Ui.Back(cr);
        crown.color = new Color(1, 1, 1, Mathf.Clamp01(cr * 3));
        crown.rectTransform.anchoredPosition = Home(crown.rectTransform.pivot) + new Vector2(0, 70f * (1 - cb));

        // the ribbon with the name unfurls from the middle
        float rb = Seg(t, 1.4f, 1.95f);
        ribbon.color = new Color(1, 1, 1, Mathf.Clamp01(rb * 3));
        ribbon.rectTransform.localScale = new Vector3(Mathf.LerpUnclamped(0.05f, 1f, Ui.Back(rb)), Mathf.Lerp(0.6f, 1f, Ui.Ease(rb)), 1);

        float ar = Ui.Ease(Seg(t, 1.9f, 2.4f));
        sub.color = new Color(1, 0.91f, 0.69f, ar * 0.9f);

        // loading bar: creeps to 90% while the café loads, fills when it's ready
        barGroup.alpha = Seg(t, 0.9f, 1.4f);
        float p = readyAt < 0 ? 0.9f * (1 - Mathf.Exp(-t * 0.6f)) : Mathf.Lerp(barFill.anchorMax.x, 1f, 0.2f);
        barFill.anchorMax = new Vector2(p, 1);

        if (shots && shot == 0 && t > 0.84f) { shot++; App.Still("1-splash-clash.png"); }
        if (shots && shot == 1 && t > 2.5f) { shot++; App.Still("2-splash.png"); }

        // open onto the café
        if (openAt < 0 && readyAt >= 0 && t >= MinTime && (!shots || shot >= 2)) openAt = Time.time + 0.25f;
        if (openAt >= 0 && Time.time > openAt)
        {
            float o = Mathf.Clamp01((Time.time - openAt) / 0.65f);
            group.alpha = 1 - Ui.Ease(o);
            group.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, Ui.Ease(o));
            group.blocksRaycasts = false;
            if (o >= 1) { Destroy(canvas.gameObject); canvas = null; if (splashCam != null) Destroy(splashCam.gameObject); }
        }
        if (t > 40f && openAt < 0) openAt = Time.time;   // never stuck on the splash
    }

    static void Fly(RawImage img, int dir, float p, float a, float spin)
    {
        img.color = new Color(1, 1, 1, a);
        var r = img.rectTransform;
        r.anchoredPosition = Home(r.pivot) + new Vector2(dir * 0.75f * Frame.x * (1 - p), -0.1f * Frame.y * (1 - p));
        r.localRotation = Quaternion.Euler(0, 0, spin * (1 - p));
    }
}
