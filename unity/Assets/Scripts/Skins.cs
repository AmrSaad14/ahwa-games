// The skins in the app: every look on sale (the same ids and prices as the server, shared/skins.ts), what you
// wear of each kind, and how each one looks: the boards (domino table cloth, tawla, chess), the pieces (domino
// tiles, tawla checkers, chess pieces), the dice, your hat and your drink. Plus the small pictures for the
// shop, the collection and the season track.
using System.Collections.Generic;
using UnityEngine;

public static class Skins
{
    /// ev: a limited skin, sold only while an event of that kind (ramadan, eid) is on.
    public class Def { public string id, cat, name, ev; public long price; public bool pass; }
    static Def D(string id, string cat, string name, long price, bool pass = false, string ev = null) => new Def { id = id, cat = cat, name = name, price = price, pass = pass, ev = ev };

    /// Everything, in shop order (prices in coins; pass: from the season track only).
    public static readonly Def[] All =
    {
        // boards
        D("felt_wood", "felt", "خشب", 0), D("felt_green", "felt", "جوخ أخضر", 150_000), D("felt_red", "felt", "جوخ أحمر", 150_000),
        D("felt_blue", "felt", "جوخ أزرق", 200_000), D("felt_marble", "felt", "رخام", 350_000), D("felt_royal", "felt", "ملكي", 0, true), D("felt_kilim", "felt", "كليم", 0, true),
        D("tawla_walnut", "tawla", "جوز وصدف", 0), D("tawla_olive", "tawla", "خشب زيتون", 200_000), D("tawla_ebony", "tawla", "أبنوس", 300_000),
        D("tawla_pearl", "tawla", "صدف لولي", 400_000), D("tawla_royal", "tawla", "ملكي زمردي", 800_000), D("tawla_pharaoh", "tawla", "فرعوني", 0, true),
        D("chess_wood", "chess", "خشب ورد", 0), D("chess_blue", "chess", "أزرق نيلي", 200_000), D("chess_tournament", "chess", "أخضر بطولات", 250_000),
        D("chess_rosewood", "chess", "ورد أحمر", 350_000), D("chess_marble", "chess", "رخام", 500_000), D("chess_gold", "chess", "أسود ودهب", 0, true),
        // pieces
        D("tiles_ivory", "tiles", "عاجي", 0), D("tiles_ebony", "tiles", "أبنوس", 150_000), D("tiles_marble", "tiles", "رخام", 300_000),
        D("tiles_jade", "tiles", "يشم", 450_000), D("tiles_gold", "tiles", "دهب", 900_000),
        D("checkers_classic", "checkers", "عاج وأبنوس", 0), D("checkers_marble", "checkers", "رخام", 150_000), D("checkers_amber", "checkers", "كهرمان", 250_000),
        D("checkers_ruby", "checkers", "ياقوت ولولي", 300_000), D("checkers_gold", "checkers", "دهب وفضة", 600_000), D("checkers_pharaoh", "checkers", "فرعوني", 0, true),
        D("pieces_wood", "pieces", "خشب", 0), D("pieces_porcelain", "pieces", "صيني وأزرق", 250_000), D("pieces_marble", "pieces", "رخام", 300_000),
        D("pieces_ruby", "pieces", "عاج وياقوت", 400_000), D("pieces_gold", "pieces", "دهب وفضة", 800_000), D("pieces_onyx", "pieces", "يشم وأونيكس", 0, true),
        // dice
        D("dice_ivory", "dice", "عاجي", 0), D("dice_ruby", "dice", "ياقوت", 100_000), D("dice_obsidian", "dice", "سبج", 200_000),
        D("dice_emerald", "dice", "زمرد", 250_000), D("dice_turquoise", "dice", "فيروز", 300_000), D("dice_gold", "dice", "دهب", 600_000), D("dice_pharaoh", "dice", "فرعوني", 0, true),
        // your look
        D("hat_classic", "hat", "الأصلي", 0), D("hat_fez_red", "hat", "طربوش أحمر", 120_000), D("hat_fez_black", "hat", "طربوش أسود", 180_000),
        D("hat_fez_green", "hat", "طربوش أخضر", 220_000), D("hat_fez_navy", "hat", "طربوش كحلي", 260_000), D("hat_fez_white", "hat", "طربوش أبيض", 0, true), D("hat_fez_gold", "hat", "طربوش دهبي", 0, true),
        D("drink_tea", "drink", "شاي", 0), D("drink_mint", "drink", "شاي بالنعناع", 80_000), D("drink_coffee", "drink", "قهوة", 80_000),
        D("drink_karkade", "drink", "كركديه", 120_000), D("drink_sahlab", "drink", "سحلب", 0, true), D("drink_qasab", "drink", "قصب", 0, true),
        // how you celebrate a win (everyone at the table sees it)
        D("win_coins", "win", "مطر كوينز", 0), D("win_confetti", "win", "ورق ملوّن", 150_000), D("win_stars", "win", "نجوم دهب", 250_000), D("win_fireworks", "win", "صواريخ", 450_000),
        // Ramadan and Eid: on sale only while the event is on
        D("felt_ramadan", "felt", "ليالي رمضان", 200_000, false, "ramadan"), D("dice_ramadan", "dice", "زهر رمضان", 120_000, false, "ramadan"),
        D("tawla_ramadan", "tawla", "طاولة رمضان", 300_000, false, "ramadan"), D("hat_fez_ramadan", "hat", "طربوش بنفسجي", 150_000, false, "ramadan"),
        D("drink_qamar", "drink", "قمر الدين", 90_000, false, "ramadan"), D("win_lanterns", "win", "فوانيس", 250_000, false, "ramadan"),
        D("felt_eid", "felt", "فرحة العيد", 200_000, false, "eid"), D("dice_eid", "dice", "زهر العيد", 120_000, false, "eid"),
        D("hat_fez_eid", "hat", "طربوش العيد", 150_000, false, "eid"), D("drink_sobia", "drink", "سوبيا", 90_000, false, "eid"), D("win_balloons", "win", "بلالين", 250_000, false, "eid"),
    };
    /// The event on now ("ramadan", "eid" or null), from the server: its limited skins are on sale.
    public static string EventKind;
    /// In the shop: everything, but a limited skin only during its event (or if you have it).
    public static bool OnSale(Def d) => d.ev == null || d.ev == EventKind || owned.Contains(d.id);
    public static Def Get(string id) { foreach (var d in All) if (d.id == id) return d; return null; }
    public static bool IsFree(Def d) => d.price == 0 && !d.pass;
    public static IEnumerable<Def> Of(string cat) { foreach (var d in All) if (d.cat == cat) yield return d; }
    public static string Default(string cat) { foreach (var d in Of(cat)) if (IsFree(d)) return d.id; return null; }
    public static string Name(string id) { var d = Get(id); return d != null ? Loc.T(d.name) : id; }

