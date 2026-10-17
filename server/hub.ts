import { PlayBilling } from './billing';
import crypto from 'node:crypto';
import { GAMES, GameId, MODES, MODE_PLAYERS, Mode, defaultTarget, gameOf, targetOptions, teamOf } from '../shared/engine';
import type { C2S, Emote, IceServer, S2C } from '../shared/protocol';
import { ECONOMY, STAKES, giftByKind, isStake, winnings, NO_ADS } from '../shared/gifts';
import { EMOTES } from '../shared/protocol';
import { Table } from '../shared/table';
import { BoardTable } from '../shared/boardtable';
import type { BoardAction } from '../shared/games/defs';

/** Any game table: domino has its own; chess, tawla and ludo share the board-game table. */
type Room = Table | BoardTable;
import { Limiter, Tokens, checkPassword, hashPassword } from './auth';
import { Store, UserRow } from './db';
import { GoogleSignIn, PhoneCodes, normalizePhone } from './login';
import { AdTickets } from './ads';

export interface Conn {
  send(msg: S2C): void;
  close(code: number, reason: string): void;
  ip: string;
}

interface Session { conn: Conn; uid: string | null; msgs: Limiter }
interface QEntry { uid: string; since: number }
/** Coins on a table for the match being played: who paid in, from which seat, and who has walked away. */
interface Escrow { stake: number; mode: Mode; paid: Map<string, { seat: number; left: boolean }> }

export interface HubOptions {
  turnMs: number; botFillMs: number; nextRoundMs: number; botDelay: [number, number]; lobbyAfterMatchMs: number; msgsPer5s?: number;
  stunUrls?: string[]; turnUrls?: string[]; turnSecret?: string;
  autoMoveMs?: number; chessClockMs?: number;
  phones?: PhoneCodes; google?: GoogleSignIn; defaultCountry?: string;
  adsSsv?: boolean;
  billing?: PlayBilling;
}

const CODE_ALPHABET = 'abcdefghjkmnpqrstuvwxyz23456789';
const cleanName = (s: unknown) => String(s ?? '').replace(/[\u0000-\u001f\u007f-\u009f​-‏‪-‮⁦-⁩<>]/g, '').trim().slice(0, 14);
const EMAIL_RE = /^[^\s@]{1,64}@[^\s@]{1,190}\.[^\s@]{2,24}$/;

/** Everything live on the server: who is connected, the matchmaking queues and every table. */
export class Hub {
  private sessions = new Set<Session>();
  private byUser = new Map<string, Session>();
  private tables = new Map<string, Room>();
  private byCode = new Map<string, Room>();
  private userTable = new Map<string, Room>();
  /** Matchmaking queues, one per mode and stake ("ffa2|20000"). */
  private queues = new Map<string, QEntry[]>();
  private escrow = new Map<string, Escrow>();
  private stopping = false;
  private adTickets = new AdTickets();
  private adLimiter = new Limiter(1, 20_000);
  private emptySince = new Map<string, number>();
  private authLimiter = new Limiter(10, 60_000);
  private giftLimiter = new Limiter(1, 1200);
  private buyLimiter = new Limiter(20, 60_000);
  private reportLimiter = new Limiter(10, 60 * 60_000);
  private ticker: ReturnType<typeof setInterval>;

  constructor(private store: Store, private tokens: Tokens, private opts: HubOptions) {
    this.ticker = setInterval(() => { this.matchmake(); this.sweep(); }, 1000);
  }

  stop() { this.stopping = true; clearInterval(this.ticker); this.tables.forEach(t => t.close()); }

  stats() { return { connections: this.sessions.size, tables: this.tables.size, queued: [...this.queues.values()].reduce((a, q) => a + q.length, 0) }; }

  /* ---------------- connections ---------------- */

  connect(conn: Conn): Session {
    const s: Session = { conn, uid: null, msgs: new Limiter(this.opts.msgsPer5s ?? 60, 5000) };
    this.sessions.add(s);
    return s;
  }

