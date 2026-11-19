// Skins seen in 3D before you buy them: a little stage far below the café, lit on its own, turning slowly in
// front of its own camera, drawn into a picture the shop shows. Boards (tawla, chess) are set up as at the start
// of a game, in the skin being looked at; tiles, dice and table cloths are laid out on a small table top.
using System.Collections.Generic;
using UnityEngine;

public class SkinPreview : MonoBehaviour
{
    static SkinPreview I;
    Camera cam; Transform stage, turn; RenderTexture rt; Light key;
    float speed = 18f;

    /// Can this kind of skin be shown in 3D?
    public static bool Has(string cat) => cat == "tawla" || cat == "checkers" || cat == "chess" || cat == "pieces" || cat == "tiles" || cat == "dice" || cat == "felt";

    /// Builds the skin on the stage and returns the picture it is drawn into (null if it can't be shown).
    public static RenderTexture Show(Skins.Def s)
    {
        if (s == null || !Has(s.cat)) return null;
        if (I == null) I = new GameObject("Skin preview").AddComponent<SkinPreview>();
        return I.Build(s);
    }

    /// Takes the stage down (the shop panel closed).
    public static void Hide()
    {
        if (I == null) return;
        I.Clear();
        if (I.cam != null) I.cam.enabled = false;
    }

