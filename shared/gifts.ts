/** Gifts players can send each other at the table, paid with coins earned by playing. */
export const GIFTS = [
  { k: 'tea', price: 10, icon: '🍵' },
  { k: 'coffee', price: 20, icon: '☕' },
  { k: 'rose', price: 30, icon: '🌹' },
  { k: 'sweets', price: 40, icon: '🍬' },
  { k: 'crown', price: 150, icon: '👑' },
  { k: 'trophy', price: 300, icon: '🏆' },
  { k: 'diamond', price: 500, icon: '💎' },
] as const;
export type GiftKind = typeof GIFTS[number]['k'];
export const giftByKind = (k: string) => GIFTS.find(g => g.k === k);

export const ECONOMY = {
  startCoins: 500,
  dailyBonus: 100,
  matchWin: 50,
  matchPlayed: 10,
};