  disconnect(s: Session) {
    this.sessions.delete(s);
    if (!s.uid || this.byUser.get(s.uid) !== s) return;
    this.byUser.delete(s.uid);
    this.dequeue(s.uid);
    this.userTable.get(s.uid)?.setOnline(s.uid, false);
  }

  handle(s: Session, raw: string) {
    if (!s.msgs.allow('m')) { s.conn.close(4008, 'rate_limited'); return; }
    let m: C2S;
    try { m = JSON.parse(raw); } catch { return; }
    if (!m || typeof m !== 'object' || typeof (m as { t?: unknown }).t !== 'string') return;
    if (m.t === 'ping') return s.conn.send({ t: 'pong', c: Number(m.c) || 0, now: Date.now() });
    if (m.t === 'hello') return this.hello(s, m.token);
    if (!s.uid) return s.conn.send({ t: 'err', code: 'not_ready' });
    const uid = s.uid;
    const err = (code: string) => s.conn.send({ t: 'err', code });
    const table = this.userTable.get(uid);
    switch (m.t) {
      case 'profile': {
        const u = this.store.get(uid)!;
        const name = m.name !== undefined ? cleanName(m.name) || u.name : u.name;
        const ch = m.ch !== undefined && Number.isInteger(m.ch) && m.ch >= 0 && m.ch <= 5 ? m.ch : u.ch;
        this.store.setProfile(uid, name, ch);
        if (table && table.phase === 'lobby') table.join({ uid, name, ch, bot: false, rating: this.store.ratingOf(uid, gameOf(table.mode)) });
        return this.sendUser(uid);
      }
      case 'register': {
        if (!this.authLimiter.allow(s.conn.ip)) return err('slow_down');
        const email = String(m.email || '').trim().toLowerCase(), pw = String(m.pw || '');
        if (!EMAIL_RE.test(email)) return err('bad_email');
        if (pw.length < 6 || pw.length > 128) return err('weak_password');
        const u = this.store.get(uid)!;
        if (u.email) return err('already_registered');
        if (this.store.byEmail(email)) return err('email_taken');
        this.store.setCredentials(uid, email, hashPassword(pw));
        return this.sendUser(uid);
      }
      case 'login': {
        if (!this.authLimiter.allow(s.conn.ip)) return err('slow_down');
        const u = this.store.byEmail(String(m.email || '').trim().toLowerCase());
        if (!u || !checkPassword(String(m.pw || ''), u.pw)) return err('bad_login');
        this.detach(s);
        return this.attach(s, u);
      }
      case 'otp': {
        const phone = normalizePhone(m.phone, this.opts.defaultCountry);
        if (!phone) return err('bad_phone');
        if (!this.opts.phones) return err('phone_off');
        this.opts.phones.send(phone, s.conn.ip).then(e => e ? err(e) : s.conn.send({ t: 'otp', phone }));
        return;
      }
      case 'otpCheck': {
        const phone = normalizePhone(m.phone, this.opts.defaultCountry);
        if (!phone || !this.opts.phones) return err('bad_code');
        if (!this.authLimiter.allow('otp:' + s.conn.ip)) return err('slow_down');
        this.opts.phones.check(phone, m.code).then(ok => {
          if (s.uid !== uid) return;
          if (!ok) return err('bad_code');
          this.signIn(s, 'phone', phone, null, null);
        });
        return;
      }
      case 'ticket': {
        const p = this.opts.google?.take(m.ticket);
        if (!p) return err('ticket_expired');
        return this.signIn(s, 'google', p.sub, p.email, p.name);
      }
      case 'logout': {
        this.detach(s);
        return this.attach(s, this.newGuest());
      }
      case 'delete': {
        this.detach(s);
        this.store.deleteUser(uid);
        return this.attach(s, this.newGuest());
      }
      case 'quick': {
        if (!MODES.includes(m.mode)) return err('bad_mode');
        const stake = isStake(m.stake) ? m.stake : STAKES[0];
        if (this.store.coinsOf(uid) < stake) return err('not_enough_coins');
        if (table) this.leaveTable(uid);
        this.dequeue(uid);
        const key = m.mode + '|' + stake;
        if (!this.queues.has(key)) this.queues.set(key, []);
        this.queues.get(key)!.push({ uid, since: Date.now() });
        this.sendQueue(key);
        return this.matchmake();
      }
      case 'cancel': this.dequeue(uid); return s.conn.send({ t: 'queue', mode: null, count: 0, need: 0, botsAt: 0, now: Date.now() });
      case 'coins': return s.conn.send({ t: 'coins', rows: this.store.coinHistory(uid) });
      case 'adStart': {
        if (this.store.adsToday(uid) >= ECONOMY.adsPerDay) return err('ad_limit');
        return s.conn.send({ t: 'adTicket', ticket: this.adTickets.issue(uid), ssv: !!this.opts.adsSsv });
      }
      case 'adWatched': {
        // with server-side verification on, only AdMob's signed call (/ads/ssv) pays out
        if (this.opts.adsSsv) return;
        if (!this.adLimiter.allow(uid)) return err('slow_down');
        if (!this.rewardAd(uid, String(m.ticket || ''))) return err('ad_limit');
        return;
      }
      case 'buySkin': {
        const e = this.store.buySkin(uid, String(m.id || ''));
        if (e) return err(e);
        return this.sendUser(uid);
      }
      case 'equipSkin': {
        const e = this.store.equipSkin(uid, String(m.id || ''));
        if (e) return err(e);
        return this.sendUser(uid);
      }
      case 'buy': {
        // a Google Play purchase: check it with Google, then credit it exactly once
        const product = String(m.product || ''), token = String(m.token || '');
        const fail = (code: string) => s.conn.send({ t: 'buyFailed', product, token, code });
        if (!this.buyLimiter.allow(uid)) return fail('slow_down');
        const billing = this.opts.billing;
        if (!billing?.enabled) return fail('purchases_off');
        const seen = this.store.purchase(token);
        if (seen && seen.product !== NO_ADS.id) {
          // already credited (the app asks again until it hears back): just confirm
          if (seen.user_id !== uid) return fail('wrong_account');
          return s.conn.send({ t: 'bought', product, token, coins: 0, adFreeUntil: this.store.get(uid)?.ad_free_until ?? 0 });
        }
        billing.verify(uid, product, token).then(r => {
          if (!r.ok) return fail(r.code);
          let coins = 0;
          if (product === NO_ADS.id) this.store.setAdFree(uid, token, product, r.orderId, r.until);
          else if (this.store.creditPack(uid, token, product, r.orderId, r.coins)) coins = r.coins;
          const now = this.byUser.get(uid);
          now?.conn.send({ t: 'bought', product, token, coins, adFreeUntil: this.store.get(uid)?.ad_free_until ?? 0 });
          this.sendUser(uid);
        }).catch(e => { console.error('[billing]', (e as Error).message); fail('store_unreachable'); });
        return;
      }
      case 'create': {
        if (!MODES.includes(m.mode)) return err('bad_mode');
        const target = targetOptions(m.mode).includes(m.target) ? m.target : defaultTarget(m.mode);
        const stake = isStake(m.stake) ? m.stake : STAKES[0];
        if (this.store.coinsOf(uid) < stake) return err('not_enough_coins');
        this.dequeue(uid);
        if (table) this.leaveTable(uid);
        const t = this.newTable(m.mode, target, true, stake);
        const u = this.store.get(uid)!;
        t.join({ uid, name: u.name, ch: u.ch, bot: false, rating: this.store.ratingOf(uid, gameOf(m.mode)) });
        this.userTable.set(uid, t);
        return this.pushView(t, uid);
      }
      case 'join': {
        const code = String(m.code || '').toLowerCase().replace(/[^a-z0-9]/g, '');
        const t = this.byCode.get(code);
        if (!t || t.closed) return err('room_not_found');
        if (t.seatOf(uid) < 0 && this.store.coinsOf(uid) < t.stake) return err('not_enough_coins');
        if (table && table !== t) this.leaveTable(uid);
        this.dequeue(uid);
        const u = this.store.get(uid)!;
        const r = t.join({ uid, name: u.name, ch: u.ch, bot: false, rating: this.store.ratingOf(uid, gameOf(t.mode)) });
        if (typeof r === 'string') return err(r);
        this.userTable.set(uid, t);
        return this.pushView(t, uid);
      }
      case 'leave': {
        this.dequeue(uid);
        if (table) this.leaveTable(uid);
        return s.conn.send({ t: 'left' });
      }
      case 'setup': {
        if (!table) return err('no_table');
        const mode = m.mode && MODES.includes(m.mode) ? m.mode : undefined;
        if (mode && gameOf(mode) !== gameOf(table.mode)) return err('bad_mode');
        const target = m.target !== undefined && targetOptions(mode ?? table.mode).includes(m.target) ? m.target : undefined;
        const stake = isStake(m.stake) ? m.stake : undefined;
        const e = table.setup(uid, mode, target, stake); return e ? err(e) : undefined;
      }
      case 'bot': { if (!table) return err('no_table'); const e = table.addBot(uid, Number(m.seat)); return e ? err(e) : undefined; }
      case 'kick': {
        if (!table) return err('no_table');
        const victim = table.seats[Number(m.seat)];
        const e = table.kick(uid, Number(m.seat));
        if (e) return err(e);
        if (victim && !victim.bot) { this.userTable.delete(victim.uid); this.byUser.get(victim.uid)?.conn.send({ t: 'left' }); }
        return;
      }
      case 'start': {
        if (!table) return err('no_table');
        if (uid !== table.hostUid) return err('not_host');
        if (table.phase !== 'lobby') return err('in_progress');
        if (table.seats.some(x => !x)) return err('seats_empty');
        // everyone puts their stake in before the first move
        if (!this.takeStakes(table)) return err('player_short');
        const e = table.start(uid);
        if (e) { this.refundAll(table, false); return err(e); }
        return;
      }
      case 'act': {
        if (!table) return err('no_table');
        const a = m.a as Record<string, unknown>;
        if (!a || typeof a !== 'object' || typeof a.t !== 'string') return err('bad_action');
        let e: string | null;
        if (table instanceof Table) {
          e = table.act(uid, a.t === 'play' ? { t: 'play', id: Number(a.id), e: a.e as 'L' | 'R' | 'C' | undefined } : a.t === 'draw' ? { t: 'draw' } : { t: 'pass' });
        } else {
          // keep only the fields each kind of move uses, as numbers
          const n = (x: unknown) => Number(x);
          const clean: BoardAction | null =
            a.t === 'move' ? { t: 'move', from: n(a.from), to: n(a.to), promo: (['q', 'r', 'b', 'n'] as const).find(p => p === a.promo) } :
            a.t === 'step' ? { t: 'step', from: n(a.from), to: n(a.to), via: Array.isArray(a.via) ? a.via.slice(0, 4).map(n) : undefined } :
            a.t === 'pawn' ? { t: 'pawn', i: n(a.i) } : a.t === 'roll' ? { t: 'roll' } : a.t === 'pass' ? { t: 'pass' } : null;
          e = clean ? table.act(uid, clean) : 'bad_action';
        }
        if (e) { err(e); this.pushView(table, uid); }
        return;
      }
      case 'emote': { if (table && EMOTES.includes(m.k)) table.emote(uid, m.k); return; }
      case 'lb': {
        const game: GameId = m.game && GAMES.includes(m.game) ? m.game : 'domino';
        const lb = this.store.leaderboard(uid, game);
        return s.conn.send({ t: 'lb', game, rows: lb.rows, me: lb.me });
      }
      case 'gift': {
        if (!table) return err('no_table');
        const from = table.seatOf(uid), to = Number(m.to), target = table.seats[to];
        const g = giftByKind(String(m.k));
        if (!g || from < 0 || !target || to === from) return err('bad_gift');
        if (target.bot) return err('gift_bot');
        if (!this.giftLimiter.allow(uid)) return err('slow_down');
        if (!this.store.sendGift(uid, target.uid, g.k, g.price)) return err('not_enough_coins');
        for (const p of table.seats) if (p && !p.bot && this.userTable.get(p.uid) === table) this.byUser.get(p.uid)?.conn.send({ t: 'gift', from, to, k: g.k });
        this.sendUser(uid); this.sendUser(target.uid);
        return;
      }
      case 'voice': { table?.setMic(uid, !!m.on); return; }
      case 'rtc': {
        // pass an audio offer/answer to one other person at the same table who is in voice chat
        if (!table || !m.data || typeof m.data.sdp !== 'string' || m.data.sdp.length > 20_000) return;
        const from = table.seatOf(uid), target = table.seats[Number(m.to)];
        if (from < 0 || !target || target.bot || !target.mic || !table.seats[from]?.mic) return;
        if (this.userTable.get(target.uid) !== table) return;
        const type = m.data.type === 'answer' ? 'answer' : 'offer';
        this.byUser.get(target.uid)?.conn.send({ t: 'rtc', from, data: { type, sdp: m.data.sdp, table: table.id } });
        return;
      }
      case 'report': {
        if (!table) return err('no_table');
        const about = table.seats[Number(m.seat)];
        if (!about || about.bot || about.uid === uid) return err('bad_report');
        if (!this.reportLimiter.allow(uid)) return err('slow_down');
        this.store.report(uid, about.uid, String(m.reason || '').slice(0, 40), table.id);
        return;
      }
    }
  }

