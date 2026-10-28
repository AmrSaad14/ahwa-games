// The season pass: a month-long track of 30 tiers. Every game, mission and daily reward earns season points
// (SP); every 100 SP opens the next tier. Each tier has a free reward and a premium one; the premium track is
// opened for the season with coins or a Google Play purchase. Seasons follow the calendar month in Cairo.
import { cairoDay } from './gifts';

export const SEASON_TIERS = 30;
export const SP_PER_TIER = 100;
/** Season points: a finished online game, a practice game, a mission, the daily reward. */
export const SP = { win: 40, loss: 15, practiceWin: 10, practiceLoss: 4, daily: 20 };
/** The premium track: with coins, or bought on Google Play (a consumable product, once per season). Premium
 * also means no full-screen ads until the season ends. */
export const SEASON_PASS = { id: 'season_pass', coins: 750_000, egp: 299 };

export type Reward = { coins: number } | { skin: string };
export interface Tier { free: Reward; prem: Reward }

const coinTier = (n: number): Reward => ({ coins: 5_000 + 1_000 * n });
const FREE_SKINS: Record<number, string> = { 10: 'hat_fez_white', 20: 'drink_sahlab' };
// The premium track is skins only (no coins: coins can be exchanged for prizes, so the pass must not sell them).
// Every third tier is a skin only the season gives; the tiers between are shop skins, cheapest first.
const PREM_SKINS: Record<number, string> = {
  1: 'drink_mint', 2: 'drink_coffee', 3: 'dice_ruby', 4: 'hat_fez_red', 5: 'drink_karkade', 6: 'drink_qasab',
  7: 'tiles_ebony', 8: 'checkers_marble', 9: 'checkers_pharaoh', 10: 'felt_green', 11: 'felt_red', 12: 'felt_royal',
  13: 'hat_fez_black', 14: 'dice_obsidian', 15: 'dice_pharaoh', 16: 'tawla_olive', 17: 'chess_blue', 18: 'hat_fez_gold',
  19: 'felt_blue', 20: 'ludo_night', 21: 'tawla_pharaoh', 22: 'hat_fez_green', 23: 'checkers_amber', 24: 'pieces_onyx',
  25: 'pieces_porcelain', 26: 'chess_tournament', 27: 'felt_kilim', 28: 'dice_emerald', 29: 'hat_fez_navy', 30: 'chess_gold',
};
/** Tier 1‥30 (index 0‥29). */
export const TIERS: Tier[] = Array.from({ length: SEASON_TIERS }, (_, i) => {
  const n = i + 1;
  return {
    free: FREE_SKINS[n] ? { skin: FREE_SKINS[n] } : n === SEASON_TIERS ? { coins: 200_000 } : coinTier(n),
    prem: { skin: PREM_SKINS[n] },
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
