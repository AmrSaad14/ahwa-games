// Adapters that let the generic board-game table run chess, tawla and ludo.
import * as C from './chess';
import * as T from './tawla';
import * as L from './ludo';
import * as V from './tables';
import { CHESS_TIME, Mode, gameOf, ludoVariant, tawlaVariant } from '../engine';

export type BoardGameId = 'chess' | 'tawla' | 'ludo';

export type BoardAction =
  | { t: 'move'; from: number; to: number; promo?: C.Promo }   // chess
  | { t: 'roll' }                                              // tawla, ludo
  | { t: 'step'; from: number; to: number }                    // tawla (one die)
  | { t: 'pawn'; i: number }                                   // ludo
  | { t: 'pass' };                                             // forced pass with nothing to play

export interface BoardEvent {
  t: 'start' | 'move' | 'roll' | 'step' | 'pawn' | 'pass' | 'end';
  s: number;                 // seat
  from?: number; to?: number; promo?: string; capture?: boolean; check?: boolean;
  dice?: number[]; hit?: boolean; i?: number; captured?: { seat: number; pawn: number }[]; home?: boolean;
}

export interface GameEnd { winner: number; points: number; kind: string }

export interface GameDef<S> {
  game: BoardGameId;
  newGame(players: number, first: number, rnd: () => number): S;
  toMove(s: S): number;
  act(s: S, seat: number, a: BoardAction, rnd: () => number): { s: S; e: BoardEvent } | string;
  /** The move a computer player (or a timed-out player) makes. */
  auto(s: S, seat: number): BoardAction;
  /** An action the game makes by itself after a short pause (nothing to choose). */
  forced(s: S, seat: number): BoardAction | null;
  over(s: S): GameEnd | null;
  view(s: S, seat: number): unknown;
  /** Chess uses clocks instead of a per-move timer. */
  clockMs?: number;
}

/* ---------------- chess ---------------- */
export interface ChessGame { c: C.ChessState; white: number; last: { from: number; to: number } | null; captured: string[] }
export interface ChessView {
  board: string[]; turn: C.Color; you: C.Color | null; white: number;
  legal: [number, number, string?][]; last: { from: number; to: number } | null; check: number; captured: string[]; moveNo: number;
}
export const chessDef: GameDef<ChessGame> = {
  game: 'chess',
  clockMs: 5 * 60_000,
  newGame: (_n, first) => ({ c: C.fromFen(C.START_FEN), white: first, last: null, captured: [] }),
  toMove: g => (g.c.turn === 'w' ? g.white : 1 - g.white),
  act(g, seat, a) {
    if (a.t !== 'move') return 'bad_action';
    const legal = C.legalMoves(g.c).filter(m => m.from === a.from && m.to === a.to);
    if (!legal.length) return 'illegal_move';
    const m = legal.find(x => x.promo === (a.promo ?? 'q')) ?? legal[0];
    const taken = g.c.b[m.to] || (g.c.b[m.from].toLowerCase() === 'p' && m.to === g.c.ep ? (g.c.turn === 'w' ? 'p' : 'P') : '');
    const c = C.make(g.c, m);
    return { s: { c, white: g.white, last: { from: m.from, to: m.to }, captured: taken ? [...g.captured, taken] : g.captured },
      e: { t: 'move', s: seat, from: m.from, to: m.to, promo: m.promo, capture: !!taken, check: C.inCheck(c) } };
  },
  auto: g => { const m = C.bestMove(g.c, 3); return { t: 'move', from: m.from, to: m.to, promo: m.promo }; },
  forced: () => null,
  over(g) {
    const o = C.outcome(g.c); if (!o) return null;
    const winner = o.winner === null ? -1 : o.winner === 'w' ? g.white : 1 - g.white;
    return { winner, points: 1, kind: o.kind };
  },
  view(g, seat): ChessView {
    const you = seat < 0 ? null : seat === g.white ? 'w' : 'b';
    const myTurn = you === g.c.turn;
    return {
      board: g.c.b, turn: g.c.turn, you, white: g.white, last: g.last, captured: g.captured, moveNo: g.c.full,
      legal: myTurn ? C.legalMoves(g.c).filter(m => !m.promo || m.promo === 'q').map(m => (m.promo ? [m.from, m.to, 'p'] : [m.from, m.to])) as [number, number, string?][] : [],
      check: C.inCheck(g.c) ? C.kingSquare(g.c.b, g.c.turn) : -1,
    };
  },
};

