// Tawla 31 (backgammon rules as played in Egyptian coffeehouses): 15 checkers each, two dice, hitting,
// the bar, bearing off. A win counts 1 point; a mars (the loser has borne off nothing) counts 2.
//
// Points are 0..23. Player 0 moves from high to low and bears off past point 0 (home = 0..5).
// Player 1 moves from low to high and bears off past point 23 (home = 18..23).
// pts[i] > 0: player 0's checkers on point i; pts[i] < 0: player 1's.

export const BAR = -1;     // "from" value for a checker entering from the bar
export const OFF = 99;     // "to" value for bearing a checker off

export interface TawlaState {
  pts: number[];           // 24 points
  bar: [number, number];
  off: [number, number];
  turn: 0 | 1;
  dice: number[];          // dice still to play this turn (doubles give four)
  rolled: [number, number] | null;
  seqs: Step[][] | null;   // every legal way to finish this turn from here
}
export interface Step { from: number; to: number; die: number; hit?: boolean }

export function startPosition(first: 0 | 1): TawlaState {
  const pts = new Array(24).fill(0);
  pts[23] = 2; pts[12] = 5; pts[7] = 3; pts[5] = 5;
  pts[0] = -2; pts[11] = -5; pts[16] = -3; pts[18] = -5;
  return { pts, bar: [0, 0], off: [0, 0], turn: first, dice: [], rolled: null, seqs: null };
}

const sign = (p: 0 | 1) => (p === 0 ? 1 : -1);
const mine = (s: TawlaState, p: 0 | 1, i: number) => s.pts[i] * sign(p) > 0;
const count = (s: TawlaState, p: 0 | 1, i: number) => Math.max(0, s.pts[i] * sign(p));
const blocked = (s: TawlaState, p: 0 | 1, i: number) => s.pts[i] * sign(p) <= -2;

function allHome(s: TawlaState, p: 0 | 1) {
  if (s.bar[p]) return false;
  for (let i = 0; i < 24; i++) {
    if (!mine(s, p, i)) continue;
    if (p === 0 ? i > 5 : i < 18) return false;
  }
  return true;
}

/** Single-die moves available right now. */
export function singleSteps(s: TawlaState, die: number): Step[] {
  const p = s.turn, out: Step[] = [];
  const target = (i: number) => (p === 0 ? i - die : i + die);
  if (s.bar[p]) {
    const to = p === 0 ? 24 - die : die - 1;
    if (!blocked(s, p, to)) out.push({ from: BAR, to, die, hit: s.pts[to] * sign(p) === -1 });
    return out;
  }
  const home = allHome(s, p);
  for (let i = 0; i < 24; i++) {
    if (!mine(s, p, i)) continue;
    const to = target(i);
    if (to >= 0 && to <= 23) {
      if (!blocked(s, p, to)) out.push({ from: i, to, die, hit: s.pts[to] * sign(p) === -1 });
    } else if (home) {
      // bearing off: exact, or with a higher die when no checker sits further back
      const dist = p === 0 ? i + 1 : 24 - i;
      if (dist === die) out.push({ from: i, to: OFF, die });
      else if (die > dist) {
        let further = false;
        if (p === 0) { for (let j = i + 1; j <= 5; j++) if (mine(s, p, j)) further = true; }
        else { for (let j = i - 1; j >= 18; j--) if (mine(s, p, j)) further = true; }
        if (!further) out.push({ from: i, to: OFF, die });
      }
    }
  }
  return out;
}

export function applyStep(s: TawlaState, st: Step): TawlaState {
  const p = s.turn, o = (1 - p) as 0 | 1, sg = sign(p);
  const n: TawlaState = { ...s, pts: s.pts.slice(), bar: [...s.bar] as [number, number], off: [...s.off] as [number, number], dice: s.dice.slice(), seqs: null };
  if (st.from === BAR) n.bar[p]--; else n.pts[st.from] -= sg;
  if (st.to === OFF) n.off[p]++;
  else {
    if (n.pts[st.to] * sg === -1) { n.pts[st.to] = 0; n.bar[o]++; }
    n.pts[st.to] += sg;
  }
  n.dice.splice(n.dice.indexOf(st.die), 1);
  return n;
}

/** Every legal complete sequence for the dice left, following the rules: use as many dice as possible,
 *  and if only one of two different dice can be used, the larger one. */