  /* ---------------- identity ---------------- */

  private newGuest(): UserRow {
    return this.store.createGuest('Player' + Math.floor(1000 + Math.random() * 9000), Math.floor(Math.random() * 5));
  }

  /**
   * Signing in with a phone number or Google: an account that already uses it is opened (as with email);
   * otherwise it is added to the account in use, so a guest keeps their coins, ratings and name.
   */
  private signIn(s: Session, via: 'phone' | 'google', key: string, email: string | null, name: string | null) {
    const uid = s.uid; if (!uid) return;
    const existing = via === 'phone' ? this.store.byPhone(key) : this.store.byGoogle(key);
    if (existing) {
      if (existing.id !== uid) { this.detach(s); this.attach(s, existing); }
      return s.conn.send({ t: 'signedIn', via, fresh: false });
    }
    const link = (id: string) => {
      if (via === 'phone') this.store.setPhone(id, key); else this.store.setGoogle(id, key, email);
      const u = this.store.get(id)!;
      // a guest name like Player1234 becomes the Google first name
      const clean = cleanName(name);
      if (via === 'google' && clean && /^Player\d{4}$/.test(u.name)) this.store.setProfile(id, clean, u.ch);
    };
    const me = this.store.get(uid)!;
    if (via === 'phone' ? me.phone : me.google_sub) {
      // this account already has a different number / Google account: start a new account for this one
      this.detach(s);
      const u = this.newGuest(); link(u.id);
      this.attach(s, this.store.get(u.id)!);
    } else { link(uid); this.sendUser(uid); }
    s.conn.send({ t: 'signedIn', via, fresh: true });
  }

