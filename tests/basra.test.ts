import { test } from 'node:test';
import assert from 'node:assert/strict';
import { newBasra, play, takeFor, botCard, cardPoints, rankOf, SEVEN_D, TWO_C, TEN_D, BasraState } from '../shared/games/basra';

const c = (rank: number, suit = 0) => suit * 13 + rank - 1;   // ♠ ♥ ♦ ♣ = 0..3

test('basra: taking by rank and by sums, queens and kings only by rank', () => {
  assert.deepEqual(takeFor(c(7), [c(7, 1), c(3), c(4, 1), c(9)]).take.sort(), [c(7, 1), c(3), c(4, 1)].sort());
  // a 9 takes 5+4 and 6+3 and another 9 at once
  const f = [c(5), c(4, 1), c(6, 2), c(3, 3), c(9, 1), c(12)];
  assert.equal(takeFor(c(9), f).take.length, 5);
  assert.deepEqual(takeFor(c(12, 2), [c(12), c(5)]).take, [c(12)]);
  assert.deepEqual(takeFor(c(13), [c(12), c(5)]).take, []);
});

test('basra: clearing the floor, the jack and the 7♦', () => {
  assert.equal(takeFor(c(8), [c(5), c(3, 1)]).basra, 10);
  assert.equal(takeFor(c(11), [c(5), c(3, 1)]).basra, 0, 'the jack takes all but no basra');
  assert.equal(takeFor(c(11), [c(5), c(3, 1)]).take.length, 2);
  assert.equal(takeFor(c(11), [c(11, 2)]).basra, 20, 'jack on a lone jack');
  assert.equal(takeFor(SEVEN_D, [c(5), c(3, 1)]).basra, 10, '7♦ on a small floor');
  assert.equal(takeFor(SEVEN_D, [c(5), c(9, 1)]).basra, 0, 'more than 10');
  assert.equal(takeFor(SEVEN_D, [c(5), c(12)]).basra, 0, 'a picture card');
  assert.equal(cardPoints([c(1), c(11), TWO_C, TEN_D]), 1 + 1 + 2 + 3);
  assert.equal(rankOf(SEVEN_D), 7);
});

test('basra: a whole game between computer players ends with a winner over 101, 52 cards each deal', () => {
  let seed = 7; const rnd = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  for (const players of [2, 4]) {
    let s: BasraState = newBasra(players, 0, rnd);
    assert.equal(s.floor.length, 4);
    assert.ok(s.floor.every(x => rankOf(x) !== 11 && x !== SEVEN_D), 'no jack or 7♦ on the first floor');
    let moves = 0, deals = 0;
    while (!s.done && moves < 5000) {
      const seat = s.turn;
      const r = play(s, seat, botCard(s, seat, 'normal', rnd), rnd);
      assert.equal(typeof r, 'object');
      if (typeof r === 'string') break;
      if (r.dealOver) { deals++; const L = r.s.lastDeal!; assert.equal(L.cards.reduce((a, b) => a + b, 0), 52); }
      s = r.s; moves++;
    }
    assert.ok(s.done, 'finished');
    assert.ok(Math.max(...s.scores) >= 101);
    assert.ok(deals >= 1);
    assert.equal(s.scores.length, players === 4 ? 2 : 2);
  }
});

test('basra: playing out of turn or a card you don\'t hold is refused', () => {
  const s = newBasra(2, 0, Math.random);
  assert.equal(play(s, 1, s.hands[1][0], Math.random), 'not_your_turn');
  assert.equal(play(s, 0, s.hands[1][0], Math.random), 'not_in_hand');
});

test('basra on the server: a practice table plays to the end with computer players', async () => {
  const { createServer } = await import('../server/index');
  const { config } = await import('../server/config');
  const WebSocket = (await import('ws')).default;
  const app = createServer({ ...config, port: 0, host: '127.0.0.1', dbPath: ':memory:', staticDir: '/nonexistent', botFillMs: 300, msgsPer5s: 10000, turnMs: 400, botDelay: [5, 10], nextRoundMs: 50,
    guestsOnline: true, login: { ...config.login, sms: 'console' } } as typeof config);
  const port = await app.listen();
  try {
    const ws = new WebSocket(`ws://127.0.0.1:${port}/ws`);
    const msgs: any[] = [];
    ws.on('message', d => msgs.push(JSON.parse(d.toString())));
    await new Promise<void>(r => ws.once('open', () => r()));
    ws.send(JSON.stringify({ t: 'hello' }));
    const until = async (fn: () => boolean, ms = 60_000) => { const end = Date.now() + ms; while (!fn()) { if (Date.now() > end) throw new Error('timed out ' + JSON.stringify(msgs.slice(-2)).slice(0, 400)); await new Promise(r => setTimeout(r, 20)); } };
    await until(() => msgs.some(m => m.t === 'welcome'));
    ws.send(JSON.stringify({ t: 'practice', mode: 'basra4' }));
    const last = () => [...msgs].reverse().find(m => m.t === 'table')?.view;
    await until(() => last()?.phase === 'play');
    const v = last();
    assert.ok(v.basra.hand.length >= 3 && v.basra.hand.length <= 4, 'four cards each (one may be played already)');
    assert.equal(v.basra.counts.length, 4);
    assert.equal(v.basra.counts[v.you], v.basra.hand.length);
    assert.deepEqual(v.seats.map((x: any) => x.team), [0, 1, 0, 1], 'partners opposite');
    // play my cards when it's my turn until the match ends
    await until(() => {
      const w = last();
      if (w?.phase === 'play' && w.basra.turn === w.you && w.basra.hand.length) ws.send(JSON.stringify({ t: 'act', a: { t: 'card', id: w.basra.hand[0] } }));
      return w?.phase === 'mend';
    }, 180_000);
    const end = last();
    assert.ok(end.match.team === 0 || end.match.team === 1);
    ws.close();
  } finally { await app.close(); }
});
