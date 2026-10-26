// A café regular: a realistic human made in Blender with MakeHuman (MPFB) and rigged with a Mixamo skeleton.
// Their body language is computed here every frame, bone by bone, from the rest pose: sitting (and breathing),
// resting the forearms on the table, reaching out to play, cheering, sulking, standing and walking; and on top
// of it, looking around (head and eyes). The old generated characters (Resources/Characters) still work as a
// fallback: they bring their own Animator.
using System.Collections.Generic;
using UnityEngine;

public class Person : MonoBehaviour
{
    /// One scene unit is about 20 cm: the characters are built in meters.
    public const float Scale = 5f;
    public const float FloorY = -3.6f;

    // where the forearms rest: the table top's height, how far in front of the chair, how far apart (scene units)
    public bool seated = true;
    public float tableY = 0, reach = 1.9f, spread = 0.8f;
    /// Height of the hip joint above the floor when sitting (meters).
    public float sitHips = 0.5f;
    /// How far forward of the chair's centre the hips sit (meters).
    public float sitForward = 0.1f;

    Animator anim;            // the old characters only
    bool rigged;              // the Mixamo skeleton was found

    // the skeleton
    Transform hips, spine, spine1, spine2, neck, head;
    readonly Transform[] shoulder = new Transform[2], upper = new Transform[2], lower = new Transform[2], hand = new Transform[2],
        thigh = new Transform[2], shin = new Transform[2], foot = new Transform[2], toe = new Transform[2], eye = new Transform[2];
    readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    Vector3 hipsRest;         // hips, in the character's own space (meters)
    readonly int[] side = new int[2];   // +1 right of the body, -1 left
    readonly float[] upperLen = new float[2], lowerLen = new float[2];
    Vector3 localFwd = Vector3.forward;

    // what they're doing (weights blend smoothly)
    float wSit = 1, wWalk, wWin, wLose, ik = 1;
    float winAt = -100, loseAt = -100;
    bool walking;
    Vector3 gesture; float gestureAt = -10f; bool slam;
    float seed, nextLook, lookYaw, lookYawTo, lookPitch, lookPitchTo, nextSaccade; Vector2 saccade;
    Vector3? lookPoint;       // set: the head turns to this point (the camera, filming the advert)
    float mouthOpen;          // 0 shut … 1 wide open (talking, from the voice)
    SkinnedMeshRenderer mouthMesh; int mouthShape = -2; float mouthFull = 100f;

    /// Look at a point (null: back to looking around the table on their own).
    public void LookAt(Vector3? point) => lookPoint = point;

