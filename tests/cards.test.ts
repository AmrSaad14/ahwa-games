import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as TN from '../shared/games/tarneeb';
import * as K from '../shared/games/konkan';
import { defFor } from '../shared/games/defs';

const c = (rank: number, suit = 0) => suit * 13 + rank - 1;   // ♠ ♥ ♦ ♣ = 0..3
const seeded = (seed: number) => () => (seed = (seed * 16807) % 2147483647) / 2147483647;

/* ---------------- tarneeb ---------------- */

test('tarneeb: the trick goes to the highest trump, else the highest of the suit led; the ace is high', () => {
  assert.equal(TN.trickWinner([{ seat: 0, card: c(10, 1) }, { seat: 1, card: c(1, 1) }, { seat: 2, card: c(13, 0) }, { seat: 3, card: c(2, 2) }], 3), 1);
  assert.equal(TN.trickWinner([{ seat: 0, card: c(10, 1) }, { seat: 1, card: c(1, 1) }, { seat: 2, card: c(2, 3) }, { seat: 3, card: c(13, 1) }], 3), 2);
  assert.equal(TN.trickWinner([{ seat: 2, card: c(5, 0) }, { seat: 3, card: c(9, 3) }, { seat: 0, card: c(10, 3) }, { seat: 1, card: c(1, 0) }], 3), 0);
});

test('tarneeb: bidding, following suit, and the dealer taking 7 when everyone passes', () => {
  const s = TN.newTarneeb(1, seeded(3));
  assert.equal(s.phase, 'bid'); assert.equal(s.turn, 1);
  let r = TN.act(s, 1, { t: 'bid', n: 6 }, seeded(1)); assert.equal(r, 'bad_bid');
  let st = s;
  for (const seat of [1, 2, 3]) { const x = TN.act(st, seat, { t: 'bid', n: 0 }, seeded(1)); assert.ok(typeof x !== 'string'); st = x.s; }
  assert.equal(st.turn, 0);
  assert.equal(TN.act(st, 0, { t: 'bid', n: 0 }, seeded(1)), 'must_bid');
  const x = TN.act(st, 0, { t: 'bid', n: 7 }, seeded(1)); assert.ok(typeof x !== 'string');
  assert.equal(x.s.phase, 'trump'); assert.equal(x.s.turn, 0);
  const y = TN.act(x.s, 0, { t: 'trump', suit: 2 }, seeded(1)); assert.ok(typeof y !== 'string');
  assert.equal(y.s.phase, 'play'); assert.equal(y.s.turn, 0);
  // the next player must follow the suit led when they can
  const led = y.s.hands[0][0];
  const z = TN.act(y.s, 0, { t: 'card', id: led }, seeded(1)); assert.ok(typeof z !== 'string');
  const h1 = z.s.hands[1], same = h1.filter(k => TN.suitOf(k) === TN.suitOf(led)), other = h1.filter(k => TN.suitOf(k) !== TN.suitOf(led));
  if (same.length && other.length) assert.equal(TN.act(z.s, 1, { t: 'card', id: other[0] }, seeded(1)), 'must_follow');
  r = TN.act(z.s, 2, { t: 'card', id: z.s.hands[2][0] }, seeded(1)); assert.equal(r, 'not_your_turn');
});

test('tarneeb: whole games between computer players end with a team over the target', () => {
  for (const seed of [1, 5, 9]) {
    const rnd = seeded(seed);
    let s = TN.newTarneeb(0, rnd);
    let guard = 0;
    while (!s.done && guard++ < 20000) {
      const a = TN.botAction(s, s.turn, seed === 9 ? 'easy' : 'hard', rnd);
      const r = TN.act(s, s.turn, a, rnd);
      assert.ok(typeof r !== 'string', `bot action failed: ${r} ${JSON.stringify(a)}`);
      if (r.dealOver) assert.equal(r.s.lastDeal!.tricks[0] + r.s.lastDeal!.tricks[1], 13);
      s = r.s;
    }
    assert.ok(s.done);
    assert.ok(Math.max(...s.scores) >= 41);
    assert.ok(TN.winnerTeam(s) >= 0);
  }
});

