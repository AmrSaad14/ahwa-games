// The game's live settings, changed from the dashboard (/admin) without a restart: the monthly prizes (Vodafone Cash),
// the weekly tournament, the season pass, the economy (starting coins, daily streak, ads, weekly prizes), how
// play runs (computer players, turn time), maintenance mode and a notice shown to every player.
// They are kept in the database (meta 'settings') and applied to the running game at once.
import { ECONOMY, STREAK, WEEKLY_PRIZES } from '../shared/gifts';
import { SEASON_PASS, TIERS, SEASON_TIERS, type Reward } from '../shared/season';
import { skinById } from '../shared/skins';
import { TOURNEY } from './tourney';
import { EVENTS, cleanEvent, type EventDef } from '../shared/events';

export type TourneyGame = 'rotate' | 'domino' | 'tawla' | 'chess' | 'ludo';

export interface GameSettings {
  /** Monthly prizes: the best player of each game (the "player of the month") wins money on Vodafone Cash. */
  prizes: {
    enabled: boolean;
    egp: number;              // EGP for each game's winner (500)
    claimDays: number;        // days the winner has to give their Vodafone Cash number
    prefixes: string[];       // accepted mobile prefixes (Vodafone: 010)
  };
  tourney: { enabled: boolean; weekday: number; hour: number; game: TourneyGame; prizes: number[]; played: number };
  season: { enabled: boolean; passCoins: number; passEgp: number; tiers: { free: Reward; prem: Reward }[] };
  economy: { startCoins: number; adReward: number; adsPerDay: number; streak: number[]; weeklyPrizes: number[]; tutorialCoins: number; comebackCoins: number; comebackDays: number };
  play: { botFillSeconds: number; turnSeconds: number };
  maintenance: { on: boolean; message: string };
  notice: { text: string; until: number };
  /** "<game> player of the month": given by itself when a month ends, to the best of each game with enough games. */
  titles: { monthly: boolean; minGames: number };
  /** Ramadan, Eid and other events, with their dates (see shared/events.ts). */
  events: EventDef[];
}

/** The settings the game ships with (read once, before anything is changed). */
function defaults(): GameSettings {
  return {
    prizes: { enabled: true, egp: 500, claimDays: 30, prefixes: ['010'] },
    tourney: { enabled: true, weekday: TOURNEY.weekday, hour: TOURNEY.hour, game: 'rotate', prizes: [...TOURNEY.prizes], played: TOURNEY.played },
    season: { enabled: true, passCoins: SEASON_PASS.coins, passEgp: SEASON_PASS.egp, tiers: TIERS.map(t => ({ free: { ...t.free }, prem: { ...t.prem } })) },
    economy: { startCoins: ECONOMY.startCoins, adReward: ECONOMY.adReward, adsPerDay: ECONOMY.adsPerDay, tutorialCoins: ECONOMY.tutorialCoins, comebackCoins: ECONOMY.comebackCoins, comebackDays: ECONOMY.comebackDays, streak: [...STREAK], weeklyPrizes: [...WEEKLY_PRIZES] },
    play: { botFillSeconds: 8, turnSeconds: 10 },
    maintenance: { on: false, message: 'السيرفر في صيانة دلوقتي، ارجع كمان شوية · Maintenance, back soon' },
    notice: { text: '', until: 0 },
    titles: { monthly: true, minGames: 10 },
    events: [],
  };
}
const SHIPPED = defaults();
const SHIPPED_TITLES = { ...SHIPPED.titles };

