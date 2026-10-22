// Storage on Node's built-in SQLite: one file, no separate database server to run.
import { DatabaseSync } from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import type { CashoutRow, CoinRow, LeaderRow, PublicUser } from '../shared/protocol';
import type { GameId } from '../shared/engine';
import { ECONOMY, WEEKLY_PRIZES, cairoDay, levelOf, nextCairoDay, streakBonus, weekKey } from '../shared/gifts';
import { Equipped, skinById } from '../shared/skins';
import { maskPhone } from './login';

export interface UserRow {
  id: string; name: string; ch: number; email: string | null; pw: string | null;
  rating: number; games: number; wins: number; created: number; seen: number;
  coins: number; gifts: number; bonus_day: string | null; ad_free_until: number; equip: string | null;
  phone: string | null; google_sub: string | null; google_email: string | null;
  pg_id: string | null; friend_code: string | null; streak?: number; xp?: number;
}

export class Store {
  private db: DatabaseSync;

  constructor(file: string) {
    if (file !== ':memory:') fs.mkdirSync(path.dirname(file), { recursive: true });
    this.db = new DatabaseSync(file);
    this.db.exec(`
      PRAGMA journal_mode = WAL;
      PRAGMA busy_timeout = 3000;
      CREATE TABLE IF NOT EXISTS meta (k TEXT PRIMARY KEY, v TEXT NOT NULL);
      CREATE TABLE IF NOT EXISTS users (
        id TEXT PRIMARY KEY, name TEXT NOT NULL, ch INTEGER NOT NULL DEFAULT 0,
        email TEXT UNIQUE, pw TEXT, rating INTEGER NOT NULL DEFAULT 1000,
        games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0,
        created INTEGER NOT NULL, seen INTEGER NOT NULL
      );
      CREATE INDEX IF NOT EXISTS users_rating ON users(rating DESC) WHERE games >= 3;
      CREATE TABLE IF NOT EXISTS matches (
        id TEXT PRIMARY KEY, mode TEXT NOT NULL, private INTEGER NOT NULL, ended INTEGER NOT NULL,
        players TEXT NOT NULL, scores TEXT NOT NULL, winner_team INTEGER NOT NULL
      );
      CREATE TABLE IF NOT EXISTS gifts (
        id INTEGER PRIMARY KEY AUTOINCREMENT, from_id TEXT NOT NULL, to_id TEXT NOT NULL,
        kind TEXT NOT NULL, price INTEGER NOT NULL, ts INTEGER NOT NULL
      );
      CREATE TABLE IF NOT EXISTS ratings (
        user_id TEXT NOT NULL, game TEXT NOT NULL, rating INTEGER NOT NULL DEFAULT 1000,
        games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (user_id, game)
      );
      CREATE INDEX IF NOT EXISTS ratings_board ON ratings(game, rating DESC);
      CREATE TABLE IF NOT EXISTS coin_log (
        id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL, ts INTEGER NOT NULL, kind TEXT NOT NULL,
        delta INTEGER NOT NULL, fee INTEGER NOT NULL DEFAULT 0, stake INTEGER NOT NULL DEFAULT 0, mode TEXT, balance INTEGER NOT NULL
      );
      CREATE INDEX IF NOT EXISTS coin_log_user ON coin_log(user_id, id DESC);
      CREATE TABLE IF NOT EXISTS reports (
        id INTEGER PRIMARY KEY AUTOINCREMENT, from_id TEXT NOT NULL, about_id TEXT NOT NULL,
        reason TEXT NOT NULL, table_id TEXT NOT NULL, ts INTEGER NOT NULL
      );
    `);
    // columns added after the first release
    const cols = new Set((this.db.prepare('PRAGMA table_info(users)').all() as { name: string }[]).map(c => c.name));
    if (!cols.has('coins')) this.db.exec(`ALTER TABLE users ADD COLUMN coins INTEGER NOT NULL DEFAULT ${ECONOMY.startCoins}`);
    if (!cols.has('gifts')) this.db.exec('ALTER TABLE users ADD COLUMN gifts INTEGER NOT NULL DEFAULT 0');
    if (!cols.has('bonus_day')) this.db.exec('ALTER TABLE users ADD COLUMN bonus_day TEXT');
    if (!cols.has('phone')) this.db.exec('ALTER TABLE users ADD COLUMN phone TEXT');
    if (!cols.has('google_sub')) this.db.exec('ALTER TABLE users ADD COLUMN google_sub TEXT; ALTER TABLE users ADD COLUMN google_email TEXT');
    if (!cols.has('ad_free_until')) this.db.exec('ALTER TABLE users ADD COLUMN ad_free_until INTEGER NOT NULL DEFAULT 0');
    if (!cols.has('equip')) this.db.exec('ALTER TABLE users ADD COLUMN equip TEXT');
    if (!cols.has('pg_id')) this.db.exec('ALTER TABLE users ADD COLUMN pg_id TEXT');
    if (!cols.has('friend_code')) this.db.exec('ALTER TABLE users ADD COLUMN friend_code TEXT');
    if (!cols.has('streak')) this.db.exec('ALTER TABLE users ADD COLUMN streak INTEGER NOT NULL DEFAULT 0');
    if (!cols.has('xp')) this.db.exec('ALTER TABLE users ADD COLUMN xp INTEGER NOT NULL DEFAULT 0');
    // the week's points for the weekly leaderboard
    this.db.exec(`CREATE TABLE IF NOT EXISTS weekly (week TEXT NOT NULL, user_id TEXT NOT NULL, points INTEGER NOT NULL DEFAULT 0,
      games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (week, user_id));
      CREATE INDEX IF NOT EXISTS weekly_rank ON weekly(week, points DESC)`);
    this.db.exec('CREATE UNIQUE INDEX IF NOT EXISTS users_pg ON users(pg_id) WHERE pg_id IS NOT NULL; CREATE UNIQUE INDEX IF NOT EXISTS users_fcode ON users(friend_code) WHERE friend_code IS NOT NULL');
    // friends: a row a→b is a request from a to b until b accepts it
    this.db.exec(`CREATE TABLE IF NOT EXISTS friends (a TEXT NOT NULL, b TEXT NOT NULL, status TEXT NOT NULL, ts INTEGER NOT NULL, PRIMARY KEY (a, b));
      CREATE INDEX IF NOT EXISTS friends_b ON friends(b)`);
    this.db.exec('CREATE TABLE IF NOT EXISTS user_skins (user_id TEXT NOT NULL, skin TEXT NOT NULL, ts INTEGER NOT NULL, PRIMARY KEY (user_id, skin))');
    // every store purchase, so the same purchase can never be credited twice
    this.db.exec(`CREATE TABLE IF NOT EXISTS purchases (
      token TEXT PRIMARY KEY, user_id TEXT NOT NULL, product TEXT NOT NULL, order_id TEXT, ts INTEGER NOT NULL, coins INTEGER NOT NULL DEFAULT 0, until INTEGER NOT NULL DEFAULT 0)`);
    // phones for push reminders (Firebase Cloud Messaging), and cash-out requests paid by hand
    this.db.exec(`CREATE TABLE IF NOT EXISTS push_tokens (token TEXT PRIMARY KEY, user_id TEXT NOT NULL, lang TEXT NOT NULL DEFAULT 'ar', platform TEXT NOT NULL DEFAULT 'android', ts INTEGER NOT NULL);
      CREATE INDEX IF NOT EXISTS push_user ON push_tokens(user_id);
      CREATE TABLE IF NOT EXISTS cashouts (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL, coins INTEGER NOT NULL, egp INTEGER NOT NULL,
        phone TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'pending', ts INTEGER NOT NULL, done_ts INTEGER, note TEXT);
      CREATE INDEX IF NOT EXISTS cashouts_user ON cashouts(user_id, id DESC);
      CREATE INDEX IF NOT EXISTS cashouts_status ON cashouts(status, id);
      CREATE TABLE IF NOT EXISTS support (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT, name TEXT, contact TEXT, text TEXT NOT NULL,
        app TEXT, ts INTEGER NOT NULL, mailed INTEGER NOT NULL DEFAULT 0)`);
    this.db.exec('CREATE UNIQUE INDEX IF NOT EXISTS users_phone ON users(phone) WHERE phone IS NOT NULL; CREATE UNIQUE INDEX IF NOT EXISTS users_google ON users(google_sub) WHERE google_sub IS NOT NULL');
    // coins moved to stakes from 5K to 1M: older balances are scaled up once
    if (!this.db.prepare("SELECT v FROM meta WHERE k = 'coins_v2'").get()) {
      this.db.exec("UPDATE users SET coins = coins * 100; INSERT INTO meta (k, v) VALUES ('coins_v2', '1')");
    }
  }

