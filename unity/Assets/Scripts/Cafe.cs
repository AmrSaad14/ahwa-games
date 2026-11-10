// The café: the room and its five game tables, laid out like the five on a die (dominoes, tawla, chess, ludo in
// the corners, estimation in the middle; the room model only has the floor, walls and counter, see
// tools/cafe-declutter.py), warm lights, the regulars at their tables, and the camera that looks over the floor or
// moves in on one table.
using System.Collections.Generic;
using UnityEngine;

public class Cafe : MonoBehaviour
{
    public static readonly string[] Games = { "domino", "tawla", "chess", "ludo", "estimation" };
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
        if (tables.ContainsKey("domino") && tables.ContainsKey("tawla") && tables.ContainsKey("chess") && tables.ContainsKey("ludo"))
        {
            var backMid = (tables["domino"].position + tables["tawla"].position) * 0.5f;
            var frontMid = (tables["chess"].position + tables["ludo"].position) * 0.5f;
            front = frontMid - backMid; front.y = 0; front.Normalize();
            center = (backMid + frontMid) * 0.5f;
            Layout(room.transform);
        }
        else { center = Vector3.zero; front = Vector3.forward; }
        Debug.Log($"[cafe] room loaded, {tables.Count} tables, {room.GetComponentsInChildren<Renderer>().Length} renderers");

        yield return null;
        SettleProps(room.transform);
        Batch(room.transform);
        Lights();
        yield return null;
        foreach (var _ in People(room.transform)) yield return null;

