// The café regulars, designed in Unity: each one is sculpted from body shapes (in meters, about 1.7 m tall),
// given a skeleton, skinned, dressed and painted, then saved as a prefab with its own Animator and animations
// (sitting at a table, cheering, sulking, standing, walking).
// Run from the menu (Kings Battle → Build characters) or by the build script before every build.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KingsBattle.Forge
{
    public class Spec
    {
        public string name;
        public bool female;
        public int age;                      // 0 young, 1 middle-aged, 2 old
        public Color skin, hair, brows, iris = new Color(0.27f, 0.17f, 0.09f);
        public string hairStyle = "short";   // short, curly, receding, long, none
        public string mustache, beard;       // thick / trim; full / short
        public string headwear;              // tarboosh, emma, hijab
        public Color scarf;
        public string top;                   // suit, polo, tee, blouse, galabeya
        public Color cloth, cloth2, pants, shoes;
        public bool stripes, glasses, sebha, watch, earrings, tie;
        public float belly;
        public float tall = 1, shoulders = 1, arms = 1, hunch, chest = 1, waist = 1, hips = 1, neck = 1, muscle = 0.4f;
    }

    public static class CharacterForge
    {
        static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        // the six regulars (the same people as in the web game)
        public static readonly Spec[] Specs =
        {
            // عم فتحي: an old-school effendi in a tarboosh, suit and glasses
            new Spec { name = "AmFathi", age = 2, skin = C(0xa77a5e), hair = C(0xd8d4cc), brows = C(0xb9b2a6), mustache = "thick", headwear = "tarboosh",
                top = "suit", cloth = C(0xf1eee6), cloth2 = C(0x4e4238), pants = C(0x4e4238), shoes = C(0x2a1a10), glasses = true, tie = true, belly = 0.6f,
                tall = 0.96f, shoulders = 0.92f, arms = 0.86f, hunch = 0.12f, chest = 0.85f, waist = 1.08f, hips = 0.98f, neck = 0.9f, muscle = 0.05f },
            // كابتن سامي: a retired football captain, thick moustache, navy polo, watch
            new Spec { name = "CaptainSami", age = 1, skin = C(0x8f654b), hair = C(0x2c231d), brows = C(0x1a120c), hairStyle = "receding", mustache = "thick",
                top = "polo", cloth = C(0x24406a), pants = C(0x7d6d55), shoes = C(0x3a2618), watch = true, belly = 0.18f,
                tall = 1.04f, shoulders = 1.14f, arms = 1.22f, chest = 1.25f, waist = 0.94f, hips = 0.96f, neck = 1.2f, muscle = 1f },
            // زيكو: a young man from the neighbourhood, curly hair and a short beard
            new Spec { name = "Ziko", age = 0, skin = C(0x7d5642), hair = C(0x1f1610), brows = C(0x140e0a), hairStyle = "curly", mustache = "trim", beard = "short",
                top = "tee", cloth = C(0x2f7d5d), pants = C(0x34465e), shoes = C(0xe9e6e0),
                tall = 1.03f, shoulders = 1f, arms = 0.95f, chest = 0.94f, waist = 0.86f, hips = 0.92f, neck = 0.95f, muscle = 0.55f },
            // منى: a mother of three in a terracotta hijab
            new Spec { name = "Mona", female = true, age = 1, skin = C(0xb98c70), hair = C(0x1e130c), brows = C(0x24160d), hairStyle = "none", headwear = "hijab", scarf = C(0xb5523b),
                top = "blouse", cloth = C(0x3d3350), pants = C(0x2a2433), shoes = C(0x1e1814),
                tall = 0.95f, shoulders = 0.92f, arms = 0.95f, chest = 1.05f, waist = 0.98f, hips = 1.12f, neck = 1f, muscle = 0.2f },
            // ندى: a university student with long dark hair
            new Spec { name = "Nada", female = true, age = 0, skin = C(0xc79b7e), hair = C(0x2a180f), brows = C(0x2a180f), hairStyle = "long", iris = C(0x5a3a1c),
                top = "blouse", cloth = C(0xd9a441), pants = C(0x3a4a66), shoes = C(0xd8cfc2), earrings = true,
                tall = 0.97f, shoulders = 0.88f, arms = 0.88f, chest = 0.88f, waist = 0.84f, hips = 1f, neck = 0.92f, muscle = 0.3f },
            // الحاج محمود: a Saidi elder in a striped galabeya and white emma, prayer beads in hand
            new Spec { name = "HajjMahmoud", age = 2, skin = C(0x825a42), hair = C(0xd8d4cc), brows = C(0xcfc8bc), mustache = "thick", beard = "full", headwear = "emma",
                top = "galabeya", cloth = C(0xd9d1c0), cloth2 = C(0x8a7f6c), stripes = true, pants = C(0xd9d1c0), shoes = C(0x5a3a22), sebha = true, belly = 0.5f,
                tall = 1f, shoulders = 1.08f, arms = 1.06f, hunch = 0.1f, chest = 1.1f, waist = 1.15f, hips = 1.08f, neck = 1.12f, muscle = 0.45f },
        };

        // the skeleton (parent of each bone; -1: the root)
        public enum B { Hips, Spine, Chest, Neck, Head, ClavL, UpperArmL, LowerArmL, HandL, ClavR, UpperArmR, LowerArmR, HandR, UpperLegL, LowerLegL, FootL, UpperLegR, LowerLegR, FootR, EyeL, EyeR, LidL, LidR, Count }
        static readonly B[] Parent =
        {
            (B)(-1), B.Hips, B.Spine, B.Chest, B.Neck, B.Chest, B.ClavL, B.UpperArmL, B.LowerArmL, B.Chest, B.ClavR, B.UpperArmR, B.LowerArmR,
            B.Hips, B.UpperLegL, B.LowerLegL, B.Hips, B.UpperLegR, B.LowerLegR, B.Head, B.Head, B.Head, B.Head,
        };

        const string Out = "Assets/Generated/Characters";
        const string PrefabDir = "Assets/Resources/Characters";

        [MenuItem("Kings Battle/Build characters")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Out);
            Directory.CreateDirectory(PrefabDir);
            var t0 = DateTime.Now;
            foreach (var s in Specs)
            {
                try { Build(s); }
                catch (Exception e) { Debug.LogError("[forge] " + s.name + ": " + e); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[forge] built {Specs.Length} characters in {(DateTime.Now - t0).TotalSeconds:0.0} s");
        }

        /* ---------------- the body plan ---------------- */

        class Rig
        {
            public Vector3[] pos = new Vector3[(int)B.Count];       // rest positions (root space, meters)
            public float hipY, headScale;
            public Vector3 headO;                                   // the head's origin (chin level, middle of the neck)
            public Vector3 H(float x, float y, float z) => headO + new Vector3(x, y, z) * headScale;   // head-space point
        }

        static Rig MakeRig(Spec s)
        {
            var r = new Rig();
            float t = s.tall, sw = 0.18f * s.shoulders * (s.female ? 0.92f : 1f);
            r.hipY = 0.92f * t;
            r.headScale = (s.female ? 0.95f : 1f) * Mathf.Lerp(1f, t, 0.5f);
            var P = r.pos;
            P[(int)B.Hips] = new Vector3(0, r.hipY, 0);
            P[(int)B.Spine] = new Vector3(0, 1.02f * t, 0);
            P[(int)B.Chest] = new Vector3(0, 1.17f * t, -0.005f);
            P[(int)B.Neck] = new Vector3(0, 1.40f * t, -0.015f + s.hunch * 0.05f);
            P[(int)B.Head] = new Vector3(0, 1.49f * t, 0.0f + s.hunch * 0.07f);
            r.headO = P[(int)B.Head];
            float shY = 1.385f * t;
            float abd = 9f * Mathf.Deg2Rad;
            float ua = 0.29f * t, fa = 0.255f * t;
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1 : 1;
                int clav = side == 0 ? (int)B.ClavL : (int)B.ClavR;
                P[clav] = new Vector3(sx * 0.03f, shY + 0.01f, -0.015f);
                P[clav + 1] = new Vector3(sx * sw, shY, -0.02f);
                P[clav + 2] = P[clav + 1] + new Vector3(sx * Mathf.Sin(abd) * ua, -Mathf.Cos(abd) * ua, 0);
                P[clav + 3] = P[clav + 2] + new Vector3(sx * Mathf.Sin(abd * 0.6f) * fa, -Mathf.Cos(abd * 0.6f) * fa, 0.01f);
                int leg = side == 0 ? (int)B.UpperLegL : (int)B.UpperLegR;
                float hx = sx * 0.09f * s.hips;
                P[leg] = new Vector3(hx, r.hipY - 0.04f, 0);
                P[leg + 1] = new Vector3(hx * 0.95f, r.hipY - 0.04f - 0.41f * t, 0.01f);
                P[leg + 2] = new Vector3(hx * 0.92f, 0.08f, -0.01f);
                // eyes and lids (in the head)
                int eye = side == 0 ? (int)B.EyeL : (int)B.EyeR, lid = side == 0 ? (int)B.LidL : (int)B.LidR;
                P[eye] = r.H(sx * 0.031f, 0.107f, 0.066f);
                P[lid] = P[eye];
            }
            return r;
        }

        /* ---------------- sculpting ---------------- */

        static List<Layer> Sculpt(Spec s, Rig r)
        {
            var P = r.pos;
            var layers = new List<Layer>();
            float t = s.tall, mu = s.muscle, fem = s.female ? 1 : 0;
            Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
            int Hb = (int)B.Hips, Sp = (int)B.Spine, Ch = (int)B.Chest, Nk = (int)B.Neck, Hd = (int)B.Head;

            // --- the torso (for clothes; the skin under them is never seen) ---
            var torso = new List<Prim>
            {
                Prim.Ell(Hb, V(0, r.hipY + 0.02f, -0.005f), V(0.165f * s.hips * (1 + fem * 0.1f), 0.11f, 0.11f)),
                Prim.Ell(Sp, V(0, 1.06f * t, 0.005f), V(0.135f * s.waist * (1 + s.belly * 0.22f), 0.12f, 0.10f * (1 + s.belly * 0.3f))),
                Prim.Ell(Ch, V(0, 1.25f * t, 0), V(0.155f * s.chest * s.shoulders, 0.15f, 0.105f * Mathf.Lerp(1, s.chest, 0.5f))),
                Prim.Cap(Ch, V(-0.15f * s.shoulders, 1.375f * t, -0.025f), V(0.15f * s.shoulders, 1.375f * t, -0.025f), 0.055f),
                Prim.Cap(Ch, V(0, 1.33f * t, -0.035f), V(0, 1.43f * t, -0.02f), 0.065f * s.neck),
                Prim.Ell(Ch, V(-0.075f, 1.28f * t, -0.07f), V(0.07f, 0.09f, 0.04f)),
                Prim.Ell(Ch, V(0.075f, 1.28f * t, -0.07f), V(0.07f, 0.09f, 0.04f)),
                Prim.Ell(Hb, V(-0.068f * s.hips, r.hipY - 0.03f, -0.065f), V(0.085f, 0.09f, 0.07f) * (1 + fem * 0.08f)),
                Prim.Ell(Hb, V(0.068f * s.hips, r.hipY - 0.03f, -0.065f), V(0.085f, 0.09f, 0.07f) * (1 + fem * 0.08f)),
            };
            if (s.belly > 0.1f) torso.Add(Prim.Ell(Sp, V(0, 1.04f * t, 0.05f + s.belly * 0.04f), V(0.12f + s.belly * 0.05f, 0.105f, 0.07f + s.belly * 0.055f)));
            if (s.female) { torso.Add(Prim.Ball(Ch, V(-0.058f, 1.255f * t, 0.075f * s.chest), 0.055f * s.chest)); torso.Add(Prim.Ball(Ch, V(0.058f, 1.255f * t, 0.075f * s.chest), 0.055f * s.chest)); }
            else { torso.Add(Prim.Ell(Ch, V(-0.065f, 1.285f * t, 0.065f + 0.015f * mu), V(0.07f, 0.05f, 0.035f) * (1 + 0.2f * mu))); torso.Add(Prim.Ell(Ch, V(0.065f, 1.285f * t, 0.065f + 0.015f * mu), V(0.07f, 0.05f, 0.035f) * (1 + 0.2f * mu))); }

            // --- arms (to the wrist) and hands ---
            List<Prim> Arm(int side, float upTo)    // upTo: how far the sleeve or skin goes (1 = shoulder to wrist, 2 = with the hand)
            {
                int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR;
                Vector3 S = P[ua], E = P[ua + 1], W = P[ua + 2];
                float th = 0.042f * Mathf.Lerp(1f, s.arms, 0.8f) * (s.female ? 0.9f : 1f);
                var l = new List<Prim>
                {
                    Prim.Ball(ua, S + V(0, -0.01f, 0), 0.052f * Mathf.Lerp(1, s.shoulders, 0.6f)),
                    Prim.Cap(ua, S, E, th, th * 0.8f),
                    Prim.Ell(ua, Vector3.Lerp(S, E, 0.42f) + V(0, 0, 0.012f), V(th * 0.95f, 0.07f, th * (0.95f + 0.25f * mu))),
                };
                if (upTo > 1.2f || upTo <= 1f)
                {
                    l.Add(Prim.Cap(ua + 1, E, W, th * 0.82f, th * 0.6f));
                    l.Add(Prim.Ell(ua + 1, Vector3.Lerp(E, W, 0.28f) + V(0, 0, 0.006f), V(th * 0.85f, 0.065f, th * 0.82f)));
                }
                return l;
            }
            List<Prim> Hand(int side)
            {
                int hb = side == 0 ? (int)B.HandL : (int)B.HandR;
                float sx = side == 0 ? -1 : 1;
                Vector3 W = P[hb];
                var dn = (P[hb] - P[hb - 1]).normalized;
                var l = new List<Prim>
                {
                    Prim.RBox(hb, W + dn * 0.045f, V(0.018f, 0.045f, 0.04f) * (s.female ? 0.9f : 1f), 0.016f, Quaternion.LookRotation(Vector3.forward, -dn)),
                };
                for (int f = 0; f < 4; f++)
                {
                    float z = -0.027f + f * 0.018f;
                    var root = W + dn * 0.088f + V(0, 0, z);
                    float len = (f == 1 || f == 2 ? 0.07f : 0.058f) * (s.female ? 0.92f : 1f);
                    l.Add(Prim.Cap(hb, root, root + dn * len + V(sx * 0.008f, 0, 0.004f), 0.0085f, 0.007f));
                }
                var th = W + dn * 0.03f + V(0, 0, 0.035f);
                l.Add(Prim.Cap(hb, th, th + dn * 0.035f + V(0, -0.005f, 0.035f), 0.011f, 0.008f));
                return l;
            }

            // --- legs ---
            List<Prim> Leg(int side)
            {
                int ul = side == 0 ? (int)B.UpperLegL : (int)B.UpperLegR;
                Vector3 Hp = P[ul], K = P[ul + 1], A = P[ul + 2];
                float th = 0.075f * Mathf.Sqrt(s.hips) * (s.female ? 1.04f : 1f);
                return new List<Prim>
                {
                    Prim.Cap(ul, Hp + V(0, 0.03f, 0), K, th, 0.05f),
                    Prim.Ell(ul, Vector3.Lerp(Hp, K, 0.35f), V(th * 1.02f, 0.13f, th * 1.02f)),
                    Prim.Ball(ul + 1, K, 0.052f),
                    Prim.Cap(ul + 1, K, A + V(0, 0.03f, 0), 0.046f, 0.032f),
                    Prim.Ell(ul + 1, Vector3.Lerp(K, A, 0.3f) + V(0, 0, -0.022f), V(0.045f, 0.09f, 0.045f) * (0.92f + 0.16f * mu)),
                };
            }

            var skinCol = s.skin;
            // ---------- skin: the neck, arms, hands (the face has its own finer layer) ----------
            var skin = new Layer { name = "Skin", color = skinCol, k = 0.025f, cell = 0.011f };
            skin.prims.Add(Prim.Cap(Nk, V(0, 1.37f * t, -0.02f), P[Hd] + V(0, 0.04f, 0), 0.052f * s.neck * (s.female ? 0.85f : 1f)));
            bool shortSleeves = s.top == "polo" || s.top == "tee";
            for (int side = 0; side < 2; side++)
            {
                if (shortSleeves) skin.prims.AddRange(Arm(side, 2));
                else { int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR; skin.prims.Add(Prim.Cap(ua + 1, Vector3.Lerp(P[ua + 1], P[ua + 2], 0.7f), P[ua + 2], 0.03f, 0.026f)); }
                skin.prims.AddRange(Hand(side));
            }
            // the neckline of a blouse shows the chest a little
            if (s.top == "blouse" && s.headwear != "hijab") skin.prims.Add(Prim.Ell(Ch, V(0, 1.35f * t, 0.035f), V(0.07f, 0.06f, 0.05f)));
            layers.Add(skin);

            // ---------- the face and head ----------
            layers.Add(Head(s, r));
            // ---------- hair, brows, moustache, beard ----------
            var hair = Hair(s, r);
            if (hair.prims.Count > 0) layers.Add(hair);
            layers.Add(FaceHair(s, r));

            // ---------- the top ----------
            float sleeve = shortSleeves ? 0.45f : 1f;
            var top = new Layer { name = "Top", color = s.cloth, k = 0.035f, grow = s.top == "galabeya" ? 0.016f : 0.011f, cell = 0.013f };
            top.prims.AddRange(torso);
            for (int side = 0; side < 2; side++)
            {
                int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR;
                var arm = Arm(side, 1);
                if (shortSleeves)
                {
                    // the sleeve ends part way down the upper arm, a little wider than the arm
                    var S = P[ua]; var E = P[ua + 1];
                    arm = new List<Prim> { Prim.Ball(ua, S + V(0, -0.01f, 0), 0.058f), Prim.Cap(ua, S, Vector3.Lerp(S, E, sleeve), 0.05f, 0.047f) };
                }
                else
                {
                    var E = P[ua + 1]; var W = P[ua + 2];
                    arm.Add(Prim.Cap(ua + 1, E, Vector3.Lerp(E, W, 0.88f), 0.04f, 0.035f));
                }
                top.prims.AddRange(arm);
            }
            float hem = s.top == "blouse" ? r.hipY - 0.08f : s.top == "galabeya" ? 0.05f : r.hipY - 0.03f;
            if (s.top == "galabeya")
            {
                // the robe falls from the hips to the ankles, wide at the hem
                top.prims.Add(Prim.Cap(Hb, V(0, r.hipY, 0), V(0, 0.1f, 0.01f), 0.19f * s.hips, 0.245f));
            }
            var wristCut = new List<(Vector3 at, Vector3 dir)>();
            top.clip = p => hem - p.y;
            if (s.top == "polo" || s.top == "suit") top.prims.Add(Prim.Cap(Ch, V(-0.055f, 1.43f * t, 0.0f), V(0.055f, 1.43f * t, 0.0f), 0.022f).Paint(s.top == "suit" ? s.cloth : Color.Lerp(s.cloth, Color.white, 0.15f)));
            if (s.tie) top.prims.Add(Prim.RBox(Ch, V(0, 1.30f * t, 0.098f + 0.01f * s.chest), V(0.022f, 0.11f, 0.008f), 0.006f).Paint(C(0x7a1722)));
            if (s.top == "polo") for (int b = 0; b < 3; b++) top.prims.Add(Prim.Ball(Ch, V(0, (1.405f - b * 0.03f) * t, 0.098f), 0.006f).Paint(Color.Lerp(s.cloth, Color.white, 0.6f)));
            if (s.watch) top.prims.Add(Prim.Cap((int)B.LowerArmL, Vector3.Lerp(P[(int)B.LowerArmL], P[(int)B.HandL], 0.9f) + V(0, 0, 0), Vector3.Lerp(P[(int)B.LowerArmL], P[(int)B.HandL], 0.97f), 0.03f).Paint(C(0xd8a84a)));
            layers.Add(top);

            // the jacket over the shirt, open in a V at the front
            if (s.top == "suit")
            {
                var jacket = new Layer { name = "Jacket", color = s.cloth2, k = 0.035f, grow = 0.026f, cell = 0.014f };
                jacket.prims.AddRange(torso);
                jacket.prims.Add(Prim.Cap(Ch, V(-0.17f * s.shoulders, 1.385f * t, -0.025f), V(0.17f * s.shoulders, 1.385f * t, -0.025f), 0.05f));
                for (int side = 0; side < 2; side++)
                {
                    int ua = side == 0 ? (int)B.UpperArmL : (int)B.UpperArmR;
                    jacket.prims.Add(Prim.Ball(ua, P[ua], 0.056f));
                    jacket.prims.Add(Prim.Cap(ua, P[ua], P[ua + 1], 0.046f, 0.042f));
                    jacket.prims.Add(Prim.Cap(ua + 1, P[ua + 1], Vector3.Lerp(P[ua + 1], P[ua + 2], 0.84f), 0.041f, 0.037f));
                }
                float jt = 1.42f * t, jb = r.hipY - 0.1f;
                jacket.clip = p =>
                {
                    float d = jb - p.y;
                    // the V: open in front above the buttons
                    if (p.z > 0.02f && p.y > 1.12f * t) d = Mathf.Max(d, -(Mathf.Abs(p.x) - Mathf.Lerp(0.015f, 0.085f, (p.y - 1.12f * t) / (jt - 1.12f * t))));
                    return d;
                };
                jacket.prims.Add(Prim.Ball(Sp, V(0, 1.1f * t, 0.12f + s.belly * 0.06f), 0.008f).Paint(C(0x1a120c)));
                layers.Add(jacket);
            }

            // ---------- trousers ----------
            if (s.top != "galabeya")
            {
                var pants = new Layer { name = "Trousers", color = s.pants, k = 0.03f, grow = 0.012f, cell = 0.013f };
                pants.prims.Add(torso[0]); pants.prims.Add(torso[1]); pants.prims.Add(torso[7]); pants.prims.Add(torso[8]);
                for (int side = 0; side < 2; side++) pants.prims.AddRange(Leg(side));
                float waistTop = 1.07f * t;
                pants.clip = p => Mathf.Max(p.y - waistTop, 0.09f - p.y);
                layers.Add(pants);
            }

            // ---------- shoes ----------
            var shoes = new Layer { name = "Shoes", color = s.shoes, k = 0.02f, cell = 0.009f };
            for (int side = 0; side < 2; side++)
            {
                int fb = side == 0 ? (int)B.FootL : (int)B.FootR;
                var A = P[fb];
                float len = s.female ? 0.115f : 0.13f;
                shoes.prims.Add(Prim.RBox(fb, A + V(0, -0.045f, 0.045f), V(0.045f, 0.035f, len), 0.03f));
                shoes.prims.Add(Prim.Cap(fb, A + V(0, -0.02f, -0.02f), A + V(0, 0.02f, -0.01f), 0.042f));
            }
            layers.Add(shoes);

            // ---------- headwear ----------
            if (s.headwear == "tarboosh")
            {
                var hw = new Layer { name = "Tarboosh", color = C(0x9e1b17), k = 0.01f, cell = 0.006f };
                // a truncated cone of red felt, its black tassel falling at the back
                hw.prims.Add(Prim.Cap(Hd, r.H(0, 0.18f, -0.005f), r.H(0, 0.265f, -0.012f), 0.083f, 0.068f));
                hw.prims.Add(Prim.Cap(Hd, r.H(0, 0.27f, -0.015f), r.H(0.01f, 0.24f, -0.075f), 0.006f).Paint(C(0x1a1414)));
                hw.prims.Add(Prim.Cap(Hd, r.H(0.01f, 0.24f, -0.075f), r.H(0.02f, 0.17f, -0.085f), 0.012f, 0.016f).Paint(C(0x1a1414)));
                hw.clip = p => (r.headO.y + 0.17f * r.headScale) - p.y;
                layers.Add(hw);
            }
            else if (s.headwear == "emma")
            {
                // the white emma: cloth wound around a cap
                var hw = new Layer { name = "Emma", color = C(0xf4f1ea), k = 0.012f, cell = 0.006f };
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.155f + i * 0.022f, rr = 0.092f - i * 0.006f;
                    for (int a = 0; a < 12; a++)
                    {
                        float a0 = a * Mathf.PI * 2 / 12 + i * 0.4f, a1 = (a + 1) * Mathf.PI * 2 / 12 + i * 0.4f;
                        hw.prims.Add(Prim.Cap(Hd, r.H(Mathf.Sin(a0) * rr, y + Mathf.Sin(a0 * 2) * 0.004f, Mathf.Cos(a0) * rr - 0.008f), r.H(Mathf.Sin(a1) * rr, y + Mathf.Sin(a1 * 2) * 0.004f, Mathf.Cos(a1) * rr - 0.008f), 0.016f));
                    }
                }
                hw.prims.Add(Prim.Ell(Hd, r.H(0, 0.2f, -0.008f), V(0.075f, 0.045f, 0.08f) * r.headScale).Paint(C(0xe8e2d6)));
                layers.Add(hw);
            }
            else if (s.headwear == "hijab")
            {
                var hw = new Layer { name = "Hijab", color = s.scarf, k = 0.04f, grow = 0.012f, cell = 0.0055f };
                hw.prims.Add(Prim.Ell(Hd, r.H(0, 0.115f, -0.008f), V(0.085f, 0.1f, 0.098f) * r.headScale));
                hw.prims.Add(Prim.Cap(Nk, V(0, 1.38f * t, -0.02f), r.H(0, 0.02f, -0.01f), 0.075f, 0.07f));
                hw.prims.Add(Prim.Ell(Ch, V(0, 1.36f * t, -0.01f), V(0.17f, 0.06f, 0.13f)));
                // the face shows through an oval opening
                var fc = r.H(0, 0.075f, 0.09f);
                hw.clip = p =>
                {
                    var q = p - fc;
                    float oval = Mathf.Sqrt(q.x * q.x / (0.058f * 0.058f) + q.y * q.y / (0.09f * 0.09f)) - 1;
                    return p.z > fc.z - 0.05f * r.headScale ? -oval * 0.05f : -1;
                };
                layers.Add(hw);
            }

            // ---------- glasses, prayer beads, earrings ----------
            if (s.glasses)
            {
                var g = new Layer { name = "Glasses", color = C(0x2a1d14), k = 0, cell = 0.0035f };
                for (int side = 0; side < 2; side++)
                {
                    float sx = side == 0 ? -1 : 1;
                    var c = r.H(sx * 0.032f, 0.108f, 0.097f);
                    for (int a = 0; a < 14; a++)
                    {
                        float a0 = a * Mathf.PI * 2 / 14, a1 = (a + 1) * Mathf.PI * 2 / 14;
                        g.prims.Add(Prim.Cap(Hd, c + V(Mathf.Cos(a0) * 0.022f, Mathf.Sin(a0) * 0.017f, 0), c + V(Mathf.Cos(a1) * 0.022f, Mathf.Sin(a1) * 0.017f, 0), 0.0025f));
                    }
                    g.prims.Add(Prim.Cap(Hd, c + V(sx * 0.022f, 0.004f, 0), r.H(sx * 0.074f, 0.112f, 0.0f), 0.0022f));
                }
                g.prims.Add(Prim.Cap(Hd, r.H(-0.01f, 0.113f, 0.098f), r.H(0.01f, 0.113f, 0.098f), 0.0025f));
                layers.Add(g);
            }
            if (s.sebha)
            {
                var b = new Layer { name = "Sebha", color = C(0x6b2a14), k = 0, cell = 0.004f };
                var hb = P[(int)B.HandR]; var dn = (P[(int)B.HandR] - P[(int)B.LowerArmR]).normalized;
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16;
                    b.prims.Add(Prim.Ball((int)B.HandR, hb + dn * (0.12f + 0.05f * (1 - Mathf.Cos(a))) + V(0.012f * Mathf.Sin(a) + 0.02f, 0, 0.03f * Mathf.Sin(a)), 0.0065f));
                }
                b.prims.Add(Prim.Cap((int)B.HandR, hb + dn * 0.12f + V(0.02f, 0, 0), hb + dn * 0.2f + V(0.025f, 0, 0), 0.004f).Paint(C(0xd8a84a)));
                layers.Add(b);
            }
            if (s.earrings)
            {
                var e = new Layer { name = "Earrings", color = C(0xd8a84a), k = 0, cell = 0.003f };
                for (int side = 0; side < 2; side++) e.prims.Add(Prim.Ball(Hd, r.H((side == 0 ? -1 : 1) * 0.075f, 0.055f, 0.0f), 0.006f));
                layers.Add(e);
            }
            return layers;
        }

        /// The head: skull, jaw, cheekbones, brow, nose, lips, ears and chin, with sockets for the eyes.
        static Layer Head(Spec s, Rig r)
        {
            int Hd = (int)B.Head, Nk = (int)B.Neck;
            float fem = s.female ? 1 : 0, old = s.age / 2f, k = r.headScale;
            Vector3 H(float x, float y, float z) => r.H(x, y, z);
            Vector3 R3(float x, float y, float z) => new Vector3(x, y, z) * k;
            var lips = Color.Lerp(s.skin, new Color(0.62f, 0.3f, 0.28f), s.female ? 0.55f : 0.32f);
            var cheek = Color.Lerp(s.skin, new Color(0.8f, 0.4f, 0.36f), 0.12f);
            var L = new Layer { name = "Face", color = s.skin, k = 0.018f * k, cell = 0.0042f };
            var p = L.prims;
            p.Add(Prim.Ell(Hd, H(0, 0.118f, -0.008f), R3(0.077f, 0.094f, 0.09f)));                                     // cranium
            p.Add(Prim.Ell(Hd, H(0, 0.075f, 0.028f), R3(0.064f - fem * 0.004f, 0.082f, 0.068f)));                      // the face
            p.Add(Prim.Cap(Hd, H(-0.043f + fem * 0.006f, 0.035f, 0.03f), H(0.043f - fem * 0.006f, 0.035f, 0.03f), 0.03f * k));   // jaw
            p.Add(Prim.Ell(Hd, H(0, 0.012f, 0.066f), R3(0.026f - fem * 0.004f, 0.022f, 0.02f)));                       // chin
            for (int sd = -1; sd <= 1; sd += 2)
            {
                p.Add(Prim.Ell(Hd, H(sd * 0.046f, 0.085f, 0.058f), R3(0.021f, 0.016f, 0.018f)).Paint(cheek));          // cheekbones
                p.Add(Prim.Ell(Hd, H(sd * 0.077f, 0.098f, -0.004f), R3(0.011f, 0.029f, 0.019f), Quaternion.Euler(0, sd * 18f, 0)));   // ears
                p.Add(Prim.Ell(Hd, H(sd * 0.031f, 0.108f, 0.083f), R3(0.0145f, 0.0088f, 0.011f)).Cut(0.006f * k));           // eye openings (almond)
                p.Add(Prim.Ell(Hd, H(sd * 0.031f, 0.094f, 0.081f), R3(0.016f, 0.006f, 0.008f)));                          // lower lids
            }
            p.Add(Prim.Cap(Hd, H(-0.04f, 0.127f, 0.075f), H(0.04f, 0.127f, 0.075f), (0.011f - fem * 0.003f) * k));    // brow ridge
            // the nose: bridge, tip and wings (longer and fuller with age)
            float nl = 1 + old * 0.12f - fem * 0.08f;
            p.Add(Prim.Cap(Hd, H(0, 0.118f, 0.086f), H(0, 0.073f - 0.004f * nl, 0.104f + 0.006f * nl), 0.0085f * k, 0.011f * k));
            p.Add(Prim.Ball(Hd, H(0, 0.07f, 0.106f + 0.006f * nl), 0.0135f * k * nl));
            for (int sd = -1; sd <= 1; sd += 2) p.Add(Prim.Ball(Hd, H(sd * 0.013f, 0.066f, 0.097f), 0.0095f * k * nl));
            // lips
            p.Add(Prim.Cap(Hd, H(-0.022f, 0.047f, 0.093f), H(0.022f, 0.047f, 0.093f), (0.0075f + fem * 0.002f) * k).Paint(lips));
            p.Add(Prim.Cap(Hd, H(-0.019f, 0.038f, 0.09f), H(0.019f, 0.038f, 0.09f), (0.0085f + fem * 0.002f) * k).Paint(lips));
            p.Add(Prim.Cap(Hd, H(-0.025f, 0.0425f, 0.1f), H(0.025f, 0.0425f, 0.1f), 0.0022f * k).Cut(0.003f * k));      // the line between them
            // the neck into the head
            p.Add(Prim.Cap(Nk, H(0, -0.06f, -0.012f), H(0, 0.05f, -0.004f), 0.05f * s.neck * (s.female ? 0.86f : 1f)));
            return L;
        }

        /// Hair on the scalp (by style), the brows, moustache and beard.
        static Layer Hair(Spec s, Rig r)
        {
            int Hd = (int)B.Head;
            float k = r.headScale;
            Vector3 H(float x, float y, float z) => r.H(x, y, z);
            var L = new Layer { name = "Hair", color = s.hair, k = 0.01f * k, cell = 0.0035f };
            var p = L.prims;
            bool scalp = s.hairStyle != "none" && s.headwear != "hijab";
            var o = r.headO;
            if (scalp)
            {
                float thick = s.hairStyle == "curly" ? 0.018f : s.hairStyle == "long" ? 0.012f : 0.0095f;
                var cran = Prim.Ell(Hd, H(0, 0.118f, -0.008f), new Vector3(0.077f + thick, 0.094f + thick, 0.09f + thick) * k);
                p.Add(cran);
                if (s.hairStyle == "curly")
                    for (int i = 0; i < 26; i++)
                    {
                        float a = i * 2.39996f, y = 0.12f + 0.08f * Mathf.Sin(i * 1.3f) * 0.5f + (i % 3) * 0.02f;
                        p.Add(Prim.Ball(Hd, H(Mathf.Sin(a) * 0.08f, Mathf.Min(0.2f, y + 0.02f), Mathf.Cos(a) * 0.085f - 0.01f), 0.018f * k));
                    }
                if (s.hairStyle == "long")
                {
                    // falling down the back past the shoulders, and framing the face
                    p.Add(Prim.Ell(Hd, H(0, 0.02f, -0.06f), new Vector3(0.085f, 0.16f, 0.05f) * k));
                    for (int sd = -1; sd <= 1; sd += 2) p.Add(Prim.Ell(Hd, H(sd * 0.072f, 0.05f, 0.0f), new Vector3(0.018f, 0.11f, 0.05f) * k));
                }
                float front = s.hairStyle == "receding" ? 0.165f : 0.142f;
                L.clip = q =>
                {
                    var h = (q - o) / k;
                    // the hairline: high at the forehead, down at the nape; clear of the ears and face
                    float line = front - (0.09f - h.z) * (s.hairStyle == "long" ? 1.6f : 1.05f);
                    float d = line - h.y;
                    if (s.hairStyle == "long" && h.z < 0.03f) d = Mathf.Min(d, -0.001f + (h.y < -0.12f ? (-0.12f - h.y) : -0.01f));
                    if (s.hairStyle == "receding" && h.y > 0.18f && Mathf.Abs(h.x) < 0.04f && h.z > -0.02f) d = 0.01f;   // thin on top
                    if (Mathf.Abs(h.x) > 0.06f && h.y < 0.125f && h.z > -0.02f && s.hairStyle != "long") d = Mathf.Max(d, 0.002f);   // the ears show
                    return d;
                };
            }
            return L;
        }

        /// Brows, moustache and beard (their own layer, so the scalp's hairline never touches them).
        static Layer FaceHair(Spec s, Rig r)
        {
            int Hd = (int)B.Head;
            float k = r.headScale;
            Vector3 H(float x, float y, float z) => r.H(x, y, z);
            var L = new Layer { name = "FaceHair", color = s.hair, k = 0.008f * k, cell = 0.0038f };
            var p = L.prims;
            var o = r.headO;
            float bw = s.age == 2 ? 0.0055f : 0.0045f;
            for (int sd = -1; sd <= 1; sd += 2)
                p.Add(Prim.Cap(Hd, H(sd * 0.016f, 0.13f, 0.091f), H(sd * 0.047f, 0.127f - (s.female ? 0.002f : 0), 0.078f), bw * k, bw * 0.75f * k).Paint(s.brows));
            if (s.mustache == "thick")
                for (int sd = -1; sd <= 1; sd += 2) p.Add(Prim.Cap(Hd, H(0, 0.056f, 0.1f), H(sd * 0.03f, 0.047f, 0.088f), 0.0075f * k, 0.0055f * k));
            else if (s.mustache == "trim")
                p.Add(Prim.Cap(Hd, H(-0.022f, 0.055f, 0.098f), H(0.022f, 0.055f, 0.098f), 0.0042f * k));
            if (s.beard != null)
            {
                float g = s.beard == "full" ? 0.012f : 0.005f;
                p.Add(Prim.Cap(Hd, H(-0.045f, 0.03f, 0.028f), H(0.045f, 0.03f, 0.028f), (0.03f + g) * k));
                p.Add(Prim.Ell(Hd, H(0, s.beard == "full" ? 0f : 0.01f, 0.07f), new Vector3(0.03f + g, 0.026f + g * 1.6f, 0.022f + g) * k));
                var mouth = H(0, 0.043f, 0.09f);
                L.clip = q =>
                {
                    var h = (q - o) / k;
                    bool beardZone = h.y < 0.062f && h.z > -0.01f;
                    bool otherHair = h.y > 0.049f && h.z > 0.07f;                            // brows and moustache
                    if (!beardZone && !otherHair) return 0.01f;
                    if (h.y < 0.049f && Vector3.Distance(q, mouth) / k < 0.024f) return 0.01f;   // the mouth stays clear
                    return -1;
                };
            }
            return L;
        }

        /* ---------------- building the mesh and the prefab ---------------- */

        static void Build(Spec s)
        {
            var rig = MakeRig(s);
            var root = new GameObject(s.name);
            var bones = new Transform[(int)B.Count];
            for (int i = 0; i < (int)B.Count; i++)
            {
                bones[i] = new GameObject(((B)i).ToString()).transform;
                var par = (int)Parent[i] < 0 ? root.transform : bones[(int)Parent[i]];
                bones[i].SetParent(par, false);
                bones[i].position = rig.pos[i];
            }
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;

            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            foreach (var L in Sculpt(s, rig))
            {
                Mesher.Build(L, verts, norms, tris);
                if (verts.Count == 0) { Debug.LogWarning($"[forge] {s.name}/{L.name}: empty"); continue; }
                var mesh = new Mesh { name = s.name + "_" + L.name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0);
                var cols = new Color[verts.Count]; var wrap = new Vector2[verts.Count]; var bw = new BoneWeight[verts.Count];
                var dist = new float[(int)B.Count];
                for (int v = 0; v < verts.Count; v++)
                {
                    var q = verts[v];
                    // the colour: the nearest painted shape, blended softly into the layer's colour
                    Color c = L.color; float cw = 0;
                    for (int d = 0; d < dist.Length; d++) dist[d] = 1e9f;
                    float best = 1e9f;
                    foreach (var pr in L.prims) { if (pr.carve) continue; float dd = pr.Dist(q); if (dd < dist[pr.bone]) dist[pr.bone] = dd; if (dd < best) best = dd; }
                    var acc = Color.black; float wsum = 0;
                    foreach (var pr in L.prims)
                    {
                        if (pr.carve) continue;
                        float dq = pr.Dist(q);
                        float w = Mathf.Exp(-(dq - best) / 0.004f);
                        if (pr.color.a > 0) w *= Mathf.Clamp01(1f - (dq - L.grow) / 0.004f);   // paint only where the shape reaches the surface
                        acc += (pr.color.a > 0 ? pr.color : L.color) * w; wsum += w;
                    }
                    if (wsum > 0) c = acc / wsum;
                    c.a = 1;
                    cols[v] = c;
                    wrap[v] = new Vector2(Mathf.Atan2(q.x, q.z) / (Mathf.PI * 2) + 0.5f, q.y);
                    // skin weights: the nearest bones by distance to their shapes, blended
                    var ws = new List<(int b, float w)>();
                    for (int b = 0; b < dist.Length; b++) if (dist[b] < 1e8f) ws.Add((b, Mathf.Exp(-(dist[b] - best) / 0.022f)));
                    ws.Sort((x, y) => y.w.CompareTo(x.w));
                    float tot = 0; for (int i = 0; i < Mathf.Min(4, ws.Count); i++) tot += ws[i].w;
                    var bwv = new BoneWeight();
                    if (ws.Count > 0) { bwv.boneIndex0 = ws[0].b; bwv.weight0 = ws[0].w / tot; }
                    if (ws.Count > 1) { bwv.boneIndex1 = ws[1].b; bwv.weight1 = ws[1].w / tot; }
                    if (ws.Count > 2) { bwv.boneIndex2 = ws[2].b; bwv.weight2 = ws[2].w / tot; }
                    if (ws.Count > 3) { bwv.boneIndex3 = ws[3].b; bwv.weight3 = ws[3].w / tot; }
                    bw[v] = bwv;
                }
                mesh.colors = cols;
                mesh.SetUVs(1, wrap);
                mesh.boneWeights = bw;
                mesh.bindposes = bind;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, $"{Out}/{mesh.name}.asset");
                var go = new GameObject(L.name);
                go.transform.SetParent(root.transform, false);
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = bones[(int)B.Hips];
                smr.sharedMaterial = MakeMaterial(s, L);
                smr.updateWhenOffscreen = false;
                smr.localBounds = new Bounds(new Vector3(0, -0.2f, 0.1f), new Vector3(1.6f, 2.4f, 1.6f));
            }
            Eyes(s, rig, bones);

            // the Animator with its animations
            var ctrl = Animations(s, rig);
            var anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{s.name}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static Material MakeMaterial(Spec s, Layer L)
        {
            var path = $"{Out}/{s.name}_{L.name}.mat";
            var m = new Material(Shader.Find("KingsBattle/Character")) { name = s.name + "_" + L.name };
            m.color = Color.white;
            bool cloth = L.name == "Top" || L.name == "Jacket" || L.name == "Trousers" || L.name == "Hijab" || L.name == "Emma";
            m.SetFloat("_Glossiness", L.name == "Face" || L.name == "Skin" ? 0.42f : L.name == "Shoes" ? 0.62f : L.name == "Glasses" ? 0.8f : L.name == "Sebha" || L.name == "Earrings" ? 0.75f : L.name == "Hair" || L.name == "FaceHair" ? 0.32f : 0.14f);
            m.SetFloat("_Sheen", L.name == "Face" || L.name == "Skin" ? 0.32f : L.name == "Hair" || L.name == "FaceHair" ? 0.5f : cloth ? 0.55f : 0.15f);
            m.SetFloat("_Weave", cloth ? 1f : 0f);
            m.SetFloat("_Metallic", L.name == "Earrings" ? 0.9f : 0f);
            if (L.name == "Top" && s.stripes) { m.SetColor("_StripeColor", new Color(s.cloth2.r, s.cloth2.g, s.cloth2.b, 0.85f)); m.SetFloat("_StripeFreq", 70); m.SetFloat("_StripeWidth", 0.18f); }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// Eyeballs (white, an iris of their colour, a dark pupil, a glint) and the upper lids that blink.
        static void Eyes(Spec s, Rig r, Transform[] bones)
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = s.name + "_Eye" };
            var px = new Color[128 * 128];
            var white = new Color(0.93f, 0.9f, 0.86f);
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x + 0.5f) / 128 - 0.5f, dy = (y + 0.5f) / 128 - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = white;
                    float ir = 0.34f;
                    if (d < ir) c = Color.Lerp(s.iris * 1.25f, s.iris * 0.55f, d / ir);
                    if (d < ir * 0.4f) c = new Color(0.03f, 0.02f, 0.02f);
                    if (Mathf.Abs(d - ir) < 0.012f) c = Color.Lerp(c, s.iris * 0.3f, 0.6f);
                    if (new Vector2(dx + 0.08f, dy - 0.08f).magnitude < 0.045f) c = Color.white;
                    c.a = 1; px[y * 128 + x] = c;
                }
            tex.SetPixels(px); tex.Apply(true);
            AssetDatabase.CreateAsset(tex, $"{Out}/{s.name}_Eye.asset");
            var eyeMat = new Material(Shader.Find("KingsBattle/Character")) { name = s.name + "_Eye" };
            eyeMat.mainTexture = tex; eyeMat.SetFloat("_Glossiness", 0.88f); eyeMat.SetFloat("_Sheen", 0f);
            AssetDatabase.CreateAsset(eyeMat, $"{Out}/{s.name}_Eye.mat");
            var lidMat = new Material(Shader.Find("KingsBattle/Character")) { name = s.name + "_Lid", color = Color.Lerp(s.skin, Color.black, 0.06f) };
            lidMat.SetFloat("_Glossiness", 0.4f); lidMat.SetFloat("_Sheen", 0.3f);
            AssetDatabase.CreateAsset(lidMat, $"{Out}/{s.name}_Lid.mat");

            float er = 0.0125f * r.headScale;
            var eyeMesh = Sphere(er, true, 1f); eyeMesh.name = s.name + "_EyeBall";
            AssetDatabase.CreateAsset(eyeMesh, $"{Out}/{eyeMesh.name}.asset");
            var lidMesh = Sphere(er * 1.12f, false, 0.42f); lidMesh.name = s.name + "_EyeLid";
            AssetDatabase.CreateAsset(lidMesh, $"{Out}/{lidMesh.name}.asset");
            for (int side = 0; side < 2; side++)
            {
                var eb = bones[side == 0 ? (int)B.EyeL : (int)B.EyeR];
                var e = new GameObject("Eyeball"); e.transform.SetParent(eb, false);
                e.AddComponent<MeshFilter>().sharedMesh = eyeMesh; e.AddComponent<MeshRenderer>().sharedMaterial = eyeMat;
                var lb = bones[side == 0 ? (int)B.LidL : (int)B.LidR];
                lb.localRotation = Quaternion.Euler(-38, 0, 0);    // open: the lid sits up over the top of the eye
                var l = new GameObject("Lid"); l.transform.SetParent(lb, false);
                l.AddComponent<MeshFilter>().sharedMesh = lidMesh; l.AddComponent<MeshRenderer>().sharedMaterial = lidMat;
            }
        }

        /// A sphere (UVs looking from the front, for the iris); `cover` < 1 keeps only the top part (a lid).
        static Mesh Sphere(float r, bool full, float cover)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const int seg = 24, ring = 16;
            for (int i = 0; i <= ring; i++)
            {
                float th = i * Mathf.PI / ring;
                for (int j = 0; j <= seg; j++)
                {
                    float ph = j * Mathf.PI * 2 / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    v.Add(d * r); n.Add(d);
                    uv.Add(new Vector2(0.5f + d.x * 0.5f, 0.5f + d.y * 0.5f));
                }
            }
            for (int i = 0; i < ring; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + seg + 1;
                    // a lid: only the upper part, and only the front half
                    if (!full)
                    {
                        float th = (i + 0.5f) * Mathf.PI / ring, ph = (j + 0.5f) * Mathf.PI * 2 / seg;
                        if (th > Mathf.PI * cover || Mathf.Cos(ph) < -0.2f) continue;
                    }
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            // outward faces
            var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                var nn = Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
                if (Vector3.Dot(nn, v[tris[i]] + v[tris[i + 1]] + v[tris[i + 2]]) < 0) { int x = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = x; }
            }
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        /* ---------------- animations ---------------- */

        class Key { public float t; public Dictionary<B, Vector3> rot = new Dictionary<B, Vector3>(); public Vector3? hips; }

        static string BonePath(B b)
        {
            var parts = new List<string>();
            for (var x = b; (int)x >= 0; x = Parent[(int)x]) parts.Insert(0, x.ToString());
            return string.Join("/", parts);
        }

        static AnimationClip Clip(string name, bool loop, Rig r, params Key[] keys)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            var bonesUsed = new HashSet<B>();
            foreach (var k in keys) foreach (var b in k.rot.Keys) bonesUsed.Add(b);
            foreach (var b in bonesUsed)
            {
                var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                foreach (var k in keys)
                {
                    var e = k.rot.TryGetValue(b, out var v) ? v : Vector3.zero;
                    var q = Quaternion.Euler(e);
                    cx.AddKey(k.t, q.x); cy.AddKey(k.t, q.y); cz.AddKey(k.t, q.z); cw.AddKey(k.t, q.w);
                }
                string path = BonePath(b);
                clip.SetCurve(path, typeof(Transform), "localRotation.x", Smooth(cx));
                clip.SetCurve(path, typeof(Transform), "localRotation.y", Smooth(cy));
                clip.SetCurve(path, typeof(Transform), "localRotation.z", Smooth(cz));
                clip.SetCurve(path, typeof(Transform), "localRotation.w", Smooth(cw));
            }
            if (Array.Exists(keys, k => k.hips.HasValue))
            {
                var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                foreach (var k in keys)
                {
                    var h = k.hips ?? new Vector3(0, r.hipY, 0);
                    px.AddKey(k.t, h.x); py.AddKey(k.t, h.y); pz.AddKey(k.t, h.z);
                }
                clip.SetCurve("Hips", typeof(Transform), "localPosition.x", Smooth(px));
                clip.SetCurve("Hips", typeof(Transform), "localPosition.y", Smooth(py));
                clip.SetCurve("Hips", typeof(Transform), "localPosition.z", Smooth(pz));
            }
            clip.EnsureQuaternionContinuity();
            var st = AnimationUtility.GetAnimationClipSettings(clip);
            st.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, st);
            return clip;
        }

        static AnimationCurve Smooth(AnimationCurve c) { for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0); return c; }

        static Key K(float t, Vector3? hips, params (B b, Vector3 e)[] r)
        {
            var k = new Key { t = t, hips = hips };
            foreach (var x in r) k.rot[x.b] = x.e;
            return k;
        }
        static (B, Vector3) R(B b, float x, float y = 0, float z = 0) => (b, new Vector3(x, y, z));

        static AnimatorController Animations(Spec s, Rig r)
        {
            float hunch = s.hunch * 40f;
            var sitHips = new Vector3(0, 0.4f, -0.02f);
            // the seated base pose: thighs forward, shins down, leaning a little over the table, forearms out
            (B, Vector3)[] Sit(float breathe, float look = 0) => new[]
            {
                R(B.Hips, 4), R(B.Spine, 3 + hunch * 0.5f), R(B.Chest, 5 + hunch * 0.5f + breathe), R(B.Neck, -6 - hunch * 0.6f), R(B.Head, 9, look),
                R(B.UpperLegL, -86, 0, -5), R(B.LowerLegL, 82), R(B.FootL, 4),
                R(B.UpperLegR, -86, 0, 5), R(B.LowerLegR, 82), R(B.FootR, 4),
                R(B.UpperArmL, -32, 0, 6), R(B.LowerArmL, -68), R(B.HandL, -8),
                R(B.UpperArmR, -32, 0, -6), R(B.LowerArmR, -68), R(B.HandR, -8),
            };
            var sitIdle = Clip("SitIdle", true, r, K(0, sitHips, Sit(0)), K(2, sitHips, Sit(1.6f, 3)), K(4, sitHips, Sit(0)));
            var win = Clip("SitWin", false, r,
                K(0, sitHips, Sit(0)),
                K(0.35f, sitHips, Over(Sit(0), R(B.UpperArmL, -150, 0, -20), R(B.UpperArmR, -150, 0, 20), R(B.LowerArmL, -25), R(B.LowerArmR, -25), R(B.Chest, -8), R(B.Head, -12))),
                K(0.7f, sitHips, Over(Sit(0), R(B.UpperArmL, -165, 0, -18), R(B.UpperArmR, -165, 0, 18), R(B.LowerArmL, -10), R(B.LowerArmR, -10), R(B.Chest, -10), R(B.Head, -15))),
                K(1.05f, sitHips, Over(Sit(0), R(B.UpperArmL, -148, 0, -20), R(B.UpperArmR, -148, 0, 20), R(B.LowerArmL, -30), R(B.LowerArmR, -30), R(B.Chest, -8), R(B.Head, -12))),
                K(1.4f, sitHips, Over(Sit(0), R(B.UpperArmL, -165, 0, -18), R(B.UpperArmR, -165, 0, 18), R(B.LowerArmL, -10), R(B.LowerArmR, -10), R(B.Chest, -10), R(B.Head, -15))),
                K(2.2f, sitHips, Sit(0)));
            var lose = Clip("SitLose", false, r,
                K(0, sitHips, Sit(0)),
                K(0.45f, sitHips, Over(Sit(0), R(B.Head, 28), R(B.Chest, 14), R(B.Spine, 8), R(B.UpperArmR, -100, 0, -12), R(B.LowerArmR, -118), R(B.HandR, -20))),
                K(1.7f, sitHips, Over(Sit(0), R(B.Head, 30, -6), R(B.Chest, 15), R(B.Spine, 9), R(B.UpperArmR, -102, 0, -12), R(B.LowerArmR, -120), R(B.HandR, -20))),
                K(2.2f, sitHips, Sit(0)));
            (B, Vector3)[] Stand(float breathe) => new[]
            {
                R(B.Chest, hunch * 0.6f + breathe), R(B.Neck, -hunch * 0.5f), R(B.Head, 2),
                R(B.UpperArmL, 2, 0, 5), R(B.LowerArmL, -10), R(B.UpperArmR, 2, 0, -5), R(B.LowerArmR, -10),
            };
            var stand = Clip("StandIdle", true, r, K(0, null, Stand(0)), K(2, null, Stand(1.4f)), K(4, null, Stand(0)));
            Key Step(float t, float ph)
            {
                float sw = Mathf.Sin(ph * Mathf.PI * 2);
                var h = new Vector3(0, r.hipY - 0.015f + Mathf.Abs(Mathf.Cos(ph * Mathf.PI * 2)) * 0.02f, 0);
                return K(t, h,
                    R(B.Hips, 0, sw * 4), R(B.Chest, 3 + hunch * 0.6f, -sw * 6), R(B.Head, 3, sw * 3),
                    R(B.UpperLegL, -sw * 24), R(B.LowerLegL, Mathf.Max(0, Mathf.Sin(ph * Mathf.PI * 2 + 1.2f)) * 45 + 4),
                    R(B.UpperLegR, sw * 24), R(B.LowerLegR, Mathf.Max(0, -Mathf.Sin(ph * Mathf.PI * 2 + 1.2f)) * 45 + 4),
                    R(B.UpperArmL, sw * 20, 0, 5), R(B.LowerArmL, -15 - Mathf.Max(0, -sw) * 15),
                    R(B.UpperArmR, -sw * 20, 0, -5), R(B.LowerArmR, -15 - Mathf.Max(0, sw) * 15));
            }
            var walkKeys = new Key[9];
            for (int i = 0; i < 9; i++) walkKeys[i] = Step(i / 8f * 1.05f, i / 8f);
            var walk = Clip("Walk", true, r, walkKeys);
            foreach (var c in new[] { sitIdle, win, lose, stand, walk }) AssetDatabase.CreateAsset(c, $"{Out}/{s.name}_{c.name}.anim");

            var path = $"{Out}/{s.name}.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "sit", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("walking", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("win", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("lose", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var sSit = sm.AddState("SitIdle"); sSit.motion = sitIdle;
            var sWin = sm.AddState("SitWin"); sWin.motion = win;
            var sLose = sm.AddState("SitLose"); sLose.motion = lose;
            var sStand = sm.AddState("StandIdle"); sStand.motion = stand;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            sm.defaultState = sSit;
            void Go(AnimatorState a, AnimatorState b, float dur, string param, AnimatorConditionMode mode, bool exit = false)
            {
                var tr = a.AddTransition(b);
                tr.duration = dur; tr.hasExitTime = exit; if (exit) tr.exitTime = 0.95f;
                if (param != null) tr.AddCondition(mode, 0, param);
            }
            Go(sSit, sWin, 0.2f, "win", AnimatorConditionMode.If);
            Go(sSit, sLose, 0.25f, "lose", AnimatorConditionMode.If);
            Go(sWin, sSit, 0.3f, null, AnimatorConditionMode.If, true);
            Go(sLose, sSit, 0.3f, null, AnimatorConditionMode.If, true);
            Go(sSit, sStand, 0.5f, "sit", AnimatorConditionMode.IfNot);
            Go(sStand, sSit, 0.6f, "sit", AnimatorConditionMode.If);
            Go(sStand, sWalk, 0.25f, "walking", AnimatorConditionMode.If);
            Go(sWalk, sStand, 0.3f, "walking", AnimatorConditionMode.IfNot);
            Go(sWalk, sSit, 0.6f, "sit", AnimatorConditionMode.If);
            return ctrl;
        }

        static (B, Vector3)[] Over((B, Vector3)[] a, params (B, Vector3)[] b)
        {
            var d = new Dictionary<B, Vector3>();
            foreach (var x in a) d[x.Item1] = x.Item2;
            foreach (var x in b) d[x.Item1] = x.Item2;
            var o = new List<(B, Vector3)>(); foreach (var kv in d) o.Add((kv.Key, kv.Value));
            return o.ToArray();
        }
    }
}
