// البصرة (Basra), the Egyptian café card game, for 2 players or 4 in two teams (partners sit opposite).
// A 52-card pack: card id = suit * 13 + (rank - 1); suits ♠ ♥ ♦ ♣ (0..3); ranks 1 (ace) … 10, 11 jack (ولد),
// 12 queen (بنت), 13 king (شايب).
//  - Deal 4 cards to each player and 4 face up on the floor (a jack or the 7♦ there goes back into the pack).
//  - On your turn play one card. It takes every floor card of its rank and, for 1…10, every group of numeral floor
//    cards adding up to it. Queens and kings only take their own rank. A card that can take must take.
//  - The jack takes the whole floor; the 7♦ (الكومي) too.
//  - Clearing the floor is a basra (بصرة): 10 points. Not with a jack, except a jack on a lone jack: 20. The 7♦
//    makes a basra only when the floor is all numerals adding up to 10 or less.
//  - When the hands are empty, 4 more each; when the pack is out, the floor goes to whoever took last.
//  - Each deal: most cards (27+) 30 points (a 26–26 tie carries the 30 to the next deal), each jack and ace 1,
//    2♣ 2, 10♦ 3, each basra 10. First side to the target (101) wins; level at the end, play another deal.

export const SUITS = ['s', 'h', 'd', 'c'] as const;
export const rankOf = (id: number) => (id % 13) + 1;
export const suitOf = (id: number) => Math.floor(id / 13);
export const SEVEN_D = 2 * 13 + 6;     // 7♦
export const TWO_C = 3 * 13 + 1;       // 2♣
export const TEN_D = 2 * 13 + 9;       // 10♦
const isNumeral = (id: number) => rankOf(id) <= 10;

export interface BasraState {
  players: number; teams: number;
  deck: number[];
  hands: number[][];
  floor: number[];
  taken: number[][];        // cards captured this deal, per team
  basras: number[];         // basra points this deal, per team (10 each, 20 for a jack on a jack)
  lastTaker: number;        // the team that took last (gets the floor at the end of the deal)
  turn: number;             // seat to play
  dealer: number;
  scores: number[];         // per team, over the deals
  carry: number;            // most-cards points carried over a 26–26 tie
  deal: number;
  target: number;
  done: boolean;
  lastDeal: { points: number[]; cards: number[]; basras: number[] } | null;   // the deal just scored, to show
}

export const teamOfSeat = (s: BasraState, seat: number) => (s.teams === 2 && s.players === 4 ? seat % 2 : seat);