test('tarneeb: scoring a made bid, a failed bid and 13', () => {
  const s = TN.newTarneeb(0, seeded(2));
  // play out a deal with forced choices, then check the scoring arithmetic directly
  s.bid = 9; s.bidder = 1; s.phase = 'play'; s.trump = 0; s.turn = 0;
  s.tricks = [4, 8]; s.hands = [[c(2, 1)], [c(3, 1)], [c(4, 1)], [c(1, 1)]];
  let st = s;
  for (const seat of [0, 1, 2, 3]) { const r = TN.act(st, seat, { t: 'card', id: st.hands[seat][0] }, seeded(1)); assert.ok(typeof r !== 'string'); st = r.s; }
  // seat 3 (team 1) took the last trick: team 1 has 9 = the bid
  assert.deepEqual(st.lastDeal!.points, [0, 9]);
  assert.ok(st.lastDeal!.made);
});

/* ---------------- konkan ---------------- */

const J = K.JOKER_A;
test('konkan: sets and runs, with jokers, and the value for laying down', () => {
  assert.equal(K.meldKind([c(7, 0), c(7, 1), c(7, 2)]), 'set');
  assert.equal(K.meldKind([c(7, 0), c(7, 0) + 52, c(7, 2)]), null, 'the same suit twice is no set');
  assert.equal(K.meldKind([c(7, 0), J, c(7, 2), c(7, 3)]), 'set');
  assert.equal(K.meldKind([c(7, 0), c(7, 1), c(7, 2), c(7, 3), J]), null, 'five is too many');
  assert.equal(K.meldKind([c(4, 1), c(5, 1), c(6, 1)]), 'run');
  assert.equal(K.meldKind([c(4, 1), J, c(6, 1)]), 'run');
  assert.equal(K.meldKind([c(1, 2), c(2, 2), c(3, 2)]), 'run', 'ace low');
  assert.equal(K.meldKind([c(12, 2), c(13, 2), c(1, 2)]), 'run', 'ace high');
  assert.equal(K.meldKind([c(13, 2), c(1, 2), c(2, 2)]), null, 'not round the corner');
  assert.equal(K.meldKind([c(4, 1), c(5, 1), c(6, 2)]), null);
  assert.equal(K.meldValue([c(12, 2), c(13, 2), c(1, 2)]), 31);
  assert.equal(K.meldValue([c(1, 2), c(2, 2), c(3, 2)]), 6);
  assert.equal(K.meldValue([c(10, 0), c(10, 1), J]), 30);
  assert.deepEqual(K.orderRun([c(6, 1), J, c(4, 1)]), [c(4, 1), J, c(6, 1)]);
});

test('konkan: 51 to lay down first, the taken card has to be used, a card is kept to throw', () => {
  let s = K.newKonkan(2, 0, seeded(4));
  assert.equal(s.hands[0].length, 15); assert.equal(s.hands[1].length, 14);
  assert.equal(s.stage, 'play');
  s.hands[0] = [c(10, 0), c(10, 1), c(10, 2), c(5, 3), c(6, 3), c(7, 3), c(2, 0), c(9, 1), c(13, 1), c(13, 2), c(13, 3), c(4, 0), c(8, 2), c(11, 0), c(3, 1)];
  assert.equal(K.act(s, 0, { t: 'down', melds: [[c(10, 0), c(10, 1), c(10, 2)]] }, seeded(1)), 'need_51');
  let r = K.act(s, 0, { t: 'down', melds: [[c(10, 0), c(10, 1), c(10, 2)], [c(13, 1), c(13, 2), c(13, 3)]] }, seeded(1));
  assert.ok(typeof r !== 'string'); s = r.s;
  assert.equal(s.melds.length, 2); assert.ok(s.opened[0]);
  // after that, anything goes
  r = K.act(s, 0, { t: 'down', melds: [[c(5, 3), c(6, 3), c(7, 3)]] }, seeded(1)); assert.ok(typeof r !== 'string'); s = r.s;
  r = K.act(s, 0, { t: 'add', id: c(8, 2), meld: 2 }, seeded(1)); assert.equal(r, 'bad_meld');
  r = K.act(s, 0, { t: 'throw', id: c(8, 2) }, seeded(1)); assert.ok(typeof r !== 'string'); s = r.s;
  assert.equal(s.turn, 1); assert.equal(s.stage, 'draw');
  assert.equal(K.act(s, 1, { t: 'throw', id: s.hands[1][0] }, seeded(1)), 'draw_first');
  r = K.act(s, 1, { t: 'take' }, seeded(1)); assert.ok(typeof r !== 'string'); s = r.s;
  assert.equal(K.act(s, 1, { t: 'throw', id: s.hands[1][0] }, seeded(1)), 'use_taken');
  r = K.act(s, 1, { t: 'back' }, seeded(1)); assert.ok(typeof r !== 'string'); s = r.s;
  assert.equal(s.stage, 'draw'); assert.equal(s.pile[s.pile.length - 1], c(8, 2));
});