const int = (v: unknown, lo: number, hi: number, d: number) => { const n = Math.round(Number(v)); return Number.isFinite(n) ? Math.min(hi, Math.max(lo, n)) : d; };
const ints = (v: unknown, lo: number, hi: number, d: number[], maxLen = 64) => Array.isArray(v) && v.length ? v.slice(0, maxLen).map((x, i) => int(x, lo, hi, d[i] ?? d[d.length - 1] ?? 0)) : d;
const text = (v: unknown, max: number, d: string) => typeof v === 'string' ? v.trim().slice(0, max) : d;
const bool = (v: unknown, d: boolean) => typeof v === 'boolean' ? v : d;
function reward(v: unknown, d: Reward): Reward {
  const r = v as { coins?: unknown; skin?: unknown };
  if (r && typeof r.skin === 'string' && skinById(r.skin)) return { skin: r.skin };
  if (r && r.coins !== undefined) return { coins: int(r.coins, 0, 100_000_000, 'coins' in d ? d.coins : 0) };
  return d;
}

/** Checks and fixes settings from the dashboard (anything missing or out of range keeps its current value). */
export function sanitize(input: unknown, cur: GameSettings): GameSettings {
  const i = (input ?? {}) as Partial<Record<keyof GameSettings, Record<string, unknown>>>;
  const c = i.prizes ?? {}, t = i.tourney ?? {}, s = i.season ?? {}, e = i.economy ?? {}, p = i.play ?? {}, m = i.maintenance ?? {}, n = i.notice ?? {}, ti = i.titles ?? {};
  const curTi = cur.titles ?? SHIPPED_TITLES;
  const games: TourneyGame[] = ['rotate', 'domino', 'tawla', 'chess', 'ludo'];
  const tiers = Array.isArray(s.tiers) ? s.tiers : cur.season.tiers;
  return {
    prizes: {
      enabled: bool(c.enabled, cur.prizes.enabled),
      egp: int(c.egp, 0, 1_000_000, cur.prizes.egp),
      claimDays: int(c.claimDays, 1, 365, cur.prizes.claimDays),
      prefixes: Array.isArray(c.prefixes) ? c.prefixes.map(x => String(x).replace(/\D/g, '')).filter(x => /^01\d$/.test(x)).slice(0, 4) : cur.prizes.prefixes,
    },
    tourney: {
      enabled: bool(t.enabled, cur.tourney.enabled),
      weekday: int(t.weekday, 0, 6, cur.tourney.weekday),
      hour: int(t.hour, 0, 23, cur.tourney.hour),
      game: games.includes(t.game as TourneyGame) ? t.game as TourneyGame : cur.tourney.game,
      prizes: ints(t.prizes, 0, 100_000_000, cur.tourney.prizes, 8),
      played: int(t.played, 0, 10_000_000, cur.tourney.played),
    },
    season: {
      enabled: bool(s.enabled, cur.season.enabled ?? true),
      passCoins: int(s.passCoins, 0, 100_000_000, cur.season.passCoins),
      passEgp: int(s.passEgp, 1, 100_000, cur.season.passEgp),
      tiers: Array.from({ length: SEASON_TIERS }, (_, k) => {
        const was = cur.season.tiers[k] ?? SHIPPED.season.tiers[k], x = tiers[k] as { free?: unknown; prem?: unknown } | undefined;
        const prem = reward(x?.prem, was.prem);
        return { free: reward(x?.free, was.free), prem: 'skin' in prem ? prem : was.prem };   // premium: skins only
      }),
    },
    economy: {
      startCoins: int(e.startCoins, 0, 100_000_000, cur.economy.startCoins),
      adReward: int(e.adReward, 0, 10_000_000, cur.economy.adReward),
      adsPerDay: int(e.adsPerDay, 0, 100, cur.economy.adsPerDay),
      tutorialCoins: int(e.tutorialCoins, 0, 10_000_000, cur.economy.tutorialCoins ?? SHIPPED.economy.tutorialCoins),
      comebackCoins: int(e.comebackCoins, 0, 10_000_000, cur.economy.comebackCoins ?? SHIPPED.economy.comebackCoins),
      comebackDays: int(e.comebackDays, 0, 60, cur.economy.comebackDays ?? SHIPPED.economy.comebackDays),
      streak: ints(e.streak, 0, 100_000_000, cur.economy.streak, 7),
      weeklyPrizes: ints(e.weeklyPrizes, 0, 100_000_000, cur.economy.weeklyPrizes, 10),
    },
    play: { botFillSeconds: int(p.botFillSeconds, 2, 120, cur.play.botFillSeconds), turnSeconds: int(p.turnSeconds, 5, 60, cur.play.turnSeconds) },
    maintenance: { on: bool(m.on, cur.maintenance.on), message: text(m.message, 300, cur.maintenance.message) },
    notice: { text: text(n.text, 400, cur.notice.text), until: int(n.until, 0, 4_102_444_800_000, cur.notice.until) },
    titles: { monthly: bool(ti.monthly, curTi.monthly), minGames: int(ti.minGames, 0, 10_000, curTi.minGames) },
    events: Array.isArray((input as { events?: unknown })?.events) ? ((input as { events: unknown[] }).events.map(e => cleanEvent(e)).filter((e): e is EventDef => !!e).slice(0, 20)) : (cur.events ?? []),
  };
}

