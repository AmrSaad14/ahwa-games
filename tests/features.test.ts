// The features that keep players coming back: missions, the season pass, rematch, coming back after dropping
// out, matchmaking by level, reports, referrals and the weekly tournament.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import WebSocket from 'ws';
import { createServer } from '../server/index';
import { config } from '../server/config';
import { chooseMove } from '../shared/bot';
import type { S2C, C2S, TableView, BoardView } from '../shared/protocol';
import { missionsFor, MISSIONS } from '../shared/missions';
import { cairoDay } from '../shared/gifts';
import { nextTourneyAt } from '../server/tourney';

let app: ReturnType<typeof createServer>;
let port = 0;

before(async () => {
  app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent',
    turnMs: 3000, botFillMs: 400, nextRoundMs: 30, botDelay: [5, 10], lobbyAfterMatchMs: 200, msgsPer5s: 10000, chessClockMs: 15_000, autoMoveMs: 30, graceMs: 1500,
    login: { ...config.login, sms: 'console' }, billing: { ...config.billing, testMode: true, serviceAccount: '' } } as typeof config);
  port = await app.listen();
});
after(async () => { await app.close(); });

class Client {
  ws!: WebSocket; msgs: S2C[] = []; view: TableView | null = null; board: BoardView | null = null; token = ''; uid = ''; autoplay = false; waiters: (() => void)[] = [];
  async open(token?: string, v = 2) {
    this.ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    await new Promise<void>((r, j) => { this.ws.once('open', () => r()); this.ws.once('error', j); });
    this.ws.on('message', d => {
      const m = JSON.parse(d.toString()) as S2C; this.msgs.push(m);
      if (m.t === 'welcome') { this.token = m.token; this.uid = m.user.id; }
      if (m.t === 'table') { if (m.view.game === 'domino') this.view = m.view; else this.board = m.view; if (this.autoplay) this.play(); }
      if (m.t === 'left') { this.view = null; this.board = null; }
      this.waiters.splice(0).forEach(w => w());
    });
    this.send({ t: 'hello', token, v });
    await this.until(() => !!this.uid && this.msgs.some(m => m.t === 'welcome'));
    return this;
  }
  send(m: C2S) { this.ws.send(JSON.stringify(m)); }
  async until(fn: () => boolean, ms = 20000) {
    const end = Date.now() + ms;
    while (!fn()) {
      if (Date.now() > end) throw new Error('timed out waiting\n' + this.msgs.filter(m => m.t !== 'table').map(m => JSON.stringify(m).slice(0, 160)).slice(-8).join('\n'));
      await new Promise<void>(r => { this.waiters.push(r); setTimeout(r, 50); });
    }
  }
  last<T extends S2C['t']>(t: T) { return [...this.msgs].reverse().find(m => m.t === t) as Extract<S2C, { t: T }> | undefined; }
  user() { return (this.last('user') ?? this.last('welcome'))!.user; }
  play() {
    const v = this.view; if (!v || v.phase !== 'play' || v.turn !== v.you) return;
    if (v.open !== null && v.hand.includes(v.open)) return this.send({ t: 'act', a: { t: 'play', id: v.open, e: 'C' } });
    const m = chooseMove(v.line, v.hand, v.yard);
    this.send({ t: 'act', a: m.t === 'play' ? { t: 'play', id: m.id, e: m.e } : m });
  }
  close() { this.ws.close(); }
}

test('daily reward: newer apps collect it themselves, once a day', async () => {
  const a = await new Client().open();
  assert.equal(a.last('bonus'), undefined);
  assert.equal(a.user().daily?.ready, true);
  a.send({ t: 'daily' });
  await a.until(() => a.last('bonus')?.coins === 5_000);
  await a.until(() => a.user().daily?.ready === false && a.user().coins === 105_000);
  a.send({ t: 'daily' });
  await new Promise(r => setTimeout(r, 200));
  assert.equal(a.msgs.filter(m => m.t === 'bonus').length, 1);
  // the reward also gives season points
  assert.equal(a.user().season?.sp, 20);
  a.close();
});

test('missions: three a day, games move them on, the reward is collected once', async () => {
  const a = await new Client().open();
  const ms = a.user().missions!;
  assert.equal(ms.length, 3);
  assert.deepEqual(ms.map(m => m.id), missionsFor(a.uid, cairoDay(Date.now())).map(m => m.id));
  const easy = MISSIONS.find(m => m.id === ms[0].id)!;
  // a mission can't be collected before it's done
  a.send({ t: 'mission', id: easy.id });
  await a.until(() => a.last('err')?.code === 'mission_not_done');
  // practice games on the phone count (not for "online" missions)
  const game = easy.game === 'any' ? 'domino' : easy.game;
  for (let i = 0; i < easy.need; i++) a.send({ t: 'practiceDone', game: game as 'domino', won: true });
  await a.until(() => a.last('missionDone')?.id === easy.id);
  const before = a.user().coins;
  a.send({ t: 'mission', id: easy.id });
  await a.until(() => a.last('missionClaimed')?.id === easy.id);
  await a.until(() => a.user().coins === before + easy.coins && !!a.user().missions!.find(m => m.id === easy.id)?.claimed);
  a.send({ t: 'mission', id: easy.id });
  await a.until(() => a.last('err')?.code === 'already_claimed');
  a.close();
});

