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
        ["اتفرج على إعلان"] = "Watch an ad", ["دخول"] = "Sign in", ["ضيف"] = "Guest",
        // errors and notes
        ["افتح بالـ"] = "Open with", ["الحجر ده مش هيركب"] = "That tile doesn't fit", ["الحجر ده مش هينفع هنا"] = "That tile can't go there",
        ["اللعبة دي جاية قريب"] = "This game is coming soon", ["حصلت مشكلة:"] = "Error:",
        ["رصيدك مش كفاية للرهان ده"] = "Not enough coins for this stake", ["مش دورك"] = "Not your turn", ["مش متصل بالسيرفر لسه"] = "Not connected yet",
        ["خلصت إعلانات النهارده، تعالى بكرة"] = "No more ads today — come back tomorrow",
        ["الإعلان مش جاهز، جرّب تاني"] = "The ad isn't ready, try again",
        ["أهلاً! اتسجلت بحساب جوجل"] = "Welcome! Signed in with Google",
        ["تسجيل الدخول بجوجل مش متاح على السيرفر ده"] = "Google sign-in isn't set up on this server",
        ["اشتريت"] = "Bought", ["لبست"] = "Now using",
        ["الشراء بالفلوس جاي قريب"] = "Buying with money is coming soon to this version",
        ["مكافأة"] = "Bonus",
        // picker and queue
        ["فردي"] = "1 vs 1", ["٣ لاعبين"] = "3 players", ["٤ لاعبين"] = "4 players", ["زوجي ٢ ضد ٢"] = "Teams 2 vs 2",
        ["لعب سريع"] = "Quick play", ["رهان"] = "Stake", ["الرهان: كل لاعب يدفع، والفايز ياخد"] = "Stake: everyone pays in, the winner takes it",
        ["قريباً"] = "Coming soon", ["بندوّر على لاعبين…"] = "Finding players", ["إلغاء"] = "Cancel",
        ["الكمبيوتر هيدخل يلعب بعد"] = "Computer players join in", ["ث"] = "s",
        // at the table
        ["خروج"] = "Leave", ["شمال"] = "Left", ["يمين"] = "Right", ["شمال ·"] = "Left ·", ["يمين ·"] = "Right ·",
        ["أنت"] = "You", ["فريقك"] = "Your team", ["هم"] = "Them", ["الجولة"] = "Round", ["·  إلى"] = "·  to", ["·  رهان"] = "·  stake",
        ["دورك"] = "Your turn", ["يلعب"] = "is playing", ["قال باص"] = "passed",
        ["الحجر ينفع في الناحيتين، دوس على ناحية أو اسحبه لها"] = "The tile fits both ends. Pick one.",
        ["فاز بالجولة"] = "won the round", ["فاز بالمباراة"] = "won the match", ["نقطة"] = "points", ["عملة"] = "coins",
        ["فريقك فاز بالجولة"] = "Your team won the round", ["الفريق التاني فاز بالجولة"] = "The other team won the round",
        ["فريقك فاز!"] = "Your team won!", ["الفريق التاني فاز"] = "The other team won", ["فزت بالمباراة!"] = "You won the match!",
        ["تعادل"] = "Draw", ["تعادل، محدش سجّل"] = "A draw. Nobody scores.", ["اللعبة مقفولة  ·"] = "Blocked game  ·",
        ["العب تاني"] = "Play again",
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
        // the board games
        ["ارمي الزهر"] = "Roll the dice", ["دورك: ارمي الزهر"] = "Your turn: roll the dice", ["كش! احمي الملك"] = "Check! Save your king",
        ["اختار هتحطها فين"] = "Pick where it goes", ["دورك: حرّك قطعة"] = "Your turn: move a piece", ["حرّك قشاطاتك"] = "Move your checkers",
        ["اختار العسكري اللي هيتحرك"] = "Pick a pawn to move", ["مالوش لعب"] = "has no move", ["إلى"] = "to", ["كسبت الجولة"] = "won the round",
        ["مارس!"] = "Mars (double)!", ["كش مات"] = "Checkmate", ["بات (تعادل)"] = "Stalemate", ["الوقت خلص"] = "Out of time", ["وقتك"] = "Your clock",
        ["طاولة عادة"] = "Backgammon", ["محبوسة"] = "Mahbousa", ["٣١"] = "31", ["جلبهار"] = "Gulbahar", ["عادة"] = "Classic",
        ["٥ دقايق"] = "5 minutes", ["٢ لاعبين"] = "2 players",
        // ways to play
        ["العب مع صاحبك"] = "Play with a friend", ["تدريب"] = "Practice", ["التدريب ببلاش"] = "practice is free",
        ["اعمل غرفة وابعت الكود"] = "Make a room and send the code", ["غرفة ودية (من غير رهان)"] = "Friendly room (no stake)",
        ["أو ادخل بكود صاحبك"] = "or enter your friend's code", ["الكود"] = "Code", ["ادخل"] = "Join", ["رجوع"] = "Back",
        ["غرفة صحابك"] = "Your friends' room", ["انسخ"] = "Copy", ["شارك"] = "Share", ["ابدأ اللعب"] = "Start the game",
        ["الكود اتنسخ"] = "Code copied", ["كله جاهز، ابدأ"] = "Everyone's here — start",
        ["ابعت الكود لصحابك، أو حط الكمبيوتر مكانهم"] = "Send the code to your friends, or fill seats with the computer",
        ["مستنيين صاحب الغرفة يبدأ"] = "Waiting for the host to start", ["مكان فاضي…"] = "Empty seat…", ["كمبيوتر"] = "Computer",
        ["صاحب الغرفة"] = "Host", ["+ كمبيوتر"] = "+ Computer",
        ["مفيش غرفة بالكود ده"] = "No room with that code", ["لسه في أماكن فاضية"] = "Some seats are still empty", ["الغرفة مليانة"] = "The room is full",
        ["حد من اللاعبين رصيده مش كفاية"] = "A player doesn't have enough coins", ["ارمي الزهر الأول"] = "Roll the dice first",
        // voice chat
        ["الصوت شغال"] = "Voice on", ["الميك مقفول"] = "Mic muted", ["اسمح بالميكروفون عشان تتكلم"] = "Allow the microphone to talk",
        ["دردشة صوتية"] = "Voice chat",
        // cash out
        ["اسحب فلوسك"] = "Cash out", ["اسحب على فودافون كاش"] = "Cash out to Vodafone Cash", ["كل"] = "Every", ["كوين"] = "coins", ["جنيه"] = "EGP",
        ["رصيدك"] = "Your coins", ["فاضلك"] = "You need", ["كوين وتقدر تسحب"] = "more coins to cash out",
        ["العب واكسب، وخد مكافأتك اليومية، وشوف إعلانات عشان تجمع أسرع"] = "Win games, take your daily bonus and watch ads to collect faster",
        ["سجّل دخول الأول عشان تقدر تسحب فلوسك، وعشان كوينزك تتحفظ"] = "Sign in first to cash out (and keep your coins safe)",
        ["تسجيل الدخول بجوجل"] = "Sign in with Google", ["اطلب السحب"] = "Request payout",
        ["رقم فودافون كاش 010xxxxxxxx"] = "Vodafone Cash number 010xxxxxxxx",
        ["الفلوس بتتحول على محفظة فودافون كاش خلال يوم عمل واحد"] = "Money is sent to your Vodafone Cash wallet within 1 working day",
        ["لسه مطلبتش سحب"] = "No payouts yet", ["اتحولت"] = "Paid", ["اترفض (الكوينز رجعت)"] = "Declined (coins returned)", ["بيتحول…"] = "Processing…",
        ["طلبك وصل! هيتحولك"] = "Request received! You'll get", ["ج.م"] = "EGP", ["خلال يوم عمل واحد"] = "within 1 working day",
        ["سجّل دخول الأول عشان تقدر تسحب"] = "Sign in first to cash out", ["اكتب رقم فودافون صحيح يبدأ بـ 010"] = "Enter a valid Vodafone number starting with 010",
        ["عندك طلب لسه بيتحول، استنى لما يخلص"] = "You already have a payout in progress", ["المبلغ مش مظبوط"] = "That amount isn't right",
        ["استنى شوية وجرّب تاني"] = "Wait a bit and try again",
        ["لازم تفتح بأكبر دوش معاك"] = "You must open with your highest double", ["اختار هتحطها في أنهي ناحية"] = "Pick which end it goes on",
        ["معاك حجر ينفع، العبه"] = "You have a tile that fits — play it", ["اسحب من الكوم الأول"] = "Draw from the pile first",
        ["انت رميت الزهر خلاص"] = "You already rolled", ["لسه عندك لعب، حرّك"] = "You still have a move to make",
        ["الحركة دي مش هينفع دلوقتي"] = "That move isn't possible right now",
        ["دقيقة"] = "1 minute", ["٣ دقايق"] = "3 minutes", ["١٠ دقايق"] = "10 minutes", ["سريع ٢"] = "Quick 2", ["سريع ٤"] = "Quick 4",
        ["ماستر ٢"] = "Master 2", ["ماستر ٤"] = "Master 4", ["لودو سريع"] = "Quick Ludo", ["لودو ماستر"] = "Master Ludo",
        ["الكمبيوتر هيدخل يلعب بعد"] = "Computer players join in", ["بندوّر على لاعبين…"] = "Finding players…",
        ["الحجر ينفع في الناحيتين، دوس على ناحية أو اسحبه لها"] = "The tile fits both ends — tap one or drag it there",
        ["العب تاني"] = "Play again", ["اتفرج على إعلان"] = "Watch an ad", ["اختار العسكري اللي هيتحرك"] = "Pick a pawn to move",
        ["بات (تعادل)"] = "Stalemate (draw)", ["قريباً"] = "Coming soon", ["الشراء بالفلوس جاي قريب"] = "Buying with money is coming soon",
        ["تعادل، محدش سجّل"] = "A draw — nobody scores", ["اختار هتحطها فين"] = "Pick where it goes", ["حصلت مشكلة:"] = "Error:",
    };
}
