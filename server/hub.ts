import { PlayBilling } from './billing';
import crypto from 'node:crypto';
import { GAMES, GameId, MODES, MODE_PLAYERS, Mode, defaultTarget, gameOf, targetOptions, teamOf } from '../shared/engine';
import type { C2S, Emote, FriendRow, IceServer, PlayerCard, S2C } from '../shared/protocol';
import { ECONOMY, STAKES, giftByKind, isStake, winnings, NO_ADS, XP, WEEKLY_PRIZES, weekKey } from '../shared/gifts';
import { EMOTES } from '../shared/protocol';
import { Table, publicId, Player, BotMaker } from '../shared/table';
import { botIdentity } from '../shared/botnames';
import { missionById } from '../shared/missions';
import { SP, SEASON_PASS, seasonOf, seasonEndsAt } from '../shared/season';
import { Titles } from './titles';
import { skinFor } from '../shared/skins';
import { Tourney, Entrant, TOURNEY } from './tourney';
import type { Settings, GameSettings } from './settings';
import { parseEquip } from './db';
import { BoardTable } from '../shared/boardtable';
import type { BoardAction } from '../shared/games/defs';

/** Any game table: domino has its own; chess, tawla and ludo share the board-game table. */
type Room = Table | BoardTable;
import { Limiter, Tokens, checkPassword, hashPassword } from './auth';
import { Store, UserRow } from './db';
import { GoogleSignIn, PhoneCodes, normalizePhone } from './login';
import { AdTickets } from './ads';
import { AppleSignIn } from './apple';

export interface Conn {
  send(msg: S2C): void;
  close(code: number, reason: string): void;
  ip: string;
}

interface Session { conn: Conn; uid: string | null; msgs: Limiter; v?: number }
interface QEntry { uid: string; since: number; rating: number; coins: number }
/** Coins on a table for the match being played: who paid in, from which seat, and who has walked away. */
interface Escrow { stake: number; mode: Mode; paid: Map<string, { seat: number; left: boolean }> }

export interface HubOptions {
  turnMs: number; botFillMs: number; nextRoundMs: number; botDelay: [number, number]; lobbyAfterMatchMs: number; msgsPer5s?: number;
  stunUrls?: string[]; turnUrls?: string[]; turnSecret?: string;
  autoMoveMs?: number; chessClockMs?: number;
  phones?: PhoneCodes; google?: GoogleSignIn; apple?: AppleSignIn; defaultCountry?: string;
  adsSsv?: boolean;
  billing?: PlayBilling;
  /** A cash-out request came in (the dashboard can be told). */
  onCashout?: (id: number, egp: number) => void;
  /** A player won a weekly prize (the server sends them a push). */
  onWeeklyPrize?: (uid: string, rank: number, coins: number) => void;
  /** A push to players' phones (the tournament's reminders, a referral bonus). */
  push?: (uids: string[], ar: [string, string], en: [string, string]) => void;
  /** How long a player who dropped out has to come back before the computer plays for them (default 30 s). */
  graceMs?: number;
  /** The live settings from the dashboard (cash-out, maintenance, notice, how play runs). */
  settings?: Settings;
  titles?: Titles;
  /** Files a support message (and e-mails it); the server sets it up (server/support.ts). */
  support?: (m: { uid: string | null; name: string; contact: string; text: string; app: string }) => number;
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
  private friendLimiter = new Limiter(30, 60 * 60_000);
  private inviteLimiter = new Limiter(20, 60_000);
  private cashLimiter = new Limiter(6, 60 * 60_000);
  private supportLimiter = new Limiter(5, 60 * 60_000);
  private practiceLimiter = new Limiter(20, 60 * 60_000);
  private referLimiter = new Limiter(10, 60 * 60_000);
  private ticker: ReturnType<typeof setInterval>;
  /** Rematch votes after a matchmade game, per table. */
  private rematch = new Map<string, Set<string>>();
  /** Tables of the weekly tournament: who sits in seat 0 and 1. */
  private tourneyTables = new Map<string, (string | null)[]>();
  readonly tourney: Tourney;

  /** The titles catalog (designs), changed from the dashboard. */
  readonly titles: Titles;
  constructor(private store: Store, private tokens: Tokens, private opts: HubOptions) {
    this.titles = opts.titles ?? new Titles(store);
    this.titles.onChange(() => { for (const ss of this.byUser.values()) ss.conn.send({ t: 'titles', list: this.titles.list }); });
    this.tourney = new Tourney({
      regs: ev => this.store.tourneyRegs(ev),
      entrant: uid => { const u = this.store.get(uid); return u ? { uid, name: u.name, ch: u.ch, bot: false, rating: this.store.ratingOf(uid, 'domino') } : null; },
      table: (ev, a, b, mode, target) => this.tourneyTable(a, b, mode, target),
      send: (uid, msg) => this.byUser.get(uid)?.conn.send(msg),
      online: uid => this.byUser.has(uid),
      pay: (uid, coins, place) => {
        this.store.addCoins(uid, coins); this.store.logCoins(uid, 'tourney', coins);
        this.sendUser(uid);
        if (place) this.opts.push?.([uid], ['مبروك! 🏆', `جيت المركز ${place} في البطولة وكسبت ${coins.toLocaleString('en')} كوين`], ['Congratulations! 🏆', `You came #${place} in the tournament and won ${coins.toLocaleString('en')} coins`]);
      },
      push: (uids, ar, en) => this.opts.push?.(uids, ar, en),
      metaGet: k => this.store.metaGet(k),
      metaSet: (k, v) => this.store.metaSet(k, v),
    });
    this.ticker = setInterval(() => {
      this.matchmake(); this.sweep();
      try { this.tourney.tick(); } catch (e) { console.error('[tourney]', (e as Error).message); }
      if (++this.ticks % 60 === 0) { this.weeklyPrizes(); try { this.monthlyTitles(); } catch (e) { console.error('[titles]', (e as Error).message); } }
    }, 1000);
  }