  secret(fromEnv: string): string {
    if (fromEnv) return fromEnv;
    const row = this.db.prepare('SELECT v FROM meta WHERE k = ?').get('secret') as { v: string } | undefined;
    if (row) return row.v;
    const s = crypto.randomBytes(32).toString('hex');
    this.db.prepare('INSERT INTO meta (k, v) VALUES (?, ?)').run('secret', s);
    return s;
  }

  createGuest(name: string, ch: number): UserRow {
    const id = 'u' + crypto.randomBytes(9).toString('base64url');
    const now = Date.now();
    this.db.prepare('INSERT INTO users (id, name, ch, coins, created, seen) VALUES (?, ?, ?, ?, ?, ?)').run(id, name, ch, ECONOMY.startCoins, now, now);
    this.logCoins(id, 'welcome', ECONOMY.startCoins);
    return this.get(id)!;
  }

  get(id: string): UserRow | undefined {
    return this.db.prepare('SELECT * FROM users WHERE id = ?').get(id) as UserRow | undefined;
  }

  byEmail(email: string): UserRow | undefined {
    return this.db.prepare('SELECT * FROM users WHERE email = ?').get(email) as UserRow | undefined;
  }

  byPhone(phone: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE phone = ?').get(phone) as UserRow | undefined; }
  byGoogle(sub: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE google_sub = ?').get(sub) as UserRow | undefined; }
  setPhone(id: string, phone: string) { this.db.prepare('UPDATE users SET phone = ? WHERE id = ?').run(phone, id); }
  setGoogle(id: string, sub: string, email: string | null) { this.db.prepare('UPDATE users SET google_sub = ?, google_email = ? WHERE id = ?').run(sub, email, id); }
  byPlayGames(pid: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE pg_id = ?').get(pid) as UserRow | undefined; }
  setPlayGames(id: string, pid: string) { this.db.prepare('UPDATE users SET pg_id = ? WHERE id = ?').run(pid, id); }

  /* ---------- friends ---------- */

  /** The short code a player gives to friends (made the first time it is asked for). */
  friendCode(id: string): string {
    const u = this.get(id); if (!u) return '';
    if (u.friend_code) return u.friend_code;
    const ABC = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    for (;;) {
      const code = Array.from({ length: 6 }, () => ABC[crypto.randomInt(ABC.length)]).join('');
      if (this.db.prepare('SELECT 1 FROM users WHERE friend_code = ?').get(code)) continue;
      this.db.prepare('UPDATE users SET friend_code = ? WHERE id = ?').run(code, id);
      return code;
    }
  }
  byFriendCode(code: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE friend_code = ?').get(code.toUpperCase()) as UserRow | undefined; }
  /** Everyone in this player's list: friends, requests they sent, requests waiting for them. */
  friendLinks(id: string): { other: string; status: 'friend' | 'outgoing' | 'incoming' }[] {
    const rows = this.db.prepare('SELECT a, b, status FROM friends WHERE a = ? OR b = ?').all(id, id) as { a: string; b: string; status: string }[];
    return rows.map(r => ({ other: r.a === id ? r.b : r.a, status: r.status === 'accepted' ? 'friend' as const : r.a === id ? 'outgoing' as const : 'incoming' as const }));
  }
  friendStatus(a: string, b: string): 'friend' | 'outgoing' | 'incoming' | null {
    return this.friendLinks(a).find(l => l.other === b)?.status ?? null;
  }
  friendCount(id: string): number { return (this.db.prepare('SELECT COUNT(*) AS n FROM friends WHERE a = ? OR b = ?').get(id, id) as { n: number }).n; }
  /** a asks b; if b had already asked a, they become friends. */
  friendRequest(a: string, b: string): 'sent' | 'friends' | 'already' {
    const st = this.friendStatus(a, b);
    if (st === 'friend' || st === 'outgoing') return 'already';
    if (st === 'incoming') { this.db.prepare("UPDATE friends SET status = 'accepted', ts = ? WHERE a = ? AND b = ?").run(Date.now(), b, a); return 'friends'; }
    this.db.prepare("INSERT INTO friends (a, b, status, ts) VALUES (?, ?, 'pending', ?)").run(a, b, Date.now());
    return 'sent';
  }
  friendAccept(me: string, from: string): boolean {
    return Number(this.db.prepare("UPDATE friends SET status = 'accepted', ts = ? WHERE a = ? AND b = ? AND status = 'pending'").run(Date.now(), from, me).changes) > 0;
  }
  friendRemove(a: string, b: string) { this.db.prepare('DELETE FROM friends WHERE (a = ? AND b = ?) OR (a = ? AND b = ?)').run(a, b, b, a); }

  touch(id: string) { this.db.prepare('UPDATE users SET seen = ? WHERE id = ?').run(Date.now(), id); }
  setProfile(id: string, name: string, ch: number) { this.db.prepare('UPDATE users SET name = ?, ch = ? WHERE id = ?').run(name, ch, id); }
  setCredentials(id: string, email: string, pw: string) { this.db.prepare('UPDATE users SET email = ?, pw = ? WHERE id = ?').run(email, pw, id); }

  /** Removes a player for good (App Store / GDPR account deletion). Past match rows only keep the opaque id. */
  deleteUser(id: string) {
    this.db.prepare('DELETE FROM users WHERE id = ?').run(id);
    this.db.prepare('DELETE FROM gifts WHERE from_id = ? OR to_id = ?').run(id, id);
    this.db.prepare('DELETE FROM reports WHERE from_id = ?').run(id);
    this.db.prepare('DELETE FROM ratings WHERE user_id = ?').run(id);
    this.db.prepare('DELETE FROM coin_log WHERE user_id = ?').run(id);
    this.db.prepare('DELETE FROM friends WHERE a = ? OR b = ?').run(id, id);
    this.db.prepare('DELETE FROM push_tokens WHERE user_id = ?').run(id);
  }

  /** The daily bonus, once per (UTC) day; it grows for each day in a row (STREAK). Returns the coins and the day. */
  claimDaily(id: string): { coins: number; streak: number } {
    const now = Date.now();
    const day = cairoDay(now), yesterday = cairoDay(now - 86_400_000);
    const u = this.get(id);
    if (!u || u.bonus_day === day) return { coins: 0, streak: u?.streak ?? 0 };
    const streak = u.bonus_day === yesterday ? (u.streak ?? 0) + 1 : 1;
    const coins = streakBonus(streak);
    const r = this.db.prepare('UPDATE users SET coins = coins + ?, bonus_day = ?, streak = ? WHERE id = ? AND (bonus_day IS NULL OR bonus_day <> ?)')
      .run(coins, day, streak, id, day);
    if (!Number(r.changes)) return { coins: 0, streak: u.streak ?? 0 };
    this.logCoins(id, 'daily', coins);
    return { coins, streak };
  }

  /** Where the player is on the daily reward track: the day today's reward is (or was) for, whether it can be
   *  collected now, and when the next one can. */
  dailyState(u: UserRow, now = Date.now()): { day: number; ready: boolean; nextAt: number; coins: number } {
    const today = cairoDay(now), yesterday = cairoDay(now - 86_400_000);
    if (u.bonus_day === today) { const day = Math.max(1, u.streak ?? 1); return { day, ready: false, nextAt: nextCairoDay(now), coins: streakBonus(day) }; }
    const day = u.bonus_day === yesterday ? (u.streak ?? 0) + 1 : 1;
    return { day, ready: true, nextAt: now, coins: streakBonus(day) };
  }

  /** Experience for a finished game. Returns what was gained and whether a new level was reached. */
  addXp(id: string, gained: number): { gained: number; xp: number; level: number; levelUp: boolean } {
    const before = (this.db.prepare('SELECT xp FROM users WHERE id = ?').get(id) as { xp: number } | undefined)?.xp ?? 0;
    const xp = before + gained;
    this.db.prepare('UPDATE users SET xp = ? WHERE id = ?').run(xp, id);
    return { gained, xp, level: levelOf(xp), levelUp: levelOf(xp) > levelOf(before) };
  }

  /** A finished online game counts for this week's leaderboard: 3 points a win, 1 for playing. */
  addWeekly(id: string, won: boolean, week = weekKey()) {
    this.db.prepare(`INSERT INTO weekly (week, user_id, points, games, wins) VALUES (?, ?, ?, 1, ?)
      ON CONFLICT(week, user_id) DO UPDATE SET points = points + excluded.points, games = games + 1, wins = wins + excluded.wins`)
      .run(week, id, won ? 3 : 1, won ? 1 : 0);
  }

  weeklyBoard(meId: string | null, week = weekKey(), limit = 50): { rows: LeaderRow[]; me: LeaderRow | null } {
    const rows = this.db.prepare(`SELECT u.id, u.name, u.ch, u.xp, w.points, w.games, w.wins FROM weekly w JOIN users u ON u.id = w.user_id
      WHERE w.week = ? ORDER BY w.points DESC, w.wins DESC, w.games ASC LIMIT ?`).all(week, limit) as { id: string; name: string; ch: number; xp: number; points: number; games: number; wins: number }[];
    const out = rows.map((r, i) => ({ rank: i + 1, name: r.name, ch: r.ch, rating: r.points, games: r.games, wins: r.wins, me: r.id === meId, level: levelOf(r.xp) }));
    let me: LeaderRow | null = null;
    const mine = meId ? this.db.prepare('SELECT points, games, wins FROM weekly WHERE week = ? AND user_id = ?').get(week, meId) as { points: number; games: number; wins: number } | undefined : undefined;
    const u = meId ? this.get(meId) : undefined;
    if (u && mine) {
      const above = this.db.prepare('SELECT COUNT(*) AS c FROM weekly WHERE week = ? AND points > ?').get(week, mine.points) as { c: number };
      me = { rank: Number(above.c) + 1, name: u.name, ch: u.ch, rating: mine.points, games: mine.games, wins: mine.wins, me: true, level: levelOf(u.xp ?? 0) };
    }
    return { rows: out, me };
  }

  /** Pays last week's top ten once (WEEKLY_PRIZES). Returns who got what. */
  payWeekly(week: string): { uid: string; rank: number; coins: number }[] {
    if (this.db.prepare("SELECT v FROM meta WHERE k = 'weekly_paid_' || ?").get(week)) return [];
    const top = this.db.prepare('SELECT user_id FROM weekly WHERE week = ? ORDER BY points DESC, wins DESC, games ASC LIMIT ?').all(week, WEEKLY_PRIZES.length) as { user_id: string }[];
    this.db.prepare("INSERT INTO meta (k, v) VALUES ('weekly_paid_' || ?, ?)").run(week, String(Date.now()));
    return top.map((r, i) => {
      this.addCoins(r.user_id, WEEKLY_PRIZES[i]); this.logCoins(r.user_id, 'weekly', WEEKLY_PRIZES[i]);
      return { uid: r.user_id, rank: i + 1, coins: WEEKLY_PRIZES[i] };
    });
  }

  coinsOf(id: string) { return (this.db.prepare('SELECT coins FROM users WHERE id = ?').get(id) as { coins: number } | undefined)?.coins ?? 0; }

  /** Adds (or with a negative n, takes) coins. */
  addCoins(id: string, n: number) { if (n) this.db.prepare('UPDATE users SET coins = coins + ? WHERE id = ?').run(n, id); }

  /** Writes a line in the player's coin history. */
  logCoins(id: string, kind: string, delta: number, extra: { fee?: number; stake?: number; mode?: string } = {}) {
    this.db.prepare('INSERT INTO coin_log (user_id, ts, kind, delta, fee, stake, mode, balance) VALUES (?, ?, ?, ?, ?, ?, ?, ?)')
      .run(id, Date.now(), kind, delta, extra.fee ?? 0, extra.stake ?? 0, extra.mode ?? null, this.coinsOf(id));
  }

  /** Takes the stake from every player at once, or from nobody if one of them can't cover it. */
  takeStakes(ids: string[], stake: number): boolean {
    this.db.exec('BEGIN');
    try {
      for (const id of ids) {
        const r = this.db.prepare('UPDATE users SET coins = coins - ? WHERE id = ? AND coins >= ?').run(stake, id, stake);
        if (!Number(r.changes)) { this.db.exec('ROLLBACK'); return false; }
      }
      this.db.exec('COMMIT');
      return true;
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
  }

  /** Rewarded ads watched today (UTC). */
  adsToday(id: string): number {
    const day = new Date(); day.setUTCHours(0, 0, 0, 0);
    return Number((this.db.prepare("SELECT COUNT(*) AS c FROM coin_log WHERE user_id = ? AND kind = 'ad' AND ts >= ?").get(id, day.getTime()) as { c: number }).c);
  }

  coinHistory(id: string, limit = 60): CoinRow[] {
    return (this.db.prepare('SELECT ts, kind, delta, fee, stake, mode, balance FROM coin_log WHERE user_id = ? ORDER BY id DESC LIMIT ?').all(id, limit) as unknown as CoinRow[])
      .map(r => ({ ...r, mode: r.mode ?? null }));
  }

  /** Moves coins for a gift in one transaction. Returns false if the sender can't afford it. */
  sendGift(from: string, to: string, kind: string, price: number): boolean {
    this.db.exec('BEGIN');
    try {
      const r = this.db.prepare('UPDATE users SET coins = coins - ? WHERE id = ? AND coins >= ?').run(price, from, price);
      if (!Number(r.changes)) { this.db.exec('ROLLBACK'); return false; }
      this.db.prepare('UPDATE users SET gifts = gifts + 1 WHERE id = ?').run(to);
      this.db.prepare('INSERT INTO gifts (from_id, to_id, kind, price, ts) VALUES (?, ?, ?, ?, ?)').run(from, to, kind, price, Date.now());
      this.logCoins(from, 'gift', -price);
      this.db.exec('COMMIT');
      return true;
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
  }

  report(from: string, about: string, reason: string, tableId: string) {
    this.db.prepare('INSERT INTO reports (from_id, about_id, reason, table_id, ts) VALUES (?, ?, ?, ?, ?)').run(from, about, reason, tableId, Date.now());
  }

  reports(limit = 100) { return this.db.prepare('SELECT * FROM reports ORDER BY ts DESC LIMIT ?').all(limit); }

  /** A player's rating in one game (1000 until they have played it). */
  ratingOf(id: string, game: GameId): number {
    const r = this.db.prepare('SELECT rating FROM ratings WHERE user_id = ? AND game = ?').get(id, game) as { rating: number } | undefined;
    return r?.rating ?? 1000;
  }

  ratingsOf(id: string): PublicUser['ratings'] {
    const rows = this.db.prepare('SELECT game, rating, games, wins FROM ratings WHERE user_id = ?').all(id) as { game: GameId; rating: number; games: number; wins: number }[];
    const out: PublicUser['ratings'] = {};
    for (const r of rows) out[r.game] = { rating: r.rating, games: r.games, wins: r.wins };
    return out;
  }

  /** Records a finished match: the per-game rating and counts, plus the player's overall totals. */
  recordResult(id: string, delta: number | null, won: boolean, game: GameId = 'domino', weekly = true) {
    this.db.prepare(`INSERT INTO ratings (user_id, game, rating, games, wins) VALUES (?, ?, MAX(100, 1000 + ?), 1, ?)
      ON CONFLICT(user_id, game) DO UPDATE SET rating = MAX(100, rating + excluded.rating - 1000), games = games + 1, wins = wins + excluded.wins`)
      .run(id, game, delta ?? 0, won ? 1 : 0);
    this.db.prepare('UPDATE users SET games = games + 1, wins = wins + ? WHERE id = ?').run(won ? 1 : 0, id);
    if (game === 'domino') this.db.prepare('UPDATE users SET rating = MAX(100, rating + ?) WHERE id = ?').run(delta ?? 0, id);
    if (weekly) this.addWeekly(id, won);
  }

  saveMatch(m: { id: string; mode: string; private: boolean; players: unknown; scores: number[]; team: number }) {
    this.db.prepare('INSERT OR REPLACE INTO matches (id, mode, private, ended, players, scores, winner_team) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(m.id, m.mode, m.private ? 1 : 0, Date.now(), JSON.stringify(m.players), JSON.stringify(m.scores), m.team);
  }

  leaderboard(meId: string | null, game: GameId = 'domino', limit = 50): { rows: LeaderRow[]; me: LeaderRow | null } {
    const rows = this.db.prepare(`SELECT u.id, u.name, u.ch, r.rating, r.games, r.wins FROM ratings r JOIN users u ON u.id = r.user_id
      WHERE r.game = ? AND r.games >= 3 ORDER BY r.rating DESC, r.wins DESC LIMIT ?`)
      .all(game, limit) as unknown as Pick<UserRow, 'id' | 'name' | 'ch' | 'rating' | 'games' | 'wins'>[];
    const out = rows.map((r, i) => ({ rank: i + 1, name: r.name, ch: r.ch, rating: r.rating, games: r.games, wins: r.wins, me: r.id === meId, level: levelOf((this.get(r.id)?.xp) ?? 0) }));
    let me: LeaderRow | null = null;
    const u = meId ? this.get(meId) : undefined;
    const mine = meId ? this.db.prepare('SELECT rating, games, wins FROM ratings WHERE user_id = ? AND game = ?').get(meId, game) as { rating: number; games: number; wins: number } | undefined : undefined;
    if (u && mine && mine.games >= 3) {
      const above = this.db.prepare('SELECT COUNT(*) AS c FROM ratings WHERE game = ? AND games >= 3 AND rating > ?').get(game, mine.rating) as { c: number };
      me = { rank: Number(above.c) + 1, name: u.name, ch: u.ch, rating: mine.rating, games: mine.games, wins: mine.wins, me: true };
    }
    return { rows: out, me };
  }

  publicUser(u: UserRow): PublicUser { return { ...toPublic(u), xp: u.xp ?? 0, level: levelOf(u.xp ?? 0), streak: u.streak ?? 0, daily: this.dailyState(u), code: this.friendCode(u.id), ratings: this.ratingsOf(u.id), adsLeft: Math.max(0, ECONOMY.adsPerDay - this.adsToday(u.id)), skins: this.skinsOf(u.id) }; }

  /** Skins this player bought in the shop. */
  skinsOf(id: string): string[] { return (this.db.prepare('SELECT skin FROM user_skins WHERE user_id = ? ORDER BY ts').all(id) as { skin: string }[]).map(r => r.skin); }
  /** Buy a skin with coins. Returns an error code, or null when bought. */
  buySkin(id: string, skin: string): string | null {
    const s = skinById(skin);
    if (!s || s.price === 0) return 'bad_skin';
    if (this.skinsOf(id).includes(skin)) return null;
    if (this.coinsOf(id) < s.price) return 'not_enough_coins';
    this.db.exec('BEGIN');
    try {
      this.db.prepare('INSERT INTO user_skins (user_id, skin, ts) VALUES (?, ?, ?)').run(id, skin, Date.now());
      this.addCoins(id, -s.price); this.logCoins(id, 'skin', -s.price);
      this.db.exec('COMMIT');
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
    this.equipSkin(id, skin);   // wear it straight away
    return null;
  }
  /** Wear a skin the player owns (or a free one). */
  equipSkin(id: string, skin: string): string | null {
    const s = skinById(skin);
    if (!s) return 'bad_skin';
    if (s.price > 0 && !this.skinsOf(id).includes(skin)) return 'not_owned';
    const u = this.get(id); if (!u) return 'bad_skin';
    const equip = { ...parseEquip(u.equip), [s.cat]: skin };
    this.db.prepare('UPDATE users SET equip = ? WHERE id = ?').run(JSON.stringify(equip), id);
    return null;
  }

  /** A store purchase seen before (same purchase token), or undefined. */
  purchase(token: string) { return this.db.prepare('SELECT * FROM purchases WHERE token = ?').get(token) as { user_id: string; product: string; coins: number; until: number } | undefined; }
  /** Credit a coin pack once per purchase token. Returns false if that token was already used. */
  creditPack(uid: string, token: string, product: string, orderId: string | null, coins: number): boolean {
    if (this.purchase(token)) return false;
    this.db.prepare('INSERT INTO purchases (token, user_id, product, order_id, ts, coins) VALUES (?, ?, ?, ?, ?, ?)').run(token, uid, product, orderId, Date.now(), coins);
    this.addCoins(uid, coins); this.logCoins(uid, 'buy', coins);
    return true;
  }
  /** The ad-free plan: paid until `until` (a renewal keeps the same token and moves the date on). */
  setAdFree(uid: string, token: string, product: string, orderId: string | null, until: number) {
    this.db.prepare(`INSERT INTO purchases (token, user_id, product, order_id, ts, until) VALUES (?, ?, ?, ?, ?, ?)
      ON CONFLICT(token) DO UPDATE SET until = excluded.until, order_id = excluded.order_id`).run(token, uid, product, orderId, Date.now(), until);
    this.db.prepare('UPDATE users SET ad_free_until = ? WHERE id = ?').run(until, uid);
  }

  /** Rows changed since the database was opened (to skip backups when nothing happened). */
  /* ---------- push reminders ---------- */

  savePushToken(uid: string, token: string, lang: string, platform: string) {
    this.db.prepare('INSERT INTO push_tokens (token, user_id, lang, platform, ts) VALUES (?, ?, ?, ?, ?) ON CONFLICT(token) DO UPDATE SET user_id = excluded.user_id, lang = excluded.lang, ts = excluded.ts')
      .run(token, uid, lang, platform, Date.now());
  }
  dropPushToken(token: string) { this.db.prepare('DELETE FROM push_tokens WHERE token = ?').run(token); }
  /** Every phone to remind, skipping players who were in the game in the last `quietMs`. */
  pushTargets(quietMs: number): { token: string; user_id: string; lang: string; name: string; coins: number }[] {
    return this.db.prepare(`SELECT p.token, p.user_id, p.lang, u.name, u.coins FROM push_tokens p JOIN users u ON u.id = p.user_id WHERE u.seen < ?`)
      .all(Date.now() - quietMs) as { token: string; user_id: string; lang: string; name: string; coins: number }[];
  }
  pushTokensOf(uid: string): { token: string; lang: string }[] { return this.db.prepare('SELECT token, lang FROM push_tokens WHERE user_id = ?').all(uid) as { token: string; lang: string }[]; }
  metaGet(k: string): string | null { return (this.db.prepare('SELECT v FROM meta WHERE k = ?').get(k) as { v: string } | undefined)?.v ?? null; }
  metaSet(k: string, v: string) { this.db.prepare('INSERT INTO meta (k, v) VALUES (?, ?) ON CONFLICT(k) DO UPDATE SET v = excluded.v').run(k, v); }

  /* ---------- cash out (Vodafone Cash, paid by hand) ---------- */

  /** Takes the coins and files the request, in one step; null when the coins aren't there or one is already waiting. */
  requestCashout(uid: string, coins: number, egp: number, phone: string): number | null | 'pending' {
    this.db.exec('BEGIN');
    try {
      if (this.db.prepare("SELECT 1 FROM cashouts WHERE user_id = ? AND status = 'pending'").get(uid)) { this.db.exec('ROLLBACK'); return 'pending'; }
      const r = this.db.prepare('UPDATE users SET coins = coins - ? WHERE id = ? AND coins >= ?').run(coins, uid, coins);
      if (!Number(r.changes)) { this.db.exec('ROLLBACK'); return null; }
      const ins = this.db.prepare('INSERT INTO cashouts (user_id, coins, egp, phone, ts) VALUES (?, ?, ?, ?, ?)').run(uid, coins, egp, phone, Date.now());
      this.db.exec('COMMIT');
      this.logCoins(uid, 'cashout', -coins);
      return Number(ins.lastInsertRowid);
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
  }
  cashoutsOf(uid: string, limit = 20): CashoutRow[] {
    return (this.db.prepare('SELECT * FROM cashouts WHERE user_id = ? ORDER BY id DESC LIMIT ?').all(uid, limit) as CashoutDb[]).map(toCashout);
  }
  cashoutsAll(status: string | null, limit = 300): (CashoutDb & { name: string; coins_now: number })[] {
    const where = status ? 'WHERE c.status = ?' : '';
    const args: (string | number)[] = status ? [status, limit] : [limit];
    return this.db.prepare(`SELECT c.*, u.name, u.coins AS coins_now FROM cashouts c LEFT JOIN users u ON u.id = c.user_id ${where} ORDER BY c.id DESC LIMIT ?`).all(...args) as (CashoutDb & { name: string; coins_now: number })[];
  }
  /** The admin paid it, or turned it down (the coins go back). Returns the request, or null if it wasn't waiting. */
  settleCashout(id: number, paid: boolean, note: string): CashoutDb | null {
    const c = this.db.prepare("SELECT * FROM cashouts WHERE id = ? AND status = 'pending'").get(id) as CashoutDb | undefined;
    if (!c) return null;
    this.db.prepare('UPDATE cashouts SET status = ?, done_ts = ?, note = ? WHERE id = ?').run(paid ? 'paid' : 'rejected', Date.now(), note.slice(0, 200), id);
    if (!paid) { this.addCoins(c.user_id, c.coins); this.logCoins(c.user_id, 'cashout_refund', c.coins); }
    return c;
  }

  /* ---------- support messages ---------- */

  addSupport(m: { uid: string | null; name: string; contact: string; text: string; app: string }): number {
    const r = this.db.prepare('INSERT INTO support (user_id, name, contact, text, app, ts) VALUES (?, ?, ?, ?, ?, ?)').run(m.uid, m.name, m.contact, m.text, m.app, Date.now());
    return Number(r.lastInsertRowid);
  }
  supportMailed(id: number) { this.db.prepare('UPDATE support SET mailed = 1 WHERE id = ?').run(id); }
  supportAll(limit = 200) { return this.db.prepare('SELECT * FROM support ORDER BY id DESC LIMIT ?').all(limit) as { id: number; user_id: string | null; name: string; contact: string; text: string; app: string; ts: number; mailed: number }[]; }

  changes(): number { return Number((this.db.prepare('SELECT total_changes() AS n').get() as { n: number }).n); }
  /** A consistent copy of the whole database in one file (safe while the game keeps running). */
  snapshot(file: string) { fs.rmSync(file, { force: true }); this.db.exec(`VACUUM INTO '${file.replace(/'/g, "''")}'`); }

  close() { this.db.close(); }
}

/** The equipped skins, stored as JSON. */
export function parseEquip(raw: string | null | undefined): Equipped {
  try { const o = JSON.parse(raw || '{}'); return o && typeof o === 'object' ? o as Equipped : {}; } catch { return {}; }
}
export const toPublic = (u: UserRow): PublicUser => ({ id: u.id, name: u.name, ch: u.ch, email: u.email, rating: u.rating, games: u.games, wins: u.wins, coins: u.coins, gifts: u.gifts, ratings: {}, adsLeft: 0, adFreeUntil: u.ad_free_until ?? 0, skins: [], equip: parseEquip(u.equip),
  phone: u.phone ? maskPhone(u.phone) : null, google: u.google_sub ? (u.google_email || 'Google') : null, playGames: !!u.pg_id, code: u.friend_code ?? '' });

export interface CashoutDb { id: number; user_id: string; coins: number; egp: number; phone: string; status: 'pending' | 'paid' | 'rejected'; ts: number; done_ts: number | null; note: string | null }
const toCashout = (c: CashoutDb): CashoutRow => ({ id: c.id, coins: c.coins, egp: c.egp, phone: c.phone, status: c.status, ts: c.ts, doneTs: c.done_ts });