  private hello(s: Session, token: unknown) {
    if (s.uid) return;
    const id = this.tokens.verify(token);
    const u = (id && this.store.get(id)) || this.newGuest();
    this.attach(s, u);
  }

  private attach(s: Session, u: UserRow) {
    const prev = this.byUser.get(u.id);
    if (prev && prev !== s) { prev.uid = null; prev.conn.close(4001, 'replaced'); }
    s.uid = u.id;
    this.byUser.set(u.id, s);
    this.store.touch(u.id);
    const bonus = this.store.claimDaily(u.id);
    const fresh = this.store.get(u.id) ?? u;
    s.conn.send({ t: 'welcome', user: this.store.publicUser(fresh), token: this.tokens.issue(u.id), now: Date.now(), ice: this.iceFor(u.id),
      login: { google: !!this.opts.google?.enabled, phone: !!this.opts.phones?.enabled } });
    if (bonus) s.conn.send({ t: 'bonus', coins: bonus });
    const t = this.userTable.get(u.id);
    if (t && !t.closed) { t.setOnline(u.id, true); this.pushView(t, u.id); }
  }

  private detach(s: Session) {
    if (!s.uid) return;
    const uid = s.uid;
    this.dequeue(uid);
    if (this.userTable.has(uid)) this.leaveTable(uid);
    this.byUser.delete(uid);
    s.uid = null;
  }

