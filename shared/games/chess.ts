// Full chess rules: legal moves (castling, en passant, promotion), check, mate, stalemate and draws,
// plus a small alpha-beta computer player. Squares are 0..63, a1 = 0, h1 = 7, a8 = 56.

export type Color = 'w' | 'b';
export type Promo = 'q' | 'r' | 'b' | 'n';
export interface ChessMove { from: number; to: number; promo?: Promo }
export interface ChessState {
  b: string[];          // '' or a piece letter: PNBRQK white, pnbrqk black
  turn: Color;
  castle: string;       // subset of 'KQkq'
  ep: number;           // en-passant target square or -1
  half: number;         // half-moves since a capture or pawn move (50-move rule)
  full: number;
  hist: string[];       // position keys, for threefold repetition
}

const N = [[2, 1], [1, 2], [-1, 2], [-2, 1], [-2, -1], [-1, -2], [1, -2], [2, -1]];
const KD = [[1, 0], [1, 1], [0, 1], [-1, 1], [-1, 0], [-1, -1], [0, -1], [1, -1]];
const ROOK = [[1, 0], [-1, 0], [0, 1], [0, -1]];
const BISHOP = [[1, 1], [1, -1], [-1, 1], [-1, -1]];
const rk = (s: number) => s >> 3, fl = (s: number) => s & 7;
const on = (r: number, f: number) => r >= 0 && r < 8 && f >= 0 && f < 8;
const colorOf = (p: string): Color | null => (p ? (p === p.toUpperCase() ? 'w' : 'b') : null);
export const sqName = (s: number) => 'abcdefgh'[fl(s)] + (rk(s) + 1);

export const START_FEN = 'rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1';

export function fromFen(fen: string): ChessState {
  const [pos, turn, castle, ep, half, full] = fen.split(' ');
  const b: string[] = new Array(64).fill('');
  pos.split('/').forEach((row, i) => {
    let f = 0; const r = 7 - i;
    for (const ch of row) { if (/\d/.test(ch)) f += Number(ch); else b[r * 8 + f++] = ch; }
  });
  const epSq = ep && ep !== '-' ? (Number(ep[1]) - 1) * 8 + 'abcdefgh'.indexOf(ep[0]) : -1;
  const s: ChessState = { b, turn: turn as Color, castle: castle === '-' ? '' : castle, ep: epSq, half: Number(half) || 0, full: Number(full) || 1, hist: [] };
  s.hist.push(key(s));
  return s;
}

const key = (s: ChessState) => s.b.map(p => p || '.').join('') + s.turn + s.castle + s.ep;

export function attacked(b: string[], sq: number, by: Color): boolean {
  const r = rk(sq), f = fl(sq);
  const P = by === 'w' ? 'P' : 'p', Nn = by === 'w' ? 'N' : 'n', K = by === 'w' ? 'K' : 'k';
  const RQ = by === 'w' ? ['R', 'Q'] : ['r', 'q'], BQ = by === 'w' ? ['B', 'Q'] : ['b', 'q'];
  const pr = by === 'w' ? r - 1 : r + 1;
  for (const df of [-1, 1]) if (on(pr, f + df) && b[pr * 8 + f + df] === P) return true;
  for (const [dr, df] of N) if (on(r + dr, f + df) && b[(r + dr) * 8 + f + df] === Nn) return true;
  for (const [dr, df] of KD) if (on(r + dr, f + df) && b[(r + dr) * 8 + f + df] === K) return true;
  for (const [dirs, set] of [[ROOK, RQ], [BISHOP, BQ]] as const) {
    for (const [dr, df] of dirs) {
      let rr = r + dr, ff = f + df;
      while (on(rr, ff)) {
        const p = b[rr * 8 + ff];
        if (p) { if (set.includes(p)) return true; break; }
        rr += dr; ff += df;
      }
    }
  }
  return false;
}

export function kingSquare(b: string[], c: Color) { return b.indexOf(c === 'w' ? 'K' : 'k'); }
export function inCheck(s: ChessState, c: Color = s.turn) { return attacked(s.b, kingSquare(s.b, c), c === 'w' ? 'b' : 'w'); }

