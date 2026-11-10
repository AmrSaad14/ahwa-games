// إستيميشن (Estimation), the trick-taking card game Egyptians play in cafés: 4 players, each for themselves,
// 13 cards each from a 52-card pack. Card id = suit * 13 + (rank - 1), suits ♠ ♥ ♦ ♣ (0..3), the ace high
// (the same cards as tarneeb).
//  1. The call (البيد): starting after the dealer, each player calls a number of tricks (4…13) with a trump, or
//     passes (a pass is for the deal). A call must be higher than the one before: more tricks, or as many with a
//     higher trump. Trumps from low to high: ♣ ♦ ♥ ♠, then صن (no trump). The call ends when the other three have
//     passed (or at 13 صن). If all four pass, the cards are dealt again by the next dealer.
//  2. The estimates (التوقّع): the other three, in turn after the caller, say how many tricks they'll take, from 0
//     to the caller's number. The last to speak can't make the four numbers add up to 13 (someone has to miss).
//  3. The play: the caller leads. Follow suit when you can; otherwise play anything. The highest trump takes the
//     trick, else the highest card of the suit led. Whoever takes a trick leads the next.
//  4. The score, per player: exactly what you said: 10 + that number (the caller 10 more). Missed (over or under):
//     minus the difference (the caller: minus 10 and the difference). Only one player got theirs: 10 more for them;
//     only one missed: 10 less for them.
//  After the deals (5), the highest score wins; tied at the top, one more deal until someone is ahead.
import { rankOf, suitOf, power, sortHand, trickWinner as tnWinner, handTricks } from './tarneeb';

export { rankOf, suitOf, power };

/** No trump (صن). */
export const SANS = 4;
/** How high a trump is in the call: ♣ ♦ ♥ ♠ صن. */
export const trumpRank = (suit: number) => (suit === SANS ? 4 : 3 - suit);

export type EstimationPhase = 'call' | 'est' | 'play';
export interface Call { n: number; suit: number }

export interface EstimationState {
  hands: number[][];
  phase: EstimationPhase;
  dealer: number;
  turn: number;
  calls: (Call | 0 | null)[];   // per seat this deal: null not yet, 0 passed, or the call
  bid: number;                  // the caller's number (0 until someone calls)
  suit: number;                 // the caller's trump (SANS for none; -1 until someone calls)
  caller: number;               // -1 none yet
  ests: (number | null)[];      // per seat: what they said they'd take (the caller's is the bid)
  trick: { seat: number; card: number }[];
  lastTrick: { seat: number; card: number }[] | null;
  lastWinner: number;
  tricks: number[];             // per seat this deal
  scores: number[];             // per seat over the deals
  deal: number;
  deals: number;                // how many deals the game is
  done: boolean;
  lastDeal: { caller: number; bid: number; suit: number; ests: number[]; tricks: number[]; points: number[] } | null;
}

function shuffle(a: number[], rnd: () => number) {
  for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; }
  return a;
}

export function newEstimation(first: number, rnd: () => number, deals = 5): EstimationState {
  const s: EstimationState = {
    hands: [], phase: 'call', dealer: (first + 3) % 4, turn: first, calls: [null, null, null, null], bid: 0, suit: -1, caller: -1,
    ests: [null, null, null, null], trick: [], lastTrick: null, lastWinner: -1, tricks: [0, 0, 0, 0], scores: [0, 0, 0, 0],
    deal: 0, deals, done: false, lastDeal: null,
  };
  startDeal(s, rnd);
  return s;
}

function startDeal(s: EstimationState, rnd: () => number) {
  const deck = shuffle(Array.from({ length: 52 }, (_, i) => i), rnd);
  s.hands = [0, 1, 2, 3].map(p => sortHand(deck.slice(p * 13, p * 13 + 13)));
  s.phase = 'call'; s.calls = [null, null, null, null]; s.bid = 0; s.suit = -1; s.caller = -1;
  s.ests = [null, null, null, null]; s.trick = []; s.lastTrick = null; s.lastWinner = -1; s.tricks = [0, 0, 0, 0]; s.deal++;
  s.turn = (s.dealer + 1) % 4;
}

/** Whether a call beats the one standing. */
export const beats = (n: number, suit: number, bid: number, cur: number) => n > bid || (n === bid && cur >= 0 && trumpRank(suit) > trumpRank(cur));

/** The numbers the player to estimate may say (the last one can't make the total 13). */
export function estOptions(s: EstimationState, seat: number): number[] {
  if (s.phase !== 'est' || seat !== s.turn) return [];
  const said = s.ests.reduce<number>((a, x) => a + (x ?? 0), 0);
  const left = s.ests.filter(x => x === null).length;
  const out: number[] = [];
  for (let n = 0; n <= s.bid; n++) if (!(left === 1 && said + n === 13)) out.push(n);
  return out;
}