  /** STUN servers plus short-lived TURN credentials (coturn "use-auth-secret" scheme). */
  private iceFor(uid: string): IceServer[] {
    const out: IceServer[] = [];
    if (this.opts.stunUrls?.length) out.push({ urls: this.opts.stunUrls });
    if (this.opts.turnUrls?.length && this.opts.turnSecret) {
      const username = `${Math.floor(Date.now() / 1000) + 24 * 3600}:${uid}`;
      const credential = crypto.createHmac('sha1', this.opts.turnSecret).update(username).digest('base64');
      out.push({ urls: this.opts.turnUrls, username, credential });
    }
    return out;
  }

  private sendUser(uid: string) {
    const u = this.store.get(uid); const s = this.byUser.get(uid);
    if (u && s) s.conn.send({ t: 'user', user: this.store.publicUser(u) });
  }

  /* ---------------- tables ---------------- */

  private newTable(mode: Mode, target: number, isPrivate: boolean, stake: number): Room {
    let code: string | null = null;
    if (isPrivate) {
      do { code = Array.from({ length: 5 }, () => CODE_ALPHABET[crypto.randomInt(CODE_ALPHABET.length)]).join(''); } while (this.byCode.has(code));
    }
    const opts = {
      id: crypto.randomUUID(), code, mode, target, private: isPrivate, stake,
      turnMs: this.opts.turnMs, nextRoundMs: this.opts.nextRoundMs, botDelay: this.opts.botDelay, offlineGraceMs: 8000, lobbyAfterMatchMs: this.opts.lobbyAfterMatchMs,
      autoMoveMs: this.opts.autoMoveMs, chessClockMs: this.opts.chessClockMs,
    };
    const hooks = {
      changed: (tb: Room) => this.broadcast(tb),
      emote: (tb: Room, seat: number, k: Emote) => this.emote(tb, seat, k),
      matchEnd: (tb: Room, res: { team: number; scores: number[]; deltas: (number | null)[] }) => this.onMatchEnd(tb, res),
      closed: (tb: Room) => this.onClosed(tb),
    };
    const t: Room = gameOf(mode) === 'domino' ? new Table(opts, hooks) : new BoardTable(opts, hooks);
    this.tables.set(t.id, t);
    if (code) this.byCode.set(code, t);
    return t;
  }