function pseudo(s: ChessState): ChessMove[] {
  const out: ChessMove[] = [], b = s.b, me = s.turn, them: Color = me === 'w' ? 'b' : 'w';
  const add = (from: number, to: number) => {
    const p = b[from].toLowerCase();
    if (p === 'p' && (rk(to) === 7 || rk(to) === 0)) for (const promo of ['q', 'r', 'b', 'n'] as Promo[]) out.push({ from, to, promo });
    else out.push({ from, to });
  };
  for (let sq = 0; sq < 64; sq++) {
    const p = b[sq]; if (!p || colorOf(p) !== me) continue;
    const r = rk(sq), f = fl(sq), t = p.toLowerCase();
    if (t === 'p') {
      const dir = me === 'w' ? 1 : -1, start = me === 'w' ? 1 : 6;
      if (on(r + dir, f) && !b[(r + dir) * 8 + f]) {
        add(sq, (r + dir) * 8 + f);
        if (r === start && !b[(r + 2 * dir) * 8 + f]) out.push({ from: sq, to: (r + 2 * dir) * 8 + f });
      }
      for (const df of [-1, 1]) {
        if (!on(r + dir, f + df)) continue;
        const to = (r + dir) * 8 + f + df;
        if ((b[to] && colorOf(b[to]) === them) || to === s.ep) add(sq, to);
      }
    } else if (t === 'n' || t === 'k') {
      for (const [dr, df] of t === 'n' ? N : KD) {
        if (!on(r + dr, f + df)) continue;
        const to = (r + dr) * 8 + f + df;
        if (!b[to] || colorOf(b[to]) === them) out.push({ from: sq, to });
      }
    } else {
      const dirs = t === 'r' ? ROOK : t === 'b' ? BISHOP : [...ROOK, ...BISHOP];
      for (const [dr, df] of dirs) {
        let rr = r + dr, ff = f + df;
        while (on(rr, ff)) {
          const to = rr * 8 + ff;
          if (b[to]) { if (colorOf(b[to]) === them) out.push({ from: sq, to }); break; }
          out.push({ from: sq, to });
          rr += dr; ff += df;
        }
      }
    }
  }
  // castling
  const home = me === 'w' ? 0 : 56, K = me === 'w' ? 'K' : 'k';
  if (b[home + 4] === K && !attacked(b, home + 4, them)) {
    const ks = me === 'w' ? 'K' : 'k', qs = me === 'w' ? 'Q' : 'q';
    if (s.castle.includes(ks) && !b[home + 5] && !b[home + 6] && b[home + 7] === (me === 'w' ? 'R' : 'r') && !attacked(b, home + 5, them) && !attacked(b, home + 6, them)) out.push({ from: home + 4, to: home + 6 });
    if (s.castle.includes(qs) && !b[home + 3] && !b[home + 2] && !b[home + 1] && b[home] === (me === 'w' ? 'R' : 'r') && !attacked(b, home + 3, them) && !attacked(b, home + 2, them)) out.push({ from: home + 4, to: home + 2 });
  }
  return out;
}

/** Plays a move and returns the new position (the input is not changed). Does not check legality. */
export function make(s: ChessState, m: ChessMove, keepHistory = true): ChessState {
  const b = s.b.slice(), p = b[m.from], t = p.toLowerCase(), me = s.turn;
  let castle = s.castle, ep = -1, half = s.half + 1;
  const captured = b[m.to];
  if (t === 'p' || captured) half = 0;
  // en passant capture
  if (t === 'p' && m.to === s.ep && !captured) b[m.to + (me === 'w' ? -8 : 8)] = '';
  b[m.to] = m.promo ? (me === 'w' ? m.promo.toUpperCase() : m.promo) : p;
  b[m.from] = '';
  if (t === 'p' && Math.abs(m.to - m.from) === 16) ep = (m.to + m.from) / 2;
  if (t === 'k') {
    castle = castle.replace(me === 'w' ? /[KQ]/g : /[kq]/g, '');
    if (m.to - m.from === 2) { b[m.from + 1] = b[m.from + 3]; b[m.from + 3] = ''; }
    if (m.from - m.to === 2) { b[m.from - 1] = b[m.from - 4]; b[m.from - 4] = ''; }
  }
  for (const [sq, right] of [[0, 'Q'], [7, 'K'], [56, 'q'], [63, 'k']] as const) if (m.from === sq || m.to === sq) castle = castle.replace(right, '');
  const n: ChessState = { b, turn: me === 'w' ? 'b' : 'w', castle, ep, half, full: s.full + (me === 'b' ? 1 : 0), hist: keepHistory ? s.hist : [] };
  if (keepHistory) n.hist = [...s.hist, key(n)];
  return n;
}