export function sequences(s: TawlaState): Step[][] {
  const all: Step[][] = [];
  const rec = (st: TawlaState, path: Step[]) => {
    const uniq = [...new Set(st.dice)];
    let moved = false;
    for (const d of uniq) for (const step of singleSteps(st, d)) {
      moved = true;
      rec(applyStep(st, step), [...path, step]);
    }
    if (!moved) all.push(path);
  };
  rec(s, []);
  const maxLen = Math.max(...all.map(p => p.length));
  let best = all.filter(p => p.length === maxLen);
  if (maxLen === 1 && s.dice.length === 2 && s.dice[0] !== s.dice[1]) {
    const hi = Math.max(...s.dice);
    if (best.some(p => p[0].die === hi)) best = best.filter(p => p[0].die === hi);
  }
  return best;
}

export function roll(s: TawlaState, rnd: () => number = Math.random): TawlaState {
  const a = 1 + Math.floor(rnd() * 6), b = 1 + Math.floor(rnd() * 6);
  const n: TawlaState = { ...s, rolled: [a, b], dice: a === b ? [a, a, a, a] : [a, b] };
  n.seqs = sequences(n);
  return n;
}

const sameStep = (a: Step, b: Step) => a.from === b.from && a.to === b.to && a.die === b.die;

/** Steps the player may take next (first steps of the remaining legal sequences). */
export function nextSteps(s: TawlaState): Step[] {
  if (!s.seqs) return [];
  const out: Step[] = [];
  for (const q of s.seqs) if (q.length && !out.some(x => sameStep(x, q[0]))) out.push(q[0]);
  return out;
}

/** Plays one step of the turn. Returns the new state, or null if the step is not allowed. */
export function playStep(s: TawlaState, from: number, to: number): TawlaState | null {
  if (!s.seqs) return null;
  const cand = nextSteps(s).filter(x => x.from === from && x.to === to);
  if (!cand.length) return null;
  // prefer the die that keeps the most options open (bearing off with the smaller valid die, etc.)
  const step = cand.sort((a, b) => a.die - b.die)[0];
  const n = applyStep(s, step);
  n.seqs = s.seqs.filter(q => q.length && sameStep(q[0], step)).map(q => q.slice(1));
  if (!n.seqs.length || n.seqs.every(q => !q.length)) n.seqs = [[]];
  return n;
}

export const turnDone = (s: TawlaState) => !!s.seqs && s.seqs.every(q => q.length === 0);

export function endTurn(s: TawlaState): TawlaState {
  return { ...s, turn: (1 - s.turn) as 0 | 1, dice: [], rolled: null, seqs: null };
}

/** null while playing; otherwise the winner and the points won (2 for a mars). */
export function winner(s: TawlaState): { winner: 0 | 1; points: number } | null {
  for (const p of [0, 1] as const) if (s.off[p] === 15) return { winner: p, points: s.off[1 - p] === 0 ? 2 : 1 };
  return null;
}

export function pipCount(s: TawlaState, p: 0 | 1) {
  let n = s.bar[p] * 25;
  for (let i = 0; i < 24; i++) n += count(s, p, i) * (p === 0 ? i + 1 : 24 - i);
  return n;
}

/* ---------------- computer player ---------------- */

function score(s: TawlaState, p: 0 | 1): number {
  const o = (1 - p) as 0 | 1;
  let v = (pipCount(s, o) - pipCount(s, p)) * 1.0 + s.off[p] * 6 + s.bar[o] * 18 - s.bar[p] * 18;
  for (let i = 0; i < 24; i++) {
    const c = count(s, p, i);
    if (c >= 2) v += (p === 0 ? i <= 6 : i >= 17) ? 7 : 4;          // made points, more in home board
    if (c === 1) {
      // a lonely checker (blot) is worse when the opponent has checkers behind it
      let danger = 0;
      for (let j = 0; j < 24; j++) if (count(s, o, j) && (p === 0 ? j < i : j > i) && Math.abs(i - j) <= 12) danger++;
      v -= 3 + danger * 1.5 + (s.bar[o] ? 4 : 0);
    }
  }
  return v;
}

/** Picks the whole sequence for the turn and returns its first step. */
export function botStep(s: TawlaState): Step | null {
  if (!s.seqs) return null;
  const opts = s.seqs.filter(q => q.length);
  if (!opts.length) return null;
  let best = opts[0], bestV = -Infinity;
  for (const q of opts) {
    let st = s;
    for (const step of q) st = applyStep(st, step);
    const v = score(st, s.turn) + Math.random() * 0.5;
    if (v > bestV) { bestV = v; best = q; }
  }
  return best[0];
}