  private leaveTable(uid: string) {
    const t = this.userTable.get(uid);
    this.userTable.delete(uid);
    if (t) this.forfeit(t, uid);
    t?.leave(uid);
  }

  /* ---------------- rewarded ads ---------------- */

  /** Pays for one watched ad. False if the ticket is unknown or used, or today's limit is reached. */
  rewardAd(uid: string, ticket: string): boolean {
    if (!this.store.get(uid) || !this.adTickets.take(ticket, uid)) return false;
    if (this.store.adsToday(uid) >= ECONOMY.adsPerDay) return false;
    this.store.addCoins(uid, ECONOMY.adReward);
    this.store.logCoins(uid, 'ad', ECONOMY.adReward);
    this.byUser.get(uid)?.conn.send({ t: 'adReward', coins: ECONOMY.adReward });
    this.sendUser(uid);
    return true;
  }

  /* ---------------- coins on the table ---------------- */

  /** Takes every person's stake as the match starts. Computer players' stakes come from the game. */
  private takeStakes(t: Room): boolean {
    const people = t.seats.map((s, i) => ({ s, i })).filter(x => x.s && !x.s.bot);
    if (!t.stake) return true;
    if (!this.store.takeStakes(people.map(x => x.s!.uid), t.stake)) return false;
    this.escrow.set(t.id, { stake: t.stake, mode: t.mode, paid: new Map(people.map(x => [x.s!.uid, { seat: x.i, left: false }])) });
    people.forEach(x => this.sendUser(x.s!.uid));
    return true;
  }