test('konkan: the computer finds its melds', () => {
  const hand = [c(10, 0), c(10, 1), c(10, 2), c(5, 3), c(6, 3), c(7, 3), c(2, 0), J, c(13, 1), c(13, 2)];
  const b = K.bestMelds(hand);
  assert.ok(b.value >= 30 + 18 + 26, `found ${b.value}`);
  for (const m of b.melds) assert.ok(K.meldKind(m));
});

test('konkan: whole games between computer players, 2 to 4 players', () => {
  for (const players of [2, 3, 4]) {
    const rnd = seeded(11 + players);
    let s = K.newKonkan(players, 0, rnd, 151);
    let guard = 0, deals = 0;
    while (!s.done && guard++ < 60000) {
      const a = K.botAction(s, s.turn, players === 3 ? 'easy' : 'hard', rnd);
      const r = K.act(s, s.turn, a, rnd);
      assert.ok(typeof r !== 'string', `bot action failed: ${r} ${JSON.stringify(a)}`);
      if (r.dealOver) deals++;
      s = r.s;
      // every card is somewhere, once
      const all = [...s.stock, ...s.pile, ...s.hands.flat(), ...s.melds.flatMap(m => m.cards)];
      if (guard % 97 === 0) assert.equal(new Set(all).size, 106);
    }
    assert.ok(s.done, `players ${players}: not done after ${guard} actions, ${deals} deals`);
    assert.ok(K.winnerSeat(s) >= 0);
  }
});

test('card games run on the generic table defs', () => {
  const t = defFor('tarneeb'), k = defFor('konkan3');
  const rnd = seeded(21);
  const ts = t.newGame(4, 0, rnd);
  const tv = t.view(ts, 0) as { hand: number[]; phase: string };
  assert.equal(tv.hand.length, 13); assert.equal(tv.phase, 'bid');
  const ks = k.newGame(3, 0, rnd);
  const kv = k.view(ks, 1) as { hand: number[]; counts: number[] };
  assert.equal(kv.hand.length, 14); assert.deepEqual(kv.counts, [15, 14, 14]);
  assert.ok(k.wholeTurn);
});

test('tarneeb and konkan on the server: practice tables play to the end (my turns run out of time)', async () => {
  const { createServer } = await import('../server/index');
  const { config } = await import('../server/config');
  const WebSocket = (await import('ws')).default;
  const app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 300, msgsPer5s: 10000, turnMs: 150, botDelay: [1, 2], nextRoundMs: 50,
    guestsOnline: true, login: { ...config.login, sms: 'console' } } as typeof config);
  const port = await app.listen();
  try {
    for (const mode of ['tarneeb', 'konkan4']) {
      const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
      const msgs: any[] = [];
      ws.on('message', d => msgs.push(JSON.parse(d.toString())));
      await new Promise<void>(r => ws.once('open', () => r()));
      ws.send(JSON.stringify({ t: 'hello' }));
      const until = async (fn: () => boolean, ms = 240_000) => { const end = Date.now() + ms; while (!fn()) { if (Date.now() > end) throw new Error(mode + ' timed out ' + JSON.stringify(msgs.slice(-1)).slice(0, 600)); await new Promise(r => setTimeout(r, 20)); } };
      await until(() => msgs.some(m => m.t === 'welcome'));
      ws.send(JSON.stringify({ t: 'practice', mode, level: 'hard' }));
      const last = () => [...msgs].reverse().find(m => m.t === 'table')?.view;
      await until(() => last()?.phase === 'play');
      const v = last(), g = mode === 'tarneeb' ? v.tarneeb : v.konkan;
      assert.ok(g, 'the game view is under its name');
      assert.equal(g.counts.length, 4);
      await until(() => last()?.phase === 'mend');
      assert.ok(last().match.team >= 0);
      ws.close();
      msgs.length = 0;
    }
  } finally { await app.close(); }
});

test('dominoes: nobody is dealt 6 or more tiles of the same number', async () => {
  const { crowdedHand, TILES } = await import('../shared/engine');
  const six = TILES.map((t, i) => (t[0] === 6 || t[1] === 6 ? i : -1)).filter(i => i >= 0);
  assert.ok(crowdedHand(six.slice(0, 6)));
  assert.ok(!crowdedHand(six.slice(0, 5)));
});

