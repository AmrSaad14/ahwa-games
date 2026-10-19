// A café regular brought to life: breathing, looking around, blinking, forearms resting on the table.
// Works on the rig exported from the web game (Torso, Head, Shoulder/Elbow/Hand L/R, Eye/Lid L/R, Hip/Knee L/R),
// whatever way the importer turned its axes: directions are measured from the rig itself.
using System.Collections.Generic;
using UnityEngine;

public class Person : MonoBehaviour
{
    public Transform torso, head;
    public readonly Transform[] shoulder = new Transform[2], elbow = new Transform[2], hand = new Transform[2], lid = new Transform[2];
    readonly Dictionary<Transform, Quaternion> rest = new Dictionary<Transform, Quaternion>();
    readonly float[] upper = new float[2], lower = new float[2];
    readonly int[] side = new int[2];
    Vector3 localFwd = Vector3.forward;   // the way the face looks, in the root's frame

    /// Where the hands rest, in the root's frame (forward, up, sideways spread); set for seated people.
    public bool seated = true;
    public float reachForward = 1.9f, handHeight = 0.25f, handSpread = 0.8f;

    Vector3 gesture; float gestureAt = -10f;
    /// Reach out to a point on the table (playing a tile there) and come back.
    public void Gesture(Vector3 point) { gesture = point; gestureAt = Time.time; }

