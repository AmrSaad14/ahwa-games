import { test } from 'node:test';
import assert from 'node:assert/strict';
import * as C from '../shared/games/chess';
import * as T from '../shared/games/tawla';
import * as L from '../shared/games/ludo';
import { BoardTable, BoardTableOptions } from '../shared/boardtable';
import type { Mode } from '../shared/engine';

/* ---------------- chess ---------------- */

test('chess: move generation matches the standard perft counts', () => {
  assert.equal(C.perft(C.fromFen(C.START_FEN), 3), 8902);
  assert.equal(C.perft(C.fromFen('r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1'), 3), 97862);
  assert.equal(C.perft(C.fromFen('8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1'), 4), 43238);
  assert.equal(C.perft(C.fromFen('rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8'), 3), 62379);
});

test('chess: mate, stalemate, repetition and material draws are detected', () => {
  let s = C.fromFen(C.START_FEN);
  const play = (from: string, to: string) => {
    const sq = (x: string) => (Number(x[1]) - 1) * 8 + 'abcdefgh'.indexOf(x[0]);
    const m = C.legalMoves(s).find(m => m.from === sq(from) && m.to === sq(to));
    assert.ok(m, `${from}${to} should be legal`); s = C.make(s, m!);
  };
  play('f2', 'f3'); play('e7', 'e5'); play('g2', 'g4'); play('d8', 'h4');   // fool's mate
  assert.deepEqual(C.outcome(s), { kind: 'mate', winner: 'b' });
  assert.equal(C.outcome(C.fromFen('7k/5Q2/6K1/8/8/8/8/8 b - - 0 1'))?.kind, 'stalemate');
  assert.equal(C.outcome(C.fromFen('8/8/4k3/8/8/3NK3/8/8 w - - 0 1'))?.kind, 'material');
  s = C.fromFen(C.START_FEN);
  for (let i = 0; i < 2; i++) { play('g1', 'f3'); play('g8', 'f6'); play('f3', 'g1'); play('f6', 'g8'); }
  assert.equal(C.outcome(s)?.kind, 'repetition');
});

test('chess: the computer finds mate in one', () => {
  const s = C.fromFen('6k1/5ppp/8/8/8/8/5PPP/3R2K1 w - - 0 1');
  const m = C.bestMove(s, 3);
  assert.deepEqual(C.outcome(C.make(s, m))?.kind, 'mate');
});

/* ---------------- tawla ---------------- */

test('tawla: starting position has 15 checkers each and 167 pips', () => {
  const s = T.startPosition(0);
  assert.equal(s.pts.filter(x => x > 0).reduce((a, b) => a + b, 0), 15);
  assert.equal(-s.pts.filter(x => x < 0).reduce((a, b) => a + b, 0), 15);
  assert.equal(T.pipCount(s, 0), 167); assert.equal(T.pipCount(s, 1), 167);
});

test('tawla: dice rules (both dice, doubles, blocked points, larger die)', () => {
  const s = T.startPosition(0);
  const r = { ...s, dice: [3, 1], rolled: [3, 1] as [number, number] }; r.seqs = T.sequences(r);
  assert.ok(r.seqs.every(q => q.length === 2), 'both dice must be used when possible');
  const d = { ...s, dice: [4, 4, 4, 4], rolled: [4, 4] as [number, number] }; d.seqs = T.sequences(d);
  assert.ok(d.seqs.every(q => q.length === 4), 'doubles are played four times');
  // a lone checker on 20: 6 then 5 and 5 then 6 both end on the blocked point 9, so only one die can be used: the 6
  const pts = new Array(24).fill(0); pts[20] = 1; pts[9] = -2; pts[23] = -13;
  const one: T.TawlaState = { pts, bar: [0, 0], off: [14, 0], turn: 0, dice: [6, 5], rolled: [6, 5], seqs: null };
  one.seqs = T.sequences(one);
  assert.ok(one.seqs.length > 0 && one.seqs.every(q => q[0].die === 6), 'the larger die must be used');
});