    // ---------- what you own and wear (from the server's user message) ----------
    static readonly HashSet<string> owned = new HashSet<string>();
    static readonly Dictionary<string, string> equip = new Dictionary<string, string>();
    public static void SetUser(Dictionary<string, object> u)
    {
        owned.Clear(); equip.Clear();
        var l = u?.Arr("skins"); if (l != null) foreach (var o in l) if (o is string s) owned.Add(s);
        var e = u?.Obj("equip"); if (e != null) foreach (var kv in e) if (kv.Value is string s) equip[kv.Key] = s;
    }
    public static bool Owns(string id) { var d = Get(id); return d != null && (IsFree(d) || owned.Contains(id)); }
    /// Looks forced on (the test run's screenshots show the new skins).
    public static readonly Dictionary<string, string> Force = new Dictionary<string, string>();
    /// What you wear of a kind: the equipped one if you own it, else the classic.
    public static string Worn(string cat) => Force.TryGetValue(cat, out var f) ? f : equip.TryGetValue(cat, out var id) && Owns(id) && Get(id)?.cat == cat ? id : Default(cat);

    // ---------- looks ----------
    static Color H(string hex) => Art.Hex(hex);

    public static (Color face, Color pip) Dice(string id)
    {
        switch (id)
        {
            case "dice_ruby": return (H("#a8121e"), Color.white);
            case "dice_obsidian": return (H("#141418"), H("#e7bd5c"));
            case "dice_emerald": return (H("#1f8a50"), Color.white);
            case "dice_turquoise": return (H("#2aa194"), Color.white);
            case "dice_gold": return (H("#e2b04a"), H("#3a2208"));
            case "dice_pharaoh": return (H("#2a52a0"), H("#f3cd62"));
            case "dice_ramadan": return (H("#241a5c"), H("#f3cd62"));
            case "dice_eid": return (H("#fff7ec"), H("#e0306b"));
            default: return (H("#f6efe0"), H("#1a0f0a"));
        }
    }

