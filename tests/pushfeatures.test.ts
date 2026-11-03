// Skins earned by watching ads, the daily-reward reminder's targets, and inviting a friend who is offline
// (a notification, and the invitation waiting for them when they open the game).
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';
import { SKINS, adsForSkin, skinById } from '../shared/skins';
import { ECONOMY, cairoDay } from '../shared/gifts';

// a made-up Firebase key: push counts as set up (sending fails quietly here, there is no network)
const { privateKey } = crypto.generateKeyPairSync('rsa', { modulusLength: 2048 });
const SA = JSON.stringify({ client_email: 'test@example.iam.gserviceaccount.com', project_id: 'test', private_key: privateKey.export({ type: 'pkcs8', format: 'pem' }).toString() });

let app: ReturnType<typeof createServer>;
let port = 0;
before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 60_000, msgsPer5s: 10000,
    guestsOnline: false, push: { ...config.push, serviceAccount: SA }, login: { ...config.login, sms: 'console' } } as typeof config);
  port = await app.listen();
});
after(async () => { await app.close(); });

async function client(token?: string) {
  const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
  const msgs: any[] = [];
  ws.on('message', d => msgs.push(JSON.parse(d.toString())));
  await new Promise<void>((r, j) => { ws.once('open', () => r()); ws.once('error', j); });
  ws.send(JSON.stringify({ t: 'hello', v: 2, token }));
  const until = async (fn: () => boolean) => { const end = Date.now() + 8000; while (!fn()) { if (Date.now() > end) throw new Error('timed out: ' + JSON.stringify(msgs.slice(-4)).slice(0, 400)); await new Promise(r => setTimeout(r, 30)); } };
  await until(() => msgs.some(m => m.t === 'welcome'));
  const last = (t: string) => [...msgs].reverse().find(m => m.t === t);
  return { ws, msgs, until, last, send: (m: unknown) => ws.send(JSON.stringify(m)), welcome: msgs.find(m => m.t === 'welcome') };
}
async function signedIn(sub: string, name: string) {
  const c = await client();
  c.send({ t: 'ticket', ticket: app.google.testTicket({ sub, email: sub + '@example.com', name }) });
  await c.until(() => c.msgs.some(m => m.t === 'signedIn'));
  await c.until(() => c.last('user')?.user.name === name);
  return c;
}

test('ads per skin: dearer skins need more ads, 30 at least and 150 at most; season and free skins none', () => {
  assert.equal(adsForSkin(skinById('drink_mint')), 30);
  assert.equal(adsForSkin(skinById('tiles_gold')), 150);
  assert.equal(adsForSkin(skinById('dice_gold')), 100);
  assert.equal(adsForSkin(skinById('dice_pharaoh')), 0);
  assert.equal(adsForSkin(skinById('dice_ivory')), 0);
  for (const s of SKINS) { const n = adsForSkin(s); if (n) { assert.ok(n >= 30 && n <= 150 && n % 5 === 0, s.id); } }
  // dearer never needs fewer
  const sorted = SKINS.filter(s => adsForSkin(s)).sort((a, b) => a.price - b.price);
  for (let i = 1; i < sorted.length; i++) assert.ok(adsForSkin(sorted[i]) >= adsForSkin(sorted[i - 1]));
});

test('watching enough ads wins the skin (worn at once); a daily limit; owned and season skins refused', () => {
  const u = app.store.createGuest('Ads', 0);
  const before = ECONOMY.skinAdsPerDay;
  ECONOMY.skinAdsPerDay = 1000;
  try {
    for (let i = 1; i < 30; i++) assert.deepEqual(app.store.addSkinAd(u.id, 'drink_mint'), { n: i, need: 30, won: false });
    assert.deepEqual(app.store.skinAdsOf(u.id), { drink_mint: 29 });
    assert.deepEqual(app.store.addSkinAd(u.id, 'drink_mint'), { n: 30, need: 30, won: true });
    assert.ok(app.store.skinsOf(u.id).includes('drink_mint'));
    assert.equal(JSON.parse(app.store.get(u.id)!.equip!).drink, 'drink_mint');
    assert.deepEqual(app.store.skinAdsOf(u.id), {});
    assert.equal(app.store.skinAdCheck(u.id, 'drink_mint'), 'owned');
    assert.equal(app.store.skinAdCheck(u.id, 'dice_pharaoh'), 'bad_skin');
    assert.equal(app.store.skinAdCheck(u.id, 'nope'), 'bad_skin');
    assert.equal(app.store.skinAdCheck(u.id, 'felt_ramadan'), 'not_on_sale');
    ECONOMY.skinAdsPerDay = 30;
    assert.equal(app.store.skinAdCheck(u.id, 'drink_coffee'), 'skin_ad_limit', '30 ads already today');
    assert.equal(app.store.addSkinAd(u.id, 'drink_coffee'), null);
  } finally { ECONOMY.skinAdsPerDay = before; }
});