test('tawla: hitting sends a checker to the bar, which must enter first', () => {
  const pts = new Array(24).fill(0); pts[10] = 1; pts[8] = -1; pts[20] = -14;
  let s: T.TawlaState = { pts, bar: [0, 0], off: [14, 0], turn: 0, dice: [2], rolled: [2, 2], seqs: null };
  s.seqs = T.sequences(s);
  s = T.playStep(s, 10, 8)!;
  assert.equal(s.bar[1], 1);
  s = T.endTurn(s); s.dice = [3]; s.seqs = T.sequences(s);
  assert.ok(T.nextSteps(s).every(x => x.from === T.BAR), 'a checker on the bar must enter first');
});

test('tawla: bearing off and mars', () => {
  const pts = new Array(24).fill(0); pts[0] = 1; pts[23] = -15;
  let s: T.TawlaState = { pts, bar: [0, 0], off: [14, 0], turn: 0, dice: [6], rolled: [6, 6], seqs: null };
  s.seqs = T.sequences(s);
  s = T.playStep(s, 0, T.OFF)!;
  assert.deepEqual(T.winner(s), { winner: 0, points: 2 });
});

/* ---------------- ludo ---------------- */

test('ludo: a six brings a pawn out, captures send pawns home, safe squares protect', () => {
  let s = L.newLudo(2);
  s = { ...s, die: 5 };
  assert.deepEqual(L.movable(s), [], 'needs a six to leave the base');
  s = { ...s, die: 6, sixes: 1 };
  const out = L.movePawn(s, 0)!;
  assert.equal(out.state.pawns[0][0], 0);
  assert.equal(out.state.turn, 0, 'a six earns another roll');
  // seat 1 (yellow, offset 26) sits 3 squares ahead of red's pawn on an unsafe square
  s = { ...L.newLudo(2), die: 3, sixes: 0 };
  s.pawns[0][0] = 2;                  // red on square 2
  s.pawns[1][0] = (5 - 26 + 52) % 52; // yellow on square 5
  const cap = L.movePawn(s, 0)!;
  assert.equal(cap.captured.length, 1);
  assert.equal(cap.state.pawns[1][0], -1);
  assert.equal(cap.state.turn, 0, 'a capture earns another roll');
  // on a safe square there is no capture
  s = { ...L.newLudo(2), die: 6, sixes: 1 };
  s.pawns[0][0] = 2; s.pawns[1][0] = (8 - 26 + 52) % 52;
  assert.equal(L.movePawn(s, 0)!.captured.length, 0);
});

test('ludo: pawns need the exact number to get home', () => {
  const s = { ...L.newLudo(2), die: 4, sixes: 0 };
  s.pawns[0] = [54, 56, 56, 56];
  assert.deepEqual(L.movable(s), []);
  const t = { ...s, die: 2 };
  const r = L.movePawn(t, 0)!;
  assert.equal(L.ludoWinner(r.state), 0);
});

/* ---------------- full matches through the board-game table ---------------- */

const fast: Omit<BoardTableOptions, 'id' | 'mode' | 'private' | 'code' | 'target'> = {
  turnMs: 50, nextRoundMs: 5, botDelay: [0, 1], offlineGraceMs: 5, lobbyAfterMatchMs: 5, autoMoveMs: 1, chessClockMs: 600_000,
};

function botMatch(mode: Mode, target = 1) {
  return new Promise<{ scores: number[]; team: number; moves: number }>((resolve, reject) => {
    let moves = 0;
    const guard = setTimeout(() => { t.close(); reject(new Error('timeout ' + mode)); }, 170_000); guard.unref();
    const t = new BoardTable({ ...fast, id: 'm', code: null, mode, target, private: false }, {
      changed(tb) { if (tb.last && tb.last.t !== 'start') moves++; },
      emote() {},
      matchEnd(tb, res) { clearTimeout(guard); tb.close(); resolve({ scores: res.scores, team: res.team, moves }); },
      closed() {},
    });
    t.fillBots();
    const e = t.start(null); if (e) reject(new Error(e));
  });
}

test('tawla: a full match to 3 points between computer players', async () => {
  const r = await botMatch('tawla', 3);
  assert.ok(Math.max(...r.scores) >= 3);
  assert.equal(r.scores.indexOf(Math.max(...r.scores)), r.team);
});

