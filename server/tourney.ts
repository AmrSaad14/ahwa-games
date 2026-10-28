// The weekly tournament: every Thursday at 9 pm Cairo time (TOURNEY_WEEKDAY / TOURNEY_HOUR to change it).
// Players sign up during the week. At the start, everyone signed up is put in a knockout bracket (8 to 64
// places; empty places get computer players). Each round, every pair plays one match at its own table; the
// winners go on. The game changes every week: dominoes, tawla, chess, ludo. Prizes are paid to the top eight,
// and everyone who played a match gets a little for taking part.
import type { Mode } from '../shared/engine';
import { cairoDay, nextCairoDay } from '../shared/gifts';
import { botIdentity } from '../shared/botnames';
import type { TourneyView, TourneyEntrant, S2C } from '../shared/protocol';

const num = (v: string | undefined, d: number) => (v !== undefined && v !== '' && !Number.isNaN(Number(v)) ? Number(v) : d);

export const TOURNEY = {
  enabled: true,                                        // (the dashboard turns it off and on)
  game: 'rotate' as 'rotate' | 'domino' | 'tawla' | 'chess' | 'ludo',   // this week's game: in turn, or always one
  weekday: num(process.env.TOURNEY_WEEKDAY, 4),        // 0 Sunday … 4 Thursday … 6 Saturday
  hour: num(process.env.TOURNEY_HOUR, 21),              // Cairo time
  prizes: [1_000_000, 500_000, 250_000, 250_000, 100_000, 100_000, 100_000, 100_000],
  played: 10_000,                                       // for everyone who played at least one match
  min: 8, max: 64,
  roundGapMs: 15_000,
  remindMs: 15 * 60_000,
};

/** This week's game: dominoes, tawla, chess, ludo in turn. */
export const TOURNEY_GAMES: { game: string; mode: Mode; target: number }[] = [
  { game: 'domino', mode: 'ffa2', target: 51 },
  { game: 'tawla', mode: 'tawla', target: 3 },
  { game: 'chess', mode: 'chess3', target: 1 },
  { game: 'ludo', mode: 'ludoq2', target: 1 },
];

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
const weekdayFmt = new Intl.DateTimeFormat('en-US', { timeZone: 'Africa/Cairo', weekday: 'short' });
/** The start of the Cairo day a moment falls in. */
const dayStart = (ts: number) => nextCairoDay(ts) - 86_400_000;

/** The next tournament start at or after `from` (Cairo time). */
export function nextTourneyAt(from: number): number {
  for (let d = 0; d < 9; d++) {
    const start = dayStart(from + d * 86_400_000);
    if (WEEKDAYS.indexOf(weekdayFmt.format(new Date(start + 12 * 3_600_000))) !== TOURNEY.weekday) continue;
    const at = start + TOURNEY.hour * 3_600_000;
    if (at >= from) return at;
  }
  return from + 7 * 86_400_000;
}
/** The tournament's id: the Cairo date it starts on. */
export const eventOf = (startsAt: number) => cairoDay(startsAt);
export const gameFor = (startsAt: number) => TOURNEY.game !== 'rotate' ? TOURNEY_GAMES.find(g => g.game === TOURNEY.game)! : TOURNEY_GAMES[Math.floor(startsAt / (7 * 86_400_000)) % TOURNEY_GAMES.length];

export interface Entrant { uid: string | null; name: string; ch: number; bot: boolean; rating: number }
interface Match { a: Entrant | null; b: Entrant | null; winner: 0 | 1 | null; table: string | null; started: boolean }

/** What the tournament needs from the rest of the server. */
export interface TourneyHost {
  regs(event: string): string[];
  entrant(uid: string): Entrant | null;
  /** A table for a match: the people in it are seated (as away until they come); returns its id. */
  table(event: string, a: Entrant, b: Entrant, mode: Mode, target: number): string;
  send(uid: string, msg: S2C): void;
  online(uid: string): boolean;
  pay(uid: string, coins: number, place: number | null): void;
  push(uids: string[], ar: [string, string], en: [string, string]): void;
  metaGet(k: string): string | null;
  metaSet(k: string, v: string): void;
}