  private ticks = 0;
  /** After a week ends, its top ten get their prizes (once). */
  weeklyPrizes(): { uid: string; rank: number; coins: number }[] {
    const last = weekKey(Date.now() - 7 * 86_400_000);
    const paid = this.store.payWeekly(last);
    for (const p of paid) {
      console.log(`[weekly] ${last}: #${p.rank} +${p.coins}`);
      this.byUser.get(p.uid)?.conn.send({ t: 'weeklyPrize', rank: p.rank, coins: p.coins, week: last });
      this.sendUser(p.uid);
      this.opts.onWeeklyPrize?.(p.uid, p.rank, p.coins);
    }
    return paid;
  }

  /** When a month ends, the best player of each game (with enough games) is "<game> player of the month" for
   *  the whole next month (once per month). */
  monthlyTitles(now = Date.now()): { uid: string; game: string; title: string; month: string }[] {
    const cfg = this.opts.settings?.value.titles ?? { monthly: true, minGames: 10 };
    const [y, mo] = seasonOf(now).split('-').map(Number);
    const prev = mo === 1 ? `${y - 1}-12` : `${y}-${String(mo - 1).padStart(2, '0')}`;
    if (this.store.metaGet('potm_' + prev)) return [];
    this.store.metaSet('potm_' + prev, String(now));
    if (!cfg.monthly) return [];
    const until = seasonEndsAt(now), won: { uid: string; game: string; title: string; month: string }[] = [];
    for (const game of GAMES) {
      const t = this.titles.monthlyFor(game); if (!t) continue;
      const top = this.store.monthlyBoard(game, prev, 1, cfg.minGames)[0]; if (!top) continue;
      this.store.grantTitle(top.id, t.id, until, prev);
      won.push({ uid: top.id, game, title: t.id, month: prev });
      console.log(`[titles] ${prev} ${game}: ${top.name}`);
      this.byUser.get(top.id)?.conn.send({ t: 'titleWon', id: t.id, month: prev });
      this.titleToTable(top.id); this.sendUser(top.id);
      this.opts.push?.([top.id], ['مبروك! 🏅', `انت ${t.ar} — اللقب بتاعك الشهر ده كله`], ['Congratulations! 🏅', `You are ${t.en}: the title is yours all month`]);
    }
    this.store.metaSet('potm_winners_' + prev, JSON.stringify(won.map(w => ({ ...w, name: this.store.get(w.uid)?.name ?? '' }))));
    return won;
  }

  /** This month's race for a game's title, as one player sees it. */
  private monthRace(uid: string, game: GameId, now = Date.now()) {
    const t = this.titles.monthlyFor(game); if (!t) return undefined;
    const cfg = this.opts.settings?.value.titles ?? { monthly: true, minGames: 10 };
    const month = seasonOf(now), top = this.store.monthlyBoard(game, month, 50, 0);
    const lead = top.find(r => r.games >= cfg.minGames) ?? null;
    const i = top.findIndex(r => r.id === uid);
    const holder = this.store.titleHolders(t.id, 1, now)[0]?.name ?? null;
    return { title: t.id, on: cfg.monthly, leader: lead?.name ?? null, leaderPoints: lead?.points ?? 0, place: i + 1, points: i >= 0 ? top[i].points : 0, endsAt: seasonEndsAt(now), minGames: cfg.minGames, holder };
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
    this.friendsChanged(s.uid);
    this.dequeue(s.uid);
    this.userTable.get(s.uid)?.setOnline(s.uid, false);
  }

