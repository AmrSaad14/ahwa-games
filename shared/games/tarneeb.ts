// الطرنيب (Tarneeb), the trick-taking card game played in Egyptian cafés: 4 players in two teams (partners sit
// opposite), 13 cards each from a 52-card pack. Card id = suit * 13 + (rank - 1), suits ♠ ♥ ♦ ♣ (0..3), the ace high.
//  - Bidding, starting after the dealer: bid 7…13 tricks, higher than the bid before, or pass (a pass is for the
//    deal). It ends when the other three have passed, or at a bid of 13. If all four pass, the dealer must take 7.
//  - The winner names the trump suit (الطرنيب) and leads the first trick.
//  - Follow suit when you can; otherwise play anything. The highest trump takes the trick, else the highest card
//    of the suit led. Whoever takes a trick leads the next.
//  - After 13 tricks: a team that made its bid scores the tricks it took (a bid of 13 made scores 26); a team that
//    failed loses its bid, and the other team scores the tricks it took. First team to the target (41) wins; both
//    over it, the higher score.

export const rankOf = (id: number) => (id % 13) + 1;
export const suitOf = (id: number) => Math.floor(id / 13);
/** The ace is high. */
export const power = (id: number) => (rankOf(id) === 1 ? 14 : rankOf(id));

export type TarneebPhase = 'bid' | 'trump' | 'play';

export interface TarneebState {
  hands: number[][];
  phase: TarneebPhase;
  dealer: number;
  turn: number;
  bids: (number | null)[];      // per seat this deal: null not yet, 0 passed, 7..13 the bid
  bid: number;                  // the highest bid
  bidder: number;               // who made it (-1 none yet)
  trump: number;                // -1 until named
  trick: { seat: number; card: number }[];
  lastTrick: { seat: number; card: number }[] | null;
  lastWinner: number;
  tricks: number[];             // per team this deal
  scores: number[];             // per team over the deals
  deal: number;
  target: number;
  done: boolean;
  lastDeal: { bid: number; bidder: number; tricks: number[]; points: number[]; made: boolean } | null;
}

export const teamOfSeat = (seat: number) => seat % 2;

