// Quick chat at the table: a few friendly phrases and emojis (the server's emotes), shown as a bubble by the
// player who sent it, with their character reacting. In practice the computer players answer now and then.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class QuickChat : MonoBehaviour
{
    App app; RectTransform hud, panel;
    /// The phrases (Arabic and English) for the emotes that are words; the rest are just an emoji.
    public static readonly Dictionary<string, (string ar, string en)> Phrases = new Dictionary<string, (string, string)>
    {
        ["hi"] = ("أهلاً!", "Hello!"), ["nice"] = ("حلوة دي!", "Nice move!"), ["hurry"] = ("يلا بقى", "Hurry up"),
        ["gg"] = ("لعبة حلوة", "Good game"), ["oops"] = ("يا خسارة", "Oops"), ["think"] = ("استنى أفكر", "Let me think"),
    };
    static readonly string[] Words = { "hi", "nice", "hurry", "gg", "oops", "think" };
    static readonly string[] Faces = { "lol", "wow", "clap", "fire", "angry", "love" };
    // how the computer answers in practice
    static readonly Dictionary<string, string[]> Replies = new Dictionary<string, string[]>
    {
        ["hi"] = new[] { "hi", "love" }, ["nice"] = new[] { "love", "clap" }, ["hurry"] = new[] { "think", "lol" }, ["gg"] = new[] { "gg", "clap" },
        ["oops"] = new[] { "lol", "oops" }, ["think"] = new[] { "hurry" }, ["lol"] = new[] { "lol" }, ["wow"] = new[] { "fire" },
        ["clap"] = new[] { "love" }, ["fire"] = new[] { "wow" }, ["angry"] = new[] { "lol", "oops" }, ["love"] = new[] { "love" },
    };
    float lastSent;

    public void Build(App a, RectTransform h)
    {
        app = a; hud = h;
        var b = Ui.Button(hud, "", new Color(0.1f, 0.06f, 0.04f, 0.85f), Color.white, 10, Toggle);
        ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(46, 46), new Vector2(-12, -96));
        var ic = Ui.Picture(b.transform, "Chat", Menus.Icon("chat")); ic.rectTransform.Fill(10); ic.color = Ui.GoldLight;

        panel = Ui.Box(hud, "QuickChat", new Color(0.1f, 0.06f, 0.04f, 0.96f)).rectTransform;
        panel.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 196), new Vector2(-64, -96));
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f);
        panel.GetComponent<Image>().raycastTarget = true;
        for (int i = 0; i < Words.Length; i++)
        {
            var k = Words[i];
            var w = Ui.Button(panel, Loc.En ? Phrases[k].en : Phrases[k].ar, new Color(1, 1, 1, 0.08f), Ui.GoldLight, 13, () => Send(k));
            ((RectTransform)w.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 32), new Vector2(8 + (i % 2) * 146, -8 - (i / 2) * 38));
            w.name = "Word " + k;
        }
        for (int i = 0; i < Faces.Length; i++)
        {
            var k = Faces[i];
            var f = Ui.Button(panel, "", new Color(1, 1, 1, 0.06f), Color.white, 10, () => Send(k));
            ((RectTransform)f.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(44, 44), new Vector2(8 + i * 48, 8));
            var e = Ui.Picture(f.transform, "E", Emoji(k)); e.rectTransform.Fill(5);
        }
        panel.gameObject.SetActive(false);
        Loc.Changed += Relabel;
    }

    void Relabel()
    {
        if (panel == null) return;
        foreach (Transform t in panel)
            if (t.name.StartsWith("Word ")) { var k = t.name.Substring(5); t.GetComponentInChildren<Text>().Set(Loc.En ? Phrases[k].en : Phrases[k].ar); }
    }

    public static Texture2D Emoji(string k) => Resources.Load<Texture2D>("UI/Emoji/" + k);

    void Toggle() { Sfx.Tap(); panel.gameObject.SetActive(!panel.gameObject.activeSelf); }
    public void Hide() { if (panel != null) panel.gameObject.SetActive(false); }

    void Send(string k)
    {
        panel.gameObject.SetActive(false);
        if (Time.time - lastSent < 1.6f) return;   // not a flood
        lastSent = Time.time;
        Sfx.Tap();
        app.SendEmote(k);
    }

    /// In practice, a computer player answers sometimes.
    public IEnumerator BotReply(TableView v, string k)
    {
        if (v == null || Random.value > 0.65f) yield break;
        var bots = new List<int>();
        for (int s = 0; s < v.seats.Count; s++) if (s != v.you && v.seats[s] != null && v.seats[s].bot) bots.Add(s);
        if (bots.Count == 0 || !Replies.TryGetValue(k, out var opts)) yield break;
        yield return new WaitForSeconds(Random.Range(0.9f, 2f));
        app.ShowEmote(bots[Random.Range(0, bots.Count)], opts[Random.Range(0, opts.Length)]);
    }
}