        camPos = camPosTo = Overview(out camLook); camLookTo = camLook;
        Apply();
        ready = true;
        if (probeRes > 0) Reflections(probeRes);
    }

    /// The five tables like the five on a die, well apart (no table in front of another, from any screen): the
    /// four game tables of the model move to the corners, and a fifth (a copy of the domino table with four
    /// chairs, the cards of a game of estimation on its cloth) stands in the middle.
    void Layout(Transform room)
    {
        var side = tables["tawla"].position - tables["domino"].position; side.y = 0; side.Normalize();
        const float W = 14f, D = 8.5f;   // half the distance between the corner tables: across, and front to back
        void Move(string g, Vector3 to)
        {
            var t = tables[g]; var by = to - t.position; by.y = 0;
            t.position += by;
            var b = Find(room, "Board_" + g); if (b != null) b.position += by;
        }
        Move("domino", center - side * W - front * D);
        Move("tawla", center + side * W - front * D);
        Move("chess", center - side * W + front * D);
        Move("ludo", center + side * W + front * D);
        // the estimation table: the domino table copied, its two chairs and two more across the other sides
        var src = tables["domino"];
        var est = Instantiate(src.gameObject, src.parent).transform;
        est.name = "Table_estimation";
        est.position = new Vector3(center.x, src.position.y, center.z); est.rotation = src.rotation; est.localScale = src.localScale;
        Transform s0 = null, s1 = null;
        foreach (Transform c in est) { if (c.name == "Seat_domino_0") s0 = c; else if (c.name == "Seat_domino_1") s1 = c; }
        if (s0 != null && s1 != null)
        {
            s0.name = "Seat_estimation_0"; s1.name = "Seat_estimation_2";
            var s2 = Instantiate(s0.gameObject, est).transform; s2.name = "Seat_estimation_1";
            s2.position = s0.position; s2.rotation = s0.rotation; s2.RotateAround(est.position, Vector3.up, 90f);
            var s3 = Instantiate(s1.gameObject, est).transform; s3.name = "Seat_estimation_3";
            s3.position = s1.position; s3.rotation = s1.rotation; s3.RotateAround(est.position, Vector3.up, 90f);
        }
        tables["estimation"] = est;
        // on its cloth: a trick in the middle and four hands face down, one at each chair
        var board = new GameObject("Board_estimation").transform;
        board.SetParent(src.parent, false);
        board.position = est.position;
        var felt = GameObject.CreatePrimitive(PrimitiveType.Quad);
        felt.name = "Felt"; Destroy(felt.GetComponent<Collider>());
        felt.transform.SetParent(board, false);
        felt.transform.localRotation = Quaternion.Euler(90, 0, 0);
        felt.transform.localScale = new Vector3(4.8f, 4.8f, 1);
        felt.GetComponent<Renderer>().sharedMaterial = Art.Mat(new Color(0.09f, 0.33f, 0.2f), 0.15f);
        var back = Resources.Load<Texture2D>("UI/Cards/back");
        var backMat = back != null ? Art.Mat(Color.white, 0.3f, 0, back) : Art.Mat(new Color(0.55f, 0.1f, 0.12f), 0.3f);
        void Card(Material m, Vector3 at, float turn, int layer)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Card"; Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(board, false);
            q.transform.localPosition = at + Vector3.up * (0.012f + layer * 0.006f);
            q.transform.localRotation = Quaternion.Euler(90, turn, 0);
            q.transform.localScale = new Vector3(0.62f, 0.87f, 1);
            q.GetComponent<Renderer>().sharedMaterial = m;
        }
        int[] trick = { 0, 25, 37, 51 };   // the ace of spades, the king of hearts, the queen of diamonds, the king of clubs
        for (int i = 0; i < 4; i++)
        {
            var dir = Quaternion.Euler(0, 90 * i, 0) * Vector3.forward;
            var tex = Resources.Load<Texture2D>("UI/Cards/c" + trick[i]);
            Card(tex != null ? Art.Mat(Color.white, 0.3f, 0, tex) : backMat, dir * 0.55f, 90 * i + (i % 2 == 0 ? 8 : -6), 1);
            for (int k = 0; k < 3; k++) Card(backMat, dir * 1.75f + Quaternion.Euler(0, 90 * i, 0) * Vector3.right * (k - 1) * 0.28f, 90 * i + (k - 1) * 9, k);
        }
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

    /// The room never moves: its pieces are drawn together in a few big batches instead of one by one (much less
    /// work for the phone each frame). The model chair is left out (it's copied for the big table), and so is any
    /// mesh the importer kept unreadable.
    void Batch(Transform room)
    {
        var gos = new List<GameObject>();
        foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            if (chairModel != null && mf.transform.IsChildOf(chairModel)) continue;
            if (mf.GetComponent<MeshRenderer>() == null) continue;
            gos.Add(mf.gameObject);
        }
        if (gos.Count > 1)
        {
            try { StaticBatchingUtility.Combine(gos.ToArray(), room.gameObject); Debug.Log($"[cafe] {gos.Count} pieces batched"); }
            catch (System.Exception e) { Debug.LogWarning("[cafe] batching: " + e.Message); }
        }
        // a light's cost is counted per object it touches: the lamps light only what's under them
        roomBounds = default; bool any = false;
        foreach (var r in room.GetComponentsInChildren<Renderer>()) { if (!any) { roomBounds = r.bounds; any = true; } else roomBounds.Encapsulate(r.bounds); }
    }
    Bounds roomBounds;

    ReflectionProbe probe;
    int probeRes = -1;
    /// The café reflected in the glossy things (pieces, tiles, glasses): one picture of the room taken once (and
    /// again when the big table comes in), at the resolution the phone's level allows; none on the cheapest.
    public void Reflections(int res)
    {
        if (!ready && res > 0) { probeRes = res; return; }
        probeRes = res;
        if (res <= 0)
        {
            if (probe != null) probe.enabled = false;
            RenderSettings.reflectionIntensity = 0.35f;   // no café picture: keep the default sky's reflection faint
            return;
        }
        RenderSettings.reflectionIntensity = 1f;
        if (probe == null)
        {
            probe = new GameObject("Café reflections").AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;   // spread over a few frames: no hitch
            probe.boxProjection = true;
            probe.hdr = false;
            probe.shadowDistance = 0;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.12f, 0.08f, 0.05f);
            probe.importance = 1;
        }
        probe.enabled = true;
        var b = roomBounds.size == Vector3.zero ? new Bounds(center, Vector3.one * 80) : roomBounds;
        probe.transform.position = center + Vector3.up * 4f;
        probe.size = b.size + Vector3.one * 2f;
        probe.center = b.center - probe.transform.position;
        probe.resolution = res;
        probe.RenderProbe();
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

    /// The regulars in the background: all of them on good phones, fewer (or none) on cheap ones, none during a game.
    public void Crowd()
    {
        int n = Perf.CrowdSize;
        for (int i = 0; i < regulars.Count; i++) if (regulars[i] != null) regulars[i].SetActive(!inGame && i < n);
    }

    System.Collections.Generic.IEnumerable<int> People(Transform room)
    {
        foreach (var (who, table, seat) in Seating)
        {
            if (regulars.Count >= Perf.CrowdSize) yield break;   // cheap phones: fewer people, less memory
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

    /// The café from above: all four tables (with their chairs and the people at them) fitted into the part of the
    /// screen the home buttons leave free, on any screen shape (long phones, short phones, tablets, split screen).
    /// The camera looks down steeply enough that the two back tables never hide behind the two front ones.
    Vector3 Overview(out Vector3 look)
    {
        if (cam == null || tables.Count < 5)
        {
            float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
            float far = Mathf.Max(1f, 1.75f / aspect);
            look = center + Vector3.down * 2f - front * 3.2f;
            return center + Vector3.up * 23f * far + front * 25f * far;
        }
        // the free part of the screen (as a share of it): the home menu is laid out on an 844 × 390 canvas that
        // grows to fill the screen ("Expand"), so its buttons' share of the width depends on the screen's shape
        float ui = Mathf.Min(Screen.width / 844f, Screen.height / 390f), w = Mathf.Max(1, Screen.width);
        float left = Mathf.Min(0.2f, 84 * ui / w), right = Mathf.Min(0.35f, 196 * ui / w), top = 0.2f, bottom = 0.1f;
        const float el = 56f;   // degrees above the floor
        float e = el * Mathf.Deg2Rad;
        var dir = front * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        var side = Vector3.Cross(Vector3.up, front);
        // each table with its chairs: a box around it, up to the heads of the people sitting there
        var pts = new List<Vector3>();
        foreach (var kv in tables)
        {
            var t = kv.Value; float hz = kv.Key == "estimation" ? 5f : 4.2f;   // chairs at its front and back too
            foreach (float y in new[] { 0f, 2.6f })
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        pts.Add(new Vector3(t.position.x, center.y, t.position.z) + side * 5f * sx + front * hz * sz + Vector3.up * y);
        }
        var tr = cam.transform; var p0 = tr.position; var r0 = tr.rotation;
        Vector3 aim = center;
        float d = 40f;
        for (int it = 0; it < 8; it++)
        {
            // as close as everything still fits (binary search on the distance)
            float a = 5f, b = 200f;
            for (int k = 0; k < 24; k++)
            {
                float m = (a + b) / 2;
                tr.position = aim + dir * m; tr.LookAt(aim);
                bool fits = true;
                foreach (var q in pts)
                {
                    var v = cam.WorldToViewportPoint(q);
                    if (v.z <= 0 || v.x < left || v.x > 1 - right || v.y < bottom || v.y > 1 - top) { fits = false; break; }
                }
                if (fits) b = m; else a = m;
            }
            d = b;
            tr.position = aim + dir * d; tr.LookAt(aim);
            // then move the aim so the tables sit in the middle of the free part
            float lx = 1, hx = 0, ly = 1, hy = 0;
            foreach (var q in pts) { var v = cam.WorldToViewportPoint(q); lx = Mathf.Min(lx, v.x); hx = Mathf.Max(hx, v.x); ly = Mathf.Min(ly, v.y); hy = Mathf.Max(hy, v.y); }
            float wantX = left + (1 - left - right) / 2, wantY = bottom + (1 - top - bottom) / 2;
            float dx = wantX - (lx + hx) / 2, dy = wantY - (ly + hy) / 2;
            if (Mathf.Abs(dx) < 0.004f && Mathf.Abs(dy) < 0.004f) break;
            // the screen's height at this distance, in world units (roughly); `side` points to the screen's left,
            // so tables too far left (dx > 0) move the aim left, and tables too low (dy > 0) bring it nearer
            float span = 2f * d * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2);
            aim += side * dx * span * cam.aspect;
            aim += front * dy * span / Mathf.Max(0.3f, Mathf.Sin(e));
        }
        tr.position = p0; tr.rotation = r0;
        look = aim;
        return aim + dir * d;
    }

    int lastW, lastH;
    /// The screen changed shape (turned, split screen, a foldable opened): frame the café again.
    void Reframe()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        bool first = lastW == 0;
        lastW = Screen.width; lastH = Screen.height;
        if (first || inGame) return;
        camPosTo = focus == null ? Overview(out camLookTo) : TableShot(focus, out camLookTo);
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
        gameKind = g; ResetZoom();
        if (inGame) camPosTo = GameShot(out camLookTo);
    }

    public void SetGame(bool on)
    {
        inGame = on;
        if (lobby != null) lobby.gameObject.SetActive(!on);
        Crowd();
        GameLight(on); ResetZoom();
        if (on) camPosTo = GameShot(out camLookTo); else Focus(focus);
        if (probe != null && probe.enabled) probe.RenderProbe();   // the room changed (the big table in, or out)
    }

    // ---------- the light while a game is on: even, neutral, from above, with no glare ----------
    Quaternion keyRot; Color keyColor; float keyIntensity, keyShadow; bool savedLight;
    Color ambSky, ambEq, ambGround;
    readonly Dictionary<Light, float> lamps = new Dictionary<Light, float>();

    void GameLight(bool on)
    {
        if (keyLight == null) return;
        if (!savedLight)
        {
            savedLight = true;
            keyRot = keyLight.transform.rotation; keyColor = keyLight.color; keyIntensity = keyLight.intensity; keyShadow = keyLight.shadowStrength;
            ambSky = RenderSettings.ambientSkyColor; ambEq = RenderSettings.ambientEquatorColor; ambGround = RenderSettings.ambientGroundColor;
        }
        // every lamp, the one over the game table too (made hidden with it), with its own brightness
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (l.type == LightType.Point && !lamps.ContainsKey(l)) lamps[l] = l.intensity;
        if (on)
        {
            // the café keeps its own warm light all around; only the light falling on the board is softened:
            // the sun from nearly straight down (short, soft shadows) and a little weaker, the lamp right above the board dimmed
            keyLight.transform.rotation = Quaternion.LookRotation(Vector3.down * 4f - front * 0.6f + Vector3.Cross(Vector3.up, front) * 0.35f);
            keyLight.intensity = keyIntensity * 0.75f;
            keyLight.shadowStrength = 0.5f;
            foreach (var kv in lamps)
            {
                var l = kv.Key; float i = kv.Value;
                if (l == null) continue;
                var p = l.transform.position; p.y = 0;
                l.intensity = p.magnitude < 5f ? i * 0.25f : i;   // no hot spot in the middle of the board
            }
        }
        else
        {
            keyLight.transform.rotation = keyRot; keyLight.color = keyColor; keyLight.intensity = keyIntensity; keyLight.shadowStrength = keyShadow;
            foreach (var kv in lamps) if (kv.Key != null) kv.Key.intensity = kv.Value;
        }
        // the camera's look: the same warm café, without the glow that washed out the white tiles and squares
        var look = cam != null ? cam.GetComponent<CafeLook>() : null;
        if (look != null)
        {
            look.bloom = on ? 0.08f : 0.35f; look.threshold = on ? 1.8f : 1.1f;
            look.exposure = on ? 0.9f : 0.95f;
        }
    }

    /// Straight down on the board (a few degrees from your side, so 3D pieces keep their shape), framed between
    /// the top bar and the bottom of the screen.
    Vector3 GameShot(out Vector3 look)
    {
        // each board: its half width and half depth (world units), how tall its pieces stand, the camera's angle
        // above the table (like sitting at it: the pieces look 3D), and the parts of the screen the game's own
        // buttons take (top bar; your tiles at the bottom in dominoes; the side buttons)
        // (the boards' real sizes: chess 9.2 + its frame, tawla 11.6 × 7.8, ludo 15 cells of 0.7 + its frame,
        //  dominoes the cloth and the tiles the others hold up at its edges)
        float hx, hz, tall, el, top = 0.1f, bottom, side = 0.075f;
        switch (gameKind)
        {
            case "chess": hx = 5.2f; hz = 5.2f; tall = 1.3f; el = 54f; bottom = 0.03f; break;
            case "tawla": hx = 5.85f; hz = 3.95f; tall = 0.5f; el = 58f; bottom = 0.04f; break;
            case "ludo": hx = 5.45f; hz = 5.45f; tall = 0.7f; el = 58f; bottom = 0.03f; break;
            default: hx = 5.5f; hz = 5.35f; tall = 0.8f; el = 58f; bottom = 0.21f; break;   // dominoes: your hand at the bottom
        }
        float e = el * Mathf.Deg2Rad;
        var dir = front * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
        // as close as the whole board still fits the free part of the screen, its middle in the middle of it:
        // found by trying the shot (the near edge looks bigger than the far one at an angle)
        var corners = new Vector3[8];
        var right = Vector3.Cross(Vector3.up, front);
        int c = 0;
        foreach (float y in new[] { 0f, tall })
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    corners[c++] = right * hx * sx + front * hz * sz + Vector3.up * y;
        var t = cam.transform; var p0 = t.position; var r0 = t.rotation;
        Vector3 aim = Vector3.up * 0.3f;
        float d = 20f;
        float lo = 0, hi = 0;
        for (int it = 0; it < 6; it++)
        {
            float a = 4f, b = 90f;
            for (int k = 0; k < 22; k++)
            {
                float m = (a + b) / 2;
                t.position = aim + dir * m; t.LookAt(aim);
                bool fits = true;
                foreach (var q in corners)
                {
                    var v = cam.WorldToViewportPoint(q);
                    if (v.z <= 0 || v.x < side || v.x > 1 - side || v.y < bottom || v.y > 1 - top) { fits = false; break; }
                }
                if (fits) b = m; else a = m;
            }
            d = b;
            t.position = aim + dir * d; t.LookAt(aim);
            lo = 1; hi = 0;
            foreach (var q in corners) { float vy = cam.WorldToViewportPoint(q).y; lo = Mathf.Min(lo, vy); hi = Mathf.Max(hi, vy); }
            float want = bottom + (1 - top - bottom) / 2, now = (lo + hi) / 2;
            if (Mathf.Abs(want - now) < 0.004f) break;
            // the board too low on the screen: aim a little nearer to you (it moves up), and the other way round
            aim += front * (want - now) * (2 * hz) / Mathf.Max(0.05f, hi - lo);
        }
        t.position = p0; t.rotation = r0;
        boardHalf = Mathf.Max(hx, hz); boardDist = d;
        look = Vector3.zero + (aim - Vector3.zero) * zoom + Vector3.up * 0.3f * (1 - zoom) + pan;
        return look + dir * (d * zoom);
    }

    // ---------- zoom: two fingers pinch in and out (the mouse wheel on a computer), and move the board while close ----------
    const float ZoomMin = 0.4f, ZoomMax = 1.25f;
    float zoom = 1f, boardHalf = 5.5f, boardDist = 20f;
    Vector3 pan;
    float pinchFrom; Vector2 pinchMid; bool pinching;

    void ResetZoom() { zoom = 1f; pan = Vector3.zero; pinching = false; }

    void ZoomInput()
    {
        if (!inGame || cam == null) return;
        bool moved = false;
        if (Input.touchCount >= 2)
        {
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            float dist = Mathf.Max(1f, (a.position - b.position).magnitude);
            Vector2 mid = (a.position + b.position) * 0.5f;
            if (!pinching)
            {
                // a pinch that starts on a menu or button belongs to it, not to the board
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && (es.IsPointerOverGameObject(a.fingerId) || es.IsPointerOverGameObject(b.fingerId))) return;
                pinching = true; pinchFrom = dist; pinchMid = mid;
            }
            else
            {
                zoom = Mathf.Clamp(zoom * pinchFrom / dist, ZoomMin, ZoomMax);
                // the fingers' middle drags the board with them
                float perPixel = 2f * boardDist * zoom * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Mathf.Max(1, Screen.height);
                Vector2 dm = mid - pinchMid;
                var right = Vector3.Cross(front, Vector3.up);
                pan -= right * dm.x * perPixel;
                pan += front * dm.y * perPixel;
                pinchFrom = dist; pinchMid = mid; moved = true;
            }
        }
        else pinching = false;
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f) { zoom = Mathf.Clamp(zoom * Mathf.Pow(0.9f, wheel), ZoomMin, ZoomMax); moved = true; }
        if (!moved) return;
        // close up, the board can move as far as its edge; at the full view or farther it stays in the middle
        float reach = boardHalf * Mathf.Max(0f, 1f - zoom);
        pan.y = 0;
        if (pan.magnitude > reach) pan = pan.normalized * reach;
        camPosTo = GameShot(out camLookTo);
        camPos = camPosTo; camLook = camLookTo;   // follows the fingers at once
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
        Reframe();
        ZoomInput();
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
        // shadows only as far as the camera looks (plus a margin): the one shadow map stays sharp where it matters
        float sd = Vector3.Distance(camPos, camLook) + 28f;
        if (Mathf.Abs(sd - QualitySettings.shadowDistance) > 3f) QualitySettings.shadowDistance = sd;
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