for (const mode of ['ludo2', 'ludo3', 'ludo4'] as Mode[]) {
  test(`ludo: a full ${mode} game between computer players`, async () => {
    const r = await botMatch(mode);
    assert.ok(r.team >= 0 && r.scores[r.team] === 1);
  });
}

test('chess: a full game between computer players ends by the rules', { timeout: 200_000 }, async () => {
  const r = await botMatch('chess');
  assert.ok(r.moves > 4);
  assert.ok(r.team === -1 || r.scores[r.team] === 1);
});

test('chess: running out of time loses', async () => {
  await new Promise<void>((resolve, reject) => {
    const t = new BoardTable({ ...fast, chessClockMs: 120, id: 'c', code: 'x', mode: 'chess', target: 1, private: true }, {
      changed() {}, emote() {},
      matchEnd(tb, res) {
        try { assert.equal(tb.result?.kind, 'time'); assert.equal(res.team, 1 - tb.state.white, 'white ran out of time, black wins'); resolve(); } catch (e) { reject(e); }
        tb.close();
      },
      closed() {},
    });
    t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
    t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
    t.start('a');
    setTimeout(() => reject(new Error('no time loss')), 3000).unref();
  });
});

test('tawla and ludo: a player who does nothing still gets moves made after the timer', async () => {
  for (const mode of ['tawla', 'ludo2'] as Mode[]) {
    await new Promise<void>((resolve, reject) => {
      let autos = 0;
      const t = new BoardTable({ ...fast, turnMs: 30, botDelay: [60_000, 60_000], id: 'i', code: 'y', mode, target: 1, private: true }, {
        changed(tb) { if (tb.last?.auto) { autos++; if (autos >= 3) { tb.close(); resolve(); } } },
        emote() {}, matchEnd() {}, closed() {},
      });
      t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
      t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
      t.start('a');
      const g = setTimeout(() => { t.close(); reject(new Error('no automatic moves in ' + mode)); }, 5000); g.unref();
    });
  }
});

test('board views only offer moves to the player whose turn it is', () => {
  const t = new BoardTable({ ...fast, turnMs: 60_000, botDelay: [60_000, 60_000], id: 'v', code: 'z', mode: 'chess', target: 1, private: true },
    { changed() {}, emote() {}, matchEnd() {}, closed() {} });
  t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 });
  t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
  t.start('a');
  const whiteUid = t.state.white === 0 ? 'a' : 'b', blackUid = whiteUid === 'a' ? 'b' : 'a';
  assert.equal(t.view(whiteUid).chess!.legal.length, 20);
  assert.equal(t.view(blackUid).chess!.legal.length, 0);
  assert.equal(t.act(blackUid, { t: 'move', from: 52, to: 36 }), 'not_your_turn');
  assert.equal(t.act(whiteUid, { t: 'move', from: 12, to: 36 }), 'illegal_move');
  assert.equal(t.act(whiteUid, { t: 'move', from: 12, to: 28 }), null);
  t.close();
});

/* ---------------- Egyptian tawla variants: محبوسة, ٣١, جلبهار ---------------- */
import * as V from '../shared/games/tables';

test('محبوسة: all 15 start on one point each and moving onto a lone checker traps it', () => {
  let s = V.startPosition('mahbousa', 0);
  assert.equal(s.c[0][23], 15); assert.equal(s.c[1][0], 15);
  // player 1 has one checker on point 10, player 0 lands there with a 3 from 13
  s.c[0][23] = 14; s.c[0][13] = 1; s.c[1][0] = 14; s.c[1][10] = 1;
  s = { ...s, rolled: [3, 1], dice: [3, 1] };
  const n = V.playStep(s, 13, 10)!;
  assert.ok(n, 'landing on a lone opposing checker is allowed');
  assert.equal(n.pinned[10], 1, 'the opposing checker is trapped underneath');
  // the trapped checker can't move, and its owner can't land on the trapping checker
  const opp = { ...n, turn: 1 as const, rolled: [1, 2] as [number, number], dice: [1, 2] };
  assert.ok(V.nextSteps(opp).every(st => st.from !== 10), 'a trapped checker cannot move');
  // moving away frees it
  const away = { ...n, dice: [1] };
  const freed = V.playStep(away, 10, 9)!;
  assert.equal(freed.pinned[10], -1);
});