/** The cards `seat` may play now. */
export function legal(s: EstimationState, seat: number): number[] {
  const h = s.hands[seat];
  if (s.phase !== 'play' || seat !== s.turn) return [];
  if (!s.trick.length) return h.slice();
  const led = suitOf(s.trick[0].card);
  const follow = h.filter(c => suitOf(c) === led);
  return follow.length ? follow : h.slice();
}

/** Who is taking the trick so far (no trump: the highest of the suit led). */
export const trickWinner = (trick: { seat: number; card: number }[], suit: number) => tnWinner(trick, suit === SANS ? -1 : suit);

export type EstimationAction = { t: 'call'; n: number; suit?: number } | { t: 'est'; n: number } | { t: 'card'; id: number };
export interface EstimationResult { s: EstimationState; trick?: { winner: number; cards: { seat: number; card: number }[] }; dealOver?: boolean; redeal?: boolean }

export function act(s0: EstimationState, seat: number, a: EstimationAction, rnd: () => number): EstimationResult | string {
  if (s0.done) return 'game_over';
  if (seat !== s0.turn) return 'not_your_turn';
  const s: EstimationState = structuredClone(s0);
  if (a.t === 'call') {
    if (s.phase !== 'call') return 'bad_action';
    const n = Math.floor(Number(a.n));
    if (n === 0) {
      s.calls[seat] = 0;
      const passed = s.calls.filter(c => c === 0).length;
      if (passed === 4) { s.dealer = (s.dealer + 1) % 4; startDeal(s, rnd); return { s, redeal: true }; }
      if (s.caller >= 0 && passed === 3) { toEstimates(s); return { s }; }
    } else {
      const suit = Math.floor(Number(a.suit));
      if (!(suit >= 0 && suit <= SANS) || n < 4 || n > 13) return 'bad_call';
      if (!beats(n, suit, s.bid, s.suit)) return 'call_higher';
      s.calls[seat] = { n, suit }; s.bid = n; s.suit = suit; s.caller = seat;
      if (n === 13 && suit === SANS) { toEstimates(s); return { s }; }
      if (s.calls.filter(c => c === 0).length === 3) { toEstimates(s); return { s }; }
    }
    let t = seat;
    do t = (t + 1) % 4; while (s.calls[t] === 0);
    s.turn = t;
    return { s };
  }
  if (a.t === 'est') {
    if (s.phase !== 'est') return 'bad_action';
    const n = Math.floor(Number(a.n));
    if (!estOptions(s, seat).includes(n)) return n >= 0 && n <= s.bid ? 'not_13' : 'bad_est';
    s.ests[seat] = n;
    const next = s.ests.findIndex((_, i) => s.ests[(seat + 1 + i) % 4] === null);
    if (next < 0) { s.phase = 'play'; s.turn = s.caller; }
    else s.turn = (seat + 1 + next) % 4;
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
  const winner = trickWinner(s.trick, s.suit);
  const cards = s.trick;
  s.tricks[winner]++;
  s.lastTrick = cards; s.lastWinner = winner; s.trick = []; s.turn = winner;
  if (s.hands.every(h => !h.length)) { endDeal(s, rnd); return { s, trick: { winner, cards }, dealOver: true }; }
  return { s, trick: { winner, cards } };
}

function toEstimates(s: EstimationState) {
  s.phase = 'est';
  s.ests = [null, null, null, null];
  s.ests[s.caller] = s.bid;
  s.turn = (s.caller + 1) % 4;
}

/** The points each player gets for a deal. */
export function dealPoints(ests: number[], tricks: number[], caller: number): number[] {
  const made = ests.map((e, i) => tricks[i] === e);
  const pts = ests.map((e, i) => {
    const diff = Math.abs(tricks[i] - e);
    if (made[i]) return 10 + e + (i === caller ? 10 : 0);
    return -(diff + (i === caller ? 10 : 0));
  });
  const winners = made.filter(Boolean).length;
  if (winners === 1) pts[made.indexOf(true)] += 10;
  if (winners === 3) pts[made.indexOf(false)] -= 10;
  return pts;
}

function endDeal(s: EstimationState, rnd: () => number) {
  const ests = s.ests.map(x => x ?? 0);
  const points = dealPoints(ests, s.tricks, s.caller);
  for (let i = 0; i < 4; i++) s.scores[i] += points[i];
  s.lastDeal = { caller: s.caller, bid: s.bid, suit: s.suit, ests, tricks: s.tricks.slice(), points };
  const top = Math.max(...s.scores);
  if (s.deal >= s.deals && s.scores.filter(x => x === top).length === 1) { s.done = true; return; }
  s.dealer = (s.dealer + 1) % 4;
  startDeal(s, rnd);
}

export const winnerSeat = (s: EstimationState) => (s.done ? s.scores.indexOf(Math.max(...s.scores)) : -1);

/* ---------------- the computer player ---------------- */

/** About how many tricks a hand takes with a trump (SANS: none), playing alone. */
export function expected(h: number[], suit: number): number {
  if (suit !== SANS) return Math.max(0, handTricks(h, suit) - 0.3);
  let t = 0;
  for (let x = 0; x < 4; x++) {
    const cs = h.filter(c => suitOf(c) === x).map(power).sort((a, b) => b - a);
    if (!cs.length) continue;
    if (cs[0] === 14) t += 1;
    if (cs.includes(13) && cs.length >= 2) t += cs[0] === 14 ? 0.85 : 0.5;
    if (cs.includes(12) && cs.length >= 3) t += 0.3;
    if (cs.length >= 5 && cs[0] >= 13) t += (cs.length - 4) * 0.6;   // a long strong suit runs at the end
  }
  return t;
}

export function botAction(s: EstimationState, seat: number, level: 'easy' | 'normal' | 'hard' = 'normal', rnd: () => number = Math.random): EstimationAction {
  const h = s.hands[seat];
  const fuzz = level === 'easy' ? (rnd() - 0.5) * 2 : 0;
  if (s.phase === 'call') {
    // the best trump for this hand, and how many tricks it should take with it
    let best = 0, bestT = -1;
    for (let x = 0; x <= SANS; x++) { const t = expected(h, x) + (x === SANS ? -0.2 : 0); if (t > bestT) { bestT = t; best = x; } }
    const want = Math.floor(bestT + 0.35 + fuzz);
    if (want >= 4) {
      for (let n = Math.max(4, s.bid); n <= want; n++) if (beats(n, best, s.bid, s.suit)) return { t: 'call', n, suit: best };
    }
    return { t: 'call', n: 0 };
  }
  if (s.phase === 'est') {
    const ok = estOptions(s, seat);
    const aim = Math.round(expected(h, s.suit) + fuzz);
    return { t: 'est', n: ok.reduce((a, b) => (Math.abs(b - aim) < Math.abs(a - aim) ? b : a), ok[0]) };
  }
  const ok = legal(s, seat);
  if (level === 'easy' && rnd() < 0.35) return { t: 'card', id: ok[Math.floor(rnd() * ok.length)] };
  return { t: 'card', id: chooseCard(s, seat, ok) };
}

function chooseCard(s: EstimationState, seat: number, ok: number[]): number {
  const low = (cs: number[]) => cs.reduce((a, b) => (power(b) < power(a) ? b : a));
  const high = (cs: number[]) => cs.reduce((a, b) => (power(b) > power(a) ? b : a));
  const trump = s.suit === SANS ? -1 : s.suit;
  const need = (s.ests[seat] ?? 0) - s.tricks[seat];   // tricks still wanted (0 or less: take no more)
  const nonTrump = ok.filter(c => suitOf(c) !== trump);
  if (!s.trick.length) {
    if (need > 0) {
      // leading for tricks: an ace, else trumps when long in them, else the top of the longest side suit
      const aces = nonTrump.filter(c => power(c) === 14);
      if (aces.length) return aces[0];
      const trumps = ok.filter(c => suitOf(c) === trump);
      if (trumps.length >= 4) return high(trumps);
      const pool = nonTrump.length ? nonTrump : ok;
      return low(pool);
    }
    // enough tricks: lead the lowest card there is, preferably not a trump
    return low(nonTrump.length ? nonTrump : ok);
  }
  const wins = (c: number) => trickWinner([...s.trick, { seat, card: c }], s.suit) === seat;
  const winning = ok.filter(wins), losing = ok.filter(c => !wins(c));
  const last = s.trick.length === 3;
  if (need > 0) {
    if (winning.length) {
      if (last) return low(winning);
      const top = high(winning);
      return suitOf(top) === trump && winning.some(c => suitOf(c) !== trump) ? low(winning.filter(c => suitOf(c) !== trump)) : top;
    }
    return low(nonTrump.length ? nonTrump : ok);
  }
  // no more tricks wanted: get rid of the highest card that still loses (dangerous cards go first)
  if (losing.length) return high(losing);
  return low(ok);
}
