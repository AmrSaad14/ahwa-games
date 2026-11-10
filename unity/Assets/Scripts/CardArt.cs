// The playing cards' pictures (Resources/UI/Cards: c0 … c51, suit * 13 + rank - 1, suits ♠ ♥ ♦ ♣, and the back)
// and the café tablecloth the card games are dealt on.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class CardArt
{
    static readonly Dictionary<int, Texture2D> faces = new Dictionary<int, Texture2D>();
    static Texture2D back;
    public static readonly string[] SuitSym = { "♠", "♥", "♦", "♣" };
    /// The Egyptian names of the suits.
    public static readonly string[] SuitName = { "بستوني", "كبة", "ديناري", "سباتي" };
    public static Color SuitColor(int s) => s == 1 || s == 2 ? new Color(0.78f, 0.15f, 0.18f) : new Color(0.1f, 0.1f, 0.14f);

    /// A card's face (or the back, for -1).
    public static Texture2D Face(int id)
    {
        if (id < 0) return back ??= Resources.Load<Texture2D>("UI/Cards/back");
        if (!faces.TryGetValue(id, out var t)) faces[id] = t = Resources.Load<Texture2D>("UI/Cards/c" + id);
        return t;
    }

    /// The café tablecloth the cards are dealt on (green baize, a gold-embroidered border), under everything; it
    /// stretches with the screen (wider phones, tablets) and keeps clear of the top bar.
    public static void Cloth(RectTransform root)
    {
        var tex = Resources.Load<Texture2D>("UI/Cards/cloth");
        if (tex == null) return;
        var c = Ui.Picture(root, "Cloth", tex).rectTransform;
        c.anchorMin = new Vector2(0.03f, 0.02f); c.anchorMax = new Vector2(0.97f, 0.88f);
        c.offsetMin = c.offsetMax = Vector2.zero;
        c.GetComponent<RawImage>().raycastTarget = false;
        c.SetAsFirstSibling();
    }
}