  handle(s: Session, raw: string) {
    if (!s.msgs.allow('m')) { s.conn.close(4008, 'rate_limited'); return; }
    let m: C2S;
    try { m = JSON.parse(raw); } catch { return; }
    if (!m || typeof m !== 'object' || typeof (m as { t?: unknown }).t !== 'string') return;
    if (m.t === 'ping') return s.conn.send({ t: 'pong', c: Number(m.c) || 0, now: Date.now() });
    if (m.t === 'hello') { s.v = typeof m.v === 'number' ? m.v : 1; return this.hello(s, m.token); }
    if (!s.uid) return s.conn.send({ t: 'err', code: 'not_ready' });
    const uid = s.uid;
    const err = (code: string) => s.conn.send({ t: 'err', code });
    const table = this.userTable.get(uid);
    // maintenance (dashboard): no new games start; games already on finish
    if (this.opts.settings?.value.maintenance.on && ['quick', 'create', 'join', 'tourneyJoin', 'rematch'].includes(m.t)) return err('maintenance');
    switch (m.t) {
      case 'daily': {
        const d = this.store.claimDaily(uid);
        if (d.coins) s.conn.send({ t: 'bonus', coins: d.coins, streak: d.streak });
        return this.sendUser(uid);
      }
      case 'rematch': return this.voteRematch(uid);
      case 'mission': {
        const r = this.store.missionClaim(uid, String(m.id || ''));
        if (typeof r === 'string') return err(r);
        s.conn.send({ t: 'missionClaimed', id: String(m.id), coins: r.coins, xp: r.xp });
        const x = this.store.addXp(uid, r.xp); s.conn.send({ t: 'xp', ...x });
        return this.sendUser(uid);
      }
      case 'seasonBuy': {
        if (!SEASON_PASS.enabled) return err('season_off');
        const e = this.store.seasonPremium(uid, false);
        if (e) return err(e);
        return this.sendUser(uid);
      }
      case 'seasonClaim': {
        if (!SEASON_PASS.enabled) return err('season_off');
        const track = m.track === 'prem' ? 'prem' : 'free';
        const r = this.store.seasonClaim(uid, Number(m.tier), track);
        if (typeof r === 'string') return err(r);
        s.conn.send({ t: 'seasonClaimed', tier: Number(m.tier), track, ...r });
        return this.sendUser(uid);
      }
      case 'tourney': return s.conn.send({ t: 'tourney', view: this.tourney.view(uid) });
      case 'tourneyJoin': case 'tourneyLeave': {
        if (m.t === 'tourneyJoin' && !TOURNEY.enabled) return err('tourney_off');
        if (this.tourney.state !== 'open') return err('tourney_started');
        if (m.t === 'tourneyJoin') this.store.tourneyJoin(this.tourney.event, uid); else this.store.tourneyLeave(this.tourney.event, uid);
        return s.conn.send({ t: 'tourney', view: this.tourney.view(uid) });
      }
      case 'tourneyGo': {
        // go to your tournament match (leaving whatever table you're at)
        const id = this.tourney.tableOf(uid), t = id ? this.tables.get(id) : undefined;
        if (!t || t.closed) return err('no_match');
        this.dequeue(uid);
        if (table && table !== t) this.leaveTable(uid);
        this.userTable.set(uid, t);
        t.setOnline(uid, true);
        return this.pushView(t, uid);
      }
      case 'referral': {
        if (!this.referLimiter.allow(uid)) return err('slow_down');
        const e = this.store.setReferrer(uid, String(m.code || ''));
        if (e) return err(e);
        // already played? the bonus is paid now
        const u = this.store.get(uid);
        if (u && u.games > 0) this.payReferral(uid);
        return this.sendUser(uid);
      }
      case 'profile': {
        const u = this.store.get(uid)!;
        const name = m.name !== undefined ? cleanName(m.name) || u.name : u.name;
        const ch = m.ch !== undefined && Number.isInteger(m.ch) && m.ch >= 0 && m.ch <= 5 ? m.ch : u.ch;
        this.store.setProfile(uid, name, ch);
        if (table && table.phase === 'lobby') table.join(this.player(uid, gameOf(table.mode)));
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
        // a one-time ticket from the Google or the Apple sign-in page
        const p = this.opts.google?.take(m.ticket);
        if (p) return this.signIn(s, 'google', p.sub, p.email, p.name);
        const a = this.opts.apple?.take(m.ticket);
        if (a) return this.signIn(s, 'apple', a.sub, a.email, a.name);
        return err('ticket_expired');
      }
      case 'playGames': {
        const g = this.opts.google;
        if (!g?.playGamesEnabled || typeof m.code !== 'string' || !this.authLimiter.allow(uid)) return err('play_games_off');
        g.playGames(m.code).then(p => this.signIn(s, 'playgames', p.sub, null, p.name), () => s.conn.send({ t: 'err', code: 'play_games_failed' }));
        return;
      }
      /* ---------- friends ---------- */
      case 'friends': return this.sendFriends(uid);
      case 'friendAdd': {
        if (!this.friendLimiter.allow(uid)) return err('slow_down');
        let other: UserRow | undefined;
        if (typeof m.code === 'string') other = this.store.byFriendCode(m.code.trim().replace(/[^a-z0-9]/gi, ''));
        else if (typeof m.pid === 'string') other = this.store.byPid(m.pid);
        else if (typeof m.seat === 'number' && table) { const p = table.seats[m.seat]; if (p && !p.bot) other = this.store.get(p.uid); }
        if (!other) return err('friend_not_found');
        if (other.id === uid) return err('friend_self');
        if (this.store.friendCount(uid) >= 300) return err('friends_full');
        const r = this.store.friendRequest(uid, other.id);
        const me = this.store.get(uid)!;
        if (r === 'sent') { s.conn.send({ t: 'friendNews', kind: 'sent', name: other.name }); this.byUser.get(other.id)?.conn.send({ t: 'friendNews', kind: 'request', name: me.name }); }
        if (r === 'friends') { s.conn.send({ t: 'friendNews', kind: 'added', name: other.name }); this.byUser.get(other.id)?.conn.send({ t: 'friendNews', kind: 'accepted', name: me.name }); }
        if (r === 'already') s.conn.send({ t: 'friendNews', kind: 'added', name: other.name });
        this.sendFriends(uid); this.sendFriends(other.id);
        return;
      }
      case 'friendAnswer': {
        const other = typeof m.pid === 'string' ? this.store.byPid(m.pid) : this.store.get(String(m.id));
        if (!other) return err('friend_not_found');
        if (m.yes) { if (this.store.friendAccept(uid, other.id)) this.byUser.get(other.id)?.conn.send({ t: 'friendNews', kind: 'accepted', name: this.store.get(uid)!.name }); }
        else this.store.friendRemove(uid, other.id);
        this.sendFriends(uid); this.sendFriends(other.id);
        return;
      }
      case 'friendRemove': {
        const id = typeof m.pid === 'string' ? this.store.byPid(m.pid)?.id ?? '' : String(m.id);
        this.store.friendRemove(uid, id);
        this.sendFriends(uid); this.sendFriends(id);
        if (typeof m.pid === 'string') { const c = this.card(uid, m.pid); if (c) s.conn.send({ t: 'profileCard', card: c }); }
        return;
      }
      case 'profileOf': {
        let pid = typeof m.pid === 'string' ? m.pid : '';
        if (!pid && typeof m.seat === 'number' && table) { const p = table.seats[m.seat]; if (p && !p.bot) pid = publicId(p.uid); }
        const c = pid ? this.card(uid, pid) : null;
        if (!c) return err('player_not_found');
        return s.conn.send({ t: 'profileCard', card: c });
      }
      case 'invite': {
        // invite a friend to the private room you are in (still gathering players)
        const id = String(m.id);
        if (!this.inviteLimiter.allow(uid)) return err('slow_down');
        if (this.store.friendStatus(uid, id) !== 'friend') return err('not_friends');
        const to = this.byUser.get(id);
        if (!to) return err('friend_offline');
        let room = table;
        if (!room || !room.code || room.phase !== 'lobby') {
          // one tap from the friends list: open a room for the two of you first (just for fun, no stake)
          const mode = m.mode && MODES.includes(m.mode) ? m.mode : 'ffa2';
          this.dequeue(uid);
          if (room) this.leaveTable(uid);
          room = this.newTable(mode, defaultTarget(mode), true, 0);
          room.join(this.player(uid, gameOf(mode)));
          this.userTable.set(uid, room);
          this.pushView(room, uid);
        }
        const me = this.store.get(uid)!;
        to.conn.send({ t: 'invited', from: { id: uid, name: me.name, ch: me.ch }, code: room.code!, mode: room.mode, stake: room.stake });
        return;
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
        this.queues.get(key)!.push({ uid, since: Date.now(), rating: this.store.ratingOf(uid, gameOf(m.mode)), coins: this.store.coinsOf(uid) });
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
        if (product === SEASON_PASS.id && !SEASON_PASS.enabled && !this.store.purchase(token)) return fail('season_off');
        if (product === NO_ADS.id) return fail('bad_product');   // no ad-free plan on its own: it comes with the season pass
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
          else if (product === SEASON_PASS.id) { if (this.store.creditPack(uid, token, product, r.orderId, 0)) this.store.seasonPremium(uid, true); }
          else if (this.store.creditPack(uid, token, product, r.orderId, r.coins)) coins = r.coins;
          const now = this.byUser.get(uid);
          now?.conn.send({ t: 'bought', product, token, coins, adFreeUntil: this.store.get(uid)?.ad_free_until ?? 0 });
          this.sendUser(uid);
        }).catch(e => { console.error('[billing]', (e as Error).message); fail('store_unreachable'); });
        return;
      }
      case 'practice': {
        // a free, unrated game against computer players, started at once (the Unity app's practice mode)
        if (!MODES.includes(m.mode)) return err('bad_mode');
        this.dequeue(uid);
        if (table) this.leaveTable(uid);
        const t = this.newTable(m.mode, defaultTarget(m.mode), true, 0);
        t.join(this.player(uid, gameOf(m.mode)));
        this.userTable.set(uid, t);
        t.fillBots();
        t.start(null);
        return;
      }
      case 'create': {
        if (!MODES.includes(m.mode)) return err('bad_mode');
        const target = targetOptions(m.mode).includes(m.target) ? m.target : defaultTarget(m.mode);
        // a room for friends: any stake from the list, or 0 to play just for fun
        const stake = m.stake === 0 ? 0 : isStake(m.stake) ? m.stake : STAKES[0];
        if (this.store.coinsOf(uid) < stake) return err('not_enough_coins');
        this.dequeue(uid);
        if (table) this.leaveTable(uid);
        const t = this.newTable(m.mode, target, true, stake);
        t.join(this.player(uid, gameOf(m.mode)));
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
        const r = t.join(this.player(uid, gameOf(t.mode)));
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
      case 'emote': {
        if (!table || !EMOTES.includes(m.k)) return;
        if (this.store.mutedUntil(uid) > Date.now()) return err('chat_muted');
        table.emote(uid, m.k); return;
      }
      case 'lb': {
        if (m.game === 'week') {
          const lb = this.store.weeklyBoard(uid);
          return s.conn.send({ t: 'lb', game: 'week', rows: lb.rows, me: lb.me, week: weekKey(), endsAt: weekEndsAt(), prizes: [...WEEKLY_PRIZES] });
        }
        const game: GameId = m.game && (GAMES as string[]).includes(m.game) ? m.game as GameId : 'domino';
        const lb = this.store.leaderboard(uid, game);
        return s.conn.send({ t: 'lb', game, rows: lb.rows, me: lb.me, month: this.monthRace(uid, game) });
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
      case 'pushToken': {
        const tok = String(m.token || '');
        if (tok.length < 20 || tok.length > 4096) return;
        this.store.savePushToken(uid, tok, m.lang === 'en' ? 'en' : 'ar', m.platform === 'ios' ? 'ios' : 'android');
        return;
      }
      case 'cashouts': return this.sendCashouts(uid);
      case 'wearTitle': {
        const id = typeof m.id === 'string' && m.id ? m.id : null;
        if (id && !this.titles.get(id)) return err('bad_title');
        if (!this.store.wearTitle(uid, id)) return err('bad_title');
        if (table) table.setTitle(uid, id ?? undefined);
        return this.sendUser(uid);
      }
      case 'practiceDone': {
        // a game against the computer on the phone: a little experience (limited, so it can't be farmed)
        if (!this.practiceLimiter.allow(uid)) return;
        const r = this.store.addXp(uid, m.won ? XP.practiceWin : XP.practiceLoss);
        s.conn.send({ t: 'xp', ...r });
        const game: GameId = (GAMES as string[]).includes(m.game) ? m.game as GameId : 'domino';
        this.afterGame(uid, game, !!m.won, false, m.won ? SP.practiceWin : SP.practiceLoss);
        return this.sendUser(uid);
      }
      case 'support': {
        const text = String(m.text || '').trim().slice(0, 4000);
        if (text.length < 3) return err('support_empty');
        if (!this.supportLimiter.allow(uid) || !this.opts.support) return err('slow_down');
        const u = this.store.get(uid);
        const contact = String(m.contact || '').trim().slice(0, 200) || u?.google_email || u?.email || u?.phone || '';
        const id = this.opts.support({ uid, name: u?.name ?? '', contact, text, app: String(m.app || '').slice(0, 80) });
        return s.conn.send({ t: 'supportSent', id });
      }
      case 'cashout': {
        const cs = this.cash;
        if (!cs.enabled) return err('cashout_off');
        const u = this.store.get(uid);
        if (!u) return;
        // paid to a real wallet: only for accounts that can be got back (Google, Apple, mobile number or e-mail)
        if (!u.google_sub && !u.apple_sub && !u.phone && !u.email) return err('sign_in_first');
        const check = this.store.cashoutCheck(uid);
        if (check.games < cs.minGames) return err('cashout_games');
        if (check.ageDays < cs.minAccountDays) return err('cashout_new_account');
        const phone = walletNumber(String(m.phone || ''), cs.prefixes);
        if (!phone) return err('bad_wallet');
        const units = Math.floor(Number(m.millions));
        if (!(units >= 1 && units <= cs.maxUnits)) return err('bad_amount');
        if (!this.cashLimiter.allow(uid)) return err('slow_down');
        const coins = units * cs.coins, egp = units * cs.egp;
        const r = this.store.requestCashout(uid, coins, egp, phone);
        if (r === 'pending') return err('cashout_pending');
        if (r === null) return err('not_enough_coins');
        console.log(`[cashout] #${r} ${egp} EGP for ${coins} coins`);
        this.opts.onCashout?.(r, egp);
        s.conn.send({ t: 'cashoutDone', id: r, egp, coins, phone });
        this.sendUser(uid); this.sendCashouts(uid);
        return;
      }
      case 'report': {
        // someone at your table (by seat), or anyone by the pid on their card
        const byPid = typeof m.pid === 'string' ? this.store.byPid(m.pid) : undefined;
        const about = byPid ? { uid: byPid.id, bot: false } : table ? table.seats[Number(m.seat)] : null;
        if (!about || about.bot || about.uid === uid) return err('bad_report');
        if (!this.reportLimiter.allow(uid)) return err('slow_down');
        this.store.report(uid, about.uid, String(m.reason || '').slice(0, 40), table?.id ?? 'card');
        // three different players reporting someone in a day switch their chat off for a day
        if (this.store.reporters(about.uid, Date.now() - 86_400_000) >= 3 && this.store.mutedUntil(about.uid) < Date.now()) {
          this.store.mute(about.uid, Date.now() + 86_400_000);
          console.log('[report] chat muted for a day:', publicId(about.uid));
          for (const t of this.tables.values()) if (t.seatOf(about.uid) >= 0) { const st = t.seats[t.seatOf(about.uid)]; if (st) st.muted = true; t.refresh(); }
          this.sendUser(about.uid);
        }
        return;
      }
    }
  }

