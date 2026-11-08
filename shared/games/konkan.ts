// الكونكان (Konkan), the Egyptian rummy: 2 to 4 players, two packs and two jokers (106 cards).
// Card id: 0…103 = pack * 52 + suit * 13 + (rank - 1) (suits ♠ ♥ ♦ ♣, ranks 1 ace … 13 king); 104 and 105 are jokers.
//  - 14 cards each; the player after the dealer starts with 15 and only throws a card on that first turn.
//  - A turn: draw from the pack, or take the top card thrown (then it has to go into something laid down this turn),
//    lay down if you can, then throw one card.
//  - Laying down (نزول): sets of 3 or 4 cards of one rank in different suits, or runs of 3+ cards of one suit in a
//    row (the ace low A-2-3 or high Q-K-A, not round the corner); a joker stands for any card. The first time in a
//    deal, what goes down together must be worth 51 or more (2…10 their number, picture cards 10, the ace 11 or 1
//    at the bottom of a run, a joker what it stands for). After that, any meld, and cards added to anyone's melds.
//  - The first to get rid of all their cards wins the deal. The others score what's left in their hands (ace 11,
//    pictures 10, joker 25), or 100 if they never laid down. When someone reaches the target (101), the game ends:
//    the lowest score wins.

export const JOKER_A = 104, JOKER_B = 105;
export const isJoker = (id: number) => id >= 104;
export const rankOf = (id: number) => (isJoker(id) ? 0 : (id % 13) + 1);
export const suitOf = (id: number) => (isJoker(id) ? -1 : Math.floor((id % 52) / 13));

export interface Meld { owner: number; kind: 'set' | 'run'; cards: number[] }   // runs kept in order, low to high

export interface KonkanState {
  players: number;
  stock: number[];
  pile: number[];               // thrown cards; the last one is on top
  hands: number[][];
  melds: Meld[];
  opened: boolean[];            // laid down at least once this deal
  turn: number;
  stage: 'draw' | 'play';       // draw first, then lay down and throw
  took: number;                 // the card taken from the pile this turn (must be used), or -1
  dealer: number;
  teams: number;                // 2 in the team game (4 players, partners opposite), else one per player
  scores: number[];             // penalty points over the deals, per team (per player when no teams)
  deal: number;
  target: number;
  done: boolean;
  lastDeal: { winner: number; points: number[] } | null;   // points per team
}

