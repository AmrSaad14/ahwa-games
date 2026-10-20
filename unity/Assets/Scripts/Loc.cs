// Languages: Arabic (the default) and English. The Arabic text itself is the key: Loc.T("لعب سريع") gives
// "Quick play" in English. Spaces around a key are kept, so pieces of a sentence can be translated one by one.
// Labels made with Ui.Label remember their key and change language by themselves; texts that are worked out
// as the game goes (scores, turns) are redrawn by their owners when Loc.Changed fires.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class Loc
{
    public static bool En { get; private set; } = PlayerPrefs.GetString("lang", "ar") == "en";
    public static event Action Changed;

    public static void Set(bool en)
    {
        if (en == En) return;
        En = en;
        PlayerPrefs.SetString("lang", en ? "en" : "ar"); PlayerPrefs.Save();
        foreach (var kv in new List<KeyValuePair<Text, string>>(tracked))
            if (kv.Key != null) kv.Key.text = Arabic.Fix(T(kv.Value)); else tracked.Remove(kv.Key);
        Changed?.Invoke();
    }

    public static string T(string ar)
    {
        if (!En || string.IsNullOrEmpty(ar)) return ar;
        var core = ar.Trim();
        if (!En_.TryGetValue(core, out var en)) return ar;
        int lead = ar.IndexOf(core, StringComparison.Ordinal);
        return ar.Substring(0, lead) + en + ar.Substring(lead + core.Length);
    }
    public static bool Has(string ar) => !string.IsNullOrEmpty(ar) && En_.ContainsKey(ar.Trim());

    static readonly Dictionary<Text, string> tracked = new Dictionary<Text, string>();
    public static void Track(Text t, string key) { if (Has(key)) tracked[t] = key; }
    public static void Untrack(Text t) { tracked.Remove(t); }

    /// A number the way people read it: 5K, 1.5M.
    public static string Short(long n) => n >= 1_000_000 ? (n / 1_000_000.0).ToString("0.#") + "M" : n >= 1000 ? (n / 1000.0).ToString("0.#") + "K" : n.ToString();

    static readonly Dictionary<string, string> En_ = new Dictionary<string, string>
    {
        // home
        ["اختار لعبتك"] = "Pick a game",
        ["اضغط على أي ترابيزة في القهوة"] = "Tap any table in the café",
        ["جاري الاتصال…"] = "Connecting…",
        ["متصل"] = "Online",
        ["دومينو"] = "Dominoes", ["طاولة"] = "Backgammon", ["شطرنج"] = "Chess", ["لودو"] = "Ludo",
        ["المتجر"] = "Shop", ["المتصدرين"] = "Leaders", ["طريقة اللعب"] = "How to play", ["الإعدادات"] = "Settings",
        ["شاهد إعلان"] = "Watch an ad", ["دخول"] = "Sign in", ["ضيف"] = "Guest",
        // errors and notes
        ["افتح بالـ"] = "Open with", ["الحجر ده مش هيركب"] = "That tile doesn't fit", ["الحجر ده مش هينفع هنا"] = "That tile can't go there",
        ["اللعبة دي لسه جاية في نسخة Unity"] = "This game is coming soon", ["خطأ:"] = "Error:",
        ["رصيدك مش كفاية للرهان ده"] = "Not enough coins for this stake", ["مش دورك"] = "Not your turn", ["مش متصل بالسيرفر لسه"] = "Not connected yet",
        ["خلصت إعلانات النهارده، تعالى بكرة"] = "No more ads today — come back tomorrow",
        ["الإعلان مش جاهز، جرّب تاني"] = "The ad isn't ready, try again",
        ["أهلاً! اتسجلت بحساب جوجل"] = "Welcome! Signed in with Google",
        ["تسجيل الدخول بجوجل مش متاح على السيرفر ده"] = "Google sign-in isn't set up on this server",
        ["اشتريت"] = "Bought", ["لبست"] = "Now using",
        ["الشراء بالفلوس جاي قريباً في نسخة Unity"] = "Buying with money is coming soon to this version",
        ["مكافأة"] = "Bonus",
        // picker and queue
        ["فردي"] = "1 vs 1", ["٣ لاعبين"] = "3 players", ["٤ لاعبين"] = "4 players", ["زوجي ٢ ضد ٢"] = "Teams 2 vs 2",
        ["لعب سريع"] = "Quick play", ["رهان"] = "Stake", ["الرهان: كل لاعب يدفع، والفايز ياخد"] = "Stake: everyone pays in, the winner takes it",
        ["قريباً في نسخة Unity"] = "Coming soon", ["البحث عن لاعبين"] = "Finding players", ["إلغاء"] = "Cancel",
        ["سينضم لاعبو الكمبيوتر خلال"] = "Computer players join in", ["ث"] = "s",
        // at the table
        ["خروج"] = "Leave", ["شمال"] = "Left", ["يمين"] = "Right", ["شمال ·"] = "Left ·", ["يمين ·"] = "Right ·",
        ["أنت"] = "You", ["فريقك"] = "Your team", ["هم"] = "Them", ["الجولة"] = "Round", ["·  إلى"] = "·  to", ["·  رهان"] = "·  stake",
        ["دورك"] = "Your turn", ["يلعب"] = "is playing", ["قال باص"] = "passed",
        ["الحجر يناسب الطرفين. اختار المكان."] = "The tile fits both ends. Pick one.",
        ["فاز بالجولة"] = "won the round", ["فاز بالمباراة"] = "won the match", ["نقطة"] = "points", ["عملة"] = "coins",
        ["فريقك فاز بالجولة"] = "Your team won the round", ["الفريق التاني فاز بالجولة"] = "The other team won the round",
        ["فريقك فاز!"] = "Your team won!", ["الفريق التاني فاز"] = "The other team won", ["فزت بالمباراة!"] = "You won the match!",
        ["تعادل"] = "Draw", ["تعادل. لا أحد يسجل."] = "A draw. Nobody scores.", ["اللعبة مقفولة  ·"] = "Blocked game  ·",
        ["العب مرة أخرى"] = "Play again",
        // settings
        ["الصوت"] = "Sound", ["الموسيقى"] = "Music", ["الاهتزاز"] = "Vibration", ["اللغة"] = "Language",
        ["العربية"] = "العربية", ["English"] = "English", ["الحساب"] = "Account", ["اسمك"] = "Your name", ["حفظ"] = "Save",
        ["تسجيل الدخول بجوجل"] = "Sign in with Google", ["تسجيل الخروج"] = "Sign out",
        ["بتلعب كضيف. سجّل عشان فلوسك وتقدمك يتحفظوا."] = "You're playing as a guest. Sign in to keep your coins and progress.",
        ["متسجل بـ"] = "Signed in as", ["تشغيل"] = "On", ["إيقاف"] = "Off", ["إغلاق"] = "Close",
        // shop
        ["عملات"] = "Coins", ["أحجار الدومينو"] = "Domino tiles", ["زهر"] = "Dice", ["بدون إعلانات"] = "No ads",
        ["شهرياً"] = "monthly", ["ج.م"] = "EGP", ["مجاني"] = "Free", ["متلبس"] = "In use", ["البس"] = "Use", ["اشتري"] = "Buy",
        ["رصيدك"] = "Your coins",
        ["عاجي"] = "Ivory", ["أبنوس"] = "Ebony", ["رخام"] = "Marble", ["يشم"] = "Jade", ["ذهب"] = "Gold",
        ["ياقوت"] = "Ruby", ["سبج"] = "Obsidian", ["فيروز"] = "Turquoise",
        // leaderboard
        ["الترتيب"] = "Rank", ["التقييم"] = "Rating", ["مكسب"] = "Wins", ["لعبة"] = "Games", ["مركزك"] = "Your place",
        ["جاري التحميل…"] = "Loading…", ["مفيش نتايج لسه"] = "No results yet",
        // ads
        ["إعلان"] = "Ad", ["المكافأة بعد"] = "Reward in", ["+٥٬٠٠٠ عملة"] = "+5,000 coins", ["فاضل النهارده"] = "left today",
        ["العب بورد باتلز مع أصحابك"] = "Play Board Battles with your friends",
        ["دومينو · طاولة · شطرنج · لودو"] = "Dominoes · Backgammon · Chess · Ludo",
    };
}