  /** A player's card as `viewer` sees it (null viewer: anyone, e.g. the public API). */
  card(viewer: string | null, pid: string): PlayerCard | null {
    const u = this.store.byPid(pid);
    const st = u ? this.store.playerStats(u.id) : null;
    if (!u || !st) return null;
    const t = this.userTable.get(u.id);
    return { pid, name: u.name, ch: u.ch, title: this.store.wornTitle(u.id), ...st, online: this.byUser.has(u.id), playing: !!t && !t.closed && t.phase !== 'lobby',
      me: viewer === u.id, friend: viewer && viewer !== u.id ? this.store.friendStatus(viewer, u.id) : null };
  }

  /** Lets a player's open app know their friends list changed (after the HTTP API changed it). */
  friendsUpdated(uid: string) { this.sendFriends(uid); }
  /** Who is connected right now. */
  isOnline(uid: string) { return this.byUser.has(uid); }
  /** Sends any message to a player's open app. */
  notify(uid: string, msg: S2C) { this.byUser.get(uid)?.conn.send(msg); }

  /* ---------------- identity ---------------- */

  /** A player's friends list, with who is online and who is in a game right now. */
  private sendFriends(uid: string) {
    const s = this.byUser.get(uid); if (!s) return;
    const rows: FriendRow[] = [];
    for (const l of this.store.friendLinks(uid)) {
      const u = this.store.get(l.other); if (!u) continue;
      const t = this.userTable.get(u.id);
      rows.push({ id: u.id, pid: publicId(u.id), name: u.name, ch: u.ch, status: l.status, online: this.byUser.has(u.id), playing: !!t && !t.closed && t.phase !== 'lobby' });
    }
    rows.sort((a, b) => (a.status === 'incoming' ? 0 : 1) - (b.status === 'incoming' ? 0 : 1) || Number(b.online) - Number(a.online) || a.name.localeCompare(b.name));
    s.conn.send({ t: 'friends', rows });
  }
  /** Someone came online or left: their online friends see it. */
  private friendsChanged(uid: string) {
    if (this.stopping) return;   // (shutting down: the database may be closed already)
    for (const l of this.store.friendLinks(uid)) if (l.status === 'friend' && this.byUser.has(l.other)) this.sendFriends(l.other);
  }