test('season pass: points open tiers, free rewards collected, premium with coins', async () => {
  const a = await new Client().open();
  assert.equal(a.user().season?.tier, 0);
  a.send({ t: 'seasonClaim', tier: 1, track: 'free' });
  await a.until(() => a.last('err')?.code === 'tier_locked');
  for (let i = 0; i < 12; i++) a.send({ t: 'practiceDone', game: 'tawla', won: true });   // 12 × 10 SP (+ maybe a mission)
  await a.until(() => (a.user().season?.tier ?? 0) >= 1);
  assert.ok(a.msgs.some(m => m.t === 'seasonTier'));
  const c0 = a.user().coins;
  a.send({ t: 'seasonClaim', tier: 1, track: 'free' });
  await a.until(() => a.last('seasonClaimed')?.tier === 1);
  await a.until(() => a.user().coins === c0 + 6_000);
  a.send({ t: 'seasonClaim', tier: 1, track: 'prem' });
  await a.until(() => a.last('err')?.code === 'not_premium');
  // 750K: not enough coins yet
  a.send({ t: 'seasonBuy' });
  await a.until(() => a.last('err')?.code === 'not_enough_coins');
  a.close();
});

test('season pass skins can be worn once earned, not bought in the shop', async () => {
  const a = await new Client().open();
  a.send({ t: 'buySkin', id: 'felt_royal' });
  await a.until(() => a.last('err')?.code === 'bad_skin');
  a.send({ t: 'equipSkin', id: 'felt_royal' });
  await a.until(() => a.last('err')?.code === 'not_owned');
  a.send({ t: 'buySkin', id: 'drink_mint' });
  await a.until(() => a.user().equip.drink === 'drink_mint');
  a.close();
});

test("matchmaking: computer players get people's names; the hat and drink show at the table", async () => {
  const a = await new Client().open();
  a.send({ t: 'equipSkin', id: 'drink_tea' });
  a.send({ t: 'quick', mode: 'ffa3', stake: 5_000 });
  await a.until(() => a.view?.phase === 'play');
  const v = a.view!;
  const bots = v.seats.filter(s => s?.bot);
  assert.equal(bots.length, 2);
  for (const b of bots) assert.ok(b!.name.length > 0, 'a name');
  assert.equal(v.seats[v.you]!.hat, 'hat_classic');
  assert.equal(v.seats[v.you]!.drink, 'drink_tea');
  a.send({ t: 'leave' });
  a.close();
});

test('matchmaking: a beginner and an expert are not put together while closer players may still come', async () => {
  const a = await new Client().open(), b = await new Client().open(), c = await new Client().open();
  (app as unknown as { hub: { store: { recordResult(id: string, d: number, w: boolean, g: string): void } } }).hub;
  const store = (app.hub as unknown as { store: { recordResult(id: string, d: number | null, w: boolean, g?: string): void } }).store;
  store.recordResult(b.uid, 900, true, 'domino');   // b is far above a
  a.send({ t: 'quick', mode: 'ffa2', stake: 5_000 });
  b.send({ t: 'quick', mode: 'ffa2', stake: 5_000 });
  c.send({ t: 'quick', mode: 'ffa2', stake: 5_000 });
  await a.until(() => a.view?.phase === 'play');
  await b.until(() => b.view?.phase === 'play');
  // a and c (both new) play each other; b gets a computer player
  assert.equal(a.view!.id, (await c.until(() => !!c.view), c.view!.id));
  assert.notEqual(a.view!.id, b.view!.id);
  for (const x of [a, b, c]) { x.send({ t: 'leave' }); x.close(); }
});

test('rematch: when everyone asks, the same people play again', async () => {
  const a = await new Client().open(), b = await new Client().open();
  a.autoplay = b.autoplay = true;
  a.send({ t: 'quick', mode: 'ffa2', stake: 5_000 });
  b.send({ t: 'quick', mode: 'ffa2', stake: 5_000 });
  await a.until(() => a.view?.phase === 'mend', 60_000);
  const first = a.view!.id;
  a.send({ t: 'rematch' });
  await b.until(() => b.last('rematch')?.seats.length === 1);
  b.send({ t: 'rematch' });
  await a.until(() => !!a.view && a.view.id !== first && a.view.phase === 'play');
  await b.until(() => b.view?.id === a.view!.id);
  for (const x of [a, b]) { x.autoplay = false; x.send({ t: 'leave' }); x.close(); }
});

