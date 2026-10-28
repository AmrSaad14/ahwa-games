// The public pages Google Play asks for: the privacy policy (/privacy), the terms of use (/terms), and a page
// to ask for an account to be deleted without the app (/delete-account). Arabic and English on each page.
// The deletion form files a support message (read at /admin/support); in the app, Settings → Delete my account
// deletes at once.

const STYLE = `body{margin:0;background:#1b120c;color:#f4e6c8;font:16px/1.65 system-ui,-apple-system,"Segoe UI",sans-serif}
main{max-width:760px;margin:0 auto;padding:28px 20px 60px}h1{color:#f3cd62;font-size:28px;margin:0 0 4px}h2{color:#f3cd62;font-size:19px;margin:28px 0 6px}
.sub{opacity:.7;font-size:14px}.ar{direction:rtl;text-align:right;border-top:1px solid rgba(243,205,98,.25);margin-top:36px;padding-top:12px}
a{color:#f3cd62}ul{padding-inline-start:22px}nav{display:flex;gap:16px;margin-bottom:18px;font-size:14px}
form{display:grid;gap:10px;margin-top:14px}input,textarea{font:inherit;padding:10px 12px;border-radius:10px;border:1px solid rgba(243,205,98,.35);background:#2a1c13;color:#fff}
button{font:inherit;font-weight:700;padding:12px;border:0;border-radius:10px;background:#f3cd62;color:#1b120c;cursor:pointer}.ok{color:#8fd19e;font-weight:700}`;

