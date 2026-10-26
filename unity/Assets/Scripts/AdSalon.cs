// Film mode for the "engagement visit" advert ("-adsalon -advoice <dir> -adout <dir> [-adinserts]"):
// a first visit in a Cairo living room. The bride's father slams a tawla case on the table before any talk of the
// shabka; the groom offers to play online; the whole family ends up playing; at 2 a.m. the father won't let the
// groom sleep. Four of the game's characters act it out, their mouths following the four voices (tools/ad/salon),
// every frame saved at 30 a second. With -adinserts it then films a few seconds of each real game (tawla,
// dominoes, ludo, chess) in the café for the cut-ins.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AdSalon : MonoBehaviour
{
    App app;
    Salon set;
    Camera cam;
    string outDir, sub = "frames";
    int frame, saved;
    bool recording;
    float[] mouth = new float[0]; string[] who = new string[0];
    readonly Dictionary<string, (string who, float start, float end)> L = new Dictionary<string, (string, float, float)>();
    Person father, groom, mother, bride;
    Transform phoneF, phoneG, phoneM, phoneB;
    readonly Dictionary<Transform, (Person p, int hand)> holding = new Dictionary<Transform, (Person, int)>();
    readonly List<Transform> glasses = new List<Transform>();
    readonly List<Vector3> glassRest = new List<Vector3>();
    float impactAt = -10;
    bool portrait;

    public void Begin(App a)
    {
        app = a;
        outDir = AdFilm.Arg("-adout", Path.Combine(Directory.GetCurrentDirectory(), "ad"));
        string voice = AdFilm.Arg("-advoice", "");
        try
        {
            var m = Json.Parse(File.ReadAllText(Path.Combine(voice, "mouth.json"))) as Dictionary<string, object>;
            var open = m.Arr("open"); var w = m.Arr("who");
            mouth = new float[open.Count]; who = new string[open.Count];
            for (int i = 0; i < open.Count; i++) { mouth[i] = (float)(double)open[i]; who[i] = i < w.Count ? w[i] as string : ""; }
            var t = Json.Parse(File.ReadAllText(Path.Combine(voice, "timeline.json"))) as Dictionary<string, object>;
            foreach (var o in t.Arr("lines")) if (o is Dictionary<string, object> l) L[l.Str("id")] = (l.Str("who"), (float)l.Num("start"), (float)l.Num("end"));
        }
        catch (System.Exception e) { Debug.Log("[salon] no voice: " + e.Message); }
        // without a voice: the script's own timing, so the film still runs
        var plain = new (string id, string w, float s, float e)[] {
            ("f1", "father", 0.8f, 3.0f), ("g1", "groom", 3.25f, 4.4f), ("g2", "groom", 5.0f, 7.9f), ("f2", "father", 8.35f, 9.3f),
            ("f3", "father", 10.8f, 12.6f), ("g3", "groom", 12.9f, 14.6f), ("g4", "groom", 16.2f, 17.0f), ("m1", "mother", 19.3f, 22.2f),
            ("b1", "bride", 23.6f, 24.6f), ("f4", "father", 25.7f, 27.0f), ("f5", "father", 29.0f, 30.3f), ("g5", "groom", 30.65f, 33.2f), ("vo", "father", 34.6f, 37.4f) };
        foreach (var (id, w, s, e) in plain) if (!L.ContainsKey(id)) L[id] = (w, s, e);
        StartCoroutine(Film());
    }

    float S(string id) => L[id].start;
    float E(string id) => L[id].end;
    float T => frame / 30f;

    void LateUpdate()
    {
        if (!recording) return;
        // the props that follow hands, and the tea glasses jumping when the case lands
        foreach (var kv in holding)
        {
            var (p, h) = kv.Value; if (p == null || !kv.Key.gameObject.activeSelf) continue;
            var pos = h == 0 ? (p.HandPos(1) + p.HandPos(-1)) / 2 : p.HandPos(h);
            var head = p.Head != null ? p.Head.position : p.transform.position + Vector3.up * 7;
            kv.Key.position = pos + Vector3.up * 0.06f + (head - pos).normalized * 0.05f;
            var away = pos - head; if (away.sqrMagnitude < 1e-4f) away = p.Forward;
            kv.Key.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }
        float since = Time.time - impactAt;
        for (int i = 0; i < glasses.Count; i++)
            if (glasses[i] != null) glasses[i].position = glassRest[i] + Vector3.up * (since < 0.5f ? Mathf.Abs(Mathf.Sin(since * 30f + i)) * 0.06f * (1 - since / 0.5f) : 0);
        StartCoroutine(Grab(Path.Combine(outDir, sub, $"f{saved:D5}.jpg")));
        saved++;
    }

    /// The finished frame as a JPEG (PNGs of 2,000 full-HD frames would fill the build machine's disk).
    IEnumerator Grab(string path)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(path, tex.EncodeToJPG(93));
        Destroy(tex);
    }

    void Mouths()
    {
        int i = Mathf.Clamp(frame, 0, Mathf.Max(0, mouth.Length - 1));
        float o = mouth.Length > 0 ? mouth[i] : 0; string w = who.Length > 0 ? who[i] : "";
        if (w == "" ) { foreach (var kv in L) if (T >= kv.Value.start && T < kv.Value.end) { w = kv.Value.who; o = 0.35f + 0.35f * Mathf.Abs(Mathf.Sin(T * 17f)); } }
        father.Mouth(w == "father" && T < S("vo") - 0.5f ? o : 0); groom.Mouth(w == "groom" ? o : 0);
        mother.Mouth(w == "mother" ? o : 0); bride.Mouth(w == "bride" ? o : 0);
    }

    static Vector3 Face(Person p) => p.Head != null ? p.Head.position + Vector3.up * 0.3f : p.transform.position + Vector3.up * 7f;
    Vector3 W(float x, float y, float z) => Salon.Origin + new Vector3(x, y, z) * Salon.M;

    /// Places the camera: looking from `pos` at `look`, a vertical field of view for 16:9. On a tall (9:16) frame the
    /// same shot is opened up so the faces still fit: a wider lens, and two-shots from further back.
    void Shot(Vector3 pos, Vector3 look, float fov, bool wide = false, float shake = 0)
    {
        if (portrait)
        {
            pos = look + (pos - look) * (wide ? 1.75f : 1.12f);
            fov = 2 * Mathf.Atan(Mathf.Tan(fov * Mathf.Deg2Rad / 2) * 1.45f) * Mathf.Rad2Deg;
        }
        if (shake > 0) pos += new Vector3(Mathf.Sin(T * 61f), Mathf.Sin(T * 47f + 1), 0) * shake;
        cam.transform.position = pos;
        cam.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        cam.fieldOfView = fov;
    }

    void Hold(Transform phone, Person p, int hand = 0)
    {
        if (p == null) { holding.Remove(phone); return; }
        phone.gameObject.SetActive(true); holding[phone] = (p, hand);
    }
    void Pose(Person p, bool phone)
    {
        // seated on the sofa: hands on the lap, or together in front of the chest holding a phone
        p.tableY = Person.FloorY + (phone ? 0.8f : 0.6f) * Salon.M;
        p.reach = (phone ? 0.3f : 0.36f) * Salon.M;
        p.spread = phone ? 0.5f : 0.75f;
    }

    IEnumerator Film()
    {
        Time.captureFramerate = 30;
        while (app.cafe == null || !app.cafe.ready) yield return null;
        portrait = Screen.height > Screen.width;
        Loc.Set(AdFilm.Arg("-adlang", "ar") == "en");
        app.HideHome();
        app.cafe.enabled = false;
        if (app.cafe.keyLight != null) app.cafe.keyLight.enabled = false;
        cam = app.cafe.cam;
        set = new GameObject("SalonSet").AddComponent<Salon>();
        set.Build();
        father = Person.Make("AmFathi", set.seatFather); groom = Person.Make("Ziko", set.seatGroom);
        mother = Person.Make("Mona", set.seatMother); bride = Person.Make("Nada", set.seatBride);
        if (father == null || groom == null || mother == null || bride == null) { Debug.LogError("[salon] a character is missing"); Application.Quit(); yield break; }
        father.Face(set.faceFather); groom.Face(set.faceGroom); mother.Face(set.faceMother); bride.Face(set.faceBride);
        foreach (var p in new[] { father, groom, mother, bride }) { Pose(p, false); p.sitHips = 0.5f; }
        phoneF = Salon.Phone("PhoneFather"); phoneG = Salon.Phone("PhoneGroom"); phoneM = Salon.Phone("PhoneMother"); phoneB = Salon.Phone("PhoneBride");
        foreach (var ph in new[] { phoneF, phoneG, phoneM, phoneB }) ph.gameObject.SetActive(false);
        Pose(mother, true); Hold(phoneM, mother);
        Pose(bride, true); Hold(phoneB, bride);
        foreach (Transform t in GameObject.Find("CoffeeTable").transform) if (t.name == "Drink") { glasses.Add(t); glassRest.Add(t.position); }
        PlayerPrefs.SetInt("tut.domino", 1);
        Directory.CreateDirectory(Path.Combine(outDir, sub));
        yield return new WaitForSeconds(1.5f);   // settle (not filmed)
        Debug.Log($"[salon] filming, {L.Count} lines, portrait {portrait}");

        recording = true;
        var box = set.tawlaBox; var boxSpot = set.tableTop + new Vector3(-0.12f, 0, 0.04f) * Salon.M;
        bool slammed = false, offered = false, snatched = false, slapped = false, finger = false, placed = false, shown = false, night = false, woke = false;
        float[] shakes = { S("f5") - 0.35f, S("f5") + 0.4f, S("f5") + 1.15f }; int shook = 0;
        float end = E("vo") + 1.6f;
        Vector3 fF = Face(father), fG = Face(groom), fM = Face(mother), fB = Face(bride);
        while (T < end)
        {
            float t = T;
            Mouths();
            fF = Face(father); fG = Face(groom); fM = Face(mother); fB = Face(bride);
            // ---- 1. the slam, then the father's face; the groom swallows
            if (!slammed) { slammed = true; box.gameObject.SetActive(true); father.Slam(boxSpot); }
            if (t < 0.62f) { box.position = father.HandPos(1) - Vector3.up * 0.05f * Salon.M; box.rotation = Quaternion.LookRotation(father.Forward); }
            else if (impactAt < 0) { impactAt = Time.time; box.position = boxSpot; box.rotation = Quaternion.Euler(0, 4, 0); }
            if (t < S("g1") - 0.1f)
            {
                father.LookAt(fG); groom.LookAt(fF);
                float up = Ui.Ease(Mathf.Clamp01((t - 0.7f) / 0.7f));
                var low = set.tableTop + new Vector3(0.05f, 0.12f, 0.75f) * Salon.M;
                var pos = Vector3.Lerp(low, fF + (father.Forward * 1.1f + Vector3.right * 0.25f) * Salon.M + Vector3.down * 0.05f * Salon.M, up);
                var look = Vector3.Lerp(boxSpot, fF - Vector3.up * 0.03f * Salon.M, up);
                Shot(pos, look, Mathf.Lerp(36, 26, up), false, Mathf.Clamp01(1 - (Time.time - impactAt) / 0.35f) * (impactAt > 0 ? 0.12f : 0));
            }
            else if (t < S("g2") - 0.15f)
            {
                father.LookAt(fG); groom.LookAt(fF);
                Shot(fG + (groom.Forward * 0.95f - Vector3.right * 0.2f) * Salon.M, fG - Vector3.up * 0.04f * Salon.M, 24f);
            }
            // ---- 2. the offer: a two-shot pushing in; he holds out his phone, the father takes it
            else if (t < E("f2") + 0.2f)
            {
                float k = Mathf.Clamp01((t - S("g2")) / (E("f2") - S("g2") + 0.3f));
                father.LookAt(t > S("f2") - 0.3f ? (phoneG.position) : fG); groom.LookAt(fF);
                if (!offered && t > S("g2") + 0.3f) { offered = true; Pose(groom, true); Hold(phoneG, groom, 1); }
                if (t > E("g2") - 0.6f && t < S("f2") + 0.1f && !snatched && (int)(t * 30) % 25 == 0) groom.Gesture(Vector3.Lerp(fG, fF, 0.55f) - Vector3.up * 0.55f * Salon.M);
                if (!snatched && t > S("f2") + 0.05f) { snatched = true; father.Gesture(phoneG.position); }
                if (snatched && t > S("f2") + 0.4f && holding.TryGetValue(phoneG, out var hh) && hh.p == groom) { Hold(phoneG, father); Pose(father, true); Pose(groom, false); }
                var mid = (fF + fG) / 2;
                Shot(mid + new Vector3(0, -0.05f, Mathf.Lerp(1.9f, 1.35f, Ui.Ease(k))) * Salon.M, mid - Vector3.up * 0.25f * Salon.M, 30f, true);
            }
            // (the tawla cut-in covers f2's end → f3's start; meanwhile the groom finds his other phone)
            // ---- 3. tawla: the father wins and points him home; the groom raises a finger: dominoes
            else if (t < E("g3") + 0.2f)
            {
                if (!holding.ContainsKey(phoneF)) { Pose(groom, true); Hold(phoneF, groom); }   // the groom's own (second) phone
                if (!slapped && t > S("f3") + 0.05f) { slapped = true; father.Slam(father.transform.position + father.Forward * 0.5f * Salon.M + Vector3.right * 0.15f * Salon.M + Vector3.up * 3.2f); }
                if (!finger && t > S("g3") + 0.1f) { finger = true; groom.Gesture(fG + groom.Forward * 0.35f * Salon.M + Vector3.right * 0.1f * Salon.M - Vector3.up * 0.1f * Salon.M); }
                father.LookAt(t < S("g3") ? fG : fG); groom.LookAt(t < S("g3") ? phoneF.position : fF);
                var mid = (fF + fG) / 2;
                Shot(mid + new Vector3(0, 0.02f, 1.75f) * Salon.M, mid - Vector3.up * 0.3f * Salon.M, 30f, true);
            }
            // (the dominoes cut-in)
            // ---- 4. "domino, uncle": the groom lays his phone down; the father frozen
            else if (t < E("g4") + 0.05f)
            {
                if (!placed && t > S("g4") + 0.15f) { placed = true; groom.Gesture(set.tableTop + new Vector3(0.25f, 0.01f, 0.1f) * Salon.M); }
                if (placed && t > S("g4") + 0.5f && holding.ContainsKey(phoneF)) { Hold(phoneF, null); phoneF.position = set.tableTop + new Vector3(0.25f, 0.01f, 0.1f) * Salon.M; phoneF.rotation = Quaternion.Euler(90, 20, 0); Pose(groom, false); }
                groom.LookAt(fF); father.LookAt(phoneF.position);
                Shot(fG + (groom.Forward * 0.9f + Vector3.left * 0.35f) * Salon.M, fG - Vector3.up * 0.05f * Salon.M, 26f);
            }
            else if (t < E("g4") + 1.0f)
            {
                father.LookAt(phoneF.position);
                float z = Ui.Ease(Mathf.Clamp01((t - E("g4") - 0.05f) / 0.22f));
                Shot(fF + (father.Forward * 1.2f + Vector3.right * 0.15f) * Salon.M, fF - Vector3.up * 0.03f * Salon.M, Mathf.Lerp(32, 15, z));
            }
            // (the ludo cut-in)
            // ---- 5. the mother, never looking up from her phone; then the two men turn to her together
            else if (t < E("m1") + 0.1f)
            {
                mother.LookAt(phoneM.position); father.LookAt(phoneG.position); groom.LookAt(fF);
                // from low and in front, so her face shows under the hood while she looks down at her phone
                Shot(fM + (mother.Forward * 1.35f - Vector3.up * 0.32f) * Salon.M, fM - Vector3.up * 0.12f * Salon.M, 30f);
            }
            else if (t < S("b1") - 0.1f)
            {
                bool turn = t > E("m1") + 0.25f;
                father.LookAt(turn ? fM : phoneG.position); groom.LookAt(turn ? fM : fF); mother.LookAt(phoneM.position);
                var mid = (fF + fG) / 2;
                Shot(mid + new Vector3(-0.15f, 0.0f, 1.6f) * Salon.M, mid - Vector3.up * 0.25f * Salon.M, 30f, true);
            }
            // ---- 6. checkmate from his own daughter, over his shoulder; then he looks from face to face
            else if (t < E("b1") + 0.1f)
            {
                if (!shown) { shown = true; bride.Gesture(Vector3.Lerp(fB, fF, 0.45f) - Vector3.up * 0.35f * Salon.M); }
                bride.LookAt(fF); father.LookAt(fB);
                var d = (fB - fF).normalized; var side = Vector3.Cross(Vector3.up, d);
                Shot(fF - d * 0.55f * Salon.M + side * 0.22f * Salon.M + Vector3.up * 0.08f * Salon.M, fB - Vector3.up * 0.05f * Salon.M, 30f);
            }
            // (the chess cut-in)
            else if (t < E("f4") + 0.4f)
            {
                float k = Mathf.Clamp01((t - S("f4") + 0.1f) / (E("f4") - S("f4") + 0.5f));
                float q = (t - S("f4")) / Mathf.Max(0.3f, E("f4") - S("f4"));
                father.LookAt(q < 0.33f ? fB : q < 0.66f ? fM : fG); bride.LookAt(fF); groom.LookAt(fF); mother.LookAt(phoneM.position);
                float a = Mathf.Lerp(-35f, 30f, Ui.Ease(k)) * Mathf.Deg2Rad;
                var fwd = father.Forward; var right = Vector3.Cross(Vector3.up, fwd);
                var dir = fwd * Mathf.Cos(a) + right * Mathf.Sin(a);
                Shot(fF + dir * 1.25f * Salon.M + Vector3.up * 0.05f * Salon.M, fF - Vector3.up * 0.06f * Salon.M, 28f);
            }
            // ---- 7. 2 a.m.: the clock, then the groom asleep on the sofa and the father shaking him awake
            else if (t < S("f5") - 0.35f)
            {
                if (!night)
                {
                    night = true; set.Night(true); set.SetClock(2f);
                    RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.28f); RenderSettings.ambientEquatorColor = new Color(0.11f, 0.11f, 0.16f);
                    mother.gameObject.SetActive(false); bride.gameObject.SetActive(false); phoneM.gameObject.SetActive(false); phoneB.gameObject.SetActive(false);
                    holding.Remove(phoneM); holding.Remove(phoneB);
                    groom.dozing = true; Pose(groom, false);
                    Hold(phoneG, father, -1);
                    var glow = new GameObject("PhoneGlow").AddComponent<Light>();   // the only light on their faces now
                    glow.transform.SetParent(phoneG, false); glow.transform.localPosition = new Vector3(0, 0, -0.3f);
                    glow.type = LightType.Point; glow.color = new Color(0.62f, 0.76f, 1f); glow.range = 1.4f * Salon.M; glow.intensity = 1.6f;
                    box.gameObject.SetActive(false); phoneF.gameObject.SetActive(false);
                }
                float k = Mathf.Clamp01((t - E("f4") - 0.4f) / 1.6f);
                var clock = set.clockHour.position;
                Shot(clock + Vector3.forward * Mathf.Lerp(1.15f, 0.95f, k) * Salon.M + Vector3.down * 0.05f * Salon.M, clock, 26f);
                father.LookAt(phoneG.position);
            }
            else if (t < S("vo") - 0.3f)
            {
                if (shook < shakes.Length && t > shakes[shook]) { shook++; father.Gesture(fG - Vector3.up * 0.75f * Salon.M + Vector3.left * 0.1f * Salon.M); }
                if (!woke && t > S("g5") - 0.3f) { woke = true; groom.dozing = false; }
                father.LookAt(fG); groom.LookAt(woke ? (Vector3?)fF : null);
                var mid = (fF + fG) / 2;
                float k = Mathf.Clamp01((t - S("f5")) / 4f);
                Shot(mid + new Vector3(0.1f, Mathf.Lerp(0.85f, 0.75f, k), Mathf.Lerp(1.5f, 1.3f, k)) * Salon.M, mid - Vector3.up * 0.3f * Salon.M, 32f, true);
            }
            else
            {
                // under the end card: the two of them still at it, the camera drifting back
                father.LookAt(phoneG.position); groom.LookAt(fF);
                var mid = (fF + fG) / 2;
                float k = Mathf.Clamp01((t - S("vo") + 0.3f) / 4f);
                Shot(mid + new Vector3(0.1f, Mathf.Lerp(0.75f, 0.9f, k), Mathf.Lerp(1.3f, 2.2f, k)) * Salon.M, mid - Vector3.up * 0.3f * Salon.M, 32f, true);
            }
            frame++;
            yield return null;
        }
        recording = false;
        Debug.Log("[salon] story done, frames " + saved);

        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-adinserts") >= 0)
        {
            // the cut-ins: a few seconds of each real game, as you see it in the app
            set.Show(false);
            foreach (var p in new[] { father, groom, mother, bride }) p.gameObject.SetActive(false);
            foreach (var ph in new[] { phoneF, phoneG, phoneM, phoneB }) ph.gameObject.SetActive(false);
            box.gameObject.SetActive(false);
            if (app.cafe.keyLight != null) app.cafe.keyLight.enabled = true;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.36f); RenderSettings.ambientEquatorColor = new Color(0.36f, 0.27f, 0.2f); RenderSettings.ambientGroundColor = new Color(0.16f, 0.11f, 0.08f);
            app.cafe.enabled = true;
            cam.fieldOfView = 38;
            foreach (var (mode, name) in new[] { ("tawla", "tawla"), ("ffa4", "domino"), ("ludo", "ludo"), ("chess", "chess") })
            {
                app.Practice(mode);
                app.cafe.Snap();
                for (int i = 0; i < 45; i++) { Move(); yield return null; }   // the game set up (not filmed)
                sub = "ins-" + name; saved = 0; Directory.CreateDirectory(Path.Combine(outDir, sub));
                recording = true;
                for (int i = 0; i < 210; i++) { Move(); yield return null; }
                recording = false;
                Debug.Log($"[salon] insert {name}: {saved} frames");
                app.Leave(); app.HideHome();
                yield return null;
            }
        }
        yield return null;
        Application.Quit();
        yield return new WaitForSecondsRealtime(5);
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    float nextMove;
    /// Your moves in the practice game, made for you at a natural pace.
    void Move()
    {
        if (Time.time < nextMove) return;
        nextMove = Time.time + 0.55f;
        var v = app.view;
        if (v == null || !v.MyTurn) return;
        if (v.Board) { app.AutoMove(); return; }
        foreach (var id in v.hand)
        {
            if (v.open >= 0 && v.line.Empty && id != v.open) continue;
            var ends = Domino.EndsFor(v.line, id);
            if (ends.Count == 0) continue;
            app.TapTile(id);
            if (ends.Count > 1) app.PlayEnd(ends[0]);
            return;
        }
    }
}