test('through the game: an ad for a skin counts towards it, not coins; guests are asked to sign in', async () => {
  const g = await client();
  g.send({ t: 'adStart', skin: 'drink_coffee' });
  await g.until(() => g.msgs.some(m => m.t === 'err'));
  assert.equal(g.last('err').code, 'sign_in_required');
  g.ws.close();

  const a = await signedIn('g-skinads', 'Samir');
  const coins = a.last('user').user.coins;
  a.send({ t: 'adStart', skin: 'drink_coffee' });
  await a.until(() => !!a.last('adTicket'));
  a.send({ t: 'adWatched', ticket: a.last('adTicket').ticket });
  await a.until(() => !!a.last('skinAd'));
  assert.deepEqual(a.last('skinAd'), { t: 'skinAd', skin: 'drink_coffee', n: 1, need: 30, won: false });
  await a.until(() => a.last('user')?.user.skinAds?.drink_coffee === 1);
  assert.equal(a.last('user').user.coins, coins, 'no coins for a skin ad');
  assert.equal(a.last('user').user.skinAdsLeft, ECONOMY.skinAdsPerDay - 1);
  // the same ticket can't count twice
  a.msgs.length = 0; a.send({ t: 'adWatched', ticket: 'used-or-fake' });
  await a.until(() => a.msgs.some(m => m.t === 'err'));
  a.ws.close();
});

test('the daily-reward reminder goes to signed-in players who have not collected today, with their streak day', () => {
  const now = Date.now();
  const yes = app.store.createGuest('Waiting', 0), done = app.store.createGuest('Collected', 0), guest = app.store.createGuest('Guest', 0);
  for (const u of [yes, done]) app.store.linkGoogle?.(u.id, 'sub-' + u.id, null);
  const db = (app.store as any).db;
  for (const u of [yes, done, guest]) db.prepare('UPDATE users SET seen = ?, google_sub = COALESCE(google_sub, ?) WHERE id = ?').run(now - 5 * 3600_000, u === guest ? null : 'sub-' + u.id, u.id);
  db.prepare('UPDATE users SET bonus_day = ?, streak = 4 WHERE id = ?').run(cairoDay(now - 86_400_000), yes.id);
  db.prepare('UPDATE users SET bonus_day = ? WHERE id = ?').run(cairoDay(now), done.id);
  for (const u of [yes, done, guest]) app.store.savePushToken(u.id, 'tok-' + u.id + '-xxxxxxxxxxxxxxxxxxxx', 'en', 'android');
  const t = app.store.dailyPushTargets(2 * 3600_000, now);
  const mine = t.filter(x => [yes.id, done.id, guest.id].includes(x.user_id));
  assert.deepEqual(mine.map(x => [x.user_id, x.day]), [[yes.id, 5]]);
});

test('inviting a friend who is offline: they get a notification, and the invitation when they open the game', async () => {
  const a = await signedIn('g-inviter', 'Hany');
  const b = await signedIn('g-invitee', 'Omar');
  const aId = a.welcome.user.id, bId = b.welcome.user.id;
  app.store.friendRequest(aId, bId); app.store.friendRequest(bId, aId);
  const bToken = b.last('welcome').token;
  b.send({ t: 'pushToken', token: 'fcm-token-for-omar-xxxxxxxxxxxxxx', lang: 'ar' });
  await new Promise(r => setTimeout(r, 100));
  b.ws.close();
  await a.until(() => true); await new Promise(r => setTimeout(r, 150));

  a.send({ t: 'invite', id: bId, mode: 'ffa2' });
  await a.until(() => !!a.last('invitePushed'));
  assert.equal(a.last('invitePushed').name, 'Omar');
  await a.until(() => !!a.last('table'));
  const code = a.last('table').view.code;

  // Omar opens the game: the invitation is there
  const b2 = await client(bToken);
  await b2.until(() => !!b2.last('invited'));
  assert.equal(b2.last('invited').code, code);
  assert.equal(b2.last('invited').from.name, 'Hany');
  void bToken;
  b2.ws.close();

  // a friend with no phone for notifications: still "offline"
  const c = await signedIn('g-nophone', 'Karim');
  const cId = c.welcome.user.id;
  app.store.friendRequest(aId, cId); app.store.friendRequest(cId, aId);
  c.ws.close(); await new Promise(r => setTimeout(r, 150));
  a.msgs.length = 0; a.send({ t: 'invite', id: cId, mode: 'ffa2' });
  await a.until(() => a.msgs.some(m => m.t === 'err'));
  assert.equal(a.last('err').code, 'friend_offline');
  a.ws.close();
});