test('dropping out: the seat waits for you, then the computer plays; coming back takes it over again', async () => {
  const a = await new Client().open(), b = await new Client().open();
  const name = 'Watcher';
  b.send({ t: 'profile', name });
  a.send({ t: 'create', mode: 'ffa2', target: 151 });
  await a.until(() => !!a.view?.code);
  b.send({ t: 'join', code: a.view!.code! });
  await a.until(() => a.view!.seats.filter(Boolean).length === 2);
  a.send({ t: 'start' });
  await b.until(() => b.view?.phase === 'play');
  a.autoplay = true; a.play();
  const token = b.token, seat = b.view!.you;
  b.close();
  await a.until(() => !!a.view?.seats[seat]?.back);
  await a.until(() => !!a.view?.seats[seat]?.away, 10_000);
  const b2 = await new Client().open(token);
  await b2.until(() => !!b2.view);
  await a.until(() => a.view?.seats[seat]?.online === true && !a.view.seats[seat]!.away);
  a.autoplay = false;
  a.send({ t: 'leave' }); b2.send({ t: 'leave' }); a.close(); b2.close();
});

test('reports: three players reporting someone switch their chat off', async () => {
  const bad = await new Client().open();
  const others = [await new Client().open(), await new Client().open(), await new Client().open()];
  bad.send({ t: 'create', mode: 'ffa4', target: 51 });
  await bad.until(() => !!bad.view?.code);
  for (const o of others) { o.send({ t: 'join', code: bad.view!.code! }); await o.until(() => !!o.view); }
  for (const o of others) o.send({ t: 'report', seat: 0, reason: 'spam' });
  await bad.until(() => (bad.user().muted ?? 0) > Date.now());
  bad.send({ t: 'emote', k: 'hi' });
  await bad.until(() => bad.last('err')?.code === 'chat_muted');
  for (const x of [bad, ...others]) { x.send({ t: 'leave' }); x.close(); }
});

test('referral: the friend code, then both get 50K after the first game', async () => {
  const inviter = await new Client().open(), fresh = await new Client().open();
  const code = inviter.user().code!;
  assert.equal(fresh.user().canRefer, true);
  fresh.send({ t: 'referral', code: fresh.user().code! });
  await fresh.until(() => fresh.last('err')?.code === 'friend_self');
  const c0 = inviter.user().coins, f0 = fresh.user().coins;
  fresh.send({ t: 'referral', code });
  await fresh.until(() => fresh.user().referred === true);
  fresh.send({ t: 'practiceDone', game: 'ludo', won: false });
  await fresh.until(() => fresh.last('referral')?.you === 'invited');
  await inviter.until(() => inviter.last('referral')?.you === 'inviter');
  await inviter.until(() => inviter.user().coins === c0 + 50_000);
  await fresh.until(() => fresh.user().coins === f0 + 50_000);
  fresh.send({ t: 'referral', code });
  await fresh.until(() => fresh.last('err')?.code === 'already_referred');
  inviter.close(); fresh.close();
});

test('friends: one tap invites a friend to a new room', async () => {
  const a = await new Client().open(), b = await new Client().open();
  a.send({ t: 'friendAdd', code: b.user().code! });
  await b.until(() => b.last('friendNews')?.kind === 'request');
  b.send({ t: 'friendAnswer', id: a.uid, yes: true });
  await a.until(() => a.last('friends')?.rows.some(r => r.status === 'friend') === true);
  a.send({ t: 'invite', id: b.uid, mode: 'tawla' });
  await b.until(() => !!b.last('invited'));
  const inv = b.last('invited')!;
  assert.equal(inv.mode, 'tawla');
  b.send({ t: 'join', code: inv.code });
  await b.until(() => b.board?.seats.filter(Boolean).length === 2);
  for (const x of [a, b]) { x.send({ t: 'leave' }); x.close(); }
});

test('tournament: sign up, the bracket fills with computer players, matches are played to a winner', async () => {
  const hub = app.hub as unknown as { tourney: { startNow(): void; state: string; view(u: string | null): import('../shared/protocol').TourneyView } };
  assert.ok(nextTourneyAt(Date.now()) > Date.now());
  const a = await new Client().open();
  a.send({ t: 'tourney' });
  await a.until(() => a.last('tourney')?.view.state === 'open');
  a.send({ t: 'tourneyJoin' });
  await a.until(() => a.last('tourney')?.view.registered === true);
  a.autoplay = true;
  hub.tourney.startNow();
  await a.until(() => !!a.last('tourneyMatch'));
  const v = a.last('tourney')!.view;
  assert.equal(v.state, 'running');
  assert.equal(v.rounds[0].length, 4);
  // the player drops out: after the grace time the computer plays their match, and the bracket plays on
  const uid = a.uid;
  a.autoplay = false; a.close();
  for (let i = 0; i < 480 && hub.tourney.state !== 'done'; i++) await new Promise(r => setTimeout(r, 500));
  assert.equal(hub.tourney.state, 'done');
  const res = hub.tourney.view(uid);
  assert.equal(res.results[0].place, 1);
  assert.ok(res.results.length >= 4);
  assert.ok(res.rounds.every(r => r.every(m => m.winner !== null)));
});
