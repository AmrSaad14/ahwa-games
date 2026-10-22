// A short guide before your first practice game of each game: a few cards on what to tap and how to win.
// Shown once per game (it can be seen again from "How to play").
using System;
using UnityEngine;
using UnityEngine.UI;

public static class Tutorial
{
    static readonly System.Collections.Generic.Dictionary<string, (string ar, string en)[]> Steps = new System.Collections.Generic.Dictionary<string, (string, string)[]>
    {
        ["domino"] = new[] {
            ("أحجارك تحت. لازم رقم الحجر يطابق رقم في أحد طرفين الخط.", "Your tiles are at the bottom. A tile must match the number at one end of the line."),
            ("دوس على الحجر يتلعب، أو اسحبه للمكان اللي بينوّر.", "Tap a tile to play it, or drag it to the glowing spot."),
            ("مفيش حجر يركب؟ اللعبة بتسحبلك من الكوم لوحدها.", "Nothing fits? The game draws from the pile for you."),
            ("خلّص أحجارك الأول تكسب الجولة، وتاخد نقط الأحجار اللي مع الباقيين.", "Empty your hand first to win the round and score the pips left in the others' hands."),
        },
        ["tawla"] = new[] {
            ("دوس على اللوح عشان ترمي الزهر.", "Tap the board to roll the dice."),
            ("دوس على قشاط وبعدين على مكانه، أو اسحبه. ممكن تمشي بالزهرين مرة واحدة.", "Tap a checker, then where it goes, or drag it. You can use both dice in one move."),
            ("لو غلطت قبل ما دورك يخلص، دوس رجّع.", "Made a mistake before your turn ends? Tap Undo."),
            ("دخّل الـ١٥ قشاط بيتك، وبعدين طلّعهم الأول تكسب.", "Bring all 15 checkers home, then bear them off first to win."),
        },
        ["chess"] = new[] {
            ("دوس على قطعة، وبعدين على مربع من اللي بينوّروا.", "Tap a piece, then one of the glowing squares."),
            ("خلي بالك من الساعة: لو وقتك خلص تخسر.", "Watch your clock: if it runs out, you lose."),
            ("اعمل كش مات للملك عشان تكسب.", "Checkmate the king to win."),
        },
        ["ludo"] = new[] {
            ("دوس على اللوح عشان ترمي الزهر.", "Tap the board to roll the die."),
            ("الـ٦ بتطلّع عسكري وبتديك رمية كمان.", "A 6 brings a pawn out and gives you another roll."),
            ("دوس على العسكري اللي بينوّر يتحرك. لو وقفت على عسكري حد يرجع بيته.", "Tap a glowing pawn to move it. Land on someone else's pawn to send it home."),
            ("دخّل الـ٤ عساكر البيت الأول تكسب.", "Get all 4 pawns home first to win."),
        },
    };

    public static bool Seen(string game) => PlayerPrefs.GetInt("tut." + game, 0) == 1;

    /// The guide for a game; `done` runs when it is closed (Start or Skip).
    public static void Show(string game, string title, Action done)
    {
        if (!Steps.TryGetValue(game, out var steps)) { done(); return; }
        var canvas = Ui.MakeCanvas("Tutorial", 60);
        var root = Ui.Rect("Tutorial", canvas.transform).Fill();
        var back = Ui.Box(root, "Back", new Color(0, 0, 0, 0.7f), false); back.rectTransform.Fill(); back.raycastTarget = true;
        var card = Ui.Box(root, "Card", new Color32(0x22, 0x15, 0x0e, 0xf8)).rectTransform;
        card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(520, 250), Vector2.zero);
        card.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.5f);
        Ui.Label(card, Loc.T("إزاي تلعب") + " " + title, 20, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform
            .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(480, 32), new Vector2(0, -10));
        var text = Ui.Label(card, "", 17, Color.white, TextAnchor.MiddleCenter, Ui.Bold);
        text.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(470, 90), new Vector2(0, 10));
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var dots = Ui.Label(card, "", 16, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        dots.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(200, 20), new Vector2(0, 62));
        int i = 0;
        Button next = null;
        void Close() { PlayerPrefs.SetInt("tut." + game, 1); PlayerPrefs.Save(); UnityEngine.Object.Destroy(canvas.gameObject); done(); }
        void Draw()
        {
            text.Set(Loc.En ? steps[i].en : steps[i].ar);
            dots.text = (i + 1) + " / " + steps.Length;
            next.GetComponentInChildren<Text>().Set(Loc.T(i == steps.Length - 1 ? "يلا نلعب" : "التالي"));
        }
        next = Ui.Button(card, "", Ui.Gold, Ui.Ink, 17, () => { Sfx.Tap(); if (i < steps.Length - 1) { i++; Draw(); } else Close(); });
        ((RectTransform)next.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(180, 42), new Vector2(Loc.En ? 70 : -70, 12));
        var skip = Ui.Button(card, "تخطي", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 15, () => { Sfx.Tap(); Close(); });
        ((RectTransform)skip.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(120, 42), new Vector2(Loc.En ? -100 : 100, 12));
        Draw();
    }
}
