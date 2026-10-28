// Small pages served by the game server: the support inbox (/admin/support?key=ADMIN_KEY) and invite links.

const esc = (s: unknown) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
const when = (ts: number | null) => ts ? new Intl.DateTimeFormat('ar-EG', { timeZone: 'Africa/Cairo', dateStyle: 'medium', timeStyle: 'short' }).format(new Date(ts)) : '';

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
