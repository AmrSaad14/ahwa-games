// Storage on Node's built-in SQLite: one file, no separate database server to run.
import { DatabaseSync } from 'node:sqlite';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import type { PrizeRow, CoinRow, LeaderRow, PublicUser } from '../shared/protocol';
import type { GameId } from '../shared/engine';
import { ECONOMY, WEEKLY_PRIZES, cairoDay, levelOf, nextCairoDay, streakBonus, weekKey } from '../shared/gifts';
import { Equipped, isFree, skinById } from '../shared/skins';
import { activeEvent } from '../shared/events';
import { CHEST, MISSION_SP, counts, missionById, missionsFor, type MissionEvent } from '../shared/missions';
import { SEASON_PASS, SEASON_TIERS, SP, TIERS, seasonEndsAt, seasonOf, tierOf } from '../shared/season';
import type { GameId as Game } from '../shared/engine';
import { maskPhone } from './login';
import { publicId } from '../shared/table';

export interface UserRow {
  id: string; name: string; ch: number; email: string | null; pw: string | null;
  rating: number; games: number; wins: number; created: number; seen: number;
  coins: number; gifts: number; bonus_day: string | null; ad_free_until: number; equip: string | null;
  phone: string | null; google_sub: string | null; google_email: string | null; apple_sub?: string | null; apple_email?: string | null;
  pg_id: string | null; friend_code: string | null; streak?: number; xp?: number;
  ref_by?: string | null; ref_paid?: number; muted_until?: number; pid?: string | null; banned?: number; ban_reason?: string | null; title?: string | null;
  tutorial?: number; nudged?: number;
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
    if (!cols.has('ref_by')) this.db.exec('ALTER TABLE users ADD COLUMN ref_by TEXT; ALTER TABLE users ADD COLUMN ref_paid INTEGER NOT NULL DEFAULT 0');
    if (!cols.has('apple_sub')) this.db.exec('ALTER TABLE users ADD COLUMN apple_sub TEXT; ALTER TABLE users ADD COLUMN apple_email TEXT');
    this.db.exec('CREATE UNIQUE INDEX IF NOT EXISTS users_apple ON users(apple_sub) WHERE apple_sub IS NOT NULL');
    if (!cols.has('muted_until')) this.db.exec('ALTER TABLE users ADD COLUMN muted_until INTEGER NOT NULL DEFAULT 0');
    // the public id other players see (player cards, friends): filled in for everyone once
    if (!cols.has('pid')) {
      this.db.exec('ALTER TABLE users ADD COLUMN pid TEXT');
      const all = this.db.prepare('SELECT id FROM users').all() as { id: string }[];
      const set = this.db.prepare('UPDATE users SET pid = ? WHERE id = ?');
      for (const u of all) set.run(publicId(u.id), u.id);
    }
    this.db.exec('CREATE UNIQUE INDEX IF NOT EXISTS users_pid ON users(pid) WHERE pid IS NOT NULL');
    // the dashboard: banned players, and a log of what the team did
    if (!cols.has('banned')) this.db.exec('ALTER TABLE users ADD COLUMN banned INTEGER NOT NULL DEFAULT 0; ALTER TABLE users ADD COLUMN ban_reason TEXT');
    // titles: the ones a player has (some until a date), the one they wear, and each game's monthly standings
    if (!cols.has('title')) this.db.exec('ALTER TABLE users ADD COLUMN title TEXT');
    // the first-game guide done (its reward paid once), and when a player who stayed away was last nudged
    if (!cols.has('tutorial')) this.db.exec('ALTER TABLE users ADD COLUMN tutorial INTEGER NOT NULL DEFAULT 0; ALTER TABLE users ADD COLUMN nudged INTEGER NOT NULL DEFAULT 0');
    // friends' café groups ("قهوة الشلة")
    this.db.exec(`CREATE TABLE IF NOT EXISTS groups (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, code TEXT NOT NULL UNIQUE, owner TEXT NOT NULL, created INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS group_members (group_id INTEGER NOT NULL, user_id TEXT NOT NULL, joined INTEGER NOT NULL, PRIMARY KEY (group_id, user_id));
      CREATE INDEX IF NOT EXISTS group_members_user ON group_members(user_id)`);
    this.db.exec('CREATE TABLE IF NOT EXISTS event_games (event TEXT NOT NULL, user_id TEXT NOT NULL, games INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (event, user_id))');
    this.db.exec('CREATE TABLE IF NOT EXISTS chest (week TEXT NOT NULL, user_id TEXT NOT NULL, done INTEGER NOT NULL DEFAULT 0, opened TEXT NOT NULL DEFAULT \'[]\', PRIMARY KEY (week, user_id))');
    this.db.exec(`CREATE TABLE IF NOT EXISTS user_titles (user_id TEXT NOT NULL, title TEXT NOT NULL, ts INTEGER NOT NULL, until INTEGER NOT NULL DEFAULT 0,
        note TEXT, PRIMARY KEY (user_id, title));
      CREATE INDEX IF NOT EXISTS user_titles_title ON user_titles(title);
      CREATE TABLE IF NOT EXISTS monthly (month TEXT NOT NULL, game TEXT NOT NULL, user_id TEXT NOT NULL, points INTEGER NOT NULL DEFAULT 0,
        games INTEGER NOT NULL DEFAULT 0, wins INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (month, game, user_id));
      CREATE INDEX IF NOT EXISTS monthly_rank ON monthly(month, game, points DESC)`);
    this.db.exec(`CREATE TABLE IF NOT EXISTS admin_log (id INTEGER PRIMARY KEY AUTOINCREMENT, ts INTEGER NOT NULL, who TEXT NOT NULL, action TEXT NOT NULL, target TEXT, detail TEXT);
      CREATE INDEX IF NOT EXISTS users_seen ON users(seen); CREATE INDEX IF NOT EXISTS users_created ON users(created);
      CREATE INDEX IF NOT EXISTS coin_log_kind ON coin_log(kind, ts)`);
    // daily missions (progress per player per Cairo day), the season pass, the weekly tournament's sign-ups
    this.db.exec(`CREATE TABLE IF NOT EXISTS missions (user_id TEXT NOT NULL, day TEXT NOT NULL, id TEXT NOT NULL, have INTEGER NOT NULL DEFAULT 0,
        claimed INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (user_id, day, id));
      CREATE TABLE IF NOT EXISTS season (user_id TEXT NOT NULL, season TEXT NOT NULL, sp INTEGER NOT NULL DEFAULT 0, premium INTEGER NOT NULL DEFAULT 0,
        free TEXT NOT NULL DEFAULT '[]', prem TEXT NOT NULL DEFAULT '[]', PRIMARY KEY (user_id, season));
      CREATE TABLE IF NOT EXISTS tourney_reg (event TEXT NOT NULL, user_id TEXT NOT NULL, ts INTEGER NOT NULL, PRIMARY KEY (event, user_id));
      CREATE INDEX IF NOT EXISTS reports_about ON reports(about_id, ts)`);
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
    // phones for push reminders (Firebase Cloud Messaging); monthly prizes paid by hand (cashouts: the old coin cash-out, no longer used)
    this.db.exec(`CREATE TABLE IF NOT EXISTS push_tokens (token TEXT PRIMARY KEY, user_id TEXT NOT NULL, lang TEXT NOT NULL DEFAULT 'ar', platform TEXT NOT NULL DEFAULT 'android', ts INTEGER NOT NULL);
      CREATE INDEX IF NOT EXISTS push_user ON push_tokens(user_id);
      CREATE TABLE IF NOT EXISTS cashouts (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL, coins INTEGER NOT NULL, egp INTEGER NOT NULL,
        phone TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'pending', ts INTEGER NOT NULL, done_ts INTEGER, note TEXT);
      CREATE INDEX IF NOT EXISTS cashouts_user ON cashouts(user_id, id DESC);
      CREATE INDEX IF NOT EXISTS cashouts_status ON cashouts(status, id);
      CREATE TABLE IF NOT EXISTS prizes (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL, month TEXT NOT NULL, game TEXT NOT NULL,
        egp INTEGER NOT NULL, status TEXT NOT NULL DEFAULT 'unclaimed', phone TEXT, ts INTEGER NOT NULL, claim_ts INTEGER, done_ts INTEGER, note TEXT,
        UNIQUE (month, game));
      CREATE INDEX IF NOT EXISTS prizes_user ON prizes(user_id, id DESC);
      CREATE INDEX IF NOT EXISTS prizes_status ON prizes(status, id);
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
    this.db.prepare('INSERT INTO users (id, name, ch, coins, created, seen, pid) VALUES (?, ?, ?, ?, ?, ?, ?)').run(id, name, ch, ECONOMY.startCoins, now, now, publicId(id));
    this.logCoins(id, 'welcome', ECONOMY.startCoins);
    return this.get(id)!;
  }

  get(id: string): UserRow | undefined {
    return this.db.prepare('SELECT * FROM users WHERE id = ?').get(id) as UserRow | undefined;
  }

  byEmail(email: string): UserRow | undefined {
    return this.db.prepare('SELECT * FROM users WHERE email = ?').get(email) as UserRow | undefined;
  }

  /** A player by the public id on their card. */
  byPid(pid: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE pid = ?').get(String(pid)) as UserRow | undefined; }

  /** What a player's card shows: matches, wins and losses (overall and per game), coins won and lost at the
   *  tables, the biggest win, the tournament's results and the level. */
  playerStats(id: string) {
    const u = this.get(id); if (!u) return null;
    const ratings = this.ratingsOf(id);
    const money = this.db.prepare(`SELECT
        COALESCE(SUM(CASE WHEN kind = 'win' THEN delta ELSE 0 END), 0) AS won,
        COALESCE(SUM(CASE WHEN kind IN ('loss', 'left') THEN -delta ELSE 0 END), 0) AS lost,
        COALESCE(MAX(CASE WHEN kind = 'win' THEN delta END), 0) AS best,
        COALESCE(SUM(CASE WHEN kind = 'tourney' THEN delta ELSE 0 END), 0) AS tourney,
        COALESCE(SUM(CASE WHEN kind = 'weekly' THEN delta ELSE 0 END), 0) AS weekly
      FROM coin_log WHERE user_id = ?`).get(id) as { won: number; lost: number; best: number; tourney: number; weekly: number };
    const games = u.games ?? 0, wins = u.wins ?? 0;
    const perGame: Record<string, { rating: number; games: number; wins: number; losses: number }> = {};
    for (const [g, r] of Object.entries(ratings)) if (r) perGame[g] = { rating: r.rating, games: r.games, wins: r.wins, losses: r.games - r.wins };
    return {
      games, wins, losses: Math.max(0, games - wins), winRate: games ? Math.round(wins / games * 100) : 0,
      coinsWon: money.won, coinsLost: money.lost, net: money.won - money.lost, bestWin: money.best,
      tourneyCoins: money.tourney, weeklyCoins: money.weekly, perGame,
      level: levelOf(u.xp ?? 0), xp: u.xp ?? 0, streak: u.streak ?? 0, gifts: u.gifts ?? 0, since: u.created,
    };
  }

  byPhone(phone: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE phone = ?').get(phone) as UserRow | undefined; }
  byGoogle(sub: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE google_sub = ?').get(sub) as UserRow | undefined; }
  setPhone(id: string, phone: string) { this.db.prepare('UPDATE users SET phone = ? WHERE id = ?').run(phone, id); }
  byApple(sub: string): UserRow | undefined { return this.db.prepare('SELECT * FROM users WHERE apple_sub = ?').get(sub) as UserRow | undefined; }
  setApple(id: string, sub: string, email: string | null) { this.db.prepare('UPDATE users SET apple_sub = ?, apple_email = ? WHERE id = ?').run(sub, email, id); }
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
    this.db.prepare('DELETE FROM missions WHERE user_id = ?').run(id);
    this.db.prepare('DELETE FROM season WHERE user_id = ?').run(id);
    this.db.prepare('DELETE FROM tourney_reg WHERE user_id = ?').run(id);
    this.db.prepare('DELETE FROM user_skins WHERE user_id = ?').run(id);
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
    this.addSeasonSp(id, SP.daily, now);
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
    const rows = this.db.prepare(`SELECT u.id, u.pid, u.name, u.ch, u.xp, w.points, w.games, w.wins FROM weekly w JOIN users u ON u.id = w.user_id
      WHERE w.week = ? ORDER BY w.points DESC, w.wins DESC, w.games ASC LIMIT ?`).all(week, limit) as { id: string; pid: string; name: string; ch: number; xp: number; points: number; games: number; wins: number }[];
    const out = rows.map((r, i) => ({ rank: i + 1, name: r.name, ch: r.ch, rating: r.points, games: r.games, wins: r.wins, me: r.id === meId, level: levelOf(r.xp), pid: r.pid, title: this.wornTitle(r.id) }));
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

  /** Different players who reported this one since `since` (three in a day mute their chat for a day). */
  reporters(about: string, since: number): number {
    return Number((this.db.prepare('SELECT COUNT(DISTINCT from_id) AS n FROM reports WHERE about_id = ? AND ts >= ?').get(about, since) as { n: number }).n);
  }
  mute(id: string, until: number) { this.db.prepare('UPDATE users SET muted_until = MAX(muted_until, ?) WHERE id = ?').run(until, id); }
  mutedUntil(id: string): number { return (this.db.prepare('SELECT muted_until FROM users WHERE id = ?').get(id) as { muted_until: number } | undefined)?.muted_until ?? 0; }

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
    const rows = this.db.prepare(`SELECT u.id, u.pid, u.xp, u.name, u.ch, r.rating, r.games, r.wins FROM ratings r JOIN users u ON u.id = r.user_id
      WHERE r.game = ? AND r.games >= 3 ORDER BY r.rating DESC, r.wins DESC LIMIT ?`)
      .all(game, limit) as unknown as (Pick<UserRow, 'id' | 'name' | 'ch' | 'rating' | 'games' | 'wins' | 'xp'> & { pid: string })[];
    const out = rows.map((r, i) => ({ rank: i + 1, name: r.name, ch: r.ch, rating: r.rating, games: r.games, wins: r.wins, me: r.id === meId, level: levelOf(r.xp ?? 0), pid: r.pid, title: this.wornTitle(r.id) }));
    let me: LeaderRow | null = null;
    const u = meId ? this.get(meId) : undefined;
    const mine = meId ? this.db.prepare('SELECT rating, games, wins FROM ratings WHERE user_id = ? AND game = ?').get(meId, game) as { rating: number; games: number; wins: number } | undefined : undefined;
    if (u && mine && mine.games >= 3) {
      const above = this.db.prepare('SELECT COUNT(*) AS c FROM ratings WHERE game = ? AND games >= 3 AND rating > ?').get(game, mine.rating) as { c: number };
      me = { rank: Number(above.c) + 1, name: u.name, ch: u.ch, rating: mine.rating, games: mine.games, wins: mine.wins, me: true };
    }
    return { rows: out, me };
  }

  publicUser(u: UserRow): PublicUser {
    const now = Date.now(), season = this.seasonState(u.id);
    // no full-screen ads: with the season pass, until the season ends
    const adFreeUntil = Math.max(u.ad_free_until ?? 0, season.premium ? season.endsAt : 0);
    return { ...toPublic(u), adFreeUntil, titles: this.titlesOf(u.id, now), title: this.wornTitle(u.id, now), chest: this.chestState(u.id, now), tutorial: !!u.tutorial, xp: u.xp ?? 0, level: levelOf(u.xp ?? 0), streak: u.streak ?? 0, daily: this.dailyState(u), code: this.friendCode(u.id), ratings: this.ratingsOf(u.id),
      adsLeft: Math.max(0, ECONOMY.adsPerDay - this.adsToday(u.id)), skins: this.skinsOf(u.id),
      missions: this.missions(u.id), missionsAt: nextCairoDay(now), season,
      canRefer: !u.ref_by && now - u.created < REFER_DAYS * 86_400_000, referred: !!u.ref_by, muted: (u.muted_until ?? 0) > now ? u.muted_until : 0 };
  }

  /* ---------- daily missions ---------- */

  /** Today's three missions with how far along they are. */
  missions(uid: string, now = Date.now()) {
    const day = cairoDay(now);
    const rows = this.db.prepare('SELECT id, have, claimed FROM missions WHERE user_id = ? AND day = ?').all(uid, day) as { id: string; have: number; claimed: number }[];
    return missionsFor(uid, day).map(m => {
      const r = rows.find(x => x.id === m.id);
      return { id: m.id, game: m.game, kind: m.kind, need: m.need, have: Math.min(m.need, r?.have ?? 0), coins: m.coins, xp: m.xp, online: !!m.online, claimed: !!r?.claimed };
    });
  }
  /** A finished game moves today's missions on. Returns the missions it just completed. */
  missionProgress(uid: string, ev: MissionEvent, now = Date.now()): string[] {
    const day = cairoDay(now), done: string[] = [];
    for (const m of missionsFor(uid, day)) {
      if (!counts(m, ev)) continue;
      const r = this.db.prepare('SELECT have FROM missions WHERE user_id = ? AND day = ? AND id = ?').get(uid, day, m.id) as { have: number } | undefined;
      const before = r?.have ?? 0;
      if (before >= m.need) continue;
      this.db.prepare('INSERT INTO missions (user_id, day, id, have) VALUES (?, ?, ?, 1) ON CONFLICT(user_id, day, id) DO UPDATE SET have = have + 1').run(uid, day, m.id);
      if (before + 1 >= m.need) done.push(m.id);
    }
    return done;
  }
  /** Collect a finished mission's reward (once). */
  missionClaim(uid: string, id: string, now = Date.now()): { coins: number; xp: number } | string {
    const day = cairoDay(now);
    const m = missionsFor(uid, day).find(x => x.id === id);
    if (!m) return 'bad_mission';
    const r = this.db.prepare('SELECT have, claimed FROM missions WHERE user_id = ? AND day = ? AND id = ?').get(uid, day, id) as { have: number; claimed: number } | undefined;
    if (!r || r.have < m.need) return 'mission_not_done';
    if (r.claimed) return 'already_claimed';
    const ch = this.db.prepare('UPDATE missions SET claimed = 1 WHERE user_id = ? AND day = ? AND id = ? AND claimed = 0').run(uid, day, id);
    if (!Number(ch.changes)) return 'already_claimed';
    const ev = activeEvent(now), coins = ev && ev.missionMult > 1 ? Math.round(m.coins * ev.missionMult) : m.coins;   // an event: more coins
    this.addCoins(uid, coins); this.logCoins(uid, 'mission', coins);
    this.addSeasonSp(uid, MISSION_SP, now);
    // one more for the weekly chest
    this.db.prepare('INSERT INTO chest (week, user_id, done) VALUES (?, ?, 1) ON CONFLICT(week, user_id) DO UPDATE SET done = done + 1').run(weekKey(now), uid);
    return { coins, xp: m.xp };
  }

  /** The weekly chest: how many missions were collected this week, and which steps were opened. */
  chestState(uid: string, now = Date.now()) {
    const week = weekKey(now);
    const r = this.db.prepare('SELECT done, opened FROM chest WHERE week = ? AND user_id = ?').get(week, uid) as { done: number; opened: string } | undefined;
    let opened: number[] = []; try { opened = JSON.parse(r?.opened || '[]'); } catch { /* none */ }
    return { week, done: r?.done ?? 0, opened, steps: CHEST.steps, coins: CHEST.coins };
  }
  /** Opens one step of the chest (0, 1, 2). */
  chestOpen(uid: string, step: number, now = Date.now()): { coins: number; xp: number } | string {
    const st = this.chestState(uid, now);
    if (!Number.isInteger(step) || step < 0 || step >= CHEST.steps.length) return 'bad_step';
    if (st.done < CHEST.steps[step]) return 'chest_locked';
    if (st.opened.includes(step)) return 'already_claimed';
    this.db.prepare('UPDATE chest SET opened = ? WHERE week = ? AND user_id = ?').run(JSON.stringify([...st.opened, step]), st.week, uid);
    this.addCoins(uid, CHEST.coins[step]); this.logCoins(uid, 'chest', CHEST.coins[step]);
    return { coins: CHEST.coins[step], xp: CHEST.xp[step] };
  }

  /* ---------- the season pass ---------- */

  private seasonRow(uid: string, season: string) {
    return this.db.prepare('SELECT sp, premium, free, prem FROM season WHERE user_id = ? AND season = ?').get(uid, season) as { sp: number; premium: number; free: string; prem: string } | undefined;
  }
  seasonState(uid: string, now = Date.now()) {
    const id = seasonOf(now), r = this.seasonRow(uid, id);
    const list = (j: string | undefined) => { try { return (JSON.parse(j || '[]') as number[]).filter(n => Number.isInteger(n)); } catch { return []; } };
    const sp = r?.sp ?? 0;
    return { id, endsAt: seasonEndsAt(now), sp, tier: tierOf(sp), premium: !!r?.premium, free: list(r?.free), prem: list(r?.prem), price: SEASON_PASS.coins, egp: SEASON_PASS.egp, tiers: TIERS };
  }
  /** Season points; returns the tier before and after. */
  addSeasonSp(uid: string, n: number, now = Date.now()): { from: number; to: number } {
    const id = seasonOf(now), before = this.seasonRow(uid, id)?.sp ?? 0;
    const ev = activeEvent(now); if (ev && ev.spMult > 1) n = Math.round(n * ev.spMult);   // an event: more season points
    if (!SEASON_PASS.enabled) return { from: tierOf(before), to: tierOf(before) };   // the season is switched off
    this.db.prepare('INSERT INTO season (user_id, season, sp) VALUES (?, ?, ?) ON CONFLICT(user_id, season) DO UPDATE SET sp = sp + excluded.sp').run(uid, id, n);
    return { from: tierOf(before), to: tierOf(before + n) };
  }
  /** Open the premium track for this season (paid with coins, or a store purchase when `paid`). */
  seasonPremium(uid: string, paid: boolean, now = Date.now()): string | null {
    const id = seasonOf(now);
    if (!paid && !SEASON_PASS.enabled) return 'season_off';
    if (this.seasonRow(uid, id)?.premium) return 'already_premium';
    this.db.exec('BEGIN');
    try {
      if (!paid) {
        const r = this.db.prepare('UPDATE users SET coins = coins - ? WHERE id = ? AND coins >= ?').run(SEASON_PASS.coins, uid, SEASON_PASS.coins);
        if (!Number(r.changes)) { this.db.exec('ROLLBACK'); return 'not_enough_coins'; }
      }
      this.db.prepare('INSERT INTO season (user_id, season, premium) VALUES (?, ?, 1) ON CONFLICT(user_id, season) DO UPDATE SET premium = 1').run(uid, id);
      this.db.exec('COMMIT');
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
    if (!paid) this.logCoins(uid, 'season', -SEASON_PASS.coins);
    return null;
  }
  /** Collect a tier's reward on one track. */
  seasonClaim(uid: string, tier: number, track: 'free' | 'prem', now = Date.now()): { coins?: number; skin?: string } | string {
    if (!SEASON_PASS.enabled) return 'season_off';
    const st = this.seasonState(uid, now);
    if (!Number.isInteger(tier) || tier < 1 || tier > SEASON_TIERS) return 'bad_tier';
    if (tier > st.tier) return 'tier_locked';
    if (track === 'prem' && !st.premium) return 'not_premium';
    const got = track === 'free' ? st.free : st.prem;
    if (got.includes(tier)) return 'already_claimed';
    const reward = TIERS[tier - 1][track];
    this.db.exec('BEGIN');
    try {
      this.db.prepare(`INSERT INTO season (user_id, season, ${track}) VALUES (?, ?, ?) ON CONFLICT(user_id, season) DO UPDATE SET ${track} = excluded.${track}`)
        .run(uid, st.id, JSON.stringify([...got, tier]));
      if ('coins' in reward) this.addCoins(uid, reward.coins);
      else this.db.prepare('INSERT OR IGNORE INTO user_skins (user_id, skin, ts) VALUES (?, ?, ?)').run(uid, reward.skin, now);
      this.db.exec('COMMIT');
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
    if ('coins' in reward) { this.logCoins(uid, 'season', reward.coins); return { coins: reward.coins }; }
    return { skin: reward.skin };
  }

  /* ---------- referrals: both get 50K when the invited friend finishes a first game ---------- */

  /** A new player says who invited them (their friend code). */
  setReferrer(uid: string, code: string): string | null {
    const u = this.get(uid); if (!u) return 'bad_code';
    if (u.ref_by) return 'already_referred';
    if (Date.now() - u.created > REFER_DAYS * 86_400_000) return 'too_late';
    const by = this.byFriendCode(code.trim().replace(/[^a-z0-9]/gi, ''));
    if (!by) return 'bad_code';
    if (by.id === uid) return 'friend_self';
    if (by.ref_by === uid) return 'bad_code';   // two accounts can't refer each other
    this.db.prepare('UPDATE users SET ref_by = ? WHERE id = ? AND ref_by IS NULL').run(by.id, uid);
    return null;
  }
  /** After the invited player's first finished game: both get the bonus (once; at most REFER_MAX per inviter). */
  referralFirstGame(uid: string): { referrer: string; coins: number; name: string } | null {
    const u = this.get(uid);
    if (!u?.ref_by || u.ref_paid) return null;
    const r = this.db.prepare('UPDATE users SET ref_paid = 1 WHERE id = ? AND ref_paid = 0').run(uid);
    if (!Number(r.changes)) return null;
    const paidBefore = Number((this.db.prepare('SELECT COUNT(*) AS n FROM users WHERE ref_by = ? AND ref_paid = 1').get(u.ref_by) as { n: number }).n) - 1;
    this.addCoins(uid, REFER_BONUS); this.logCoins(uid, 'referral', REFER_BONUS);
    if (paidBefore < REFER_MAX && this.get(u.ref_by)) { this.addCoins(u.ref_by, REFER_BONUS); this.logCoins(u.ref_by, 'referral', REFER_BONUS); }
    return { referrer: u.ref_by, coins: REFER_BONUS, name: u.name };
  }

  /* ---------- the weekly tournament's sign-ups ---------- */

  tourneyJoin(event: string, uid: string) { this.db.prepare('INSERT OR IGNORE INTO tourney_reg (event, user_id, ts) VALUES (?, ?, ?)').run(event, uid, Date.now()); }
  tourneyLeave(event: string, uid: string) { this.db.prepare('DELETE FROM tourney_reg WHERE event = ? AND user_id = ?').run(event, uid); }
  tourneyRegs(event: string): string[] { return (this.db.prepare('SELECT user_id FROM tourney_reg WHERE event = ? ORDER BY ts').all(event) as { user_id: string }[]).map(r => r.user_id); }
  tourneyIn(event: string, uid: string): boolean { return !!this.db.prepare('SELECT 1 FROM tourney_reg WHERE event = ? AND user_id = ?').get(event, uid); }

  /** Skins this player bought in the shop. */
  skinsOf(id: string): string[] { return (this.db.prepare('SELECT skin FROM user_skins WHERE user_id = ? ORDER BY ts').all(id) as { skin: string }[]).map(r => r.skin); }
  /** Buy a skin with coins. Returns an error code, or null when bought. */
  buySkin(id: string, skin: string): string | null {
    const s = skinById(skin);
    if (!s || s.price === 0 || s.pass) return 'bad_skin';
    if (this.skinsOf(id).includes(skin)) return null;
    if (s.event && activeEvent()?.kind !== s.event) return 'not_on_sale';   // a limited skin: only during its event
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
    if (!isFree(s) && !this.skinsOf(id).includes(skin)) return 'not_owned';
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
  /** Meta rows whose key starts with `prefix`, newest key first. */
  metaLike(prefix: string) { return this.db.prepare("SELECT k, v FROM meta WHERE k LIKE ? || '%' ORDER BY k DESC").all(prefix) as { k: string; v: string }[]; }
  metaSet(k: string, v: string) { this.db.prepare('INSERT INTO meta (k, v) VALUES (?, ?) ON CONFLICT(k) DO UPDATE SET v = excluded.v').run(k, v); }

  /* ---------- monthly prizes: the best player of each game wins money on Vodafone Cash, paid by hand ---------- */

  /** The prize for a game's month (once): waiting for the winner's Vodafone Cash number. */
  createPrize(uid: string, month: string, game: string, egp: number, now = Date.now()): number | null {
    const r = this.db.prepare("INSERT OR IGNORE INTO prizes (user_id, month, game, egp, status, ts) VALUES (?, ?, ?, ?, 'unclaimed', ?)").run(uid, month, game, egp, now);
    return Number(r.changes) ? Number(r.lastInsertRowid) : null;
  }
  /** A player's prizes, newest first. */
  prizesOf(uid: string, limit = 12): PrizeRow[] {
    return (this.db.prepare('SELECT * FROM prizes WHERE user_id = ? ORDER BY id DESC LIMIT ?').all(uid, limit) as PrizeDb[]).map(toPrize);
  }
  /** The winner gives the number to pay to. Returns an error code, or null. */
  claimPrize(uid: string, id: number, phone: string, now = Date.now()): string | null {
    const p = this.db.prepare('SELECT * FROM prizes WHERE id = ?').get(id) as PrizeDb | undefined;
    if (!p || p.user_id !== uid) return 'prize_gone';
    if (p.status !== 'unclaimed') return p.status === 'pending' ? 'prize_waiting' : 'prize_gone';
    this.db.prepare("UPDATE prizes SET status = 'pending', phone = ?, claim_ts = ?, note = NULL WHERE id = ?").run(phone, now, id);
    return null;
  }
  prizesAll(status: string | null, limit = 300): (PrizeDb & { name: string; pid: string })[] {
    const where = status ? 'WHERE p.status = ?' : '';
    const args: (string | number)[] = status ? [status, limit] : [limit];
    return this.db.prepare(`SELECT p.*, u.name, u.pid FROM prizes p LEFT JOIN users u ON u.id = p.user_id ${where} ORDER BY p.id DESC LIMIT ?`).all(...args) as (PrizeDb & { name: string; pid: string })[];
  }
  /** The team paid it, turned it down for good, or asks the winner for the number again. Returns the prize, or null. */
  settlePrize(id: number, action: 'paid' | 'reject' | 'again', note: string, now = Date.now()): PrizeDb | null {
    const p = this.db.prepare('SELECT * FROM prizes WHERE id = ?').get(id) as PrizeDb | undefined;
    if (!p || (action === 'paid' && p.status !== 'pending') || (action === 'again' && p.status !== 'pending') || (action === 'reject' && !['pending', 'unclaimed'].includes(p.status))) return null;
    const status = action === 'paid' ? 'paid' : action === 'reject' ? 'rejected' : 'unclaimed';
    this.db.prepare('UPDATE prizes SET status = ?, done_ts = ?, note = ? WHERE id = ?').run(status, action === 'again' ? null : now, note.slice(0, 200) || null, id);
    return { ...p, status };
  }
  /** Prizes nobody claimed in time. Returns how many. */
  expirePrizes(days: number, now = Date.now()): number {
    return Number(this.db.prepare("UPDATE prizes SET status = 'expired', done_ts = ? WHERE status = 'unclaimed' AND ts < ?").run(now, now - days * 86_400_000).changes);
  }
  prizeGet(id: number) { return this.db.prepare('SELECT * FROM prizes WHERE id = ?').get(id) as PrizeDb | undefined; }

  /* ---------- the dashboard (/admin) ---------- */

  private one<T>(sql: string, ...a: (string | number | null)[]): T { return this.db.prepare(sql).get(...a) as T; }
  private n(sql: string, ...a: (string | number | null)[]): number { return Number((this.db.prepare(sql).get(...a) as { n: number | null }).n ?? 0); }

  /** The numbers on the dashboard's front page. */
  adminOverview(now = Date.now()) {
    const day = nextCairoDay(now) - 86_400_000, week = now - 7 * 86_400_000;
    const season = seasonOf(now);
    return {
      players: this.n('SELECT COUNT(*) AS n FROM users'),
      signedIn: this.n('SELECT COUNT(*) AS n FROM users WHERE google_sub IS NOT NULL OR apple_sub IS NOT NULL OR phone IS NOT NULL OR email IS NOT NULL'),
      newToday: this.n('SELECT COUNT(*) AS n FROM users WHERE created >= ?', day),
      activeToday: this.n('SELECT COUNT(*) AS n FROM users WHERE seen >= ?', day),
      activeWeek: this.n('SELECT COUNT(*) AS n FROM users WHERE seen >= ?', week),
      gamesToday: this.n('SELECT COUNT(*) AS n FROM matches WHERE ended >= ?', day),
      coinsHeld: this.n('SELECT SUM(coins) AS n FROM users'),
      houseFeesToday: this.n("SELECT SUM(fee) AS n FROM coin_log WHERE ts >= ?", day),
      purchasesToday: this.n('SELECT COUNT(*) AS n FROM purchases WHERE ts >= ?', day),
      purchasesWeek: this.n('SELECT COUNT(*) AS n FROM purchases WHERE ts >= ?', week),
      seasonPremium: this.n('SELECT COUNT(*) AS n FROM season WHERE season = ? AND premium = 1', season),
      prizePending: this.n("SELECT COUNT(*) AS n FROM prizes WHERE status = 'pending'"),
      prizePendingEgp: this.n("SELECT SUM(egp) AS n FROM prizes WHERE status = 'pending'"),
      prizeUnclaimed: this.n("SELECT COUNT(*) AS n FROM prizes WHERE status = 'unclaimed'"),
      prizePaidEgp: this.n("SELECT SUM(egp) AS n FROM prizes WHERE status = 'paid'"),
      reportsWeek: this.n('SELECT COUNT(*) AS n FROM reports WHERE ts >= ?', week),
      support: this.n('SELECT COUNT(*) AS n FROM support WHERE ts >= ?', week),
      banned: this.n('SELECT COUNT(*) AS n FROM users WHERE banned = 1'),
    };
  }

  /** Day by day for the last `days` Cairo days: new players, active players, games, purchases, cash-outs. */
  adminDaily(days = 14, now = Date.now()) {
    const out: { day: string; newPlayers: number; active: number; games: number; purchases: number; prizeEgp: number }[] = [];
    let end = nextCairoDay(now);
    for (let i = 0; i < days; i++) {
      const start = end - 86_400_000;
      out.unshift({
        day: cairoDay(start + 3_600_000),
        newPlayers: this.n('SELECT COUNT(*) AS n FROM users WHERE created >= ? AND created < ?', start, end),
        active: this.n("SELECT COUNT(DISTINCT user_id) AS n FROM coin_log WHERE ts >= ? AND ts < ?", start, end),
        games: this.n('SELECT COUNT(*) AS n FROM matches WHERE ended >= ? AND ended < ?', start, end),
        purchases: this.n('SELECT COUNT(*) AS n FROM purchases WHERE ts >= ? AND ts < ?', start, end),
        prizeEgp: this.n("SELECT SUM(egp) AS n FROM prizes WHERE done_ts >= ? AND done_ts < ? AND status = 'paid'", start, end),
      });
      end = start;
    }
    return out;
  }

  /** Players by id, public id, friend code, name, e-mail or phone. */
  adminSearch(q: string, limit = 50, offset = 0): UserRow[] {
    q = q.trim();
    if (!q) return this.db.prepare('SELECT * FROM users ORDER BY seen DESC LIMIT ? OFFSET ?').all(limit, offset) as unknown as UserRow[];
    const like = '%' + q.replace(/[%_]/g, '') + '%', digits = q.replace(/\D/g, '');
    return this.db.prepare(`SELECT * FROM users WHERE id = ? OR pid = ? OR friend_code = ? OR name LIKE ? OR email LIKE ? OR google_email LIKE ? OR apple_email LIKE ?
      OR (? != '' AND phone LIKE ?) ORDER BY seen DESC LIMIT ? OFFSET ?`)
      .all(q, q, q.toUpperCase(), like, like, like, like, digits, '%' + digits.slice(-9) + '%', limit, offset) as unknown as UserRow[];
  }

  /** Everything about one player, for the dashboard. */
  adminPlayer(id: string) {
    const u = this.get(id); if (!u) return null;
    return {
      user: u, pub: this.publicUser(u), stats: this.playerStats(id), skins: this.skinsOf(id), coins: this.coinHistory(id, 80),
      purchases: this.db.prepare('SELECT product, order_id, ts, coins, until FROM purchases WHERE user_id = ? ORDER BY ts DESC LIMIT 50').all(id),
      prizes: this.db.prepare('SELECT * FROM prizes WHERE user_id = ? ORDER BY id DESC LIMIT 50').all(id),
      reportsAbout: this.db.prepare('SELECT r.*, u.name AS from_name FROM reports r LEFT JOIN users u ON u.id = r.from_id WHERE about_id = ? ORDER BY ts DESC LIMIT 50').all(id),
      reportsBy: this.n('SELECT COUNT(*) AS n FROM reports WHERE from_id = ?', id),
      season: this.seasonState(id), friends: this.friendCount(id), referredBy: u.ref_by ?? null,
      titles: this.titlesOf(id), title: this.wornTitle(id),
    };
  }

  /** Coins added (or taken, when negative) by the team. */
  adminCoins(id: string, delta: number, kind = 'admin'): number | null {
    const u = this.get(id); if (!u) return null;
    const d = Math.max(-u.coins, Math.round(delta));
    this.addCoins(id, d); this.logCoins(id, kind, d);
    return u.coins + d;
  }
  adminGiveSkin(id: string, skin: string, give: boolean): boolean {
    if (!skinById(skin) || !this.get(id)) return false;
    if (give) this.db.prepare('INSERT OR IGNORE INTO user_skins (user_id, skin, ts) VALUES (?, ?, ?)').run(id, skin, Date.now());
    else this.db.prepare('DELETE FROM user_skins WHERE user_id = ? AND skin = ?').run(id, skin);
    return true;
  }
  adminBan(id: string, on: boolean, reason: string) { this.db.prepare('UPDATE users SET banned = ?, ban_reason = ? WHERE id = ?').run(on ? 1 : 0, on ? reason.slice(0, 200) : null, id); }
  adminRename(id: string, name: string) { this.db.prepare('UPDATE users SET name = ? WHERE id = ?').run(name.slice(0, 24), id); }
  adminUnmute(id: string) { this.db.prepare('UPDATE users SET muted_until = 0 WHERE id = ?').run(id); }
  adminSeasonPremium(id: string, on: boolean, now = Date.now()) {
    const sid = seasonOf(now);
    this.db.prepare('INSERT INTO season (user_id, season, premium) VALUES (?, ?, ?) ON CONFLICT(user_id, season) DO UPDATE SET premium = excluded.premium').run(id, sid, on ? 1 : 0);
  }
  adminSeasonSp(id: string, sp: number, now = Date.now()) {
    const sid = seasonOf(now);
    this.db.prepare('INSERT INTO season (user_id, season, sp) VALUES (?, ?, ?) ON CONFLICT(user_id, season) DO UPDATE SET sp = excluded.sp').run(id, sid, Math.max(0, Math.round(sp)));
  }

  /** Coins for every player (or those seen in the last `activeDays` days). Returns how many got them. */
  adminCoinsToAll(coins: number, activeDays: number, kind = 'gift_all', now = Date.now()): number {
    const since = activeDays > 0 ? now - activeDays * 86_400_000 : 0;
    this.db.exec('BEGIN');
    try {
      this.db.prepare('UPDATE users SET coins = coins + ? WHERE seen >= ? AND banned = 0').run(coins, since);
      const r = this.db.prepare('INSERT INTO coin_log (user_id, ts, kind, delta, balance) SELECT id, ?, ?, ?, coins FROM users WHERE seen >= ? AND banned = 0').run(now, kind, coins, since);
      this.db.exec('COMMIT');
      return Number(r.changes);
    } catch (e) { this.db.exec('ROLLBACK'); throw e; }
  }
  adminIdsSeenSince(since: number): string[] { return (this.db.prepare('SELECT id FROM users WHERE seen >= ? AND banned = 0').all(since) as { id: string }[]).map(r => r.id); }

  /** The season pass this month: how many bought it, and how far players have got. */
  adminSeason(now = Date.now()) {
    const sid = seasonOf(now);
    const rows = this.db.prepare('SELECT sp, premium FROM season WHERE season = ?').all(sid) as { sp: number; premium: number }[];
    const tiers = Array.from({ length: SEASON_TIERS + 1 }, () => 0);
    for (const r of rows) tiers[tierOf(r.sp)]++;
    return { season: sid, endsAt: seasonEndsAt(now), players: rows.length, premium: rows.filter(r => r.premium).length,
      paidPlay: this.n("SELECT COUNT(*) AS n FROM purchases WHERE product = 'season_pass' AND ts >= ?", now - 40 * 86_400_000), tiers };
  }

  purchasesAll(limit = 200) {
    return this.db.prepare('SELECT p.*, u.name FROM purchases p LEFT JOIN users u ON u.id = p.user_id ORDER BY p.ts DESC LIMIT ?').all(limit);
  }
  reportsFull(limit = 200) {
    return this.db.prepare(`SELECT r.*, a.name AS from_name, b.name AS about_name, b.muted_until, b.banned FROM reports r
      LEFT JOIN users a ON a.id = r.from_id LEFT JOIN users b ON b.id = r.about_id ORDER BY r.ts DESC LIMIT ?`).all(limit);
  }
  tourneyHistory(limit = 20) {
    return (this.db.prepare("SELECT k, v FROM meta WHERE k LIKE 'tourney_results_%' ORDER BY k DESC LIMIT ?").all(limit) as { k: string; v: string }[])
      .map(r => { try { return { event: r.k.slice('tourney_results_'.length), ...JSON.parse(r.v) }; } catch { return null; } }).filter(Boolean);
  }
  adminLog(who: string, action: string, target: string | null, detail: unknown) {
    this.db.prepare('INSERT INTO admin_log (ts, who, action, target, detail) VALUES (?, ?, ?, ?, ?)').run(Date.now(), who, action, target, detail === undefined ? null : JSON.stringify(detail).slice(0, 2000));
  }
  adminLogAll(limit = 300) { return this.db.prepare('SELECT * FROM admin_log ORDER BY id DESC LIMIT ?').all(limit); }

  /* ---------- the first-game guide, coming back ---------- */

  /** The first-game guide was finished: its reward, once. Returns the coins (0 if already paid). */
  tutorialDone(uid: string, coins: number): number {
    const r = this.db.prepare('UPDATE users SET tutorial = 1 WHERE id = ? AND tutorial = 0').run(uid);
    if (!Number(r.changes) || coins <= 0) return 0;
    this.addCoins(uid, coins); this.logCoins(uid, 'tutorial', coins);
    return coins;
  }
  /** A gift for a player back after `days` days or more away (called before `touch`). Returns the coins, or 0. */
  comebackGift(u: UserRow, days: number, coins: number, now = Date.now()): number {
    if (days <= 0 || coins <= 0 || !u.seen || now - u.seen < days * 86_400_000 || now - u.created < days * 86_400_000) return 0;
    this.addCoins(u.id, coins); this.logCoins(u.id, 'comeback', coins);
    return coins;
  }
  /** Players away for `days` days (not more than a day longer), with a phone to remind, not reminded since. */
  awayToNudge(days: number, now = Date.now(), limit = 500): string[] {
    const to = now - days * 86_400_000, from = to - 86_400_000;
    const ids = (this.db.prepare(`SELECT DISTINCT u.id FROM users u JOIN push_tokens p ON p.user_id = u.id
      WHERE u.seen >= ? AND u.seen < ? AND u.nudged < u.seen AND u.banned = 0 LIMIT ?`).all(from, to, limit) as { id: string }[]).map(r => r.id);
    const mark = this.db.prepare('UPDATE users SET nudged = ? WHERE id = ?');
    for (const id of ids) mark.run(now, id);
    return ids;
  }

  /* ---------- events ---------- */

  /** One more online game during an event; returns the count. */
  eventGame(event: string, uid: string): number {
    this.db.prepare('INSERT INTO event_games (event, user_id, games) VALUES (?, ?, 1) ON CONFLICT(event, user_id) DO UPDATE SET games = games + 1').run(event, uid);
    return this.eventGames(event, uid);
  }
  eventGames(event: string, uid: string): number { return (this.db.prepare('SELECT games FROM event_games WHERE event = ? AND user_id = ?').get(event, uid) as { games: number } | undefined)?.games ?? 0; }
  eventStats(event: string) {
    return this.db.prepare('SELECT COUNT(*) AS players, COALESCE(SUM(games), 0) AS games FROM event_games WHERE event = ?').get(event) as { players: number; games: number };
  }

  /* ---------- friends' café groups ---------- */

  static readonly GROUP_MAX = 30;      // members in a group
  static readonly GROUPS_EACH = 5;     // groups a player can be in

  groupsOf(uid: string) {
    return this.db.prepare(`SELECT g.id, g.name, g.code, g.owner, (SELECT COUNT(*) FROM group_members x WHERE x.group_id = g.id) AS members
      FROM groups g JOIN group_members m ON m.group_id = g.id WHERE m.user_id = ? ORDER BY m.joined`).all(uid) as { id: number; name: string; code: string; owner: string; members: number }[];
  }
  /** Are two players in a group together? */
  shareGroup(a: string, b: string): boolean { return !!this.db.prepare('SELECT 1 FROM group_members x JOIN group_members y ON y.group_id = x.group_id WHERE x.user_id = ? AND y.user_id = ?').get(a, b); }
  groupGet(id: number) { return this.db.prepare('SELECT * FROM groups WHERE id = ?').get(id) as { id: number; name: string; code: string; owner: string; created: number } | undefined; }
  groupMembers(id: number): string[] { return (this.db.prepare('SELECT user_id FROM group_members WHERE group_id = ? ORDER BY joined').all(id) as { user_id: string }[]).map(r => r.user_id); }
  /** A new group (you're in it). Returns its id, or an error code. */
  groupCreate(uid: string, name: string, now = Date.now()): number | string {
    name = name.trim().replace(/\s+/g, ' ').slice(0, 24);
    if (name.length < 2) return 'bad_name';
    if (this.groupsOf(uid).length >= Store.GROUPS_EACH) return 'too_many_groups';
    const ALPHA = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    let code = '';
    do { code = 'G' + Array.from({ length: 5 }, () => ALPHA[Math.floor(Math.random() * ALPHA.length)]).join(''); } while (this.db.prepare('SELECT 1 FROM groups WHERE code = ?').get(code));
    const r = this.db.prepare('INSERT INTO groups (name, code, owner, created) VALUES (?, ?, ?, ?)').run(name, code, uid, now);
    const id = Number(r.lastInsertRowid);
    this.db.prepare('INSERT INTO group_members (group_id, user_id, joined) VALUES (?, ?, ?)').run(id, uid, now);
    return id;
  }
  /** Join by the group's code. Returns its id, or an error code. */
  groupJoin(uid: string, code: string, now = Date.now()): number | string {
    const g = this.db.prepare('SELECT * FROM groups WHERE code = ?').get(code.trim().toUpperCase().replace(/[^A-Z0-9]/g, '')) as { id: number } | undefined;
    if (!g) return 'group_not_found';
    const members = this.groupMembers(g.id);
    if (members.includes(uid)) return g.id;
    if (members.length >= Store.GROUP_MAX) return 'group_full';
    if (this.groupsOf(uid).length >= Store.GROUPS_EACH) return 'too_many_groups';
    this.db.prepare('INSERT INTO group_members (group_id, user_id, joined) VALUES (?, ?, ?)').run(g.id, uid, now);
    return g.id;
  }
  /** Leave a group (the last one out closes it; the owner leaving hands it to the longest member). */
  groupLeave(uid: string, id: number) {
    this.db.prepare('DELETE FROM group_members WHERE group_id = ? AND user_id = ?').run(id, uid);
    const left = this.groupMembers(id);
    if (!left.length) this.db.prepare('DELETE FROM groups WHERE id = ?').run(id);
    else if (this.groupGet(id)?.owner === uid) this.db.prepare('UPDATE groups SET owner = ? WHERE id = ?').run(left[0], id);
  }
  /** The group's members with their points this week (the global weekly points: 3 a win, 1 a game). */
  groupWeekly(id: number, week = weekKey()) {
    return this.db.prepare(`SELECT u.id, u.pid, u.name, u.ch, COALESCE(w.points, 0) AS points, COALESCE(w.games, 0) AS games, COALESCE(w.wins, 0) AS wins
      FROM group_members m JOIN users u ON u.id = m.user_id LEFT JOIN weekly w ON w.user_id = m.user_id AND w.week = ?
      WHERE m.group_id = ? ORDER BY points DESC, wins DESC, u.name`).all(week, id) as { id: string; pid: string; name: string; ch: number; points: number; games: number; wins: number }[];
  }

  /* ---------- titles ---------- */

  /** The titles a player has now (expired ones left out). */
  titlesOf(uid: string, now = Date.now()): { id: string; until: number; note: string | null }[] {
    return (this.db.prepare('SELECT title AS id, until, note FROM user_titles WHERE user_id = ? AND (until = 0 OR until > ?) ORDER BY ts DESC').all(uid, now) as { id: string; until: number; note: string | null }[]);
  }
  /** The title a player wears (null if none, or it ran out). */
  wornTitle(uid: string, now = Date.now()): string | null {
    const r = this.db.prepare('SELECT u.title FROM users u JOIN user_titles t ON t.user_id = u.id AND t.title = u.title WHERE u.id = ? AND (t.until = 0 OR t.until > ?)').get(uid, now) as { title: string } | undefined;
    return r?.title ?? null;
  }
  /** Wear one of your titles (null: none). */
  wearTitle(uid: string, id: string | null): boolean {
    if (id && !this.titlesOf(uid).some(t => t.id === id)) return false;
    this.db.prepare('UPDATE users SET title = ? WHERE id = ?').run(id, uid);
    return true;
  }
  /** Gives a title (until a date, or 0 for good); a new one is worn at once. */
  grantTitle(uid: string, id: string, until = 0, note: string | null = null, wear = true) {
    this.db.prepare(`INSERT INTO user_titles (user_id, title, ts, until, note) VALUES (?, ?, ?, ?, ?)
      ON CONFLICT(user_id, title) DO UPDATE SET ts = excluded.ts, until = excluded.until, note = excluded.note`).run(uid, id, Date.now(), until, note);
    if (wear) this.db.prepare('UPDATE users SET title = ? WHERE id = ?').run(id, uid);
  }
  revokeTitle(uid: string, id: string) {
    this.db.prepare('DELETE FROM user_titles WHERE user_id = ? AND title = ?').run(uid, id);
    this.db.prepare('UPDATE users SET title = NULL WHERE id = ? AND title = ?').run(uid, id);
  }
  /** A title taken out of the catalog: nobody has it any more. */
  dropTitle(id: string) { this.db.prepare('DELETE FROM user_titles WHERE title = ?').run(id); this.db.prepare('UPDATE users SET title = NULL WHERE title = ?').run(id); }
  /** How many players have each title now. */
  titleCounts(now = Date.now()): Record<string, number> {
    const out: Record<string, number> = {};
    for (const r of this.db.prepare('SELECT title, COUNT(*) AS n FROM user_titles WHERE until = 0 OR until > ? GROUP BY title').all(now) as { title: string; n: number }[]) out[r.title] = Number(r.n);
    return out;
  }
  titleHolders(id: string, limit = 100, now = Date.now()) {
    return this.db.prepare(`SELECT t.user_id AS id, u.name, u.pid, t.ts, t.until, t.note FROM user_titles t JOIN users u ON u.id = t.user_id
      WHERE t.title = ? AND (t.until = 0 OR t.until > ?) ORDER BY t.ts DESC LIMIT ?`).all(id, now, limit) as { id: string; name: string; pid: string; ts: number; until: number; note: string | null }[];
  }

  /** A finished online game counts for the month's standings in its game: 3 points a win, 1 for playing. */
  addMonthly(uid: string, game: string, won: boolean, month: string) {
    this.db.prepare(`INSERT INTO monthly (month, game, user_id, points, games, wins) VALUES (?, ?, ?, ?, 1, ?)
      ON CONFLICT(month, game, user_id) DO UPDATE SET points = points + excluded.points, games = games + 1, wins = wins + excluded.wins`)
      .run(month, game, uid, won ? 3 : 1, won ? 1 : 0);
  }
  /** The month's standings in one game (players with at least `minGames` games). */
  monthlyBoard(game: string, month: string, limit = 10, minGames = 0) {
    return this.db.prepare(`SELECT m.user_id AS id, u.name, u.pid, u.ch, m.points, m.games, m.wins FROM monthly m JOIN users u ON u.id = m.user_id
      WHERE m.month = ? AND m.game = ? AND m.games >= ? AND u.banned = 0 ORDER BY m.points DESC, m.wins DESC, m.games ASC LIMIT ?`)
      .all(month, game, minGames, limit) as { id: string; name: string; pid: string; ch: number; points: number; games: number; wins: number }[];
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
  phone: u.phone ? maskPhone(u.phone) : null, google: u.google_sub ? (u.google_email || 'Google') : null, apple: u.apple_sub ? (u.apple_email || 'Apple') : null, playGames: !!u.pg_id, code: u.friend_code ?? '', pid: u.pid ?? publicId(u.id) });

/** Referrals: the bonus each side gets, how long a new player has to enter a code, and the most one inviter is paid for. */
export const REFER_BONUS = 50_000, REFER_DAYS = 7, REFER_MAX = 20;

export interface PrizeDb { id: number; user_id: string; month: string; game: string; egp: number; status: PrizeRow['status']; phone: string | null; ts: number; claim_ts: number | null; done_ts: number | null; note: string | null }
const toPrize = (p: PrizeDb): PrizeRow => ({ id: p.id, month: p.month, game: p.game, egp: p.egp, status: p.status, phone: p.phone, ts: p.ts, doneTs: p.done_ts, note: p.note });