    /// A tawla board: the box wood, the field wood, the two colours of points (each base, dark grain, light grain), the tray, the rail tops.
    public class TawlaLook { public Color[] box, field, ptA, ptB; public Color tray, pearl, line; }
    static Color[] W(string a, string b, string c) => new[] { H(a), H(b), H(c) };
    public static TawlaLook Tawla(string id)
    {
        switch (id)
        {
            case "tawla_olive": return new TawlaLook { box = W("#4a3a1a", "#2a200c", "#6a5428"), field = W("#9aa36a", "#7a8450", "#b3bb82"), ptA = W("#4a2a14", "#2a160a", "#6a4022"), ptB = W("#e8dcc0", "#c8b894", "#f6eedc"), tray = H("#2a3a1a"), pearl = H("#e8dcc0"), line = H("#e8dcc0") };
            case "tawla_ebony": return new TawlaLook { box = W("#100c0a", "#050302", "#241c18"), field = W("#2a2420", "#16110e", "#3e3530"), ptA = W("#efe6d2", "#cfc2a6", "#fff8ea"), ptB = W("#a8322a", "#701c16", "#c8483c"), tray = H("#3a0a0e"), pearl = H("#c9a25a"), line = H("#c9a25a") };
            case "tawla_pearl": return new TawlaLook { box = W("#2a1d14", "#120b06", "#463020"), field = W("#f2eee6", "#d8d2c6", "#ffffff"), ptA = W("#1d1d24", "#0a0a0e", "#3a3a44"), ptB = W("#1f8f86", "#0f5a54", "#3ab0a6"), tray = H("#13403c"), pearl = H("#efe8dc"), line = H("#c9a25a") };
            case "tawla_royal": return new TawlaLook { box = W("#120c0a", "#050302", "#2a1c16"), field = W("#145a45", "#0b3a2c", "#1f7a5e"), ptA = W("#e3b75a", "#a7792a", "#f6d58c"), ptB = W("#18110f", "#060303", "#30241f"), tray = H("#3a070c"), pearl = H("#f3cd62"), line = H("#f3cd62") };
            case "tawla_ramadan": return new TawlaLook { box = W("#241a5c", "#120c34", "#3a2c80"), field = W("#efe3c4", "#d8c8a0", "#fbf3dc"), ptA = W("#3a2c80", "#1e1650", "#5a48a8"), ptB = W("#c99a30", "#8a6418", "#f0c862"), tray = H("#1e1650"), pearl = H("#f3cd62"), line = H("#f3cd62") };
            case "tawla_pharaoh": return new TawlaLook { box = W("#1f3f7a", "#10234a", "#2f5aa0"), field = W("#e2c98a", "#c4a462", "#f2dca6"), ptA = W("#1f4e9a", "#12306a", "#3a6ac0"), ptB = W("#2f8a7a", "#1a5a4e", "#46aa98"), tray = H("#12306a"), pearl = H("#f3cd62"), line = H("#f3cd62") };
            default: return new TawlaLook { box = W("#6b3a1e", "#3d1f0e", "#8c5530"), field = W("#c9a46c", "#a7834f", "#dcbd88"), ptA = W("#3b2414", "#24140a", "#5a3a22"), ptB = W("#8e2a1c", "#5e180e", "#b0402c"), tray = H("#1e3b26"), pearl = H("#bdb3a2"), line = H("#e7d3a8") };
        }
    }

