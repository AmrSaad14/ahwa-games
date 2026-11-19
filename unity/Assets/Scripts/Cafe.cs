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
        DressTables();   // (before the layout: the estimation table is a copy of the domino one, so it gets it too)
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
        Props(room.transform);
        Sign(room.transform);
        Door(room.transform);
        yield return null;
        foreach (var _ in People(room.transform)) yield return null;

        Perf.Compress(room);   // (the room's pictures, the tables and the door: less memory on phones)
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

    /// The four game tables in a real wooden café table (Poly Haven's Wooden Table 02, CC0): the model's own table
    /// is hidden and the new one stands in its place, as tall as it and with a top as wide, so the boards, the
    /// chairs and the regulars stay just where they were.
    void DressTables()
    {
        var model = Resources.Load<GameObject>("Art/WoodTable");
        if (model == null) { Debug.LogWarning("[cafe] no Art/WoodTable"); return; }
        foreach (var g in Games)
        {
            if (!tables.TryGetValue(g, out var t)) continue;
            var own = new List<Renderer>();
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                bool skip = false;
                for (var p = r.transform; p != null && p != t; p = p.parent) if (p.name.StartsWith("Seat_") || p.name.StartsWith("Board_")) { skip = true; break; }
                if (!skip) own.Add(r);
            }
            if (own.Count == 0) continue;
            Bounds all = own[0].bounds, top = own[0].bounds; float area = 0;
            foreach (var r in own)
            {
                all.Encapsulate(r.bounds);
                float a = r.bounds.size.x * r.bounds.size.z;
                if (a > area) { area = a; top = r.bounds; }
            }
            var nt = Instantiate(model);
            nt.name = "WoodTable";
            var rs = nt.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Destroy(nt); continue; }
            Bounds Measure() { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
            var mb = Measure();
            if (mb.size.y < 1e-4f || mb.size.x < 1e-4f || mb.size.z < 1e-4f) { Destroy(nt); continue; }
            var s0 = nt.transform.localScale;
            nt.transform.localScale = new Vector3(s0.x * top.size.x / mb.size.x, s0.y * all.size.y / mb.size.y, s0.z * top.size.z / mb.size.z);
            mb = Measure();
            nt.transform.position += new Vector3(all.center.x - mb.center.x, all.min.y - mb.min.y, all.center.z - mb.center.z);
            nt.transform.SetParent(t, true);
            foreach (var r in rs) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
            foreach (var r in own) { if (r.transform == t) r.enabled = false; else r.gameObject.SetActive(false); }
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
            tableTop[g] = tb.max.y;
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
            hanging.Add((l, l.transform.position));
        }
        keyBase = sun.color; keyIBase = sun.intensity;
        skyBase = RenderSettings.ambientSkyColor; eqBase = RenderSettings.ambientEquatorColor; groundBase = RenderSettings.ambientGroundColor;
    }

    // ---------- the café's own things: brass lanterns along the back wall, a shisha by the back tables ----------
    readonly List<(Material m, Color c)> lanternGlass = new List<(Material, Color)>();
    void Props(Transform room)
    {
        if (!tables.ContainsKey("domino") || !tables.ContainsKey("tawla")) return;
        var side = tables["tawla"].position - tables["domino"].position; side.y = 0; side.Normalize();
        var brass = Art.Mat(Art.Hex("#b8893a"), 0.72f, 0.9f);
        var sh = Shader.Find("Sprites/Default");
        // the lanterns (fawanees): hanging in a row behind the back tables, high up, their coloured glass glowing
        Color[] glass = { new Color(2.2f, 1.25f, 0.45f), new Color(2.0f, 0.55f, 0.35f), new Color(0.7f, 1.6f, 0.9f), new Color(2.2f, 1.25f, 0.45f) };
        float[] across = { -1.5f, -0.5f, 0.5f, 1.5f };
        float back = Vector3.Dot(center - tables["domino"].position, front) + 5.5f;   // behind the back tables, before the wall
        for (int i = 0; i < 4; i++)
        {
            var at = new Vector3(center.x, 0, center.z) + side * across[i] * 9.5f - front * back + Vector3.up * (6.5f + (i % 2) * 0.8f);
            var g = Lantern(room, at, brass, sh != null ? new Material(sh) { color = glass[i] } : null, 1.5f);
            if (g != null) lanternGlass.Add((g, glass[i]));
        }
        // a shisha on the floor behind each back table, on its outer side
        if (lobby == null) return;
        foreach (var (g, dir) in new[] { ("domino", -1f), ("tawla", 1f) })
        {
            var at = tables[g].position - front * 4.6f + side * dir * 2.6f;
            at.y = Person.FloorY;
            Shisha(lobby, at, brass, sh, dir > 0 ? new Color(0.16f, 0.45f, 0.42f) : new Color(0.5f, 0.14f, 0.2f));
        }
    }

    /// The game's logo and name on the café's back wall, between the back tables: a cut-out plaque standing off the
    /// wall, its edge given depth by layers of darker brass behind the picture.
    void Sign(Transform room)
    {
        var tex = Resources.Load<Texture2D>("UI/Logo");
        var sh = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Sprites/Default");
        if (tex == null || sh == null) return;
        // the back wall: the room's widest thin piece behind the tables, facing them
        var side = Vector3.Cross(Vector3.up, front);
        float wall = -1; float wallBottom = Person.FloorY;
        foreach (var r in room.GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            float thick = Mathf.Abs(Vector3.Dot(b.size, front)), wide = Mathf.Abs(Vector3.Dot(b.size, side));
            float back = Vector3.Dot(center - b.center, front);
            if (thick < 1.2f && wide > 25f && b.size.y > 8f && back > 12f && (wall < 0 || back < wall)) { wall = back - thick / 2; wallBottom = b.min.y; }
        }
        if (wall < 0) { Debug.LogWarning("[cafe] no back wall for the sign"); return; }
        var root = new GameObject("Sign").transform;
        signT = root;
        root.SetParent(room, true);
        float h = 5.6f, w = h * tex.width / tex.height;
        root.position = new Vector3(center.x, wallBottom + 4.6f, center.z) - front * (wall - 0.12f);
        root.rotation = Quaternion.LookRotation(-front, Vector3.up);   // its face toward the room
        for (int k = 6; k >= 0; k--)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = k == 0 ? "Logo" : "Edge"; Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false);
            q.transform.localScale = new Vector3(w, h, 1);
            q.transform.localPosition = new Vector3(0, 0, k * 0.035f);   // (behind the face, toward the wall)
            // (a quad shows its face toward -z: here, the room)
            float d = k == 0 ? 1f : 0.32f + 0.04f * (6 - k);
            var m = new Material(sh) { mainTexture = tex, color = k == 0 ? Color.white : new Color(0.62f * d * 1.4f, 0.45f * d * 1.4f, 0.16f * d * 1.4f, 1) };
            m.renderQueue = 3000 + (6 - k);   // drawn back to front
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = k == 6 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// The café's door: Poly Haven's Large Castle Door (CC0, an old studded wooden double door), standing in the
    /// back wall where the room model's old doorway was (taken out of the model; its spot is the "Doorway" node).
    void Door(Transform room)
    {
        var model = Resources.Load<GameObject>("Art/CafeDoor");
        var spot = Find(room, "Doorway");
        if (model == null || spot == null) { Debug.LogWarning("[cafe] no door or no doorway"); return; }
        var door = Instantiate(model);
        door.name = "Door";
        door.transform.rotation = Quaternion.LookRotation(front, Vector3.up);   // its face to the room
        var ds = door.GetComponentsInChildren<Renderer>();
        if (ds.Length == 0) { Destroy(door); return; }
        Bounds Measure() { var b = ds[0].bounds; foreach (var r in ds) b.Encapsulate(r.bounds); return b; }
        var mb = Measure();
        door.transform.localScale *= 8.4f / Mathf.Max(0.01f, mb.size.y);   // the old doorway's height, and its frame
        mb = Measure();
        var at = spot.position;
        door.transform.position += new Vector3(at.x - mb.center.x, at.y - mb.min.y, at.z - mb.center.z) + front * (Mathf.Abs(Vector3.Dot(mb.size, front)) / 2 + 0.06f);
        door.transform.SetParent(room, true);
        doorT = door.transform;
        foreach (var r in ds) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; r.receiveShadows = true; }
    }

    Transform doorT, signT;

    /// A new player's first look round the café: in at the door, the sign with the game's name, over the tables,
    /// then the whole café. A tap anywhere cuts it short.
    public System.Collections.IEnumerator Tour()
    {
        if (!ready) yield break;
        var stops = new List<(Vector3 pos, Vector3 look, float wait)>();
        Vector3 Before(Transform t, float back, float up) { var c = t.position + Vector3.up * 1.5f; return c + front * back + Vector3.up * up; }
        if (doorT != null) stops.Add((Before(doorT, 13f, 3.5f), doorT.position + Vector3.up * 3.6f, 2.2f));
        if (signT != null) stops.Add((Before(signT, 12f, 2.5f), signT.position, 2.4f));
        foreach (var g in new[] { "domino", "estimation", "chess" })
            if (tables.ContainsKey(g)) { var p = TableShot(g, out var l); stops.Add((p, l, 1.9f)); }
        fixedShot = true;
        bool skip = false;
        foreach (var (pos, look, wait) in stops)
        {
            camPosTo = pos; camLookTo = look;
            for (float t = 0; t < wait; t += Time.deltaTime)
            {
                if (Input.GetMouseButtonDown(0) || Input.touchCount > 0) { skip = true; break; }
                yield return null;
            }
            if (skip) break;
        }
        fixedShot = false;
        Focus(null);
    }

    /// A brass lantern, its glass (a hexagon, wider in the middle) glowing; returns the glass's material.
    static Material Lantern(Transform parent, Vector3 at, Material brass, Material glassMat, float k)
    {
        var root = new GameObject("Lantern").transform;
        root.SetParent(parent, true); root.position = at; root.localScale = Vector3.one * k;
        if (glassMat != null)
        {
            var g = Art.Obj("Glass", root, Art.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.3f, 0.02f), new Vector2(0.46f, 0.55f), new Vector2(0.34f, 1.1f), new Vector2(0.001f, 1.12f) }, 6), glassMat, false);
            g.transform.localPosition = Vector3.zero;
        }
        // the frame: a ring round the waist, a crown on top with its domed cap, a drop below, the chain up
        var waist = Art.Obj("Ring", root, Art.Lathe(new[] { new Vector2(0.47f, 0.5f), new Vector2(0.5f, 0.55f), new Vector2(0.47f, 0.6f) }, 6), brass, false);
        var crown = Art.Obj("Crown", root, Art.Lathe(new[] { new Vector2(0.4f, 1.05f), new Vector2(0.5f, 1.18f), new Vector2(0.36f, 1.3f), new Vector2(0.18f, 1.55f), new Vector2(0.05f, 1.75f), new Vector2(0.001f, 1.8f) }, 12), brass, false);
        var drop = Art.Obj("Drop", root, Art.Lathe(new[] { new Vector2(0.001f, -0.45f), new Vector2(0.08f, -0.3f), new Vector2(0.22f, -0.08f), new Vector2(0.32f, 0.02f), new Vector2(0.001f, 0.03f) }, 12), brass, false);
        var chain = Art.Obj("Chain", root, Art.Prim(PrimitiveType.Cylinder), brass, false);
        chain.transform.localScale = new Vector3(0.04f, 4f, 0.04f); chain.transform.localPosition = Vector3.up * 5.8f;
        return glassMat;
    }

    /// A shisha: a coloured glass base, a brass stem with its tray and clay bowl (a coal glowing on top), the hose.
    static void Shisha(Transform parent, Vector3 at, Material brass, Shader glow, Color baseColor)
    {
        var root = new GameObject("Shisha").transform;
        root.SetParent(parent, true); root.position = at; root.localScale = Vector3.one * 1.15f;
        var vase = Art.Mat(baseColor, 0.9f, 0.1f);
        Art.Obj("Base", root, Art.Lathe(new[] { new Vector2(0.001f, 0), new Vector2(0.36f, 0.02f), new Vector2(0.6f, 0.18f), new Vector2(0.7f, 0.45f), new Vector2(0.66f, 0.75f), new Vector2(0.46f, 1.05f), new Vector2(0.22f, 1.25f), new Vector2(0.15f, 1.4f), new Vector2(0.001f, 1.41f) }, 28), vase);
        Art.Obj("Stem", root, Art.Lathe(new[] { new Vector2(0.11f, 1.35f), new Vector2(0.13f, 1.6f), new Vector2(0.07f, 1.8f), new Vector2(0.09f, 2.2f), new Vector2(0.06f, 2.6f), new Vector2(0.14f, 2.75f), new Vector2(0.06f, 2.9f), new Vector2(0.07f, 3.3f), new Vector2(0.001f, 3.31f) }, 16), brass);
        Art.Obj("Tray", root, Art.Lathe(new[] { new Vector2(0.001f, 2.95f), new Vector2(0.55f, 2.97f), new Vector2(0.62f, 3.04f), new Vector2(0.001f, 3.0f) }, 24), brass);
        var clay = Art.Mat(new Color(0.55f, 0.24f, 0.12f), 0.3f);
        Art.Obj("Bowl", root, Art.Lathe(new[] { new Vector2(0.06f, 3.25f), new Vector2(0.22f, 3.5f), new Vector2(0.24f, 3.62f), new Vector2(0.001f, 3.63f) }, 16), clay);
        if (glow != null)
        {
            var coal = Art.Obj("Coal", root, Art.Lathe(new[] { new Vector2(0.001f, 3.62f), new Vector2(0.14f, 3.64f), new Vector2(0.1f, 3.72f), new Vector2(0.001f, 3.74f) }, 10), new Material(glow) { color = new Color(2.4f, 0.7f, 0.2f) }, false);
        }
        // the hose: from the stem's side, down in a loop, its mouthpiece resting on the base
        var hose = Art.Mat(new Color(0.22f, 0.12f, 0.08f), 0.35f);
        Vector3 p0 = new Vector3(0.12f, 2.4f, 0), p1 = new Vector3(1.3f, 1.6f, 0.3f), p2 = new Vector3(1.2f, 0.2f, 0.8f), p3 = new Vector3(0.45f, 0.85f, 0.7f);
        Vector3 prev = p0;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f, u = 1 - t;
            var q = u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3;
            var seg = Art.Obj("Hose", root, Art.Prim(PrimitiveType.Cylinder), hose, false);
            seg.transform.localPosition = (prev + q) / 2;
            seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, q - prev);
            seg.transform.localScale = new Vector3(0.1f, (q - prev).magnitude / 2 + 0.02f, 0.1f);
            prev = q;
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
            // their glass of tea on the table in front of them, a little to their right, steaming
            var toChair = chair.position - t.position; toChair.y = 0; toChair.Normalize();
            var at = t.position + toChair * 2.5f + Vector3.Cross(Vector3.up, toChair) * 0.95f;
            at.y = tableTop.TryGetValue(table, out var ty) ? ty : t.position.y;
            var glass = Look.Drink(seat % 2 == 0 ? "drink_tea" : "drink_mint", t, at, 1.1f);
            if (glass != null)
            {
                float ls = Mathf.Abs(t.lossyScale.x); if (ls > 1e-4f) glass.transform.localScale = Vector3.one * (1.1f / ls);   // the same size on any table
                Steam(at + Vector3.up * 0.75f);
            }
            sitters.Add((person, at, table));
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
        // (the rewards and the shop down the left; your card at the top right; the big button and the games along the bottom)
        float left = Mathf.Min(0.2f, 84 * ui / w), right = Mathf.Min(0.1f, 24 * ui / w), top = Mathf.Min(0.3f, 84 * ui / Mathf.Max(1, Screen.height));
        float bottom = Mathf.Min(0.32f, 94 * ui / Mathf.Max(1, Screen.height));
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
        if (first) return;
        if (inGame) { camPosTo = GameShot(out camLookTo); return; }   // (ludo turns the phone upright)
        ResetLobbyZoom();
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
        GameLight(on); ResetZoom(); ResetLobbyZoom();
        if (on) camPosTo = GameShot(out camLookTo); else { Focus(focus); moodSnap = true; }
        if (probe != null && probe.enabled) probe.RenderProbe();   // the room changed (the big table in, or out)
    }

    // ---------- the light while a game is on: even, neutral, from above, with no glare ----------
    Quaternion keyRot; Color keyColor; float keyIntensity, keyShadow; bool savedLight;
    Color ambSky, ambEq, ambGround;
    readonly Dictionary<Light, float> lamps = new Dictionary<Light, float>();

    void GameLight(bool on)
    {
        if (keyLight == null) return;
        ResetMood();   // the game's light is the same at any hour: it starts from the café's own
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
            // at the table, the café floor around the board falls away into shadow, so the eye stays on the game
            look.vignette = on ? 0.9f : 0.5f;
            look.saturation = on ? 0.98f : 1.08f;
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
        // held upright (ludo): the buttons and the names take rows above and below the board instead of its sides
        if (TallCanvas.Upright) { top = 0.2f; bottom = 0.19f; side = 0.03f; }
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

    // the café floor (the lobby): two fingers (or the wheel) bring the camera nearer or farther and move it
    const float LobbyMin = 0.35f, LobbyMax = 1.15f;
    float lobbyZoom = 1f; Vector3 lobbyPan, lobbyBase, lobbyLook; bool lobbyHasBase;
    void ResetLobbyZoom() { lobbyZoom = 1f; lobbyPan = Vector3.zero; lobbyHasBase = false; }

    void LobbyZoomInput()
    {
        bool moved = false;
        if (Input.touchCount >= 2)
        {
            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
            float dist = Mathf.Max(1f, (a.position - b.position).magnitude);
            Vector2 mid = (a.position + b.position) * 0.5f;
            if (!pinching)
            {
                // a pinch that starts on a menu or button belongs to it
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null && (es.IsPointerOverGameObject(a.fingerId) || es.IsPointerOverGameObject(b.fingerId))) return;
                pinching = true; pinchFrom = dist; pinchMid = mid;
            }
            else
            {
                lobbyZoom = Mathf.Clamp(lobbyZoom * pinchFrom / dist, LobbyMin, LobbyMax);
                float far = Vector3.Distance(camPosTo, camLookTo);
                float perPixel = 2f * far * Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2) / Mathf.Max(1, Screen.height);
                Vector2 dm = mid - pinchMid;
                var right = Vector3.Cross(front, Vector3.up);
                lobbyPan -= right * dm.x * perPixel;
                lobbyPan += front * dm.y * perPixel;
                pinchFrom = dist; pinchMid = mid; moved = true;
            }
        }
        else pinching = false;
        // the mouse wheel, unless the pointer is on a menu (its lists scroll with the wheel)
        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) > 0.01f)
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || !es.IsPointerOverGameObject()) { lobbyZoom = Mathf.Clamp(lobbyZoom * Mathf.Pow(0.9f, wheel), LobbyMin, LobbyMax); moved = true; }
        }
        if (!moved) return;
        if (!lobbyHasBase) { lobbyBase = focus == null ? Overview(out lobbyLook) : TableShot(focus, out lobbyLook); lobbyHasBase = true; }
        // close up, the view can move over the café; at the full view it stays put
        float reach = 14f * Mathf.Max(0f, 1f - lobbyZoom);
        lobbyPan.y = 0;
        if (lobbyPan.magnitude > reach) lobbyPan = lobbyPan.normalized * reach;
        camLookTo = lobbyLook + lobbyPan;
        camPosTo = camLookTo + (lobbyBase - lobbyLook) * lobbyZoom;
        camPos = camPosTo; camLook = camLookTo;   // follows the fingers at once
    }

    void ZoomInput()
    {
        if (cam == null) return;
        if (!inGame) { LobbyZoomInput(); return; }
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
        ResetLobbyZoom();
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
        Life();
    }

    // ---------- the café alive: its light following the hour in Cairo, the lamps swaying, the regulars ----------
    // ---------- sipping, slamming a tile, laughing, the steam rising from their glasses ----------
    /// The month of Ramadan is on (the event from the server): after sunset the café takes on its Ramadan night.
    public bool Ramadan;
    /// The hour to show instead of Cairo's (the test screenshots); below 0, the real one.
    public float ForceHour { get => forceHour; set { forceHour = value; moodSnap = true; } }
    float forceHour = -1;
    readonly Dictionary<string, float> tableTop = new Dictionary<string, float>();
    readonly List<(Light l, Vector3 at)> hanging = new List<(Light, Vector3)>();
    readonly List<(Person p, Vector3 glass, string table)> sitters = new List<(Person, Vector3, string)>();
    readonly List<(Transform t, Vector3 at, float phase, MeshRenderer r)> puffs = new List<(Transform, Vector3, float, MeshRenderer)>();
    Color keyBase, skyBase, eqBase, groundBase; float keyIBase;
    bool moodSnap = true; float nextMove = 4f;
    static readonly Color LampColor = new Color(1f, 0.72f, 0.42f);
    MaterialPropertyBlock puffBlock;

    struct Mood
    {
        public Color key, amb, lamp; public float keyI, lampI;
        public Mood(Color key, float keyI, Color amb, float lampI, Color? lamp = null) { this.key = key; this.keyI = keyI; this.amb = amb; this.lampI = lampI; this.lamp = lamp ?? LampColor; }
        public static Mood Lerp(Mood a, Mood b, float t) => new Mood(Color.Lerp(a.key, b.key, t), Mathf.Lerp(a.keyI, b.keyI, t), Color.Lerp(a.amb, b.amb, t), Mathf.Lerp(a.lampI, b.lampI, t), Color.Lerp(a.lamp, b.lamp, t));
    }
    Mood mood; bool hasMood;

    // the day in the café: the light through the door (the key light), the room's own light (a tint on the
    // ambient) and the lamps (stronger as it gets dark), at hours through the day
    static readonly (float h, Mood m)[] Day =
    {
        (0f,    new Mood(new Color(0.7f, 0.72f, 0.9f), 0.6f, new Color(0.78f, 0.78f, 0.88f), 1.4f)),   // night
        (5f,    new Mood(new Color(0.7f, 0.72f, 0.9f), 0.6f, new Color(0.78f, 0.78f, 0.88f), 1.4f)),
        (7f,    new Mood(new Color(1f, 0.92f, 0.8f), 1.05f, new Color(1.05f, 1.06f, 1.12f), 0.7f)),        // morning
        (12f,   new Mood(new Color(1f, 0.96f, 0.88f), 1.2f, new Color(1.15f, 1.15f, 1.17f), 0.55f)),       // noon
        (16f,   new Mood(new Color(1f, 0.9f, 0.74f), 1.12f, new Color(1.06f, 1.04f, 1.02f), 0.7f)),        // afternoon
        (17.6f, new Mood(new Color(1f, 0.8f, 0.6f), 0.95f, new Color(0.98f, 0.9f, 0.84f), 1.0f)),          // sunset
        (20f,   new Mood(new Color(1f, 0.86f, 0.66f), 1.0f, new Color(1f, 1f, 1f), 1.12f)),               // evening: the café's own look
        (23f,   new Mood(new Color(0.82f, 0.78f, 0.82f), 0.75f, new Color(0.8f, 0.8f, 0.92f), 1.35f)),     // late
        (24f,   new Mood(new Color(0.7f, 0.72f, 0.9f), 0.6f, new Color(0.78f, 0.78f, 0.88f), 1.4f)),
    };
    // Ramadan nights: deep blue and violet outside, the lamps glowing orange like lanterns
    static readonly Mood RamadanNight = new Mood(new Color(0.72f, 0.66f, 0.92f), 0.62f, new Color(0.84f, 0.76f, 0.94f), 1.6f, new Color(1f, 0.64f, 0.3f));

    /// The time in Cairo (Egypt: UTC+2, and UTC+3 in summer, from the last Friday of April to the last Thursday of October).
    public static float CairoHour()
    {
        var u = System.DateTime.UtcNow;
        System.DateTime Last(int month, System.DayOfWeek day)
        {
            var d = new System.DateTime(u.Year, month, System.DateTime.DaysInMonth(u.Year, month));
            while (d.DayOfWeek != day) d = d.AddDays(-1);
            return d;
        }
        bool summer = u >= Last(4, System.DayOfWeek.Friday).AddHours(-2) && u < Last(10, System.DayOfWeek.Thursday).AddHours(21);
        var c = u.AddHours(summer ? 3 : 2);
        return c.Hour + c.Minute / 60f;
    }

    Mood MoodAt(float h)
    {
        Mood m = Day[0].m;
        for (int i = 0; i + 1 < Day.Length; i++)
            if (h >= Day[i].h && h <= Day[i + 1].h) { m = Mood.Lerp(Day[i].m, Day[i + 1].m, Mathf.InverseLerp(Day[i].h, Day[i + 1].h, h)); break; }
        if (Ramadan)
        {
            // from sunset (fading in over half an hour) until the dawn meal
            float k = h >= 17.5f ? Mathf.Clamp01((h - 17.5f) / 0.5f) : h < 4f ? 1f : h < 4.5f ? 1f - (h - 4f) / 0.5f : 0f;
            m = Mood.Lerp(m, RamadanNight, k);
        }
        return m;
    }

    void ResetMood()
    {
        if (keyLight == null || hanging.Count == 0) return;
        keyLight.color = keyBase; keyLight.intensity = keyIBase;
        RenderSettings.ambientSkyColor = skyBase; RenderSettings.ambientEquatorColor = eqBase; RenderSettings.ambientGroundColor = groundBase;
        foreach (var (l, at) in hanging) if (l != null) { l.color = LampColor; l.intensity = 2.2f; l.transform.position = at; }
    }

    void Life()
    {
        if (inGame || AdFilm.Any || keyLight == null || hanging.Count == 0) return;
        // the light: drifts toward the hour's (or snaps, coming back from a game)
        var want = MoodAt(forceHour >= 0 ? forceHour : CairoHour());
        mood = !hasMood || moodSnap ? want : Mood.Lerp(mood, want, 1 - Mathf.Exp(-Time.deltaTime * 0.5f));
        hasMood = true; moodSnap = false;
        keyLight.color = mood.key; keyLight.intensity = keyIBase * mood.keyI;
        RenderSettings.ambientSkyColor = skyBase * mood.amb; RenderSettings.ambientEquatorColor = eqBase * mood.amb; RenderSettings.ambientGroundColor = groundBase * mood.amb;
        // the lamps, each swaying a little on its chain (its pool of light swinging over the table)
        float t = Time.time;
        var side = Vector3.Cross(Vector3.up, front);
        for (int i = 0; i < hanging.Count; i++)
        {
            var (l, at) = hanging[i]; if (l == null) continue;
            float a = Mathf.Sin(t * 0.9f + i * 1.7f), b = Mathf.Sin(t * 0.63f + i * 2.9f);
            l.transform.position = at + side * a * 0.45f + front * b * 0.3f;
            l.color = mood.lamp;
            l.intensity = 2.2f * mood.lampI * (1f + 0.025f * Mathf.Sin(t * 7.3f + i * 3.1f));
        }
        // the lanterns' glass glows brighter as the lamps do (strongest on Ramadan nights)
        foreach (var (m, c) in lanternGlass) if (m != null) { var k = Mathf.Lerp(0.55f, 1.15f, Mathf.InverseLerp(0.55f, 1.65f, mood.lampI)); m.color = new Color(c.r * k, c.g * k, c.b * k, 1); }
        // the steam over the glasses: soft puffs rising and fading, turned to the camera
        if (puffs.Count > 0 && cam != null)
        {
            if (puffBlock == null) puffBlock = new MaterialPropertyBlock();
            var rot = cam.transform.rotation;
            foreach (var (tr, at, phase, r) in puffs)
            {
                if (tr == null) continue;
                float u = Mathf.Repeat(t * 0.32f + phase, 1f);
                tr.position = at + Vector3.up * (u * 2.1f) + side * Mathf.Sin(u * 5f + phase * 9f) * 0.22f * u;
                tr.rotation = rot;
                tr.localScale = Vector3.one * (0.35f + u * 0.9f);
                puffBlock.SetColor("_Color", new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.PI) * 0.26f));
                r.SetPropertyBlock(puffBlock);
            }
        }
        // now and then one of the regulars does something: reaches for their tea, slams a tile down, laughs
        if (t < nextMove || sitters.Count == 0) return;
        nextMove = t + Random.Range(2.2f, 5.5f);
        var (p, glass, table) = sitters[Random.Range(0, sitters.Count)];
        if (p == null || !p.gameObject.activeInHierarchy) return;
        float roll = Random.value;
        var mid = tables[table].position; mid.y = glass.y;
        if (roll < 0.45f) p.Gesture(glass);
        else if (roll < 0.72f)
        {
            var toP = p.transform.position - mid; toP.y = 0; toP.Normalize();
            if (table == "domino" || table == "tawla") p.Slam(mid + toP * 1.3f); else p.Gesture(mid + toP * 0.8f);
        }
        else p.Cheer();
    }

    /// Steam over a hot glass at `at`: a few soft puffs (none on the phones that save their power).
    void Steam(Vector3 at)
    {
        if (Perf.Saver) return;
        var sh = Shader.Find("Sprites/Default");
        if (sh == null) return;
        if (steamMat == null) steamMat = new Material(sh) { mainTexture = Ui.Glow(64), color = Color.white };
        for (int k = 0; k < 3; k++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Steam"; Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = steamMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (lobby != null) q.transform.SetParent(lobby, true);
            puffs.Add((q.transform, at, k / 3f, r));
        }
    }
    Material steamMat;

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
        ClearView();
        // shadows only as far as the camera looks (plus a margin): the one shadow map stays sharp where it matters
        float sd = Vector3.Distance(camPos, camLook) + 28f;
        if (Mathf.Abs(sd - QualitySettings.shadowDistance) > 3f) QualitySettings.shadowDistance = sd;
    }

    /// Nobody's head fills the screen: a regular the camera passes close to (the fly-in, zooming in on a table)
    /// fades out of view until the camera has moved on.
    bool fixedShot;
    /// A set shot (test portraits, the advert) rather than the café view.
    public bool FixedShot => fixedShot;
    readonly Dictionary<GameObject, Renderer[]> bodies = new Dictionary<GameObject, Renderer[]>();
    void ClearView()
    {
        if (inGame) return;
        var c = cam.transform.position;
        foreach (var r in regulars)
        {
            if (r == null || !r.activeSelf) continue;
            if (!bodies.TryGetValue(r, out var rs)) bodies[r] = rs = r.GetComponentsInChildren<Renderer>(true);
            var head = r.transform.position + Vector3.up * 2.2f;
            // (a set shot, like a portrait or the advert, means to be close)
            bool near = !fixedShot && ((c - head).sqrMagnitude < 4.5f * 4.5f || (c - r.transform.position - Vector3.up * 1.2f).sqrMagnitude < 3.5f * 3.5f);
            if (rs.Length > 0 && rs[0] != null && rs[0].enabled == near) foreach (var x in rs) if (x != null) x.enabled = !near;
        }
    }

    float shakeUntil;
    /// The table shakes for a moment.
    public void Shake(float seconds) { if (!Perf.Saver) shakeUntil = Time.time + seconds; }

    /// A fixed shot (test portraits); null goes back to the normal camera.
    public void Shot(Vector3? pos, Vector3 look = default)
    {
        fixedShot = pos != null;
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