    float seed, nextLook, lookYaw, lookYawTo, lookPitch, nextBlink, blinkAt = -1;

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }

    void Awake()
    {
        torso = Find(transform, "Torso");
        head = Find(transform, "Head");
        string[] lr = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            shoulder[i] = Find(transform, "Shoulder" + lr[i]);
            elbow[i] = Find(transform, "Elbow" + lr[i]);
            hand[i] = Find(transform, "Hand" + lr[i]);
            lid[i] = Find(transform, "Lid" + lr[i]);
        }
        foreach (var t in new[] { torso, head, shoulder[0], shoulder[1], elbow[0], elbow[1], lid[0], lid[1] })
            if (t != null) rest[t] = t.localRotation;
        var sh0 = shoulder[0] != null ? shoulder[0].parent : null;
        var sh1 = shoulder[1] != null ? shoulder[1].parent : null;
        if (sh0 != null) rest[sh0] = sh0.localRotation;
        if (sh1 != null) rest[sh1] = sh1.localRotation;
        // which way the face looks: from the middle of the head toward the eyes
        var eyeL = Find(transform, "EyeL"); var eyeR = Find(transform, "EyeR");
        if (head != null && eyeL != null && eyeR != null)
        {
            var f = (eyeL.position + eyeR.position) * 0.5f - head.position;
            f = transform.InverseTransformDirection(f); f.y = 0;
            if (f.sqrMagnitude > 1e-6f) localFwd = f.normalized;
        }
        var fwd = transform.TransformDirection(localFwd);
        var right = Vector3.Cross(Vector3.up, fwd);
        for (int i = 0; i < 2; i++)
        {
            if (shoulder[i] == null || elbow[i] == null || hand[i] == null) continue;
            upper[i] = Vector3.Distance(shoulder[i].position, elbow[i].position);
            lower[i] = Vector3.Distance(elbow[i].position, hand[i].position);
            side[i] = Vector3.Dot(shoulder[i].position - (torso != null ? torso.position : transform.position), right) >= 0 ? 1 : -1;
        }
        seed = Random.value * 100f;
        nextLook = Time.time + Random.Range(0.5f, 2.5f);
        nextBlink = Time.time + Random.Range(1f, 4f);
    }

    /// Turns the person (about the vertical) so their face looks at a point.
    public void Face(Vector3 point)
    {
        var to = point - transform.position; to.y = 0;
        if (to.sqrMagnitude < 1e-6f) return;
        var fwdNow = transform.TransformDirection(localFwd); fwdNow.y = 0;
        transform.rotation = Quaternion.FromToRotation(fwdNow.normalized, to.normalized) * transform.rotation;
        // keep upright (FromToRotation on flat vectors only turns about the vertical, but guard against drift)
        var e = transform.eulerAngles; transform.rotation = Quaternion.Euler(0, e.y, 0);
    }

    Vector3 Fwd => transform.TransformDirection(localFwd);

    void LateUpdate()
    {
        float t = Time.time + seed;
        var fwd = Fwd; fwd.y = 0; fwd.Normalize();
        var up = Vector3.up;
        var right = Vector3.Cross(up, fwd);
        float s = transform.lossyScale.y;

        // breathing: the chest rises a little
        if (torso != null)
        {
            torso.localRotation = rest[torso];
            torso.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 1.6f) * 1.2f, right) * torso.rotation;
        }

        // looking around now and then, mostly at the table
        if (Time.time > nextLook)
        {
            nextLook = Time.time + Random.Range(1.8f, 5f);
            lookYawTo = Random.value < 0.55f ? Random.Range(-12f, 12f) : Random.Range(-38f, 38f);
            lookPitch = Random.value < 0.6f ? 14f : Random.Range(-4f, 8f);
        }
        lookYaw = Mathf.Lerp(lookYaw, lookYawTo, 1 - Mathf.Exp(-Time.deltaTime * 4f));
        if (head != null)
        {
            head.localRotation = rest[head];
            head.rotation = Quaternion.AngleAxis(lookYaw, up) * Quaternion.AngleAxis(lookPitch * 0.6f + Mathf.Sin(t * 0.7f) * 1.5f, right) * head.rotation;
        }

        // forearms on the table, hands drifting a little
        if (seated)
        {
            var root = transform.position;
            for (int i = 0; i < 2; i++)
            {
                if (upper[i] <= 0) continue;
                float wob = Mathf.Sin(t * 0.5f + i * 2.1f) * 0.08f;
                var target = root + fwd * (reachForward + wob) * s + right * side[i] * handSpread * s + up * handHeight * s;
                // the hand on the side of the tile goes out to it
                float gt = Time.time - gestureAt;
                if (gt < 1f && (Vector3.Dot(gesture - root, right) >= 0 ? 1 : -1) == side[i])
                {
                    float k = gt < 0.35f ? Ui.Ease(gt / 0.35f) : gt < 0.6f ? 1f : 1f - Ui.Ease((gt - 0.6f) / 0.4f);
                    var to = gesture + up * 0.25f * s;
                    var lift = up * Mathf.Sin(Mathf.Clamp01(gt / 0.35f) * Mathf.PI) * 0.6f * s;
                    target = Vector3.Lerp(target, to, k) + (gt < 0.35f ? lift : Vector3.zero);
                }
                Reach(i, target, fwd, right, up);
            }
        }

        // blinking
        if (Time.time > nextBlink) { blinkAt = Time.time; nextBlink = Time.time + Random.Range(2f, 5.5f); }
        float closed = 0;
        if (blinkAt >= 0) { float b = (Time.time - blinkAt) / 0.16f; if (b < 1) closed = Mathf.Sin(b * Mathf.PI); else blinkAt = -1; }
        for (int i = 0; i < 2; i++)
        {
            if (lid[i] == null) continue;
            lid[i].localRotation = rest[lid[i]];
            if (closed > 0) lid[i].rotation = Quaternion.AngleAxis(closed * 48f, right) * lid[i].rotation;
        }
    }

    /// Two-bone reach: the hand to `target`, the elbow out, down and a little back.
    void Reach(int i, Vector3 target, Vector3 fwd, Vector3 right, Vector3 up)
    {
        var sh = shoulder[i]; var el = elbow[i]; var hd = hand[i];
        if (sh.parent != null && rest.TryGetValue(sh.parent, out var pr)) sh.parent.localRotation = pr;
        sh.localRotation = rest[sh];
        el.localRotation = rest[el];
        float a = upper[i], b = lower[i];
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
        sh.rotation = Quaternion.FromToRotation(el.position - S, elbowWant - S) * sh.rotation;
        var handWant = S + dir * d;
        el.rotation = Quaternion.FromToRotation(hd.position - el.position, handWant - el.position) * el.rotation;
    }
}