function shuffle(a: number[], rnd: () => number) {
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

export function newBasra(players: number, first: number, rnd: () => number, target = 101): BasraState {
  const s: BasraState = {
    players, teams: players === 4 ? 2 : players, deck: [], hands: [], floor: [], taken: [], basras: [], lastTaker: -1,
    turn: first, dealer: (first + players - 1) % players, scores: new Array(players === 4 ? 2 : players).fill(0), carry: 0, deal: 0, target, done: false, lastDeal: null,
  };
  startDeal(s, rnd);
  return s;
}

function startDeal(s: BasraState, rnd: () => number) {
  s.deck = shuffle(Array.from({ length: 52 }, (_, i) => i), rnd);
  s.hands = Array.from({ length: s.players }, () => []);
  s.taken = Array.from({ length: s.teams }, () => []);
  s.basras = new Array(s.teams).fill(0);
  s.lastTaker = -1;
  s.deal++;
  // the floor: 4 cards, no jack and no 7♦ (those go back into the middle of the pack)
  s.floor = [];
  while (s.floor.length < 4) {
    const c = s.deck.pop()!;
    if (rankOf(c) === 11 || c === SEVEN_D) s.deck.splice(Math.floor(rnd() * Math.max(1, s.deck.length - 8)), 0, c);
    else s.floor.push(c);
  }
  dealHands(s);
  s.turn = (s.dealer + 1) % s.players;
}

function dealHands(s: BasraState) {
  for (let r = 0; r < 4; r++) for (let p = 0; p < s.players; p++) { const c = s.deck.pop(); if (c !== undefined) s.hands[p].push(c); }
}

/** The best set of floor cards a numeral can take: every card of its rank, plus as many cards as possible in
 *  groups of numerals adding up to it (groups don't share cards). */
function numeralTake(rank: number, floor: number[]): number[] {
  const same = floor.filter(c => rankOf(c) === rank);
  const rest = floor.filter(c => rankOf(c) !== rank && isNumeral(c));
  // every group of rest adding up to rank (as bit masks), then the largest packing of disjoint groups
  const groups: number[] = [];
  const n = Math.min(rest.length, 14);   // (a floor that big is rare; the first 14 numerals are searched)
  for (let m = 1; m < 1 << n; m++) {
    let sum = 0;
    for (let i = 0; i < n; i++) if (m & (1 << i)) sum += rankOf(rest[i]);
    if (sum === rank) groups.push(m);
  }
  let best = 0, bestCount = 0;
  const pop = (m: number) => { let c = 0; while (m) { m &= m - 1; c++; } return c; };
  const search = (i: number, used: number) => {
    if (pop(used) > bestCount) { bestCount = pop(used); best = used; }
    for (let k = i; k < groups.length; k++) if (!(groups[k] & used)) search(k + 1, used | groups[k]);
  };
  search(0, 0);
  return [...same, ...rest.filter((_, i) => best & (1 << i))];
}

/** What playing `card` takes from the floor, and the basra it makes (0, 10 or 20). */
export function takeFor(card: number, floor: number[]): { take: number[]; basra: number } {
  if (!floor.length) return { take: [], basra: 0 };
  const r = rankOf(card);
  if (r === 11) return { take: floor.slice(), basra: floor.length === 1 && rankOf(floor[0]) === 11 ? 20 : 0 };
  if (card === SEVEN_D) {
    const small = floor.every(isNumeral) && floor.reduce((a, c) => a + rankOf(c), 0) <= 10;
    return { take: floor.slice(), basra: small ? 10 : 0 };
  }
  const take = r >= 12 ? floor.filter(c => rankOf(c) === r) : numeralTake(r, floor);
  return { take, basra: take.length && take.length === floor.length ? 10 : 0 };
}

export interface BasraPlay { s: BasraState; took: number[]; basra: number; dealOver: boolean }

/** Plays a card. Returns an error code instead when it isn't allowed. */
export function play(s0: BasraState, seat: number, card: number, rnd: () => number): BasraPlay | string {
  if (s0.done) return 'game_over';
  if (seat !== s0.turn) return 'not_your_turn';
  if (!s0.hands[seat].includes(card)) return 'not_in_hand';
  const s: BasraState = structuredClone(s0);
  s.hands[seat] = s.hands[seat].filter(c => c !== card);
  const team = teamOfSeat(s, seat);
  const { take, basra } = takeFor(card, s.floor);
  if (take.length) {
    s.floor = s.floor.filter(c => !take.includes(c));
    s.taken[team].push(card, ...take);
    s.basras[team] += basra;
    s.lastTaker = team;
  } else s.floor.push(card);
  s.turn = (seat + 1) % s.players;
  let dealOver = false;
  if (s.hands.every(h => h.length === 0)) {
    if (s.deck.length) dealHands(s);
    else { dealOver = true; endDeal(s, rnd); }
  }
  return { s, took: take, basra, dealOver };
}

/** Card points in a pile: jacks and aces 1, 2♣ 2, 10♦ 3. */
export const cardPoints = (cards: number[]) => cards.reduce((a, c) => a + (rankOf(c) === 11 || rankOf(c) === 1 ? 1 : 0) + (c === TWO_C ? 2 : 0) + (c === TEN_D ? 3 : 0), 0);

function endDeal(s: BasraState, rnd: () => number) {
  // what's left on the floor goes to whoever took last
  if (s.floor.length && s.lastTaker >= 0) s.taken[s.lastTaker].push(...s.floor);
  s.floor = [];
  const counts = s.taken.map(t => t.length);
  const points = s.taken.map((t, i) => cardPoints(t) + s.basras[i]);
  const most = Math.max(...counts), leaders = counts.filter(c => c === most).length;
  if (leaders === 1) { points[counts.indexOf(most)] += 30 + s.carry; s.carry = 0; }
  else s.carry += 30;
  s.lastDeal = { points, cards: counts, basras: s.basras.slice() };
  for (let i = 0; i < s.teams; i++) s.scores[i] += points[i];
  const top = Math.max(...s.scores);
  if (top >= s.target && s.scores.filter(x => x === top).length === 1) { s.done = true; return; }
  s.dealer = (s.dealer + 1) % s.players;
  startDeal(s, rnd);
}

export const winnerTeam = (s: BasraState) => (s.done ? s.scores.indexOf(Math.max(...s.scores)) : -1);

/* ---------------- the computer player ---------------- */

/** How good playing `card` is: what it takes (cards, points, basras) and what it leaves the next player. */
function value(s: BasraState, seat: number, card: number): number {
  const { take, basra } = takeFor(card, s.floor);
  let v = 0;
  if (take.length) {
    v += (take.length + 1) * 1.0 + cardPoints([card, ...take]) * 3 + basra * 2.2;
    // saving the jack and the 7♦ for a fuller floor
    if (rankOf(card) === 11 || card === SEVEN_D) v -= take.length < 3 && !basra ? 6 : 0;
  } else {
    // a card thrown away: cheap ones first, never the jack, the 7♦ or a point card when it can be helped
    v -= cardPoints([card]) * 3 + (rankOf(card) === 11 ? 8 : 0) + (card === SEVEN_D ? 8 : 0);
    v -= rankOf(card) <= 10 ? rankOf(card) * 0.05 : 0.6;
  }
  // the floor it leaves: a single card or a small total invites the next player's basra
  const left = take.length ? s.floor.filter(c => !take.includes(c)) : [...s.floor, card];
  if (left.length === 1) v -= 4;
  else if (left.length && left.every(isNumeral) && left.reduce((a, c) => a + rankOf(c), 0) <= 10) v -= 2.5;
  return v;
}

export function botCard(s: BasraState, seat: number, level: 'easy' | 'normal' | 'hard' = 'normal', rnd: () => number = Math.random): number {
  const hand = s.hands[seat];
  if (level === 'easy' && rnd() < 0.5) return hand[Math.floor(rnd() * hand.length)];
  let best = hand[0], bv = -Infinity;
  for (const c of hand) { const v = value(s, seat, c) + rnd() * (level === 'hard' ? 0.2 : 1.2); if (v > bv) { bv = v; best = c; } }
  return best;
}
