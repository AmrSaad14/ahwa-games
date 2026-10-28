// The dashboard (/admin) and its API (/api/admin): signing in, players, rewards, settings, prize requests.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { createServer } from '../server/index';
import { config } from '../server/config';
import { ECONOMY, STREAK } from '../shared/gifts';
import { TOURNEY } from '../server/tourney';

const KEY = 'test-admin-key-123';
let app: ReturnType<typeof createServer>;
let base = '';
let cookie = '';
before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', adminKey: KEY,
    turnMs: 3000, botFillMs: 300, nextRoundMs: 30, botDelay: [5, 10], lobbyAfterMatchMs: 200, msgsPer5s: 10000, autoMoveMs: 30,
    login: { ...config.login, sms: 'console' }, billing: { ...config.billing, testMode: true, serviceAccount: '' } } as typeof config);
  base = `http://127.0.0.1:${await app.listen()}`;
});
after(async () => { await app.close(); });

async function adm(method: string, path: string, body?: unknown, auth = true) {
  const r = await fetch(base + '/api/admin' + path, { method, headers: { 'Content-Type': 'application/json', ...(auth && cookie ? { Cookie: cookie } : {}) }, body: body ? JSON.stringify(body) : undefined });
  return { status: r.status, json: await r.json().catch(() => null) as Record<string, any>, headers: r.headers };
}
async function guest(name: string) {
  const r = await fetch(base + '/api/v1/auth/guest', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ name, ch: 1 }) });
  return await r.json() as { token: string; user: { id: string; coins: number } };
}

test('admin: the page is served, the API needs the key', async () => {
  const page = await fetch(base + '/admin');
  assert.equal(page.status, 200);
  assert.match(await page.text(), /Control room/);
  assert.equal(page.headers.get('x-frame-options'), 'DENY');
  assert.equal((await adm('GET', '/overview', undefined, false)).status, 401);
  assert.equal((await adm('POST', '/login', { key: 'nope' }, false)).status, 403);
  const ok = await adm('POST', '/login', { key: KEY }, false);
  assert.equal(ok.status, 200);
  cookie = (ok.headers.get('set-cookie') || '').split(';')[0];
  assert.match(cookie, /^bb_admin=\d+\./);
  const o = await adm('GET', '/overview');
  assert.equal(o.status, 200);
  assert.equal(o.json.daily.length, 14);
  assert.equal(typeof o.json.live.online, 'number');
  // the header key works too (for scripts)
  const h = await fetch(base + '/api/admin/overview', { headers: { 'x-admin-key': KEY } });
  assert.equal(h.status, 200);
});

test('admin: players, coins, skins, ban', async () => {
  const g = await guest('Dash Player');
  const found = await adm('GET', '/players?q=Dash');
  assert.ok(found.json.rows.some((r: { id: string }) => r.id === g.user.id));
  const before = (await adm('GET', '/players/' + g.user.id)).json.user.coins;
  assert.equal((await adm('POST', '/players/' + g.user.id, { action: 'coins', amount: 250_000, reason: 'test' })).json.coins, before + 250_000);
  assert.equal((await adm('POST', '/players/' + g.user.id, { action: 'skin', skin: 'felt_royal' })).status, 200);
  const p = (await adm('GET', '/players/' + g.user.id)).json;
  assert.ok(p.skins.includes('felt_royal'));
  assert.ok(p.coins.some((c: { kind: string }) => c.kind === 'admin_gift'));
  await adm('POST', '/players/' + g.user.id, { action: 'ban', reason: 'cheating' });
  assert.equal((await adm('GET', '/players/' + g.user.id)).json.row.banned, true);
  await adm('POST', '/players/' + g.user.id, { action: 'unban' });
  assert.equal((await adm('GET', '/players/' + g.user.id)).json.row.banned, false);
  const log = (await adm('GET', '/log')).json.rows.map((r: { action: string }) => r.action);
  for (const a of ['coins', 'skin', 'ban', 'unban', 'login']) assert.ok(log.includes(a), a);
});

test('admin: a gift for everyone', async () => {
  const g = await guest('Gift Getter');
  const r = await adm('POST', '/rewards', { coins: 10_000, activeDays: 7, message: '', push: false });
  assert.ok(r.json.players >= 1);
  assert.equal((await adm('GET', '/players/' + g.user.id)).json.user.coins, g.user.coins + 10_000);
});

