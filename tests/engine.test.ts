import { test } from 'node:test';
import assert from 'node:assert/strict';
import { GAME_MODES, TILES, endsFor, leftVal, lineTiles, newLine, place, rightVal, shuffled, Line, pipSum } from '../shared/engine';
import { footprint, layoutLine } from '../shared/layout';
import { Table, TableOptions } from '../shared/table';
import { chooseMove } from '../shared/bot';

test('tile set has 28 unique tiles', () => {
  assert.equal(TILES.length, 28);
  assert.equal(new Set(TILES.map(t => t.join())).size, 28);
});

test('placing keeps open ends correct', () => {
  const l = newLine();
  const id = (a: number, b: number) => TILES.findIndex(t => t[0] === Math.min(a, b) && t[1] === Math.max(a, b));
  place(l, id(3, 5), 'C');
  assert.equal(leftVal(l), 3); assert.equal(rightVal(l), 5);
  place(l, id(5, 6), 'R'); assert.equal(rightVal(l), 6);
  place(l, id(1, 3), 'L'); assert.equal(leftVal(l), 1);
  assert.deepEqual(endsFor(l, id(1, 6)).sort(), ['L', 'R']);
  assert.throws(() => place(l, id(2, 2), 'L'));
});

function randomLine(): Line {
  const deck = shuffled(TILES.map((_, i) => i));
  const l = newLine(); place(l, deck.pop()!, 'C');
  let moved = true;
  while (moved) {
    moved = false;
    for (let i = 0; i < deck.length; i++) {
      const e = endsFor(l, deck[i]);
      if (e.length) { place(l, deck[i], e[Math.floor(Math.random() * e.length)]); deck.splice(i, 1); moved = true; break; }
    }
  }
  return l;
}

test('layout never overlaps and every tile touches its neighbour on the matching number', () => {
  for (let trial = 0; trial < 400; trial++) {
    const l = randomLine();
    for (const xMax of [5.3, 8.7]) {
      const pl = layoutLine(l, xMax);
      assert.equal(pl.length, lineTiles(l).length);
      for (let i = 0; i < pl.length; i++) for (let j = i + 1; j < pl.length; j++) {
        const A = footprint(pl[i]), B = footprint(pl[j]);
        const ox = Math.min(A.x1, B.x1) - Math.max(A.x0, B.x0), oy = Math.min(A.y1, B.y1) - Math.max(A.y0, B.y0);
        assert.ok(!(ox > 1e-6 && oy > 1e-6), `overlap in trial ${trial}`);
      }
      // the tail (value a) of each arm tile must sit right next to the previous tile's head
      const byId = new Map(pl.map(p => [p.id, p]));
      for (const arm of [l.R, l.L]) {
        let prev = byId.get(l.f!)!;
        for (const [id, inner] of arm) {
          const cur = byId.get(id)!;
          assert.equal(cur.a, inner);
          const dbl = cur.a === cur.b;
          const crossDouble = dbl && Math.abs(Math.abs(cur.ang - Math.atan2(0, 1)) % Math.PI - Math.PI / 2) < 1e-6 && Math.abs(Math.cos(cur.ang)) < 0.5;
          // tail point of the current tile (centre of its first half, or centre for a crosswise double)
          const tail = crossDouble ? { x: cur.x, y: cur.y } : { x: cur.x - Math.cos(cur.ang) * 0.5, y: cur.y - Math.sin(cur.ang) * 0.5 };
          const d = Math.hypot(tail.x - prev.x, tail.y - prev.y);
          assert.ok(d < 1.6, `tile ${id} is detached (${d.toFixed(2)})`);
          prev = cur;
        }
      }
    }
  }
});

const fast: Omit<TableOptions, 'id' | 'mode' | 'private' | 'code'> = {
  target: 101, turnMs: 50, nextRoundMs: 5, botDelay: [1, 2], offlineGraceMs: 5, lobbyAfterMatchMs: 5, autoMoveMs: 5,
};

for (const mode of GAME_MODES.domino) {
  test(`a full ${mode} match between computer players finishes with valid scores`, async () => {
    let ended = false, invariantErrors = 0;
    await new Promise<void>((resolve, reject) => {
      const t = new Table({ ...fast, id: 't', code: null, mode, private: false, target: mode === 'team' ? 151 : 101 }, {
        changed(tb) {
          if (tb.phase === 'play') {
            const total = tb.hands.flat().length + tb.yard.length + lineTiles(tb.line).length;
            if (total !== (mode === 'ffa3' ? 27 : 28)) invariantErrors++;   // three players: the 0|0 is out
          }
        },
        emote() {},
        matchEnd(tb, res) {
          ended = true;
          assert.ok(Math.max(...res.scores) >= tb.target);
          assert.equal(res.scores.indexOf(Math.max(...res.scores)), res.team);
          tb.close();
        },
        closed() { resolve(); },
      });
      for (let i = 0; i < t.n; i++) t.addBot(t.hostUid ?? 'nobody', i);
      // no human host: bots are added directly
      if (t.seats.some(s => !s)) { t.hostUid = 'h'; for (let i = 0; i < t.n; i++) t.addBot('h', i); }
      const err = t.start('h'); if (err) reject(new Error(err));
      setTimeout(() => reject(new Error('timeout')), 20000).unref();
    });
    assert.ok(ended);
    assert.equal(invariantErrors, 0);
  });
}