  private newGuest(): UserRow {
    return this.store.createGuest('Player' + Math.floor(1000 + Math.random() * 9000), Math.floor(Math.random() * 5));
  }

  /**
   * Signing in with a phone number or Google: an account that already uses it is opened (as with email);
   * otherwise it is added to the account in use, so a guest keeps their coins, ratings and name.
   */
  private signIn(s: Session, via: 'phone' | 'google' | 'playgames' | 'apple', key: string, email: string | null, name: string | null) {
    const uid = s.uid; if (!uid) return;
    const existing = via === 'phone' ? this.store.byPhone(key) : via === 'google' ? this.store.byGoogle(key) : via === 'apple' ? this.store.byApple(key) : this.store.byPlayGames(key);
    if (existing) {
      if (existing.id !== uid) { this.detach(s); this.attach(s, existing); }
      return s.conn.send({ t: 'signedIn', via, fresh: false });
    }
    const link = (id: string) => {
      if (via === 'phone') this.store.setPhone(id, key); else if (via === 'google') this.store.setGoogle(id, key, email); else if (via === 'apple') this.store.setApple(id, key, email); else this.store.setPlayGames(id, key);
      const u = this.store.get(id)!;
      // a guest name like Player1234 becomes the Google first name
      const clean = cleanName(name);
      if (via !== 'phone' && clean && /^Player\d{4}$/.test(u.name)) this.store.setProfile(id, clean, u.ch);
    };
    const me = this.store.get(uid)!;
    if (via === 'phone' ? me.phone : via === 'google' ? me.google_sub : via === 'apple' ? me.apple_sub : me.pg_id) {
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
    if (u.banned) { s.conn.send({ t: 'err', code: 'banned', reason: u.ban_reason ?? '' }); s.conn.close(4003, 'banned'); return; }
    this.attach(s, u);
    // the dashboard's notice and maintenance message
    const st = this.opts.settings?.value;
    if (st?.notice.text && st.notice.until > Date.now()) s.conn.send({ t: 'notice', text: st.notice.text });
    if (st?.maintenance.on) s.conn.send({ t: 'notice', text: st.maintenance.message });
    s.conn.send({ t: 'titles', list: this.titles.list });
  }

  private attach(s: Session, u: UserRow) {
    const prev = this.byUser.get(u.id);
    if (prev && prev !== s) { prev.uid = null; prev.conn.close(4001, 'replaced'); }
    s.uid = u.id;
    this.byUser.set(u.id, s);
    this.store.touch(u.id);
    // the daily reward: newer phones show the track and the player collects it; older ones get it at once
    const daily = (s.v ?? 1) < 2 ? this.store.claimDaily(u.id) : { coins: 0, streak: 0 };
    const fresh = this.store.get(u.id) ?? u;
    s.conn.send({ t: 'welcome', user: this.pub(fresh), token: this.tokens.issue(u.id), now: Date.now(), ice: this.iceFor(u.id),
      login: { google: !!this.opts.google?.enabled, apple: !!this.opts.apple?.enabled, phone: !!this.opts.phones?.enabled, playGames: this.opts.google?.playGamesEnabled ? this.opts.google.clientId : undefined } });
    this.friendsChanged(u.id);
    if (daily.coins) s.conn.send({ t: 'bonus', coins: daily.coins, streak: daily.streak });
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

  private sendCashouts(uid: string) {
    const cs = this.cash;
    this.byUser.get(uid)?.conn.send({ t: 'cashouts', rows: this.store.cashoutsOf(uid), rate: { coins: cs.coins, egp: cs.egp }, maxMillions: cs.maxUnits });
  }

  /** Lets the player know (cash-out paid or turned down), if they're connected. */
  refreshPlayer(uid: string) { this.sendUser(uid); this.sendCashouts(uid); }

  /** The live cash-out rules. */
  private get cash(): GameSettings['cashout'] { return this.opts.settings?.value.cashout ?? CASHOUT; }
  /** A player as the app sees them, with what the cash-out offers right now. */
  private pub(u: UserRow) {
    const c = this.cash;
    return { ...this.store.publicUser(u), cashout: { on: c.enabled, coins: c.coins, egp: c.egp, maxUnits: c.maxUnits, minGames: c.minGames }, adReward: ECONOMY.adReward,
      // what the dashboard switched on: the tournament section and the season pass
      features: { tourney: TOURNEY.enabled !== false, season: SEASON_PASS.enabled } };
  }
  /** How long matchmaking waits for people, and a turn's length (dashboard). */
  setTiming(botFillMs: number, turnMs: number) { this.opts.botFillMs = botFillMs; this.opts.turnMs = turnMs; }
  /** Players online now. */
  get onlineIds(): string[] { return [...this.byUser.keys()]; }
  /** A message on every open app (the dashboard's notice). */
  noticeAll(text: string) { for (const ss of this.byUser.values()) ss.conn.send({ t: 'notice', text }); }
  /** Someone's account changed from the dashboard: their app updates (or, banned, they are sent away). */
  adminChanged(uid: string) {
    const u = this.store.get(uid), ss = this.byUser.get(uid);
    if (!u || !ss) return;
    if (u.banned) { ss.conn.send({ t: 'err', code: 'banned', reason: u.ban_reason ?? '' }); this.detach(ss); ss.conn.close(4003, 'banned'); return; }
    this.titleToTable(uid);
    this.sendUser(uid);
  }
  /** The title a player wears, onto their seat at the table (when it changed from outside the game). */
  private titleToTable(uid: string) { const t = this.userTable.get(uid); if (t && !t.closed) t.setTitle(uid, this.store.wornTitle(uid) ?? undefined); }

  private sendUser(uid: string) {
    const u = this.store.get(uid); const s = this.byUser.get(uid);
    if (u && s) s.conn.send({ t: 'user', user: this.pub(u) });
  }

  /* ---------------- tables ---------------- */

  /** A person as they sit at a table: name, character, rating in this game and their look (hat, drink). */
  private player(uid: string, game: GameId): Player {
    const u = this.store.get(uid)!;
    const eq = parseEquip(u.equip), owned = this.store.skinsOf(uid);
    return { uid, name: u.name, ch: u.ch, bot: false, rating: this.store.ratingOf(uid, game),
      hat: skinFor('hat', eq, owned), drink: skinFor('drink', eq, owned), muted: (u.muted_until ?? 0) > Date.now(), title: this.store.wornTitle(uid) ?? undefined };
  }

  /** Computer players in matchmade games and the tournament get people's names and a rating near the table's. */
  private botMaker(rating: number): BotMaker { return (_seat, used) => botIdentity(used, rating); }

  private newTable(mode: Mode, target: number, isPrivate: boolean, stake: number, humanBots = false): Room {
    let code: string | null = null;
    if (isPrivate) {
      do { code = Array.from({ length: 5 }, () => CODE_ALPHABET[crypto.randomInt(CODE_ALPHABET.length)]).join(''); } while (this.byCode.has(code));
    }
    const opts = {
      id: crypto.randomUUID(), code, mode, target, private: isPrivate, stake,
      turnMs: this.opts.turnMs, nextRoundMs: this.opts.nextRoundMs, botDelay: this.opts.botDelay, offlineGraceMs: this.opts.graceMs ?? 30_000, lobbyAfterMatchMs: this.opts.lobbyAfterMatchMs, humanBots,
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
    if (t && this.rematch.has(t.id)) this.sendRematch(t, true);
  }

  /* ---------------- rematch: the same people again ---------------- */

  private voteRematch(uid: string) {
    const t = this.userTable.get(uid);
    if (!t || t.closed || t.isPrivate || t.phase !== 'mend' || this.tourneyTables.has(t.id)) return;
    const votes = this.rematch.get(t.id) ?? new Set<string>();
    votes.add(uid); this.rematch.set(t.id, votes);
    const here = t.humans().filter(h => h.online && this.userTable.get(h.uid) === t);
    if (here.every(h => votes.has(h.uid))) return this.startRematch(t, here.map(h => h.uid));
    this.sendRematch(t, here.length < t.humans().length);
  }

  private sendRematch(t: Room, gone: boolean) {
    const votes = this.rematch.get(t.id) ?? new Set<string>();
    const seats = t.seats.map((s, i) => (s && !s.bot && votes.has(s.uid) ? i : -1)).filter(i => i >= 0);
    const here = t.humans().filter(h => h.online && this.userTable.get(h.uid) === t);
    for (const h of here) this.byUser.get(h.uid)?.conn.send({ t: 'rematch', seats, need: here.length, gone: gone || here.length < t.humans().length });
  }

  private startRematch(old: Room, people: string[]) {
    this.rematch.delete(old.id);
    if (people.some(uid => this.store.coinsOf(uid) < old.stake)) {
      for (const uid of people) this.byUser.get(uid)?.conn.send({ t: 'err', code: 'rematch_coins' });
      return;
    }
    const t = this.newTable(old.mode, old.target, false, old.stake, true);
    const game = gameOf(old.mode);
    // the same seats: people where they sat, the same computer players in theirs
    old.seats.forEach((s, i) => {
      if (s && !s.bot && people.includes(s.uid)) { t.join(this.player(s.uid, game)); this.userTable.set(s.uid, t); }
      else if (s?.bot) { t.join({ uid: 'bot' + i, name: s.name, ch: s.ch, bot: true, rating: s.rating }); }
      else t.join({ uid: 'bot' + i, ...botIdentity(t.seats.filter(Boolean).map(x => x!.ch), 1000), bot: true });
    });
    if (!this.takeStakes(t)) { people.forEach(uid => { this.userTable.delete(uid); this.byUser.get(uid)?.conn.send({ t: 'err', code: 'rematch_coins' }); }); t.close(); return; }
    t.start(null);
    old.close();
    this.botHello(t);
  }

  /* ---------------- the weekly tournament's tables ---------------- */

  /** A match of the tournament: both entrants seated (people as away until they come), started at once. */
  private tourneyTable(a: Entrant, b: Entrant, mode: Mode, target: number): string {
    const t = this.newTable(mode, target, false, 0, true);
    const game = gameOf(mode);
    for (const e of [a, b]) {
      if (e.uid) t.join(this.player(e.uid, game));
      else t.join({ uid: 'bot' + t.seats.filter(Boolean).length, name: e.name, ch: e.ch, bot: true, rating: e.rating });
    }
    this.tourneyTables.set(t.id, [a.uid, b.uid]);
    t.start(null);
    // people who aren't at this table yet: away until they open their match (the computer covers after the grace time)
    for (const e of [a, b]) if (e.uid && this.userTable.get(e.uid) !== t) t.setOnline(e.uid, false);
    // anyone sitting idle in the menus is taken there straight away
    for (const e of [a, b]) if (e.uid && this.byUser.has(e.uid) && !this.userTable.has(e.uid)) { this.userTable.set(e.uid, t); t.setOnline(e.uid, true); this.pushView(t, e.uid); }
    return t.id;
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
    // the weekly leaderboard counts matchmade games and rooms with at least two people (not a room full of bots)
    const weekly = !t.isPrivate || t.seats.filter(s => s && !s.bot).length >= 2;
    const vsPeople = t.seats.filter(s => s && !s.bot).length >= 2 || !t.isPrivate;
    const xpFor = (uid: string, won: boolean) => {
      const r = this.store.addXp(uid, won ? XP.win : XP.loss);
      this.byUser.get(uid)?.conn.send({ t: 'xp', ...r });
      this.afterGame(uid, game, won, vsPeople, won ? SP.win : SP.loss);
    };
    // the tournament: who goes on
    if (this.tourneyTables.has(t.id)) setTimeout(() => this.tourney.onResult(t.id, res.team), 0);
    // computer players with people's names say "good game" sometimes
    if (!t.isPrivate) t.seats.forEach((s, i) => { if (s?.bot && Math.random() < 0.45) setTimeout(() => { if (!t.closed) this.emote(t, i, Math.random() < 0.7 ? 'gg' : 'clap'); }, 1200 + Math.random() * 2500); });
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
        this.store.recordResult(s.uid, res.deltas[i], won, game, weekly);
        xpFor(s.uid, won);
        this.sendUser(s.uid);
      });
    } else {
      t.seats.forEach((s, i) => { if (s && !s.bot) { const won = teamOf(t.mode, i) === res.team; this.store.recordResult(s.uid, null, won, game, weekly); xpFor(s.uid, won); this.sendUser(s.uid); } });
    }
    this.settle(t, res.team);
    this.store.saveMatch({ id: t.id + ':' + Date.now(), mode: t.mode, private: t.isPrivate, players, scores: res.scores, team: res.team });
  }