test('admin: settings apply to the running game and are checked', async () => {
  const s = await adm('PUT', '/settings', { economy: { startCoins: 77_000, streak: [1, 2, 3, 4, 5, 6, 7] }, tourney: { weekday: 5, hour: 21, game: 'chess' }, cashout: { coins: 5, prefixes: ['010', 'abc'] } });
  assert.equal(s.json.economy.startCoins, 77_000);
  assert.equal(ECONOMY.startCoins, 77_000);
  assert.deepEqual(STREAK, [1, 2, 3, 4, 5, 6, 7]);
  assert.equal(TOURNEY.weekday, 5);
  assert.equal(s.json.cashout.coins, 10_000);           // clamped to the lowest allowed
  assert.deepEqual(s.json.cashout.prefixes, ['010']);
  const t = (await adm('GET', '/tourney')).json;
  assert.equal(t.view.game, 'chess');
  assert.equal(new Date(t.view.startsAt).getUTCDay() === 5 || new Date(t.view.startsAt + 3 * 3_600_000).getUTCDay() === 5, true);
  const g = await guest('After Settings');
  assert.equal(g.user.coins, 77_000);
  await adm('POST', '/settings/reset', {});
  assert.notEqual(ECONOMY.startCoins, 77_000);
});

test('admin: maintenance blocks new games', async () => {
  await adm('PUT', '/settings', { maintenance: { on: true } });
  assert.equal((await adm('GET', '/overview')).json.flags.maintenance, true);
  await adm('PUT', '/settings', { maintenance: { on: false } });
});

test('titles: the designs, giving one, wearing it, and player of the month', async () => {
  const t = await adm('GET', '/titles');
  assert.ok(t.json.list.length >= 16);
  assert.ok(t.json.list.some((x: { id: string; monthly?: string }) => x.id === 'potm_domino' && x.monthly === 'domino'));
  // a new design from the dashboard (bad colours keep the defaults)
  const made = await adm('PUT', '/titles', { id: 'night_owl', ar: 'بومة الليل', en: 'Night Owl', style: { bg: ['#101030', 'nope'], fg: '#ffffff', border: '#8888ff', icon: 'star', effect: 'glow' } });
  assert.equal(made.json.id, 'night_owl');
  assert.match(made.json.style.bg[1], /^#[0-9a-f]{6}$/);
  const g = await guest('Title Holder');
  assert.equal((await adm('POST', '/players/' + g.user.id, { action: 'title', title: 'night_owl', days: 30, notify: false })).status, 200);
  const me = await fetch(base + '/api/v1/me', { headers: { Authorization: 'Bearer ' + g.token } }).then(r => r.json()) as { title: string; titles: { id: string; until: number }[] };
  assert.equal(me.title, 'night_owl');
  assert.ok(me.titles[0].until > Date.now() + 29 * 86_400_000);
  assert.equal((await adm('GET', '/titles/night_owl/holders')).json.rows.length, 1);
  // the monthly ones can't be removed; the others can, and are taken from players
  assert.equal((await adm('DELETE', '/titles/potm_domino')).status, 400);
  assert.equal((await adm('DELETE', '/titles/night_owl')).status, 200);
  assert.equal(app.store.wornTitle(g.user.id), null);
  // last month's best domino player (enough games) gets the title for this month
  const { seasonOf } = await import('../shared/season');
  const [y, mo] = seasonOf(Date.now()).split('-').map(Number);
  const prev = mo === 1 ? `${y - 1}-12` : `${y}-${String(mo - 1).padStart(2, '0')}`;
  app.store.metaSet('potm_' + prev, '');   // not given yet
  const a = await guest('Domino Ace'), b = await guest('Few Games');
  for (let i = 0; i < 12; i++) app.store.addMonthly(a.user.id, 'domino', i % 2 === 0, prev);
  for (let i = 0; i < 5; i++) app.store.addMonthly(b.user.id, 'domino', true, prev);   // more points a game, but too few games
  const won = app.hub.monthlyTitles();
  assert.deepEqual(won.map(w => [w.uid, w.title]), [[a.user.id, 'potm_domino']]);
  assert.equal(app.store.wornTitle(a.user.id), 'potm_domino');
  assert.deepEqual(app.hub.monthlyTitles(), [], 'once a month');
});