test('محبوسة: two checkers close a point, and a mars counts double', () => {
  const s = V.startPosition('mahbousa', 0);
  s.c[0][23] = 14; s.c[0][13] = 1; s.c[1][0] = 13; s.c[1][10] = 2;
  const r = { ...s, rolled: [3, 5] as [number, number], dice: [3, 5] };
  assert.ok(!V.nextSteps(r).some(st => st.from === 13 && st.to === 10), 'two opposing checkers close the point');
  const end = V.startPosition('mahbousa', 0); end.c[0][23] = 0; end.off = [15, 0];
  assert.deepEqual(V.winner(end), { winner: 0, points: 2, kind: 'mars' });
});

test('٣١: both race the same way from diagonal corners; one checker holds a point', () => {
  const s = V.startPosition('tawla31', 0);
  assert.equal(s.c[0][23], 15); assert.equal(s.c[1][11], 15, 'player 1 starts in the diagonally opposite corner');
  assert.equal(V.pointOf('tawla31', 1, 1), 12, 'player 1 finishes on points 12..17');
  s.c[1][11] = 14; s.c[1][20] = 1;   // a single opposing checker on 20
  const r = { ...s, rolled: [3, 1] as [number, number], dice: [3, 1] };
  assert.ok(!V.nextSteps(r).some(st => st.to === 20), 'a single opposing checker blocks the point');
});

test('٣١: the first checker must pass the opponent start before a second one leaves', () => {
  const s = V.startPosition('tawla31', 0);
  s.c[0][23] = 14; s.c[0][18] = 1;   // one runner out, still before the opponent start (point 11)
  const r = { ...s, rolled: [2, 1] as [number, number], dice: [2, 1] };
  assert.ok(V.nextSteps(r).every(st => st.from === 18), 'only the runner may move');
  s.c[0][18] = 0; s.c[0][9] = 1;     // runner past point 11
  const r2 = { ...s, rolled: [2, 1] as [number, number], dice: [2, 1] };
  assert.ok(V.nextSteps(r2).some(st => st.from === 23), 'now others may leave the start');
});

test('٣١: the winner scores one point per opposing checker left on the board', () => {
  const s = V.startPosition('tawla31', 0); s.c[0][23] = 0; s.off = [15, 4];
  assert.deepEqual(V.winner(s), { winner: 0, points: 11, kind: 'count' });
});

test('جلبهار: after three rolls a double climbs to six, and a blocked run passes to the opponent', () => {
  let s = V.startPosition('gulbahar', 0);
  s.rolls = [3, 3];
  const dice = [0.4, 0.4];   // 3-3
  let i = 0; const rnd = () => dice[i++ % 2];
  s = V.roll(s, rnd);
  assert.deepEqual(s.rolled, [3, 3]);
  assert.deepEqual(s.dice, [3, 3, 3, 3]);
  assert.deepEqual(s.later, [4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6]);
  // early in the game a double is just four moves
  let e = V.startPosition('gulbahar', 0); i = 0; e = V.roll(e, rnd);
  assert.deepEqual(e.later, []);
  // stuck mid-run: the opponent takes over the rest
  const stuck = { ...s, dice: [3, 3], later: [4, 4, 4, 4] };
  stuck.c = [new Array(24).fill(0), new Array(24).fill(0)];
  stuck.c[0][2] = 15;                          // all home but can't bear off 3s? they can: put them so nothing fits
  stuck.c[0][2] = 0; stuck.c[0][23] = 15; stuck.c[1][20] = 1; stuck.c[1][11] = 14;   // 23-3=20 blocked
  const n = V.endTurn(stuck);
  assert.equal(n.turn, 1, 'the opponent plays the rest of the run');
  assert.equal(n.carry, 1);
});

for (const mode of ['mahbousa', 'tawla31', 'gulbahar'] as Mode[]) {
  test(`${mode}: a full match between computer players`, { timeout: 200_000 }, async () => {
    const r = await botMatch(mode, mode === 'tawla31' ? 31 : 3);
    assert.ok(Math.max(...r.scores) >= (mode === 'tawla31' ? 31 : 3));
  });
}