test('a player who keeps timing out still gets the full turn time every turn', async () => {
  await new Promise<void>((resolve, reject) => {
    let sawAway = false, autoMoves = 0, earliest = Infinity, turnStarted = 0;
    const t = new Table({ ...fast, turnMs: 80, id: 'x', code: 'abcde', mode: 'ffa2', private: true }, {
      changed(tb) {
        if (tb.seats[0]?.away) sawAway = true;
        if (tb.phase === 'play' && tb.turn === 0 && tb.turnEndsAt && !turnStarted) turnStarted = Date.now();
        if (tb.last?.auto && tb.last.s === 0 && turnStarted) { autoMoves++; earliest = Math.min(earliest, Date.now() - turnStarted); turnStarted = 0; }
      },
      emote() {},
      matchEnd(tb) {
        try { assert.ok(!sawAway, 'never marked away'); assert.ok(autoMoves >= 5); assert.ok(earliest >= 70, `auto move came after ${earliest}ms`); } catch (e) { reject(e); }
        tb.close();
      },
      closed() { resolve(); },
    });
    t.join({ uid: 'u1', name: 'Amr', ch: 0, bot: false, rating: 1000 });
    assert.equal(t.addBot('u1', 1), null);
    assert.equal(t.start('u1'), null);
    setTimeout(() => reject(new Error('timeout')), 30000).unref();
  });
});

test('when a turn times out, a move is made automatically and flagged', async () => {
  await new Promise<void>((resolve, reject) => {
    let done = false;
    const t = new Table({ ...fast, turnMs: 40, botDelay: [60_000, 60_000], id: 'to', code: 'z', mode: 'ffa2', private: true }, {
      changed(tb) {
        if (done || !tb.last || tb.last.s < 0) return;
        if (tb.last.t !== 'deal' && tb.last.t !== 'round' && tb.last.t !== 'match') {
          done = true;
          try { assert.equal(tb.last.auto, true); assert.ok(['play', 'draw', 'pass'].includes(tb.last.t)); resolve(); } catch (e) { reject(e); }
          tb.close();
        }
      },
      emote() {}, matchEnd() {}, closed() {},
    });
    t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
    t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
    t.start('a');
    setTimeout(() => reject(new Error('no automatic move')), 3000).unref();
  });
});

test('with no tile that fits, the game draws and then passes for the player by itself', async () => {
  const t = new Table({ ...fast, turnMs: 60_000, botDelay: [60_000, 60_000], autoMoveMs: 300, id: 'np', code: 'n', mode: 'ffa2', private: true },
    { changed() {}, emote() {}, matchEnd() {}, closed() {} });
  t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
  t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
  t.start('a');
  const id = (a: number, b: number) => TILES.findIndex(x => x[0] === a && x[1] === b);
  // line shows 6 at both ends; the player to move holds no 6 and the boneyard has two tiles without a 6
  t.line = newLine(); place(t.line, id(6, 6), 'C');
  const me = t.turn, other = 1 - me;
  t.hands[me] = [id(0, 1), id(1, 2)];
  t.hands[other] = [id(2, 3), id(3, 4)];
  t.yard = [id(0, 0), id(1, 1)];
  const uid = me === 0 ? 'a' : 'b';
  try {
    t.refresh();
    // two draws (~0.2 s each) then a pass (0.3 s); the other player's own automatic move would follow 0.3 s later
    await new Promise(r => setTimeout(r, 850));
    assert.equal(t.yard.length, 0, 'drew the whole boneyard');
    assert.equal(t.hands[me].length, 4);
    assert.equal(t.turn, other, 'passed the turn after drawing');
    assert.equal(t.last?.t, 'pass');
    assert.equal(t.last?.forced, true);
    assert.equal(t.seats[t.seatOf(uid)]!.timeouts, 0, 'not counted as a missed turn');
  } finally { t.close(); }
});

