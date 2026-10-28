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
  const s = await adm('PUT', '/settings', { economy: { startCoins: 77_000, streak: [1, 2, 3, 4, 5, 6, 7] }, tourney: { weekday: 5, hour: 21, game: 'chess' }, prizes: { egp: -5, prefixes: ['010', 'abc'] } });
  assert.equal(s.json.economy.startCoins, 77_000);
  assert.equal(ECONOMY.startCoins, 77_000);
  assert.deepEqual(STREAK, [1, 2, 3, 4, 5, 6, 7]);
  assert.equal(TOURNEY.weekday, 5);
  assert.equal(s.json.prizes.egp, 0);           // clamped to the lowest allowed
  assert.deepEqual(s.json.prizes.prefixes, ['010']);
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
  // and 500 EGP: the winner gives a Vodafone Cash number, the team pays it
  const pz = app.store.prizesOf(a.user.id)[0];
  assert.deepEqual([pz.egp, pz.status, pz.game, pz.month], [500, 'unclaimed', 'domino', prev]);
  assert.equal(app.store.claimPrize(b.user.id, pz.id, '01012345678'), 'prize_gone', 'not theirs');
  assert.equal(app.store.claimPrize(a.user.id, pz.id, '01012345678'), null);
  const list = await adm('GET', '/prizes?status=pending');
  assert.equal(list.json.rows[0].phone, '01012345678');
  assert.equal((await adm('POST', '/prizes/' + pz.id, { action: 'again', note: 'wrong number' })).status, 200);
  assert.equal(app.store.prizesOf(a.user.id)[0].status, 'unclaimed');
  app.store.claimPrize(a.user.id, pz.id, '01098765432');
  assert.equal((await adm('POST', '/prizes/' + pz.id, { action: 'paid', note: 'ref 1' })).status, 200);
  assert.equal(app.store.prizesOf(a.user.id)[0].status, 'paid');
  assert.equal((await adm('POST', '/prizes/' + pz.id, { action: 'paid' })).status, 409, 'paid once');
  assert.equal((await adm('GET', '/overview')).json.prizePaidEgp, 500);
});

test('admin: the season pass and the tournament can be switched off', async () => {
  const { SEASON_PASS } = await import('../shared/season');
  const g = await guest('Switch Tester');
  await adm('PUT', '/settings', { season: { enabled: false }, tourney: { enabled: false } });
  assert.equal(SEASON_PASS.enabled, false);
  assert.equal(TOURNEY.enabled, false);
  assert.equal(app.store.seasonClaim(g.user.id, 1, 'free'), 'season_off');
  assert.equal(app.store.seasonPremium(g.user.id, false), 'season_off');
  const sp = app.store.seasonState(g.user.id).sp;
  app.store.addSeasonSp(g.user.id, 50);
  assert.equal(app.store.seasonState(g.user.id).sp, sp, 'no season points while it is off');
  const j = await fetch(base + '/api/v1/tournament/join', { method: 'POST', headers: { Authorization: 'Bearer ' + g.token } });
  assert.equal(j.status, 409);
  assert.equal(((await j.json()) as { error: string }).error, 'tourney_off');
  assert.equal((await adm('GET', '/overview')).json.flags.season, false);
  await adm('PUT', '/settings', { season: { enabled: true }, tourney: { enabled: true } });
  assert.equal(SEASON_PASS.enabled, true);
});

