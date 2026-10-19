// A café regular made by the character generator (Unity skeleton + Animator). The Animator plays their body
// animations (sitting and breathing, cheering, sulking, standing, walking); on top of that, every frame:
// they look around, blink, rest their forearms on the table and reach out to play a tile.
using System.Collections.Generic;
using UnityEngine;

public class Person : MonoBehaviour
{
    /// One scene unit is about 20 cm: the characters are built in meters.
    public const float Scale = 5f;
    public const float FloorY = -3.6f;

    public Transform chest, head;
    public readonly Transform[] upper = new Transform[2], lower = new Transform[2], hand = new Transform[2], lid = new Transform[2];
    readonly Quaternion[] lidRest = new Quaternion[2];
    readonly float[] upperLen = new float[2], lowerLen = new float[2];
    readonly int[] side = new int[2];
    Vector3 localFwd = Vector3.forward;
    Animator anim;
    float ik = 1;

    // where the forearms rest: the table top's height, how far in front of the chair, how far apart (scene units)
    public bool seated = true;
    public float tableY = 0, reach = 1.9f, spread = 0.8f;

    Vector3 gesture; float gestureAt = -10f;
    float seed, nextLook, lookYaw, lookYawTo, lookPitch, nextBlink, blinkAt = -1;

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }

    /// A regular, made from their prefab, standing on the café floor at `at` (x, z).
    public static Person Make(string name, Vector3 at)
    {
        var prefab = Resources.Load<GameObject>("Characters/" + name);
        if (prefab == null) { Debug.LogWarning("[person] no character " + name); return null; }
        var g = Instantiate(prefab, new Vector3(at.x, FloorY, at.z), Quaternion.identity);
        g.name = name;
        g.transform.localScale = Vector3.one * Scale;
        return g.AddComponent<Person>();
    }

    void Awake()
    {
        anim = GetComponent<Animator>();
        chest = Find(transform, "Chest");
        head = Find(transform, "Head");
        string[] lr = { "L", "R" };
        for (int i = 0; i < 2; i++)
        {
            upper[i] = Find(transform, "UpperArm" + lr[i]);
            lower[i] = Find(transform, "LowerArm" + lr[i]);
            hand[i] = Find(transform, "Hand" + lr[i]);
            lid[i] = Find(transform, "Lid" + lr[i]);
            if (lid[i] != null) lidRest[i] = lid[i].localRotation;
            if (upper[i] != null && lower[i] != null && hand[i] != null)
            {
                upperLen[i] = Vector3.Distance(upper[i].position, lower[i].position);
                lowerLen[i] = Vector3.Distance(lower[i].position, hand[i].position) * 1.12f;   // to the palm
            }
        }
        var eyeL = Find(transform, "EyeL"); var eyeR = Find(transform, "EyeR");
        if (head != null && eyeL != null && eyeR != null)
        {
            var f = (eyeL.position + eyeR.position) * 0.5f - head.position;
            f = transform.InverseTransformDirection(f); f.y = 0;
            if (f.sqrMagnitude > 1e-8f) localFwd = f.normalized;
        }
        var fwd = transform.TransformDirection(localFwd);
        var right = Vector3.Cross(Vector3.up, fwd);
        for (int i = 0; i < 2; i++)
            if (upper[i] != null) side[i] = Vector3.Dot(upper[i].position - transform.position, right) >= 0 ? 1 : -1;
        seed = Random.value * 100f;
        nextLook = Time.time + Random.Range(0.5f, 2.5f);
        nextBlink = Time.time + Random.Range(1f, 4f);
        if (anim != null) anim.Play("SitIdle", 0, Random.value);   // not everyone breathes in step
    }

    public void Face(Vector3 point)
    {
        var to = point - transform.position; to.y = 0;
        if (to.sqrMagnitude < 1e-6f) return;
        var fwdNow = transform.TransformDirection(localFwd); fwdNow.y = 0;
        transform.rotation = Quaternion.FromToRotation(fwdNow.normalized, to.normalized) * transform.rotation;
        var e = transform.eulerAngles; transform.rotation = Quaternion.Euler(0, e.y, 0);
    }

    /// Reach out to a point on the table (playing a tile there) and come back.
    public void Gesture(Vector3 point) { gesture = point; gestureAt = Time.time; }
    public void Win() { if (anim != null) anim.SetTrigger("win"); }
    public void Lose() { if (anim != null) anim.SetTrigger("lose"); }
    public void Sit(bool on) { seated = on; if (anim != null) anim.SetBool("sit", on); }
    public void Walking(bool on) { if (anim != null) anim.SetBool("walking", on); }

    bool Idle => anim == null || (anim.GetCurrentAnimatorStateInfo(0).IsName("SitIdle") && !anim.IsInTransition(0));

    void LateUpdate()
    {
        float t = Time.time + seed;
        var fwd = transform.TransformDirection(localFwd); fwd.y = 0; fwd.Normalize();
        var up = Vector3.up;
        var right = Vector3.Cross(up, fwd);

        // looking around now and then, mostly at the table
        if (Time.time > nextLook)
        {
            nextLook = Time.time + Random.Range(1.8f, 5f);
            lookYawTo = Random.value < 0.55f ? Random.Range(-12f, 12f) : Random.Range(-38f, 38f);
            lookPitch = Random.value < 0.6f ? 8f : Random.Range(-6f, 4f);
        }
        lookYaw = Mathf.Lerp(lookYaw, lookYawTo, 1 - Mathf.Exp(-Time.deltaTime * 4f));
        float calm = Idle ? 1 : 0.2f;
        if (head != null) head.rotation = Quaternion.AngleAxis(lookYaw * calm, up) * Quaternion.AngleAxis((lookPitch + Mathf.Sin(t * 0.7f) * 1.5f) * calm, right) * head.rotation;

        // forearms on the table (while sitting quietly), a hand going out to play
        ik = Mathf.MoveTowards(ik, seated && Idle ? 1 : 0, Time.deltaTime * 4f);
        if (ik > 0.001f)
        {
            var root = transform.position; root.y = tableY;
            for (int i = 0; i < 2; i++)
            {
                if (upperLen[i] <= 0) continue;
                float wob = Mathf.Sin(t * 0.5f + i * 2.1f) * 0.08f;
                var target = root + fwd * (reach + wob) + right * side[i] * spread + up * 0.12f;
                float gt = Time.time - gestureAt;
                if (gt < 1f && (Vector3.Dot(gesture - root, right) >= 0 ? 1 : -1) == side[i])
                {
                    float k = gt < 0.35f ? Ui.Ease(gt / 0.35f) : gt < 0.6f ? 1f : 1f - Ui.Ease((gt - 0.6f) / 0.4f);
                    var lift = up * Mathf.Sin(Mathf.Clamp01(gt / 0.35f) * Mathf.PI) * 0.6f;
                    target = Vector3.Lerp(target, gesture + up * 0.2f, k) + (gt < 0.35f ? lift : Vector3.zero);
                }
                Reach(i, target, fwd, right, up, ik);
            }
        }

        // blinking
        if (Time.time > nextBlink) { blinkAt = Time.time; nextBlink = Time.time + Random.Range(2f, 5.5f); }
        float closed = 0;
        if (blinkAt >= 0) { float b = (Time.time - blinkAt) / 0.16f; if (b < 1) closed = Mathf.Sin(b * Mathf.PI); else blinkAt = -1; }
        for (int i = 0; i < 2; i++)
        {
            if (lid[i] == null) continue;
            lid[i].localRotation = Quaternion.Euler(closed * 40f, 0, 0) * lidRest[i];
        }
    }

    /// Two-bone reach: the hand to `target`, the elbow out, down and a little back (blended by w over the animation).
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
        var shWas = sh.rotation;
        var shTo = Quaternion.FromToRotation(el.position - S, elbowWant - S) * sh.rotation;
        sh.rotation = Quaternion.Slerp(shWas, shTo, w);
        var handWant = S + dir * d;
        var elWas = el.rotation;
        var elTo = Quaternion.FromToRotation(hd.position - el.position, handWant - el.position) * el.rotation;
        el.rotation = Quaternion.Slerp(elWas, elTo, w);
    }
}
