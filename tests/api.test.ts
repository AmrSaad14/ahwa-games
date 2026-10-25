// The HTTP API (/api/v1) and player cards.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';
import type { S2C } from '../shared/protocol';

let app: ReturnType<typeof createServer>;
let base = '';
before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent',
    turnMs: 3000, botFillMs: 300, nextRoundMs: 30, botDelay: [5, 10], lobbyAfterMatchMs: 200, msgsPer5s: 10000, autoMoveMs: 30,
    login: { ...config.login, sms: 'console' }, billing: { ...config.billing, testMode: true, serviceAccount: '' } } as typeof config);
  base = `http://127.0.0.1:${await app.listen()}`;
});
after(async () => { await app.close(); });

async function call(method: string, path: string, token?: string, body?: unknown) {
  const r = await fetch(base + path, { method, headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) }, body: body ? JSON.stringify(body) : undefined });
  return { status: r.status, json: await r.json().catch(() => null) as Record<string, any> };
}

test('api: index, docs, openapi, health and catalog are public', async () => {
  assert.equal((await call('GET', '/api/v1')).json.version, 1);
  const spec = (await call('GET', '/api/v1/openapi.json')).json;
  assert.equal(spec.openapi, '3.0.3');
  assert.ok(Object.keys(spec.paths).length > 25);
  assert.ok((await call('GET', '/api/v1/health')).json.ok);
  assert.ok((await call('GET', '/api/v1/catalog')).json.skins.length > 30);
  const docs = await fetch(base + '/api/docs');
  assert.equal(docs.status, 200);
  assert.match(await docs.text(), /Board Battles API/);
  assert.equal((await call('GET', '/api/v1/nothing')).status, 404);
  assert.equal((await call('GET', '/api/v1/me')).status, 401);
});

test('api: a guest account, your profile, the daily reward, missions, season, shop', async () => {
  const g = await call('POST', '/api/v1/auth/guest', undefined, { name: 'Api Tester', ch: 2 });
  assert.equal(g.status, 200);
  const t = g.json.token as string;
  assert.equal(g.json.user.name, 'Api Tester');
  const me = await call('PATCH', '/api/v1/me', t, { name: 'Renamed' });
  assert.equal(me.json.name, 'Renamed');
  assert.equal((await call('GET', '/api/v1/daily', t)).json.ready, true);
  const d = await call('POST', '/api/v1/daily/claim', t);
  assert.equal(d.json.coins, 5_000);
  assert.equal((await call('POST', '/api/v1/daily/claim', t)).json.error, 'already_claimed');
  assert.equal((await call('GET', '/api/v1/missions', t)).json.missions.length, 3);
  const s = await call('GET', '/api/v1/season', t);
  assert.equal(s.json.tiers.length, 30);
  assert.equal(s.json.sp, 20);
  assert.equal((await call('POST', '/api/v1/season/claim', t, { tier: 1, track: 'free' })).json.error, 'tier_locked');
  const shop = await call('GET', '/api/v1/shop', t);
  assert.ok(shop.json.skins.find((x: { id: string }) => x.id === 'hat_fez_red'));
  const bought = await call('POST', '/api/v1/shop/buy', t, { id: 'drink_mint' });
  assert.equal(bought.json.equip.drink, 'drink_mint');
  assert.equal((await call('POST', '/api/v1/shop/buy', t, { id: 'felt_royal' })).json.error, 'bad_skin');
  assert.ok((await call('GET', '/api/v1/me/coins', t)).json.rows.length >= 3);
  assert.equal((await call('GET', '/api/v1/leaderboard?game=week')).json.game, 'week');
  assert.equal((await call('GET', '/api/v1/leaderboard?game=nope')).status, 400);
});

test('api: player cards with stats; add, accept and remove friends', async () => {
  const a = (await call('POST', '/api/v1/auth/guest', undefined, { name: 'Alpha' })).json;
  const b = (await call('POST', '/api/v1/auth/guest', undefined, { name: 'Beta' })).json;
  const bCard = (await call('GET', '/api/v1/me/card', b.token)).json;
  assert.equal(bCard.me, true);
  for (const k of ['games', 'wins', 'losses', 'winRate', 'coinsWon', 'coinsLost', 'net', 'bestWin', 'level', 'since']) assert.ok(k in bCard, k);
  const seen = (await call('GET', '/api/v1/players/' + bCard.pid, a.token)).json;
  assert.equal(seen.name, 'Beta'); assert.equal(seen.friend, null); assert.equal(seen.me, false);
  assert.equal((await call('GET', '/api/v1/players/' + bCard.pid)).json.name, 'Beta');   // public
  assert.equal((await call('POST', '/api/v1/friends', a.token, { pid: bCard.pid })).json.result, 'sent');
  const aPid = (await call('GET', '/api/v1/me/card', a.token)).json.pid;
  assert.equal((await call('GET', '/api/v1/players/' + aPid, b.token)).json.friend, 'incoming');
  assert.equal((await call('POST', `/api/v1/friends/${aPid}/accept`, b.token)).json.status, 'friend');
  assert.equal((await call('GET', '/api/v1/friends', a.token)).json.rows[0].status, 'friend');
  assert.equal((await call('DELETE', '/api/v1/friends/' + bCard.pid, a.token)).json.status, null);
  assert.equal((await call('GET', '/api/v1/friends', b.token)).json.rows.length, 0);
});