  /** Walking out of a match in progress loses the stake. */
  private forfeit(t: Room, uid: string) {
    const e = this.escrow.get(t.id), p = e?.paid.get(uid);
    if (!e || !p || p.left) return;
    p.left = true;
    this.store.logCoins(uid, 'left', -e.stake, { stake: e.stake, mode: e.mode });
    this.byUser.get(uid)?.conn.send({ t: 'payout', kind: 'left', delta: -e.stake, fee: 0, stake: e.stake });
    this.sendUser(uid);
  }

  /** Gives the stakes back (a server restart, or a match that couldn't start). */
  private refundAll(t: Room, log: boolean) {
    const e = this.escrow.get(t.id); if (!e) return;
    this.escrow.delete(t.id);
    for (const [uid, p] of e.paid) {
      if (p.left) continue;
      this.store.addCoins(uid, e.stake);
      if (log) this.store.logCoins(uid, 'refund', 0, { stake: e.stake, mode: e.mode });
      this.sendUser(uid);
    }
  }

  /** The winners take the losers' stakes, less the game's 10%. */
  private settle(t: Room, team: number) {
    const e = this.escrow.get(t.id); if (!e) return;
    this.escrow.delete(t.id);
    const won = (i: number) => teamOf(t.mode, i) === team;
    const winners = t.seats.filter((_, i) => won(i)).length, losers = t.seats.length - winners;
    const w = winnings(e.stake, winners, losers);
    for (const [uid, p] of e.paid) {
      if (p.left) continue;
      let kind: string, delta: number, fee = 0;
      if (team < 0) { kind = 'draw'; delta = 0; this.store.addCoins(uid, e.stake); }
      else if (won(p.seat)) { kind = 'win'; delta = w.net; fee = w.fee; this.store.addCoins(uid, e.stake + w.net); }
      else { kind = 'loss'; delta = -e.stake; }
      this.store.logCoins(uid, kind, delta, { fee, stake: e.stake, mode: e.mode });
      this.byUser.get(uid)?.conn.send({ t: 'payout', kind, delta, fee, stake: e.stake });
      this.sendUser(uid);
    }
  }

  private pushView(t: Room, uid: string) {
    const s = this.byUser.get(uid);
    if (s && this.userTable.get(uid) === t) s.conn.send({ t: 'table', view: t.view(uid) });
  }

  private broadcast(t: Room) {
    for (const seat of t.seats) if (seat && !seat.bot) this.pushView(t, seat.uid);
  }

  private emote(t: Room, seat: number, k: Emote) {
    for (const s of t.seats) if (s && !s.bot && this.userTable.get(s.uid) === t) this.byUser.get(s.uid)?.conn.send({ t: 'emote', seat, k });
  }

  private onMatchEnd(t: Room, res: { team: number; scores: number[]; deltas: (number | null)[] }) {
    const game = gameOf(t.mode);
    const players = t.seats.map(s => s && { uid: s.bot ? null : s.uid, bot: s.bot, rating: s.rating });
    if (!t.isPrivate) {
      // Elo against the average rating of the other side(s). Computer players count as 1000.
      t.seats.forEach((s, i) => {
        if (!s || s.bot) return;
        const myTeam = teamOf(t.mode, i);
        const opp = t.seats.filter((o, j) => o && teamOf(t.mode, j) !== myTeam).map(o => (o!.bot ? 1000 : o!.rating));
        const avg = opp.reduce((a, b) => a + b, 0) / Math.max(1, opp.length);
        const expected = 1 / (1 + Math.pow(10, (avg - s.rating) / 400));
        const won = myTeam === res.team, draw = res.team < 0;
        const played = this.store.ratingsOf(s.uid)?.[game]?.games ?? 0;
        const k = played < 20 ? 40 : 28;
        res.deltas[i] = Math.round(k * ((draw ? 0.5 : won ? 1 : 0) - expected));
        this.store.recordResult(s.uid, res.deltas[i], won, game);
        this.sendUser(s.uid);
      });
    } else {
      t.seats.forEach((s, i) => { if (s && !s.bot) { this.store.recordResult(s.uid, null, teamOf(t.mode, i) === res.team, game); this.sendUser(s.uid); } });
    }
    this.settle(t, res.team);
    this.store.saveMatch({ id: t.id + ':' + Date.now(), mode: t.mode, private: t.isPrivate, players, scores: res.scores, team: res.team });
  }

