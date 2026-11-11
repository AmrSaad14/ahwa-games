// Leagues (bronze to diamond, per game and season: their rewards), the governorates, and the governorate picker
// (Settings, and the leaderboard's governorate ranking).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class Menus
{

    static readonly (string id, string ar, string en, Color color, long reward, int from)[] Leagues =
    {
        ("bronze", "برونز", "Bronze", new Color(0.80f, 0.50f, 0.20f), 5_000, 0),
        ("silver", "فضة", "Silver", new Color(0.78f, 0.80f, 0.84f), 15_000, 300),
        ("gold", "دهب", "Gold", new Color(1f, 0.82f, 0.36f), 40_000, 700),
        ("platinum", "بلاتين", "Platinum", new Color(0.50f, 0.88f, 0.86f), 100_000, 1200),
        ("diamond", "ماس", "Diamond", new Color(0.55f, 0.72f, 1f), 250_000, 1800),
    };
    static int LeagueIndex(string id) { for (int i = 0; i < Leagues.Length; i++) if (Leagues[i].id == id) return i; return 0; }
    static string LeagueName(string id) { var l = Leagues[LeagueIndex(id)]; return Loc.En ? l.en : l.ar; }

    static readonly string[,] Govs =
    {
        { "cairo", "القاهرة", "Cairo" }, { "giza", "الجيزة", "Giza" }, { "alex", "الإسكندرية", "Alexandria" }, { "qalyubia", "القليوبية", "Qalyubia" },
        { "sharqia", "الشرقية", "Sharqia" }, { "dakahlia", "الدقهلية", "Dakahlia" }, { "gharbia", "الغربية", "Gharbia" }, { "monufia", "المنوفية", "Monufia" },
        { "beheira", "البحيرة", "Beheira" }, { "kafr", "كفر الشيخ", "Kafr El Sheikh" }, { "damietta", "دمياط", "Damietta" }, { "portsaid", "بورسعيد", "Port Said" },
        { "ismailia", "الإسماعيلية", "Ismailia" }, { "suez", "السويس", "Suez" }, { "fayoum", "الفيوم", "Faiyum" }, { "benisuef", "بني سويف", "Beni Suef" },
        { "minya", "المنيا", "Minya" }, { "assiut", "أسيوط", "Asyut" }, { "sohag", "سوهاج", "Sohag" }, { "qena", "قنا", "Qena" },
        { "luxor", "الأقصر", "Luxor" }, { "aswan", "أسوان", "Aswan" }, { "redsea", "البحر الأحمر", "Red Sea" }, { "newvalley", "الوادي الجديد", "New Valley" },
        { "matrouh", "مطروح", "Matrouh" }, { "northsinai", "شمال سيناء", "North Sinai" }, { "southsinai", "جنوب سيناء", "South Sinai" },
    };
    static string GovName(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < Govs.GetLength(0); i++) if (Govs[i, 0] == id) return Loc.En ? Govs[i, 2] : Govs[i, 1];
        return id;
    }

    /// Settings: which governorate you play for.
    void OpenGovPicker()
    {
        Panel(Loc.T("محافظتك"), out var body, new Vector2(600, 340));
        var list = Scroll(body, 4);
        RectTransform line = null;
        for (int i = 0; i < Govs.GetLength(0); i++)
        {
            if (i % 3 == 0) line = Row(list, 38);
            string id = Govs[i, 0];
            int k = Loc.En ? i % 3 : 2 - i % 3;
            bool on = user?.Str("gov") == id;
            var b = Ui.Button(line, Loc.En ? Govs[i, 2] : Govs[i, 1], on ? Ui.Gold : Card, on ? Ui.Ink : Color.white, 14, () =>
            {
                Sfx.Tap();
                app.Send(new Dictionary<string, object> { ["t"] = "setGov", ["gov"] = id });
                if (user != null) { user["gov"] = id; user["country"] = "EG"; }
                Close(); PickerDone();
            });
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(k / 3f, 0); rt.anchorMax = new Vector2((k + 1) / 3f, 1); rt.offsetMin = new Vector2(3, 2); rt.offsetMax = new Vector2(-3, -2);
        }
    }
}
