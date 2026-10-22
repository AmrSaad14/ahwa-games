import { chooseMove } from './bot';
import {
  End, HAND_SIZE, Line, MODE_PLAYERS, Mode, TILES, canPlay, endsFor, isDouble, newLine, pipSum, place,
  shuffled, teamCount, teamOf,
  defaultTarget, targetOptions,
} from './engine';
import type { Action, Emote, LastAction, MatchResult, RoundResult, TableView } from './protocol';
import { EMOTES } from './protocol';

export interface Player { uid: string; name: string; ch: number; bot: boolean; rating: number; hat?: string; drink?: string; muted?: boolean }
interface Seat extends Player { online: boolean; away: boolean; timeouts: number; lastEmote: number; mic?: boolean; offAt?: number }
/** A computer player's name and looks when it fills a seat (matchmaking gives them people's names). */
export type BotMaker = (seat: number, usedCh: number[]) => { name: string; ch: number; rating: number };

export interface TableOptions {
  id: string;
  code: string | null;
  mode: Mode;
  target: number;
  private: boolean;
  stake?: number;          // coins each player puts in; the server settles them
  turnMs: number;          // time a person has for a move
  nextRoundMs: number;     // pause between rounds
  botDelay: [number, number];
  offlineGraceMs: number;  // a disconnected player has this long to come back before the computer plays for them
  humanBots?: boolean;     // computer players take their time like people (matchmade games)
  lobbyAfterMatchMs: number;
  autoMoveMs?: number;     // pause before the game draws or passes for a player with no tile that fits (default 900)
}

export interface TableHooks {
  changed(t: Table): void;
  emote(t: Table, seat: number, k: Emote): void;
  matchEnd(t: Table, res: MatchResult): void;
  closed(t: Table): void;
}

export const BOT_NAMES = ['bot0', 'bot1', 'bot2', 'bot3'];

/** Short public id derived from a user id: lets players block someone without revealing their account id. */
export function publicId(uid: string) {
  let h = 2166136261, g = 52711;
  for (let i = 0; i < uid.length; i++) { const c = uid.charCodeAt(i); h = Math.imul(h ^ c, 16777619); g = Math.imul(g ^ c, 2246822507); }
  return (h >>> 0).toString(36) + (g >>> 0).toString(36);
}

/**
 * One domino table: seats, rounds, scoring, turn timers and computer players.
 * Used as the authoritative game on the server and as the local practice game on the client.
 */
export class Table {
  readonly id: string;
  code: string | null;
  mode: Mode;
  target: number;
  readonly isPrivate: boolean;
  stake: number;
  seats: (Seat | null)[];
  phase: TableView['phase'] = 'lobby';
  hands: number[][] = [];
  yard: number[] = [];
  line: Line = newLine();
  turn = -1;
  passes = 0;
  scores: number[];
  round = 0;
  starter = -1;
  /** Egyptian opening: when the round's opener is found by the heaviest double, that double must be played first. */
  opening: number | null = null;
  last: LastAction | null = null;
  result: RoundResult | null = null;
  match: MatchResult | null = null;
  turnEndsAt = 0;
  nextAt = 0;
  hostUid: string | null = null;
  v = 0;
  closed = false;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private lastPlayedValue: number[] = [];

  constructor(public opts: TableOptions, private hooks: TableHooks) {
    this.id = opts.id; this.code = opts.code; this.mode = opts.mode; this.target = opts.target; this.isPrivate = opts.private; this.stake = opts.stake ?? 0;
    this.seats = new Array(MODE_PLAYERS[this.mode]).fill(null);
    this.scores = new Array(teamCount(this.mode)).fill(0);
  }

  get n() { return MODE_PLAYERS[this.mode]; }
  seatOf(uid: string) { return this.seats.findIndex(s => s?.uid === uid); }
  humans() { return this.seats.filter((s): s is Seat => !!s && !s.bot); }
  hasOnlineHuman() { return this.humans().some(s => s.online); }

  /* ---------------- seating ---------------- */

  join(p: Player): number | string {
    const cur = this.seatOf(p.uid);
    if (cur >= 0) { const s = this.seats[cur]!; s.online = true; s.offAt = undefined; s.name = p.name; s.ch = p.ch; s.hat = p.hat; s.drink = p.drink; s.muted = p.muted; this.bump(); return cur; }
    if (this.phase !== 'lobby') return 'in_progress';
    const i = this.seats.findIndex(s => !s);
    if (i < 0) return 'room_full';
    this.seats[i] = { ...p, online: true, away: false, timeouts: 0, lastEmote: 0 };
    if (!this.hostUid && !p.bot) this.hostUid = p.uid;
    this.bump();
    return i;
  }

