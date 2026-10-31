// The café: the room and its four game tables (exported from the web game), warm lights, the regulars at
// their tables, and the camera that looks over the floor or moves in on one table.
using System.Collections.Generic;
using UnityEngine;

public class Cafe : MonoBehaviour
{
    public static readonly string[] Games = { "domino", "tawla", "chess", "ludo" };
    public readonly Dictionary<string, Transform> tables = new Dictionary<string, Transform>();
    public Camera cam;
    public string focus;            // the table the camera has moved in on (null: the whole floor)
    public bool ready;
    public Light keyLight;          // the warm directional light (switched off while the advert films in the living room)

    Vector3 center, front;          // the middle of the four tables; the way toward the camera side of the room
    public Vector3 Front => front;
    public Vector3 Center => center;
    Transform lobby;                // the four small game tables (hidden while a game is on)
    Transform chairModel;           // a café chair, copied for the game table
    readonly List<GameObject> regulars = new List<GameObject>();
    bool inGame;
    Vector3 camPos, camLook, camPosTo, camLookTo;
    float drift;

    // who sits where: two regulars at each table, one seat left free at chess and ludo (for you)
    static readonly (string who, string table, int seat)[] Seating =
    {
        ("AmFathi", "domino", 0), ("HajjMahmoud", "domino", 1),
        ("CaptainSami", "tawla", 0), ("Ziko", "tawla", 1),
        ("Nada", "chess", 1), ("Mona", "ludo", 0),
    };