export class Tourney {
  startsAt: number;
  /** The tournament's id (its Cairo date) and game: fixed when it's announced, even if it's started early. */
  event = '';
  def = TOURNEY_GAMES[0];
  state: 'open' | 'running' | 'done' = 'open';
  rounds: Match[][] = [];
  results: { place: number; uid: string | null; name: string; coins: number }[] = [];
  private nextRoundAt = 0;
  private reminded = false;
  private doneAt = 0;
  private played = new Set<string>();

  constructor(private host: TourneyHost, now = Date.now()) {
    this.startsAt = nextTourneyAt(now - 3 * 3_600_000);
    // a tournament that was running when the server stopped can't be picked up again: move on to the next one
    if (this.startsAt <= now) {
      if (this.host.metaGet('tourney_done_' + eventOf(this.startsAt))) this.startsAt = nextTourneyAt(now + 1);
      else if (now - this.startsAt > 10 * 60_000) { this.host.metaSet('tourney_done_' + eventOf(this.startsAt), 'missed'); this.startsAt = nextTourneyAt(now + 1); }
    }
    this.event = eventOf(this.startsAt); this.def = gameFor(this.startsAt);
  }

  /** Starts the tournament now (for testing: /admin/tourney?start=1). */
  startNow(now = Date.now()) { if (this.state === 'open') { this.startsAt = now; this.reminded = true; this.tick(now); } }

  /** The day, hour or game changed (dashboard): a tournament not started yet moves to the new time. */
  reschedule(now = Date.now()) {
    if (this.state !== 'open') return;
    this.startsAt = nextTourneyAt(now + 1);
    this.event = eventOf(this.startsAt); this.def = gameFor(this.startsAt); this.reminded = false;
  }
  /** Stops a running tournament (dashboard): no more rounds, nobody is paid; the next one opens. */
  cancel(now = Date.now()) {
    if (this.state === 'running') this.host.metaSet('tourney_done_' + this.event, 'cancelled');
    this.state = 'done'; this.doneAt = 0; this.reset(now);
  }

  tick(now = Date.now()) {
    if (this.state === 'open' && !TOURNEY.enabled) return;   // switched off: nothing starts
    if (this.state === 'open') {
      if (!this.reminded && now >= this.startsAt - TOURNEY.remindMs) {
        this.reminded = true;
        const regs = this.host.regs(this.event);
        if (regs.length) this.host.push(regs, ['البطولة كمان ربع ساعة 🏆', 'افتح اللعبة عشان تلحق أول ماتش'], ['The tournament starts in 15 minutes 🏆', 'Open the game to be ready for your first match']);
      }
      if (now >= this.startsAt) this.start(now);
      return;
    }
    if (this.state === 'running') {
      if (this.nextRoundAt && now >= this.nextRoundAt) { this.nextRoundAt = 0; this.startRound(now); }
      return;
    }
    // done: an hour later, open the next one
    if (now - this.doneAt > 60 * 60_000) this.reset(now);
  }

  private reset(now: number) {
    this.startsAt = nextTourneyAt(now + 1);
    this.event = eventOf(this.startsAt); this.def = gameFor(this.startsAt);
    this.state = 'open'; this.rounds = []; this.results = []; this.reminded = false; this.played.clear(); this.nextRoundAt = 0;
  }

