// The lucky wheel: it comes up when the game opens with today's free spin; after that, each spin is unlocked by
// watching ads (the 2nd needs 1 ad, the 3rd 3, the 4th 4, …). The server picks the prize (shared/gifts.ts WHEEL,
// same segments in the same order) and pays it; the wheel then turns to land on it.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{
    /// The prizes on the wheel, in order (the control room can change them: App.ApplyConfig).
    public static readonly long[] WheelCoins = { 1_000, 5_000, 2_000, 25_000, 3_000, 10_000, 1_500, 50_000 };
    static readonly Color[] WheelColors =
    {
        new Color(0.62f, 0.11f, 0.18f), new Color(0.93f, 0.71f, 0.29f), new Color(0.16f, 0.45f, 0.34f), new Color(0.93f, 0.71f, 0.29f),
        new Color(0.62f, 0.11f, 0.18f), new Color(0.93f, 0.71f, 0.29f), new Color(0.16f, 0.45f, 0.34f), new Color(0.98f, 0.86f, 0.45f),
    };
    static Texture2D wheelTex;
    RectTransform wheelDisc;
    Text wheelButtonText;
    bool wheelShown, wheelSpinning;
    float wheelAngle;

    /// The wheel's face, drawn once: eight coloured slices with a gold rim and a hub.
    static Texture2D WheelTexture()
    {
        if (wheelTex != null) return wheelTex;
        const int n = 512; float r = n / 2f;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r, d = Mathf.Sqrt(dx * dx + dy * dy);
                Color c = new Color(0, 0, 0, 0);
                if (d < r - 2)
                {
                    // slice k runs clockwise from the top
                    float a = Mathf.Repeat(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
                    int k = (int)(a / 45f);
                    c = WheelColors[k];
                    // the lines between slices: about 3 pixels wide at any distance from the middle
                    float off = Mathf.Repeat(a, 45f); off = Mathf.Min(off, 45f - off) * Mathf.Deg2Rad * d;
                    if (off < 1.6f) c = Color.Lerp(c, new Color(0.35f, 0.2f, 0.08f), 0.85f);
                    if (d > r - 22) c = new Color(0.93f, 0.71f, 0.29f);                        // the gold rim
                    if (d > r - 22 && ((int)(a / 15f)) % 2 == 0 && d < r - 8 && d > r - 18) c = new Color(1f, 0.95f, 0.7f);   // lights on the rim
                    if (d < 46) c = new Color(0.35f, 0.08f, 0.12f);                            // hub
                    if (d < 30) c = new Color(0.93f, 0.71f, 0.29f);
                }
                float aa = Mathf.Clamp01(r - 1 - d);
                c.a *= aa;
                px[y * n + x] = c;
            }
        wheelTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        wheelTex.SetPixels32(px); wheelTex.Apply();
        return wheelTex;
    }

    /// The small wheel button on the home screen (bottom left), with a dot when a spin is ready.
    GameObject wheelDot;
    void BuildWheelButton(RectTransform home)
    {
        var b = Ui.Button(home, "", new Color(0, 0, 0, 0), Color.white, 10, () => { Sfx.Tap(); OpenWheel(); });
        ((RectTransform)b.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(62, 62), new Vector2(14, 14));
        var pic = Ui.Picture(b.transform, "Wheel", WheelTexture()); pic.rectTransform.Fill(2);
        pic.gameObject.AddComponent<Spin>().speed = 18f;
        var lbl = Ui.Label(b.transform, "العجلة", 11, Ui.GoldLight, TextAnchor.MiddleCenter, Ui.Bold);
        lbl.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 16), new Vector2(0, -14));
        wheelDot = Dot((RectTransform)b.transform);
    }

    /// Opens the wheel at start: once a day, while today's free spin is waiting.
    bool MaybeShowWheel(Dictionary<string, object> u)
    {
        var w = u?.Obj("wheel");
        if (wheelShown || w == null || !w.Bool("ready") || w.Num("spins") > 0 || IsOpen || app.view != null || App.Recording || AdFilm.Any) return false;
        wheelShown = true;
        OpenWheel();
        return true;
    }

    void UpdateWheelDot()
    {
        if (wheelDot != null) wheelDot.SetActive(user?.Obj("wheel")?.Bool("ready") ?? false);
    }

    public void OpenWheel()
    {
        Panel(Loc.T("عجلة الحظ"), out var body, new Vector2(620, 360));
        // the wheel on the left (the right in English), the button and the text beside it
        var side = Loc.En ? 1f : -1f;
        var holder = Ui.Rect("Wheel", body).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(280, 280), new Vector2(-side * 140, -6));
        var glow = Ui.Picture(holder, "Glow", Ui.Glow(256)); glow.color = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.35f); glow.rectTransform.Fill(-40);
        wheelDisc = Ui.Rect("Disc", holder); wheelDisc.anchorMin = Vector2.zero; wheelDisc.anchorMax = Vector2.one; wheelDisc.offsetMin = wheelDisc.offsetMax = Vector2.zero;
        Ui.Picture(wheelDisc, "Face", WheelTexture()).rectTransform.Fill();
        for (int i = 0; i < WheelCoins.Length; i++)
        {
            // each slice's prize, written along it from the rim towards the hub
            float a = i * 45f + 22.5f;
            var holderL = Ui.Rect("Prize", wheelDisc); holderL.anchorMin = holderL.anchorMax = new Vector2(0.5f, 0.5f); holderL.sizeDelta = Vector2.zero;
            holderL.localRotation = Quaternion.Euler(0, 0, -a);
            var t = Ui.Label(holderL, Loc.Short(WheelCoins[i]), 17, i % 2 == 1 || i == 7 ? Ui.Ink : Color.white, TextAnchor.MiddleCenter, Ui.Bold);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(80, 24); t.rectTransform.anchoredPosition = new Vector2(0, 92);
        }
        wheelDisc.localRotation = Quaternion.Euler(0, 0, wheelAngle);
        // the pointer at the top
        var pointer = Ui.Box(holder, "Pointer", Ui.GoldLight, false).rectTransform;
        pointer.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(22, 34), new Vector2(0, 8));
        pointer.localRotation = Quaternion.Euler(0, 0, 0);
        var tip = Ui.Box(pointer, "Tip", Ui.GoldLight, false).rectTransform;
        tip.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(16, 16), new Vector2(0, -4)); tip.localRotation = Quaternion.Euler(0, 0, 45);

        var col = Ui.Rect("Side", body).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(260, 280), new Vector2(side * 150, -6));
        var title = Ui.Label(col, "لفّ واكسب لحد ٥٠ ألف كوين", 17, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        title.horizontalOverflow = HorizontalWrapMode.Wrap; title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(250, 52), new Vector2(0, -10));
        var info = Ui.Label(col, "", 12, Color.white, TextAnchor.MiddleCenter);
        info.horizontalOverflow = HorizontalWrapMode.Wrap; info.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(250, 64), new Vector2(0, -70));
        var btn = Ui.Button(col, "", Ui.Gold, Ui.Ink, 16, WheelPressed);
        ((RectTransform)btn.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(230, 54), new Vector2(0, 50));
        wheelButtonText = btn.GetComponentInChildren<Text>();
        var note = Ui.Label(col, "لفّة ببلاش كل يوم. اللفّة التانية بإعلان، والتالتة بـ ٣ إعلانات، والرابعة بـ ٤…", 10, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter);
        note.horizontalOverflow = HorizontalWrapMode.Wrap; note.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(250, 40), new Vector2(0, 4));
        refreshOpen = () => RefreshWheel(info);
        RefreshWheel(info);
    }

    void RefreshWheel(Text info)
    {
        if (wheelButtonText == null) return;
        var w = user?.Obj("wheel");
        int spins = (int)(w?.Num("spins") ?? 0), ads = (int)(w?.Num("ads") ?? 0), cost = (int)(w?.Num("cost") ?? 0), max = (int)(w?.Num("max") ?? 10);
        bool ready = w?.Bool("ready") ?? false;
        if (spins >= max) { wheelButtonText.Set(Loc.T("خلصت لفّات النهارده")); info.Set(Loc.T("تعالى بكرة للفّة ببلاش")); return; }
        if (ready) { wheelButtonText.Set(spins == 0 ? Loc.T("لفّ ببلاش") : Loc.T("لفّ")); info.Set(spins == 0 ? Loc.T("لفّتك ببلاش النهارده جاهزة") : Loc.T("اللفّة جاهزة!")); return; }
        wheelButtonText.Set("▶ " + Loc.T("شوف إعلان") + "  " + ads + "/" + cost);
        info.Set(Loc.T("اللفّة الجاية بـ") + " " + cost + " " + Loc.T(cost == 1 ? "إعلان" : "إعلانات"));
    }

    void WheelPressed()
    {
        if (wheelSpinning || adShowing) return;
        Sfx.Tap();
        if (app.net == null || !app.net.Online) { app.Toast(Loc.T("مش متصل بالسيرفر لسه")); return; }
        var w = user?.Obj("wheel");
        if (w == null) return;
        if (w.Num("spins") >= w.Num("max")) { app.Toast(Loc.T("تعالى بكرة للفّة ببلاش")); return; }
        if (w.Bool("ready")) { wheelSpinning = true; app.Send(new Dictionary<string, object> { ["t"] = "wheelSpin" }); return; }
        app.Send(new Dictionary<string, object> { ["t"] = "adStart", ["wheel"] = true });
    }

    /// The server's answer to a spin: turn the wheel so the prize stops under the pointer.
    void WheelResult(Dictionary<string, object> m)
    {
        int index = (int)m.Num("index"); long coins = (long)m.Num("coins");
        if (user != null && m.Obj("wheel") != null) user["wheel"] = m.Obj("wheel");
        if (wheelDisc == null) { wheelSpinning = false; app.Toast("+" + Loc.Short(coins) + " " + Loc.T("كوين")); return; }
        StartCoroutine(TurnWheel(index, coins));
    }

    IEnumerator TurnWheel(int index, long coins)
    {
        // slice `index` is centred at index*45+22.5 degrees clockwise from the top; turning the disc by that much
        // anticlockwise (positive z) brings it under the pointer
        float target = index * 45f + 22.5f + Random.Range(-15f, 15f);
        float start = wheelAngle, end = start - Mathf.Repeat(start, 360f) + 360f * 6 + target;
        float dur = 4.2f, lastTick = start;
        for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
        {
            if (wheelDisc == null) break;
            float k = 1 - Mathf.Pow(1 - t / dur, 3);
            wheelAngle = Mathf.Lerp(start, end, k);
            wheelDisc.localRotation = Quaternion.Euler(0, 0, wheelAngle);
            if (wheelAngle - lastTick >= 45f) { lastTick += 45f * Mathf.Floor((wheelAngle - lastTick) / 45f); Sfx.Tick(); }
            yield return null;
        }
        wheelAngle = end;
        if (wheelDisc != null) wheelDisc.localRotation = Quaternion.Euler(0, 0, wheelAngle);
        wheelSpinning = false;
        Sfx.Coins(); Sfx.Chime(); Sfx.Buzz();
        app.Toast(Loc.T("مبروك!") + "  +" + Loc.Short(coins) + " " + Loc.T("كوين"), 3f);
        refreshOpen?.Invoke();
        UpdateWheelDot();
    }
}

/// Turns a UI element slowly (the home screen's wheel button).
public class Spin : MonoBehaviour
{
    public float speed = 20f;
    void Update() { transform.Rotate(0, 0, -speed * Time.unscaledDeltaTime); }
}
