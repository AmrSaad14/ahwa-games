// Egyptian tawla variants without hitting: محبوسة (Mahbousa), ٣١ (31) and جلبهار (Gulbahar).
// Regular tawla («عادة», backgammon) lives in tawla.ts.
//
// Board points are numbered 0..23 the same way as in tawla.ts, so the same 3D board draws all of them.
// Each player walks a path of 24 points: path index 24 is where all 15 checkers start, 1 is the last point,
// and 0 means borne off. Player 0 always starts on point 23 and finishes on points 0..5.
//   محبوسة: players move toward each other (player 1 starts on point 0 and finishes on 18..23).
//   ٣١ and جلبهار: both move the same way round the board, starting in diagonally opposite corners
//   (player 1 starts on point 11 and finishes on 12..17).
//
// محبوسة: landing on a lone opposing checker traps (pins) it under yours until you move away;
//   a point with two or more opposing checkers, or an opposing checker that is itself trapping, is closed.
//   Trapping the opponent's last checker on their starting point («الأم», the mother) wins a double game.
// ٣١: one checker holds a point; before moving a second checker off the start, the first one must get past
//   the opponent's starting point; you may not build a wall of six points in a row in front of the opponent.
//   The winner scores one point per opposing checker still on the board; the set is played to 31.
// جلبهار: one checker holds a point; after each player's first three rolls a double is played four times,
//   then each higher number four times up to six (3-3: four 3s, four 4s, four 5s, four 6s). When the player
//   can't go on, the opponent plays the rest of that run.
// All: doubles are played four times, both dice must be used when possible (else the larger one),
//   bearing off starts when all 15 are home, and a win is 1 point, 2 (مارس) if the loser bore off none
//   (except ٣١, scored per checker).

import type { TawlaVariant } from '../engine';

export type TablesVariant = Exclude<TawlaVariant, 'aada'>;
export const OFF = 99;

export interface TablesState {
  v: TablesVariant;
  c: [number[], number[]];       // checkers of each player on each board point
  pinned: number[];              // per point: whose lone checker is trapped underneath (-1: none), محبوسة only
  off: [number, number];
  turn: 0 | 1;
  rolled: [number, number] | null;
  dice: number[];                // steps still to play this turn (current run of a جلبهار double)
  later: number[];               // جلبهار: the rest of a double run after the current four
  rolls: [number, number];       // rolls each player has made (جلبهار doubles start after three)
  carry: 0 | 1 | null;           // جلبهار: the player finishing the opponent's run, who rolls next as usual
}

export interface Step { from: number; to: number; die: number }

const same = (v: TablesVariant) => v !== 'mahbousa';
/** Board point of a player's path index (1..24). */
export function pointOf(v: TablesVariant, p: 0 | 1, r: number) {
  if (p === 0) return r - 1;
  return same(v) ? (r - 1 + 12) % 24 : 24 - r;
}
/** Path index (1..24) of a board point for a player. */
export function pathOf(v: TablesVariant, p: 0 | 1, pt: number) {
  if (p === 0) return pt + 1;
  return same(v) ? ((pt - 12 + 24) % 24) + 1 : 24 - pt;
}

export function startPosition(v: TablesVariant, first: 0 | 1): TablesState {
  const c: [number[], number[]] = [new Array(24).fill(0), new Array(24).fill(0)];
  c[0][pointOf(v, 0, 24)] = 15; c[1][pointOf(v, 1, 24)] = 15;
  return { v, c, pinned: new Array(24).fill(-1), off: [0, 0], turn: first, rolled: null, dice: [], later: [], rolls: [0, 0], carry: null };
}

const clone = (s: TablesState): TablesState => ({ ...s, c: [s.c[0].slice(), s.c[1].slice()], pinned: s.pinned.slice(), off: [s.off[0], s.off[1]], dice: s.dice.slice(), later: s.later.slice(), rolls: [s.rolls[0], s.rolls[1]] });

/** The checkers a player may move from a point (a trapped checker can't move). */
function movableAt(s: TablesState, p: 0 | 1, pt: number) {
  const mine = s.c[p][pt];
  if (!mine) return false;
  return !(s.pinned[pt] === p && mine === 1 && s.c[1 - p][pt] > 0);
}

function allHome(s: TablesState, p: 0 | 1) {
  for (let pt = 0; pt < 24; pt++) if (s.c[p][pt] && pathOf(s.v, p, pt) > 6) return false;
  return true;
}

/** ٣١: the first checker must get past the opponent's start before a second leaves the start. */
function postilionBlocks(s: TablesState, p: 0 | 1, fromR: number) {
  if (s.v !== 'tawla31' || fromR !== 24) return false;
  let out = 0, passed = false;
  for (let pt = 0; pt < 24; pt++) {
    const n = s.c[p][pt]; if (!n) continue;
    const r = pathOf(s.v, p, pt);
    if (r < 24) { out += n; if (r < 12) passed = true; }
  }
  return out > 0 && !passed && s.off[p] === 0;
}

