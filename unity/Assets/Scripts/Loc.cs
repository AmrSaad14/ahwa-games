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
    public static bool En { get; private set; } = PlayerPrefs.GetString("lang", "en") == "en";   // English until the player picks Arabic
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
        ["دوس على المكان اللي بينوّر أو اسحب الحجر له"] = "The tile fits both ends. Pick one.",
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
        ["طريقة العرض"] = "View", ["كامل"] = "Full", ["اللوح بس"] = "Board only", ["عرض اللوح بس"] = "Board only view", ["عرض كامل"] = "Full view",
        ["توفير البطارية"] = "Battery saver", ["إزاي تلعب"] = "How to play", ["يلا نلعب"] = "Let's play", ["التالي"] = "Next", ["تخطي"] = "Skip",
        ["العب دلوقتي"] = "Play now", ["الأسبوع"] = "This week", ["أول ١٠ بيكسبوا كوينز كل يوم اتنين"] = "Top 10 win coins every Monday",
        ["الأول"] = "1st", ["فاضل"] = "ends in", ["ي"] = "d", ["س"] = "h", ["نقط"] = "Points", ["الجايزة"] = "Prize", ["مستوى"] = "Lv",
        ["مستوى جديد!"] = "Level up!", ["مبروك! جيت المركز"] = "Congratulations! You finished", ["مكافأة النهارده"] = "Today's reward", ["أهلاً! اتسجلت بحساب أبل"] = "Welcome! Signed in with Apple", ["الدخول بجوجل"] = "Sign in with Google", ["الدخول بأبل"] = "Sign in with Apple", ["سجّل دخولك"] = "Sign in", ["عشان كوينزك وتقدمك يتحفظوا وتلعب من أي موبايل"] = "Keep your coins and progress, and play on any phone", ["كمّل بجوجل"] = "Continue with Google", ["كمّل بأبل"] = "Continue with Apple", ["تسجيل الدخول بأبل مش متاح على السيرفر ده"] = "Sign in with Apple isn't set up on this server",
        ["المهام"] = "Missions", ["الموسم"] = "Season", ["صحابك"] = "Friends", ["البطولة"] = "Tournament",
        ["الشات مقفول عندك شوية بسبب بلاغات"] = "Your chat is off for a while after reports", ["لسه المهمة مخلصتش"] = "That mission isn't done yet", ["خدتها خلاص"] = "Already collected", ["لسه موصلتش للمستوى ده"] = "You haven't reached that tier yet",
        ["افتح الموسم المميز الأول"] = "Unlock the premium season first", ["الموسم المميز مفتوح عندك"] = "Your premium season is already open", ["البطولة بدأت خلاص"] = "The tournament has already started", ["مفيش ماتش ليك دلوقتي"] = "No match for you right now",
        ["الكود ده مش صحيح"] = "That code isn't right", ["دخلت كود صاحبك قبل كده"] = "You already entered a friend's code", ["كود الدعوة للاعبين الجداد بس"] = "Invite codes are for new players", ["ده كودك انت"] = "That's your own code",
        ["مفيش لاعب بالكود ده"] = "No player has that code", ["صاحبك مش متصل دلوقتي"] = "Your friend isn't online", ["حد رصيده مش كفاية للماتش تاني"] = "Someone doesn't have the coins for a rematch", ["لسه مش معاك"] = "You don't have it yet",
        ["أصوات القهوة"] = "Café sounds", ["كود الدعوة"] = "Invite code", ["كود صاحبك"] = "Friend's code", ["تأكيد"] = "OK",
        ["طربوش"] = "Hats", ["مفرش"] = "Cloth", ["مشروب"] = "Drinks", ["المفرش بيبان ليك على ترابيزتك"] = "The cloth shows on your table",
        ["الطربوش بيبان لكل اللي على الترابيزة (للرجالة)"] = "Everyone at the table sees your hat (on the men)", ["المشروب بيبان قدامك لكل اللي على الترابيزة"] = "Everyone at the table sees your drink", ["ده لاعب كمبيوتر"] = "That's a computer player", ["بطاقة اللاعب"] = "Player card",
        ["بيلعب دلوقتي"] = "Playing now", ["مش متصل"] = "Offline", ["من"] = "Since", ["ماتشات"] = "Matches",
        ["خسارة"] = "Losses", ["نسبة المكسب"] = "Win rate", ["كوينز كسبها"] = "Coins won", ["كوينز خسرها"] = "Coins lost",
        ["صافي المكسب"] = "Net gains", ["أكبر مكسب"] = "Biggest win", ["جوايز البطولات"] = "Prize money", ["لسه ملعبش أونلاين"] = "No online games yet",
        ["شارك كودك"] = "Share your code", ["إلغاء الصداقة"] = "Remove friend", ["الطلب اتبعت · إلغاء"] = "Requested · Cancel", ["قبول الصداقة"] = "Accept friend",
        ["أضف صديق"] = "Add friend", ["إلغاء الكتم"] = "Unmute", ["كتم الشات"] = "Mute chat", ["مش هتشوف رسايله"] = "You won't see their messages",
        ["هتشوف رسايله تاني"] = "You'll see their messages again", ["بلاغ"] = "Report", ["وصلنا البلاغ، شكراً. ومش هتشوف رسايله"] = "Thanks, we got your report. You won't see their messages", ["كودك"] = "Your code",
        ["ادعي صحابك  +50K"] = "Invite friends  +50K", ["أضف"] = "Add", ["لسه مفيش صحاب. ابعت كودك أو دوس على أي لاعب في الترابيزة وضيفه"] = "No friends yet. Share your code, or tap any player at a table to add them", ["عايز يبقى صاحبك"] = "wants to be your friend",
        ["الطلب اتبعت"] = "Request sent", ["قبول"] = "Accept", ["رفض"] = "Decline", ["ادعيه يلعب"] = "Invite to play",
        ["بيدعوك تلعب"] = "invites you to play", ["دعوة للعب"] = "Invitation", ["ادخل العب"] = "Join", ["بعدين"] = "Later",
        ["بقى صاحبك"] = "is now your friend", ["طلب الصداقة اتبعت لـ"] = "Friend request sent to", ["خلصت مهمة! خد جايزتك"] = "Mission done! Collect your reward", ["وصلت مستوى"] = "You reached tier",
        ["في الموسم!"] = "of the season!", ["جالك"] = "You got", ["ماتشك في البطولة جاهز! افتح البطولة وادخل"] = "Your tournament match is ready! Open the tournament to play", ["لعب أول ماتش، خدتوا انتو الاتنين"] = "played their first game: you both got",
        ["هدية دعوة صاحبك"] = "Your friend's invite gift", ["مهام النهارده"] = "Today's missions", ["خد الجايزة"] = "Collect", ["لسه"] = "Not yet",
        ["مهام جديدة بعد"] = "New missions in", ["الماتشات ضد الكمبيوتر بتتحسب كمان"] = "Games against the computer count too", ["المستوى"] = "Tier", ["خلصت الموسم!"] = "Season complete!",
        ["المميز مفتوح"] = "Premium open", ["افتح المميز"] = "Unlock premium", ["رصيدك مش كفاية"] = "Not enough coins", ["مميز"] = "Premium",
        ["كل ماتش ومهمة ومكافأة يومية بتديك نقط للموسم"] = "Every game, mission and daily reward earns season points", ["مقفول"] = "Locked", ["افتح المميز الأول"] = "Unlock premium first", ["الأصلي"] = "Original",
        ["أخضر بطولات"] = "Tournament green", ["أزرق نيلي"] = "Nile blue", ["أسود ودهب"] = "Black & gold", ["أو"] = "or", ["استنى لحد ما تتصل بالسيرفر"] = "Wait until you're connected", ["الدفع لسه بيتأكد، هيوصلك أول ما يخلص"] = "The payment is still being confirmed; you'll get it as soon as it is", ["الزهر في الطاولة واللودو"] = "Your dice in tawla and ludo", ["الشراء لسه مش شغال على السيرفر"] = "Purchases aren't switched on on the server yet", ["الشراء ما تمش، محدش خصم منك حاجة"] = "The purchase didn't go through; nothing was charged", ["الشراء مش متاح دلوقتي"] = "Purchases aren't available right now", ["الشراء من Google Play على الموبايل بس"] = "Purchases go through Google Play on your phone", ["الطاولة بتبان ليك وانت بتلعب"] = "Your tawla board while you play", ["المميز اتفتح! الموسم كله بقى ليك"] = "Premium unlocked! The whole season is yours", ["المنتج ده لسه مش متاح في Google Play"] = "This product isn't on Google Play yet", ["ترابيزات"] = "Boards", ["ترابيزة الدومينو بتبان ليك وانت بتلعب"] = "Your domino table while you play", ["جوز وصدف"] = "Walnut & pearl", ["خشب زيتون"] = "Olive wood", ["خشب ورد"] = "Rosewood", ["دهب"] = "Gold", ["دهب وفضة"] = "Gold & silver", ["رقعة الشطرنج بتبان ليك وانت بتلعب"] = "Your chess board while you play", ["زمرد"] = "Emerald", ["صدف لولي"] = "Mother of pearl", ["صيني وأزرق"] = "Porcelain & cobalt", ["عاج وأبنوس"] = "Ivory & ebony", ["عاج وياقوت"] = "Ivory & ruby", ["عساكر الشطرنج بتاعتك"] = "Your chess pieces", ["عندك"] = "You have", ["فرعوني"] = "Pharaoh", ["قشاط الطاولة بتاعك"] = "Your tawla checkers", ["قطع"] = "Pieces", ["قطع الدومينو اللي في إيدك وعلى الترابيزة"] = "The domino tiles in your hand and on the table", ["كل مجموعتك"] = "Whole collection", ["كهرمان"] = "Amber", ["مجموعتي"] = "Collection", ["مفيش إعلانات خلاص، استمتع"] = "No more ads. Enjoy!", ["ملكي زمردي"] = "Royal emerald", ["من الموسم"] = "Season", ["ورد أحمر"] = "Red rosewood", ["ياقوت ولولي"] = "Ruby & pearl", ["يشم وأونيكس"] = "Jade & onyx",
        ["طربوش أحمر"] = "Red fez", ["طربوش أسود"] = "Black fez", ["طربوش أخضر"] = "Green fez", ["طربوش كحلي"] = "Navy fez",
        ["طربوش أبيض"] = "White fez", ["طربوش دهبي"] = "Gold fez", ["خشب"] = "Wood", ["جوخ أخضر"] = "Green baize",
        ["جوخ أحمر"] = "Red baize", ["جوخ أزرق"] = "Blue baize", ["ملكي"] = "Royal", ["كليم"] = "Kilim",
        ["شاي"] = "Tea", ["شاي بالنعناع"] = "Mint tea", ["قهوة"] = "Coffee", ["كركديه"] = "Hibiscus",
        ["سحلب"] = "Sahlab", ["قصب"] = "Sugarcane", ["زهر دهب"] = "Gold dice", ["دومينو فردي"] = "Dominoes 1 vs 1",
        ["شطرنج ٣ دقايق"] = "3-minute chess", ["بطولة الأسبوع"] = "Weekly tournament", ["لاعب"] = "players", ["التاني"] = "2nd",
        ["التالت والرابع"] = "3rd–4th", ["من ٥ لـ ٨"] = "5th–8th", ["خروج المغلوب، ماتش واحد كل دور، وكل اللي يلعب ياخد"] = "Knockout, one match a round; everyone who plays gets", ["تبدأ بعد"] = "Starts in",
        ["د"] = "m", ["إنت مشترك  ·  انسحب"] = "You're in  ·  Drop out", ["اشترك ببلاش"] = "Sign up free", ["النهائي"] = "Final",
        ["نص النهائي"] = "Semi-final", ["ربع النهائي"] = "Quarter-final", ["إنت في ماتشك"] = "You're in your match", ["ادخل ماتشك"] = "Go to my match",
        ["خرجت من البطولة، حظ أحسن الأسبوع الجاي"] = "You're out. Better luck next week!", ["البطولة شغالة دلوقتي"] = "The tournament is on now", ["البطل"] = "Winner", ["إنت"] = "you",
        ["ماتش تاني معاهم"] = "Rematch", ["لاعبين جداد"] = "New players", ["مستنيهم"] = "Waiting", ["حد من اللاعبين مشي"] = "A player left",
        ["عايزين ماتش تاني معاك!"] = "They want a rematch!", ["بيرجع"] = "back in", ["الكمبيوتر بيلعب مكانه"] = "computer playing", ["الدعوة اتبعتت، استنى صاحبك في الغرفة"] = "Invitation sent: wait for your friend in the room",
        ["الرسالة اتنسخت"] = "Message copied", ["يومي"] = "Daily", ["اتاخدت"] = "Collected", ["المكافأة الجاية بعد"] = "Next reward in", ["المكافأة جاهزة!"] = "Your reward is ready!", ["من يوم ٧ وطالع: 50K كل يوم"] = "From day 7 on: 50K every day",
        ["ارجع كل يوم والمكافأة تكبر"] = "Come back every day and it grows", ["يوم"] = "Day", ["خد"] = "Collect", ["مستوى الكمبيوتر"] = "Computer level", ["سهل"] = "Easy", ["عادي"] = "Normal", ["صعب"] = "Hard", ["رجّع"] = "Undo",
        ["الدعم الفني"] = "Contact support", ["كلّمنا"] = "Message us", ["اكتب مشكلتك أو اقتراحك وهنرد عليك في أقرب وقت"] = "Tell us about a problem or an idea — we'll get back to you soon",
        ["رسالتك…"] = "Your message…", ["إيميلك أو رقمك (اختياري)"] = "Your e-mail or phone (optional)", ["ابعت"] = "Send",
        ["اكتب رسالتك الأول"] = "Write your message first", ["وصلتنا رسالتك، شكراً! هنرد عليك قريب"] = "Thanks! We got your message and will reply soon",
        ["دقيقة"] = "1 minute", ["٣ دقايق"] = "3 minutes", ["١٠ دقايق"] = "10 minutes", ["سريع ٢"] = "Quick 2", ["سريع ٤"] = "Quick 4",
        ["ماستر ٢"] = "Master 2", ["ماستر ٤"] = "Master 4", ["لودو سريع"] = "Quick Ludo", ["لودو ماستر"] = "Master Ludo",
        ["الكمبيوتر هيدخل يلعب بعد"] = "Computer players join in", ["بندوّر على لاعبين…"] = "Finding players…",
        ["دوس على المكان اللي بينوّر أو اسحب الحجر له"] = "Tap a glowing spot or drag the tile there",
        ["العب تاني"] = "Play again", ["اتفرج على إعلان"] = "Watch an ad", ["اختار العسكري اللي هيتحرك"] = "Pick a pawn to move",
        ["بات (تعادل)"] = "Stalemate (draw)", ["قريباً"] = "Coming soon", ["الشراء بالفلوس جاي قريب"] = "Buying with money is coming soon",
        ["تعادل، محدش سجّل"] = "A draw — nobody scores", ["اختار هتحطها فين"] = "Pick where it goes", ["حصلت مشكلة:"] = "Error:",
    };
}
