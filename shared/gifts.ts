/** Gifts players can send each other at the table, paid with coins earned by playing. */
export const GIFTS = [
  { k: 'tea', price: 1_000, icon: '🍵' },
  { k: 'coffee', price: 2_000, icon: '☕' },
  { k: 'rose', price: 3_000, icon: '🌹' },
  { k: 'sweets', price: 4_000, icon: '🍬' },
  { k: 'crown', price: 15_000, icon: '👑' },
  { k: 'trophy', price: 30_000, icon: '🏆' },
  { k: 'diamond', price: 50_000, icon: '💎' },
] as const;
export type GiftKind = typeof GIFTS[number]['k'];
export const giftByKind = (k: string) => GIFTS.find(g => g.k === k);

export const ECONOMY = {
  startCoins: 100_000,
  dailyBonus: 10_000,
  adReward: 5_000,      // coins for watching a rewarded video ad
  adsPerDay: 10,
};

/** Coins each player puts on the table for one match (online games). */
export const STAKES = [5_000, 20_000, 50_000, 100_000, 250_000, 500_000, 1_000_000] as const;
/** The game keeps this share of what the winners take from the losers. */
export const HOUSE_FEE = 0.1;
export const isStake = (n: unknown): n is number => STAKES.includes(n as typeof STAKES[number]);

/**
 * What a winning player gets on top of their own stake back: the losers' stakes shared among the winners,
 * less the 10% fee. 1 vs 1 at 20K: the winner gets 20K back + 18K (2K fee); the loser loses 20K.
 */
export function winnings(stake: number, winners: number, losers: number) {
  const gross = Math.floor(stake * losers / Math.max(1, winners));
  const fee = Math.floor(gross * HOUSE_FEE);
  return { gross, fee, net: gross - fee };
}

/** 5K, 250K, 1M */
export function shortCoins(n: number) {
  const a = Math.abs(n), sign = n < 0 ? '-' : '';
  if (a >= 1_000_000) return sign + +(a / 1_000_000).toFixed(a % 1_000_000 ? 1 : 0) + 'M';
  if (a >= 1_000) return sign + +(a / 1_000).toFixed(a % 1_000 && a < 10_000 ? 1 : 0) + 'K';
  return sign + a;
}

/* ---------------- the shop: coin packs and the ad-free plan ----------------
 * Sold through Google Play Billing (App Store later). The product IDs must match the ones created in the
 * Play Console; the EGP prices here are only shown when the store can't be reached (the store's own,
 * localized price is shown otherwise). Coins can't be turned back into money. */
export interface CoinPack { id: string; coins: number; egp: number }
export const COIN_PACKS: CoinPack[] = [
  { id: 'coins_50k', coins: 50_000, egp: 200 },
  { id: 'coins_100k', coins: 100_000, egp: 379 },
  { id: 'coins_500k', coins: 500_000, egp: 599 },
  { id: 'coins_1m', coins: 1_000_000, egp: 899 },
];
/** Monthly subscription: no full-screen ads (the optional "watch an ad for coins" button stays). */
export const NO_ADS = { id: 'no_ads_monthly', egp: 199, days: 30 };
export const packById = (id: string) => COIN_PACKS.find(p => p.id === id);