/** ٣١: six held points in a row with an opposing checker behind them would shut the opponent in. */
function makesWall(s: TablesState, p: 0 | 1) {
  const o = (1 - p) as 0 | 1;
  // walk the opponent's path: a run of six points held by p with an opponent checker still behind it
  let run = 0, behind = false;
  for (let r = 24; r >= 1; r--) {
    const pt = pointOf(s.v, o, r);
    if (s.c[o][pt]) { behind = true; run = 0; continue; }
    if (s.c[p][pt]) { run++; if (run >= 6 && behind) return true; } else run = 0;
  }
  return false;
}

function canLand(s: TablesState, p: 0 | 1, pt: number) {
  const o = 1 - p;
  if (!s.c[o][pt]) return true;
  // محبوسة: trap a lone opposing checker that isn't itself trapping one of yours
  return s.v === 'mahbousa' && s.c[o][pt] === 1 && s.c[p][pt] === 0;
}

export function singleSteps(s: TablesState, die: number): Step[] {
  const p = s.turn, out: Step[] = [];
  const home = allHome(s, p);
  let highest = 0;
  if (home) for (let pt = 0; pt < 24; pt++) if (s.c[p][pt]) highest = Math.max(highest, pathOf(s.v, p, pt));
  for (let pt = 0; pt < 24; pt++) {
    if (!movableAt(s, p, pt)) continue;
    const r = pathOf(s.v, p, pt), t = r - die;
    if (postilionBlocks(s, p, r)) continue;
    if (t >= 1) {
      const to = pointOf(s.v, p, t);
      if (!canLand(s, p, to)) continue;
      if (s.v === 'tawla31') { const n = applyStep(s, { from: pt, to, die }); if (makesWall(n, p)) continue; }
      out.push({ from: pt, to, die });
    } else if (home && (t === 0 || r === highest)) {
      out.push({ from: pt, to: OFF, die });
    }
  }
  return out;
}

export function applyStep(s: TablesState, st: Step): TablesState {
  const n = clone(s), p = s.turn, o = (1 - p) as 0 | 1;
  n.c[p][st.from]--;
  if (!n.c[p][st.from] && n.pinned[st.from] === o) n.pinned[st.from] = -1;   // the trapped checker is free again
  if (st.to === OFF) n.off[p]++;
  else {
    if (s.v === 'mahbousa' && n.c[o][st.to] === 1 && n.c[p][st.to] === 0) n.pinned[st.to] = o;
    n.c[p][st.to]++;
  }
  return n;
}

const key = (s: TablesState) => s.c[0].join(',') + '|' + s.c[1].join(',') + '|' + s.pinned.join(',') + '|' + s.off.join(',');

/** Every legal way to play the remaining dice; only the longest count (and the larger die when only one fits). */
export function sequences(s: TablesState): Step[][] {
  const results: Step[][] = [];
  let best = 0;
  const seen = new Set<string>();
  const go = (st: TablesState, dice: number[], path: Step[]) => {
    const k = key(st) + '#' + dice.slice().sort().join('');
    if (seen.has(k)) return;
    seen.add(k);
    let moved = false;
    for (const d of [...new Set(dice)]) {
      for (const step of singleSteps(st, d)) {
        moved = true;
        const rest = dice.slice(); rest.splice(rest.indexOf(d), 1);
        go(applyStep(st, step), rest, [...path, step]);
      }
    }
    if (!moved) {
      if (path.length > best) { best = path.length; results.length = 0; }
      if (path.length === best) results.push(path);
    }
  };
  go({ ...s, turn: s.turn }, s.dice, []);
  if (best === 1 && s.dice.length === 2 && s.dice[0] !== s.dice[1]) {
    const big = Math.max(...s.dice);
    if (results.some(q => q[0].die === big)) return results.filter(q => q[0].die === big);
  }
  return results;
}

export function nextSteps(s: TablesState): Step[] {
  if (!s.rolled || !s.dice.length) return [];
  const seqs = sequences(s);
  const seen = new Set<string>(), out: Step[] = [];
  for (const q of seqs) if (q.length) { const k = q[0].from + '>' + q[0].to; if (!seen.has(k)) { seen.add(k); out.push(q[0]); } }
  return out;
}

export function roll(s: TablesState, rnd: () => number = Math.random): TablesState {
  const a = 1 + Math.floor(rnd() * 6), b = 1 + Math.floor(rnd() * 6);
  const n = clone(s);
  n.rolled = [a, b];
  n.rolls[s.turn]++;
  n.later = [];
  if (a !== b) n.dice = [a, b];
  else {
    n.dice = [a, a, a, a];
    // جلبهار: after each player's first three rolls a double climbs to six
    if (s.v === 'gulbahar' && n.rolls[s.turn] > 3) for (let d = a + 1; d <= 6; d++) n.later.push(d, d, d, d);
  }
  return n;
}

