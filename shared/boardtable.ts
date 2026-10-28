import { CHESS_TIME, MODE_PLAYERS, Mode, defaultTarget, gameOf, targetOptions } from './engine';
import { BOT_NAMES, BotMaker, Player, publicId } from './table';
import type { BoardView, Emote, MatchResult } from './protocol';
import { EMOTES } from './protocol';
import { BoardAction, BoardEvent, BoardGameId, GameDef, GameEnd, defFor } from './games/defs';

interface Seat extends Player { online: boolean; away: boolean; timeouts: number; lastEmote: number; mic?: boolean; offAt?: number }

export interface BoardTableOptions {
  id: string;
  code: string | null;
  mode: Mode;
  target: number;
  private: boolean;
  stake?: number;          // coins each player puts in; the server settles them
  turnMs: number;            // per-move time for tawla and ludo
  nextRoundMs: number;       // pause between games of a tawla match
  botDelay: [number, number];
  offlineGraceMs: number;    // a disconnected player has this long to come back before the computer plays for them
  humanBots?: boolean;       // computer players take their time like people (matchmade games)
  lobbyAfterMatchMs: number;
  autoMoveMs?: number;       // pause before a move the game makes by itself
  chessClockMs?: number;     // chess: time per player (default 5 minutes)
  chessIncrementMs?: number; // chess: time added after each move
}

export interface BoardTableHooks {
  changed(t: BoardTable): void;
  emote(t: BoardTable, seat: number, k: Emote): void;
  matchEnd(t: BoardTable, res: MatchResult): void;
  closed(t: BoardTable): void;
}

/**
 * A chess, tawla or ludo table: seats, turns, timers (or chess clocks) and computer players,
 * with the rules supplied by the game's definition. Runs on the server and in practice mode.
 */
export class BoardTable {
  readonly id: string;
  readonly game: BoardGameId;
  code: string | null;
  mode: Mode;
  target: number;
  readonly isPrivate: boolean;
  stake: number;
  seats: (Seat | null)[];
  phase: BoardView['phase'] = 'lobby';
  state: any = null;
  scores: number[];
  round = 0;
  lastWinner = -1;
  last: BoardView['last'] = null;
  result: GameEnd | null = null;
  match: MatchResult | null = null;
  turnEndsAt = 0;
  nextAt = 0;
  clocks: number[] | null = null;
  private clockFrom = 0;            // when the current player's clock started running
  hostUid: string | null = null;
  v = 0;
  closed = false;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private def: GameDef<any>;
  private rnd = Math.random;

  constructor(public opts: BoardTableOptions, private hooks: BoardTableHooks) {
    this.id = opts.id; this.code = opts.code; this.mode = opts.mode; this.target = opts.target; this.isPrivate = opts.private; this.stake = opts.stake ?? 0;
    this.game = gameOf(opts.mode) as BoardGameId;
    this.def = defFor(this.mode);
    this.seats = new Array(MODE_PLAYERS[this.mode]).fill(null);
    this.scores = this.seats.map(() => 0);
  }

  get n() { return MODE_PLAYERS[this.mode]; }
  seatOf(uid: string) { return this.seats.findIndex(s => s?.uid === uid); }
  humans() { return this.seats.filter((s): s is Seat => !!s && !s.bot); }
  hasOnlineHuman() { return this.humans().some(s => s.online); }

  /* ---------------- seating (same behaviour as the domino table) ---------------- */

  join(p: Player): number | string {
    const cur = this.seatOf(p.uid);
    if (cur >= 0) { const s = this.seats[cur]!; s.online = true; s.offAt = undefined; s.name = p.name; s.ch = p.ch; s.hat = p.hat; s.drink = p.drink; s.muted = p.muted; s.title = p.title; this.bump(); return cur; }
    if (this.phase !== 'lobby') return 'in_progress';
    const i = this.seats.findIndex(s => !s);
    if (i < 0) return 'room_full';
    this.seats[i] = { ...p, online: true, away: false, timeouts: 0, lastEmote: 0 };
    if (!this.hostUid && !p.bot) this.hostUid = p.uid;
    this.bump();
    return i;
  }

