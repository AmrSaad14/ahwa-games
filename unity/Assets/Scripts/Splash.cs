// The opening splash, as in the web game: two swords fly in and clash with a spark, the gold king rises
// in a glow, KINGS BATTLE writes itself in with معركة الملوك under it, a loading bar fills, then it opens
// onto the café.
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class Splash : MonoBehaviour
{
    const float MinTime = 2.9f;
    const string Title = "KINGS BATTLE";

    Canvas canvas;
    CanvasGroup group;
    RectTransform stage, swordL, swordR, spark, king, rays, glow, barFill;
    RawImage sparkImg, kingImg, swordLImg, swordRImg, raysImg, glowImg;
    Text title, arabic;
    CanvasGroup barGroup;
    float t0, readyAt = -1, openAt = -1;
    bool shots;
    int shot;

    public bool Opened => openAt >= 0;
    public void Ready() { if (readyAt < 0) readyAt = Time.realtimeSinceStartup; }

    public void Show()
    {
        shots = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-shots") >= 0;
        canvas = Ui.MakeCanvas("Splash", 100);
        var root = Ui.Rect("Splash", canvas.transform).Fill();
        group = root.gameObject.AddComponent<CanvasGroup>();
        Ui.Picture(root, "Background", Ui.Radial(new Color32(0x7a, 0x14, 0x24, 0xff), new Color32(0x4a, 0x0b, 0x16, 0xff), new Color32(0x1c, 0x06, 0x09, 0xff))).rectTransform.Fill();

        raysImg = Ui.Picture(root, "Rays", Ui.Rays());
        rays = raysImg.rectTransform.Place(new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f), new Vector2(1400, 1400), Vector2.zero);
        glowImg = Ui.Picture(root, "Glow", Ui.Glow());
        glowImg.color = new Color(1f, 0.81f, 0.43f, 0f);
        glow = glowImg.rectTransform.Place(new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), new Vector2(360, 360), Vector2.zero);

        stage = Ui.Rect("Stage", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400, 390), Vector2.zero);
        // the emblem: 724 × 900 layers, all the same frame
        var emblem = Ui.Rect("Emblem", stage).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, 186), new Vector2(0, -26));
        swordLImg = Layer(emblem, "SwordL", new Vector2(0.5f, 0.544f)); swordL = swordLImg.rectTransform;
        swordRImg = Layer(emblem, "SwordR", new Vector2(0.5f, 0.544f)); swordR = swordRImg.rectTransform;
        sparkImg = Layer(emblem, "Spark", new Vector2(0.5f, 0.639f)); spark = sparkImg.rectTransform;
        kingImg = Layer(emblem, "King", new Vector2(0.5f, 0.111f)); king = kingImg.rectTransform;

        title = Ui.Label(stage, "", 40, Ui.Gold, TextAnchor.MiddleCenter, Ui.Display);
        title.supportRichText = true;
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(400, 50), new Vector2(0, -214));
        var sh = title.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0.23f, 0.04f, 0.06f, 1); sh.effectDistance = new Vector2(0, -3);
        arabic = Ui.Label(stage, "معركة الملوك", 22, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Body);
        arabic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(400, 34), new Vector2(0, -262));
        arabic.color = new Color(1, 0.91f, 0.69f, 0);

        var bar = Ui.Box(root, "Bar", new Color(1, 0.91f, 0.66f, 0.14f));
        bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(240, 4), new Vector2(0, 26));
        barGroup = bar.gameObject.AddComponent<CanvasGroup>(); barGroup.alpha = 0;
        var fill = Ui.Box(bar.transform, "Fill", Ui.Gold);
        barFill = fill.rectTransform;
        barFill.anchorMin = Vector2.zero; barFill.anchorMax = new Vector2(0, 1); barFill.offsetMin = barFill.offsetMax = Vector2.zero;

        t0 = Time.realtimeSinceStartup;
        Update();
    }

    RawImage Layer(RectTransform parent, string name, Vector2 pivot)
    {
        var img = Ui.Picture(parent, name, Resources.Load<Texture2D>("UI/" + name));
        var r = img.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(150, 186);
        r.pivot = pivot;
        r.anchoredPosition = new Vector2((pivot.x - 0.5f) * 150, (pivot.y - 0.5f) * 186);
        img.color = new Color(1, 1, 1, 0);
        return img;
    }

    static float Seg(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

    void Update()
    {
        if (canvas == null) return;
        float t = Time.realtimeSinceStartup - t0;

        // background rays turn slowly; the glow swells behind the emblem
        raysImg.color = new Color(1, 1, 1, Ui.Ease(Seg(t, 0.2f, 1.4f)));
        rays.localRotation = Quaternion.Euler(0, 0, -t * 9f);
        float g = Seg(t, 0.55f, 2.95f);
        float ga = g < 0.25f ? g / 0.25f : Mathf.Lerp(1f, 0.75f, (g - 0.25f) / 0.75f);
        float gs = g < 0.25f ? Mathf.Lerp(0.4f, 1.15f, g / 0.25f) : Mathf.Lerp(1.15f, 1f, (g - 0.25f) / 0.75f);
        glowImg.color = new Color(1f, 0.81f, 0.43f, ga * 0.55f);
        glow.localScale = Vector3.one * gs;

        // the swords fly in and clash
        float s = Seg(t, 0.1f, 0.7f), sb = Ui.Back(s);
        float sa = Mathf.Clamp01(s / 0.4f);
        Sword(swordL, swordLImg, -1, sb, sa);
        Sword(swordR, swordRImg, 1, sb, sa);

        // the spark at the clash
        float k = Seg(t, 0.6f, 1.15f);
        float ka = k <= 0 || k >= 1 ? 0 : k < 0.3f ? k / 0.3f : 1 - (k - 0.3f) / 0.7f;
        float ks = k < 0.3f ? Mathf.Lerp(0.2f, 1.25f, k / 0.3f) : Mathf.Lerp(1.25f, 1.6f, (k - 0.3f) / 0.7f);
        sparkImg.color = new Color(1, 1, 1, ka);
        spark.localScale = Vector3.one * ks;
        spark.localRotation = Quaternion.Euler(0, 0, -45f * k);

        // a little shake as they meet
        float sh = Seg(t, 0.62f, 0.94f);
        stage.anchoredPosition = sh > 0 && sh < 1 ? new Vector2(Mathf.Sin(sh * 40f) * 5f * (1 - sh), Mathf.Cos(sh * 33f) * 3f * (1 - sh)) : Vector2.zero;

        // the king rises
        float kg = Seg(t, 0.7f, 1.4f), kb = Ui.Back(kg);
        kingImg.color = new Color(1, 1, 1, Mathf.Clamp01(kg * 2.5f));
        king.anchoredPosition = new Vector2(0, (0.111f - 0.5f) * 186 - 24f * (1 - kb));
        king.localScale = Vector3.one * Mathf.LerpUnclamped(0.7f, 1f, kb);

        // the title, letter by letter
        var sbd = new StringBuilder();
        for (int i = 0; i < Title.Length; i++)
        {
            float la = Ui.Ease(Seg(t, 1.15f + i * 0.045f, 1.4f + i * 0.045f));
            sbd.Append("<color=#F3CD62").Append(((int)(la * 255)).ToString("X2")).Append('>').Append(Title[i]).Append("</color>");
        }
        title.text = sbd.ToString();
        float ar = Ui.Ease(Seg(t, 1.75f, 2.35f));
        arabic.color = new Color(1, 0.91f, 0.69f, ar);
        arabic.rectTransform.anchoredPosition = new Vector2(0, -262 - 10 * (1 - ar));

        // loading bar: creeps to 90% while the café loads, fills when it's ready
        barGroup.alpha = Seg(t, 0.9f, 1.4f);
        float p = readyAt < 0 ? 0.9f * (1 - Mathf.Exp(-t * 0.6f)) : Mathf.Lerp(barFill.anchorMax.x, 1f, 0.2f);
        barFill.anchorMax = new Vector2(p, 1);

        if (shots && shot == 0 && t > 0.62f) { shot++; Capture("1-splash-clash.png"); }
        if (shots && shot == 1 && t > 2.5f) { shot++; Capture("2-splash.png"); }

        // open onto the café
        if (openAt < 0 && readyAt >= 0 && t >= MinTime && (!shots || shot >= 2)) openAt = Time.realtimeSinceStartup + 0.25f;
        if (openAt >= 0 && Time.realtimeSinceStartup > openAt)
        {
            float o = Mathf.Clamp01((Time.realtimeSinceStartup - openAt) / 0.65f);
            group.alpha = 1 - Ui.Ease(o);
            group.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, Ui.Ease(o));
            group.blocksRaycasts = false;
            if (o >= 1) { Destroy(canvas.gameObject); canvas = null; }
        }
        if (t > 15f && openAt < 0) openAt = Time.realtimeSinceStartup;   // never stuck on the splash
    }

    static void Sword(RectTransform r, RawImage img, int dir, float p, float a)
    {
        img.color = new Color(1, 1, 1, a);
        var home = new Vector2(0, (0.544f - 0.5f) * 186);
        r.anchoredPosition = home + new Vector2(dir * 0.47f * 150 * (1 - p), -0.13f * 186 * (1 - p));
        r.localRotation = Quaternion.Euler(0, 0, -dir * 70f * (1 - p));
    }

    static void Capture(string name)
    {
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "shots");
        Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name));
    }
}