/** Play one step if it is legal (one of `nextSteps`). */
export function playStep(s: TablesState, from: number, to: number): TablesState | null {
  const st = nextSteps(s).find(x => x.from === from && x.to === to);
  if (!st) return null;
  const n = applyStep(s, st);
  n.dice.splice(n.dice.indexOf(st.die), 1);
  return n;
}

/** Nothing left to play this turn (all dice used, or none can be used). */
export const turnDone = (s: TablesState) => !!s.rolled && (s.dice.length === 0 || nextSteps(s).length === 0);

/** After the dice are used up or blocked: move on through a جلبهار run, hand it over, or pass the turn. */
export function endTurn(s: TablesState): TablesState {
  const n = clone(s);
  const blocked = n.dice.length > 0;
  if (n.v === 'gulbahar' && n.rolled && n.rolled[0] === n.rolled[1]) {
    if (!blocked && n.later.length) {
      // this four is done: carry on with the next number of the run
      n.dice = n.later.splice(0, 4);
      if (nextSteps(n).length) return n;
      return endTurn(n);
    }
    if (blocked && n.carry === null && n.rolls[n.turn] > 3) {
      // stuck in the middle of a climbing run: the opponent plays the rest of it, then rolls as usual
      const o = (1 - n.turn) as 0 | 1;
      n.turn = o; n.carry = o;
      if (nextSteps(n).length) return n;
      if (n.later.length) { n.dice = n.later.splice(0, 4); if (nextSteps(n).length) return n; }
      n.dice = []; n.later = [];
    }
  }
  if (n.carry !== null) {
    // the opponent finished (or couldn't finish) the run: it is now their own turn to roll
    n.turn = n.carry; n.carry = null; n.rolled = null; n.dice = []; n.later = [];
    return n;
  }
  n.turn = (1 - n.turn) as 0 | 1; n.rolled = null; n.dice = []; n.later = [];
  return n;
}

export function winner(s: TablesState): { winner: 0 | 1; points: number; kind: string } | null {
  if (s.v === 'mahbousa') for (const p of [0, 1] as const) {
    // the "mother": trapping the opponent's last checker on their starting point wins a double game
    const o = (1 - p) as 0 | 1, start = pointOf(s.v, o, 24);
    if (s.pinned[start] === o && s.c[o][start] === 1 && s.c[p][start] > 0) return { winner: p, points: 2, kind: 'mother' };
  }
  for (const p of [0, 1] as const) {
    if (s.off[p] < 15) continue;
    const o = 1 - p;
    if (s.v === 'tawla31') return { winner: p, points: 15 - s.off[o], kind: 'count' };
    const mars = s.off[o] === 0;
    return { winner: p, points: mars ? 2 : 1, kind: mars ? 'mars' : 'win' };
  }
  return null;
}

export function pipCount(s: TablesState, p: 0 | 1) {
  let n = 0;
  for (let pt = 0; pt < 24; pt++) n += s.c[p][pt] * pathOf(s.v, p, pt);
  return n;
}

/* ---------------- computer player ---------------- */
function evaluate(s: TablesState, p: 0 | 1) {
  const o = (1 - p) as 0 | 1;
  let v = (pipCount(s, o) - pipCount(s, p)) + s.off[p] * 8;
  for (let pt = 0; pt < 24; pt++) {
    if (s.v === 'mahbousa') {
      if (s.pinned[pt] === o) v += 18 + (pathOf(s.v, o, pt) > 18 ? 10 : 0);   // trapped deep in their start: great
      if (s.pinned[pt] === p) v -= 18;
      // a lone checker within reach of an opposing checker behind it can be trapped
      if (s.c[p][pt] === 1 && s.pinned[pt] !== o && pathOf(s.v, p, pt) > 6) {
        for (let d = 1; d <= 6; d++) {
          const r = pathOf(s.v, o, pt) + d;
          if (r <= 24 && s.c[o][pointOf(s.v, o, r)] && movableAt(s, o, pointOf(s.v, o, r))) { v -= 6; break; }
        }
      }
      if (s.c[p][pt] >= 2) v += 2;
    } else if (s.c[p][pt]) v += 1.5;   // ٣١ and جلبهار: every held point blocks the opponent
  }
  return v;
}

export function botStep(s: TablesState): Step | null {
  const seqs = sequences(s).filter(q => q.length);
  if (!seqs.length) return null;
  let best = seqs[0], bv = -Infinity;
  for (const q of seqs.slice(0, 400)) {
    let st = s;
    for (const step of q) st = applyStep(st, step);
    const v = evaluate(st, s.turn) + Math.random() * 0.5;
    if (v > bv) { bv = v; best = q; }
  }
  return best[0];
}