test('retention: new missions count, the weekly chest, the first-game guide, the gift for coming back', async () => {
  const { counts, MISSIONS, CHEST } = await import('../shared/missions');
  const m = (id: string) => MISSIONS.find(x => x.id === id)!;
  assert.equal(counts(m('gift_1'), { type: 'gift' }), true);
  assert.equal(counts(m('play_any_3'), { type: 'gift' }), false);
  assert.equal(counts(m('friend_1'), { type: 'game', game: 'tawla', won: false, online: true, friend: true }), true);
  assert.equal(counts(m('friend_1'), { type: 'game', game: 'tawla', won: true, online: true }), false);
  assert.equal(counts(m('bigwin_1'), { type: 'game', game: 'chess', won: true, online: true, stake: 100_000 }), true);
  assert.equal(counts(m('bigwin_1'), { type: 'game', game: 'chess', won: true, online: true, stake: 20_000 }), false);
  // the chest: three collected missions open the first step
  const g = await guest('Chest Opener');
  const st = app.store.chestState(g.user.id);
  assert.deepEqual([st.done, st.opened], [0, []]);
  assert.equal(app.store.chestOpen(g.user.id, 0), 'chest_locked');
  for (const ms of app.store.missions(g.user.id)) for (let i = 0; i < ms.need; i++) app.store.missionProgress(g.user.id, ms.kind === 'gift' ? { type: 'gift' } : { type: 'game', game: ms.game === 'any' ? 'domino' : ms.game, won: true, online: true, friend: true, stake: 1_000_000 });
  for (const ms of app.store.missions(g.user.id)) assert.equal(typeof app.store.missionClaim(g.user.id, ms.id), 'object', ms.id);
  assert.equal(app.store.chestState(g.user.id).done, 3);
  const before = app.store.coinsOf(g.user.id);
  assert.deepEqual(app.store.chestOpen(g.user.id, 0), { coins: CHEST.coins[0], xp: CHEST.xp[0] });
  assert.equal(app.store.coinsOf(g.user.id), before + CHEST.coins[0]);
  assert.equal(app.store.chestOpen(g.user.id, 0), 'already_claimed');
  // the guide pays once
  assert.equal(app.store.tutorialDone(g.user.id, 10_000), 10_000);
  assert.equal(app.store.tutorialDone(g.user.id, 10_000), 0);
  // back after 5 days: a gift; after 1 day: none; and one push reminder while away
  const u = app.store.get(g.user.id)!;
  assert.equal(app.store.comebackGift({ ...u, seen: Date.now() - 86_400_000, created: Date.now() - 30 * 86_400_000 }, 3, 30_000), 0);
  assert.equal(app.store.comebackGift({ ...u, seen: Date.now() - 5 * 86_400_000, created: Date.now() - 30 * 86_400_000 }, 3, 30_000), 30_000);
});

test('events: dates from the dashboard, double season points, limited skins only during them, the event title', async () => {
  const { activeEvent } = await import('../shared/events');
  const now = Date.now();
  const s = await adm('PUT', '/settings', { events: [
    { id: 'ramadan_test', kind: 'ramadan', ar: 'رمضان كريم', en: 'Ramadan Kareem', from: now - 86_400_000, to: now + 86_400_000, spMult: 2, missionMult: 1.5, titleId: 'ramadan_lantern', titleGames: 2 },
    { id: 'bad', kind: 'eid', ar: 'x', en: 'x', from: now, to: now - 1 },
  ] });
  assert.equal(s.json.events.length, 1, 'an event that ends before it starts is dropped');
  assert.equal(activeEvent()?.id, 'ramadan_test');
  const g = await guest('Event Player');
  app.store.addCoins(g.user.id, 1_000_000);
  const sp = app.store.seasonState(g.user.id).sp;
  app.store.addSeasonSp(g.user.id, 10);
  assert.equal(app.store.seasonState(g.user.id).sp, sp + 20, 'season points ×2');
  assert.equal(app.store.buySkin(g.user.id, 'felt_ramadan'), null, 'the Ramadan cloth is on sale');
  assert.equal(app.store.buySkin(g.user.id, 'felt_eid'), 'not_on_sale', 'not the Eid one');
  await adm('PUT', '/settings', { events: [] });
  assert.equal(activeEvent(), null);
  assert.equal(app.store.buySkin(g.user.id, 'dice_ramadan'), 'not_on_sale', 'after Ramadan: not on sale');
  assert.ok(app.store.skinsOf(g.user.id).includes('felt_ramadan'), 'but kept by who bought it');
});