    /// A chess board: light and dark squares, the frame and the stringing line; wood grain, flat or marble.
    public class ChessLook { public Color[] light, dark, frame; public Color line; public string kind; public bool metalFrame; }
    public static ChessLook Chess(string id)
    {
        switch (id)
        {
            case "chess_blue": return new ChessLook { kind = "flat", light = W("#e8edf4", "#d6dde8", "#f6f8fb"), dark = W("#4a6d9a", "#3a5a84", "#5a80b0"), frame = W("#22324a", "#121c2a", "#34486a"), line = H("#c9d4e4") };
            case "chess_tournament": return new ChessLook { kind = "flat", light = W("#eeeed2", "#e2e2c4", "#f6f6e2"), dark = W("#769656", "#63804a", "#86a664"), frame = W("#2e3a24", "#1a2214", "#45553a"), line = H("#eeeed2") };
            case "chess_rosewood": return new ChessLook { kind = "wood", light = W("#f0d8b0", "#d8b888", "#fae8c8"), dark = W("#7a2a24", "#4a1410", "#9a3c32"), frame = W("#3a1410", "#1e0806", "#5a2420"), line = H("#f0d8b0") };
            case "chess_marble": return new ChessLook { kind = "marble", light = W("#f4f2ee", "#b8b4ac", "#ffffff"), dark = W("#3e434a", "#1a1d22", "#7a8088"), frame = W("#1c1c20", "#0a0a0c", "#34343a"), line = H("#c9c4bb") };
            case "chess_gold": return new ChessLook { kind = "flat", light = W("#f3d98a", "#d8b660", "#ffe9b0"), dark = W("#1a1614", "#0a0807", "#2e2826"), frame = W("#c9962f", "#8a6420", "#f0c862"), line = H("#f3cd62"), metalFrame = true };
            // the classic board: light maple and walnut, far apart in brightness so every square reads at a glance
            default: return new ChessLook { kind = "wood", light = W("#f0dab4", "#e4c99c", "#f8e8cc"), dark = W("#8f5d36", "#7a4c2a", "#a26c42"), frame = W("#4a2414", "#2a1208", "#64341e"), line = H("#f0d8a8") };
        }
    }