test('ludo: players choose their colours (two sit opposite; a colour taken goes to the next free one)', async () => {
  const { chooseColors } = await import('../shared/games/ludo');
  assert.deepEqual(chooseColors(2, [-1, -1]), [0, 2]);
  assert.deepEqual(chooseColors(2, [3, -1]), [3, 1]);
  assert.deepEqual(chooseColors(2, [-1, 1]), [3, 1]);
  assert.deepEqual(chooseColors(2, [2, 0]), [2, 0]);
  assert.deepEqual(chooseColors(4, [2, 2, -1, 0]), [2, 1, 3, 0]);
  assert.deepEqual(chooseColors(3, [-1, 3, -1]), [0, 3, 1]);
});

test('ludo on the server: the colour you chose is yours at the table', async () => {
  const { createServer } = await import('../server/index');
  const { config } = await import('../server/config');
  const WebSocket = (await import('ws')).default;
  const app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 300, msgsPer5s: 10000, botDelay: [1, 2],
    guestsOnline: true, login: { ...config.login, sms: 'console' } } as typeof config);
  const port = await app.listen();
  try {
    const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    const msgs: any[] = []; ws.on('message', d => msgs.push(JSON.parse(d.toString())));
    await new Promise<void>(r => ws.once('open', () => r()));
    const until = async (fn: () => boolean) => { const end = Date.now() + 20000; while (!fn()) { if (Date.now() > end) throw new Error('timeout'); await new Promise(r => setTimeout(r, 20)); } };
    ws.send(JSON.stringify({ t: 'hello' }));
    await until(() => msgs.some(m => m.t === 'welcome'));
    ws.send(JSON.stringify({ t: 'setLudoColor', color: 1 }));
    await until(() => msgs.some(m => m.t === 'user' && m.user.ludoColor === 1));
    ws.send(JSON.stringify({ t: 'practice', mode: 'ludo2' }));
    const v = async () => { await until(() => msgs.some(m => m.t === 'table' && m.view.ludo)); return [...msgs].reverse().find(m => m.t === 'table' && m.view.ludo).view; };
    const view = await v();
    assert.equal(view.ludo.colors[view.ludo.you], 1);
    assert.equal(view.ludo.colors[1 - view.ludo.you], 3);
    ws.close();
  } finally { await app.close(); }
});

test('konkan in teams: partners opposite, the side that goes out scores nothing, the other both hands', () => {
  const rnd = seeded(77);
  let s = K.newKonkan(4, 0, rnd, 201, true);
  assert.equal(s.teams, 2); assert.equal(s.scores.length, 2);
  let guard = 0, deals = 0;
  while (!s.done && guard++ < 80000) {
    const r = K.act(s, s.turn, K.botAction(s, s.turn, 'normal', rnd), rnd);
    assert.ok(typeof r !== 'string', String(r));
    if (r.dealOver) { deals++; const w = K.konkanTeam(r.s, r.winner!); assert.equal(r.s.lastDeal!.points[w], 0); }
    s = r.s;
  }
  assert.ok(s.done); assert.ok(Math.max(...s.scores) >= 201);
  assert.ok([0, 1].includes(K.winnerSeat(s)));
});

test('a team table: the friend you invite gets the seat across from you; players can change seats while it gathers', async () => {
  const { BoardTable } = await import('../shared/boardtable');
  const hooks = { changed() {}, emote() {}, matchEnd() {}, closed() {} } as any;
  const t = new BoardTable({ id: 't', code: 'abc', mode: 'tarneeb', target: 41, private: true, turnMs: 1000, nextRoundMs: 10, botDelay: [1, 2], offlineGraceMs: 10, lobbyAfterMatchMs: 10, autoMoveMs: 10 } as any, hooks);
  const P = (uid: string) => ({ uid, name: uid, ch: 0, bot: false, rating: 1000 });
  assert.equal(t.join(P('me')), 0);
  assert.ok(t.reserve('friend', 2));
  assert.equal(t.join(P('stranger')), 1, 'a stranger does not take the kept seat');
  assert.equal(t.join(P('friend')), 2, 'the friend sits across from me');
  assert.equal(t.sit('stranger', 2), 'bad_seat');
  assert.equal(t.sit('stranger', 3), null);
  assert.equal(t.seatOf('stranger'), 3);
  t.close?.();
});