  /** After any finished game (online or practice on the phone): missions, season points, the referral bonus. */
  private afterGame(uid: string, game: GameId, won: boolean, online: boolean, sp: number) {
    const conn = this.byUser.get(uid)?.conn;
    for (const id of this.store.missionProgress(uid, game, won, online)) conn?.send({ t: 'missionDone', id });
    if (online) this.store.addMonthly(uid, game, won, seasonOf(Date.now()));   // the race for player of the month
    const tier = this.store.addSeasonSp(uid, sp);
    if (tier.to > tier.from) conn?.send({ t: 'seasonTier', tier: tier.to });
    this.payReferral(uid);
  }

  private payReferral(uid: string) {
    const r = this.store.referralFirstGame(uid);
    if (!r) return;
    const name = this.store.get(r.referrer)?.name ?? '';
    this.byUser.get(uid)?.conn.send({ t: 'referral', coins: r.coins, name, you: 'invited' });
    this.byUser.get(r.referrer)?.conn.send({ t: 'referral', coins: r.coins, name: r.name, you: 'inviter' });
    this.sendUser(uid); this.sendUser(r.referrer);
    this.opts.push?.([r.referrer], ['صاحبك وصل! 🎁', `${r.name} لعب أول ماتش، وخدتوا انتو الاتنين ${r.coins.toLocaleString('en')} كوين`], ['Your friend joined! 🎁', `${r.name} played their first game: you both got ${r.coins.toLocaleString('en')} coins`]);
  }