/** Puts settings into the running game: the shared tables the rest of the server reads. */
function apply(s: GameSettings) {
  Object.assign(TOURNEY, { weekday: s.tourney.weekday, hour: s.tourney.hour, prizes: [...s.tourney.prizes], played: s.tourney.played, enabled: s.tourney.enabled, game: s.tourney.game });
  SEASON_PASS.coins = s.season.passCoins; SEASON_PASS.egp = s.season.passEgp; SEASON_PASS.enabled = s.season.enabled;
  s.season.tiers.forEach((t, k) => { if (TIERS[k]) { TIERS[k].free = { ...t.free }; TIERS[k].prem = { ...t.prem }; } });
  ECONOMY.startCoins = s.economy.startCoins; ECONOMY.adReward = s.economy.adReward; ECONOMY.adsPerDay = s.economy.adsPerDay;
  ECONOMY.tutorialCoins = s.economy.tutorialCoins; ECONOMY.comebackCoins = s.economy.comebackCoins; ECONOMY.comebackDays = s.economy.comebackDays;
  STREAK.splice(0, STREAK.length, ...s.economy.streak);
  WEEKLY_PRIZES.splice(0, WEEKLY_PRIZES.length, ...s.economy.weeklyPrizes);
  EVENTS.splice(0, EVENTS.length, ...s.events);
}

export interface SettingsStore { metaGet(k: string): string | null; metaSet(k: string, v: string): void }

export class Settings {
  value: GameSettings;
  private listeners: ((s: GameSettings, before: GameSettings) => void)[] = [];
  /** Whether the settings were ever changed from the dashboard (else the server's own config rules play). */
  readonly saved: boolean;
  constructor(private store: SettingsStore, play?: GameSettings['play']) {
    if (play) SHIPPED.play = { ...play };
    let saved: unknown = null;
    try { saved = JSON.parse(store.metaGet('settings') || 'null'); } catch { /* none */ }
    this.saved = !!saved;
    this.value = saved ? sanitize(saved, SHIPPED) : structuredClone(SHIPPED);
    apply(this.value);
  }
  onChange(f: (s: GameSettings, before: GameSettings) => void) { this.listeners.push(f); }
  /** Saves new settings (checked first) and applies them at once; returns what was saved. */
  update(input: unknown): GameSettings {
    const before = this.value;
    const merged = sanitize(deepMerge(before, input), before);
    this.value = merged;
    this.store.metaSet('settings', JSON.stringify(merged));
    apply(merged);
    for (const f of this.listeners) { try { f(merged, before); } catch (e) { console.error('[settings]', (e as Error).message); } }
    return merged;
  }
  /** Back to the settings the game shipped with. */
  reset(): GameSettings { return this.update(structuredClone(SHIPPED)); }
}

function deepMerge(a: unknown, b: unknown): unknown {
  if (Array.isArray(b) || typeof b !== 'object' || b === null) return b === undefined ? a : b;
  const out: Record<string, unknown> = { ...(a as Record<string, unknown>) };
  for (const [k, v] of Object.entries(b)) out[k] = deepMerge(out[k], v);
  return out;
}
