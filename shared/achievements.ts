// Achievement badges on the player's profile. Each is worked out from the player's numbers (no extra
// bookkeeping): `of` names the number, `need` how much of it unlocks the badge.
export interface Achievement { id: string; ar: string; en: string; of: string; need: number; icon: string }
export const ACHIEVEMENTS: Achievement[] = [
  { id: 'first', ar: 'أول فوز', en: 'First win', of: 'wins', need: 1, icon: 'star' },
  { id: 'wins10', ar: '١٠ انتصارات', en: '10 wins', of: 'wins', need: 10, icon: 'star' },
  { id: 'wins100', ar: '١٠٠ انتصار', en: '100 wins', of: 'wins', need: 100, icon: 'trophy' },
  { id: 'wins1000', ar: 'أسطورة القهوة: ١٠٠٠ انتصار', en: 'Café legend: 1,000 wins', of: 'wins', need: 1000, icon: 'crown' },
  { id: 'closer', ar: 'قفّال: قفلت ١٠٠ ماتش دومينو', en: 'Closer: won 100 domino matches', of: 'domino.wins', need: 100, icon: 'domino' },
  { id: 'tawla10', ar: '١٠ طاولات ورا بعض', en: '10 tawla wins in a row', of: 'tawla.best', need: 10, icon: 'fire' },
  { id: 'domino10', ar: '١٠ دومينو ورا بعض', en: '10 domino wins in a row', of: 'domino.best', need: 10, icon: 'fire' },
  { id: 'chess50', ar: 'ملك الشطرنج: ٥٠ فوز', en: 'Chess king: 50 wins', of: 'chess.wins', need: 50, icon: 'crown' },
  { id: 'ludo50', ar: 'سلطان اللودو: ٥٠ فوز', en: 'Ludo sultan: 50 wins', of: 'ludo.wins', need: 50, icon: 'star' },
  { id: 'allgames', ar: 'بتاع كله: فزت في الأربع ألعاب', en: 'All-rounder: a win in all four games', of: 'gamesWon', need: 4, icon: 'star' },
  { id: 'regular', ar: 'ابن القهوة: ٣٠ يوم ورا بعض', en: 'Regular: 30 days in a row', of: 'streak', need: 30, icon: 'calendar' },
  { id: 'games500', ar: '٥٠٠ ماتش', en: '500 matches', of: 'games', need: 500, icon: 'cup' },
  { id: 'friends10', ar: 'اجتماعي: ١٠ صحاب', en: 'Social: 10 friends', of: 'friends', need: 10, icon: 'friends' },
  { id: 'rich', ar: 'مليونير: كسبت مليون كوين', en: 'Millionaire: won 1M coins', of: 'coinsWon', need: 1_000_000, icon: 'coins' },
];

/** Every badge with how far the player is: have (capped at need) and done. */
export function achievementsOf(nums: Record<string, number>) {
  return ACHIEVEMENTS.map(a => { const have = Math.max(0, nums[a.of] ?? 0); return { id: a.id, ar: a.ar, en: a.en, icon: a.icon, need: a.need, have: Math.min(have, a.need), done: have >= a.need }; });
}