test('api: tournament sign-up, referral code, report and support', async () => {
  const a = (await call('POST', '/api/v1/auth/guest')).json, b = (await call('POST', '/api/v1/auth/guest')).json;
  assert.equal((await call('POST', '/api/v1/tournament/join', a.token)).json.registered, true);
  assert.equal((await call('GET', '/api/v1/tournament', a.token)).json.registered, true);
  assert.equal((await call('DELETE', '/api/v1/tournament/join', a.token)).json.registered, false);
  assert.equal((await call('POST', '/api/v1/referral', b.token, { code: a.user.code })).json.referred, true);
  assert.equal((await call('POST', '/api/v1/referral', b.token, { code: a.user.code })).json.error, 'already_referred');
  const bPid = (await call('GET', '/api/v1/me/card', b.token)).json.pid;
  assert.equal((await call('POST', '/api/v1/report', a.token, { pid: bPid, reason: 'spam' })).json.reported, true);
  assert.ok((await call('POST', '/api/v1/support', undefined, { text: 'hello from the api' })).json.id > 0);
});

test('cards in the game: tap a seat, add the friend from the card, the card shows it', async () => {
  const open = async () => {
    const ws = new WebSocket(base.replace('http', 'ws') + '/ws');
    const msgs: S2C[] = [];
    await new Promise(r => ws.once('open', r));
    ws.on('message', d => msgs.push(JSON.parse(d.toString())));
    ws.send(JSON.stringify({ t: 'hello', v: 2 }));
    const until = async (f: () => boolean) => { for (let i = 0; i < 200 && !f(); i++) await new Promise(r => setTimeout(r, 25)); assert.ok(f()); };
    const last = <T extends S2C['t']>(t: T) => [...msgs].reverse().find(m => m.t === t) as Extract<S2C, { t: T }> | undefined;
    await until(() => !!last('welcome'));
    return { ws, msgs, until, last, send: (m: unknown) => ws.send(JSON.stringify(m)) };
  };
  const a = await open(), b = await open();
  a.send({ t: 'create', mode: 'ffa2', target: 51 });
  await a.until(() => !!a.last('table')?.view.code);
  b.send({ t: 'join', code: a.last('table')!.view.code });
  await b.until(() => (b.last('table')?.view.seats.filter(Boolean).length ?? 0) === 2);
  const other = 1 - b.last('table')!.view.you;
  b.send({ t: 'profileOf', seat: other });
  await b.until(() => !!b.last('profileCard'));
  const card = b.last('profileCard')!.card;
  assert.equal(card.friend, null);
  b.send({ t: 'friendAdd', pid: card.pid });
  await a.until(() => a.last('friendNews')?.kind === 'request');
  b.send({ t: 'profileOf', pid: card.pid });
  await b.until(() => b.msgs.filter(m => m.t === 'profileCard').length === 2 && b.last('profileCard')!.card.friend === 'outgoing');
  b.send({ t: 'friendRemove', pid: card.pid });
  await b.until(() => b.msgs.filter(m => m.t === 'profileCard').length === 3 && b.last('profileCard')!.card.friend === null);
  a.ws.close(); b.ws.close();
});

test('sign in with Apple: a ticket links the Apple id; the same Apple id opens that account later', async () => {
  const open = async (token?: string) => {
    const ws = new WebSocket(base.replace('http', 'ws') + '/ws');
    const msgs: S2C[] = [];
    await new Promise(r => ws.once('open', r));
    ws.on('message', d => msgs.push(JSON.parse(d.toString())));
    ws.send(JSON.stringify({ t: 'hello', v: 2, token }));
    const until = async (f: () => boolean) => { for (let i = 0; i < 200 && !f(); i++) await new Promise(r => setTimeout(r, 25)); assert.ok(f()); };
    const last = <T extends S2C['t']>(t: T) => [...msgs].reverse().find(m => m.t === t) as Extract<S2C, { t: T }> | undefined;
    await until(() => !!last('welcome'));
    return { ws, msgs, until, last, send: (m: unknown) => ws.send(JSON.stringify(m)) };
  };
  const a = await open();
  const uid = a.last('welcome')!.user.id;
  a.send({ t: 'ticket', ticket: app.apple.testTicket({ sub: 'apple-123', email: 'x@privaterelay.appleid.com', name: 'Laila' }) });
  await a.until(() => a.last('signedIn')?.via === 'apple');
  await a.until(() => a.last('user')?.user.apple === 'x@privaterelay.appleid.com' && a.last('user')!.user.name === 'Laila');
  a.ws.close();
  const b = await open();
  b.send({ t: 'ticket', ticket: app.apple.testTicket({ sub: 'apple-123', email: null, name: null }) });
  await b.until(() => b.last('signedIn')?.fresh === false);
  await b.until(() => b.msgs.filter(m => m.t === 'welcome').length === 2 && b.last('welcome')!.user.id === uid);
  const r = await fetch(base + '/auth/apple/start');
  assert.equal(r.status, 404);   // not set up in tests
  b.ws.close();
});