/* ---------------- tawla ---------------- */
export interface TawlaView {
  pts: number[]; bar: [number, number]; off: [number, number]; turn: number; you: number;
  rolled: [number, number] | null; dice: number[]; steps: [number, number][]; pips: [number, number];
  /** عادة, محبوسة, ٣١ or جلبهار */
  variant: 'aada' | V.TablesVariant;
  /** محبوسة: per point, whose lone checker is trapped underneath (-1: none) */
  pinned?: number[];
  /** جلبهار: dice still to come in a climbing double run */
  later?: number[];
  /** جلبهار: this player is finishing the opponent's run */
  carry?: boolean;
}
export const tawlaDef: GameDef<T.TawlaState> = {
  game: 'tawla',
  newGame: (_n, first) => T.startPosition(first as 0 | 1),
  toMove: s => s.turn,
  act(s, seat, a, rnd) {
    if (a.t === 'roll') {
      if (s.rolled) return 'already_rolled';
      const n = T.roll(s, rnd);
      return { s: n, e: { t: 'roll', s: seat, dice: n.rolled! } };
    }
    if (a.t === 'step') {
      const n = T.playStep(s, a.from, a.to);
      if (!n) return 'illegal_move';
      const hit = n.bar[1 - seat] > s.bar[1 - seat];
      return { s: T.turnDone(n) && !T.winner(n) ? T.endTurn(n) : n, e: { t: 'step', s: seat, from: a.from, to: a.to, hit } };
    }
    if (a.t === 'pass') {
      if (!s.rolled || !T.turnDone(s)) return 'must_move';
      return { s: T.endTurn(s), e: { t: 'pass', s: seat, dice: s.rolled } };
    }
    return 'bad_action';
  },
  auto(s) {
    if (!s.rolled) return { t: 'roll' };
    const st = T.botStep(s);
    return st ? { t: 'step', from: st.from, to: st.to } : { t: 'pass' };
  },
  forced: s => (s.rolled && T.turnDone(s) ? { t: 'pass' } : null),
  over: s => { const w = T.winner(s); return w ? { winner: w.winner, points: w.points, kind: w.points === 2 ? 'mars' : 'win' } : null; },
  view: (s, seat): TawlaView => ({
    pts: s.pts, bar: s.bar, off: s.off, turn: s.turn, you: seat, rolled: s.rolled, dice: s.dice,
    steps: seat === s.turn ? T.nextSteps(s).map(x => [x.from, x.to] as [number, number]) : [],
    pips: [T.pipCount(s, 0), T.pipCount(s, 1)], variant: 'aada',
  }),
};