    /// Pieces of a side: colours (base, dark, light), how glossy and how metallic, and the surface (wood grain, marble, plain).
    public class Finish { public Color[] c; public float smooth, metal; public string kind; }
    static Finish F(string a, string b, string c, float smooth, float metal, string kind) => new Finish { c = W(a, b, c), smooth = smooth, metal = metal, kind = kind };
    public static (Finish light, Finish dark) Checkers(string id)
    {
        switch (id)
        {
            case "checkers_marble": return (F("#f4f2ee", "#b8b4ac", "#ffffff", 0.8f, 0, "marble"), F("#2a2d32", "#0e1012", "#5a6068", 0.82f, 0, "marble"));
            case "checkers_amber": return (F("#e0902a", "#a85a10", "#f6c060", 0.85f, 0, "plain"), F("#1a1a1e", "#08080a", "#3a3a42", 0.9f, 0, "plain"));
            case "checkers_ruby": return (F("#f2ece2", "#d6ccbc", "#ffffff", 0.88f, 0.05f, "plain"), F("#a8121e", "#5a0810", "#d83a44", 0.85f, 0, "plain"));
            case "checkers_gold": return (F("#d8dade", "#a0a4aa", "#f4f6f8", 0.75f, 0.9f, "plain"), F("#d8a840", "#9a7020", "#f6d47a", 0.75f, 0.9f, "plain"));
            case "checkers_pharaoh": return (F("#f3cd62", "#b88a2a", "#ffe9a0", 0.7f, 0.85f, "plain"), F("#1f4e9a", "#0f2a5a", "#3a6ac0", 0.8f, 0.1f, "plain"));
            default: return (F("#efe3c8", "#d6c6a2", "#fbf2dc", 0.72f, 0, "plain"), F("#3a160e", "#1e0a06", "#5a2a1a", 0.8f, 0, "plain"));
        }
    }
    public static (Finish white, Finish black) Pieces(string id)
    {
        switch (id)
        {
            case "pieces_porcelain": return (F("#f6f6f4", "#dcdcd8", "#ffffff", 0.9f, 0, "plain"), F("#1f3f8a", "#102456", "#3a5ab8", 0.9f, 0, "plain"));
            case "pieces_marble": return (F("#f4f2ee", "#b8b4ac", "#ffffff", 0.82f, 0, "marble"), F("#2a2d32", "#0e1012", "#5a6068", 0.85f, 0, "marble"));
            case "pieces_ruby": return (F("#f2e8d4", "#d8c8a8", "#fff6e4", 0.8f, 0, "plain"), F("#9a1018", "#50060c", "#c8323c", 0.88f, 0, "plain"));
            case "pieces_gold": return (F("#d8a840", "#9a7020", "#f6d47a", 0.78f, 0.95f, "plain"), F("#c8ccd2", "#8a8e96", "#eef0f4", 0.78f, 0.95f, "plain"));
            case "pieces_onyx": return (F("#3aa070", "#1a6a46", "#5ac090", 0.88f, 0.05f, "marble"), F("#141416", "#050506", "#34343a", 0.92f, 0.05f, "marble"));
            default: return (F("#e3c592", "#b98d55", "#f3dfb6", 0.68f, 0, "wood"), F("#1d130e", "#0a0604", "#3a281d", 0.78f, 0, "wood"));
        }
    }

    // ---------- textures and materials made from a look ----------
    /// Marble: a soft base with dark wandering veins.
    public static Color Marble(float x, float y, Color[] c, float seed)
    {
        float n = Mathf.PerlinNoise(x * 0.012f + seed, y * 0.012f + seed * 2) * 6f + Mathf.PerlinNoise(x * 0.05f + seed, y * 0.05f) * 1.2f;
        float vein = Mathf.Pow(1 - Mathf.Abs(Mathf.Sin((x * 0.01f + y * 0.006f + n) * 3.1f)), 14f);
        var b = Color.Lerp(c[0], c[2], Mathf.PerlinNoise(x * 0.02f + seed * 3, y * 0.02f) * 0.6f);
        return Color.Lerp(b, c[1], vein * 0.8f);
    }
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    /// A material for a finish (cached by its key).
    public static Material Mat(string key, Finish f, int seed = 1)
    {
        if (mats.TryGetValue(key, out var m) && m != null) return m;
        Texture tex = null;
        if (f.kind == "wood") tex = Art.WoodTex(128, 256, f.c[0], f.c[1], f.c[2], 30, 1.1f * seed);
        else if (f.kind == "marble") tex = Art.Paint(256, 256, (x, y) => Marble(x, y, f.c, seed * 1.7f));
        m = Art.Mat(tex != null ? Color.white : f.c[0], f.smooth, f.metal, tex);
        mats[key] = m;
        return m;
    }

