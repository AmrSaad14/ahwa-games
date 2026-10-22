// The admin page for cash-out requests: /admin/cashouts?key=ADMIN_KEY
// Pay each one by Vodafone Cash yourself, then press "تم الدفع" (the player gets a notification), or "رفض" to
// turn it down (the coins go back to the player).
import type { CashoutDb } from './db';

const esc = (s: unknown) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const when = (ts: number | null) => ts ? new Intl.DateTimeFormat('ar-EG', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(ts)) : '';
const STATUS: Record<string, string> = { pending: 'مستني الدفع', paid: 'اتدفع', rejected: 'اترفض' };

export function cashoutPage(rows: (CashoutDb & { name: string; coins_now: number })[], key: string, status: string, pushOn: boolean): string {
  const k = esc(key);
  const pending = rows.filter(r => r.status === 'pending');
  const total = pending.reduce((a, r) => a + r.egp, 0);
  const tabs = ['pending', 'paid', 'rejected', 'all'].map(s =>
    `<a class="tab${s === status ? ' on' : ''}" href="/admin/cashouts?key=${encodeURIComponent(key)}&status=${s}">${s === 'all' ? 'الكل' : STATUS[s]}</a>`).join('');
  const body = rows.map(r => `
    <tr class="${r.status}">
      <td>#${r.id}</td>
      <td>${esc(when(r.ts))}</td>
      <td>${esc(r.name || '—')}<div class="sub">${esc(r.user_id)}</div></td>
      <td class="phone"><b dir="ltr">${esc(r.phone)}</b> <button type="button" onclick="navigator.clipboard.writeText('${esc(r.phone)}');this.textContent='✓'">نسخ</button></td>
      <td><b>${r.egp} ج.م</b><div class="sub">${r.coins.toLocaleString('en')} كوين</div></td>
      <td>${STATUS[r.status]}${r.done_ts ? `<div class="sub">${esc(when(r.done_ts))}</div>` : ''}${r.note ? `<div class="sub">${esc(r.note)}</div>` : ''}</td>
      <td>${r.status === 'pending' ? `
        <form method="post" action="/admin/cashouts" onsubmit="return confirm('اتأكدت إنك حولت ${r.egp} جنيه على ${esc(r.phone)}؟')">
          <input type="hidden" name="key" value="${k}"><input type="hidden" name="id" value="${r.id}"><input type="hidden" name="action" value="paid">
          <input name="note" placeholder="رقم العملية (اختياري)"><button class="ok">تم الدفع</button>
        </form>
        <form method="post" action="/admin/cashouts" onsubmit="return confirm('ترفض الطلب وترجّع الكوينز؟')">
          <input type="hidden" name="key" value="${k}"><input type="hidden" name="id" value="${r.id}"><input type="hidden" name="action" value="reject">
          <input name="note" placeholder="السبب (اختياري)"><button class="no">رفض</button>
        </form>` : ''}</td>
    </tr>`).join('');
  return `<!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<meta name="robots" content="noindex"><title>طلبات السحب · Board Battles</title>
<style>
  body{font-family:system-ui,Segoe UI,Tahoma,sans-serif;background:#1b120c;color:#ffe9b0;margin:0;padding:16px}
  h1{color:#f3cd62;font-size:22px;margin:0 0 4px} .muted{opacity:.7;font-size:13px}
  .tabs{margin:14px 0;display:flex;gap:6px;flex-wrap:wrap} .tab{padding:6px 12px;border-radius:8px;background:#2a1c13;color:#ffe9b0;text-decoration:none}
  .tab.on{background:#f3cd62;color:#1b120c;font-weight:700}
  .wrap{overflow-x:auto} table{border-collapse:collapse;width:100%;min-width:760px;background:#22150e;border-radius:10px}
  th,td{padding:10px;border-bottom:1px solid #3a2a1e;text-align:right;vertical-align:top;font-size:14px}
  th{color:#f3cd62;font-weight:600} .sub{font-size:11px;opacity:.6;margin-top:3px}
  tr.paid td{opacity:.65} tr.rejected td{opacity:.45}
  form{display:flex;gap:4px;margin:0 0 6px} input{background:#120b07;border:1px solid #4a3626;color:#ffe9b0;border-radius:6px;padding:5px;font-size:12px;width:130px}
  button{border:0;border-radius:6px;padding:6px 10px;font-weight:700;cursor:pointer;background:#3a2a1e;color:#ffe9b0}
  button.ok{background:#2f6b3a;color:#fff} button.no{background:#6b2f2f;color:#fff}
  .phone b{font-size:16px;letter-spacing:.5px}
</style></head><body>
<h1>طلبات السحب على فودافون كاش</h1>
<div class="muted">كل ١٬٠٠٠٬٠٠٠ كوين = ١٠ جنيه · مستني الدفع: ${pending.length} طلب (${total} جنيه) · الإشعارات: ${pushOn ? 'شغالة' : 'مش متفعّلة (FIREBASE_SERVICE_ACCOUNT)'}</div>
<div class="tabs">${tabs}</div>
<div class="wrap"><table><thead><tr><th>الطلب</th><th>التاريخ</th><th>اللاعب</th><th>رقم فودافون كاش</th><th>المبلغ</th><th>الحالة</th><th></th></tr></thead>
<tbody>${body || '<tr><td colspan="7" class="muted">مفيش طلبات</td></tr>'}</tbody></table></div>
</body></html>`;
}