  /** A person leaves the table on purpose. */
  leave(uid: string) {
    const i = this.seatOf(uid); if (i < 0) return;
    if (this.phase === 'lobby') this.seats[i] = null;
    else { const s = this.seats[i]!; s.online = false; s.away = true; s.mic = false; }
    if (this.hostUid === uid) this.hostUid = this.humans().find(h => h.online && h.uid !== uid)?.uid ?? null;
    if (!this.hasOnlineHuman()) return this.close();
    this.bump();
  }

  setOnline(uid: string, online: boolean) {
    const i = this.seatOf(uid); if (i < 0) return;
    const s = this.seats[i]!;
    if (s.online === online) return;
    s.online = online;
    if (!online) { s.mic = false; s.offAt = Date.now(); }
    else s.offAt = undefined;
    if (online && this.phase !== 'lobby') { s.away = false; s.timeouts = 0; if (this.phase === 'play' && this.turn === i && !this.turnEndsAt) this.turnEndsAt = Date.now() + this.opts.turnMs; }
    if (!online && this.hostUid === uid) {
      const other = this.humans().find(h => h.online);
      if (other) this.hostUid = other.uid;
    }
    if (online && !this.hostUid) this.hostUid = uid;
    this.bump();
  }

  /** Joins or leaves the table's voice chat. */
  setMic(uid: string, on: boolean) {
    const i = this.seatOf(uid); if (i < 0) return;
    const s = this.seats[i]!;
    if (!!s.mic === on) return;
    s.mic = on; this.bump();
  }