/* ---------------- ludo variants ---------------- */
test('ludo quick: one pawn starts out and the first pawn home wins', () => {
  const s = L.newLudo(2, 'quick');
  assert.deepEqual(s.pawns[0], [0, -1, -1, -1]);
  const near = { ...s, die: 2, sixes: 0, pawns: [[54, -1, -1, -1], [0, -1, -1, -1]] };
  const r = L.movePawn(near, 0)!;
  assert.equal(L.ludoWinner(r.state), 0);
});

test('ludo master: no home column before a capture; pawns go round instead', () => {
  const s = { ...L.newLudo(2, 'master'), die: 4, sixes: 0 };
  s.pawns[0][0] = 49;
  const r = L.movePawn(s, 0)!;
  assert.equal(r.state.pawns[0][0], 1, 'went past its start and round again (49 + 4 = 53 → square 1)');
  const s2 = { ...L.newLudo(2, 'master'), die: 2, sixes: 0 }; s2.pawns[0][0] = 49;
  assert.equal(L.movePawn(s2, 0)!.state.pawns[0][0], L.LAP, 'lands on the square just before its start');
  const s3 = { ...L.newLudo(2, 'master'), die: 4, sixes: 0, captures: [1, 0] }; s3.pawns[0][0] = 49;
  assert.equal(L.movePawn(s3, 0)!.state.pawns[0][0], 53, 'after a capture it turns into the home column');
});

for (const mode of ['ludoq2', 'ludoq4', 'ludom2', 'ludom4'] as Mode[]) {
  test(`${mode}: a full game between computer players`, { timeout: 200_000 }, async () => {
    const r = await botMatch(mode);
    assert.ok(r.team >= 0 && r.scores[r.team] === 1);
  });
}

test('chess modes have their own clocks', () => {
  for (const [mode, min] of [['chess1', 1], ['chess3', 3], ['chess', 5], ['chess10', 10]] as [Mode, number][]) {
    const { chessClockMs, ...opts } = fast;
    const t = new BoardTable({ ...opts, id: 'k', code: 'k', mode, target: 1, private: true }, { changed() {}, emote() {}, matchEnd() {}, closed() {} });
    t.join({ uid: 'a', name: 'A', ch: 0, bot: false, rating: 1000 }); t.join({ uid: 'b', name: 'B', ch: 1, bot: false, rating: 1000 });
    t.start('a');
    assert.equal(t.view('a').clocks![0], min * 60_000);
    t.close();
  }
});

test('mahbousa: trapping the mother (last checker on its start) wins a double game', () => {
  const s = V.startPosition('mahbousa', 0);
  s.c[1][0] = 1; s.c[0][0] = 1; s.pinned[0] = 1;   // player 1 starts on point 0: its last checker there is trapped
  assert.deepEqual(V.winner(s), { winner: 0, points: 2, kind: 'mother' });
});

test('tawla: one checker can use both dice in a single move (5 and 2 → 7 points)', async () => {
  const { defFor } = await import('../shared/games/defs');
  for (const mode of ['tawla', 'mahbousa', 'tawla31', 'gulbahar'] as Mode[]) {
    const def = defFor(mode);
    let s = def.newGame(2, 0, Math.random) as never;
    const seq = [4 / 6 + 0.01, 1 / 6 + 0.01];   // a 5 and a 2
    const r = def.act(s, 0, { t: 'roll' }, () => seq.shift() ?? 0.5);
    assert.ok(typeof r !== 'string'); s = r.s as never;
    const v = def.view(s, 0) as { combos?: [number, number, number[]][]; steps: [number, number][] };
    assert.ok(v.combos && v.combos.length > 0, 'combined moves are offered');
    for (const [from, to, via] of v.combos!) {
      assert.ok(!v.steps.some(x => x[0] === from && x[1] === to), 'a combined move is not also a single-die move');
      const done = def.act(s, 0, { t: 'step', from, to, via }, Math.random);
      assert.ok(typeof done !== 'string', `combined move ${from}→${to} via ${via} is legal`);
      assert.equal(done.e?.from, from); assert.equal(done.e?.to, to);
    }
    // a made-up route is refused
    const [from, to] = v.combos![0];
    assert.equal(def.act(s, 0, { t: 'step', from, to, via: [5] }, Math.random), 'illegal_move');
  }
});

