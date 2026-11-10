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

// basra is no longer offered by the app (its rules stay for old records): no server game to play
