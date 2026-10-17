// Storage on Node's built-in SQLite: one file, no separate database server to run.
import { DatabaseSync } from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import type { CoinRow, LeaderRow, PublicUser } from '../shared/protocol';
import type { GameId } from '../shared/engine';
import { ECONOMY } from '../shared/gifts';
import { maskPhone } from './login';

export interface UserRow {
  id: string; name: string; ch: number; email: string | null; pw: string | null;
  rating: number; games: number; wins: number; created: number; seen: number;
  coins: number; gifts: number; bonus_day: string | null;
  phone: string | null; google_sub: string | null; google_email: string | null;
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
  }

  /** Gives the daily coin bonus once per (UTC) day. Returns the coins added, or 0. */
  claimDaily(id: string): number {
    const day = new Date().toISOString().slice(0, 10);
    const r = this.db.prepare('UPDATE users SET coins = coins + ?, bonus_day = ? WHERE id = ? AND (bonus_day IS NULL OR bonus_day <> ?)')
      .run(ECONOMY.dailyBonus, day, id, day);
    if (!Number(r.changes)) return 0;
    this.logCoins(id, 'daily', ECONOMY.dailyBonus);
    return ECONOMY.dailyBonus;
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
  recordResult(id: string, delta: number | null, won: boolean, game: GameId = 'domino') {
    this.db.prepare(`INSERT INTO ratings (user_id, game, rating, games, wins) VALUES (?, ?, MAX(100, 1000 + ?), 1, ?)
      ON CONFLICT(user_id, game) DO UPDATE SET rating = MAX(100, rating + excluded.rating - 1000), games = games + 1, wins = wins + excluded.wins`)
      .run(id, game, delta ?? 0, won ? 1 : 0);
    this.db.prepare('UPDATE users SET games = games + 1, wins = wins + ? WHERE id = ?').run(won ? 1 : 0, id);
    if (game === 'domino') this.db.prepare('UPDATE users SET rating = MAX(100, rating + ?) WHERE id = ?').run(delta ?? 0, id);
  }

  saveMatch(m: { id: string; mode: string; private: boolean; players: unknown; scores: number[]; team: number }) {
    this.db.prepare('INSERT OR REPLACE INTO matches (id, mode, private, ended, players, scores, winner_team) VALUES (?, ?, ?, ?, ?, ?, ?)')
      .run(m.id, m.mode, m.private ? 1 : 0, Date.now(), JSON.stringify(m.players), JSON.stringify(m.scores), m.team);
  }

  leaderboard(meId: string | null, game: GameId = 'domino', limit = 50): { rows: LeaderRow[]; me: LeaderRow | null } {
    const rows = this.db.prepare(`SELECT u.id, u.name, u.ch, r.rating, r.games, r.wins FROM ratings r JOIN users u ON u.id = r.user_id
      WHERE r.game = ? AND r.games >= 3 ORDER BY r.rating DESC, r.wins DESC LIMIT ?`)
      .all(game, limit) as unknown as Pick<UserRow, 'id' | 'name' | 'ch' | 'rating' | 'games' | 'wins'>[];
    const out = rows.map((r, i) => ({ rank: i + 1, name: r.name, ch: r.ch, rating: r.rating, games: r.games, wins: r.wins, me: r.id === meId }));
    let me: LeaderRow | null = null;
    const u = meId ? this.get(meId) : undefined;
    const mine = meId ? this.db.prepare('SELECT rating, games, wins FROM ratings WHERE user_id = ? AND game = ?').get(meId, game) as { rating: number; games: number; wins: number } | undefined : undefined;
    if (u && mine && mine.games >= 3) {
      const above = this.db.prepare('SELECT COUNT(*) AS c FROM ratings WHERE game = ? AND games >= 3 AND rating > ?').get(game, mine.rating) as { c: number };
      me = { rank: Number(above.c) + 1, name: u.name, ch: u.ch, rating: mine.rating, games: mine.games, wins: mine.wins, me: true };
    }
    return { rows: out, me };
  }

  publicUser(u: UserRow): PublicUser { return { ...toPublic(u), ratings: this.ratingsOf(u.id), adsLeft: Math.max(0, ECONOMY.adsPerDay - this.adsToday(u.id)) }; }

  /** Rows changed since the database was opened (to skip backups when nothing happened). */
  changes(): number { return Number((this.db.prepare('SELECT total_changes() AS n').get() as { n: number }).n); }
  /** A consistent copy of the whole database in one file (safe while the game keeps running). */
  snapshot(file: string) { fs.rmSync(file, { force: true }); this.db.exec(`VACUUM INTO '${file.replace(/'/g, "''")}'`); }

  close() { this.db.close(); }
}

export const toPublic = (u: UserRow): PublicUser => ({ id: u.id, name: u.name, ch: u.ch, email: u.email, rating: u.rating, games: u.games, wins: u.wins, coins: u.coins, gifts: u.gifts, ratings: {}, adsLeft: 0,
  phone: u.phone ? maskPhone(u.phone) : null, google: u.google_sub ? (u.google_email || 'Google') : null });