  leave(uid: string) {
    const i = this.seatOf(uid); if (i < 0) return;
    if (this.phase === 'lobby') this.seats[i] = null;
    else { const s = this.seats[i]!; s.online = false; s.away = true; s.mic = false; }
    if (this.hostUid === uid) this.hostUid = this.humans().find(h => h.online && h.uid !== uid)?.uid ?? null;
    if (!this.hasOnlineHuman()) return this.close();
    this.bump();
  }

  /** A player changed the title they wear. */
  setTitle(uid: string, title: string | undefined) { const i = this.seatOf(uid); if (i >= 0) { this.seats[i]!.title = title; this.bump(); } }

  setOnline(uid: string, online: boolean) {
    const i = this.seatOf(uid); if (i < 0) return;
    const s = this.seats[i]!;
    if (s.online === online) return;
    s.online = online;
    if (!online) { s.mic = false; s.offAt = Date.now(); }
    else s.offAt = undefined;
    if (online && this.phase !== 'lobby') {
      s.away = false; s.timeouts = 0;
      if (this.phase === 'play' && this.state && this.def.toMove(this.state) === i && !this.turnEndsAt) this.turnEndsAt = Date.now() + this.opts.turnMs;
    }
    if (!online && this.hostUid === uid) { const o = this.humans().find(h => h.online); if (o) this.hostUid = o.uid; }
    if (online && !this.hostUid) this.hostUid = uid;
    this.bump();
  }

  setMic(uid: string, on: boolean) {
    const i = this.seatOf(uid); if (i < 0) return;
    const s = this.seats[i]!; if (!!s.mic === on) return;
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
    this.seats[seat] = null; this.bump();
    return null;
  }

