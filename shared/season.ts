// The season pass: a month-long track of 30 tiers. Every game, mission and daily reward earns season points
// (SP); every 100 SP opens the next tier. Each tier has a free reward and a premium one; the premium track is
// opened for the season with coins or a Google Play purchase. Seasons follow the calendar month in Cairo.
import { cairoDay } from './gifts';

export const SEASON_TIERS = 30;
export const SP_PER_TIER = 100;
/** Season points: a finished online game, a practice game, a mission, the daily reward. */
export const SP = { win: 40, loss: 15, practiceWin: 10, practiceLoss: 4, daily: 20 };
/** The premium track: with coins, or bought on Google Play (a consumable product, once per season). */
export const SEASON_PASS = { id: 'season_pass', coins: 750_000, egp: 149 };

export type Reward = { coins: number } | { skin: string };
export interface Tier { free: Reward; prem: Reward }

const coinTier = (n: number, prem: boolean): Reward => ({ coins: prem ? 15_000 + 3_000 * n : 5_000 + 1_000 * n });
const FREE_SKINS: Record<number, string> = { 10: 'hat_fez_white', 20: 'drink_sahlab' };
const PREM_SKINS: Record<number, string> = { 5: 'drink_qasab', 10: 'felt_royal', 15: 'hat_fez_gold', 20: 'drink_karkade', 25: 'dice_gold', 30: 'felt_kilim' };
/** Tier 1‥30 (index 0‥29). */
export const TIERS: Tier[] = Array.from({ length: SEASON_TIERS }, (_, i) => {
  const n = i + 1;
  return {
    free: FREE_SKINS[n] ? { skin: FREE_SKINS[n] } : n === SEASON_TIERS ? { coins: 200_000 } : coinTier(n, false),
    prem: PREM_SKINS[n] ? { skin: PREM_SKINS[n] } : coinTier(n, true),
  };
});

/** The season a moment falls in: "2026-11". */
export const seasonOf = (ts: number) => cairoDay(ts).slice(0, 7);
/** When a season ends: the first moment of the next month in Cairo. */
export function seasonEndsAt(ts: number): number {
  const s = seasonOf(ts);
  let lo = ts, hi = ts + 32 * 86_400_000;
  while (hi - lo > 1000) { const mid = Math.floor((lo + hi) / 2); if (seasonOf(mid) === s) lo = mid; else hi = mid; }
  return Math.floor(hi / 1000) * 1000;
}
/** Tiers reached with this many season points. */
export const tierOf = (sp: number) => Math.min(SEASON_TIERS, Math.floor(sp / SP_PER_TIER));
