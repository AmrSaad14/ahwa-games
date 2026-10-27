// Checks a running game server from the outside, the way the app uses it:
//   node scripts/check-server.mjs https://ahwa-games.a7medhussien.com
// health and version, the API, the legal pages, the sign-in settings, and a real WebSocket session
// (a guest account is made, the leaderboard and the shop answer, a practice-free quick game is queued and left).
import WebSocket from 'ws';

const base = (process.argv[2] || 'https://ahwa-games.a7medhussien.com').replace(/\/$/, '');
const results = [];
const ok = (name, pass, detail = '') => { results.push({ name, pass, detail }); console.log(`${pass ? 'PASS' : 'FAIL'}  ${name}${detail ? '  · ' + detail : ''}`); };

async function get(path) {
  const t = Date.now();
  try { const r = await fetch(base + path, { redirect: 'manual' }); const text = await r.text(); return { status: r.status, text, ms: Date.now() - t, headers: r.headers }; }
  catch (e) { return { status: 0, text: String(e.message || e), ms: Date.now() - t }; }
}

const h = await get('/api/v1/health');
let health = {}; try { health = JSON.parse(h.text); } catch { /* not JSON */ }
ok('server answers /api/v1/health', h.status === 200 && health.ok === true, `HTTP ${h.status}, ${h.ms} ms ${h.status === 200 ? JSON.stringify(health).slice(0, 160) : h.text.slice(0, 120)}`);
const cat = await get('/api/v1/catalog');
let catalog = {}; try { catalog = JSON.parse(cat.text); } catch { /* */ }
ok('catalog has the new skins (checkers, chess pieces)', cat.status === 200 && Array.isArray(catalog.skins) && catalog.skins.some(s => s.cat === 'checkers') && catalog.skins.some(s => s.cat === 'pieces'), `HTTP ${cat.status}${catalog.skins ? ', ' + catalog.skins.length + ' skins' : ''}`);
ok('season pass is 299 EGP', catalog.season?.pass?.egp === 299, `egp ${catalog.season?.pass?.egp}`);
for (const p of ['/privacy', '/terms', '/delete-account', '/api/docs']) { const r = await get(p); ok(`page ${p}`, r.status === 200, `HTTP ${r.status}`); }
const g = await get('/auth/google/start?to=app');
ok('Google sign-in is set up (redirects to Google)', g.status >= 300 && g.status < 400 && /accounts\.google\.com/.test(g.headers?.get('location') || ''), `HTTP ${g.status} → ${(g.headers?.get('location') || g.text).slice(0, 90)}`);
if (g.status >= 300 && g.status < 400) {
  const loc = g.headers.get('location') || '';
  const red = decodeURIComponent((loc.match(/redirect_uri=([^&]+)/) || [])[1] || '');
  ok('Google comes back to this address', red === base + '/auth/google/callback', red || '(none)');
}

// the live game, like the app: connect, become a guest, ask for the leaderboard and the shop
await new Promise(resolve => {
  const url = base.replace(/^http/, 'ws') + '/ws';
  const ws = new WebSocket(url);
  const seen = new Set(); let done = false;
  const finish = () => { if (done) return; done = true; try { ws.close(); } catch { /* */ } resolve(); };
  const timer = setTimeout(() => { ok('WebSocket session', false, 'timed out; seen: ' + [...seen].join(', ')); finish(); }, 15000);
  ws.on('open', () => { ok('WebSocket connects to /ws', true, url); ws.send(JSON.stringify({ t: 'hello', v: 2 })); });
  ws.on('error', e => { ok('WebSocket connects to /ws', false, String(e.message || e)); clearTimeout(timer); finish(); });
  ws.on('message', raw => {
    let m; try { m = JSON.parse(String(raw)); } catch { return; }
    seen.add(m.t);
    if (m.t === 'welcome') {
      ok('a guest account is made', !!m.user?.id, `coins ${m.user?.coins}, token ${m.token ? 'yes' : 'no'}`);
      ws.send(JSON.stringify({ t: 'lb', game: 'domino' }));
      ws.send(JSON.stringify({ t: 'quick', mode: 'ffa2', stake: 5000 }));
    }
    if ((m.t === 'queue' || m.t === 'table') && !seen.has('queued')) {
      seen.add('queued');
      ok('matchmaking answers (quick game queued)', true, m.t);
      ws.send(JSON.stringify({ t: 'cancel' })); ws.send(JSON.stringify({ t: 'leave' }));
      clearTimeout(timer); setTimeout(finish, 300);
    }
    if (m.t === 'err') console.log('  (server said: ' + m.code + ')');
  });
});

const failed = results.filter(r => !r.pass).length;
console.log(`\n${results.length - failed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