    // ---------- pictures for the shop, the collection and the season ----------
    static readonly Dictionary<string, Texture> pics = new Dictionary<string, Texture>();
    /// A small picture of a skin (and a tint for the icon-based ones: hats and drinks).
    public static Texture Picture(string id, out Color tint)
    {
        tint = Color.white;
        var d = Get(id); if (d == null) return Menus.Icon("coin");
        if (d.cat == "hat") { tint = id == "hat_classic" ? new Color(1, 1, 1, 0.5f) : Look.HatColor(id); return Menus.Icon("fez"); }
        if (d.cat == "drink") { tint = Look.DrinkColor(id); tint.a = 1; return Menus.Icon("glass"); }
        if (d.cat == "win")
        {
            switch (id)
            {
                case "win_confetti": tint = new Color(0.95f, 0.45f, 0.6f); return Menus.Icon("t_star");
                case "win_stars": tint = Ui.Gold; return Menus.Icon("t_star");
                case "win_fireworks": tint = new Color(1f, 0.55f, 0.25f); return Menus.Icon("t_flame");
                case "win_lanterns": tint = Ui.Gold; return Menus.Icon("t_lantern");
                case "win_balloons": tint = new Color(0.95f, 0.35f, 0.5f); return GameUi.Disc.texture;
                default: return Menus.Icon("coin");
            }
        }
        if (d.cat == "felt") return id == "felt_wood" ? Art.WoodTex(96, 96, H("#8a5a32"), H("#5a3418"), H("#a8743e")) : Look.FeltTexture(id);
        if (d.cat == "tiles") return TileArt.Preview(id);
        if (pics.TryGetValue(id, out var t) && t != null) return t;
        Texture2D p = null;
        const int S = 128;
        switch (d.cat)
        {
            case "dice":
            {
                var (face, pip) = Dice(id);
                p = DiePicture(face, pip);
                break;
            }
            case "tawla":
            {
                var L = Tawla(id);
                p = Art.Paint(S, S, (x, y) =>
                {
                    int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(S - 1 - x, S - 1 - y));
                    if (e < 8 || Mathf.Abs(x - S / 2) < 5) return Art.Wood(x * 2, y * 2, L.box[0], L.box[1], L.box[2], 50, 1);
                    float u = (x - 8) % ((S - 16) / 2f) / ((S - 16) / 2f) * 6f;   // six points per half
                    int c = Mathf.FloorToInt(u); float cx = c + 0.5f, w = 0.46f;
                    float d0 = y < S / 2 ? (y - 8) / (S * 0.42f) : (S - 9 - y) / (S * 0.42f);
                    bool inPoint = d0 >= 0 && d0 < 1 && Mathf.Abs(u - cx) < w * (1 - d0);
                    if (inPoint) { bool red = (c + (y < S / 2 ? 0 : 1)) % 2 == 1; var pc = red ? L.ptB : L.ptA; return Art.Wood(x * 3, y * 3, pc[0], pc[1], pc[2], 60, 2); }
                    return Art.Wood(x * 2, y * 2, L.field[0], L.field[1], L.field[2], 60, 0.5f);
                }, false);
                break;
            }
            case "chess":
            {
                var L = Chess(id);
                p = Art.Paint(S, S, (x, y) =>
                {
                    int e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(S - 1 - x, S - 1 - y));
                    if (e < 10) return L.metalFrame ? Color.Lerp(L.frame[0], L.frame[2], (x + y) % 23 / 23f * 0.4f) : Art.Wood(x * 2, y * 2, L.frame[0], L.frame[1], L.frame[2], 50, 2);
                    if (e < 12) return L.line;
                    int f = (x - 12) * 4 / (S - 24), r = (y - 12) * 4 / (S - 24);
                    var c = (f + r) % 2 == 1 ? L.light : L.dark;
                    return L.kind == "wood" ? Art.Wood(x * 3, y * 3, c[0], c[1], c[2], 50, f + r) : L.kind == "marble" ? Marble(x * 3, y * 3, c, f * 0.7f + r) : c[0];
                }, false);
                break;
            }
            case "checkers":
            {
                var (a, b) = Checkers(id);
                p = Art.Paint(S, S, (x, y) =>
                {
                    var c = Disc(x, y, 44, 66, 38, a); if (c.a > 0) return c;
                    c = Disc(x, y, 84, 56, 38, b); return c;
                }, false);
                break;
            }
            case "pieces":
            {
                var (w, b) = Pieces(id);
                p = Art.Paint(S, S, (x, y) =>
                {
                    var c = Pawn(x, y, 42, w); if (c.a > 0) return c;
                    return Pawn(x, y, 86, b);
                }, false);
                break;
            }
        }
        if (p == null) return Menus.Icon("coin");
        pics[id] = p;
        return p;
    }

    /// A checker seen from above at (cx, cy), radius r: a rim, a ring, a little shine.
    static Color Disc(int x, int y, float cx, float cy, float r, Finish f)
    {
        float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
        if (d > r) return Color.clear;
        var c = f.kind == "marble" ? Marble(x * 3, y * 3, f.c, cx) : Color.Lerp(f.c[0], f.c[2], Mathf.Clamp01((y - cy) / r * 0.5f + 0.3f));
        if (d > r - 4) c = Color.Lerp(c, f.c[1], 0.7f);
        if (Mathf.Abs(d - r * 0.62f) < 1.5f) c = Color.Lerp(c, f.c[1], 0.6f);
        if (f.metal > 0.5f) c = Color.Lerp(c, Color.white, Mathf.Pow(Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x, y), new Vector2(cx - r * 0.3f, cy + r * 0.35f)) / (r * 0.5f)), 2) * 0.6f);
        c.a = Mathf.Clamp01(r - d);
        return c;
    }
    /// A pawn silhouette standing at x = cx: a round head, a collar and a wide foot.
    static Color Pawn(int x, int y, float cx, Finish f)
    {
        float dx = Mathf.Abs(x - cx);
        bool head = Vector2.Distance(new Vector2(x, y), new Vector2(cx, 92)) < 16;
        bool collar = y > 70 && y < 78 && dx < 18;
        bool body = y >= 30 && y <= 72 && dx < 8 + (72 - y) * 0.42f;
        bool foot = y >= 18 && y < 30 && dx < 30;
        if (!(head || collar || body || foot)) return Color.clear;
        var c = f.kind == "marble" ? Marble(x * 3, y * 3, f.c, cx) : f.kind == "wood" ? Art.Wood(x * 4, y * 2, f.c[0], f.c[1], f.c[2], 30, cx) : Color.Lerp(f.c[1], f.c[2], Mathf.Clamp01(0.5f + (cx - x) / 30f));
        if (f.metal > 0.5f) c = Color.Lerp(c, Color.white, Mathf.Clamp01(1 - Mathf.Abs(x - (cx - 6)) / 5f) * 0.5f);
        c.a = 1;
        return c;
    }

    static Texture2D DiePicture(Color face, Color pip)
    {
        const int S = 96; const float R = 18;
        Vector2[] pips = { new Vector2(28, 28), new Vector2(68, 28), new Vector2(48, 48), new Vector2(28, 68), new Vector2(68, 68) };
        return Art.Paint(S, S, (x, y) =>
        {
            float dx = Mathf.Max(0, Mathf.Max(R - x, x - (S - 1 - R))), dy = Mathf.Max(0, Mathf.Max(R - y, y - (S - 1 - R)));
            float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
            var c = face * (0.9f + 0.1f * (1 - y / (float)S));
            foreach (var p in pips) { float d = Vector2.Distance(new Vector2(x, y), p); c = Color.Lerp(c, pip, Mathf.Clamp01(8.5f - d)); }
            c.a = a; return c;
        }, false);
    }
}
