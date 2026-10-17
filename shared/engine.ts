// Core domino rules shared by the server, the practice mode and the client UI.
// Double-six set: 28 tiles, identified by index 0..27.

export const TILES: ReadonlyArray<readonly [number, number]> = (() => {
  const t: [number, number][] = [];
  for (let a = 0; a <= 6; a++) for (let b = a; b <= 6; b++) t.push([a, b]);
  return t;
})();

/**
 * Every way to play, across all games, following how they are played in Egypt.
 * Domino: ffa2 (فردي), ffa3, ffa4, team (زوجي).
 * Tawla: tawla (عادة, regular backgammon), mahbousa (محبوسة), tawla31 (٣١).
 * Chess: one standard game, 5 minutes each plus 2 seconds a move.
 * Ludo: classic rules for 2, 3 or 4 players.
 * (The rules engines still know gulbahar, the other chess clocks and quick/master ludo, but they are not offered.)
 */
export type Mode = 'ffa2' | 'ffa3' | 'ffa4' | 'team'
  | 'tawla' | 'mahbousa' | 'tawla31' | 'gulbahar'
  | 'chess1' | 'chess3' | 'chess' | 'chess10'
  | 'ludo2' | 'ludo3' | 'ludo4' | 'ludoq2' | 'ludoq4' | 'ludom2' | 'ludom4';
export type GameId = 'domino' | 'tawla' | 'chess' | 'ludo';
export const GAMES: GameId[] = ['domino', 'tawla', 'chess', 'ludo'];
/** The ways to play that the app offers, per game. */
export const GAME_MODES: Record<GameId, Mode[]> = {
  domino: ['ffa2', 'ffa3', 'ffa4', 'team'],
  tawla: ['tawla', 'mahbousa', 'tawla31'],
  chess: ['chess'],
  ludo: ['ludo2', 'ludo3', 'ludo4'],
};
/** Every mode the rules engines understand (including ones no longer offered). */
const ALL_MODES: Record<GameId, Mode[]> = {
  domino: ['ffa2', 'ffa3', 'ffa4', 'team'],
  tawla: ['tawla', 'mahbousa', 'tawla31', 'gulbahar'],
  chess: ['chess1', 'chess3', 'chess', 'chess10'],
  ludo: ['ludo2', 'ludo3', 'ludo4', 'ludoq2', 'ludoq4', 'ludom2', 'ludom4'],
};
export const MODES: Mode[] = GAMES.flatMap(g => GAME_MODES[g]);
export const MODE_PLAYERS: Record<Mode, number> = {
  ffa2: 2, ffa3: 3, ffa4: 4, team: 4,
  tawla: 2, mahbousa: 2, tawla31: 2, gulbahar: 2,
  chess1: 2, chess3: 2, chess: 2, chess10: 2,
  ludo2: 2, ludo3: 3, ludo4: 4, ludoq2: 2, ludoq4: 4, ludom2: 2, ludom4: 4,
};
export const gameOf = (m: Mode): GameId => (GAMES.find(g => ALL_MODES[g].includes(m)) ?? 'domino');
/** Tawla variants. */
export type TawlaVariant = 'aada' | 'mahbousa' | 'tawla31' | 'gulbahar';
export const tawlaVariant = (m: Mode): TawlaVariant => (m === 'tawla' ? 'aada' : (m as TawlaVariant));
/** Ludo variants: classic, quick (first pawn home wins, one pawn starts out), master (capture before going home). */
export type LudoVariant = 'classic' | 'quick' | 'master';
export const ludoVariant = (m: Mode): LudoVariant => (m.startsWith('ludoq') ? 'quick' : m.startsWith('ludom') ? 'master' : 'classic');
/** Chess clock per mode: minutes per player and seconds added per move. */
export const CHESS_TIME: Partial<Record<Mode, { min: number; inc: number }>> = { chess1: { min: 1, inc: 0 }, chess3: { min: 3, inc: 2 }, chess: { min: 5, inc: 2 }, chess10: { min: 10, inc: 5 } };
/** Domino plays to 51/101/151 points; tawla to 3/5/7 points (31 to 31); chess and ludo are one game. */
export const TARGETS = [51, 101, 151] as const;
export function targetOptions(m: Mode): readonly number[] {
  const g = gameOf(m);
  if (g === 'domino') return TARGETS;
  if (g === 'tawla') return m === 'tawla31' ? [31] : [3, 5, 7];
  return [1];
}
/** Egyptian defaults: 1 vs 1 and free-for-all to 101, partners to 151, tawla sets to 3, ٣١ to 31. */
export function defaultTarget(m: Mode): number {
  const g = gameOf(m);
  if (g === 'domino') return m === 'team' ? 151 : 101;
  if (g === 'tawla') return m === 'tawla31' ? 31 : 3;
  return 1;
}
export const HAND_SIZE = 7;

export type End = 'L' | 'R' | 'C';
/** One tile on an arm of the line: [tileId, inner value (touching), outer value (open)]. */
export type ArmTile = [number, number, number];

export interface Line {
  f: number | null;              // first tile id (the spinner in the middle)
  fo: [number, number] | null;   // its orientation: fo[0] faces left, fo[1] faces right
  L: ArmTile[];
  R: ArmTile[];
}

export const newLine = (): Line => ({ f: null, fo: null, L: [], R: [] });
export const isDouble = (id: number) => TILES[id][0] === TILES[id][1];
export const pips = (id: number) => TILES[id][0] + TILES[id][1];
export const pipSum = (ids: readonly number[]) => ids.reduce((s, id) => s + pips(id), 0);

export function leftVal(l: Line): number {
  return l.L.length ? l.L[l.L.length - 1][2] : (l.fo as [number, number])[0];
}
export function rightVal(l: Line): number {
  return l.R.length ? l.R[l.R.length - 1][2] : (l.fo as [number, number])[1];
}

/** Which ends a tile can be placed on. Empty array means it doesn't fit. */
export function endsFor(l: Line, id: number): End[] {
  if (l.f === null) return ['C'];
  const [a, b] = TILES[id];
  const lv = leftVal(l), rv = rightVal(l);
  const out: End[] = [];
  if (a === lv || b === lv) out.push('L');
  if (a === rv || b === rv) out.push('R');
  return out;
}

export const canPlay = (l: Line, hand: readonly number[]) => hand.some(id => endsFor(l, id).length > 0);

/** Places a tile. Throws if the move is illegal. */
export function place(l: Line, id: number, end: End): void {
  const ends = endsFor(l, id);
  if (!ends.includes(end)) throw new Error('illegal_move');
  const [a, b] = TILES[id];
  if (end === 'C') { l.f = id; l.fo = [a, b]; return; }
  const v = end === 'L' ? leftVal(l) : rightVal(l);
  l[end].push([id, v, a === v ? b : a]);
}

/** Every tile id currently on the table. */
export function lineTiles(l: Line): number[] {
  if (l.f === null) return [];
  return [l.f, ...l.L.map(x => x[0]), ...l.R.map(x => x[0])];
}

export function shuffled<T>(arr: T[], rnd: () => number = Math.random): T[] {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

export const teamOf = (mode: Mode, seat: number) => (mode === 'team' ? seat % 2 : seat);
export const teamCount = (mode: Mode) => (mode === 'team' ? 2 : MODE_PLAYERS[mode]);