  private onClosed(t: Room) {
    // a match nobody finished: on a server restart everyone gets their stake back, otherwise those who walked away lose it
    if (this.escrow.has(t.id)) {
      if (this.stopping) this.refundAll(t, true);
      else { for (const uid of this.escrow.get(t.id)!.paid.keys()) this.forfeit(t, uid); this.escrow.delete(t.id); }
    }
    this.tables.delete(t.id);
    if (t.code) this.byCode.delete(t.code);
    this.emptySince.delete(t.id);
    for (const s of t.seats) {
      if (s && !s.bot && this.userTable.get(s.uid) === t) {
        this.userTable.delete(s.uid);
        this.byUser.get(s.uid)?.conn.send({ t: 'left' });
      }
    }
  }

  /** Close tables nobody is connected to any more. */
  private sweep() {
    const now = Date.now();
    for (const t of this.tables.values()) {
      const online = t.seats.some(s => s && !s.bot && this.byUser.has(s.uid) && this.userTable.get(s.uid) === t);
      if (online) { this.emptySince.delete(t.id); continue; }
      const since = this.emptySince.get(t.id) ?? now;
      this.emptySince.set(t.id, since);
      if (now - since > 3 * 60_000) t.close();
    }
  }

  /* ---------------- matchmaking ---------------- */

  private dequeue(uid: string) {
    for (const [key, q] of this.queues) {
      const i = q.findIndex(e => e.uid === uid);
      if (i >= 0) { q.splice(i, 1); this.sendQueue(key); }
    }
  }

  private sendQueue(key: string) {
    const q = this.queues.get(key) ?? [];
    const [mode, st] = key.split('|') as [Mode, string];
    const need = MODE_PLAYERS[mode];
    const botsAt = q.length ? q[0].since + this.opts.botFillMs : 0;
    for (const e of q) this.byUser.get(e.uid)?.conn.send({ t: 'queue', mode, count: q.length, need, botsAt, now: Date.now(), stake: Number(st) });
  }

  private matchmake() {
    const now = Date.now();
    for (const [key, q] of this.queues) {
      const [mode, st] = key.split('|') as [Mode, string];
      const stake = Number(st), need = MODE_PLAYERS[mode];
      // drop anyone who disconnected or can no longer cover the stake
      for (let i = q.length - 1; i >= 0; i--) {
        if (!this.byUser.has(q[i].uid)) q.splice(i, 1);
        else if (this.store.coinsOf(q[i].uid) < stake) { this.byUser.get(q[i].uid)?.conn.send({ t: 'err', code: 'not_enough_coins' }); q.splice(i, 1); }
      }
      let formed = false;
      while (q.length >= need || (q.length > 0 && now - q[0].since >= this.opts.botFillMs)) {
        const group = q.splice(0, need);
        const t = this.newTable(mode, defaultTarget(mode), false, stake);
        // spread people around the table; in team games keep strangers on opposite teams when possible
        group.forEach(e => {
          const u = this.store.get(e.uid)!;
          t.join({ uid: e.uid, name: u.name, ch: u.ch, bot: false, rating: this.store.ratingOf(e.uid, gameOf(mode)) });
          this.userTable.set(e.uid, t);
        });
        t.fillBots();
        if (!this.takeStakes(t)) { group.forEach(e => { this.userTable.delete(e.uid); this.byUser.get(e.uid)?.conn.send({ t: 'err', code: 'not_enough_coins' }); }); t.close(); continue; }
        t.start(null);
        formed = true;
      }
      if (formed || q.length) this.sendQueue(key);
      if (!q.length) this.queues.delete(key);
    }
  }
}