  private start(now: number) {
    const people = this.host.regs(this.event).map(u => this.host.entrant(u)).filter((e): e is Entrant => !!e).slice(0, TOURNEY.max);
    this.host.metaSet('tourney_done_' + this.event, 'started');
    if (!people.length) { console.log(`[tourney] ${this.event}: nobody signed up`); this.reset(now); return; }
    let size = TOURNEY.min;
    while (size < people.length) size *= 2;
    // people first, each against a computer player where there are enough of them, then shuffled pairs
    const shuffled = people.sort(() => Math.random() - 0.5);
    const avg = shuffled.reduce((a, p) => a + p.rating, 0) / shuffled.length;
    const used: number[] = [];
    const bots: Entrant[] = Array.from({ length: size - shuffled.length }, () => { const b = botIdentity(used.length > 5 ? [] : used, avg); used.push(b.ch); return { uid: null, bot: true, ...b }; });
    const slots: Entrant[] = [];
    const pairsWithBot = Math.min(shuffled.length, bots.length);
    for (let i = 0; i < size / 2; i++) {
      if (i < pairsWithBot) { slots.push(shuffled[i], bots[i]); continue; }
      const rest = [...shuffled.slice(pairsWithBot), ...bots.slice(pairsWithBot)];
      const k = (i - pairsWithBot) * 2;
      slots.push(rest[k], rest[k + 1]);
    }
    const first: Match[] = [];
    for (let i = 0; i < size; i += 2) first.push({ a: slots[i], b: slots[i + 1], winner: null, table: null, started: false });
    this.rounds = [first];
    for (let n = size / 4; n >= 1; n /= 2) this.rounds.push(Array.from({ length: n }, () => ({ a: null, b: null, winner: null, table: null, started: false })));
    this.state = 'running';
    console.log(`[tourney] ${this.event}: ${people.length} people, bracket of ${size}, ${this.def.mode}`);
    this.host.push(people.map(p => p.uid!), ['البطولة بدأت! 🏆', 'ماتشك جاهز، ادخل العب'], ['The tournament has started! 🏆', 'Your match is ready, come and play']);
    this.startRound(now);
  }

  private get roundNo() { return this.rounds.findIndex(r => r.some(m => m.winner === null)); }

  private startRound(now: number) {
    const r = this.roundNo;
    if (r < 0) return this.finish(now);
    for (const m of this.rounds[r]) {
      if (m.started || !m.a || !m.b) continue;
      m.started = true;
      if (m.a.bot && m.b.bot) { this.decide(m, Math.random() < 0.5 ? 0 : 1, now, false); continue; }
      m.table = this.host.table(this.event, m.a, m.b, this.def.mode, this.def.target);
      for (const e of [m.a, m.b]) if (e.uid) { this.played.add(e.uid); this.host.send(e.uid, { t: 'tourneyMatch', table: m.table, round: r + 1 }); }
    }
    this.broadcast();
    this.afterDecide(now);
  }

  /** A tournament table's match ended: `seat` (0 or 1, the order the entrants were seated) won; -1 a draw. */
  onResult(tableId: string, seat: number, now = Date.now()) {
    const r = this.roundNo; if (r < 0) return;
    const m = this.rounds[r].find(x => x.table === tableId);
    if (!m || m.winner !== null) return;
    this.decide(m, seat === 0 || seat === 1 ? seat : (Math.random() < 0.5 ? 0 : 1), now, true);
  }
  /** A tournament table closed before its match ended: the side still there goes on. */
  onClosed(tableId: string, stayed: number | null, now = Date.now()) {
    const r = this.roundNo; if (r < 0) return;
    const m = this.rounds[r].find(x => x.table === tableId);
    if (!m || m.winner !== null) return;
    this.decide(m, stayed === 0 || stayed === 1 ? stayed : (Math.random() < 0.5 ? 0 : 1), now, true);
  }

  private decide(m: Match, w: 0 | 1, now: number, after: boolean) {
    m.winner = w;
    const r = this.rounds.findIndex(round => round.includes(m)), i = this.rounds[r].indexOf(m);
    const next = this.rounds[r + 1]?.[i >> 1];
    if (next) { if (i % 2 === 0) next.a = w === 0 ? m.a : m.b; else next.b = w === 0 ? m.a : m.b; }
    if (after) { this.broadcast(); this.afterDecide(now); }
  }