function page(title: string, body: string): string {
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>${title} · Board Battles</title><style>${STYLE}</style></head><body><main>
<nav><a href="/privacy">Privacy</a><a href="/terms">Terms</a><a href="/delete-account">Delete account</a></nav>${body}</main></body></html>`;
}

const UPDATED = '28 October 2026';

/** What the cash-out offers right now (the dashboard's settings). */
export interface CashoutTerms { enabled: boolean; coins: number; egp: number }
const fmt = (n: number) => n.toLocaleString('en');

export function privacyPage(c?: CashoutTerms): string {
  return page('Privacy policy', `
<h1>Privacy policy</h1><div class="sub">Board Battles, by Qimma Labs · last updated ${UPDATED}</div>
<h2>What we keep</h2><ul>
<li><b>Your game account:</b> a player id the app makes on first start, the name and character you choose, your coins, level, ratings, game results, skins, friends, missions and season progress.</li>
<li><b>If you sign in:</b> with Google, your Google account id, name and e-mail; with Apple, your Apple id (and e-mail if you share it); with a mobile number, that number.</li>
<li><b>Purchases:</b> what you bought on Google Play and its order number, to credit it once. We never see your card or payment details.</li>
<li><b>Messages to support</b> and reports you send about other players, with your player id.</li>
<li><b>Notifications:</b> your device's push token, if you allow reminders.</li>
${c?.enabled ? '<li><b>Prize requests:</b> the Vodafone Cash number you give us, the amount and the date, to pay you and keep a record.</li>' : ''}</ul>
<h2>What we don't keep</h2><ul><li>Voice chat goes directly between the players' phones (or through our relay when a direct route is blocked); it is never recorded.</li>
<li>Table chat is quick phrases and emojis, not stored after the game.</li><li>We don't sell your data and don't use it for anything but running the game.</li></ul>
<h2>Services we use</h2><ul>
<li><b>Google Play</b> (purchases, the install referrer for invite codes), <b>Google AdMob</b> (ads; AdMob may use your device's advertising id, see <a href="https://policies.google.com/technologies/ads">Google's ad policy</a>), <b>Firebase Cloud Messaging</b> (reminders).</li>
<li><b>Google</b> and <b>Apple</b> sign-in, and an SMS provider for mobile-number codes, only when you choose them.</li></ul>
<h2>How long</h2><p>As long as your account exists. Delete it any time: in the app, Settings → Delete my account, or on the <a href="/delete-account">delete-account page</a>. Everything above is removed; purchase records the law requires us to keep are kept for that time only.</p>
<h2>Children</h2><p>The game is for players 13 and over${c?.enabled ? '; prize requests are for adults (18+) living in Egypt' : '. Coins can\'t be cashed out'}.</p>
<h2>Contact</h2><p>In the app: Settings → Contact support.</p>
<div class="ar"><h1>سياسة الخصوصية</h1>
<h2>بنحتفظ بإيه</h2><ul><li>حسابك في اللعبة: رقم اللاعب، الاسم والشخصية، الكوينز، المستوى، النتايج، السكينز، الصحاب، المهام والموسم.</li>
<li>لو سجلت دخول: بجوجل (رقم الحساب والاسم والإيميل)، بأبل (رقم الحساب، والإيميل لو شاركته)، أو برقم الموبايل.</li>
<li>المشتريات من Google Play ورقم الطلب، عشان تتحسب مرة واحدة. عمرنا ما بنشوف بيانات الكارت.</li>
<li>رسايل الدعم والبلاغات، وتوكن الإشعارات لو سمحت بيها.</li>${c?.enabled ? '<li>رقم فودافون كاش اللي بتكتبه في طلب الجايزة، عشان نحولك ونحتفظ بسجل.</li>' : ''}</ul>
<h2>مش بنحتفظ بـ</h2><ul><li>الكلام الصوتي مش بيتسجل. شات الترابيزة مش بيتخزن. مش بنبيع بياناتك.</li></ul>
<h2>خدمات بنستخدمها</h2><ul><li>Google Play، Google AdMob للإعلانات، Firebase للإشعارات، وتسجيل الدخول بجوجل أو أبل أو الموبايل لو اخترته.</li></ul>
<h2>امسح حسابك</h2><p>من اللعبة: الإعدادات ← امسح حسابي، أو من <a href="/delete-account">صفحة مسح الحساب</a>.</p></div>`);
}

export function termsPage(c?: CashoutTerms): string {
  return page('Terms of use', `
<h1>Terms of use</h1><div class="sub">Board Battles, by Qimma Labs · last updated ${UPDATED}</div>
<ul><li>Board Battles is a game for fun. Coins can't be sold or traded between players.</li>
${c?.enabled ? `<li><b>Prizes:</b> players aged 18 or over who live in Egypt can ask to exchange coins for a Vodafone Cash transfer: every ${fmt(c.coins)} coins = ${fmt(c.egp)} EGP. Requests are checked by hand before paying (the account must be signed in and have played enough online games); we can refuse a request that breaks these terms, and the coins then go back. The rate and the rules can change.</li>` : `<li><b>Coins have no money value</b>: they can't be cashed out.</li>`}
<li>Coins can't be bought. Purchases (the season pass, the ad-free month) are digital items for the game, sold through Google Play under its refund rules.</li>
<li>Be decent: no cheating, no abuse in names or chat, no harassment. Players others report are muted automatically, and we may close accounts that break these rules.</li>
<li>One account per player. Don't use bots or modified apps.</li>
<li>The game is provided as it is; we do our best to keep it running but can't promise it never stops. We may change or end features and seasons.</li>
<li>You must be 13 or over to play.</li></ul>
<div class="ar"><h1>شروط الاستخدام</h1><ul>
<li>بورد باتلز لعبة للتسلية. الكوينز مش بتتباع ولا بتتبادل بين اللاعيبة.</li>
${c?.enabled ? `<li><b>الجوايز:</b> اللاعيبة من ١٨ سنة وساكنين في مصر يقدروا يطلبوا تحويل الكوينز لفودافون كاش: كل ${fmt(c.coins)} كوين = ${fmt(c.egp)} جنيه. الطلبات بتتراجع قبل الدفع (لازم الحساب يكون متسجل ولعبت ماتشات أونلاين كفاية)، وممكن نرفض الطلب اللي بيخالف الشروط وترجعلك الكوينز. السعر والقواعد ممكن يتغيروا.</li>` : `<li><b>الكوينز ملهاش قيمة فلوس</b>: مش بتتسحب.</li>`}
<li>الكوينز مش بتتباع. المشتريات (الموسم المميز، بدون إعلانات) حاجات رقمية جوه اللعبة، بتتباع من خلال Google Play وبقواعد الاسترجاع بتاعته.</li>
<li>خليك محترم: ممنوع الغش والشتيمة والمضايقة. اللي بيتبلغ عنه بيتكتم، وممكن نقفل الحساب اللي بيكسر القواعد.</li>
<li>حساب واحد لكل لاعب، وممنوع البوتات والنسخ المعدلة. لازم يكون عندك ١٣ سنة أو أكتر.</li></ul></div>`);
}

export function deletePage(_?: CashoutTerms): string {
  return page('Delete your account', `
<h1>Delete your Board Battles account</h1>
<p><b>Fastest, in the game:</b> Settings → <b>Delete my account</b>. It is deleted at once.</p>
<p><b>Without the app:</b> send your friend code (Friends → Your code) and how you signed in. We delete the account within 7 days:
your profile, coins, games, skins, friends and messages. Records of Google Play purchases are kept only as long as the law requires.</p>
<form id="f"><input name="code" placeholder="Your friend code (e.g. M6QUVY)" maxlength="12" required>
<input name="contact" placeholder="How you signed in (Google e-mail, phone number…)" maxlength="200">
<textarea name="note" rows="3" placeholder="Anything else (optional)" maxlength="500"></textarea>
<button>Ask to delete my account · امسح حسابي</button><div id="m"></div></form>
<div class="ar"><h1>امسح حسابك</h1><p>من اللعبة: الإعدادات ← <b>امسح حسابي</b>، وبيتمسح على طول.</p>
<p>من غير اللعبة: اكتب كود الصحاب بتاعك وإزاي كنت بتسجل دخول، وهنمسح الحساب خلال ٧ أيام.</p></div>
<script>document.getElementById('f').onsubmit=async function(e){e.preventDefault();var d=new FormData(this),m=document.getElementById('m');
var text='Account deletion request. Friend code: '+d.get('code')+'. '+(d.get('note')||'');
try{var r=await fetch('/api/support',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({text:text,contact:d.get('contact'),app:'delete-account page'})});
m.className=r.ok?'ok':'';m.textContent=r.ok?'Received. Your account will be deleted within 7 days. · وصلنا طلبك.':'Something went wrong, try again.';if(r.ok)this.reset();}catch(_){m.textContent='Something went wrong, try again.'}};</script>`);
}