function shuffle(a: number[], rnd: () => number) {
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

/** The team a seat plays for: partners sit opposite in the team game. */
export const konkanTeam = (s: { teams: number; players: number }, seat: number) => (s.teams === 2 && s.players === 4 ? seat % 2 : seat);

export function newKonkan(players: number, first: number, rnd: () => number, target = 101, teams = false): KonkanState {
  const nt = teams && players === 4 ? 2 : players;
  const s: KonkanState = {
    players, stock: [], pile: [], hands: [], melds: [], opened: [], turn: first, stage: 'play', took: -1,
    dealer: (first + players - 1) % players, teams: nt, scores: new Array(nt).fill(0), deal: 0, target, done: false, lastDeal: null,
  };
  startDeal(s, rnd);
  return s;
}

function startDeal(s: KonkanState, rnd: () => number) {
  s.stock = shuffle(Array.from({ length: 106 }, (_, i) => i), rnd);
  s.hands = Array.from({ length: s.players }, () => s.stock.splice(-14));
  s.turn = (s.dealer + 1) % s.players;
  s.hands[s.turn].push(s.stock.pop()!);
  s.pile = []; s.melds = []; s.opened = new Array(s.players).fill(false);
  s.stage = 'play'; s.took = -1; s.deal++;
}

/* ---------------- melds ---------------- */

/** The cards a run stands for, low to high, with each joker's place, or null when it isn't a run. */
function runShape(cards: number[]): { suit: number; lo: number; len: number } | null {
  const real = cards.filter(c => !isJoker(c)), jokers = cards.length - real.length;
  if (cards.length < 3 || !real.length) return null;
  const suit = suitOf(real[0]);
  if (real.some(c => suitOf(c) !== suit)) return null;
  // try the ace low, then high
  for (const aceHigh of [false, true]) {
    const vals = real.map(c => (rankOf(c) === 1 && aceHigh ? 14 : rankOf(c))).sort((a, b) => a - b);
    if (new Set(vals).size !== vals.length) continue;
    const span = vals[vals.length - 1] - vals[0] + 1;
    const gaps = span - vals.length;
    if (gaps > jokers) continue;
    // the spare jokers go above, then below, inside 1…14 (A low or high, never both ends)
    let lo = vals[0], len = span + (jokers - gaps);
    let hi = lo + len - 1;
    const top = aceHigh ? 14 : 13;
    if (hi > top) { lo -= hi - top; hi = top; }
    if (lo < (aceHigh ? 2 : 1)) continue;
    return { suit, lo, len };
  }
  return null;
}

export function isSet(cards: number[]): boolean {
  const real = cards.filter(c => !isJoker(c));
  if (cards.length < 3 || cards.length > 4 || !real.length) return false;
  if (real.some(c => rankOf(c) !== rankOf(real[0]))) return false;
  return new Set(real.map(suitOf)).size === real.length;
}

export const meldKind = (cards: number[]): 'set' | 'run' | null => (isSet(cards) ? 'set' : runShape(cards) ? 'run' : null);

/** The value of a meld for the 51 needed to lay down first. */
export function meldValue(cards: number[]): number {
  const v = (r: number) => (r === 1 || r === 14 ? 11 : r >= 10 ? 10 : r);
  if (isSet(cards)) { const r = rankOf(cards.find(c => !isJoker(c))!); return v(r) * cards.length; }
  const sh = runShape(cards); if (!sh) return 0;
  let t = 0;
  for (let r = sh.lo; r < sh.lo + sh.len; r++) t += r === 1 ? 1 : v(r);
  return t;
}

/** A run in order, low to high, with the jokers in their places (for showing). */
export function orderRun(cards: number[]): number[] {
  const sh = runShape(cards); if (!sh) return cards;
  const real = cards.filter(c => !isJoker(c)), jokers = cards.filter(isJoker);
  const out: number[] = [];
  for (let r = sh.lo; r < sh.lo + sh.len; r++) {
    const want = r === 14 ? 1 : r;
    const i = real.findIndex(c => rankOf(c) === want);
    out.push(i >= 0 ? real.splice(i, 1)[0] : jokers.pop()!);
  }
  return out;
}

/** Hand penalty: ace 11, pictures 10, joker 25, others their number. */
export const cardPenalty = (c: number) => (isJoker(c) ? 25 : rankOf(c) === 1 ? 11 : Math.min(10, rankOf(c)));

/* ---------------- a turn ---------------- */

export type KonkanAction =
  | { t: 'draw' }                                  // from the pack
  | { t: 'take' }                                  // the top thrown card
  | { t: 'back' }                                  // put the taken card back (it couldn't be used) and draw instead
  | { t: 'down'; melds: number[][] }               // lay down one or more melds
  | { t: 'add'; id: number; meld: number }         // add a card to a meld on the table
  | { t: 'throw'; id: number };                    // throw a card: ends the turn

export interface KonkanResult { s: KonkanState; drew?: number; took?: number; dealOver?: boolean; winner?: number }

export function act(s0: KonkanState, seat: number, a: KonkanAction, rnd: () => number): KonkanResult | string {
  if (s0.done) return 'game_over';
  if (seat !== s0.turn) return 'not_your_turn';
  const s: KonkanState = structuredClone(s0);
  const hand = s.hands[seat];
  switch (a.t) {
    case 'draw': {
      if (s.stage !== 'draw') return 'already_drew';
      if (!s.stock.length) refill(s, rnd);
      if (!s.stock.length) return 'no_cards';
      const c = s.stock.pop()!;
      hand.push(c); s.stage = 'play';
      return { s, drew: c };
    }
    case 'take': {
      if (s.stage !== 'draw') return 'already_drew';
      if (!s.pile.length) return 'no_cards';
      const c = s.pile.pop()!;
      hand.push(c); s.stage = 'play'; s.took = c;
      return { s, took: c };
    }
    case 'back': {
      if (s.took < 0 || !hand.includes(s.took)) return 'bad_action';
      s.hands[seat] = hand.filter(c => c !== s.took);
      s.pile.push(s.took); s.took = -1; s.stage = 'draw';
      return { s };
    }
    case 'down': {
      if (s.stage !== 'play') return 'draw_first';
      const melds = (a.melds ?? []).map(m => m.map(Number));
      if (!melds.length) return 'bad_meld';
      const used = melds.flat();
      if (new Set(used).size !== used.length || used.some(c => !hand.includes(c))) return 'not_in_hand';
      if (used.length >= hand.length) return 'keep_one';          // a card has to be left to throw
      for (const m of melds) if (!meldKind(m)) return 'bad_meld';
      if (!s.opened[seat] && melds.reduce((t, m) => t + meldValue(m), 0) < 51) return 'need_51';
      for (const m of melds) {
        const kind = meldKind(m)!;
        s.melds.push({ owner: seat, kind, cards: kind === 'run' ? orderRun(m) : m });
      }
      s.hands[seat] = hand.filter(c => !used.includes(c));
      s.opened[seat] = true;
      if (used.includes(s.took)) s.took = -1;
      return { s };
    }
    case 'add': {
      if (s.stage !== 'play') return 'draw_first';
      if (!s.opened[seat]) return 'lay_down_first';
      const id = Number(a.id), m = s.melds[Number(a.meld)];
      if (!m) return 'bad_meld';
      if (!hand.includes(id)) return 'not_in_hand';
      if (hand.length <= 1) return 'keep_one';
      const cards = [...m.cards, id];
      if (meldKind(cards) !== m.kind) return 'bad_meld';
      m.cards = m.kind === 'run' ? orderRun(cards) : cards;
      s.hands[seat] = hand.filter(c => c !== id);
      if (id === s.took) s.took = -1;
      return { s };
    }
    case 'throw': {
      if (s.stage !== 'play') return 'draw_first';
      const id = Number(a.id);
      if (!hand.includes(id)) return 'not_in_hand';
      if (s.took >= 0) return 'use_taken';
      s.hands[seat] = hand.filter(c => c !== id);
      s.pile.push(id);
      if (!s.hands[seat].length) { endDeal(s, seat, rnd); return { s, dealOver: true, winner: seat }; }
      s.turn = (seat + 1) % s.players; s.stage = 'draw'; s.took = -1;
      return { s };
    }
  }
  return 'bad_action';
}

/** The pack ran out: the thrown cards (but the top one) are shuffled into a new pack. */
function refill(s: KonkanState, rnd: () => number) {
  if (s.pile.length < 2) return;
  const top = s.pile.pop()!;
  s.stock = shuffle(s.pile, rnd); s.pile = [top];
}

function endDeal(s: KonkanState, winner: number, rnd: () => number) {
  // what each player is left holding (100 if they never laid down); in the team game the side that went out
  // scores nothing, the other side both its hands
  const wt = konkanTeam(s, winner);
  const points = new Array(s.teams).fill(0);
  s.hands.forEach((h, i) => {
    const t = konkanTeam(s, i);
    if (t === wt) return;
    points[t] += !s.opened[i] ? 100 : h.reduce((a, c) => a + cardPenalty(c), 0);
  });
  for (let i = 0; i < s.teams; i++) s.scores[i] += points[i];
  s.lastDeal = { winner, points };
  if (Math.max(...s.scores) >= s.target) { s.done = true; return; }
  s.dealer = (s.dealer + 1) % s.players;
  startDeal(s, rnd);
}

/** The lowest score wins: the seat (or, in the team game, the team). A tie: whoever won the last deal, else the
 *  first in order. */
export function winnerSeat(s: KonkanState): number {
  if (!s.done) return -1;
  const low = Math.min(...s.scores);
  if (s.lastDeal) { const t = konkanTeam(s, s.lastDeal.winner); if (s.scores[t] === low) return t; }
  return s.scores.indexOf(low);
}

/* ---------------- the computer player ---------------- */

/** Every meld that can be made from these cards (sets and runs, jokers filling in). */
export function candidates(hand: number[]): number[][] {
  const jokers = hand.filter(isJoker), real = hand.filter(c => !isJoker(c));
  const out: number[][] = [];
  // sets: one card per suit of a rank, 2…4 real cards, jokers up to 4
  for (let r = 1; r <= 13; r++) {
    const bySuit = [0, 1, 2, 3].map(x => real.find(c => rankOf(c) === r && suitOf(c) === x)).filter((c): c is number => c !== undefined);
    const n = bySuit.length;
    for (let m = 1; m < 1 << n; m++) {
      const pick = bySuit.filter((_, i) => m & (1 << i));
      for (let j = 0; j <= Math.min(jokers.length, 4 - pick.length); j++) {
        const cards = [...pick, ...jokers.slice(0, j)];
        if (cards.length >= 3 && cards.length <= 4 && pick.length >= 2 - (j > 0 ? 1 : 0)) out.push(cards);
      }
    }
  }
  // runs: per suit, every stretch (ace low or high) with jokers filling the holes
  for (let suit = 0; suit < 4; suit++) {
    const at = new Map<number, number>();
    for (const c of real) if (suitOf(c) === suit && !at.has(rankOf(c))) at.set(rankOf(c), c);
    const card = (v: number) => at.get(v === 14 ? 1 : v);
    for (let lo = 1; lo <= 12; lo++) {
      let need = 0; const cards: number[] = [];
      for (let hi = lo; hi <= 14; hi++) {
        if (hi === 14 && lo === 1) break;
        const c = card(hi);
        if (c === undefined) { need++; if (need > jokers.length) break; } else cards.push(c);
        if (hi - lo + 1 >= 3 && cards.length >= 2) out.push([...cards, ...jokers.slice(0, need)]);
      }
    }
  }
  return out.filter(m => meldKind(m));
}

/** The best way to lay these cards down: disjoint melds, most value first (searched, the hand is small). */
export function bestMelds(hand: number[], keep = 1): { melds: number[][]; value: number; cards: number } {
  const cand = candidates(hand).map(m => ({ m, v: meldValue(m) })).sort((a, b) => b.v - a.v).slice(0, 60);
  let best = { melds: [] as number[][], value: 0, cards: 0 };
  let steps = 0;
  const go = (i: number, used: Set<number>, chosen: number[][], value: number) => {
    if (++steps > 40000) return;
    const cards = used.size;
    if (cards <= hand.length - keep && (value > best.value || (value === best.value && cards > best.cards))) best = { melds: chosen.slice(), value, cards };
    for (let k = i; k < cand.length; k++) {
      const { m, v } = cand[k];
      if (m.some(c => used.has(c))) continue;
      if (used.size + m.length > hand.length - keep) continue;
      m.forEach(c => used.add(c)); chosen.push(m);
      go(k + 1, used, chosen, value + v);
      chosen.pop(); m.forEach(c => used.delete(c));
    }
  };
  go(0, new Set(), [], 0);
  return best;
}

/** How useful a card is to keep: jokers most, then cards close to others of the same rank or suit. */
function keepValue(hand: number[], c: number): number {
  if (isJoker(c)) return 100;
  let v = 0;
  for (const o of hand) {
    if (o === c || isJoker(o)) continue;
    if (rankOf(o) === rankOf(c) && suitOf(o) !== suitOf(c)) v += 3;
    if (suitOf(o) === suitOf(c)) { const d = Math.abs(rankOf(o) - rankOf(c)); if (d === 1) v += 3; else if (d === 2) v += 1.5; }
  }
  return v - cardPenalty(c) * 0.12;
}

/** A card from the hand that fits a meld on the table (and that meld's index), if any. */
export function fitOnTable(s: KonkanState, hand: number[]): { id: number; meld: number } | null {
  for (const id of hand) {
    if (isJoker(id)) continue;
    for (let i = 0; i < s.melds.length; i++) {
      const m = s.melds[i];
      if (meldKind([...m.cards, id]) === m.kind) return { id, meld: i };
    }
  }
  return null;
}

export function botAction(s: KonkanState, seat: number, level: 'easy' | 'normal' | 'hard' = 'normal', rnd: () => number = Math.random): KonkanAction {
  const hand = s.hands[seat];
  if (s.stage === 'draw') {
    const top = s.pile[s.pile.length - 1];
    if (top !== undefined && level !== 'easy') {
      // take the thrown card when it lets us lay it down this turn
      const before = bestMelds(hand), after = bestMelds([...hand, top]);
      const usesIt = after.melds.some(m => m.includes(top));
      if (usesIt && after.value > before.value && (s.opened[seat] || after.value >= 51)) return { t: 'take' };
    }
    return { t: 'draw' };
  }
  // lay down
  const b = bestMelds(hand);
  const mustUse = s.took >= 0;
  // took the thrown card but can't lay it down after all (a player who timed out): put it back
  if (mustUse && !(b.melds.some(m => m.includes(s.took)) && (s.opened[seat] || b.value >= 51))) {
    const f = s.opened[seat] && hand.length > 1 ? fitOnTable(s, [s.took]) : null;
    return f ? { t: 'add', id: f.id, meld: f.meld } : { t: 'back' };
  }
  if (b.melds.length && (s.opened[seat] ? true : b.value >= 51)) {
    // a careful player keeps quiet until they can lay down most of their hand (hard), others go down at once
    if (!(level === 'hard' && !s.opened[seat] && !mustUse && hand.length - b.cards > 5 && rnd() < 0.5)) return { t: 'down', melds: b.melds };
  }
  if (s.opened[seat] && hand.length > 1) { const f = fitOnTable(s, hand); if (f) return { t: 'add', id: f.id, meld: f.meld }; }
  // throw the least useful card (never the one just taken: that has to be used first)
  const pool = hand.filter(c => c !== s.took);
  if (level === 'easy' && rnd() < 0.35) return { t: 'throw', id: pool[Math.floor(rnd() * pool.length)] };
  let worst = pool[0], wv = Infinity;
  for (const c of pool) { const v = keepValue(hand, c) + (level === 'hard' ? 0 : rnd() * 0.8); if (v < wv) { wv = v; worst = c; } }
  return { t: 'throw', id: worst };
}