  private afterDecide(now: number) {
    const r = this.roundNo;
    if (r < 0) return this.finish(now);
    const round = this.rounds[r];
    // the round is over once every match in it was decided: the next one starts after a short pause
    if (round.every(m => !m.started) && !this.nextRoundAt) this.nextRoundAt = now + TOURNEY.roundGapMs;
  }

  private finish(now: number) {
    if (this.state !== 'running') return;
    this.state = 'done'; this.doneAt = now;
    // places: 1 the winner, 2 the final's loser, 3‥4 the semi-finals' losers, 5‥8 the quarter-finals'
    const placed: { e: Entrant; place: number }[] = [];
    const last = this.rounds.length - 1;
    const fin = this.rounds[last][0];
    if (fin.a && fin.b && fin.winner !== null) { placed.push({ e: fin.winner === 0 ? fin.a : fin.b, place: 1 }, { e: fin.winner === 0 ? fin.b : fin.a, place: 2 }); }
    for (let r = last - 1, place = 3; r >= 0 && place <= 8; r--) {
      for (const m of this.rounds[r]) if (m.a && m.b && m.winner !== null) placed.push({ e: m.winner === 0 ? m.b : m.a, place });
      place = place === 3 ? 5 : 9;
    }
    this.results = placed.map(p => ({ place: p.place, uid: p.e.uid, name: p.e.name, coins: TOURNEY.prizes[p.place - 1] ?? 0 })).sort((a, b) => a.place - b.place);
    for (const res of this.results) if (res.uid && res.coins) this.host.pay(res.uid, res.coins, res.place);
    for (const uid of this.played) if (!this.results.some(x => x.uid === uid && x.coins)) this.host.pay(uid, TOURNEY.played, null);
    this.host.metaSet('tourney_done_' + this.event, 'done');
    this.host.metaSet('tourney_results_' + this.event, JSON.stringify({ game: this.def.game, size: this.rounds[0]?.length * 2 || 0, people: this.played.size, results: this.results }));
    console.log(`[tourney] ${this.event}: won by ${this.results[0]?.name ?? '—'}`);
    this.broadcast();
  }

  /** Who should hear about changes: everyone signed up. */
  private broadcast() { for (const uid of this.host.regs(this.event)) if (this.host.online(uid)) this.host.send(uid, { t: 'tourney', view: this.view(uid) }); }

  /** The table a player's current match is at (or null). */
  tableOf(uid: string): string | null {
    const r = this.roundNo; if (r < 0) return null;
    return this.rounds[r].find(m => m.started && m.winner === null && (m.a?.uid === uid || m.b?.uid === uid))?.table ?? null;
  }

  view(uid: string | null, registered = false): TourneyView {
    const me = (e: Entrant | null): TourneyEntrant | null => e && { name: e.name, ch: e.ch, bot: e.bot, me: !!uid && e.uid === uid };
    const reg = registered || (!!uid && this.host.regs(this.event).includes(uid));
    let you: TourneyView['you'] = null;
    if (uid && this.rounds.length && reg) {
      const lost = this.rounds.some(r => r.some(m => m.winner !== null && ((m.winner === 0 ? m.b : m.a)?.uid === uid)));
      const place = this.results.find(x => x.uid === uid)?.place ?? null;
      you = { alive: !lost && this.state === 'running', table: this.tableOf(uid), place };
    }
    return {
      event: this.event, game: this.def.game, mode: this.def.mode, startsAt: this.startsAt, state: this.state,
      registered: reg, count: this.state === 'open' ? this.host.regs(this.event).length : this.rounds[0]?.length * 2 || 0,
      prizes: [...TOURNEY.prizes],
      rounds: this.rounds.map(r => r.map(m => ({ a: me(m.a), b: me(m.b), winner: m.winner, live: m.started && m.winner === null }))),
      you, results: this.results.map(x => ({ place: x.place, name: x.name, coins: x.coins, me: !!uid && x.uid === uid })),
      now: Date.now(),
    };
  }
}