function shuffle(a: number[], rnd: () => number) {
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

/** A hand sorted the way people hold it: by suit, high to low. */
export const sortHand = (h: number[]) => h.sort((a, b) => suitOf(a) - suitOf(b) || power(b) - power(a));

export function newTarneeb(first: number, rnd: () => number, target = 41): TarneebState {
  const s: TarneebState = {
    hands: [], phase: 'bid', dealer: (first + 3) % 4, turn: first, bids: [null, null, null, null], bid: 0, bidder: -1, trump: -1,
    trick: [], lastTrick: null, lastWinner: -1, tricks: [0, 0], scores: [0, 0], deal: 0, target, done: false, lastDeal: null,
  };
  startDeal(s, rnd);
  return s;
}

function startDeal(s: TarneebState, rnd: () => number) {
  const deck = shuffle(Array.from({ length: 52 }, (_, i) => i), rnd);
  s.hands = [0, 1, 2, 3].map(p => sortHand(deck.slice(p * 13, p * 13 + 13)));
  s.phase = 'bid'; s.bids = [null, null, null, null]; s.bid = 0; s.bidder = -1; s.trump = -1;
  s.trick = []; s.tricks = [0, 0]; s.deal++;
  s.turn = (s.dealer + 1) % 4;
}

/** The cards `seat` may play now. */
export function legal(s: TarneebState, seat: number): number[] {
  const h = s.hands[seat];
  if (s.phase !== 'play' || seat !== s.turn) return [];
  if (!s.trick.length) return h.slice();
  const led = suitOf(s.trick[0].card);
  const follow = h.filter(c => suitOf(c) === led);
  return follow.length ? follow : h.slice();
}

/** Who is taking the trick so far. */
export function trickWinner(trick: { seat: number; card: number }[], trump: number): number {
  const led = suitOf(trick[0].card);
  let best = trick[0];
  for (const p of trick.slice(1)) {
    const a = suitOf(p.card), b = suitOf(best.card);
    if (a === b ? power(p.card) > power(best.card) : a === trump) best = p;
  }
  return best.seat;
}

export type TarneebAction = { t: 'bid'; n: number } | { t: 'trump'; suit: number } | { t: 'card'; id: number };
export interface TarneebResult { s: TarneebState; trick?: { winner: number; cards: { seat: number; card: number }[] }; dealOver?: boolean }

export function act(s0: TarneebState, seat: number, a: TarneebAction, rnd: () => number): TarneebResult | string {
  if (s0.done) return 'game_over';
  if (seat !== s0.turn) return 'not_your_turn';
  const s: TarneebState = structuredClone(s0);
  if (a.t === 'bid') {
    if (s.phase !== 'bid') return 'bad_action';
    const n = Math.floor(Number(a.n));
    if (n !== 0 && (n < 7 || n > 13 || n <= s.bid)) return 'bad_bid';
    const passes = s.bids.filter(b => b === 0).length;
    // all three others passed and no bid yet: the dealer has to take it
    if (n === 0 && passes === 3 && s.bidder < 0) return 'must_bid';
    s.bids[seat] = n;
    if (n) { s.bid = n; s.bidder = seat; }
    const out = s.bids.filter(b => b === 0).length;
    if (s.bid === 13 || (s.bidder >= 0 && out === 3)) { s.phase = 'trump'; s.turn = s.bidder; return { s }; }
    // the next player still in the bidding
    let t = seat;
    do t = (t + 1) % 4; while (s.bids[t] === 0);
    s.turn = t;
    return { s };
  }
  if (a.t === 'trump') {
    if (s.phase !== 'trump') return 'bad_action';
    const suit = Math.floor(Number(a.suit));
    if (suit < 0 || suit > 3) return 'bad_suit';
    s.trump = suit; s.phase = 'play'; s.turn = s.bidder;
    return { s };
  }
  if (a.t !== 'card') return 'bad_action';
  if (s.phase !== 'play') return 'bad_action';
  const card = Number(a.id);
  if (!s.hands[seat].includes(card)) return 'not_in_hand';
  if (!legal(s, seat).includes(card)) return 'must_follow';
  s.hands[seat] = s.hands[seat].filter(c => c !== card);
  s.trick.push({ seat, card });
  if (s.trick.length < 4) { s.turn = (seat + 1) % 4; return { s }; }
  const winner = trickWinner(s.trick, s.trump);
  const cards = s.trick;
  s.tricks[teamOfSeat(winner)]++;
  s.lastTrick = cards; s.lastWinner = winner; s.trick = []; s.turn = winner;
  if (s.hands.every(h => !h.length)) { endDeal(s, rnd); return { s, trick: { winner, cards }, dealOver: true }; }
  return { s, trick: { winner, cards } };
}

function endDeal(s: TarneebState, rnd: () => number) {
  const bt = teamOfSeat(s.bidder), ot = 1 - bt;
  const made = s.tricks[bt] >= s.bid;
  const points = [0, 0];
  if (made) points[bt] = s.bid === 13 ? 26 : s.tricks[bt];
  else { points[bt] = -s.bid; points[ot] = s.tricks[ot]; }
  s.scores[0] += points[0]; s.scores[1] += points[1];
  s.lastDeal = { bid: s.bid, bidder: s.bidder, tricks: s.tricks.slice(), points, made };
  const top = Math.max(...s.scores);
  if (top >= s.target && s.scores[0] !== s.scores[1]) { s.done = true; return; }
  s.dealer = (s.dealer + 1) % 4;
  startDeal(s, rnd);
}

export const winnerTeam = (s: TarneebState) => (s.done ? (s.scores[0] > s.scores[1] ? 0 : 1) : -1);

/* ---------------- the computer player ---------------- */

/** About how many tricks a hand takes with the given trump (counting a little on the partner). */
export function handTricks(h: number[], trump: number): number {
  let t = 0;
  for (let suit = 0; suit < 4; suit++) {
    const cs = h.filter(c => suitOf(c) === suit).map(power).sort((a, b) => b - a);
    if (!cs.length) { if (suit !== trump) t += h.some(c => suitOf(c) === trump) ? 0.8 : 0; continue; }
    if (cs[0] === 14) t += 1;
    if (cs.includes(13) && cs.length >= 2) t += cs[0] === 14 ? 0.9 : 0.6;
    if (cs.includes(12) && cs.length >= 3) t += 0.35;
    if (suit === trump) t += Math.max(0, cs.length - 3) * 0.9 + (cs.includes(11) && cs.length >= 4 ? 0.3 : 0);
  }
  return t;
}
export const bestTrump = (h: number[]) => [0, 1, 2, 3].reduce((b, x) => (handTricks(h, x) + h.filter(c => suitOf(c) === x).length * 0.05 > handTricks(h, b) + h.filter(c => suitOf(c) === b).length * 0.05 ? x : b), 0);

export function botAction(s: TarneebState, seat: number, level: 'easy' | 'normal' | 'hard' = 'normal', rnd: () => number = Math.random): TarneebAction {
  const h = s.hands[seat];
  if (s.phase === 'bid') {
    const est = handTricks(h, bestTrump(h)) + (level === 'hard' ? 3.2 : 3) + (level === 'easy' ? (rnd() - 0.5) * 2 : 0);
    // the partner bid: count on them a bit more
    const partner = (seat + 2) % 4, pb = s.bids[partner] ?? 0;
    const want = Math.floor(est + (pb ? 0.6 : 0));
    const passes = s.bids.filter(b => b === 0).length;
    if (passes === 3 && s.bidder < 0) return { t: 'bid', n: 7 };
    if (want >= 7 && want > s.bid && !(s.bidder === partner && want < s.bid + 2)) return { t: 'bid', n: Math.max(s.bid + 1, 7) };
    return { t: 'bid', n: 0 };
  }
  if (s.phase === 'trump') return { t: 'trump', suit: bestTrump(h) };
  const ok = legal(s, seat);
  if (level === 'easy' && rnd() < 0.4) return { t: 'card', id: ok[Math.floor(rnd() * ok.length)] };
  return { t: 'card', id: chooseCard(s, seat, ok) };
}

function chooseCard(s: TarneebState, seat: number, ok: number[]): number {
  const low = (cs: number[]) => cs.reduce((a, b) => (power(b) < power(a) ? b : a));
  const high = (cs: number[]) => cs.reduce((a, b) => (power(b) > power(a) ? b : a));
  const nonTrump = ok.filter(c => suitOf(c) !== s.trump);
  if (!s.trick.length) {
    // leading: an ace, else trumps when the team bid and has length, else a low card of the longest side suit
    const aces = nonTrump.filter(c => power(c) === 14);
    if (aces.length) return aces[0];
    const trumps = ok.filter(c => suitOf(c) === s.trump);
    if (teamOfSeat(seat) === teamOfSeat(s.bidder) && trumps.length >= 4) return high(trumps);
    const pool = nonTrump.length ? nonTrump : ok;
    const bySuit = [0, 1, 2, 3].map(x => pool.filter(c => suitOf(c) === x)).filter(x => x.length).sort((a, b) => b.length - a.length);
    return low(bySuit[0]);
  }
  const winning = trickWinner(s.trick, s.trump);
  const partnerWins = teamOfSeat(winning) === teamOfSeat(seat);
  const beats = ok.filter(c => trickWinner([...s.trick, { seat, card: c }], s.trump) === seat);
  const last = s.trick.length === 3;
  if (partnerWins) {
    // the partner has it: keep the good cards (unless last and the partner's card could still be beaten — it can't)
    const cheap = nonTrump.length ? nonTrump : ok;
    return low(cheap);
  }
  if (beats.length) {
    // take it as cheaply as possible (last to play) or with something safe
    if (last) return low(beats);
    const top = high(beats);
    return power(top) === 14 || suitOf(top) === s.trump ? low(beats) : top;
  }
  const cheap = nonTrump.length ? nonTrump : ok;
  return low(cheap);
}
