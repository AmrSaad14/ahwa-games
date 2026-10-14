// Ludo for 2 to 4 players. Each colour has 4 pawns.
// A pawn's progress: -1 in the base, 0..50 on the shared track (0 = its start square),
// 51..55 in its own home column, 56 = home (finished).
// Rules: a 6 brings a pawn out; a 6, a capture or reaching home earns another roll; three 6s in a row
// end the turn; landing on a lone opponent pawn outside a safe square sends it back to base.
// Variants (as played on Ludo Star / Jawaker in Egypt):
//   classic: get all four pawns home.
//   quick (سريع): each player starts with one pawn already on its start square; the first pawn home wins.
//   master (ماستر): you must capture an opponent pawn before any of your pawns may enter the home column;
//     until then pawns keep going round the board. Progress -2 marks the square just before a colour's start.

export const TRACK = 52, HOME = 56;
export const COLOR_OFFSET = [0, 13, 26, 39];       // start squares of red, green, yellow, blue
export const SAFE = new Set([0, 8, 13, 21, 26, 34, 39, 47]);

export type LudoVariant = 'classic' | 'quick' | 'master';
/** The track square just before a colour's own start, used only by master-mode pawns going round again. */
export const LAP = -2;

export interface LudoState {
  variant: LudoVariant;
  captures: number[];      // pawns each seat has captured (master: one opens the home column)
  colors: number[];        // colour index for each seat (2 players sit opposite each other)
  pawns: number[][];       // per seat, 4 progress values
  turn: number;
  die: number | null;      // rolled value waiting to be played
  sixes: number;           // sixes rolled in a row this turn
  finished: number[];      // seats in the order they got all pawns home
}

export function newLudo(players: number, variant: LudoVariant = 'classic'): LudoState {
  const colors = players === 2 ? [0, 2] : players === 3 ? [0, 1, 2] : [0, 1, 2, 3];
  const start = variant === 'quick' ? [0, -1, -1, -1] : [-1, -1, -1, -1];
  return { variant, captures: colors.map(() => 0), colors, pawns: colors.map(() => start.slice()), turn: 0, die: null, sixes: 0, finished: [] };
}

/** Absolute track square of a pawn, or -1 when it is in base, its home column or home. */
export function square(s: LudoState, seat: number, prog: number) {
  if (prog === LAP) return (COLOR_OFFSET[s.colors[seat]] + 51) % TRACK;
  if (prog < 0 || prog > 50) return -1;
  return (COLOR_OFFSET[s.colors[seat]] + prog) % TRACK;
}

export function movable(s: LudoState, seat = s.turn, die = s.die): number[] {
  if (die === null) return [];
  const out: number[] = [];
  s.pawns[seat].forEach((p, i) => {
    if (p === HOME) return;
    if (p === -1) { if (die === 6) out.push(i); return; }
    if (circling(s, seat, p)) { out.push(i); return; }    // master, no capture yet: it can always go round
    if (p + die <= HOME) out.push(i);
  });
  return out;
}

/** Master mode before the first capture: a pawn on the track goes round instead of turning home. */
function circling(s: LudoState, seat: number, p: number) {
  return s.variant === 'master' && s.captures[seat] === 0 && (p === LAP || (p >= 0 && p <= 50));
}
/** Where a pawn ends up after moving `die` squares. */
function advance(s: LudoState, seat: number, p: number, die: number) {
  if (p === -1) return 0;
  if (circling(s, seat, p)) {
    const rel = (p === LAP ? 51 : p) + die;        // relative track square, 0..51 round the board
    if (rel <= 50) return rel;
    const r = rel % 52;                             // past its own start again
    return r === 51 ? LAP : r;
  }
  return p + die;
}

export function rollDie(s: LudoState, rnd: () => number = Math.random): LudoState {
  const d = 1 + Math.floor(rnd() * 6);
  return { ...s, die: d, sixes: d === 6 ? s.sixes + 1 : s.sixes };
}

function nextTurn(s: LudoState): LudoState {
  const n = s.pawns.length;
  let t = s.turn;
  for (let k = 0; k < n; k++) { t = (t + 1) % n; if (!s.finished.includes(t)) break; }
  return { ...s, turn: t, die: null, sixes: 0 };
}