/* ---------------- محبوسة, ٣١ and جلبهار ---------------- */
function tablesDef(variant: V.TablesVariant): GameDef<V.TablesState> {
  return {
    game: 'tawla',
    newGame: (_n, first) => V.startPosition(variant, first as 0 | 1),
    toMove: s => s.turn,
    act(s, seat, a, rnd) {
      if (a.t === 'roll') {
        if (s.rolled) return 'already_rolled';
        const n = V.roll(s, rnd);
        return { s: n, e: { t: 'roll', s: seat, dice: n.rolled! } };
      }
      if (a.t === 'step') {
        if (!s.rolled) return 'must_roll';
        const n = V.playStep(s, a.from, a.to);
        if (!n) return 'illegal_move';
        const trapped = variant === 'mahbousa' && a.to !== V.OFF && n.pinned[a.to] === 1 - seat && s.pinned[a.to] !== 1 - seat;
        return { s: V.turnDone(n) && !V.winner(n) ? V.endTurn(n) : n, e: { t: 'step', s: seat, from: a.from, to: a.to, hit: trapped } };
      }
      if (a.t === 'pass') {
        if (!s.rolled || !V.turnDone(s)) return 'must_move';
        return { s: V.endTurn(s), e: { t: 'pass', s: seat, dice: s.rolled } };
      }
      return 'bad_action';
    },
    auto(s) {
      if (!s.rolled) return { t: 'roll' };
      const st = V.botStep(s);
      return st ? { t: 'step', from: st.from, to: st.to } : { t: 'pass' };
    },
    forced: s => (s.rolled && V.turnDone(s) ? { t: 'pass' } : null),
    over: s => V.winner(s),
    view: (s, seat): TawlaView => {
      const pts = s.c[0].map((a, i) => {
        const b = s.c[1][i];
        if (a && b) return s.pinned[i] === 0 ? -b : a;   // the trapping stack is on top
        return a ? a : -b;
      });
      return {
        pts, bar: [0, 0], off: s.off, turn: s.turn, you: seat, rolled: s.rolled, dice: s.dice,
        steps: seat === s.turn ? V.nextSteps(s).map(x => [x.from, x.to] as [number, number]) : [],
        pips: [V.pipCount(s, 0), V.pipCount(s, 1)], variant,
        pinned: variant === 'mahbousa' ? s.pinned : undefined,
        later: s.later.length ? s.later : undefined, carry: s.carry !== null ? true : undefined,
      };
    },
  };
}

/* ---------------- ludo ---------------- */
export interface LudoView { colors: number[]; pawns: number[][]; turn: number; die: number | null; movable: number[]; finished: number[]; you: number; variant: L.LudoVariant; captures: number[] }
function ludoDef(variant: L.LudoVariant): GameDef<L.LudoState> { return {
  game: 'ludo',
  newGame: (n, first) => ({ ...L.newLudo(n, variant), turn: first }),
  toMove: s => s.turn,
  act(s, seat, a, rnd) {
    if (a.t === 'roll') {
      if (s.die !== null) return 'already_rolled';
      const n = L.rollDie(s, rnd);
      return { s: n, e: { t: 'roll', s: seat, dice: [n.die!] } };
    }
    if (a.t === 'pawn') {
      const r = L.movePawn(s, Number(a.i));
      if (!r) return 'illegal_move';
      return { s: r.state, e: { t: 'pawn', s: seat, i: a.i, captured: r.captured, home: r.home } };
    }
    if (a.t === 'pass') {
      if (s.die === null || (L.movable(s).length && s.sixes < 3)) return 'must_move';
      return { s: L.passTurn(s.sixes >= 3 ? { ...s, die: 0 } : s), e: { t: 'pass', s: seat, dice: [s.die] } };
    }
    return 'bad_action';
  },
  auto(s) {
    if (s.die === null) return { t: 'roll' };
    const p = s.sixes >= 3 ? null : L.botPawn(s);
    return p === null ? { t: 'pass' } : { t: 'pawn', i: p };
  },
  forced(s) {
    if (s.die === null) return null;
    if (s.sixes >= 3 || !L.movable(s).length) return { t: 'pass' };
    const m = L.movable(s);
    return m.length === 1 ? { t: 'pawn', i: m[0] } : null;   // only one pawn can move: move it
  },
  over: s => { const w = L.ludoWinner(s); return w >= 0 ? { winner: w, points: 1, kind: 'win' } : null; },
  view: (s, seat): LudoView => ({
    colors: s.colors, pawns: s.pawns, turn: s.turn, die: s.die, finished: s.finished, you: seat,
    movable: seat === s.turn && s.sixes < 3 ? L.movable(s) : [], variant: s.variant, captures: s.captures,
  }),
}; }

const DEFS = new Map<Mode, GameDef<any>>();
/** The rules engine for a mode. */
export function defFor(mode: Mode): GameDef<any> {
  let d = DEFS.get(mode);
  if (!d) {
    const g = gameOf(mode);
    if (g === 'chess') d = { ...chessDef, clockMs: (CHESS_TIME[mode]?.min ?? 5) * 60_000 };
    else if (g === 'ludo') d = ludoDef(ludoVariant(mode));
    else { const v = tawlaVariant(mode); d = v === 'aada' ? tawlaDef : tablesDef(v); }
    DEFS.set(mode, d);
  }
  return d;
}