test('moves are validated', () => {
  const t = new Table({ ...fast, turnMs: 60_000, botDelay: [60_000, 60_000], id: 'v', code: 'q', mode: 'ffa2', private: true },
    { changed() {}, emote() {}, matchEnd() {}, closed() {} });
  t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
  t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
  assert.equal(t.start('b'), 'not_host');
  assert.equal(t.start('a'), null);
  const turnUid = t.turn === 0 ? 'a' : 'b', other = turnUid === 'a' ? 'b' : 'a';
  assert.equal(t.act(other, { t: 'draw' }), 'not_your_turn');
  const notMine = TILES.map((_, i) => i).find(i => !t.hands[t.turn].includes(i))!;
  assert.equal(t.act(turnUid, { t: 'play', id: notMine }), 'no_tile');
  assert.equal(t.act(turnUid, { t: 'pass' }), 'can_play');
  const mine = t.opening ?? t.hands[t.turn][0];   // the heaviest double must open
  assert.equal(t.act(turnUid, { t: 'play', id: mine }), null);
  const v = t.view(other);
  assert.equal(v.hand.length, 7);
  assert.equal(v.seats[t.seatOf(turnUid)]!.count, 6);
  assert.ok(!('hands' in v), 'other hands must not be in the view');
  t.close();
});

test('bot always returns a legal move', () => {
  for (let i = 0; i < 300; i++) {
    const l = newLine();
    const deck = shuffled(TILES.map((_, k) => k));
    place(l, deck.pop()!, 'C');
    const hand = deck.splice(0, 7);
    const m = chooseMove(l, hand, deck.length);
    if (m.t === 'play') assert.ok(endsFor(l, m.id).includes(m.e));
    else assert.equal(hand.some(id => endsFor(l, id).length), false);
    assert.ok(pipSum(hand) >= 0);
  }
});

/* ---------------- Egyptian deal and opening ---------------- */
import { defaultTarget } from '../shared/engine';

function table(mode: 'ffa2' | 'ffa3' | 'ffa4' | 'team') {
  const t = new Table({ id: 't', code: 'x', mode, target: defaultTarget(mode), private: true, turnMs: 60_000, nextRoundMs: 60_000, botDelay: [60_000, 60_000], offlineGraceMs: 60_000, lobbyAfterMatchMs: 60_000 },
    { changed() {}, emote() {}, matchEnd() {}, closed() {} });
  const n = { ffa2: 2, ffa3: 3, ffa4: 4, team: 4 }[mode];
  for (let i = 0; i < n; i++) t.join({ uid: 'u' + i, name: 'P' + i, ch: i, bot: false, rating: 1000 });
  t.start('u0');
  return t;
}

test('three players: the 0|0 is taken out and everyone gets 9 tiles, no boneyard', () => {
  const t = table('ffa3');
  const v = t.view('u0');
  assert.equal(v.hand.length, 9);
  assert.equal(v.yard, 0);
  assert.ok(![0, 1, 2].some(s => t.view('u' + s).hand.includes(0)), 'nobody holds the 0|0');
  t.close();
});

test('partners (زوجي): the double six opens and plays to 151', () => {
  const t = table('team');
  const v = t.view('u0');
  assert.equal(v.target, 151);
  const opener = 'u' + v.turn;
  const ov = t.view(opener);
  assert.equal(ov.open, 27, 'the 6|6 must open');
  const other = ov.hand.find(id => id !== 27)!;
  assert.equal(t.act(opener, { t: 'play', id: other, e: 'C' }), 'must_open');
  assert.equal(t.act(opener, { t: 'play', id: 27, e: 'C' }), null);
  t.close();
});

test('1 vs 1 plays to 101 and the heaviest double opens', () => {
  const t = table('ffa2');
  const v = t.view('u0');
  assert.equal(v.target, 101);
  const ov = t.view('u' + v.turn);
  if (ov.open !== null) assert.ok(ov.hand.includes(ov.open));
  t.close();
});

test('the block game (house rule): nobody draws, the yard stays out, and a full match still finishes', async () => {
  let ended = false, drew = false, yardSeen = -1;
  await new Promise<void>((resolve, reject) => {
    const t = new Table({ ...fast, id: 'blk', code: null, mode: 'ffa2', private: false, block: true }, {
      changed(tb) {
        if (tb.last?.t === 'draw') drew = true;
        if (tb.phase === 'play') { yardSeen = tb.view('x').yard; assert.equal(tb.view('x').block, true); }
      },
      emote() {},
      matchEnd(tb) { ended = true; tb.close(); },
      closed() { resolve(); },
    });
    t.hostUid = 'h'; for (let i = 0; i < t.n; i++) t.addBot('h', i);
    const err = t.start('h'); if (err) reject(new Error(err));
    setTimeout(() => reject(new Error('timeout')), 20000).unref();
  });
  assert.ok(ended);
  assert.equal(drew, false, 'nobody drew');
  assert.equal(yardSeen, 0, 'the players see no pile');
});