    const float LobbyReach = (4.5f - 3.2f) + 0.55f;   // chair to table edge, then a little onto the table

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }

    public void Build() { var it = BuildSteps(); while (it.MoveNext()) { } }

    /// Builds the café a piece at a time, so the splash keeps moving while it loads.
    public System.Collections.IEnumerator BuildSteps()
    {
        // camera
        var camGo = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
        cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 38;
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 220;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x1c, 0x10, 0x0a, 0xff);
        cam.allowHDR = true;
        cam.allowMSAA = true;
        camGo.AddComponent<CafeLook>();   // bloom, tone mapping, warm grade, vignette

        // the room
        var prefab = Resources.Load<GameObject>("Art/cafe");
        if (prefab == null) { Debug.LogError("[cafe] Art/cafe not found (is glTFast importing the .glb files?)"); yield break; }
        var room = Instantiate(prefab);
        room.name = "Cafe";
        foreach (var g in Games)
        {
            var t = Find(room.transform, "Table_" + g);
            if (t != null) tables[g] = t; else Debug.LogWarning("[cafe] no table for " + g);
        }
        lobby = Find(room.transform, "Tables");
        chairModel = Find(room.transform, "Seat_domino_0");
        Debug.Log($"[cafe] room loaded, {tables.Count} tables, {room.GetComponentsInChildren<Renderer>().Length} renderers");
        if (tables.Count == 4)
        {
            center = Vector3.zero; foreach (var t in tables.Values) center += t.position; center /= 4;
            var backMid = (tables["domino"].position + tables["tawla"].position) * 0.5f;
            var frontMid = (tables["chess"].position + tables["ludo"].position) * 0.5f;
            front = frontMid - backMid; front.y = 0; front.Normalize();
        }
        else { center = Vector3.zero; front = Vector3.forward; }

        yield return null;
        SettleProps(room.transform);
        Lights();
        yield return null;
        foreach (var _ in People(room.transform)) yield return null;

        camPos = camPosTo = Overview(out camLook); camLookTo = camLook;
        Apply();
        ready = true;
    }

    /// Each table's game (its board, or the domino tiles) sits in the middle of that table's top, resting on it.
    void SettleProps(Transform room)
    {
        foreach (var g in Games)
        {
            if (!tables.TryGetValue(g, out var table)) continue;
            var board = Find(room, "Board_" + g);
            if (board == null) continue;
            // the table top: the widest flat part of the table (not its chairs, not the board)
            Renderer top = null; float best = 0;
            foreach (var r in table.GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(board) || r.name.StartsWith("Seat") || (r.transform.parent != null && r.transform.parent.name.StartsWith("Seat"))) continue;
                var b = r.bounds; float area = b.size.x * b.size.z;
                if (area > best) { best = area; top = r; }
            }
            if (top == null) continue;
            var tb = top.bounds;
            Bounds bb = default; bool any = false;
            foreach (var r in board.GetComponentsInChildren<Renderer>()) { if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds); }
            if (!any) continue;
            var move = new Vector3(tb.center.x - bb.center.x, tb.max.y - bb.min.y + 0.005f, tb.center.z - bb.center.z);
            board.position += move;
            Debug.Log($"[cafe] {g}: board moved by {move}");
        }
    }

    void Lights()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.36f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.27f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.11f, 0.08f);
        // the warm light from the lamps above, casting the shadows
        var sun = new GameObject("Key light").AddComponent<Light>();
        keyLight = sun;
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.86f, 0.66f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.75f;
        sun.transform.rotation = Quaternion.LookRotation(-front * 0.45f + Vector3.down + Vector3.Cross(Vector3.up, front) * 0.3f);
        // a hanging lamp over every table
        foreach (var t in tables.Values)
        {
            var l = new GameObject("Lamp").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.42f);
            l.intensity = 2.2f;
            l.range = 14;
            l.transform.position = t.position + Vector3.up * 7.5f;
        }
    }

    System.Collections.Generic.IEnumerable<int> People(Transform room)
    {
        foreach (var (who, table, seat) in Seating)
        {
            if (!tables.TryGetValue(table, out var t)) continue;
            var chair = Find(t, "Seat_" + table + "_" + seat);
            if (chair == null) { Debug.LogWarning("[cafe] no chair for " + who); continue; }
            var person = Person.Make(who, chair.position);
            if (person == null) continue;
            person.reach = LobbyReach;
            person.Face(t.position);
            var p = person.gameObject;
            regulars.Add(p);
            yield return 0;   // one person a frame
        }
    }

    Vector3 Overview(out Vector3 look)
    {
        float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
        float far = Mathf.Max(1f, 1.75f / aspect);
        look = center + Vector3.down * 2f - front * 3.2f;
        return center + Vector3.up * 23f * far + front * 25f * far;
    }

    Vector3 TableShot(string g, out Vector3 look)
    {
        var t = tables[g].position;
        var right = Vector3.Cross(Vector3.up, -front);   // screen right when looking over the room
        look = t + Vector3.down * 0.6f + right * 3.4f;
        return look + Vector3.up * 11.5f + front * 13.5f;
    }

    /// A café chair at `at`, facing `toward`.
    public GameObject MakeChair(Vector3 at, Vector3 toward)
    {
        if (chairModel == null) return null;
        var c = Instantiate(chairModel.gameObject);
        c.name = "Chair";
        // the model chair faces its own table: turn the copy from that way to the new one
        var tbl = tables["domino"].position;
        var was = tbl - chairModel.position; was.y = 0;
        var want = toward - at; want.y = 0;
        c.transform.rotation = Quaternion.FromToRotation(was.normalized, want.normalized) * chairModel.rotation;
        c.transform.position = new Vector3(at.x, chairModel.position.y, at.z);
        c.transform.localScale = chairModel.lossyScale;
        return c;
    }

    /// The café while a game is on: the four small tables and their regulars step aside for the big table.
    /// Every game is seen the same way, for everyone: straight down on the board, filling the screen between the
    /// top bar and the bottom (your tiles in dominoes), in a clear, even light.
    public static bool BoardView => true;
    public void SetView(bool board) { if (inGame) camPosTo = GameShot(out camLookTo); }

    string gameKind = "domino";
    /// Which game is on the table: each board has its own size and its own camera height.
    public void SetGameKind(string g)
    {
        g = string.IsNullOrEmpty(g) ? "domino" : g;
        if (g == gameKind) return;
        gameKind = g;
        if (inGame) camPosTo = GameShot(out camLookTo);
    }

    public void SetGame(bool on)
    {
        inGame = on;
        if (lobby != null) lobby.gameObject.SetActive(!on);
        foreach (var r in regulars) if (r != null) r.SetActive(!on);
        GameLight(on);
        if (on) camPosTo = GameShot(out camLookTo); else Focus(focus);
    }

    // ---------- the light while a game is on: even, neutral, from above, with no glare ----------
    Quaternion keyRot; Color keyColor; float keyIntensity, keyShadow; bool savedLight;
    Color ambSky, ambEq, ambGround;
    readonly List<(Light l, float i)> lamps = new List<(Light, float)>();
    GameObject backdrop;

    void GameLight(bool on)
    {
        if (keyLight == null) return;
        if (!savedLight)
        {
            savedLight = true;
            keyRot = keyLight.transform.rotation; keyColor = keyLight.color; keyIntensity = keyLight.intensity; keyShadow = keyLight.shadowStrength;
            ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Point) lamps.Add((l, l.intensity));
        }
        if (on)
        {
            // nearly straight down, a touch from your side, so pieces cast short soft shadows away from you
            keyLight.transform.rotation = Quaternion.LookRotation(Vector3.down * 4f - front * 0.6f + Vector3.Cross(Vector3.up, front) * 0.35f);
            // soft: enough to read every tile and square, never bright enough to wash them out
            keyLight.color = new Color(1f, 0.96f, 0.9f);
            keyLight.intensity = 0.8f;
            keyLight.shadowStrength = 0.3f;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.44f, 0.41f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.31f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.13f);
            foreach (var (l, i) in lamps) if (l != null) l.intensity = i * 0.15f;   // no hot spot in the middle of the board
        }
        else
        {
            keyLight.transform.rotation = keyRot; keyLight.color = keyColor; keyLight.intensity = keyIntensity; keyLight.shadowStrength = keyShadow;
            RenderSettings.ambientSkyColor = ambSky; RenderSettings.ambientEquatorColor = ambEq; RenderSettings.ambientGroundColor = ambGround;
            foreach (var (l, i) in lamps) if (l != null) l.intensity = i;
        }
        // the camera's look: no glow on the white tiles and squares, a light vignette
        var look = cam != null ? cam.GetComponent<CafeLook>() : null;
        if (look != null)
        {
            look.bloom = on ? 0f : 0.35f; look.threshold = on ? 2.5f : 1.1f;
            look.vignette = on ? 0.18f : 0.5f; look.saturation = on ? 1.02f : 1.08f; look.exposure = on ? 0.9f : 0.95f;
            look.warm = on ? new Color(1.01f, 1.0f, 0.98f) : new Color(1.04f, 1.0f, 0.93f);
        }
        // the busy café floor fades behind a dark cloth around the table, so the eye stays on the board
        if (backdrop == null)
        {
            backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Destroy(backdrop.GetComponent<Collider>());
            backdrop.transform.position = new Vector3(0, -0.08f, 0);
            backdrop.transform.rotation = Quaternion.Euler(90, 0, 0);
            backdrop.transform.localScale = new Vector3(90, 90, 1);
            const int S = 128;
            var tex = Art.Paint(S, S, (x, y) => { float d = Vector2.Distance(new Vector2(x, y), new Vector2(S / 2f, S / 2f)) / (S / 2f); return Color.Lerp(new Color(0.16f, 0.11f, 0.08f), new Color(0.05f, 0.035f, 0.025f), Mathf.Clamp01(d * 1.4f)); });
            var r = backdrop.GetComponent<MeshRenderer>();
            r.sharedMaterial = Art.Mat(Color.white, 0.05f, 0, tex);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        backdrop.SetActive(on);
    }

    /// Straight down on the board (a few degrees from your side, so 3D pieces keep their shape), framed between
    /// the top bar and the bottom of the screen.
    Vector3 GameShot(out Vector3 look)
    {
        float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
        float vf = cam.fieldOfView * Mathf.Deg2Rad;
        float hf = 2 * Mathf.Atan(Mathf.Tan(vf / 2) * aspect);
        // each board: its half width and half depth (world units), the camera's angle above the table, and the
        // parts of the screen the game's own buttons take (top bar; your tiles at the bottom in dominoes; the side buttons)
        // (the boards' real sizes: chess 9.2 + its frame, tawla 11.6 × 7.8, ludo 15 cells of 0.7 + its frame,
        //  dominoes the cloth and the tiles the others hold up at its edges): as close as the whole board still fits
        float hx, hz, el, top = 0.105f, bottom, side = 0.02f;
        switch (gameKind)
        {
            case "chess": hx = 5.2f; hz = 5.0f; el = 64f; bottom = 0.02f; break;   // a little tilted: the pieces keep their shapes
            case "tawla": hx = 5.85f; hz = 3.95f; el = 82f; bottom = 0.05f; break;
            case "ludo": hx = 5.45f; hz = 5.45f; el = 84f; bottom = 0.02f; break;
            default: hx = 5.5f; hz = 5.35f; el = 84f; bottom = 0.21f; break;   // dominoes: your hand at the bottom
        }
        float free = 1 - top - bottom, freeW = 1 - 2 * side;
        float H = hz / free;                                   // the half height the screen must show
        float d = Mathf.Max(H / Mathf.Tan(vf / 2), hx / freeW / Mathf.Tan(hf / 2)) + 0.3f;   // + the pieces' height
        float e = el * Mathf.Deg2Rad;
        // the board's middle sits in the middle of the free part of the screen
        float shift = H * (bottom - top);
        look = Vector3.zero + front * shift + Vector3.up * 0.3f;
        return look + front * d * Mathf.Cos(e) + Vector3.up * d * Mathf.Sin(e);
    }

    public void Focus(string g)
    {
        focus = g != null && tables.ContainsKey(g) ? g : null;
        camPosTo = focus == null ? Overview(out camLookTo) : TableShot(focus, out camLookTo);
    }

    /// The table under a screen point (taps on the café floor), or null.
    public string TableAt(Vector2 screen)
    {
        if (cam == null) return null;
        var ray = cam.ScreenPointToRay(screen);
        if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
        float k = (center.y - ray.origin.y) / ray.direction.y;
        if (k < 0) return null;
        var hit = ray.origin + ray.direction * k;
        string best = null; float bd = 6f;
        foreach (var kv in tables) { float d = Vector2.Distance(new Vector2(hit.x, hit.z), new Vector2(kv.Value.position.x, kv.Value.position.z)); if (d < bd) { bd = d; best = kv.Key; } }
        return best;
    }

    void Update()
    {
        if (!ready) return;
        float k = 1 - Mathf.Exp(-Time.deltaTime * 2.2f);
        camPos = Vector3.Lerp(camPos, camPosTo, k);
        camLook = Vector3.Lerp(camLook, camLookTo, k);
        drift += Time.deltaTime;
        Apply();
    }

    void Apply()
    {
        // a slow sway, so the café never looks like a still picture
        float sw = inGame ? 0.25f : 1f;
        var sway = sw * Vector3.Cross(Vector3.up, front) * Mathf.Sin(drift * 0.18f) * 0.6f + Vector3.up * Mathf.Sin(drift * 0.23f) * 0.25f * sw;
        // a short jolt (a tile slammed down): fades quickly
        float sh = shakeUntil - Time.time;
        var jolt = sh > 0 ? new Vector3(Mathf.Sin(Time.time * 61f), Mathf.Sin(Time.time * 47f + 1f), 0) * sh * 0.5f : Vector3.zero;
        cam.transform.position = camPos + sway + jolt;
        cam.transform.LookAt(camLook + jolt * 0.5f);
    }

    float shakeUntil;
    /// The table shakes for a moment.
    public void Shake(float seconds) { if (!Perf.Saver) shakeUntil = Time.time + seconds; }

    /// A fixed shot (test portraits); null goes back to the normal camera.
    public void Shot(Vector3? pos, Vector3 look = default)
    {
        if (pos == null) { Focus(focus); return; }
        camPos = camPosTo = pos.Value; camLook = camLookTo = look; drift = 0;
        if (ready) Apply();
    }
    /// The camera straight to where it is heading (no glide), e.g. after filming elsewhere.
    public void Snap() { camPos = camPosTo; camLook = camLookTo; drift = 0; if (ready) Apply(); }
    public IEnumerable<Person> Regulars() { foreach (var r in regulars) if (r != null) { var p = r.GetComponent<Person>(); if (p != null) yield return p; } }

    /// Flies in from close to the floor when the splash opens.
    public void Entrance()
    {
        camPos = camPosTo + Vector3.up * 6f + front * 10f;
    }
}