/** What happens after the roll when no pawn can move (or after three sixes): the turn passes. */
export function passTurn(s: LudoState): LudoState {
  if (s.die === 6 && s.sixes < 3) return { ...s, die: null };   // a 6 still earns another roll
  return nextTurn(s);
}

export interface LudoMoveResult { state: LudoState; captured: { seat: number; pawn: number }[]; home: boolean }

export function movePawn(s: LudoState, pawn: number): LudoMoveResult | null {
  const seat = s.turn, die = s.die;
  if (die === null || !movable(s).includes(pawn)) return null;
  const pawns = s.pawns.map(a => a.slice());
  const cur = pawns[seat][pawn];
  const nextProg = advance(s, seat, cur, die);
  pawns[seat][pawn] = nextProg;
  const captured: { seat: number; pawn: number }[] = [];
  const sq = square(s, seat, nextProg);
  if (sq >= 0 && !SAFE.has(sq)) {
    s.pawns.forEach((arr, os) => {
      if (os === seat) return;
      arr.forEach((p, pi) => { if (square(s, os, p) === sq) captured.push({ seat: os, pawn: pi }); });
    });
    // two or more pawns of one colour form a block that can't be captured
    const bySeat = new Map<number, number>();
    captured.forEach(c => bySeat.set(c.seat, (bySeat.get(c.seat) ?? 0) + 1));
    for (let i = captured.length - 1; i >= 0; i--) if ((bySeat.get(captured[i].seat) ?? 0) >= 2) captured.splice(i, 1);
    captured.forEach(c => { pawns[c.seat][c.pawn] = -1; });
  }
  const home = nextProg === HOME;
  const captures = s.captures.slice(); captures[seat] += captured.length;
  let n: LudoState = { ...s, pawns, captures };
  const done = s.variant === 'quick' ? home : pawns[seat].every(p => p === HOME);   // quick: the first pawn home wins
  if (done && !n.finished.includes(seat)) n = { ...n, finished: [...n.finished, seat] };
  const again = (die === 6 && s.sixes < 3) || captured.length > 0 || home;
  if (n.finished.includes(seat) || !again || s.sixes >= 3) n = nextTurn(n);
  else n = { ...n, die: null, sixes: die === 6 ? s.sixes : 0 };
  return { state: n, captured, home };
}

/** The game ends as soon as one player has all four pawns home. */
export const ludoWinner = (s: LudoState) => (s.finished.length ? s.finished[0] : -1);

/* ---------------- computer player ---------------- */
export function botPawn(s: LudoState): number | null {
  const opts = movable(s);
  if (!opts.length) return null;
  let best = opts[0], bestV = -Infinity;
  for (const i of opts) {
    const r = movePawn(s, i)!;
    const cur = s.pawns[s.turn][i], nxt = r.state.pawns[s.turn][i];
    const prog = (x: number) => (x === LAP ? 51 : x);
    let v = r.captured.length * (s.variant === 'master' && s.captures[s.turn] === 0 ? 160 : 100) + (r.home ? (s.variant === 'quick' ? 400 : 80) : 0) + (cur === -1 ? 60 : 0) + prog(nxt) * 0.6;
    if (cur >= 0 && nxt >= 0 && nxt < prog(cur) && nxt <= 50) v -= 30;   // master: going round again
    const sq = square(s, s.turn, nxt);
    if (sq >= 0 && SAFE.has(sq)) v += 15;
    if (nxt > 50) v += 25;  // safe in the home column
    // danger: opponent pawns 1..6 squares behind the landing square
    if (sq >= 0 && !SAFE.has(sq)) {
      s.pawns.forEach((arr, os) => {
        if (os === s.turn) return;
        arr.forEach(p => { const o = square(s, os, p); if (o >= 0) { const d = (sq - o + TRACK) % TRACK; if (d >= 1 && d <= 6) v -= 20; } });
      });
    }
    v += Math.random() * 3;
    if (v > bestV) { bestV = v; best = i; }
  }
  return best;
}