    /// The mouth, 0 shut to 1 open: the "MouthOpen" shape made with the character (tools/characters/make.py).
    public void Mouth(float open)
    {
        mouthOpen = Mathf.Clamp01(open);
        if (mouthShape == -2)
        {
            mouthShape = -1;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh; if (m == null) continue;
                for (int i = 0; i < m.blendShapeCount; i++)
                    if (m.GetBlendShapeName(i).ToLowerInvariant().Contains("mouth")) { mouthMesh = r; mouthShape = i; break; }
                if (mouthShape >= 0) break;
            }
            if (mouthShape < 0)   // the importer may not keep the name: the one mesh with a single shape is the body's
                foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.sharedMesh != null && r.sharedMesh.blendShapeCount == 1) { mouthMesh = r; mouthShape = 0; break; }
            if (mouthMesh != null) { var mm = mouthMesh.sharedMesh; mouthFull = mm.GetBlendShapeFrameWeight(mouthShape, mm.GetBlendShapeFrameCount(mouthShape) - 1); }
            Debug.Log($"[person] {name}: full weight {mouthFull}, mouth shape {(mouthShape >= 0 ? mouthMesh.sharedMesh.GetBlendShapeName(mouthShape) + " on " + mouthMesh.name : "not found")}");
        }
        // the shape's full weight is its last frame's (100 from some importers, 1 from others)
        if (mouthMesh != null) mouthMesh.SetBlendShapeWeight(mouthShape, mouthOpen * mouthFull);
    }

    static Transform Find(Transform t, string name)
    {
        // Mixamo names come as "mixamorig:Hips" (or with the colon replaced on import)
        var n = t.name; int c = Mathf.Max(n.LastIndexOf(':'), n.LastIndexOf('_'));
        if (n == name || (c >= 0 && n.Substring(c + 1) == name && n.StartsWith("mixamorig"))) return t;
        foreach (Transform ch in t) { var f = Find(ch, name); if (f != null) return f; }
        return null;
    }

    /// A regular, made from their model, standing on the café floor at `at` (x, z).
    public static Person Make(string name, Vector3 at)
    {
        var prefab = Resources.Load<GameObject>("People/" + name) ?? Resources.Load<GameObject>("Characters/" + name);
        if (prefab == null) { Debug.LogWarning("[person] no character " + name); return null; }
        var g = Instantiate(prefab, new Vector3(at.x, FloorY, at.z), Quaternion.identity);
        g.name = name;
        g.transform.localScale = Vector3.one * Scale;
        foreach (var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.updateWhenOffscreen = true; r.quality = SkinQuality.Bone4; }
        return g.AddComponent<Person>();
    }

    void Awake()
    {
        anim = GetComponent<Animator>();
        hips = Find(transform, "Hips");
        rigged = hips != null && anim == null;
        seed = Random.value * 100f;
        nextLook = Time.time + Random.Range(0.5f, 2.5f);
        if (!rigged) { Legacy(); return; }

        spine = Find(transform, "Spine"); spine1 = Find(transform, "Spine1"); spine2 = Find(transform, "Spine2");
        neck = Find(transform, "Neck"); head = Find(transform, "Head");
        string[] lr = { "Left", "Right" };
        for (int i = 0; i < 2; i++)
        {
            shoulder[i] = Find(transform, lr[i] + "Shoulder");
            upper[i] = Find(transform, lr[i] + "Arm");
            lower[i] = Find(transform, lr[i] + "ForeArm");
            hand[i] = Find(transform, lr[i] + "Hand");
            thigh[i] = Find(transform, lr[i] + "UpLeg");
            shin[i] = Find(transform, lr[i] + "Leg");
            foot[i] = Find(transform, lr[i] + "Foot");
            toe[i] = Find(transform, lr[i] + "ToeBase");
            eye[i] = Find(transform, lr[i] + "Eye");
        }
        foreach (var t in hips.GetComponentsInChildren<Transform>(true)) rest[t] = t.localRotation;   // the skeleton only (not the person's own turn)
        hipsRest = transform.InverseTransformPoint(hips.position);
        // how far the head (and anything worn on it) reaches above the head bone, standing in the rest pose
        if (head != null)
        {
            float top = float.MinValue;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) top = Mathf.Max(top, r.bounds.max.y);
            if (top > float.MinValue) headTop = Mathf.Clamp((top - head.position.y) / transform.lossyScale.y, 0.15f, 0.5f);
        }

        // which way they face: the toes are in front of the ankles
        if (toe[0] != null && foot[0] != null)
        {
            var f = transform.InverseTransformDirection(toe[0].position - foot[0].position); f.y = 0;
            if (f.sqrMagnitude > 1e-8f) localFwd = f.normalized;
        }
        var right = Vector3.Cross(Vector3.up, localFwd);
        for (int i = 0; i < 2; i++)
        {
            if (upper[i] == null) continue;
            side[i] = Vector3.Dot(transform.InverseTransformPoint(upper[i].position), right) >= 0 ? 1 : -1;
            upperLen[i] = Vector3.Distance(upper[i].position, lower[i].position) / Scale;
            lowerLen[i] = Vector3.Distance(lower[i].position, hand[i].position) / Scale * 1.15f;   // to the palm
        }
    }

    // ---------- the old generated characters (Animator) ----------
    Transform lHead; readonly Transform[] lLid = new Transform[2]; readonly Quaternion[] lLidRest = new Quaternion[2];
    float nextBlink, blinkAt = -1;
    void Legacy()
    {
        lHead = FindPlain(transform, "Head");
        for (int i = 0; i < 2; i++) { lLid[i] = FindPlain(transform, "Lid" + (i == 0 ? "L" : "R")); if (lLid[i] != null) lLidRest[i] = lLid[i].localRotation; }
        nextBlink = Time.time + Random.Range(1f, 4f);
        if (anim != null) anim.Play("SitIdle", 0, Random.value);
    }
    static Transform FindPlain(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindPlain(c, name); if (f != null) return f; }
        return null;
    }

    public Transform Head => head != null ? head : lHead;
    float headTop = 0.25f;   // meters from the head bone to the top of the head (or the hat on it)
    /// The very top of the person's head (hat included), for names and bubbles over them.
    public Vector3 Top => Head != null ? Head.position + Vector3.up * (headTop * transform.lossyScale.y) : transform.position + Vector3.up * 1.4f * transform.lossyScale.y;
    public Vector3 Forward { get { var f = transform.TransformDirection(localFwd); f.y = 0; return f.normalized; } }

    public void Face(Vector3 point)
    {
        var to = point - transform.position; to.y = 0;
        if (to.sqrMagnitude < 1e-6f) return;
        var fwdNow = transform.TransformDirection(localFwd); fwdNow.y = 0;
        transform.rotation = Quaternion.FromToRotation(fwdNow.normalized, to.normalized) * transform.rotation;
        var e = transform.eulerAngles; transform.rotation = Quaternion.Euler(0, e.y, 0);
    }

    /// Reach out to a point on the table (playing a tile there) and come back.
    public void Gesture(Vector3 point) { gesture = point; gestureAt = Time.time; slam = false; }
    /// The last tile, slammed down: the hand goes up high, then comes down hard on the table at `point`.
    public void Slam(Vector3 point) { gesture = point; gestureAt = Time.time; slam = true; }
    /// A quick cheer (a big double in tawla): arms up for a moment.
    public void Cheer() { winAt = Time.time - 1.3f; if (anim != null) anim.SetTrigger("win"); }
    public void Win() { winAt = Time.time; if (anim != null) anim.SetTrigger("win"); }
    public void Lose() { loseAt = Time.time; if (anim != null) anim.SetTrigger("lose"); }
    public void Sit(bool on) { seated = on; if (anim != null) anim.SetBool("sit", on); }
    public void Walking(bool on) { walking = on; if (anim != null) anim.SetBool("walking", on); }

    // ---------- posing ----------
    // directions are in the character's own frame: x right, y up, z forward (meters)
    Vector3 R => Vector3.Cross(Vector3.up, localFwd);
    Vector3 Dir(float right, float up, float fwd, int s = 1) => (R * right * s + Vector3.up * up + localFwd * fwd).normalized;

    /// Turn a bone so that it points (to `child`) along `dir` (character space), blended by w.
    void Aim(Transform bone, Transform child, Vector3 dir, float w = 1)
    {
        if (bone == null || child == null || w <= 0) return;
        var now = child.position - bone.position;
        if (now.sqrMagnitude < 1e-10f) return;
        var want = transform.TransformDirection(dir);
        var q = Quaternion.FromToRotation(now, want) * bone.rotation;
        bone.rotation = w >= 1 ? q : Quaternion.Slerp(bone.rotation, q, w);
    }
    /// Bend a bone forward (+) about the character's right axis.
    void Bend(Transform bone, float deg) { if (bone != null && Mathf.Abs(deg) > 0.01f) bone.rotation = Quaternion.AngleAxis(deg, transform.TransformDirection(R)) * bone.rotation; }
    void Turn(Transform bone, float deg) { if (bone != null && Mathf.Abs(deg) > 0.01f) bone.rotation = Quaternion.AngleAxis(deg, transform.up) * bone.rotation; }

    static float Smooth(float v, float to, float speed) => Mathf.MoveTowards(v, to, Time.deltaTime * speed);

    void LateUpdate()
    {
        if (!rigged) { LegacyUpdate(); return; }
        float t = Time.time + seed;
        float now = Time.time;

        // what they're doing
        bool cheering = now - winAt < 2.6f, sulking = now - loseAt < 3.2f;
        wSit = Smooth(wSit, seated && !walking ? 1 : 0, 2.5f);
        wWalk = Smooth(wWalk, walking ? 1 : 0, 3f);
        wWin = Smooth(wWin, cheering ? 1 : 0, cheering ? 4f : 2f);
        wLose = Smooth(wLose, sulking ? 1 : 0, sulking ? 3f : 1.5f);
        ik = Smooth(ik, seated && !walking && !cheering ? 1 : 0, 4f);

        foreach (var kv in rest) kv.Key.localRotation = kv.Value;
        // ---- hips: down onto the chair; a bob when walking
        float walkPhase = t * 6.2f;
        var hp = hipsRest;   // meters, in the character's own space
        hp.y = Mathf.Lerp(hipsRest.y, sitHips, wSit) + (Mathf.Abs(Mathf.Sin(walkPhase)) * 0.025f - 0.012f) * wWalk;
        hp += localFwd * sitForward * wSit;   // on the front of the seat, clear of the chair back
        hips.position = transform.TransformPoint(hp);

        // ---- legs
        for (int i = 0; i < 2; i++)
        {
            int sd = side[i];
            float swing = Mathf.Sin(walkPhase + i * Mathf.PI) * wWalk;
            // standing (rest, a touch closer together) → walking swing → sitting (thighs forward, shins down)
            var thighStand = Dir(0.04f, -1, swing * 0.45f, sd);
            var thighSit = Dir(0.1f, -0.06f, 1, sd);
            Aim(thigh[i], shin[i], Vector3.Slerp(thighStand, thighSit, wSit));
            float kneeBend = Mathf.Max(0, -Mathf.Cos(walkPhase + i * Mathf.PI)) * 0.55f * wWalk;
            var shinStand = Dir(0.02f, -1, swing * 0.3f - kneeBend, sd);
            var shinSit = Dir(0.03f, -1, -0.06f, sd);
            Aim(shin[i], foot[i], Vector3.Slerp(shinStand, shinSit, wSit));
            Aim(foot[i], toe[i], Dir(0.05f, -0.45f, 1, sd));
        }

        // ---- spine: a little forward when sitting at the table, more when sulking, back when cheering; breathing
        float breath = Mathf.Sin(t * 1.9f);
        Bend(spine, 4 * wSit + 8 * wLose - 4 * wWin + Mathf.Sin(walkPhase * 2) * 1.5f * wWalk);
        Bend(spine1, 3 * wSit + 8 * wLose - 3 * wWin + breath * 0.8f);
        Bend(spine2, 2 * wSit + 6 * wLose - 4 * wWin - breath * 1.2f);
        Turn(spine1, Mathf.Sin(walkPhase) * 5 * wWalk);
        for (int i = 0; i < 2; i++) if (shoulder[i] != null) Bend(shoulder[i], breath * 0.6f + 4 * wLose);

        // ---- arms: by the sides (standing), swinging (walking), on the table (sitting), up in the air (cheering)
        var fwdW = transform.TransformDirection(localFwd); var upW = transform.up; var rightW = transform.TransformDirection(R);
        for (int i = 0; i < 2; i++)
        {
            if (upper[i] == null) continue;
            int sd = side[i];
            float swing = Mathf.Sin(walkPhase + i * Mathf.PI + Mathf.PI) * wWalk;
            var armStand = Dir(0.16f, -1, 0.03f + swing * 0.35f, sd);
            var armSit = Dir(0.22f, -0.8f, 0.55f, sd);
            float pump = Mathf.Abs(Mathf.Sin((now - winAt) * 7f)) * 0.25f;
            var armWin = Dir(0.45f, 1, 0.12f + pump, sd);
            var arm = Vector3.Slerp(Vector3.Slerp(armStand, armSit, wSit), armWin, wWin);
            Aim(upper[i], lower[i], arm);
            var foreStand = Dir(0.04f, -1, 0.12f + Mathf.Max(0, swing) * 0.4f, sd);
            var foreSit = Dir(-0.15f, -0.15f, 1, sd);
            var foreWin = Dir(0.12f, 1, 0.1f, sd);
            Aim(lower[i], hand[i], Vector3.Slerp(Vector3.Slerp(foreStand, foreSit, wSit), foreWin, wWin));
        }

        // forearms on the table (while sitting quietly), a hand going out to play
        if (ik > 0.001f && reach > 0)
        {
            var root = transform.position; root.y = tableY;
            for (int i = 0; i < 2; i++)
            {
                if (upperLen[i] <= 0) continue;
                float wob = Mathf.Sin(t * 0.5f + i * 2.1f) * 0.08f;
                var target = root + fwdW * (reach + wob) + rightW * side[i] * spread + upW * 0.12f;
                float gt = now - gestureAt;
                if (slam && gt < 1.5f && (Vector3.Dot(gesture - root, rightW) >= 0 ? 1 : -1) == side[i])
                {
                    // up high above the spot (0–0.5 s), down hard (0.5–0.6 s), resting there, then back
                    var high = gesture + upW * 2.2f - fwdW * 0.4f;
                    if (gt < 0.5f) target = Vector3.Lerp(target, high, Ui.Ease(gt / 0.5f));
                    else if (gt < 0.6f) target = Vector3.Lerp(high, gesture + upW * 0.15f, (gt - 0.5f) / 0.1f);
                    else if (gt < 1.0f) target = gesture + upW * 0.15f;
                    else target = Vector3.Lerp(gesture + upW * 0.15f, target, Ui.Ease((gt - 1.0f) / 0.5f));
                }
                else if (!slam && gt < 1f && (Vector3.Dot(gesture - root, rightW) >= 0 ? 1 : -1) == side[i])
                {
                    float k = gt < 0.35f ? Ui.Ease(gt / 0.35f) : gt < 0.6f ? 1f : 1f - Ui.Ease((gt - 0.6f) / 0.4f);
                    var lift = upW * Mathf.Sin(Mathf.Clamp01(gt / 0.35f) * Mathf.PI) * 0.6f;
                    target = Vector3.Lerp(target, gesture + upW * 0.2f, k) + (gt < 0.35f ? lift : Vector3.zero);
                }
                Reach(i, target, fwdW, rightW, upW, ik * (1 - wLose * 0.4f));
            }
        }
        // hands flat-ish, following the forearm
        for (int i = 0; i < 2; i++) if (hand[i] != null && lower[i] != null) Bend(hand[i], -10 * wSit);

        // ---- head: looking around now and then, mostly at the table; down when sulking, up when cheering
        if (now > nextLook)
        {
            nextLook = now + Random.Range(1.8f, 5f);
            lookYawTo = Random.value < 0.55f ? Random.Range(-12f, 12f) : Random.Range(-38f, 38f);
            lookPitchTo = Random.value < 0.6f ? 14f * wSit : Random.Range(-6f, 6f);
        }
        if (lookPoint.HasValue && head != null)
        {
            // at the camera: turn the head (and a little the neck) toward it, small nods while talking
            var to = lookPoint.Value - head.position; var flat = Vector3.ProjectOnPlane(to, upW);
            lookYawTo = Mathf.Clamp(Vector3.SignedAngle(fwdW, flat, upW), -65f, 65f);
            lookPitchTo = Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(to, upW), flat.magnitude) * Mathf.Rad2Deg, -20f, 30f) + mouthOpen * 4f;
            nextLook = now + 1f;
        }
        float ease = 1 - Mathf.Exp(-Time.deltaTime * 4f);
        lookYaw = Mathf.Lerp(lookYaw, lookYawTo * (1 - wWin) * (1 - wLose * 0.7f), ease);
        lookPitch = Mathf.Lerp(lookPitch, lookPitchTo, ease);
        float pitch = lookPitch * (1 - wWin) + 24 * wLose - 14 * wWin + Mathf.Sin(t * 0.7f) * 1.5f;
        Turn(neck, lookYaw * 0.35f); Bend(neck, pitch * 0.35f);
        Turn(head, lookYaw * 0.65f); Bend(head, pitch * 0.65f);
        if (wLose > 0.01f && head != null) head.rotation = Quaternion.AngleAxis(Mathf.Sin((now - loseAt) * 5f) * 9f * wLose, upW) * head.rotation;   // shaking the head

        // eyes: small darting movements, leading the head
        if (now > nextSaccade) { nextSaccade = now + Random.Range(0.4f, 2.2f); saccade = new Vector2(Random.Range(-7f, 7f), Random.Range(-3f, 4f)); }
        for (int i = 0; i < 2; i++)
            if (eye[i] != null)
                eye[i].rotation = Quaternion.AngleAxis(saccade.x + (lookYawTo - lookYaw) * 0.4f, upW) * Quaternion.AngleAxis(saccade.y, rightW) * eye[i].rotation;
    }

    /// Two-bone reach: the hand to `target`, the elbow out, down and a little back (blended by w).
    void Reach(int i, Vector3 target, Vector3 fwd, Vector3 right, Vector3 up, float w)
    {
        var sh = upper[i]; var el = lower[i]; var hd = hand[i];
        float s = transform.lossyScale.y;
        float a = upperLen[i] * s, b = lowerLen[i] * s;
        var S = sh.position;
        var toT = target - S;
        float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(a - b) + 0.01f, a + b - 0.01f);
        var dir = toT.normalized;
        var pole = right * side[i] * 0.7f - up * 0.75f - fwd * 0.45f;
        pole -= dir * Vector3.Dot(pole, dir);
        if (pole.sqrMagnitude < 1e-6f) pole = right * side[i];
        pole.Normalize();
        float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1f, 1f);
        var elbowWant = S + dir * (a * cosA) + pole * (a * Mathf.Sqrt(1 - cosA * cosA));
        var shTo = Quaternion.FromToRotation(el.position - S, elbowWant - S) * sh.rotation;
        sh.rotation = Quaternion.Slerp(sh.rotation, shTo, w);
        var handWant = S + dir * d;
        var elTo = Quaternion.FromToRotation(hd.position - el.position, handWant - el.position) * el.rotation;
        el.rotation = Quaternion.Slerp(el.rotation, elTo, w);
    }

    void LegacyUpdate()
    {
        if (Time.time > nextLook)
        {
            nextLook = Time.time + Random.Range(1.8f, 5f);
            lookYawTo = Random.value < 0.55f ? Random.Range(-12f, 12f) : Random.Range(-38f, 38f);
        }
        lookYaw = Mathf.Lerp(lookYaw, lookYawTo, 1 - Mathf.Exp(-Time.deltaTime * 4f));
        if (lHead != null) lHead.rotation = Quaternion.AngleAxis(lookYaw * 0.5f, Vector3.up) * lHead.rotation;
        if (Time.time > nextBlink) { blinkAt = Time.time; nextBlink = Time.time + Random.Range(2f, 5.5f); }
        float closed = 0;
        if (blinkAt >= 0) { float b = (Time.time - blinkAt) / 0.16f; if (b < 1) closed = Mathf.Sin(b * Mathf.PI); else blinkAt = -1; }
        for (int i = 0; i < 2; i++) if (lLid[i] != null) lLid[i].localRotation = Quaternion.Euler(closed * 40f, 0, 0) * lLidRest[i];
    }
}
