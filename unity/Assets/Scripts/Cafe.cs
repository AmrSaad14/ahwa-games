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

    Vector3 center, front;          // the middle of the four tables; the way toward the camera side of the room
    public Vector3 Front => front;
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

    public void Build()
    {
        // camera
        var camGo = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
        cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 38;
        cam.nearClipPlane = 0.5f; cam.farClipPlane = 220;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x1c, 0x10, 0x0a, 0xff);
        cam.allowHDR = true;

        // the room
        var prefab = Resources.Load<GameObject>("Art/cafe");
        if (prefab == null) { Debug.LogError("[cafe] Art/cafe not found (is glTFast importing the .glb files?)"); return; }
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

        Lights();
        People(room.transform);

        camPos = camPosTo = Overview(out camLook); camLookTo = camLook;
        Apply();
        ready = true;
    }

    void Lights()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.36f);
        RenderSettings.ambientEquatorColor = new Color(0.36f, 0.27f, 0.2f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.11f, 0.08f);
        // the warm light from the lamps above, casting the shadows
        var sun = new GameObject("Key light").AddComponent<Light>();
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

    void People(Transform room)
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
    public void SetGame(bool on)
    {
        inGame = on;
        if (lobby != null) lobby.gameObject.SetActive(!on);
        foreach (var r in regulars) if (r != null) r.SetActive(!on);
        if (on) camPosTo = GameShot(out camLookTo); else Focus(focus);
    }

    /// Looking down at the game table from your side, framed to the screen.
    Vector3 GameShot(out Vector3 look)
    {
        float aspect = Mathf.Max(0.5f, (float)Screen.width / Mathf.Max(1, Screen.height));
        float vf = cam.fieldOfView * Mathf.Deg2Rad;
        float hf = 2 * Mathf.Atan(Mathf.Tan(vf / 2) * aspect);
        float el = aspect < 0.9f ? 1.08f : 0.98f;
        float d = Mathf.Max(9.6f / Mathf.Tan(hf / 2), 8.5f / Mathf.Tan(vf / 2)) * 1.02f;
        look = Vector3.zero + front * 1.3f;
        return look + front * d * Mathf.Cos(el) + Vector3.up * d * Mathf.Sin(el);
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
        cam.transform.position = camPos + sway;
        cam.transform.LookAt(camLook);
    }

    /// A fixed shot (test portraits); null goes back to the normal camera.
    public void Shot(Vector3? pos, Vector3 look = default)
    {
        if (pos == null) { Focus(focus); return; }
        camPos = camPosTo = pos.Value; camLook = camLookTo = look; drift = 0;
    }
    public IEnumerable<Person> Regulars() { foreach (var r in regulars) if (r != null) { var p = r.GetComponent<Person>(); if (p != null) yield return p; } }

    /// Flies in from close to the floor when the splash opens.
    public void Entrance()
    {
        camPos = camPosTo + Vector3.up * 6f + front * 10f;
    }
}
