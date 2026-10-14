import { End, Line, TILES, endsFor, isDouble, leftVal, lineTiles, rightVal } from './engine';

export type BotMove = { t: 'play'; id: number; e: End } | { t: 'draw' } | { t: 'pass' };

/**
 * Heuristic computer player.
 * - Gets rid of heavy tiles and doubles early (they are hard to place later).
 * - Keeps open ends on numbers it still holds, so it is less likely to get stuck.
 * - Prefers leaving ends on numbers that are mostly played already (opponents are unlikely to hold them).
 * - In team games, avoids closing a number its partner just played on when it has a choice.
 */
export function chooseMove(line: Line, hand: readonly number[], yardCount: number, level: 'easy' | 'normal' = 'normal', partnerLast?: number): BotMove {
  const options: { id: number; e: End }[] = [];
  for (const id of hand) for (const e of endsFor(line, id)) options.push({ id, e });
  if (!options.length) return yardCount > 0 ? { t: 'draw' } : { t: 'pass' };
  if (level === 'easy') {
    const o = options[Math.floor(Math.random() * options.length)];
    return { t: 'play', ...o };
  }
  // how many of each number are already visible (on the table or in my hand)
  const seen = new Array(7).fill(0);
  for (const id of [...lineTiles(line), ...hand]) { const [a, b] = TILES[id]; seen[a]++; if (a !== b) seen[b]++; }
  const holdCount = (v: number, without: number) => hand.filter(id => id !== without && (TILES[id][0] === v || TILES[id][1] === v)).length;

  let best = options[0], bestScore = -Infinity;
  for (const o of options) {
    const [a, b] = TILES[o.id];
    let openNew: number, otherEnd: number;
    if (o.e === 'C') { openNew = b; otherEnd = a; }
    else {
      const v = o.e === 'L' ? leftVal(line) : rightVal(line);
      openNew = a === v ? b : a;
      otherEnd = o.e === 'L' ? rightVal(line) : leftVal(line);
    }
    let s = (a + b) * 1.0;
    if (isDouble(o.id)) s += 6;
    s += holdCount(openNew, o.id) * 3 + holdCount(otherEnd, o.id) * 1.5;
    s += seen[openNew] * 1.2;                       // ends that others probably can't match
    if (partnerLast !== undefined && o.e !== 'C') {
      const v = o.e === 'L' ? leftVal(line) : rightVal(line);
      if (v === partnerLast) s -= 2;               // leave the partner's number open
    }
    s += Math.random() * 1.5;
    if (s > bestScore) { bestScore = s; best = o; }
  }
  return { t: 'play', ...best };
}