  setup(byUid: string, mode?: Mode, target?: number, stake?: number): string | null {
    if (byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    if (target !== undefined) this.target = target;
    if (stake !== undefined) this.stake = stake;
    if (mode && mode !== this.mode) {
      if (gameOf(mode) !== this.game) return 'bad_mode';
      const people = this.seats.filter(Boolean) as Seat[];
      const n = MODE_PLAYERS[mode];
      const humans = people.filter(p => !p.bot), bots = people.filter(p => p.bot);
      if (humans.length > n) return 'too_many_players';
      this.mode = mode;
      this.def = defFor(mode);
      if (!targetOptions(mode).includes(this.target)) this.target = defaultTarget(mode);
      const next: (Seat | null)[] = new Array(n).fill(null);
      [...humans, ...bots].slice(0, n).forEach((p, i) => { next[i] = p; if (p.bot) p.uid = BOT_NAMES[i]; });
      this.seats = next;
      this.scores = next.map(() => 0);
    }
    this.bump();
    return null;
  }

  start(byUid: string | null): string | null {
    if (byUid !== null && byUid !== this.hostUid) return 'not_host';
    if (this.phase !== 'lobby') return 'in_progress';
    if (this.seats.some(s => !s)) return 'seats_empty';
    this.scores = this.seats.map(() => 0);
    this.round = 0; this.lastWinner = -1; this.match = null;
    this.seats.forEach(s => { s!.away = false; s!.timeouts = 0; });
    this.startGame();
    this.bump();
    return null;
  }

  /* ---------------- play ---------------- */

  act(uid: string, a: BoardAction): string | null {
    const i = this.seatOf(uid);
    if (i < 0) return 'not_seated';
    if (this.phase !== 'play') return 'not_playing';
    if (i !== this.def.toMove(this.state)) return 'not_your_turn';
    const s = this.seats[i]!; s.timeouts = 0; s.away = false;
    const err = this.apply(i, a);
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

  /** Chess time per player: the mode's clock, unless the server or a test overrides it. */
  private clockMs() { return this.opts.chessClockMs ?? this.def.clockMs!; }

  private startGame() {
    this.round++;
    this.result = null;
    const first = this.lastWinner >= 0 ? this.lastWinner : Math.floor(this.rnd() * this.n);
    this.state = this.def.newGame(this.n, this.game === 'chess' ? first % 2 : first, this.rnd);
    this.phase = 'play';
    if (this.def.clockMs) { this.clocks = this.seats.map(() => this.clockMs()); }
    this.last = { t: 'start', s: this.def.toMove(this.state), k: ++this.v };
    this.beginTurn();
  }

  private beginTurn() {
    const seat = this.def.toMove(this.state), s = this.seats[seat]!;
    this.clockFrom = Date.now();
    if (this.clocks) this.turnEndsAt = this.clockFrom + this.clocks[seat];
    else this.turnEndsAt = !s.bot && !s.away && (s.online || this.inGrace(s)) ? Date.now() + this.opts.turnMs : 0;
  }

  /** A player who dropped out still has time to come back (the computer hasn't taken over yet). */
  private inGrace(s: Seat) { return !s.online && !s.away && !s.bot && s.offAt !== undefined && Date.now() < s.offAt + this.opts.offlineGraceMs; }
  private botWait(lo: number, hi: number) {
    if (!this.opts.humanBots) return lo + Math.random() * (hi - lo);
    return lo * 1.3 + Math.random() * (hi - lo) * 1.6 + (Math.random() < 0.12 ? hi * (0.8 + Math.random()) : 0);   // now and then a long think
  }

  private apply(seat: number, a: BoardAction, flags: { auto?: boolean; forced?: boolean } = {}): string | null {
    const before = this.def.toMove(this.state);
    const r = this.def.act(this.state, seat, a, this.rnd);
    if (typeof r === 'string') return r;
    if (this.clocks) {
      this.clocks[seat] = Math.max(0, this.clocks[seat] - (Date.now() - this.clockFrom)) + (this.opts.chessIncrementMs ?? (CHESS_TIME[this.mode]?.inc ?? 2) * 1000);
    }
    this.state = r.s;
    this.last = { ...r.e, k: ++this.v, ...flags };
    const end = this.def.over(this.state);
    if (end) { this.endGame(end); return null; }
    // the same player keeps playing (tawla steps, ludo extra rolls): the per-move timer starts again
    if (this.def.toMove(this.state) !== before || !this.clocks) this.beginTurn();
    return null;
  }

  private endGame(end: GameEnd) {
    this.result = end;
    this.turnEndsAt = 0;
    if (end.winner >= 0) { this.scores[end.winner] += end.points; this.lastWinner = end.winner; }
    this.last = { t: 'end', s: end.winner, k: ++this.v };
    const done = this.game !== 'tawla' || end.winner < 0 || Math.max(...this.scores) >= this.target;
    if (done) {
      this.phase = 'mend';
      const best = Math.max(...this.scores);
      const leaders = this.scores.filter(x => x === best).length;
      this.match = { team: end.winner < 0 && leaders > 1 ? -1 : this.scores.indexOf(best), scores: this.scores.slice(), deltas: this.seats.map(() => null) };
      this.nextAt = Date.now() + (this.isPrivate ? this.opts.lobbyAfterMatchMs : 90_000);
      this.hooks.matchEnd(this, this.match);
    } else {
      this.phase = 'rend';
      this.nextAt = Date.now() + this.opts.nextRoundMs;
    }
  }

  private botMove(seat: number, flags: { auto?: boolean } = {}) {
    const a = this.def.auto(this.state, seat);
    if (this.apply(seat, a, flags)) this.apply(seat, { t: 'pass' }, flags);
    this.bump();
  }

  private backToLobby() {
    this.phase = 'lobby';
    this.seats = this.seats.map(s => (s && (s.bot || s.online) ? { ...s, away: false, timeouts: 0 } : null));
    this.state = null; this.result = null; this.match = null; this.clocks = null;
    this.scores = this.seats.map(() => 0); this.round = 0; this.last = null; this.nextAt = 0; this.lastWinner = -1;
    if (!this.hostUid || !this.humans().some(h => h.uid === this.hostUid)) this.hostUid = this.humans()[0]?.uid ?? null;
    if (!this.humans().length) this.close();
  }

  private schedule() {
    if (this.timer) { clearTimeout(this.timer); this.timer = null; }
    if (this.closed) return;
    const now = Date.now();
    const at = (ms: number, fn: () => void) => { this.timer = setTimeout(() => { this.timer = null; if (!this.closed) fn(); }, Math.max(0, ms)); };
    if (this.phase === 'play') {
      const seat = this.def.toMove(this.state), s = this.seats[seat]!;
      const forced = this.def.forced(this.state, seat);
      const [lo, hi] = this.opts.botDelay;
      if (forced) {
        at(this.opts.autoMoveMs ?? 900, () => { this.apply(seat, forced, { forced: true }); this.bump(); });
      } else if (s.bot || s.away) {
        at(this.botWait(lo, hi), () => this.botMove(seat));
      } else if (!s.online && now >= (s.offAt ?? now) + this.opts.offlineGraceMs) {
        // dropped out and didn't come back in time: the computer plays for them from now on
        s.away = true;
        at(lo, () => this.botMove(seat));
      } else if (this.clocks) {
        const graceEnd = !s.online ? (s.offAt ?? now) + this.opts.offlineGraceMs : Infinity;
        if (graceEnd < this.clockFrom + this.clocks[seat]) { at(graceEnd - now, () => { s.away = true; this.bump(); }); return; }
        // chess: the clock decides; a player who runs out of time loses
        at(this.clockFrom + this.clocks[seat] - now, () => {
          this.clocks![seat] = 0;
          this.endGame({ winner: 1 - seat, points: 1, kind: 'time' });
          this.bump();
        });
      } else if (!s.online) {
        const graceEnd = (s.offAt ?? now) + this.opts.offlineGraceMs;
        at(Math.min(graceEnd, this.turnEndsAt || graceEnd) - now, () => {
          if (Date.now() >= graceEnd - 5) s.away = true;
          if (!s.away) s.timeouts++;
          this.botMove(seat, s.away ? {} : { auto: true });
        });
      } else {
        at(this.turnEndsAt - now, () => { s.timeouts++; this.botMove(seat, { auto: true }); });
      }
    } else if (this.phase === 'rend') {
      at(this.nextAt - now, () => { this.startGame(); this.bump(); });
    } else if (this.phase === 'mend') {
      at(this.nextAt - now, () => { if (!this.isPrivate) return this.close(); this.backToLobby(); if (!this.closed) this.bump(); });
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

  refresh() { this.bump(); }

  view(uid: string): BoardView {
    const you = this.seatOf(uid);
    const turn = this.phase === 'play' && this.state ? this.def.toMove(this.state) : -1;
    const clocks = this.clocks ? this.clocks.map((c, i) => (i === turn && this.phase === 'play' ? Math.max(0, c - (Date.now() - this.clockFrom)) : c)) : null;
    const v: BoardView = {
      game: this.game, id: this.id, code: this.code, mode: this.mode, target: this.target, private: this.isPrivate, stake: this.stake, phase: this.phase, you,
      seats: this.seats.map((s, i) => s && {
        name: s.name, ch: s.ch, bot: s.bot, away: s.away, online: s.online, count: 0, team: i, host: s.uid === this.hostUid,
        rating: s.rating, mic: !!s.mic && s.online, pid: s.bot ? '' : publicId(s.uid),
        back: this.inGrace(s) && this.phase !== 'lobby' ? s.offAt! + this.opts.offlineGraceMs : undefined,
        hat: s.hat, drink: s.drink, muted: s.muted || undefined, title: s.title || undefined,
      }),
      turn, turnEndsAt: this.turnEndsAt, turnMs: this.clocks ? this.clockMs() : this.opts.turnMs, clocks,
      round: this.round, scores: this.scores.slice(), last: this.last, result: this.result, match: this.match,
      nextAt: this.nextAt, now: Date.now(), v: this.v,
    };
    if (this.state) (v as unknown as Record<string, unknown>)[this.game] = this.def.view(this.state, you);
    return v;
  }
}