  addBot(byUid: string, seat: number): string | null {
    if (byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    if (seat < 0 || seat >= this.n || this.seats[seat]) return 'bad_seat';
    const used = this.seats.filter(Boolean).map(s => s!.ch);
    const ch = [5, 0, 1, 2, 3, 4].find(c => !used.includes(c)) ?? 5;
    this.seats[seat] = { uid: BOT_NAMES[seat], name: '', ch, bot: true, rating: 1000, online: true, away: false, timeouts: 0, lastEmote: 0 };
    this.bump();
    return null;
  }

  /** Fill every empty seat with a computer player (used by matchmaking, with people's names from `make`). */
  fillBots(make?: BotMaker) {
    const used = this.seats.filter(Boolean).map(s => s!.ch);
    for (let i = 0; i < this.n; i++) if (!this.seats[i]) {
      const b = make?.(i, used);
      const ch = b?.ch ?? [5, 0, 1, 2, 3, 4].find(c => !used.includes(c)) ?? 5; used.push(ch);
      this.seats[i] = { uid: BOT_NAMES[i], name: b?.name ?? '', ch, bot: true, rating: b?.rating ?? 1000, online: true, away: false, timeouts: 0, lastEmote: 0 };
    }
  }

  kick(byUid: string, seat: number): string | null {
    if (byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    const s = this.seats[seat];
    if (!s || s.uid === this.hostUid) return 'bad_seat';
    this.seats[seat] = null;
    this.bump();
    return null;
  }

  setup(byUid: string, mode?: Mode, target?: number, stake?: number): string | null {
    if (byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    if (target !== undefined) this.target = target;
    if (stake !== undefined) this.stake = stake;
    if (mode && mode !== this.mode) {
      if (!targetOptions(mode).includes(this.target)) this.target = defaultTarget(mode);
      const people = this.seats.filter(Boolean) as Seat[];
      const n = MODE_PLAYERS[mode];
      const humans = people.filter(p => !p.bot), bots = people.filter(p => p.bot);
      if (humans.length > n) return 'too_many_players';
      this.mode = mode;
      const next: (Seat | null)[] = new Array(n).fill(null);
      [...humans, ...bots].slice(0, n).forEach((p, i) => { next[i] = p; if (p.bot) p.uid = BOT_NAMES[i]; });
      this.seats = next;
      this.scores = new Array(teamCount(mode)).fill(0);
    }
    this.bump();
    return null;
  }

  start(byUid: string | null): string | null {
    if (byUid !== null && byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    if (this.seats.some(s => !s)) return 'seats_empty';
    this.scores = new Array(teamCount(this.mode)).fill(0);
    this.round = 0; this.starter = -1; this.match = null;
    this.seats.forEach(s => { s!.away = false; s!.timeouts = 0; });
    this.startRound();
    this.bump();
    return null;
  }

  /* ---------------- moves ---------------- */

  act(uid: string, a: Action): string | null {
    const i = this.seatOf(uid);
    if (i < 0) return 'not_seated';
    if (this.phase !== 'play') return 'not_playing';
    if (i !== this.turn) return 'not_your_turn';
    const s = this.seats[i]!; s.timeouts = 0; s.away = false;
    const err = this.doMove(i, a);
    if (!err) this.bump();
    return err;
  }

  emote(uid: string, k: Emote) {
    const i = this.seatOf(uid);
    if (i < 0 || !EMOTES.includes(k)) return;
    const s = this.seats[i]!, now = Date.now();
    if (now - s.lastEmote < 1500) return;
    s.lastEmote = now;
    this.hooks.emote(this, i, k);
  }

  private nextSeat(s: number) { return (s + 1) % this.n; }

  private startRound() {
    this.round++;
    // Egyptian deal: 7 each; with three players the 0|0 is taken out and all 27 tiles are dealt, 9 each
    const three = this.mode === 'ffa3';
    const deck = shuffled(TILES.map((_, i) => i).filter(i => !three || i !== 0));
    const size = three ? 9 : HAND_SIZE;
    this.hands = this.seats.map(() => deck.splice(0, size).sort((a, b) => a - b));
    this.yard = deck;
    this.line = newLine();
    this.passes = 0;
    this.result = null;
    this.lastPlayedValue = new Array(this.n).fill(-1);
    let start = this.starter;
    this.opening = null;
    if (start < 0) {
      // the heaviest double opens (in 2 vs 2 that is always the double six); with no double at all, the heaviest tile
      let best = -1;
      this.hands.forEach((h, s) => h.forEach(id => {
        const v = isDouble(id) ? 100 + TILES[id][0] : TILES[id][0] + TILES[id][1];
        if (v > best) { best = v; start = s; this.opening = id; }
      }));
    }
    this.starter = start;
    this.phase = 'play';
    this.last = { t: 'deal', s: start, k: ++this.v };
    this.beginTurn(start);
  }

  private beginTurn(seat: number) {
    this.turn = seat;
    const s = this.seats[seat]!;
    this.turnEndsAt = !s.bot && !s.away && (s.online || this.inGrace(s)) ? Date.now() + this.opts.turnMs : 0;
  }

  /** A player who dropped out still has time to come back (the computer hasn't taken over yet). */
  private inGrace(s: Seat) { return !s.online && !s.away && !s.bot && s.offAt !== undefined && Date.now() < s.offAt + this.opts.offlineGraceMs; }
  /** How long a computer player waits: people's names think like people (sometimes a while). */
  private botWait(lo: number, hi: number) {
    if (!this.opts.humanBots) return lo + Math.random() * (hi - lo);
    return lo * 1.3 + Math.random() * (hi - lo) * 1.6 + (Math.random() < 0.12 ? hi * (0.8 + Math.random()) : 0);   // now and then a long think
  }

  private doMove(seat: number, a: Action): string | null {
    const hand = this.hands[seat];
    if (a.t === 'play') {
      const id = a.id;
      if (!Number.isInteger(id) || !hand.includes(id)) return 'no_tile';
      if (this.line.f === null && this.opening !== null && id !== this.opening) return 'must_open';
      const ends = endsFor(this.line, id);
      if (!ends.length) return 'illegal_move';
      const e: End = a.e && ends.includes(a.e) ? a.e : (ends.length === 1 ? ends[0] : '' as End);
      if (!e) return 'choose_end';
      const before = e === 'L' || e === 'R';
      place(this.line, id, e);
      if (before) { const arm = this.line[e as 'L' | 'R']; this.lastPlayedValue[seat] = arm[arm.length - 1][2]; }
      hand.splice(hand.indexOf(id), 1);
      this.passes = 0;
      this.last = { t: 'play', s: seat, id, e, k: ++this.v };
      if (!hand.length) { this.endRound(seat, false); return null; }
      this.beginTurn(this.nextSeat(seat));
      return null;
    }
    if (a.t === 'draw') {
      if (canPlay(this.line, hand)) return 'can_play';
      if (!this.yard.length) return 'yard_empty';
      hand.push(this.yard.pop()!);
      hand.sort((x, y) => x - y);
      this.last = { t: 'draw', s: seat, k: ++this.v };
      // drawing gives a little time back so the player can look at the new tile
      if (this.turnEndsAt) this.turnEndsAt = Math.max(this.turnEndsAt, Date.now() + Math.min(5000, this.opts.turnMs));
      return null;
    }
    if (a.t === 'pass') {
      if (canPlay(this.line, hand)) return 'can_play';
      if (this.yard.length) return 'must_draw';
      this.passes++;
      this.last = { t: 'pass', s: seat, k: ++this.v };
      if (this.passes >= this.n) { this.endRound(-1, true); return null; }
      this.beginTurn(this.nextSeat(seat));
      return null;
    }
    return 'bad_action';
  }

  private botPlay(seat: number, timedOut = false) {
    const partner = this.mode === 'team' ? (seat + 2) % 4 : -1;
    const m = this.line.f === null && this.opening !== null && this.hands[seat].includes(this.opening)
      ? { t: 'play' as const, id: this.opening, e: 'C' as End }
      : chooseMove(this.line, this.hands[seat], this.yard.length, 'normal', partner >= 0 && this.lastPlayedValue[partner] >= 0 ? this.lastPlayedValue[partner] : undefined);
    this.doMove(seat, m.t === 'play' ? { t: 'play', id: m.id, e: m.e } : m);
    if (timedOut && this.last) this.last.auto = true;
    this.bump();
  }

  private endRound(winner: number, blocked: boolean) {
    const pips = this.hands.map(h => pipSum(h));
    const nt = teamCount(this.mode);
    const teamPips = new Array(nt).fill(0);
    pips.forEach((p, s) => { teamPips[teamOf(this.mode, s)] += p; });
    let team = -1, points = 0;
    if (!blocked) {
      team = teamOf(this.mode, winner);
    } else {
      const min = Math.min(...teamPips);
      const lows = teamPips.map((p, t) => (p === min ? t : -1)).filter(t => t >= 0);
      if (lows.length === 1) {
        team = lows[0];
        const members = this.seats.map((_, s) => s).filter(s => teamOf(this.mode, s) === team);
        winner = members.reduce((b, s) => (pips[s] < pips[b] ? s : b), members[0]);
      } else winner = -1;
    }
    if (team >= 0) {
      points = teamPips.reduce((sum, p, t) => (t === team ? sum : sum + p), 0);
      this.scores[team] += points;
    }
    this.result = { winner, team, blocked, points, pips, hands: this.hands.map(h => h.slice()) };
    this.starter = winner >= 0 ? winner : -1;   // a tied block: the heaviest double opens again
    this.turn = -1; this.turnEndsAt = 0;
    this.last = { t: 'round', s: winner, k: ++this.v };
    if (Math.max(...this.scores) >= this.target) this.endMatch();
    else { this.phase = 'rend'; this.nextAt = Date.now() + this.opts.nextRoundMs; }
  }

  private endMatch() {
    const best = Math.max(...this.scores);
    const team = this.scores.indexOf(best);
    this.phase = 'mend';
    this.match = { team, scores: this.scores.slice(), deltas: this.seats.map(() => null) };
    this.last = { t: 'match', s: this.result?.winner ?? -1, k: ++this.v };
    this.nextAt = Date.now() + (this.isPrivate ? this.opts.lobbyAfterMatchMs : 90_000);
    this.hooks.matchEnd(this, this.match);
  }

  /** Private rooms go back to the room screen after a match. */
  private backToLobby() {
    this.phase = 'lobby';
    this.seats = this.seats.map(s => (s && (s.bot || s.online) ? { ...s, away: false, timeouts: 0 } : null));
    this.hands = []; this.yard = []; this.line = newLine(); this.turn = -1; this.result = null; this.match = null;
    this.scores = new Array(teamCount(this.mode)).fill(0); this.round = 0; this.starter = -1; this.last = null; this.nextAt = 0;
    if (!this.hostUid || !this.humans().some(h => h.uid === this.hostUid)) this.hostUid = this.humans()[0]?.uid ?? null;
    if (!this.humans().length) return this.close();
  }

  /* ---------------- timers ---------------- */

  private schedule() {
    if (this.timer) { clearTimeout(this.timer); this.timer = null; }
    if (this.closed) return;
    const now = Date.now();
    const at = (ms: number, fn: () => void) => { this.timer = setTimeout(() => { this.timer = null; if (!this.closed) fn(); }, Math.max(0, ms)); };
    if (this.phase === 'play') {
      const seat = this.turn, s = this.seats[seat]!;
      const [lo, hi] = this.opts.botDelay;
      const drawing = this.last?.t === 'draw' && this.last.s === seat;
      if (s.bot || s.away) {
        at(drawing ? lo * 0.5 : this.botWait(lo, hi), () => this.botPlay(seat));
      } else if (!s.online) {
        // dropped out: their turn runs as usual while they have time to come back; after that the computer plays for them
        const graceEnd = (s.offAt ?? now) + this.opts.offlineGraceMs;
        if (now >= graceEnd) { s.away = true; at(lo, () => this.botPlay(seat)); }
        else at(Math.min(graceEnd, this.turnEndsAt || graceEnd) - now, () => {
          if (Date.now() >= graceEnd - 5) s.away = true;
          if (!s.away) s.timeouts++;
          this.botPlay(seat, !s.away);
        });
      } else if (!canPlay(this.line, this.hands[seat])) {
        // nothing fits: draw for them (one tile at a time, so they can see it) or pass when the boneyard is empty
        const pause = this.opts.autoMoveMs ?? 900;
        at(drawing ? pause * 0.72 : pause, () => {
          this.doMove(seat, this.yard.length ? { t: 'draw' } : { t: 'pass' });
          if (this.last) this.last.forced = true;
          // once a drawn tile fits, the player gets their full turn time to play it
          if (this.turn === seat && this.phase === 'play' && canPlay(this.line, this.hands[seat])) this.turnEndsAt = Date.now() + this.opts.turnMs;
          this.bump();
        });
      } else {
        at(this.turnEndsAt - now, () => {
          // a player who is here always gets their full turn; the computer only covers seats whose player left or disconnected
          s.timeouts++;
          this.botPlay(seat, true);
        });
      }
    } else if (this.phase === 'rend') {
      at(this.nextAt - now, () => { this.startRound(); this.bump(); });
    } else if (this.phase === 'mend') {
      at(this.nextAt - now, () => {
        if (!this.isPrivate) return this.close();
        this.backToLobby(); if (!this.closed) this.bump();
      });
    }
  }

  private bump() {
    if (this.closed) return;
    this.v++;
    this.hooks.changed(this);
    this.schedule();
  }

  close() {
    if (this.closed) return;
    this.closed = true;
    if (this.timer) clearTimeout(this.timer);
    this.hooks.closed(this);
  }

  /** Re-send state, e.g. after the server filled in rating changes. */
  refresh() { this.bump(); }

  /* ---------------- views ---------------- */

  view(uid: string): TableView {
    const you = this.seatOf(uid);
    const reveal = this.phase === 'rend' || this.phase === 'mend';
    return {
      game: 'domino', id: this.id, code: this.code, mode: this.mode, target: this.target, private: this.isPrivate, stake: this.stake, phase: this.phase, you,
      seats: this.seats.map((s, i) => s && {
        name: s.name, ch: s.ch, bot: s.bot, away: s.away, online: s.online, count: this.hands[i]?.length ?? 0,
        team: teamOf(this.mode, i), host: s.uid === this.hostUid, rating: s.rating, mic: !!s.mic && s.online, pid: s.bot ? '' : publicId(s.uid),
        back: this.inGrace(s) && this.phase !== 'lobby' ? s.offAt! + this.opts.offlineGraceMs : undefined,
        hat: s.hat, drink: s.drink, muted: s.muted || undefined,
      }),
      hand: you >= 0 && this.hands[you] ? this.hands[you].slice() : [],
      line: this.line, yard: this.yard.length, open: this.line.f === null ? this.opening : null, turn: this.turn, turnEndsAt: this.turnEndsAt, turnMs: this.opts.turnMs,
      round: this.round, scores: this.scores.slice(), last: this.last,
      result: reveal ? this.result : null, match: this.match, nextAt: this.nextAt, now: Date.now(), v: this.v,
    };
  }
}