  private onClosed(t: Room) {
    this.rematch.delete(t.id);
    const tt = this.tourneyTables.get(t.id);
    if (tt) {
      this.tourneyTables.delete(t.id);
      // closed before the end (everyone left): whoever is still seated and online goes on
      const stayed = t.match ? null : tt.findIndex(uid => uid && t.seatOf(uid) >= 0 && t.seats[t.seatOf(uid)]?.online);
      if (!t.match) this.tourney.onClosed(t.id, stayed !== null && stayed >= 0 ? stayed : (tt[0] === null ? 0 : tt[1] === null ? 1 : null));
    }
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
      if (this.tourneyTables.has(t.id)) continue;   // tournament matches play on (the computer covers people who aren't there)
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

  /**
   * Matchmaking. People wait in a queue per game mode and stake. A table is formed from players of a similar
   * level (rating) and wealth (coins): at first only close matches, the range widening the longer the first
   * person has waited. After botFillMs (8 s) the table is filled with computer players instead.
   */
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
      while (q.length) {
        const first = q[0];
        const waited = (now - first.since) / 1000;
        const close = (e: QEntry) => {
          const w = Math.max(waited, (now - e.since) / 1000);
          const ratingOk = Math.abs(e.rating - first.rating) <= 120 + 30 * w;
          const lo = Math.max(1, Math.min(e.coins, first.coins)), hi = Math.max(e.coins, first.coins);
          const coinsOk = hi / lo <= 6 * Math.pow(2, w / 3);
          return ratingOk && coinsOk;
        };
        const group = [first, ...q.slice(1).filter(close)].slice(0, need);
        const timeUp = now - first.since >= this.opts.botFillMs;
        if (group.length < need && !timeUp) break;
        for (const e of group) q.splice(q.indexOf(e), 1);
        const t = this.newTable(mode, defaultTarget(mode), false, stake, true);
        const game = gameOf(mode);
        group.forEach(e => { t.join(this.player(e.uid, game)); this.userTable.set(e.uid, t); });
        const avg = group.reduce((a, e) => a + e.rating, 0) / group.length;
        t.fillBots(this.botMaker(avg));
        if (!this.takeStakes(t)) { group.forEach(e => { this.userTable.delete(e.uid); this.byUser.get(e.uid)?.conn.send({ t: 'err', code: 'not_enough_coins' }); }); t.close(); continue; }
        t.start(null);
        this.botHello(t);
        formed = true;
      }
      if (formed || q.length) this.sendQueue(key);
      if (!q.length) this.queues.delete(key);
    }
  }

  /** Computer players with people's names greet the table now and then, like people do. */
  private botHello(t: Room) {
    t.seats.forEach((s, i) => {
      if (s?.bot && s.name && Math.random() < 0.35) setTimeout(() => { if (!t.closed) this.emote(t, i, Math.random() < 0.75 ? 'hi' : 'think'); }, 1500 + Math.random() * 4000);
    });
  }
}

