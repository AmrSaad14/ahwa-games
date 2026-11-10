using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Ordering a drink during a game: a glass button on the right (under the sound button) opens the menu; a drink
// is paid with coins on the server ("order"), and then the waiter brings it to your place, seen by everyone at
// the table ("served", see DominoTable.Serve). The prices are the server's (shared/gifts.ts DRINK_MENU).
public class DrinkMenu : MonoBehaviour
{
    public static readonly (string id, int price)[] Menu =
    {
        ("drink_tea", 1_000), ("drink_mint", 1_500), ("drink_coffee", 2_000),
        ("drink_karkade", 2_500), ("drink_qasab", 2_500), ("drink_sahlab", 3_000),
    };
    App app;
    GameObject button;
    RectTransform panel;
    readonly List<Text> labels = new List<Text>();
    float lastSent = -99;

    public void Build(App a, RectTransform hud)
    {
        app = a;
        var b = Ui.Button(hud, "", new Color(0.1f, 0.06f, 0.04f, 0.85f), Color.white, 10, Toggle);
        ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(46, 46), new Vector2(-12, -200));
        var ic = Ui.Picture(b.transform, "Glass", Menus.Icon("glass")); ic.rectTransform.Fill(9); ic.color = Ui.GoldLight;
        button = b.gameObject;

        panel = Ui.Box(hud, "Drinks", new Color(0.1f, 0.06f, 0.04f, 0.96f)).rectTransform;
        panel.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(300, 196), new Vector2(-64, -150));
        panel.gameObject.AddComponent<Outline>().effectColor = new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.45f);
        panel.GetComponent<Image>().raycastTarget = true;
        var title = Ui.Label(panel, "اطلب مشروب", 14, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold);
        title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(280, 24), new Vector2(0, -4));
        labels.Add(title);
        for (int i = 0; i < Menu.Length; i++)
        {
            var (id, price) = Menu[i];
            var w = Ui.Button(panel, "", new Color(1, 1, 1, 0.08f), Ui.GoldLight, 13, () => Order(id, price));
            ((RectTransform)w.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 48), new Vector2(8 + (i % 2) * 146, -32 - (i / 2) * 54));
            w.name = "Drink " + id;
            // a dot in the drink's colour, its name, and its price
            var dot = Ui.Box(w.transform, "Colour", Look.DrinkColor(id));
            dot.rectTransform.Place(new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(Loc.En ? 0 : 1, 0.5f), new Vector2(16, 16), new Vector2(Loc.En ? 10 : -10, 0));
            var txt = w.GetComponentInChildren<Text>();
            txt.rectTransform.offsetMin = new Vector2(Loc.En ? 30 : 6, 0); txt.rectTransform.offsetMax = new Vector2(Loc.En ? -6 : -30, 0);
            txt.alignment = TextAnchor.UpperCenter; txt.rectTransform.offsetMax += new Vector2(0, -6);
            labels.Add(txt);
            var pr = Ui.Label(w.transform, price.ToString("N0"), 12, new Color(1, 0.85f, 0.4f), TextAnchor.LowerCenter, Ui.Bold);
            pr.rectTransform.Fill(); pr.rectTransform.offsetMin = new Vector2(0, 5);
        }
        panel.gameObject.SetActive(false);
        Relabel();
        Loc.Changed += Relabel;
    }

    void Relabel()
    {
        if (panel == null) return;
        labels[0].Set(Loc.T("اطلب مشروب"));
        for (int i = 0; i < Menu.Length; i++) labels[i + 1].Set(Skins.Name(Menu[i].id));
    }

    void Toggle()
    {
        Sfx.Tap();
        panel.gameObject.SetActive(!panel.gameObject.activeSelf);
    }

    void Order(string id, int price)
    {
        if (Time.time - lastSent < 5f) { app.Toast(Loc.T("استنى الجرسون يجيب اللي طلبته")); return; }
        Sfx.Tap();
        lastSent = Time.time;
        app.Send(new Dictionary<string, object> { ["t"] = "order", ["k"] = id });
        panel.gameObject.SetActive(false);
    }

    void Update()
    {
        // only while you play online (not in the waiting room, not watching someone else's game, not in practice
        // against the computer: there's no café table to bring it to)
        var v = app != null ? app.view : null;
        bool on = v != null && v.phase != "lobby" && v.you >= 0 && !app.PracticeGame;
        if (button != null && button.activeSelf != on) button.SetActive(on);
        if (!on && panel != null && panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
    }
}