/** The support inbox: /admin/support?key=ADMIN_KEY */
export function supportPage(rows: { id: number; user_id: string | null; name: string; contact: string; text: string; app: string; ts: number; mailed: number }[], mailOn: boolean, to: string): string {
  const body = rows.map(r => `<div class="m"><div class="h"><b>#${r.id}</b> · ${esc(when(r.ts))} · ${esc(r.name || 'player')} ${r.contact ? '· <span dir="ltr">' + esc(r.contact) + '</span>' : ''} ${r.mailed ? '· ✉️' : ''}<span class="sub"> ${esc(r.app)} ${esc(r.user_id ?? '')}</span></div><div class="t">${esc(r.text)}</div></div>`).join('');
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="noindex"><title>Support · Board Battles</title>
<style>body{font-family:system-ui,Segoe UI,Tahoma,sans-serif;background:#1b120c;color:#ffe9b0;margin:0;padding:16px}h1{color:#f3cd62;font-size:22px;margin:0 0 4px}
.muted{opacity:.7;font-size:13px;margin-bottom:14px}.m{background:#22150e;border-radius:10px;padding:12px;margin-bottom:10px}.h{font-size:13px;opacity:.85;margin-bottom:6px}
.t{white-space:pre-wrap;font-size:15px;line-height:1.5;color:#fff}.sub{opacity:.5;font-size:11px}</style></head><body>
<h1>Support messages</h1><div class="muted">${rows.length} messages · e-mail to ${esc(to)}: ${mailOn ? 'on' : 'off (set SMTP_HOST, SMTP_USER, SMTP_PASS)'}</div>${body || '<div class="muted">No messages yet</div>'}</body></html>`;
}

/** The page a "Play with me" link opens: straight into the app's room, or the store if it isn't installed. */
export function invitePage(code: string, scheme: string, pkg: string): string {
  const app = `${scheme}://join?code=${encodeURIComponent(code)}`;
  const store = `https://play.google.com/store/apps/details?id=${encodeURIComponent(pkg)}`;
  const intent = `intent://join?code=${encodeURIComponent(code)}#Intent;scheme=${scheme};package=${pkg};S.browser_fallback_url=${encodeURIComponent(store)};end`;
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Play Board Battles with me</title><meta property="og:title" content="Play Board Battles with me!"><meta property="og:description" content="Room code ${esc(code)} · Dominoes, Backgammon, Chess and Ludo">
<style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:radial-gradient(circle at 50% 40%,#7a1424,#2a070c);font-family:system-ui,sans-serif;color:#ffe9b0;text-align:center}
.c{padding:24px;max-width:420px}h1{color:#f3cd62;font-size:26px;margin:0 0 8px}.code{font-size:40px;letter-spacing:8px;font-weight:800;color:#fff;margin:16px 0}
a.b{display:block;margin:12px auto;padding:14px 18px;border-radius:12px;background:#f3cd62;color:#1b120c;font-weight:800;text-decoration:none;font-size:18px}
a.s{background:transparent;border:1px solid #f3cd62;color:#f3cd62}.m{opacity:.75;font-size:14px}</style></head><body><div class="c">
<h1>Play Board Battles with me!</h1><div class="m">العب معايا بورد باتلز</div><div class="code">${esc(code)}</div>
<a class="b" id="open" href="${esc(intent)}">Open the game · افتح اللعبة</a>
<a class="b s" href="${esc(store)}">Get it on Google Play</a>
<div class="m">Already in the game? Tap "Play with a friend" and enter the code.</div></div>
<script>if(!/Android/i.test(navigator.userAgent)){document.getElementById('open').href=${JSON.stringify(app)}}setTimeout(function(){location.href=document.getElementById('open').href},400)</script></body></html>`;
}

/** A friend's invite link (/i/CODE): opens the game if it's installed; otherwise Google Play, which hands the code
 *  to the game on its first start (the Play install referrer), so both players get the referral bonus. */
export function referPage(code: string, scheme: string, pkg: string): string {
  const store = `https://play.google.com/store/apps/details?id=${encodeURIComponent(pkg)}&referrer=${encodeURIComponent('ref=' + code)}`;
  const intent = `intent://ref?code=${encodeURIComponent(code)}#Intent;scheme=${scheme};package=${pkg};S.browser_fallback_url=${encodeURIComponent(store)};end`;
  return `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Play Board Battles with me</title><meta property="og:title" content="Play Board Battles with me! 🎁 50K coins for you"><meta property="og:description" content="Dominoes, Backgammon, Chess and Ludo in an Egyptian café. Use my code ${esc(code)} and we both get 50K coins.">
<style>body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;background:radial-gradient(circle at 50% 40%,#7a1424,#2a070c);font-family:system-ui,sans-serif;color:#ffe9b0;text-align:center}
.c{padding:24px;max-width:420px}h1{color:#f3cd62;font-size:26px;margin:0 0 8px}.code{font-size:36px;letter-spacing:6px;font-weight:800;color:#fff;margin:14px 0}
a.b{display:block;margin:12px auto;padding:14px 18px;border-radius:12px;background:#f3cd62;color:#1b120c;font-weight:800;text-decoration:none;font-size:18px}.m{opacity:.8;font-size:14px;line-height:1.5}</style></head><body><div class="c">
<h1>Play Board Battles with me!</h1><div class="m">العب معايا بورد باتلز، وإحنا الاتنين ناخد ٥٠ ألف كوين</div><div class="code">${esc(code)}</div>
<a class="b" id="go" href="${esc(intent)}">Get the game · نزّل اللعبة</a>
<div class="m">We both get 50K coins after your first game. If the code doesn't fill in by itself, enter it in Settings → Invite code.</div></div>
<script>if(!/Android/i.test(navigator.userAgent)){document.getElementById('go').href=${JSON.stringify(store)}}setTimeout(function(){location.href=document.getElementById('go').href},500)</script></body></html>`;
}
