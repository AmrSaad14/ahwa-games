// Film mode ("-ad -advoice <dir> -adout <dir>"): shoots the advert's scenes in the café, timed to the voice-over
// (tools/ad: timeline.json, mouth.json). Am Fathi and Hajj Mahmoud talk at their table with their mouths following
// the voice; the camera moves from shot to shot; a real game of dominoes plays for the line about the phone.
// Every frame is saved (30 a second, game time steps 1/30 s whatever the machine's speed), then CI makes a video.
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AdFilm : MonoBehaviour
{
    public static readonly bool On = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ad") >= 0;
    static string Arg(string name, string def)
    {
        var a = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : def;
    }

    App app;
    string outDir;
    int frame;
    bool recording;
    float[] mouth = new float[0]; string[] who = new string[0];
    readonly List<(string id, string speaker, float start, float end)> lines = new List<(string, string, float, float)>();
    Person fathi, hajj;

    public void Begin(App a)
    {
        app = a;
        outDir = Arg("-adout", Path.Combine(Directory.GetCurrentDirectory(), "ad"));
        Directory.CreateDirectory(Path.Combine(outDir, "frames"));
        string voice = Arg("-advoice", "");
        try
        {
            var m = Json.Parse(File.ReadAllText(Path.Combine(voice, "mouth.json"))) as Dictionary<string, object>;
            var open = m.Arr("open"); var w = m.Arr("who");
            mouth = new float[open.Count]; who = new string[open.Count];
            for (int i = 0; i < open.Count; i++) { mouth[i] = (float)(double)open[i]; who[i] = i < w.Count ? w[i] as string : ""; }
            var t = Json.Parse(File.ReadAllText(Path.Combine(voice, "timeline.json"))) as Dictionary<string, object>;
            foreach (var o in t.Arr("lines")) if (o is Dictionary<string, object> l) lines.Add((l.Str("id"), l.Str("who"), (float)l.Num("start"), (float)l.Num("end")));
        }
        catch (System.Exception e) { Debug.Log("[ad] no voice: " + e.Message); }
        if (lines.Count == 0)   // without a voice: a plain timing, so the film still runs
            foreach (var (id, s, e) in new[] { ("l1", 0f, 6f), ("l2", 6.3f, 10.5f), ("l3", 10.7f, 13.6f), ("l4", 13.7f, 14.9f), ("l5", 15.2f, 18f), ("l6", 18.3f, 22f), ("l7", 22.3f, 25.5f) })
                lines.Add((id, id == "l4" ? "hajj" : "fathi", s, e));
        StartCoroutine(Film());
    }

    float Start(string id, float def = 0) { foreach (var l in lines) if (l.id == id) return l.start; return def; }
    float End(string id, float def = 0) { foreach (var l in lines) if (l.id == id) return l.end; return def; }

    void LateUpdate()
    {
        if (!recording) return;
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "frames", $"f{frame:D5}.png"));
        frame++;
    }

    float T => frame / 30f;

    /// The mouths, from the voice: whoever is speaking now opens and closes theirs.
    void Mouths()
    {
        int i = Mathf.Clamp(frame, 0, Mathf.Max(0, mouth.Length - 1));
        float o = mouth.Length > 0 ? mouth[i] : 0;
        string w = who.Length > 0 ? who[i] : "";
        if (fathi != null) fathi.Mouth(w == "fathi" ? o : 0);
        if (hajj != null) hajj.Mouth(w == "hajj" ? o : 0);
    }

    string Speaker(float t) { foreach (var l in lines) if (t >= l.start && t < l.end) return l.speaker; return ""; }

    /// A close shot of one face: in front of him (the regulars face each other across their table), a little to the side.
    (Vector3 pos, Vector3 look) Close(Person p, float dist, float sideways)
    {
        var a = FaceOf(p); var f = p.Forward; var side = Vector3.Cross(Vector3.up, f);
        return (a + f * dist + side * sideways + Vector3.up * 0.35f, a - Vector3.up * 0.1f);
    }

    /// Both of them at their table, from the side that faces the café's front; `zoom` 1 is the usual distance.
    (Vector3 pos, Vector3 look) Two(float zoom)
    {
        var a = FaceOf(fathi); var b = FaceOf(hajj); var mid = (a + b) / 2;
        var dir = Vector3.Cross(Vector3.up, b - a).normalized;
        if (Vector3.Dot(dir, app.cafe.Front) < 0) dir = -dir;
        float d = ((b - a).magnitude * 0.85f + 1.5f) * zoom;
        return (mid + dir * d + Vector3.up * (0.25f * d), mid - Vector3.up * 0.4f);
    }

    static Vector3 FaceOf(Person p) => p.Head != null ? p.Head.position + Vector3.up * 0.25f : p.transform.position + Vector3.up * 7f;

    IEnumerator Film()
    {
        Time.captureFramerate = 30;
        while (app.cafe == null || !app.cafe.ready) yield return null;
        Loc.Set(Arg("-adlang", "ar") == "en");
        app.HideHome();
        foreach (var p in app.cafe.Regulars()) { if (p.name.StartsWith("AmFathi")) fathi = p; if (p.name.StartsWith("HajjMahmoud")) hajj = p; }
        PlayerPrefs.SetInt("tut.domino", 1);
        yield return new WaitForSeconds(2f);   // let the café settle (not filmed)
        Debug.Log($"[ad] filming: fathi {fathi != null}, hajj {hajj != null}, lines {lines.Count}");
        recording = true;
        float end = End("l7", 25.5f) + 4.2f;   // the end card goes over the last seconds
        bool inGame = false, hajjCheered = false, fathiCheered = false;
        var cam = app.cafe;
        while (T < end)
        {
            float t = T;
            Mouths();
            // who looks where: the one talking at the camera, the other at him
            Vector3 camAt = cam.cam.transform.position;
            string speaking = Speaker(t);
            if (fathi != null) fathi.LookAt(speaking == "hajj" && hajj != null ? FaceOf(hajj) : camAt);
            if (hajj != null) hajj.LookAt(speaking == "fathi" && fathi != null ? FaceOf(fathi) : camAt);
            if (t < Start("l2", 6.3f))
            {
                // 1. Am Fathi alone, close; when he names Hajj Mahmoud the camera pulls back to show them both
                if (fathi != null && hajj != null)
                {
                    float wide = Ui.Ease(Mathf.Clamp01((t - Start("l1") - 2.4f) / 1.8f));
                    var (cp, cl) = Close(fathi, 3.4f - 0.3f * Mathf.Clamp01(t / 2.4f), 0.5f);
                    var (wp, wl) = Two(1f - 0.08f * Mathf.Clamp01((t - 4f) / 3f));
                    cam.Shot(Vector3.Lerp(cp, wp, wide), Vector3.Lerp(cl, wl, wide));
                }
            }
            else if (t < Start("l3", 10.7f))
            {
                // 2. the café: a slow glide past the tables (dominoes, tawla, chess, ludo)
                float k = Mathf.Clamp01((t - Start("l2")) / Mathf.Max(0.1f, Start("l3") - Start("l2")));
                var c = cam.Center; var f = cam.Front; var r = Vector3.Cross(Vector3.up, f);
                var pos = c + f * 24f + r * Mathf.Lerp(-10f, 10f, Ui.Ease(k)) + Vector3.up * Mathf.Lerp(13f, 10f, k);
                cam.Shot(pos, c + r * Mathf.Lerp(-5f, 5f, k) + Vector3.up * 2f);
            }
            else if (t < Start("l5", 15.2f))
            {
                // 3. Hajj Mahmoud, sure no one can beat him; on his own line he throws his arms up
                if (hajj != null)
                {
                    float k = Mathf.Clamp01((t - Start("l3")) / 4f);
                    var (p3, l3) = Close(hajj, Mathf.Lerp(3.6f, 2.9f, k), -0.7f);
                    cam.Shot(p3, l3);
                    if (!hajjCheered && t >= Start("l4", 13.7f)) { hajjCheered = true; hajj.Win(); }
                }
            }
            else if (t < Start("l6", 18.3f) - 0.4f)
            {
                // 4. Am Fathi, close, leaning in: "so we brought the café to your phone"
                if (fathi != null)
                {
                    float k = Mathf.Clamp01((t - Start("l5")) / 3f);
                    var (p4, l4) = Close(fathi, Mathf.Lerp(3.2f, 2.4f, Ui.Ease(k)), 0.6f);
                    cam.Shot(p4, l4);
                }
            }
            else if (t < Start("l7", 22.3f) - 0.2f)
            {
                // 5. the game itself: a real game of dominoes for four, played here
                if (!inGame) { inGame = true; cam.Shot(null); app.Practice("ffa4"); }
                PlayMyTurn();
            }
            else
            {
                // 6. back at the table, both of them, cheering: the end card goes over the last seconds
                if (inGame) { inGame = false; app.Leave(); app.HideHome(); }
                if (fathi != null && hajj != null)
                {
                    float k = Ui.Ease(Mathf.Clamp01((t - Start("l7")) / 5f));
                    var (p6, l6) = Two(Mathf.Lerp(0.95f, 1.25f, k));
                    cam.Shot(p6 + Vector3.up * k, l6);
                    if (!fathiCheered && t > End("l7", 25.5f) - 0.6f) { fathiCheered = true; fathi.Win(); hajj.Win(); }
                }
            }
            yield return null;
        }
        recording = false;
        Debug.Log("[ad] done, frames " + frame);
        yield return null;
        Application.Quit();
        yield return new WaitForSecondsRealtime(5);
        System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    float nextMove;
    void PlayMyTurn()
    {
        var v = app.view;
        if (v == null || !v.MyTurn || v.Board || T < nextMove) return;
        nextMove = T + 0.7f;
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