export function legalMoves(s: ChessState): ChessMove[] {
  return pseudo(s).filter(m => !inCheck(make(s, m, false), s.turn));
}

export function perft(s: ChessState, depth: number): number {
  if (depth === 0) return 1;
  let n = 0;
  for (const m of legalMoves(s)) n += depth === 1 ? 1 : perft(make(s, m, false), depth - 1);
  return n;
}

export type ChessEnd = { kind: 'mate' | 'stalemate' | 'fifty' | 'repetition' | 'material'; winner: Color | null };

export function outcome(s: ChessState): ChessEnd | null {
  if (!legalMoves(s).length) return inCheck(s) ? { kind: 'mate', winner: s.turn === 'w' ? 'b' : 'w' } : { kind: 'stalemate', winner: null };
  if (s.half >= 100) return { kind: 'fifty', winner: null };
  const k = s.hist[s.hist.length - 1];
  if (s.hist.filter(x => x === k).length >= 3) return { kind: 'repetition', winner: null };
  const rest = s.b.filter(p => p && p.toLowerCase() !== 'k');
  if (!rest.length || (rest.length === 1 && /[bnBN]/.test(rest[0]))) return { kind: 'material', winner: null };
  return null;
}

/* ---------------- computer player ---------------- */

const VAL: Record<string, number> = { p: 100, n: 320, b: 330, r: 500, q: 900, k: 0 };
// simple piece-square bonuses (from white's side; mirrored for black)
const CENTER = [0, 1, 2, 3, 3, 2, 1, 0];
function pst(t: string, sq: number, c: Color) {
  const r = c === 'w' ? rk(sq) : 7 - rk(sq), f = fl(sq);
  switch (t) {
    case 'p': return r * 8 + (f >= 2 && f <= 5 ? r * 2 : 0);
    case 'n': return (CENTER[r] + CENTER[f]) * 6 - 10;
    case 'b': return (CENTER[r] + CENTER[f]) * 4;
    case 'r': return r === 6 ? 20 : 0;
    case 'q': return (CENTER[r] + CENTER[f]) * 2;
    case 'k': return r === 0 ? (f === 6 || f === 2 ? 25 : 5) : -15 * r;
  }
  return 0;
}
function evaluate(s: ChessState): number {
  let score = 0;
  s.b.forEach((p, sq) => {
    if (!p) return;
    const c = colorOf(p)!, t = p.toLowerCase();
    const v = VAL[t] + pst(t, sq, c);
    score += c === 'w' ? v : -v;
  });
  return s.turn === 'w' ? score : -score;
}
function order(s: ChessState, ms: ChessMove[]) {
  return ms.map(m => ({ m, k: (s.b[m.to] ? 10 * VAL[s.b[m.to].toLowerCase()] - VAL[s.b[m.from].toLowerCase()] : 0) + (m.promo === 'q' ? 800 : 0) }))
    .sort((a, b) => b.k - a.k).map(x => x.m);
}
function search(s: ChessState, depth: number, alpha: number, beta: number, ply: number): number {
  const ms = legalMoves(s);
  if (!ms.length) return inCheck(s) ? -100000 + ply : 0;
  if (depth === 0) return evaluate(s);
  for (const m of order(s, ms)) {
    const v = -search(make(s, m, false), depth - 1, -beta, -alpha, ply + 1);
    if (v >= beta) return beta;
    if (v > alpha) alpha = v;
  }
  return alpha;
}
export function bestMove(s: ChessState, depth = 3): ChessMove {
  // shuffle first so equally good moves vary from game to game (the ordering sort keeps that order among ties)
  const moves = legalMoves(s);
  for (let i = moves.length - 1; i > 0; i--) { const j = Math.floor(Math.random() * (i + 1)); [moves[i], moves[j]] = [moves[j], moves[i]]; }
  const ms = order(s, moves);
  let best = ms[0], bestV = -Infinity;
  for (const m of ms) {
    const v = -search(make(s, m, false), depth - 1, -Infinity, -bestV, 1);
    if (v > bestV) { bestV = v; best = m; }
  }
  return best;
}