    void Awake()
    {
        stage = new GameObject("Stage").transform;
        stage.SetParent(transform, false);
        stage.position = new Vector3(0, -400, 0);   // far below the café floor: no camera of the café sees it
        turn = new GameObject("Turn").transform; turn.SetParent(stage, false);
        cam = new GameObject("Preview camera").AddComponent<Camera>();
        cam.transform.SetParent(stage, false);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x22, 0x15, 0x0e, 0xff);
        cam.fieldOfView = 30; cam.nearClipPlane = 0.3f; cam.farClipPlane = 120;
        cam.allowHDR = false; cam.allowMSAA = true;
        cam.enabled = false;
        key = new GameObject("Preview light").AddComponent<Light>();
        key.transform.SetParent(stage, false);
        key.type = LightType.Point; key.range = 60; key.intensity = 1.6f; key.color = new Color(1f, 0.9f, 0.76f);
        key.transform.localPosition = new Vector3(-6, 14, -8);
    }

    void Clear()
    {
        for (int i = turn.childCount - 1; i >= 0; i--) Destroy(turn.GetChild(i).gameObject);
        turn.localRotation = Quaternion.identity;
    }

    RenderTexture Build(Skins.Def s)
    {
        Clear();
        if (rt == null) { rt = new RenderTexture(640, 400, 16) { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) }; rt.Create(); }
        try
        {
            switch (s.cat)
            {
                case "tawla": case "checkers": BoardOf("tawla", s); break;
                case "chess": case "pieces": BoardOf("chess", s); break;
                case "tiles": Tiles(s.id); break;
                case "dice": Dice(s.id); break;
                case "felt": Felt(s.id); break;
            }
        }
        catch (System.Exception e) { Debug.LogWarning("[preview] " + s.id + ": " + e.Message); }
        Frame();
        cam.targetTexture = rt;
        cam.enabled = true;
        return rt;
    }

    /// A board set up as at the start of a game, in the skin looked at (only while it is built).
    void BoardOf(string game, Skins.Def s)
    {
        Skins.Force[s.cat] = s.id;
        try
        {
            Board b = game == "chess" ? new ChessBoard(turn) : (Board)new TawlaBoard(turn);
            var mode = GameUi.GameModes[game][0].mode;
            var rules = BoardRules.For(mode);
            rules.NewGame(2, 0, new System.Random(3));
            var m = new Dictionary<string, object> { ["game"] = game, ["id"] = "preview", ["mode"] = mode, ["phase"] = "play", ["you"] = 0, ["turn"] = -1, ["seats"] = new List<object>(), [game] = rules.View(0) };
            var v = TableView.Parse(Json.Parse(Json.Write(m)) as Dictionary<string, object>);
            b.Render(v, -1000, new List<int>(), new List<int>());
            if (game == "tawla")
            {
                // and a pair of dice in the skin's colours
                var (face, pip) = Skins.Dice(Skins.Worn("dice"));
                var d1 = Art.Die(turn, 6, 0.55f, face, pip); d1.transform.localPosition = new Vector3(1.6f, 0.75f, 0.2f); d1.transform.localRotation = Quaternion.Euler(0, 20, 0);
                var d2 = Art.Die(turn, 5, 0.55f, face, pip); d2.transform.localPosition = new Vector3(2.4f, 0.75f, -0.3f); d2.transform.localRotation = Quaternion.Euler(0, -15, 0);
            }
        }
        finally { Skins.Force.Remove(s.cat); }
    }

    /// Four tiles of the look, on a dark wooden top.
    void Tiles(string id)
    {
        Top(new Color(0.24f, 0.13f, 0.07f), 7, 4.5f);
        string keep = TileArt.SkinId;
        TileArt.SetSkin(id);
        try
        {
            int[,] faces = { { 6, 5 }, { 3, 3 }, { 4, 1 }, { 2, 0 } };
            for (int i = 0; i < 4; i++)
            {
                var t = TileArt.Make(faces[i, 0], faces[i, 1]);
                t.transform.SetParent(turn, false);
                t.transform.localPosition = new Vector3(-2.5f + i * 1.65f, TileArt.H / 2 + 0.02f, i % 2 == 0 ? -0.4f : 0.5f);
                t.transform.localRotation = Quaternion.Euler(0, 90 + (i - 1.5f) * 7f, 0);
                t.transform.localScale = Vector3.one * 1.4f;
                // its own copy of the body's colour: the shared one goes back to the tiles you wear
                var r = t.GetComponent<MeshRenderer>(); r.material = new Material(r.sharedMaterial) { color = TileArt.Ivory };
            }
        }
        finally { TileArt.SetSkin(keep); }
    }

    void Dice(string id)
    {
        Top(new Color(0.07f, 0.3f, 0.16f), 5, 3.5f);
        var (face, pip) = Skins.Dice(id);
        var a = Art.Die(turn, 6, 1.1f, face, pip); a.transform.localPosition = new Vector3(-0.9f, 0.55f, 0); a.transform.localRotation = Quaternion.Euler(0, 25, 0);
        var b = Art.Die(turn, 3, 1.1f, face, pip); b.transform.localPosition = new Vector3(0.9f, 0.55f, 0.3f); b.transform.localRotation = Quaternion.Euler(0, -20, 0);
    }

    void Felt(string id)
    {
        Top(new Color(0.24f, 0.13f, 0.07f), 8, 8);
        Look.Felt(id, turn, turn.position + Vector3.up * 0.01f, 3.6f, Quaternion.identity);
        // two tiles resting on the cloth, for scale
        var t = TileArt.Make(6, 4); t.transform.SetParent(turn, false); t.transform.localPosition = new Vector3(-0.8f, TileArt.H / 2 + 0.04f, 0); t.transform.localScale = Vector3.one * 1.4f;
        var u = TileArt.Make(4, 1); u.transform.SetParent(turn, false); u.transform.localPosition = new Vector3(1.4f, TileArt.H / 2 + 0.04f, 0.2f); u.transform.localRotation = Quaternion.Euler(0, 90, 0); u.transform.localScale = Vector3.one * 1.4f;
    }

    /// A slab of polished wood (or felt) to set things on.
    void Top(Color c, float w, float d)
    {
        var top = Art.Box("Top", turn, w, 0.3f, d, Vector3.down * 0.3f, Art.Mat(Color.white, 0.45f, 0, Art.WoodTex(256, 128, c, c * 0.7f, c * 1.25f, 40, 2.2f)));
        top.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// The camera back far enough that everything fits, looking down at it from the front.
    void Frame()
    {
        var rs = turn.GetComponentsInChildren<Renderer>();
        var b = new Bounds(turn.position, Vector3.one);
        bool any = false;
        foreach (var r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
        float radius = Mathf.Max(1f, b.extents.magnitude);
        float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.82f;
        cam.transform.position = b.center + new Vector3(0, Mathf.Sin(52 * Mathf.Deg2Rad), -Mathf.Cos(52 * Mathf.Deg2Rad)) * dist;
        cam.transform.LookAt(b.center);
        key.transform.position = b.center + new Vector3(-radius, radius * 2.2f, -radius * 1.4f);
        key.range = radius * 8;
    }

    void Update()
    {
        if (cam != null && cam.enabled) turn.Rotate(Vector3.up, speed * Time.unscaledDeltaTime, Space.World);
    }

    void OnDestroy() { if (rt != null) rt.Release(); }
}