/** Cash out: every 1,000,000 coins is 10 EGP on Vodafone Cash, paid by hand (see /admin/cashouts). */
/** (Without the dashboard's settings: off, 1,000,000 coins = 5 EGP.) */
export const CASHOUT: GameSettings['cashout'] = { enabled: false, coins: 1_000_000, egp: 5, maxUnits: 10, minGames: 0, minAccountDays: 0, prefixes: ['010'] };

/** A Vodafone Cash number as 010xxxxxxxx (accepts +20 / 0020, spaces and Arabic digits), or null. */
export function walletNumber(raw: string, prefixes: string[] = ['010']): string | null {
  let d = raw.replace(/[\u0660-\u0669]/g, c => String(c.charCodeAt(0) - 0x0660)).replace(/[\u06F0-\u06F9]/g, c => String(c.charCodeAt(0) - 0x06F0)).replace(/\D/g, '');
  if (d.startsWith('0020')) d = '0' + d.slice(4);
  else if (d.startsWith('20') && d.length === 12) d = '0' + d.slice(2);
  else if (d.length === 10 && d.startsWith('1')) d = '0' + d;
  return /^01\d{9}$/.test(d) && prefixes.includes(d.slice(0, 3)) ? d : null;
}

/** When this week's leaderboard closes (next Monday 00:00 UTC). */
export function weekEndsAt(ms = Date.now()): number {
  const d = new Date(ms); d.setUTCHours(0, 0, 0, 0);
  const day = (d.getUTCDay() + 6) % 7;
  return d.getTime() + (7 - day) * 86_400_000;
}
