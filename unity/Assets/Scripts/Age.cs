// The neutral age question (Google Play's Families policy, for an app played by children and adults): asked once,
// on the first start, before any ad. The answer stays on the phone.
//   under 16 (a child in the EU; under 13 in the US): no ads at all (the ads SDK isn't even started). Unity's
//     rewarded video can't be closed after 5 seconds, which the Families policy asks of ads children see.
//   children (under 13): also no voice chat and no location
//   under 18: ads that aren't personalised (Unity Ads "non-behavioral"), and no money prizes (they need a wallet number)
// Until it's answered the player counts as a child.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public static class Age
{
    const string Key = "age.year";
    public static int Year => PlayerPrefs.GetInt(Key, 0);
    public static bool Known => Year > 1900;
    /// Years old at least (the birthday may not have come yet this year).
    public static int Years => Known ? DateTime.Now.Year - Year - 1 : 0;
    public static bool Child => Years < 13;
    /// Old enough for ads (16 and over).
    public static bool AdsAge => Years >= 16;
    public static bool Minor => Years < 18;

    public static void Set(int year) { PlayerPrefs.SetInt(Key, year); PlayerPrefs.Save(); Debug.Log("[age] set (" + (Child ? "child" : Minor ? "under 18" : "adult") + ")"); }

    /// Asks the year of birth: a plain question, no hint of the "right" answer, no way past it without one.
    public static IEnumerator Ask(App app)
    {
        var canvas = Ui.MakeCanvas("Age", 60);
        canvas.gameObject.AddComponent<TallCanvas>();
        var root = Ui.Rect("Age", canvas.transform).Fill();
        var shade = Ui.Box(root, "Shade", new Color(0.05f, 0.02f, 0.02f, 0.82f), false); shade.rectTransform.Fill(); shade.raycastTarget = true;
        var card = Ui.Box(root, "Card", new Color32(0x2a, 0x10, 0x12, 0xf6)).rectTransform;
        card.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 250), Vector2.zero);
        Ui.Label(card, "سنة ميلادك كام؟", 22, Ui.Gold, TextAnchor.MiddleCenter, Ui.Bold).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, 36), new Vector2(0, -22));
        var sub = Ui.Label(card, "عشان نظبط اللعبة على سنك", 13, new Color(1, 1, 1, 0.75f));
        sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(340, 22), new Vector2(0, -58));

        // the year, typed (four digits)
        var bg = Ui.Box(card, "Year", new Color(1, 1, 1, 0.94f)); bg.raycastTarget = true;
        bg.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(180, 50), new Vector2(0, 2));
        var text = Ui.Label(bg.transform, "", 26, Ui.Ink, TextAnchor.MiddleCenter, Ui.Bold);
        UnityEngine.Object.Destroy(text.GetComponent<FitText>());
        text.rectTransform.Fill(6); text.raycastTarget = false; text.supportRichText = false;
        var hint = Ui.Label(bg.transform, "YYYY", 22, new Color(0, 0, 0, 0.3f), TextAnchor.MiddleCenter, Ui.Bold);
        UnityEngine.Object.Destroy(hint.GetComponent<FitText>());
        hint.rectTransform.Fill(6);
        var field = bg.gameObject.AddComponent<InputField>();
        field.textComponent = text; field.placeholder = hint; field.characterLimit = 4;
        field.contentType = InputField.ContentType.IntegerNumber;
        field.keyboardType = TouchScreenKeyboardType.NumberPad;

        var err = Ui.Label(card, "", 12, new Color(1f, 0.6f, 0.55f));
        err.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(340, 18), new Vector2(0, -36));

        bool done = false;
        var ok = Ui.Button(card, "تمام", Ui.Gold, Ui.Ink, 17, () =>
        {
            Sfx.Tap();
            int now = DateTime.Now.Year;
            if (!int.TryParse(field.text.Trim(), out int y) || y < now - 110 || y > now) { err.Set(Loc.T("اكتب السنة بالأرقام، زي ") + (now - 20)); return; }
            Set(y); done = true;
        });
        ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 46), new Vector2(0, 20));

        if (App.Recording)
        {
            // the test run: show it, take its picture, answer for a grown-up (so the rest of the run is as before)
            yield return new WaitForSeconds(0.8f);
            App.Still("0-age.png");
            yield return new WaitForSeconds(0.3f);
            Set(1990); done = true;
        }
        while (!done) yield return null;
        UnityEngine.Object.Destroy(canvas.gameObject);
    }
}
