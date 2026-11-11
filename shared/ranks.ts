// The leaderboard: one ranking for the whole game (every game together, online games only: 3 points a win,
// 1 for playing), all time or this month, over everyone, one country or one governorate. On the first day of
// each month the top of last month's ranking gets a title for 30 days: the player of the month, the champion of
// each country, and the champion of each governorate.

export const RANK_PERIODS = ['all', 'month'] as const;
export type RankPeriod = typeof RANK_PERIODS[number];
export const RANK_SCOPES = ['all', 'country', 'gov'] as const;
export type RankScope = typeof RANK_SCOPES[number];

/** The monthly titles, by ranking: whoever tops that ranking for a month wears it the next 30 days. */
export const RANK_TITLES: Record<RankScope, string> = { all: 'potm_all', country: 'champ_country', gov: 'champ_gov' };
/** How long a monthly title lasts. */
export const RANK_TITLE_DAYS = 30;

/** Countries a player can pick (the game's players are mostly Arab). Any other ISO code from a phone is kept too. */
export const COUNTRIES: { id: string; ar: string; en: string; dial: string }[] = [
  { id: 'EG', ar: 'مصر', en: 'Egypt', dial: '+20' }, { id: 'SA', ar: 'السعودية', en: 'Saudi Arabia', dial: '+966' },
  { id: 'AE', ar: 'الإمارات', en: 'UAE', dial: '+971' }, { id: 'KW', ar: 'الكويت', en: 'Kuwait', dial: '+965' },
  { id: 'QA', ar: 'قطر', en: 'Qatar', dial: '+974' }, { id: 'BH', ar: 'البحرين', en: 'Bahrain', dial: '+973' },
  { id: 'OM', ar: 'عمان', en: 'Oman', dial: '+968' }, { id: 'JO', ar: 'الأردن', en: 'Jordan', dial: '+962' },
  { id: 'LB', ar: 'لبنان', en: 'Lebanon', dial: '+961' }, { id: 'SY', ar: 'سوريا', en: 'Syria', dial: '+963' },
  { id: 'IQ', ar: 'العراق', en: 'Iraq', dial: '+964' }, { id: 'PS', ar: 'فلسطين', en: 'Palestine', dial: '+970' },
  { id: 'LY', ar: 'ليبيا', en: 'Libya', dial: '+218' }, { id: 'TN', ar: 'تونس', en: 'Tunisia', dial: '+216' },
  { id: 'DZ', ar: 'الجزائر', en: 'Algeria', dial: '+213' }, { id: 'MA', ar: 'المغرب', en: 'Morocco', dial: '+212' },
  { id: 'SD', ar: 'السودان', en: 'Sudan', dial: '+249' }, { id: 'YE', ar: 'اليمن', en: 'Yemen', dial: '+967' },
];
/** A country code: two capital letters. */
export const isCountry = (c: unknown): c is string => typeof c === 'string' && /^[A-Z]{2}$/.test(c);
/** The country of a phone number in +<code><number> form, if it is one of ours. */
export function countryOfPhone(phone: string | null | undefined): string {
  if (!phone) return '';
  const c = [...COUNTRIES].sort((a, b) => b.dial.length - a.dial.length).find(x => phone.startsWith(x.dial));
  return c?.id ?? '';
}
